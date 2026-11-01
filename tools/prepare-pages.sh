#!/usr/bin/env bash
#
# 배포본을 GitHub Pages 가 서비스할 수 있는 모양으로 고친다. 자리에서 고친다(in place).
#
#   사용법:  tools/prepare-pages.sh publish/wwwroot
#
# Actions 워크플로와 손으로 올리는 스크립트가 **같은 이 파일**을 쓴다.
# 두 길이 갈라지면, 손으로 올린 것은 되는데 자동 배포만 깨지는 일이 생긴다.
#
# Pages 는 저장소 이름이 붙은 주소로 서비스한다
# (https://<계정>.github.io/Football-TacticManager/). 그래서 네 가지가 필요하다.
set -euo pipefail

REPO_NAME=Football-TacticManager
OUT="${1:?올릴 디렉터리를 주세요 (예: publish/wwwroot)}"

[ -f "$OUT/index.html" ] || { echo "$OUT/index.html 이 없습니다"; exit 1; }

# 1) <base href> — "/" 로 두면 _framework 를 도메인 뿌리에서 찾아 전부 404 가 난다.
sed -i "s|<base href=\"/\" />|<base href=\"/$REPO_NAME/\" />|" "$OUT/index.html"
grep -q "<base href=\"/$REPO_NAME/\" />" "$OUT/index.html" \
    || { echo "base href 를 바꾸지 못했습니다 — index.html 모양이 달라졌나요?"; exit 1; }

# 2) Jekyll 끄기 — 밑줄로 시작하는 폴더를 숨긴다. Blazor 는 _framework 에 산다.
touch "$OUT/.nojekyll"

# 3) 새로고침해도 같은 화면 — Pages 는 클라이언트 라우팅을 모른다.
#    /squad 를 새로고침하면 404 를 돌려주는데, 그 내용이 앱이면 Blazor 가 주소를 보고 그린다.
cp "$OUT/index.html" "$OUT/404.html"

# 4) 줄바꿈을 건드리지 못하게 막는다.
#
#    한 번 데였다. 저장소 뿌리의 .gitattributes 는 소스를 LF 로 맞추는데, 그 규칙이
#    배포본에도 걸려서 dotnet.js 가 CRLF → LF 로 310바이트 줄었다. index.html 에 박힌
#    integrity 해시는 줄기 전 바이트로 계산된 값이라, 브라우저가 다 받아 놓고 버렸다.
#    화면은 "불러오는 중"에서 멈추고 콘솔에만 한 줄 남는다 — 네트워크는 200 이라
#    서버를 의심하게 된다. 빌드 산출물은 한 바이트도 바뀌면 안 된다.
printf '* -text\n' > "$OUT/.gitattributes"

# 5) .br/.gz 는 버린다. Pages 는 내용 협상을 하지 않아 알아보지 못하고,
#    대신 자기가 gzip 으로 압축해서 보낸다. 두고 봐야 용량만 두 배가 된다.
find "$OUT" -type f \( -name '*.br' -o -name '*.gz' \) -delete

echo "준비 끝 — $(find "$OUT" -type f | wc -l)개 파일 · $(du -sh "$OUT" | cut -f1)"
