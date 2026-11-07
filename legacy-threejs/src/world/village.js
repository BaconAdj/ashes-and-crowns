import * as THREE from 'three';
import { getHeightAt } from './terrain.js';
import { fbm } from './noise.js';
import balance from '../../data/balance.json';
import worldData from '../../data/world.json';

const { mapWidth, mapDepth } = balance.world;
const B = balance.village;

// Deterministic but village-unique seeded hash
function seededRand(seed, idx) {
  const x = Math.sin(seed * 127.1 + idx * 311.7) * 43758.5453;
  return x - Math.floor(x);
}

// Materials — shared, dark timber medieval
const MAT = {
  daub:    new THREE.MeshLambertMaterial({ color: 0x8a7a5a }), // wattle & daub
  timber:  new THREE.MeshLambertMaterial({ color: 0x3a2c1a }), // dark oak frames
  thatch:  new THREE.MeshLambertMaterial({ color: 0x6a5a2a }), // thatch roofs
  stone:   new THREE.MeshLambertMaterial({ color: 0x5a5248 }), // stone church
  stoneDark:new THREE.MeshLambertMaterial({ color: 0x3a3530 }),
  dirt:    new THREE.MeshLambertMaterial({ color: 0x4a3c28 }), // paths
  well:    new THREE.MeshLambertMaterial({ color: 0x3a3530 }),
  door:    new THREE.MeshLambertMaterial({ color: 0x2a1a0a }),
};

function makeHouse(seed, i, baseH) {
  const group = new THREE.Group();
  const w = B.houseWidthRange[0]  + seededRand(seed, i * 7 + 1) * (B.houseWidthRange[1]  - B.houseWidthRange[0]);
  const d = B.houseDepthRange[0]  + seededRand(seed, i * 7 + 2) * (B.houseDepthRange[1]  - B.houseDepthRange[0]);
  const h = B.houseHeightRange[0] + seededRand(seed, i * 7 + 3) * (B.houseHeightRange[1] - B.houseHeightRange[0]);

  // Walls
  const wallGeo = new THREE.BoxGeometry(w, h, d);
  const wall = new THREE.Mesh(wallGeo, MAT.daub);
  wall.position.y = h / 2;
  wall.castShadow = true;
  wall.receiveShadow = true;
  group.add(wall);

  // Timber frame bands
  const frameGeo = new THREE.BoxGeometry(w + 0.05, 0.2, d + 0.05);
  for (let fh = 1; fh < h; fh += Math.max(1.5, h / 3)) {
    const frame = new THREE.Mesh(frameGeo, MAT.timber);
    frame.position.y = fh;
    group.add(frame);
  }

  // Roof (pitched)
  const roofH = 1.8 + seededRand(seed, i * 7 + 4) * 1.2;
  const roofGeo = new THREE.ConeGeometry(Math.sqrt(w * w + d * d) / 2 + 0.3, roofH, 4);
  const roof = new THREE.Mesh(roofGeo, MAT.thatch);
  roof.position.y = h + roofH / 2;
  roof.rotation.y = Math.atan2(d, w);
  roof.castShadow = true;
  group.add(roof);

  // Door
  const doorGeo = new THREE.BoxGeometry(0.9, 1.8, 0.1);
  const door = new THREE.Mesh(doorGeo, MAT.door);
  door.position.set(0, 0.9, d / 2 + 0.05);
  group.add(door);

  group.position.y = baseH;
  return group;
}

function makeChurch(baseH) {
  const group = new THREE.Group();

  // Nave
  const naveGeo = new THREE.BoxGeometry(8, B.churchHeight, 14);
  const nave = new THREE.Mesh(naveGeo, MAT.stone);
  nave.position.y = B.churchHeight / 2;
  nave.castShadow = true;
  nave.receiveShadow = true;
  group.add(nave);

  // Tower
  const towerGeo = new THREE.BoxGeometry(5, B.churchTowerHeight, 5);
  const tower = new THREE.Mesh(towerGeo, MAT.stoneDark);
  tower.position.set(-1.5, B.churchTowerHeight / 2, -9.5);
  tower.castShadow = true;
  group.add(tower);

  // Tower crenellations
  for (let ci = 0; ci < 4; ci++) {
    const cx = (ci % 2 === 0 ? -3 : 3);
    const cz = (ci < 2 ? -12 : -7);
    const crenGeo = new THREE.BoxGeometry(1.5, 2, 1.5);
    const cren = new THREE.Mesh(crenGeo, MAT.stoneDark);
    cren.position.set(cx, B.churchTowerHeight + 1, cz);
    group.add(cren);
  }

  // Nave roof
  const roofGeo = new THREE.CylinderGeometry(0.1, 5.5, 5, 4);
  const roof = new THREE.Mesh(roofGeo, MAT.stone);
  roof.position.y = B.churchHeight + 2.5;
  group.add(roof);

  // Arched windows (simplified as dark rectangles)
  const winMat = new THREE.MeshLambertMaterial({ color: 0x1a1a2a });
  for (let w = 0; w < 3; w++) {
    const winGeo = new THREE.BoxGeometry(0.1, 2.2, 1.0);
    const win = new THREE.Mesh(winGeo, winMat);
    win.position.set(4.05, B.churchHeight * 0.55, -4 + w * 4);
    group.add(win);
  }

  group.position.y = baseH;
  return group;
}

function makeWell(baseH) {
  const group = new THREE.Group();
  const baseGeo = new THREE.CylinderGeometry(1.2, 1.4, 0.6, 8);
  const base = new THREE.Mesh(baseGeo, MAT.stone);
  base.position.y = 0.3;
  group.add(base);

  const wallGeo = new THREE.CylinderGeometry(1.0, 1.0, 0.8, 8, 1, true);
  const wall = new THREE.Mesh(wallGeo, MAT.stone);
  wall.position.y = 1.0;
  group.add(wall);

  // Crossbeam
  const beamGeo = new THREE.BoxGeometry(0.2, 1.8, 2.6);
  const beam = new THREE.Mesh(beamGeo, MAT.timber);
  beam.position.y = 2.2;
  group.add(beam);

  const roofGeo = new THREE.ConeGeometry(1.6, 1.2, 4);
  const roof = new THREE.Mesh(roofGeo, MAT.thatch);
  roof.position.y = 3.8;
  group.add(roof);

  group.position.y = baseH;
  return group;
}

function makePath(x1, z1, x2, z2, baseH) {
  const dx = x2 - x1, dz = z2 - z1;
  const len = Math.sqrt(dx * dx + dz * dz);
  const pathGeo = new THREE.PlaneGeometry(1.8, len);
  pathGeo.rotateX(-Math.PI / 2);
  const path = new THREE.Mesh(pathGeo, MAT.dirt);
  path.position.set((x1 + x2) / 2, baseH + 0.05, (z1 + z2) / 2);
  path.rotation.y = Math.atan2(dx, dz);
  path.receiveShadow = true;
  return path;
}

export function buildVillage(scene, villageData) {
  const { nx, nz, name } = villageData;
  const seed = nx * 1000 + nz * 999;

  const wx = (nx - 0.5) * mapWidth;
  const wz = (nz - 0.5) * mapDepth;
  const baseH = getHeightAt(wx, wz);

  const group = new THREE.Group();
  group.name = `village_${villageData.id}`;
  group.position.set(wx, 0, wz);

  const radius = B.villageRadius;
  const houseCount = Math.floor(B.houseCount[0] + seededRand(seed, 0) * (B.houseCount[1] - B.houseCount[0]));

  // Church is always at the centre
  const church = makeChurch(baseH);
  group.add(church);

  // Well near church
  const well = makeWell(baseH);
  well.position.set(12, 0, 0);
  group.add(well);

  // Paths from well to ring of houses
  const housePositions = [];
  for (let i = 0; i < houseCount; i++) {
    const angle = (i / houseCount) * Math.PI * 2 + seededRand(seed, i * 3 + 10) * 0.8;
    const r = radius * (0.55 + seededRand(seed, i * 3 + 11) * 0.45);
    const hx = Math.cos(angle) * r;
    const hz = Math.sin(angle) * r;
    const hBase = getHeightAt(wx + hx, wz + hz);

    const house = makeHouse(seed, i, hBase - baseH);
    house.position.set(hx, 0, hz);
    house.rotation.y = angle + Math.PI + (seededRand(seed, i * 3 + 12) - 0.5) * 0.6;
    group.add(house);
    housePositions.push({ x: hx, z: hz });

    // Dirt path from house to village centre
    const path = makePath(hx * 0.9, hz * 0.9, hx * 0.25, hz * 0.25, baseH - baseH);
    group.add(path);
  }

  scene.add(group);

  return { group, position: new THREE.Vector3(wx, baseH, wz), name };
}

// Build all spawn villages from world data
export function buildAllVillages(scene) {
  return worldData.spawnVillages.map(v => buildVillage(scene, v));
}
