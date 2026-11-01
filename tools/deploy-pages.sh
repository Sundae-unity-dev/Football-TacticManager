#!/usr/bin/env bash
#
# 손으로 배포하는 길. **평소에는 쓰지 않습니다.**
#
# 평소 배포는 GitHub Actions 가 합니다(.github/workflows/pages.yml). main 에 앱 코드가
# 들어오면 테스트와 지문을 확인하고 올립니다. Actions 탭에서 손으로 돌릴 수도 있습니다.
#
# 이 스크립트는 Actions 가 막혔을 때(쿼터 소진, 러너 장애) 쓰는 비상구입니다.
# 쓰려면 저장소 Settings → Pages 에서 소스를 **gh-pages 브랜치**로 되돌려야 하고,
# 다시 Actions 에 맡길 때 소스를 **GitHub Actions** 로 바꿔 놔야 합니다.
#
#   사용법:  tools/deploy-pages.sh            # 새로 빌드해서 올린다 (AOT 라 20분쯤)
#            tools/deploy-pages.sh --no-build # publish/ 에 있는 것을 그대로 올린다
#
# 한 가지 더: 배포본을 git 브랜치로 나르면 한 번에 24MB 씩 저장소가 불어납니다.
# Actions 길(아티팩트)은 그 짐을 지지 않습니다 — 그래서 평소에는 그쪽을 씁니다.
set -euo pipefail

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

echo "== Pages 용으로 손보기 =="
# Actions 와 같은 파일을 쓴다. 두 길이 갈라지면 한쪽만 깨진다.
bash "$ROOT/tools/prepare-pages.sh" "$OUT"

echo "== $BRANCH 로 올리기 =="
# 이미 있으면 그 위에 쌓는다. 처음이면 뿌리 없는 가지로 시작한다.
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

    cp -r "$OUT/." .
    # prepare-pages.sh 가 '* -text' 를 심어 두지만, autocrlf 가 켜져 있어도
    # 손대지 못하게 한 번 더 못을 박는다. 산출물은 한 바이트도 바뀌면 안 된다.
    git -c core.autocrlf=false add -A
    git -c core.autocrlf=false commit -qm "배포본 $(date '+%Y-%m-%d %H:%M') — $(git -C "$ROOT" rev-parse --short HEAD)"
    git push -u origin "$BRANCH"
)
git worktree remove --force "$WORK"

echo "== 끝. https://$(git remote get-url origin | sed -E 's|.*github.com[:/]([^/]+)/.*|\1|' | tr 'A-Z' 'a-z').github.io/Football-TacticManager/ =="
