# Decisions log

Short entries, newest at the bottom. Each: date, decision, why, consequences.

## 2026-11-07 — Retire the Three.js prototype, rebuild in Unity 6 URP
- Tag `threejs-prototype` marks the last Three.js state; code moved to `legacy-threejs/` (not deleted) so it stays greppable. Delete later if it becomes noise.
- Work happens on branch `unity-rebuild`. Pushes go to the branch the cloud session was opened on (`claude/loving-turing-l9hc7l`) by fast-forwarding it to `unity-rebuild`, since that is the only branch the session may push to; the user decides when.

## 2026-11-07 — Unity project in a subfolder
- The Unity project is `UnityProject/`, not the repo root, so docs, tooling and the legacy prototype do not get imported as assets.

## 2026-11-07 — Code-driven project setup instead of the Unity CLI
- The authoring environment (cloud container) has no Unity editor, no Unity CLI and no Blender. Instead of `unity projects create`, the project is a hand-written minimal Unity project (ProjectVersion + manifest) plus an `[InitializeOnLoad]` bootstrap that creates the URP asset, enables the Input System, makes UI Toolkit panel settings and builds the Phase 0 scene on first open. All of it is idempotent and also reachable from the `Ashes and Crowns` menu and from batch mode.
- A hand-written `ProjectSettings.asset` ships with `activeInputHandler: 2` (both input systems) and linear colour space, so the first Play has input without a restart. If Unity rejects the file (version skew), delete it: Unity regenerates defaults and the bootstrap sets the input handler and warns to restart.
- Consequence: Phase 0 code is unverified in an editor until the user opens it. `.meta` files are generated on first open and must be committed then.

## 2026-11-07 — Data files live in Assets/Data and are read as plain files
- `balance.json` etc. are read with `File.ReadAllText` from `Assets/Data` in the editor (hot-editable, no reimport) and from `StreamingAssets/Data` in builds (copied by a build preprocessor). `JsonUtility` fills plain `[Serializable]` classes with public fields; the same classes deserialize with System.Text.Json in the .NET test harness.

## 2026-11-07 — Battle rendering: `Graphics.RenderMeshInstanced`, not Entities Graphics
- Chosen for Phase 0 through Phase 7 start: no ECS dependency, works with plain NativeArrays + Burst jobs, one draw per side per 1023 instances. Revisit at Phase 7 if 2,000 animated soldiers with vertex-animation textures need BatchRendererGroup; the SoA layout makes that swap local to `BattleDirector.Render`.
- Soldiers are structure-of-arrays NativeArrays. Jobs: hash (parallel) → targeting (parallel, 3x3 cell lookup in a `NativeParallelMultiHashMap`) → move (parallel, terrain heightfield copy) → combat (single-threaded IJob, writes other soldiers' health) → unit summary → matrices.
- `soldiersPerUnit` in balance is the 1-rendered-equals-N-men escape hatch; Phase 0 uses 1.

## 2026-11-07 — Command rule
- Command mode is purely positional: inside `command.vantageRadius` of the vantage marker you have the RTS layer; outside you do not. WASD still moves your body in command mode (so you can walk or ride off the hill); the RTS camera pans with the arrow keys and zooms with the wheel.

## 2026-11-07 — Sim tests also run under .NET 8
- `Tools/dotnet-tests` compiles `Assets/Scripts/Sim` and `Assets/Scripts/Tests/EditMode/Sim` with NUnit so the rules layer can be verified without an editor. The Unity Test Runner runs the identical files.

## 2026-11-07 — Phase 0 static review applied
- Independent review found two compile errors (duplicate `_fps` field in the HUD; PlayMode test asmdef missing Unity.Collections) and robustness items (non-throwing `TryReloadAllNullIn`, re-run mode switch on mount, selection array sized to unit count, ask before discarding a dirty scene, drop obsolete Lightmapping flags). All applied.

## Phase 0 summary (what exists, what is stubbed)
- Built: project skeleton; Sim (rng, balance + validator, formations, combat math, wound model) with 32 passing tests; Battle (50 v 50 instanced soldiers, Burst jobs, orders, formations, enemy sergeant AI, banners, selection rings); Game (player, horse, third-person camera, mode manager, bootstrap, perf probe); Combat (swing/block/wound melee); UI Toolkit HUD with death screen; editor bootstrap and scene builder; batch build scripts; PlayMode battle smoke test.
- Stubbed: Save, Trades assemblies (placeholders); calendar/bells; soldier animation (capsules); audio (none); blood decals (bodies stay, no decals); death only resets the scene (Phase 2 deletes the save).
- Not done here: opening in Unity, fps measurement, Blender pipeline.

## 2026-11-07 — Phase 0 verified on the user's Mac
- Unity 6000.6.8f1 opened the project first time with no compile errors; the bootstrap generated the scene. About 100 fps with 100 soldiers (worst frame 21 ms). Unity confirmed as the engine.
- Observed: the Phase 0 ground rendered glossy white (the mud Texture2D saved as `.asset` did not survive import) and the image was over-exposed. Fixed in Phase 1 by writing textures as PNG and lowering post exposure. Rebuild the battlefield scene to pick it up.
- The editor on the Mac is 6000.6, newer than the 6000.0 the manifest targets; Unity upgraded packages itself. ProjectVersion.txt is whatever the editor last wrote.

## 2026-11-07 — Phase 1 geography and scale
- England's outline comes from the public-domain `datasets/geo-countries` GeoJSON (Natural Earth derived), simplified to 420 points south of the Tweed and stored normalised in `Assets/Data/england_coast.json`. Uplands, lowlands and rivers are stylised radial bumps and polylines authored by hand from a map; no binary heightmap is committed. `EnglandTerrainBuilder` rasterises it at editor time (deterministic).
- Overworld scale: one Unity terrain of 8192 m covers 673 km, so 1 unit is about 82 m across country, while villages, battles and buildings are 1:1. Riding London to Bristol takes a few minutes at a gallop. This is the Mount-and-Blade style compression the GDD's "scaled-down England" implies. Phase 5 decides whether settlements need separate 1:1 local areas.
- Real heights are scaled by 0.12 (Snowdonia 950 m becomes about 114 m) over a 30 m land base; sea level is a flat plane at 12 m.

## 2026-11-07 — Phase 1 clock, light and weather
- Julian calendar in Sim (`Calendar`), starting Sunday 22 December 1135 (Stephen's coronation). Bells at 6, 9, 12, 15, 18, 21 named Prime, Terce, Sext, None, Vespers, Compline (the GDD's dawn/midday/evening plus three more; tune in balance.json). One real second is 1/90 of a game hour, so a day lasts 36 minutes.
- `Daylight` computes real solar elevation and azimuth for 52 degrees north; `DayNightController` drives sun, ambient and fog colour from it; weather is a per-month spell table (`WeatherModel`) blended smoothly. Debug keys `]` (+1 h) and `[` (+1 day) in the England scene.
- Buildings: procedural meshes in C# are the fallback; `Tools/blender/*.py` reproduce the same shapes as FBX. The village builder prefers FBX when present. The Blender run has to happen on the user's machine (no Blender in the cloud container).

## 2026-11-07 — Phase 1 static review applied
- Missing `using AshesAndCrowns.UI` in the England scene builder (compile error), inside-out triangle winding in the procedural buildings, snow emission capped by maxParticles, particle systems explicitly started. Noted for later: the England scene has no death path yet (nothing can wound you there until Phase 4).

## Phase 1 summary (what exists, what is stubbed)
- Built: England terrain from real coastline + stylised relief, sea plane, three terrain layers by height/slope; day/night with real solar geometry; weather spells with rain and snow particles and fog; church bells as the clock with HUD toast and audio stub; village of a church, alehouse, nine huts and a well on flattened ground; new England scene, menus, build settings; Sim tests (49 total) and a PlayMode clock test.
- Stubbed: bell audio clips, trees and forests, roads, rivers as visible water (they only carve the ground), other settlements, interiors, NPCs.
- Not done here: Blender run (scripts are ready), fps measurement of the big terrain.
