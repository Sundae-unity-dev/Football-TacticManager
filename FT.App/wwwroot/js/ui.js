// 화면 설정. "세계"가 아니라 "이 브라우저"에 속한 값만 둔다 — 세이브에 넣지 않는다.
//
// 글씨 크기는 html 의 class 로 건다. 화면의 치수가 거의 전부 rem 이라
// 뿌리 하나만 바꾸면 표도 배지도 버튼도 같이 늘어난다(css/app.css 맨 위).

const KEY = 'ft.ui.scale';
const CLASSES = ['ui-lg', 'ui-xl'];

export function get() {
    try { return localStorage.getItem(KEY) || 'base'; } catch { return 'base'; }
}

export function set(scale) {
    const html = document.documentElement;
    html.classList.remove(...CLASSES);
    if (scale === 'lg') html.classList.add('ui-lg');
    else if (scale === 'xl') html.classList.add('ui-xl');
    try { localStorage.setItem(KEY, scale); } catch { /* 시크릿 창이면 이번만 적용된다 */ }
}
