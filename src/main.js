import * as THREE from 'three';
import { buildTerrain, findSpawnPoint } from './world/terrain.js';
import { buildAllVillages } from './world/village.js';
import { SkySystem } from './world/sky.js';
import { TimeSystem } from './systems/time.js';
import { WeatherSystem } from './systems/weather.js';
import { PlayerController } from './player/controller.js';
import { HorseEntity } from './player/horse.js';
import worldData from '../data/world.json';

// ── Renderer ───────────────────────────────────────────────────────────────
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.outputColorSpace = THREE.SRGBColorSpace;
document.body.appendChild(renderer.domElement);

window.addEventListener('resize', () => {
  renderer.setSize(window.innerWidth, window.innerHeight);
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
});

// ── Scene & Camera ─────────────────────────────────────────────────────────
const scene  = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(65, window.innerWidth / window.innerHeight, 0.5, 2000);

// ── World ──────────────────────────────────────────────────────────────────
buildTerrain(scene);
buildAllVillages(scene);

// ── Systems ────────────────────────────────────────────────────────────────
const timeSystem    = new TimeSystem();
const weatherSystem = new WeatherSystem(scene, camera);
const skySystem     = new SkySystem(scene);

// ── Player & Horse ─────────────────────────────────────────────────────────
const player = new PlayerController(scene, camera);

// Spawn in first village
const spawnVillage = worldData.spawnVillages[0];
const spawnPos = findSpawnPoint(spawnVillage.nx, spawnVillage.nz);
player.setPosition(spawnPos.clone().setY(spawnPos.y + 1));

// Horse spawns a few metres from player
const horseSpawn = spawnPos.clone();
horseSpawn.x += 6;
horseSpawn.y = spawnPos.y;
const horse = new HorseEntity(scene, horseSpawn);
horse.rider = player;

// ── Church bell notifications ──────────────────────────────────────────────
const bellNotif = document.getElementById('bell-notif');
const bellDisp  = document.getElementById('bell-display');

timeSystem.onBell((name, hour) => {
  if (bellNotif) {
    bellNotif.textContent = `🔔 ${name}`;
    bellNotif.style.opacity = '1';
    setTimeout(() => { bellNotif.style.opacity = '0'; }, 4000);
  }
});

// ── Pointer Lock ──────────────────────────────────────────────────────────
const overlay   = document.getElementById('pointer-lock-overlay');
const startBtn  = document.getElementById('start-btn');

function requestLock() {
  renderer.domElement.requestPointerLock();
}

if (startBtn)  startBtn.addEventListener('click', requestLock);
if (overlay)   overlay.addEventListener('click',  requestLock);

document.addEventListener('pointerlockchange', () => {
  const locked = !!document.pointerLockElement;
  player.setPointerLocked(locked);
  if (overlay) overlay.style.display = locked ? 'none' : 'flex';
});

document.addEventListener('pointerlockerror', () => {
  console.warn('Pointer lock failed');
});

// ── HUD helpers ────────────────────────────────────────────────────────────
const locationEl  = document.getElementById('location-name');
const actionEl    = document.getElementById('action-prompt');

function updateHUD() {
  // Bell display
  if (bellDisp) bellDisp.textContent = timeSystem.bellName + '  ' + timeSystem.dateString;

  // Action prompt: mount hint when near horse
  if (actionEl) {
    if (!player.onHorse) {
      const d = player.distanceTo(horse.position);
      actionEl.textContent = d < 5 ? '[E] Mount' : '';
    } else {
      actionEl.textContent = '[E] Dismount   [Shift] Gallop';
    }
  }

  // Location name — nearest village
  if (locationEl && _nearestVillageName) {
    locationEl.textContent = _nearestVillageName;
  }
}

// ── Village proximity label ────────────────────────────────────────────────
const _villages = worldData.spawnVillages;
let   _nearestVillageName = '';

function updateLocationLabel() {
  let best = Infinity;
  let name = '';
  for (const v of _villages) {
    // rough world coords from normalized
    const wx = (v.nx - 0.5) * 4096;
    const wz = (v.nz - 0.5) * 4096;
    const dx = player.position.x - wx;
    const dz = player.position.z - wz;
    const d2 = dx * dx + dz * dz;
    if (d2 < best) { best = d2; name = v.name; }
  }
  _nearestVillageName = best < 200 * 200 ? name : '';
}

// ── Game Loop ─────────────────────────────────────────────────────────────
let _lastTime = performance.now();
let _hourAccum = 0;

function loop() {
  requestAnimationFrame(loop);

  const now = performance.now();
  const dt  = Math.min((now - _lastTime) / 1000, 0.05); // cap at 50ms
  _lastTime = now;

  // Advance time
  timeSystem.update(dt);

  // Hourly systems
  _hourAccum += dt;
  const secPerHour = 60; // matches balance.time.realSecondsPerGameHour
  if (_hourAccum >= secPerHour) {
    _hourAccum -= secPerHour;
    weatherSystem.hourlyTick(timeSystem);
  }

  // Update horse (pass player keys and yaw)
  horse.update(dt, player.keys, player.yaw);

  // Update player
  player.update(dt, timeSystem);

  // Update world systems
  skySystem.update(timeSystem.hour, weatherSystem.state);
  weatherSystem.update(dt, player.worldPosition);

  // HUD
  updateLocationLabel();
  updateHUD();

  renderer.render(scene, camera);
}

loop();
