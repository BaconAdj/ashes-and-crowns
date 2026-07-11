import * as THREE from 'three';
import balance from '../../data/balance.json';

const W = balance.weather;

export class WeatherSystem {
  constructor(scene, camera) {
    this.scene  = scene;
    this.camera = camera;

    this.state = { raining: false, foggy: false, snowing: false, wind: new THREE.Vector3(0.3, 0, 0.1) };
    this._hoursUntilChange = W.changeIntervalHours;
    this._rain = null;
    this._snow = null;
    this._buildRain();
    this._buildSnow();
  }

  _buildRain() {
    const count = W.rainDropCount;
    const geo = new THREE.BufferGeometry();
    const pos = new Float32Array(count * 3);
    for (let i = 0; i < count; i++) {
      pos[i * 3]     = (Math.random() - 0.5) * 120;
      pos[i * 3 + 1] = Math.random() * 80;
      pos[i * 3 + 2] = (Math.random() - 0.5) * 120;
    }
    geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    const mat = new THREE.PointsMaterial({ color: 0x8899aa, size: 0.06, transparent: true, opacity: 0.55, fog: false });
    this._rain = new THREE.Points(geo, mat);
    this._rain.visible = false;
    this.scene.add(this._rain);
  }

  _buildSnow() {
    const count = 3000;
    const geo = new THREE.BufferGeometry();
    const pos = new Float32Array(count * 3);
    for (let i = 0; i < count; i++) {
      pos[i * 3]     = (Math.random() - 0.5) * 120;
      pos[i * 3 + 1] = Math.random() * 80;
      pos[i * 3 + 2] = (Math.random() - 0.5) * 120;
    }
    geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    const mat = new THREE.PointsMaterial({ color: 0xdde8f0, size: 0.18, transparent: true, opacity: 0.75, fog: false });
    this._snow = new THREE.Points(geo, mat);
    this._snow.visible = false;
    this.scene.add(this._snow);
  }

  // Called every game-hour advance
  hourlyTick(timeSystem) {
    this._hoursUntilChange--;
    if (this._hoursUntilChange <= 0) {
      this._hoursUntilChange = W.changeIntervalHours + Math.random() * 3;
      this._roll(timeSystem);
    }
  }

  _roll(timeSystem) {
    const isWinter = timeSystem.season === 'Winter';
    const isAutumn = timeSystem.season === 'Autumn';
    const rChance = W.rainChanceNovember * (isWinter ? 1.2 : isAutumn ? 1.0 : 0.55);
    const sChance = W.snowChanceNovember * (isWinter ? 2.0 : 0.2);
    const fChance = W.fogChanceNovember  * (isWinter ? 1.3 : 0.6);

    const r = Math.random();
    this.state.raining = r < rChance;
    this.state.snowing = !this.state.raining && r < rChance + sChance;
    this.state.foggy   = !this.state.raining && !this.state.snowing && Math.random() < fChance;
  }

  update(dt, playerPos) {
    // Attach particles to camera so they always surround the player
    if (this._rain) {
      this._rain.visible = this.state.raining;
      this._rain.position.copy(playerPos);
    }
    if (this._snow) {
      this._snow.visible = this.state.snowing;
      this._snow.position.copy(playerPos);
    }

    if (this.state.raining || this.state.snowing) {
      const speed = this.state.raining ? W.rainSpeed : 4;
      const pos = this.state.raining ? this._rain.geometry.attributes.position
                                     : this._snow.geometry.attributes.position;
      const arr = pos.array;
      for (let i = 0; i < arr.length; i += 3) {
        arr[i + 1] -= speed * dt;
        if (arr[i + 1] < -5) arr[i + 1] += 80;
        if (this.state.raining) arr[i] += this.state.wind.x * dt * 0.8;
      }
      pos.needsUpdate = true;
    }
  }

  get weatherLabel() {
    if (this.state.snowing) return '❄ Snow';
    if (this.state.raining) return '🌧 Rain';
    if (this.state.foggy)   return '🌫 Fog';
    return '';
  }
}
