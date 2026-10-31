"""계층 의존 · 파일 순환 · fan-out 을 센다.

    py -I docs/dep.py FT.Core [출력파일]

구조는 느낌이 아니라 재서 고친다. 2026-10-31 이 도구로 계층 위반 4회와 순환 12쌍을 찾아
각각 0 과 3(전부 설명 가능)으로 줄였다 — README "결합도를 재고 고친 기록" 참고.

세는 규칙:
- 네임스페이스 바로 아래(들여쓰기 4칸)의 타입만 센다. 중첩 타입은 이름이 겹친다
  (Kind · Verdict · Slot · Goal) — 그대로 세면 없는 의존이 보인다.
- 점 뒤의 이름은 멤버 접근이다(TrainingCategory.SetPieces). 타입 참조가 아니다.
- 주석과 문자열은 센 뒤가 아니라 세기 전에 지운다.
"""
import io, os, re, sys, collections

root = sys.argv[1] if len(sys.argv) > 1 else "FT.Core"
files = []
for dirpath, dirnames, filenames in os.walk(root):
    if os.sep + "obj" in dirpath or os.sep + "bin" in dirpath:
        continue
    for f in filenames:
        if f.endswith(".cs"):
            files.append(os.path.join(dirpath, f))

# 네임스페이스 바로 아래(들여쓰기 4칸)의 타입만 센다. 중첩 타입은 이름이 겹친다
# (Kind · Verdict · Slot · Goal 처럼) — 그대로 세면 없는 의존이 보인다.
decl = re.compile(
    r"^    (?:(?:sealed|static|abstract|partial|public|internal|readonly|record)\s+)*"
    r"(?:class|struct|enum|interface)\s+(\w+)", re.M)

owner = {}
text = {}
layer_of = {}

def layer(path):
    parts = path.replace("\\", "/").split("/")
    return parts[1] if len(parts) > 2 else "root"

for path in files:
    s = io.open(path, encoding="utf-8-sig", errors="replace").read()
    s = re.sub(r"///.*", "", s)
    s = re.sub(r"//.*", "", s)
    s = re.sub(r"/\*.*?\*/", "", s, flags=re.S)
    s = re.sub(r'"(?:[^"\\\n]|\\.)*"', '""', s)
    text[path] = s
    layer_of[path] = layer(path)
    for m in decl.finditer(s):
        owner.setdefault(m.group(1), path)

edges = collections.Counter()
for path, s in text.items():
    # 점 뒤의 이름은 멤버 접근이다(TrainingCategory.SetPieces). 타입 참조가 아니다.
    words = collections.Counter(re.findall(r"(?<![.\w])(\w+)", s))
    for name, count in words.items():
        dst = owner.get(name)
        if dst and dst != path:
            edges[(path, dst)] += count

out = []
def say(line=""):
    out.append(line)

mat = collections.Counter()
for (a, b), n in edges.items():
    mat[(layer_of[a], layer_of[b])] += n

layers = [l for l in ["Model", "Balance", "Diagnostics", "Simulation", "World"]
          if any(layer_of[p] == l for p in files)]

say("== 계층 의존 (행=쓰는 쪽, 열=쓰이는 쪽, 참조 횟수) ==")
say("%-13s" % "" + "".join("%13s" % l for l in layers))
for a in layers:
    say("%-13s" % a + "".join("%13s" % mat.get((a, b), 0) for b in layers))

allowed = set()
rank = {"Model": 0, "Balance": 1, "Diagnostics": 0, "Simulation": 2, "World": 3}
for a in layers:
    for b in layers:
        if rank[b] <= rank[a]:
            allowed.add((a, b))
allowed.discard(("Model", "Diagnostics"))      # 모델은 아무것도 몰라야 한다
allowed.discard(("Balance", "Diagnostics"))

say()
say("== 계층 규칙 위반 (아래 계층이 위 계층을 참조) ==")
bad = [(a, b, n) for (a, b), n in mat.items() if (a, b) not in allowed]
if not bad:
    say("  없음")
for a, b, n in sorted(bad, key=lambda x: -x[2]):
    say("  %s -> %s : %d" % (a, b, n))
    for (sa, sb), n2 in sorted(edges.items(), key=lambda x: -x[1]):
        if layer_of[sa] == a and layer_of[sb] == b:
            say("      %s -> %s (%d)" % (os.path.basename(sa), os.path.basename(sb), n2))

fanout = collections.Counter()
fanin = collections.Counter()
for (a, b), n in edges.items():
    fanout[a] += 1
    fanin[b] += 1

say()
say("== 나가는 의존이 많은 파일 (fan-out = 참조하는 파일 수) ==")
for path, n in fanout.most_common(12):
    say("  %-32s %2d  (들어오는 참조 %d)" % (os.path.basename(path), n, fanin.get(path, 0)))

say()
say("== 가장 많이 참조되는 파일 (fan-in) ==")
for path, n in fanin.most_common(10):
    say("  %-32s %2d" % (os.path.basename(path), n))

say()
say("== 순환 (파일 쌍) ==")
seen = set()
cycles = []
for (a, b) in edges:
    if (b, a) in edges and (b, a) not in seen:
        seen.add((a, b))
        cycles.append((a, b, edges[(a, b)], edges[(b, a)]))
for a, b, n1, n2 in sorted(cycles, key=lambda x: -(x[2] + x[3])):
    say("  %-26s <-> %-26s (%d / %d)" % (os.path.basename(a), os.path.basename(b), n1, n2))
say("  총 %d 쌍" % len(cycles))

io.open(sys.argv[2] if len(sys.argv) > 2 else "dep.txt", "w", encoding="utf-8").write("\n".join(out) + "\n")
print("\n".join(out))
