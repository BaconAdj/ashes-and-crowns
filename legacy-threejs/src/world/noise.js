// Deterministic value noise — no external deps
// Produces consistent terrain every run (fixed seed = fixed England)

const SEED = 1135; // Year of the Anarchy begins

function hash(n) {
  let x = Math.sin(n + SEED) * 43758.5453;
  return x - Math.floor(x);
}

function lerp(a, b, t) {
  const u = t * t * (3 - 2 * t); // smoothstep
  return a + (b - a) * u;
}

export function noise2(x, z) {
  const ix = Math.floor(x), iz = Math.floor(z);
  const fx = x - ix, fz = z - iz;
  const a = hash(ix + iz * 57);
  const b = hash(ix + 1 + iz * 57);
  const c = hash(ix + (iz + 1) * 57);
  const d = hash(ix + 1 + (iz + 1) * 57);
  return lerp(lerp(a, b, fx), lerp(c, d, fx), fz);
}

export function fbm(x, z, octaves = 5) {
  let v = 0, amp = 1, freq = 1, max = 0;
  for (let i = 0; i < octaves; i++) {
    v += noise2(x * freq, z * freq) * amp;
    max += amp;
    amp *= 0.5;
    freq *= 2.1;
  }
  return v / max;
}

// England silhouette mask — returns 0 outside, 1 inside
// Normalised coords: nx in [0,1] west-east, nz in [0,1] north-south
export function englandMask(nx, nz) {
  // Rough England outline via ellipse + deformations
  const cx = 0.52, cz = 0.52;
  const dx = (nx - cx) / 0.36;
  const dz = (nz - cz) / 0.52;
  const dist = Math.sqrt(dx * dx + dz * dz);

  // Add some coastline noise
  const coastNoise = fbm(nx * 4, nz * 4, 3) * 0.22;
  const inside = dist + coastNoise < 1.0;

  // Scotland blob at north
  const sdx = (nx - 0.42) / 0.18;
  const sdz = (nz - 0.10) / 0.20;
  const inScotland = Math.sqrt(sdx * sdx + sdz * sdz) + fbm(nx * 5, nz * 5, 2) * 0.2 < 1.0;

  // Exclude Irish Sea (northwest)
  const irishCut = nx < 0.28 && nz > 0.42 && nz < 0.72;

  return (inside || inScotland) && !irishCut ? 1 : 0;
}

// Height for given normalised coords — bakes England geography
export function englandHeight(nx, nz, maxH) {
  if (!englandMask(nx, nz)) return -8; // below sea

  // Large-scale regional topography
  let h = 0;

  // Pennines spine (high, north-centre)
  const pennX = Math.abs(nx - 0.50);
  const pennZ = 0.35 - nz;
  if (pennZ > 0 && pennX < 0.12) {
    h += Math.max(0, (0.12 - pennX) / 0.12) * (pennZ / 0.35) * 0.7;
  }

  // Scottish Highlands (far north)
  if (nz < 0.20) h += (1 - nz / 0.20) * 0.6;

  // Southwest moors (Dartmoor / Exmoor rough)
  const swDist = Math.sqrt((nx - 0.20) ** 2 + (nz - 0.80) ** 2);
  if (swDist < 0.14) h += (0.14 - swDist) / 0.14 * 0.4;

  // East Anglia flat
  if (nx > 0.72 && nz > 0.50) h -= 0.15;

  // Thames valley dip
  if (nz > 0.60 && nz < 0.70 && nx > 0.40 && nx < 0.78)
    h -= 0.1 * (1 - Math.abs(nz - 0.65) / 0.05);

  // Midlands gentle
  if (nz > 0.42 && nz < 0.60 && nx > 0.38 && nx < 0.68) h += 0.05;

  h = Math.max(0, Math.min(1, h));

  // Layered noise
  const fine = fbm(nx * 8, nz * 8, 5);
  const coarse = fbm(nx * 3, nz * 3, 3);
  const combined = h * 0.55 + coarse * 0.28 + fine * 0.17;

  return Math.max(0.5, combined * maxH);
}
