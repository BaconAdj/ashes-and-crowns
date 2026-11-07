import * as THREE from 'three';
import { englandHeight, englandMask } from './noise.js';
import balance from '../../data/balance.json';

const { mapWidth, mapDepth, terrainSegments, terrainMaxHeight, waterLevel } = balance.world;

// Exported heightmap for player collision
export let heightmap = null;
export let hmWidth = 0;
export let hmDepth = 0;

export function getHeightAt(worldX, worldZ) {
  if (!heightmap) return 0;
  const nx = (worldX + mapWidth / 2) / mapWidth;
  const nz = (worldZ + mapDepth / 2) / mapDepth;
  const ix = Math.floor(nx * (hmWidth - 1));
  const iz = Math.floor(nz * (hmDepth - 1));
  if (ix < 0 || iz < 0 || ix >= hmWidth - 1 || iz >= hmDepth - 1) return 0;
  const fx = nx * (hmWidth - 1) - ix;
  const fz = nz * (hmDepth - 1) - iz;
  const h00 = heightmap[iz * hmWidth + ix];
  const h10 = heightmap[iz * hmWidth + ix + 1];
  const h01 = heightmap[(iz + 1) * hmWidth + ix];
  const h11 = heightmap[(iz + 1) * hmWidth + ix + 1];
  return h00 * (1 - fx) * (1 - fz) + h10 * fx * (1 - fz) + h01 * (1 - fx) * fz + h11 * fx * fz;
}

export function buildTerrain(scene) {
  const segs = terrainSegments;
  hmWidth = segs + 1;
  hmDepth = segs + 1;
  heightmap = new Float32Array(hmWidth * hmDepth);

  const geo = new THREE.PlaneGeometry(mapWidth, mapDepth, segs, segs);
  geo.rotateX(-Math.PI / 2);

  const pos = geo.attributes.position;
  const colors = [];

  for (let iz = 0; iz <= segs; iz++) {
    for (let ix = 0; ix <= segs; ix++) {
      const nx = ix / segs;
      const nz = iz / segs;
      const h = englandHeight(nx, nz, terrainMaxHeight);
      const idx = iz * (segs + 1) + ix;
      pos.setY(idx, h);
      heightmap[idx] = h;

      // Vertex colours
      const mask = englandMask(nx, nz);
      let r, g, b;
      if (!mask || h < waterLevel + 0.5) {
        // Water / sea
        r = 0.10; g = 0.16; b = 0.24;
      } else if (h < 12) {
        // Coastal / low — pale muddy green
        r = 0.35; g = 0.38; b = 0.26;
      } else if (h < 40) {
        // Lowland — muted green
        r = 0.28; g = 0.33; b = 0.20;
      } else if (h < 75) {
        // Upland — moorland brown
        r = 0.36; g = 0.32; b = 0.22;
      } else if (h < 100) {
        // High moor — grey-brown
        r = 0.42; g = 0.38; b = 0.30;
      } else {
        // Peaks — grey rock
        r = 0.52; g = 0.50; b = 0.48;
      }
      colors.push(r, g, b);
    }
  }

  geo.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
  geo.computeVertexNormals();

  const mat = new THREE.MeshLambertMaterial({
    vertexColors: true,
    fog: true,
  });

  const mesh = new THREE.Mesh(geo, mat);
  mesh.receiveShadow = true;
  mesh.name = 'terrain';
  scene.add(mesh);

  // Water plane
  const waterGeo = new THREE.PlaneGeometry(mapWidth * 1.5, mapDepth * 1.5);
  waterGeo.rotateX(-Math.PI / 2);
  const waterMat = new THREE.MeshLambertMaterial({
    color: 0x1a2d4a,
    transparent: true,
    opacity: 0.82,
    fog: true,
  });
  const water = new THREE.Mesh(waterGeo, waterMat);
  water.position.y = waterLevel;
  water.name = 'water';
  scene.add(water);

  return mesh;
}

// Find a good spawn point near a normalised coordinate
export function findSpawnPoint(nx, nz) {
  const wx = (nx - 0.5) * mapWidth;
  const wz = (nz - 0.5) * mapDepth;
  const h = getHeightAt(wx, wz);
  return new THREE.Vector3(wx, h + 2, wz);
}
