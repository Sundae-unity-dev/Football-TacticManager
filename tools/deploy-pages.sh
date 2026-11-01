#!/usr/bin/env bash
#
# 배포본을 GitHub Pages(gh-pages 브랜치)에 올린다.
#
#   사용법:  tools/deploy-pages.sh            # 새로 빌드해서 올린다 (AOT 때문에 20분쯤)
#            tools/deploy-pages.sh --no-build # publish/ 에 있는 것을 그대로 올린다
#
# Pages 는 저장소 이름이 붙은 주소로 서비스한다
# (https://<계정>.github.io/Football-TacticManager/). 그래서 세 가지를 손봐야 한다.
#
#   1. <base href>  — "/" 로 두면 _framework 를 도메인 뿌리에서 찾아 전부 404 가 난다.
#   2. .nojekyll    — Jekyll 이 밑줄로 시작하는 폴더를 숨긴다. Blazor 는 _framework 에 산다.
#   3. 404.html     — Pages 는 클라이언트 라우팅을 모른다. /squad 를 새로고침하면 404 다.
#                     index.html 을 그대로 두면 Blazor 가 주소를 보고 그 화면을 그린다.
#
# .br/.gz 는 올리지 않는다 — Pages 는 그 파일을 알아보지 못하고(내용 협상을 하지 않는다)
# 자기 방식으로 압축해서 보낸다. 올려 봐야 용량만 두 배가 된다.
set -euo pipefail

REPO_NAME=Football-TacticManager
BRANCH=gh-pages
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/publish/wwwroot"

cd "$ROOT"

if [ "${1:-}" != "--no-build" ]; then
    echo "== 배포본 빌드 (AOT 라 오래 걸린다) =="
    rm -rf publish
    dotnet publish FT.App -c Release -o publish
fi

[ -f "$OUT/index.html" ] || { echo "publish/wwwroot 가 없습니다. --no-build 를 뺐나요?"; exit 1; }

echo "== 올릴 것 준비 =="
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

# .br/.gz 를 뺀 나머지를 복사한다
(cd "$OUT" && find . -type f ! -name '*.br' ! -name '*.gz' -print0 \
    | while IFS= read -r -d '' f; do
          mkdir -p "$STAGE/$(dirname "$f")"
          cp "$OUT/$f" "$STAGE/$f"
      done)

# 1) base href
sed -i "s|<base href=\"/\" />|<base href=\"/$REPO_NAME/\" />|" "$STAGE/index.html"
grep -q "<base href=\"/$REPO_NAME/\" />" "$STAGE/index.html" \
    || { echo "base href 를 바꾸지 못했습니다 — index.html 모양이 달라졌나요?"; exit 1; }

# 2) Jekyll 끄기   3) 새로고침해도 같은 화면
touch "$STAGE/.nojekyll"
cp "$STAGE/index.html" "$STAGE/404.html"

# 4) 줄바꿈을 건드리지 못하게 막는다.
#
#    이걸 빼먹고 한 번 데였다. 저장소 뿌리의 .gitattributes 는 소스를 LF 로 맞추는데,
#    그 규칙이 배포본에도 걸려서 dotnet.js 가 CRLF → LF 로 310바이트 줄었다.
#    index.html 에 박힌 integrity 해시는 줄어들기 전 바이트로 계산된 값이라
#    브라우저가 받아 놓고 "해시가 다르다"며 버렸고, 화면은 "불러오는 중"에서 멈췄다.
#    빌드 산출물은 한 바이트도 바뀌면 안 된다.
printf '* -text\n' > "$STAGE/.gitattributes"

echo "   $(find "$STAGE" -type f | wc -l)개 파일 · $(du -sh "$STAGE" | cut -f1)"

echo "== $BRANCH 로 올리기 =="
# 이미 있으면 그 위에 쌓는다. 처음이면 뿌리 없는 가지로 시작한다.
# 어느 쪽이든 밀어내기(force)를 쓰지 않는다 — 배포 역사도 역사다.
git fetch -q origin "$BRANCH" 2>/dev/null && HAVE_BRANCH=1 || HAVE_BRANCH=0

WORK="$(mktemp -d)"
if [ "$HAVE_BRANCH" = 1 ]; then
    git worktree add -q "$WORK" -B "$BRANCH" "origin/$BRANCH"
else
    git worktree add -q --detach "$WORK"
fi
(
    cd "$WORK"
    [ "$HAVE_BRANCH" = 1 ] || git checkout -q --orphan "$BRANCH"
    git rm -rq --cached . 2>/dev/null || true
    find . -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +

    cp -r "$STAGE/." .
    # autocrlf 가 켜져 있어도 손대지 못하게 한 번 더 못을 박는다
    git -c core.autocrlf=false add -A
    git -c core.autocrlf=false commit -qm "배포본 $(date '+%Y-%m-%d %H:%M') — $(git -C "$ROOT" rev-parse --short HEAD)"
    git push -u origin "$BRANCH"
)
git worktree remove --force "$WORK"

echo "== 끝. https://$(git remote get-url origin | sed -E 's|.*github.com[:/]([^/]+)/.*|\1|' | tr 'A-Z' 'a-z').github.io/$REPO_NAME/ =="
