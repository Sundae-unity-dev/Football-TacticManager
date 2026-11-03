# -*- coding: utf-8 -*-
"""
기획서를 크롬 북마크바에 넣는다.

    py tools/add-bookmarks.py            # 넣기
    py tools/add-bookmarks.py --check    # 지금 넣을 수 있는 상태인지만 본다

**크롬이 완전히 꺼져 있어야 한다.** 크롬은 북마크를 메모리에 들고 있다가 종료할 때
파일로 덮어쓴다 - 켜진 채로 파일을 고치면 그 내용이 통째로 사라진다.

여러 번 돌려도 같은 주소는 다시 쌓이지 않는다(먼저 지우고 새로 넣는다).
고치기 전에 원본을 Bookmarks.backup-<시각> 으로 남긴다.
"""
import io
import json
import os
import shutil
import sys
import time
import uuid

PROFILE = os.path.join(os.environ.get('LOCALAPPDATA', ''),
                       'Google', 'Chrome', 'User Data', 'Default')
BOOKMARKS = os.path.join(PROFILE, 'Bookmarks')

PLAN_HTML = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                         'docs', 'plan.html')

GITHUB_DOC = ('https://github.com/Sundae-unity-dev/Football-TacticManager'
              '/blob/main/docs/%EB%A6%AC%EA%B7%B8-%EC%A0%9C%EB%8F%84.md')


def file_url(path):
    return 'file:///' + path.replace('\\', '/').replace(' ', '%20')


WANT = [
    ('⚽ 기획서', file_url(PLAN_HTML)),
    ('⚽ 기획서(GitHub)', GITHUB_DOC),
]


def chrome_running():
    """크롬이 떠 있는지. tasklist 로 본다 - 윈도 어디서나 있다."""
    out = os.popen('tasklist /FI "IMAGENAME eq chrome.exe" /NH 2>nul').read()
    return 'chrome.exe' in out.lower()


def chrome_time():
    """크롬의 시각: 1601-01-01 부터의 마이크로초."""
    return str(int((time.time() + 11644473600) * 1000000))


def max_id(node, best=0):
    try:
        best = max(best, int(node.get('id', 0)))
    except (TypeError, ValueError):
        pass
    for child in node.get('children', []) or []:
        best = max(best, max_id(child))
    return best


def main():
    check_only = '--check' in sys.argv

    if not os.path.exists(BOOKMARKS):
        print('[x] 북마크 파일을 못 찾았습니다: %s' % BOOKMARKS)
        return 1
    if not os.path.exists(PLAN_HTML):
        print('[x] 기획서가 없습니다. 먼저: py tools/make-plan.py')
        return 1

    running = chrome_running()
    print('기획서    : %s' % PLAN_HTML)
    print('북마크    : %s' % BOOKMARKS)
    print('크롬      : %s' % ('실행 중 - 닫아야 합니다' if running else '꺼져 있음'))

    if check_only:
        return 0
    if running:
        print()
        print('[!] 크롬이 켜져 있어 넣지 않았습니다.')
        print('    크롬을 완전히 닫고(트레이 아이콘까지) 다시 돌려 주세요.')
        return 2

    data = json.loads(io.open(BOOKMARKS, encoding='utf-8').read())
    bar = data['roots']['bookmark_bar']
    bar.setdefault('children', [])

    urls = {u for _n, u in WANT}
    before = len(bar['children'])
    bar['children'] = [c for c in bar['children'] if c.get('url') not in urls]
    replaced = before - len(bar['children'])

    next_id = max_id(data['roots']['bookmark_bar'])
    for key in ('other', 'synced'):
        if key in data['roots']:
            next_id = max(next_id, max_id(data['roots'][key]))

    now = chrome_time()
    for name, url in WANT:
        next_id += 1
        bar['children'].append({
            'date_added': now,
            'date_last_used': '0',
            'guid': str(uuid.uuid4()),
            'id': str(next_id),
            'name': name,
            'type': 'url',
            'url': url,
        })

    # 체크섬을 지우면 크롬이 다시 계산한다. 남겨 두면 "바뀌었다"고 의심한다.
    data.pop('checksum', None)

    backup = BOOKMARKS + '.backup-' + time.strftime('%Y%m%d-%H%M%S')
    shutil.copy2(BOOKMARKS, backup)

    io.open(BOOKMARKS, 'w', encoding='utf-8', newline='\n').write(
        json.dumps(data, ensure_ascii=False, indent=3))

    print()
    print('[o] 북마크바에 넣었습니다 (%d개 교체, %d개 추가)' % (replaced, len(WANT)))
    for name, url in WANT:
        print('    - %s' % name)
    print('    원본 백업: %s' % backup)
    print()
    print('    크롬을 켜고 Ctrl+Shift+B 로 북마크바를 켜면 보입니다.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
