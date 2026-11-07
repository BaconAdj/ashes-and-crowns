import * as THREE from 'three';
import { getHeightAt } from '../world/terrain.js';
import balance from '../../data/balance.json';

const P = balance.player;

export class PlayerController {
  constructor(scene, camera) {
    this.scene  = scene;
    this.camera = camera;

    // State
    this.position  = new THREE.Vector3();
    this.velocity  = new THREE.Vector3();
    this.yaw       = 0;    // camera yaw (radians)
    this.pitch     = 0.3;  // camera pitch
    this.onHorse   = false;
    this.horse     = null; // HorseEntity ref, set by horse.js

    this.health  = P.healthMax;
    this.hunger  = P.hungerMax * 0.9;
    this.stamina = P.staminaMax;

    this.wounds = [];
    this.permanentInjuries = [];

    // Input
    this.keys = {};
    this._mouseDX = 0;
    this._mouseDY = 0;
    this._pointerLocked = false;

    // Mesh (simple capsule stand-in)
    this._buildMesh(scene);
    this._setupInput();
  }

  _buildMesh(scene) {
    const group = new THREE.Group();

    // Body
    const bodyGeo = new THREE.CylinderGeometry(0.3, 0.3, 1.2, 8);
    const bodyMat = new THREE.MeshLambertMaterial({ color: 0x3a2c1a });
    const body = new THREE.Mesh(bodyGeo, bodyMat);
    body.position.y = 0.9;
    body.castShadow = true;
    group.add(body);

    // Head
    const headGeo = new THREE.SphereGeometry(0.22, 8, 8);
    const headMat = new THREE.MeshLambertMaterial({ color: 0xc4a07a });
    const head = new THREE.Mesh(headGeo, headMat);
    head.position.y = 1.7;
    head.castShadow = true;
    group.add(head);

    // Cloak
    const cloakGeo = new THREE.CylinderGeometry(0.28, 0.42, 1.0, 8);
    const cloakMat = new THREE.MeshLambertMaterial({ color: 0x2a2218 });
    const cloak = new THREE.Mesh(cloakGeo, cloakMat);
    cloak.position.y = 0.7;
    group.add(cloak);

    this.mesh = group;
    scene.add(group);
  }

  _setupInput() {
    document.addEventListener('keydown', e => { this.keys[e.code] = true; });
    document.addEventListener('keyup',   e => { this.keys[e.code] = false; });

    document.addEventListener('mousemove', e => {
      if (!this._pointerLocked) return;
      this._mouseDX += e.movementX;
      this._mouseDY += e.movementY;
    });

    document.addEventListener('pointerlockchange', () => {
      this._pointerLocked = !!document.pointerLockElement;
    });
  }

  setPointerLocked(locked) {
    this._pointerLocked = locked;
  }

  setPosition(v) {
    this.position.copy(v);
    this.mesh.position.copy(v);
  }

  update(dt, timeSystem) {
    if (!this._pointerLocked) { this._mouseDX = 0; this._mouseDY = 0; return; }

    // Camera rotation from mouse
    const sensitivity = 0.002;
    this.yaw   -= this._mouseDX * sensitivity;
    this.pitch -= this._mouseDY * sensitivity;
    this.pitch  = Math.max(P.cameraPitchMin, Math.min(P.cameraPitchMax, this.pitch));
    this._mouseDX = 0;
    this._mouseDY = 0;

    if (this.onHorse && this.horse) {
      this._updateMounted(dt);
    } else {
      this._updateOnFoot(dt, timeSystem);
    }

    this._updateCamera();
    this._updateHUD(timeSystem);
  }

  _updateOnFoot(dt, timeSystem) {
    const sprinting = this.keys['ShiftLeft'] || this.keys['ShiftRight'];
    const speed = sprinting && this.stamina > 5 ? P.sprintSpeed : P.walkSpeed;

    // Movement vector in camera-yaw space
    const fwd  = new THREE.Vector3(-Math.sin(this.yaw), 0, -Math.cos(this.yaw));
    const right = new THREE.Vector3(Math.cos(this.yaw), 0, -Math.sin(this.yaw));
    const move  = new THREE.Vector3();

    if (this.keys['KeyW'] || this.keys['ArrowUp'])    move.addScaledVector(fwd,   1);
    if (this.keys['KeyS'] || this.keys['ArrowDown'])  move.addScaledVector(fwd,  -1);
    if (this.keys['KeyA'] || this.keys['ArrowLeft'])  move.addScaledVector(right,-1);
    if (this.keys['KeyD'] || this.keys['ArrowRight']) move.addScaledVector(right, 1);

    if (move.lengthSq() > 0) {
      move.normalize().multiplyScalar(speed * dt);
      this.position.add(move);
      // Face direction of movement
      this.mesh.rotation.y = Math.atan2(-move.x, -move.z);
    }

    // Stamina drain/regen
    if (sprinting && move.lengthSq() > 0) {
      this.stamina = Math.max(0, this.stamina - P.staminaDrainSprint * dt);
    } else {
      this.stamina = Math.min(P.staminaMax, this.stamina + P.staminaRegenRate * dt);
    }

    // Snap to terrain
    const groundH = getHeightAt(this.position.x, this.position.z);
    this.position.y = groundH + P.height * 0.5;
    this.mesh.position.copy(this.position);
    this.mesh.position.y = groundH;

    // Hunger
    const hungerRate = P.hungerDrainPerHour / 3600;
    this.hunger = Math.max(0, this.hunger - hungerRate * dt);
    if (this.hunger === 0 && timeSystem) {
      this.health = Math.max(0, this.health - 0.5 * dt);
    }
  }

  _updateMounted(dt) {
    // Position follows horse
    if (this.horse) {
      this.position.copy(this.horse.position);
      this.position.y += balance.horse.height + 0.5;
      this.mesh.position.copy(this.horse.position);
      this.mesh.position.y += balance.horse.height - 0.3;
      this.mesh.rotation.y = this.horse.mesh.rotation.y;
    }
  }

  _updateCamera() {
    // Third-person orbit
    const offset = new THREE.Vector3(
      -Math.sin(this.yaw) * Math.cos(this.pitch) * P.cameraDistance,
      Math.sin(this.pitch) * P.cameraDistance + P.cameraHeight,
      -Math.cos(this.yaw) * Math.cos(this.pitch) * P.cameraDistance
    );

    const target = this.position.clone().add(new THREE.Vector3(0, P.height * 0.8, 0));
    const camPos = target.clone().add(offset);

    // Smooth camera
    this.camera.position.lerp(camPos, 0.12);
    this.camera.lookAt(target);
  }

  _updateHUD(timeSystem) {
    const hpEl   = document.getElementById('fill-health');
    const hunEl  = document.getElementById('fill-hunger');
    const stamEl = document.getElementById('fill-stamina');
    if (hpEl)   hpEl.style.width   = (this.health  / P.healthMax  * 100) + '%';
    if (hunEl)  hunEl.style.width  = (this.hunger  / P.hungerMax  * 100) + '%';
    if (stamEl) stamEl.style.width = (this.stamina / P.staminaMax * 100) + '%';
  }

  // Check proximity to an entity
  distanceTo(v) { return this.position.distanceTo(v); }

  get worldPosition() { return this.position.clone(); }
}
