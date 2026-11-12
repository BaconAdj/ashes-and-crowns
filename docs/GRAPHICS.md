# The road to "at least Skyrim"

The owner's bar (12 November 2026): the finished game looks at least as good as Skyrim (2011), better if we can.

## Where we are
- Geometry: about fifteen low-polygon models written by Python scripts (`Tools/blender`). Correct in size and period, simple in shape.
- Surfaces: tileable drawn textures with normal maps (`Editor/SurfaceTextures.cs`) on true-scale texture coordinates. Flat colour for people and animals.
- Light: URP, one sun with four soft shadow cascades, HDR, ACES tone mapping, ambient occlusion, fog, a graded look, point lights for fires and torches.
- Motion: no skeletons. Bodies bob and lean by code.

## What Skyrim has that we do not, in order of how much it shows
1. **Characters with skeletons, faces and animation.** Walking, fighting, working, sitting, sleeping. This is the largest gap by far.
2. **Authored textures.** Painted or photo-scanned albedo, normal, roughness and occlusion at 1 to 2 K for every surface.
3. **Dense set dressing.** Hundreds of small modelled things: fences, barrels, carts, tools, cloth, food, rubbish.
4. **Vegetation.** Grass, weeds, hedges, several tree species with wind.
5. **Sky and weather.** A real sky, clouds, a moon, volumetric fog and light shafts.
6. **Water.** Reflection, refraction, flow, foam.
7. **Sound.** Not graphics, but half of how a place feels.

## What can be done from code alone (my side)
- More and better generated models, with bevels, sagging ridges and per-building variation.
- Terrain detail grass and a second and third tree, with a wind shader.
- A procedural sky and clouds, light shafts, a water shader for the Thames.
- Decals: mud at doors, soot over smoke holes, moss on north walls.
- Level-of-detail meshes and occlusion so a whole town stays fast.

## What cannot come from code and has to be brought in
- **Rigged, animated people and animals.** Options: Mixamo (free with an Adobe account, needs a rigged mesh), Unity Asset Store medieval character packs (paid, roughly 30 to 150 dollars each), or commissioned work.
- **Scanned materials.** Poly Haven and ambientCG are free and public domain. This session's network rules block them; allowing `polyhaven.com` and `ambientcg.com` in the environment, or downloading on the Mac, unlocks them.
- **Scanned props and plants.** Quixel Megascans (free for Unity users under its licence terms), or Asset Store packs.
- A decision on money and licences belongs to the owner. Nothing is bought or downloaded without being asked.

## Suggested order
1. Characters: pick one rigged medieval peasant and one soldier with a shared skeleton, wire walk, idle, work, sit, sleep, strike, block.
2. Materials: replace the five drawn surfaces and the three ground layers with scanned sets.
3. Vegetation and sky.
4. Set dressing for Wallingford, street by street.
5. Water, decals, sound.
