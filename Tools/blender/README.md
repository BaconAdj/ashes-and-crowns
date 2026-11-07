# Blender asset pipeline

No Blender was available in the Phase 0 authoring environment, so this folder only fixes the conventions.

- Scripts run headless: `blender -b -P Tools/blender/<script>.py -- --out UnityProject/Assets/Art/<Category>/<Name>.fbx`
- 1 unit = 1 metre. +Y up on export (`axis_up='Y', axis_forward='-Z'`), apply scale, apply unit scale.
- Materials by name only; one shared muted palette texture `Assets/Art/Palette/palette.png` (to be made in Phase 1). No per-asset textures.
- Export FBX with `use_selection=True`, `mesh_smooth_type='FACE'`, no cameras or lights, bake_anim only for rigs.
- First real asset (Phase 1): a village hut, then the church.
