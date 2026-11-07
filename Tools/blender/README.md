# Blender asset pipeline

Headless scripts that generate the village buildings as FBX. The Unity village builder prefers
`UnityProject/Assets/Art/Buildings/<Name>.fbx` and falls back to the identical procedural mesh
(`ProceduralBuildings.cs`) when the FBX is missing, so the game works with or without Blender.

- Run all: `Tools/blender/build_all.sh` (set `BLENDER=/Applications/Blender.app/Contents/MacOS/Blender` on macOS).
- Run one: `blender -b -P Tools/blender/gen_hut.py -- --out UnityProject/Assets/Art/Buildings/Hut.fbx`
- Conventions: 1 unit = 1 m. Origin at the ground centre. Blender +Y (front) exports to Unity +Z. Two material slots named `Walls` and `Roof`; Unity remaps them to the palette materials (wattle/thatch or stone/slate). Flat shading. No cameras, lights or animation.
- FBX files are tracked by Git LFS (see `.gitattributes`).
- After generating: in Unity run `Ashes and Crowns > Rebuild > England Scene`. The console says `using Blender asset ...` for each FBX it found.
- Next assets (Phase 4/5): market stall, fence, cart, hay rick, a shared muted palette texture `Assets/Art/Palette/palette.png`.
