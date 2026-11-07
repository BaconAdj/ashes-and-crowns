import * as THREE from 'three';
import { getHeightAt } from '../world/terrain.js';
import balance from '../../data/balance.json';

const H = balance.horse;

export class HorseEntity {
  constructor(scene, spawnPos) {
    this.scene    = scene;
    this.position = spawnPos.clone();
    this.velocity = new THREE.Vector3();
    this.yaw      = 0;
    this.speed    = 0;
    this.mounted  = false;
    this.rider    = null; // PlayerController ref

    this._buildMesh(scene);
    this._ePressed = false;
  }

  _buildMesh(scene) {
    const group = new THREE.Group();

    // Body
    const bodyGeo = new THREE.BoxGeometry(0.9, 0.85, 2.2);
    const bodyMat = new THREE.MeshLambertMaterial({ color: 0x3d2b1a });
    const body = new THREE.Mesh(bodyGeo, bodyMat);
    body.position.y = 1.1;
    body.castShadow = true;
    group.add(body);

    // Head & neck
    const neckGeo = new THREE.BoxGeometry(0.4, 0.7, 0.4);
    const neck = new THREE.Mesh(neckGeo, bodyMat);
    neck.position.set(0, 1.65, -1.0);
    neck.rotation.x = -0.4;
    group.add(neck);

    const headGeo = new THREE.BoxGeometry(0.38, 0.38, 0.65);
    const head = new THREE.Mesh(headGeo, bodyMat);
    head.position.set(0, 2.05, -1.35);
    head.castShadow = true;
    group.add(head);

    // Legs (4)
    const legGeo = new THREE.BoxGeometry(0.22, 0.9, 0.22);
    const legMat = new THREE.MeshLambertMaterial({ color: 0x2c1e10 });
    const legOffsets = [
      [-0.32, 0.45,  0.7],
      [ 0.32, 0.45,  0.7],
      [-0.32, 0.45, -0.7],
      [ 0.32, 0.45, -0.7],
    ];
    for (const [lx, ly, lz] of legOffsets) {
      const leg = new THREE.Mesh(legGeo, legMat);
      leg.position.set(lx, ly, lz);
      group.add(leg);
    }

    // Mane
    const maneGeo = new THREE.BoxGeometry(0.12, 0.45, 1.2);
    const maneMat = new THREE.MeshLambertMaterial({ color: 0x1a0f06 });
    const mane = new THREE.Mesh(maneGeo, maneMat);
    mane.position.set(0, 1.65, -0.3);
    group.add(mane);

    this.mesh = group;
    this.mesh.position.copy(this.position);
    scene.add(group);
  }

  update(dt, keys, playerYaw) {
    if (this.mounted && this.rider) {
      this._updateMounted(dt, keys, playerYaw);
    } else {
      // Idle — stay on terrain
      const groundH = getHeightAt(this.position.x, this.position.z);
      this.position.y = groundH;
      this.mesh.position.copy(this.position);
    }

    // Mount / dismount (E key)
    const eDown = keys['KeyE'];
    if (eDown && !this._ePressed) {
      this._ePressed = true;
      if (!this.mounted && this.rider) {
        const dist = this.position.distanceTo(this.rider.position);
        if (dist < H.mountDistance) this._mount();
      } else if (this.mounted) {
        this._dismount();
      }
    }
    if (!eDown) this._ePressed = false;
  }

  _updateMounted(dt, keys, playerYaw) {
    const galloping = keys['ShiftLeft'] || keys['ShiftRight'];
    const targetSpeed = galloping ? H.gallopSpeed : H.walkSpeed;

    const fwd = new THREE.Vector3(-Math.sin(playerYaw), 0, -Math.cos(playerYaw));
    const move = new THREE.Vector3();

    if (keys['KeyW'] || keys['ArrowUp'])    move.addScaledVector(fwd,  1);
    if (keys['KeyS'] || keys['ArrowDown'])  move.addScaledVector(fwd, -1);

    if (move.lengthSq() > 0) {
      move.normalize();
      this.speed = THREE.MathUtils.lerp(this.speed, targetSpeed, dt * 3);
      this.position.addScaledVector(move, this.speed * dt);
      this.yaw = Math.atan2(-move.x, -move.z);
      this.mesh.rotation.y = this.yaw;
    } else {
      this.speed = THREE.MathUtils.lerp(this.speed, 0, dt * 5);
    }

    const groundH = getHeightAt(this.position.x, this.position.z);
    this.position.y = groundH;
    this.mesh.position.copy(this.position);
  }

  _mount() {
    this.mounted = true;
    this.rider.onHorse = true;
    this.rider.horse   = this;
    this.yaw = this.rider.yaw;
  }

  _dismount() {
    this.mounted = false;
    if (this.rider) {
      this.rider.onHorse = false;
      this.rider.horse   = null;
      // Place rider beside horse
      const side = new THREE.Vector3(Math.cos(this.yaw), 0, -Math.sin(this.yaw));
      const dismountPos = this.position.clone().addScaledVector(side, 1.5);
      dismountPos.y = getHeightAt(dismountPos.x, dismountPos.z) + balance.player.height * 0.5;
      this.rider.setPosition(dismountPos);
    }
  }

  get worldPosition() { return this.position.clone(); }
}
