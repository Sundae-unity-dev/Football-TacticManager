# -*- coding: utf-8 -*-
"""
기획서 한 장 만들기 — README.md 와 docs/*.md 를 하나의 HTML 로 묶는다.

크롬 북마크로 열어 보려고 만든 것이다. 인터넷 없이도 열리고, 표와 차례가
그대로 나온다. 머메이드 그림만 CDN 이 있으면 그려지고, 없으면 글로 남는다.

    py tools/make-plan.py

결과는 docs/plan.html 이다(생성물이라 git 에는 올리지 않는다).
문서를 고친 뒤 다시 돌리면 그 자리에서 갱신된다 — 북마크는 그대로 쓴다.
"""
import io
import os
import re
import html
import datetime

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'docs', 'plan.html')

# 순서가 곧 차례다. 기획서가 맨 앞에 온다.
DOCS = [
    ('리그 제도', 'docs/리그-제도.md', '나라마다의 팀 수·승강·스플릿, 그리고 넣을 순서'),
    ('처음으로', 'README.md', '무슨 게임이고 어떻게 돌리나'),
    ('구조', 'docs/구조.md', '계층과 의존 방향'),
    ('밸런싱', 'docs/밸런싱.md', '20시즌을 돌려 수치를 맞추는 하네스'),
    ('성능', 'docs/성능.md', '어디가 느렸고 어떻게 쟀나'),
    ('배포', 'docs/배포.md', 'GitHub Pages · 태그 · 릴리스'),
    ('조심할 것', 'docs/조심할-것.md', '데여 본 것들'),
]

INLINE_HTML = r'(</?(?:b|i|em|strong|br|kbd|sub|sup)\s*/?>)'

# 코드를 잠시 치워 둘 자리표. 본문에 나올 리 없는 모양이면 된다.
CODE_MARK = '@@CODE%d@@'
CODE_FIND = r'@@CODE(\d+)@@'


def slug(s):
    return 'd-' + re.sub(r'[^0-9a-zA-Z가-힣]+', '-', s).strip('-').lower()


# ── 한 줄 안의 서식 ───────────────────────────────────────────────────
def inline(t):
    """
    코드를 자리표로 빼 두고 서식을 입힌 뒤 되돌린다.

    코드를 먼저 <code> 로 바꿔 버리면 **굵게 `코드` 굵게** 처럼 코드를 품은
    굵은 글씨가 깨진다 - 앞뒤의 ** 가 서로 다른 조각에 떨어져 짝을 못 찾는다.
    """
    codes = []

    def stash(m):
        codes.append(html.escape(m.group(1)))
        return CODE_MARK % (len(codes) - 1)

    t = _marks(re.sub(r'`([^`]+)`', stash, t))
    return re.sub(CODE_FIND,
                  lambda m: '<code>' + codes[int(m.group(1))] + '</code>',
                  t)


def _marks(t):
    # <b> <i> 같은 날 HTML 만 남기고 나머지는 escape 한다.
    parts = re.split(INLINE_HTML, t)
    t = ''.join(p if j % 2 else html.escape(p) for j, p in enumerate(parts))

    t = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', _link, t)
    t = re.sub(r'\*\*([^*]+)\*\*', r'<strong>\1</strong>', t)
    t = re.sub(r'(?<![\w*])\*([^*\n]+)\*(?![\w*])', r'<em>\1</em>', t)
    return t


def _link(m):
    text, href = m.group(1), m.group(2)

    # 문서끼리의 링크는 이 페이지 안으로 돌린다.
    base = href.split('#')[0].split('/')[-1]
    for title, path, _note in DOCS:
        if base and base == path.split('/')[-1]:
            return '<a href="#' + slug(title) + '">' + text + '</a>'

    if href.startswith('../'):
        href = href[3:]
    return '<a href="' + html.escape(href) + '" target="_blank" rel="noopener">' + text + '</a>'


# ── 블록 ──────────────────────────────────────────────────────────────
def render(md, doc_title, headings):
    lines = md.split('\n')
    n = len(lines)
    out = []
    i = 0

    while i < n:
        ln = lines[i]

        # 코드 울타리
        if ln.startswith('```'):
            lang = ln[3:].strip()
            i += 1
            body = []
            while i < n and not lines[i].startswith('```'):
                body.append(lines[i])
                i += 1
            i += 1
            code = html.escape('\n'.join(body))
            if lang == 'mermaid':
                out.append('<pre class="mermaid">' + code + '</pre>')
            else:
                out.append('<pre><code>' + code + '</code></pre>')
            continue

        # 날 HTML (<details> 묶음)
        if ln.lstrip().startswith(('<details', '</details', '<summary', '</summary')):
            out.append(ln)
            i += 1
            continue

        # 제목
        m = re.match(r'^(#{1,4})\s+(.*)$', ln)
        if m:
            level = len(m.group(1))
            text = m.group(2).strip()
            i += 1
            if level == 1:
                continue                        # 문서 제목은 바깥에서 붙인다
            sid = slug(doc_title + '-' + text)
            if level == 2:
                headings.append((sid, text))
            out.append('<h%d id="%s">%s</h%d>' % (level, sid, inline(text), level))
            continue

        # 표
        if ln.startswith('|'):
            rows = []
            while i < n and lines[i].startswith('|'):
                rows.append(lines[i])
                i += 1
            out.append(table(rows))
            continue

        # 인용
        if ln.startswith('>'):
            body = []
            while i < n and lines[i].startswith('>'):
                body.append(lines[i][1:].strip())
                i += 1
            out.append('<blockquote>' + inline(' '.join(body)) + '</blockquote>')
            continue

        # 목록
        if re.match(r'^\s*(?:[-*]|\d+\.)\s+', ln):
            ordered = bool(re.match(r'^\s*\d+\.\s+', ln))
            items = []
            while i < n:
                m2 = re.match(r'^\s*(?:[-*]|\d+\.)\s+(.*)$', lines[i])
                if m2:
                    items.append(m2.group(1))
                elif items and lines[i].startswith('  ') and lines[i].strip():
                    items[-1] += ' ' + lines[i].strip()     # 이어지는 줄
                else:
                    break
                i += 1
            tag = 'ol' if ordered else 'ul'
            out.append('<' + tag + '>'
                       + ''.join('<li>' + inline(x) + '</li>' for x in items)
                       + '</' + tag + '>')
            continue

        # 구분선
        if re.match(r'^-{3,}$', ln.strip()):
            out.append('<hr>')
            i += 1
            continue

        # 빈 줄
        if not ln.strip():
            i += 1
            continue

        # 문단
        body = []
        while i < n and lines[i].strip():
            s = lines[i]
            if s.startswith(('|', '>', '#', '```', '<')):
                break
            if re.match(r'^\s*(?:[-*]|\d+\.)\s+', s) or re.match(r'^-{3,}$', s.strip()):
                break
            body.append(s.strip())
            i += 1
        if body:
            out.append('<p>' + inline(' '.join(body)) + '</p>')
        else:
            i += 1

    return '\n'.join(out)


def table(rows):
    cells = [[c.strip() for c in r.strip().strip('|').split('|')] for r in rows]

    divider = len(cells) >= 2 and all(re.match(r'^:?-{2,}:?$', c)
                                      for c in cells[1] if c)
    head, body = (cells[0], cells[2:]) if divider else (None, cells)

    out = ['<table>']
    if head:
        out.append('<thead><tr>'
                   + ''.join('<th>' + inline(c) + '</th>' for c in head)
                   + '</tr></thead>')
    out.append('<tbody>')
    for r in body:
        out.append('<tr>' + ''.join('<td>' + inline(c) + '</td>' for c in r) + '</tr>')
    out.append('</tbody></table>')
    return ''.join(out)


# ── 페이지 ────────────────────────────────────────────────────────────
CSS = """
:root{--bg:#0f1216;--panel:#161b22;--line:#262d36;--ink:#dfe5ec;--dim:#8b97a6;
      --accent:#6ea8fe;--accent2:#7ee0a8;--code:#1b2129}
@media (prefers-color-scheme: light){
  :root{--bg:#fbfcfd;--panel:#fff;--line:#e3e8ee;--ink:#1d2530;--dim:#64717f;
        --accent:#1f6feb;--accent2:#1a7f4b;--code:#f3f5f8}}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--ink);
     font:16px/1.75 "Pretendard","Noto Sans KR",-apple-system,"Segoe UI",sans-serif}
a{color:var(--accent);text-decoration:none}
a:hover{text-decoration:underline}
#wrap{display:flex;align-items:flex-start;max-width:1280px;margin:0 auto}
nav{position:sticky;top:0;width:252px;flex:none;height:100vh;overflow-y:auto;
    padding:22px 14px 40px;border-right:1px solid var(--line)}
nav h1{font-size:15px;margin:0 0 4px;letter-spacing:-.2px}
nav .ver{font-size:12px;color:var(--dim);margin-bottom:18px}
nav .doc{display:block;padding:7px 10px;margin-top:4px;border-radius:7px;
         color:var(--ink);font-size:14px;font-weight:600}
nav .doc:hover{background:var(--panel);text-decoration:none}
nav .doc small{display:block;color:var(--dim);font-size:11.5px;line-height:1.45;
               font-weight:400}
nav .sub{display:block;padding:3px 10px 3px 22px;font-size:12.5px;color:var(--dim)}
nav .sub:hover{color:var(--accent);text-decoration:none}
main{flex:1;min-width:0;padding:30px 42px 140px}
section{margin-bottom:60px;scroll-margin-top:16px}
h2,h3,h4{scroll-margin-top:16px}
.doctitle{font-size:28px;margin:0 0 2px;letter-spacing:-.5px}
.docnote{color:var(--dim);font-size:13.5px;margin:0 0 26px}
h2{font-size:21px;margin:42px 0 12px;padding-top:14px;border-top:1px solid var(--line);
   letter-spacing:-.3px}
h3{font-size:17px;margin:28px 0 8px;color:var(--accent2)}
h4{font-size:15px;margin:20px 0 6px}
p{margin:12px 0}
code{background:var(--code);padding:1.5px 5px;border-radius:4px;font-size:13px;
     font-family:"Cascadia Code",Consolas,monospace}
pre{background:var(--code);border:1px solid var(--line);border-radius:9px;
    padding:14px 16px;overflow-x:auto;font-size:13px;line-height:1.6}
pre code{background:none;padding:0}
pre.mermaid{color:var(--dim);white-space:pre-wrap}
table{border-collapse:collapse;width:100%;margin:16px 0;font-size:14px;
      border:1px solid var(--line);border-radius:9px;overflow:hidden}
th,td{padding:8px 12px;text-align:left;border-bottom:1px solid var(--line);
      vertical-align:top}
th{background:var(--panel);font-weight:600;font-size:13px;color:var(--dim)}
tbody tr:last-child td{border-bottom:none}
blockquote{margin:16px 0;padding:10px 16px;border-left:3px solid var(--accent);
           background:var(--panel);border-radius:0 8px 8px 0;color:var(--dim)}
blockquote strong{color:var(--ink)}
ul,ol{padding-left:22px}
li{margin:5px 0}
hr{border:none;border-top:1px solid var(--line);margin:28px 0}
details{background:var(--panel);border:1px solid var(--line);border-radius:9px;
        padding:10px 14px;margin:10px 0}
summary{cursor:pointer;font-weight:600;font-size:14.5px}
details[open] summary{margin-bottom:8px;color:var(--accent)}
.top{display:flex;gap:8px;align-items:center;margin-bottom:14px;flex-wrap:wrap}
.pill{font-size:12px;padding:3px 10px;border-radius:999px;background:var(--panel);
      border:1px solid var(--line);color:var(--dim)}
@media (max-width:860px){nav{display:none}main{padding:20px}}
"""

JS = """
// 머메이드는 있으면 그리고, 없으면(오프라인) 글로 남는다 — 페이지는 멀쩡하다.
(function () {
  var s = document.createElement('script');
  s.src = 'https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js';
  s.onload = function () {
    try {
      mermaid.initialize({
        startOnLoad: true,
        theme: matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'default'
      });
    } catch (e) { /* 그림만 안 나온다 */ }
  };
  document.head.appendChild(s);
})();
"""


def version():
    try:
        proj = io.open(os.path.join(ROOT, 'FT.App', 'FT.App.csproj'),
                       encoding='utf-8').read()
    except OSError:
        return '?'
    m = re.search(r'<Version>([^<]+)</Version>', proj)
    return 'v' + m.group(1) if m else '?'


def main():
    sections = []
    navs = []

    for title, path, note in DOCS:
        full = os.path.join(ROOT, path.replace('/', os.sep))
        if not os.path.exists(full):
            print('  skipped: %s not found' % path)
            continue

        md = io.open(full, encoding='utf-8').read()
        # 문서 위아래의 길잡이 줄은 사이드바가 대신하므로 뺀다.
        md = re.sub(r'^\[←[^\n]*\n', '', md, flags=re.M)

        heads = []
        body = render(md, title, heads)
        sid = slug(title)

        sections.append(
            '<section id="%s"><h1 class="doctitle">%s</h1>'
            '<p class="docnote">%s · <code>%s</code></p>%s</section>'
            % (sid, html.escape(title), html.escape(note), html.escape(path), body))

        navs.append('<a class="doc" href="#%s">%s<small>%s</small></a>%s'
                    % (sid, html.escape(title), html.escape(note),
                       ''.join('<a class="sub" href="#%s">%s</a>' % (h, html.escape(t))
                               for h, t in heads)))

    stamp = datetime.date.today().isoformat()
    page = (
        '<!DOCTYPE html>\n<html lang="ko"><head><meta charset="utf-8">\n'
        '<meta name="viewport" content="width=device-width,initial-scale=1">\n'
        '<title>Football Tactician 기획서</title>\n<style>' + CSS + '</style></head>\n'
        '<body><div id="wrap">\n'
        '<nav><h1>⚽ Football Tactician</h1>\n'
        '<div class="ver">' + version() + ' · ' + stamp + ' 기준</div>\n'
        + ''.join(navs) +
        '<a class="doc" href="https://github.com/Sundae-unity-dev/Football-TacticManager"'
        ' target="_blank" rel="noopener">GitHub<small>저장소와 릴리스</small></a>\n'
        '<a class="doc" href="https://sundae-unity-dev.github.io/Football-TacticManager/"'
        ' target="_blank" rel="noopener">플레이<small>배포된 게임</small></a>\n'
        '</nav>\n<main><div class="top"><span class="pill">기획서</span>'
        '<span class="pill">문서 ' + str(len(sections)) + '개</span>'
        '<span class="pill">' + stamp + ' 에 만듦</span></div>\n'
        + ''.join(sections) +
        '\n</main></div><script>' + JS + '</script></body></html>\n')

    io.open(OUT, 'w', encoding='utf-8', newline='\n').write(page)
    # 윈도 콘솔은 cp949 라 em dash 를 못 찍는다. 아스키로만 알린다.
    print('made %s (%s bytes, %d docs)'
          % (OUT, format(len(page.encode('utf-8')), ','), len(sections)))


if __name__ == '__main__':
    main()
