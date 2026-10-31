// 세이브 저장소. IndexedDB 를 쓰고, 막혀 있으면 localStorage 로 떨어진다.
//
// localStorage 는 약 5MB 가 한도이고 문자열만 담는다. 이적 기록과 구단 역사가 쌓이면
// 금방 모자라므로 IndexedDB 에 바이트 그대로 넣는다.

const DB_NAME = 'football-tactician';
const DB_VERSION = 1;
const STORE = 'saves';
const LS_PREFIX = 'ft.fallback.';

let dbPromise = null;

function openDb() {
    if (dbPromise) return dbPromise;

    dbPromise = new Promise((resolve, reject) => {
        if (!('indexedDB' in globalThis)) {
            reject(new Error('indexedDB unavailable'));
            return;
        }
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = () => {
            const db = req.result;
            if (!db.objectStoreNames.contains(STORE)) db.createObjectStore(STORE);
        };
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error ?? new Error('indexedDB open failed'));
        req.onblocked = () => reject(new Error('indexedDB blocked'));
    }).catch(err => {
        dbPromise = null;            // 다음 호출에서 다시 시도한다
        throw err;
    });

    return dbPromise;
}

function tx(db, mode) {
    return db.transaction(STORE, mode).objectStore(STORE);
}

function request(req) {
    return new Promise((resolve, reject) => {
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });
}

export async function put(key, base64) {
    try {
        const db = await openDb();
        await request(tx(db, 'readwrite').put(base64, key));
        return 'indexeddb';
    } catch {
        try {
            localStorage.setItem(LS_PREFIX + key, base64);
            return 'localstorage';
        } catch {
            return 'none';           // 시크릿 창이거나 용량 초과
        }
    }
}

export async function get(key) {
    try {
        const db = await openDb();
        const value = await request(tx(db, 'readonly').get(key));
        if (value !== undefined && value !== null) return value;
    } catch {
        // IndexedDB 가 막혔으면 아래로 떨어진다
    }
    try {
        return localStorage.getItem(LS_PREFIX + key);
    } catch {
        return null;
    }
}

export async function remove(key) {
    try {
        const db = await openDb();
        await request(tx(db, 'readwrite').delete(key));
    } catch {
        // 무시
    }
    try {
        localStorage.removeItem(LS_PREFIX + key);
    } catch {
        // 무시
    }
}

/// 어느 저장소가 실제로 쓰이는지. 화면에 표시한다.
export async function backend() {
    try {
        await openDb();
        return 'IndexedDB';
    } catch {
        try {
            localStorage.setItem(LS_PREFIX + '__probe', '1');
            localStorage.removeItem(LS_PREFIX + '__probe');
            return 'localStorage';
        } catch {
            return '없음';
        }
    }
}
