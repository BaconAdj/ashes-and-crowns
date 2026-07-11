import * as THREE from 'three';

// Sky, sun, ambient lighting — driven by the time system
export class SkySystem {
  constructor(scene) {
    this.scene = scene;

    // Ambient — fills shadows softly
    this.ambient = new THREE.AmbientLight(0x8899aa, 0.35);
    scene.add(this.ambient);

    // Sun / directional
    this.sun = new THREE.DirectionalLight(0xfff4e0, 1.2);
    this.sun.castShadow = true;
    this.sun.shadow.mapSize.set(2048, 2048);
    this.sun.shadow.camera.near = 1;
    this.sun.shadow.camera.far = 800;
    this.sun.shadow.camera.left = -300;
    this.sun.shadow.camera.right = 300;
    this.sun.shadow.camera.top = 300;
    this.sun.shadow.camera.bottom = -300;
    this.sun.shadow.bias = -0.0003;
    scene.add(this.sun);

    // Moon (dim fill at night)
    this.moon = new THREE.DirectionalLight(0x4466aa, 0.08);
    scene.add(this.moon);

    // Hemisphere sky/ground tint
    this.hemi = new THREE.HemisphereLight(0x8899cc, 0x4a3e28, 0.4);
    scene.add(this.hemi);

    // Fog
    scene.fog = new THREE.Fog(0x9aaabb, 180, 900);
  }

  // hour: 0-24 float
  update(hour, weatherState) {
    const t = hour / 24;
    // Sun arc — rises east (x+), sets west (x-)
    const sunAngle = (t - 0.25) * Math.PI * 2;
    const sunHeight = Math.sin((t - 0.08) * Math.PI);

    this.sun.position.set(
      Math.cos(sunAngle) * 400,
      Math.max(-50, sunHeight * 350),
      Math.sin(sunAngle) * 400
    );

    const isDawn  = hour >= 5.5  && hour < 7.5;
    const isDay   = hour >= 7.5  && hour < 17.5;
    const isDusk  = hour >= 17.5 && hour < 20;
    const isNight = hour >= 20   || hour < 5.5;

    let skyCol, fogCol, sunInt, ambInt;

    if (isDawn) {
      const p = (hour - 5.5) / 2;
      skyCol = lerpHex(0x1a1e2a, 0xe89060, p);
      fogCol = lerpHex(0x1a1e2a, 0xc4a080, p);
      sunInt = lerp(0.1, 1.1, p);
      ambInt = lerp(0.08, 0.35, p);
    } else if (isDay) {
      const rain = weatherState?.raining ? 1 : 0;
      skyCol = lerpHex(0xb8c8d8, 0x6a7a8a, rain * 0.6);
      fogCol = lerpHex(0xa8b8c4, 0x7a8a94, rain * 0.6);
      sunInt = lerp(1.1, 0.55, rain);
      ambInt = lerp(0.38, 0.55, rain * 0.5);
    } else if (isDusk) {
      const p = (hour - 17.5) / 2.5;
      skyCol = lerpHex(0xe89060, 0x1a1e2a, p);
      fogCol = lerpHex(0xc4a080, 0x1a1e2a, p);
      sunInt = lerp(0.9, 0.05, p);
      ambInt = lerp(0.32, 0.06, p);
    } else {
      skyCol = 0x080c14;
      fogCol = 0x080c14;
      sunInt = 0.0;
      ambInt = 0.04;
    }

    this.scene.background = new THREE.Color(skyCol);
    this.scene.fog.color.set(fogCol);
    this.sun.intensity = sunInt;
    this.ambient.intensity = ambInt;
    this.moon.intensity = isNight ? 0.12 : 0;
    this.moon.position.set(-this.sun.position.x, Math.abs(this.sun.position.y) + 100, -this.sun.position.z);

    // Weather fog
    if (weatherState?.raining) {
      this.scene.fog.near = 80;
      this.scene.fog.far = 380;
    } else if (weatherState?.foggy) {
      this.scene.fog.near = 40;
      this.scene.fog.far = 220;
    } else {
      this.scene.fog.near = 180;
      this.scene.fog.far = 900;
    }
  }
}

function lerp(a, b, t) { return a + (b - a) * Math.min(1, Math.max(0, t)); }

function lerpHex(c1, c2, t) {
  const r1 = (c1 >> 16) & 0xff, g1 = (c1 >> 8) & 0xff, b1 = c1 & 0xff;
  const r2 = (c2 >> 16) & 0xff, g2 = (c2 >> 8) & 0xff, b2 = c2 & 0xff;
  const r = Math.round(r1 + (r2 - r1) * t);
  const g = Math.round(g1 + (g2 - g1) * t);
  const b = Math.round(b1 + (b2 - b1) * t);
  return (r << 16) | (g << 8) | b;
}
