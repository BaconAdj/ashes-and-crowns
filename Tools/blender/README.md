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

## Art set (added after the eleven gameplay phases)
`build_all.sh` now also generates `Characters/Soldier.fbx` (spear and shield, instanced a thousand times), `Characters/Man.fbx`
(the player, villagers), `Characters/Horse.fbx`, `Nature/Oak.fbx`, `Siege/WallSegment.fbx`, `Siege/Tower.fbx`,
`Buildings/Keep.fbx` and `Props/Cart.fbx`. Every consumer falls back to the old primitives when a file is missing, so the
scenes build either way; the console lists each FBX it found. Slot 1 is always `Walls` and slot 2 `Roof` so one importer
rule maps them to palette materials. Blender 4.x; run from the repo root on macOS:

    BLENDER=/Applications/Blender.app/Contents/MacOS/Blender Tools/blender/build_all.sh
