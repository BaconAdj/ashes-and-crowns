# Model pipeline (Tools/blender)

Small Python scripts generate every model in the game as Wavefront OBJ. Blender is **optional**: the geometry is built by
the pure-Python `MeshBuilder` in `common.py` (boxes, battered boxes, tubes, cones, ellipsoids with noise, gable roofs,
pyramids, wedges), written with normals and two material groups. If Blender is on PATH the same run also writes an FBX
next to each OBJ; Unity's builders look for `.fbx` first and `.obj` second, so either works.

- Run all: `Tools/blender/build_all.sh` from the repo root (uses `blender` or `$BLENDER` when present, else `python3`).
- Run one: `python3 Tools/blender/gen_hut.py -- --out UnityProject/Assets/Art/Buildings/Hut.obj`
- Conventions: 1 unit = 1 m. Origin at the ground centre. Modelling space is Blender's (x right, +y forward, z up); the
  writer swaps to y-up. Slot 1 is `Walls`, slot 2 is `Roof`; Unity maps them to palette materials (wattle/thatch,
  stone/slate, cloth/skin, coat/tack, bark/leaf, cloth/iron). The dark second slot is also used for doors, windows,
  arrow slits and tack so openings read as openings without extra materials.
- Triangle budgets: soldier about 660 (instanced a thousand times), everything else under 1,000.
- After generating: in Unity run `Ashes and Crowns > Rebuild > England Scene` (and Battlefield / Siege). The console
  says `using Blender asset ...` for each file it found.
- `body.py` holds the shared 1.75 m human used by `gen_man.py` and `gen_soldier.py`.

The OBJ files are committed (small text). FBX files are not (Git LFS); they are a local convenience only.

## History
The first art set was flat boxes and its OBJ writer oriented faces before swapping axes, which left every model
inside-out in Unity (dark, featureless masses). The writer now orients faces in the written space and emits normals.
