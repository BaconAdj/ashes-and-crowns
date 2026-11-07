const DB_NAME  = 'ashes-crowns';
const DB_VER   = 1;
const STORE    = 'save';
const SAVE_KEY = 'world';

let _db = null;

function openDB() {
  if (_db) return Promise.resolve(_db);
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, DB_VER);
    req.onupgradeneeded = e => {
      e.target.result.createObjectStore(STORE);
    };
    req.onsuccess = e => { _db = e.target.result; resolve(_db); };
    req.onerror   = e => reject(e.target.error);
  });
}

export async function saveGame(state) {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx  = db.transaction(STORE, 'readwrite');
    const req = tx.objectStore(STORE).put(state, SAVE_KEY);
    req.onsuccess = () => resolve();
    req.onerror   = e => reject(e.target.error);
  });
}

export async function loadGame() {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx  = db.transaction(STORE, 'readonly');
    const req = tx.objectStore(STORE).get(SAVE_KEY);
    req.onsuccess = e => resolve(e.target.result ?? null);
    req.onerror   = e => reject(e.target.error);
  });
}

// Synchronous-ish delete — fires and forgets, but also awaitable
export async function deleteSave() {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx  = db.transaction(STORE, 'readwrite');
    const req = tx.objectStore(STORE).delete(SAVE_KEY);
    req.onsuccess = () => resolve();
    req.onerror   = e => reject(e.target.error);
  });
}

export function serializeState(player, timeSystem) {
  return {
    version: 1,
    savedAt: Date.now(),
    time: {
      hour:  timeSystem.hour,
      day:   timeSystem.day,
      month: timeSystem.month,
      year:  timeSystem.year,
    },
    player: {
      x:       player.position.x,
      y:       player.position.y,
      z:       player.position.z,
      yaw:     player.yaw,
      health:  player.health,
      hunger:  player.hunger,
      stamina: player.stamina,
      wounds:          player.wounds.slice(),
      permanentInjuries: player.permanentInjuries.slice(),
    },
  };
}

export function applyState(state, player, timeSystem) {
  if (!state) return;
  const { time, player: p } = state;
  timeSystem.hour  = time.hour;
  timeSystem.day   = time.day;
  timeSystem.month = time.month;
  timeSystem.year  = time.year;
  player.position.set(p.x, p.y, p.z);
  player.mesh.position.copy(player.position);
  player.yaw     = p.yaw;
  player.health  = p.health;
  player.hunger  = p.hunger;
  player.stamina = p.stamina;
  player.wounds             = p.wounds || [];
  player.permanentInjuries  = p.permanentInjuries || [];
}
