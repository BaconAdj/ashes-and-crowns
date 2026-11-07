# Decisions log

Short entries, newest at the bottom. Each: date, decision, why, consequences.

## 2026-11-07 — Retire the Three.js prototype, rebuild in Unity 6 URP
- Tag `threejs-prototype` marks the last Three.js state; code moved to `legacy-threejs/` (not deleted) so it stays greppable. Delete later if it becomes noise.
- Work happens on branch `unity-rebuild`. Pushes go to the branch the cloud session was opened on (`claude/loving-turing-l9hc7l`) by fast-forwarding it to `unity-rebuild`, since that is the only branch the session may push to; the user decides when.

## 2026-11-07 — Unity project in a subfolder
- The Unity project is `UnityProject/`, not the repo root, so docs, tooling and the legacy prototype do not get imported as assets.

## 2026-11-07 — Code-driven project setup instead of the Unity CLI
- The authoring environment (cloud container) has no Unity editor, no Unity CLI and no Blender. Instead of `unity projects create`, the project is a hand-written minimal Unity project (ProjectVersion + manifest) plus an `[InitializeOnLoad]` bootstrap that creates the URP asset, enables the Input System, makes UI Toolkit panel settings and builds the Phase 0 scene on first open. All of it is idempotent and also reachable from the `Ashes and Crowns` menu and from batch mode.
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

## Phase 0 summary (what exists, what is stubbed)
- Built: project skeleton; Sim (rng, balance + validator, formations, combat math, wound model) with 32 passing tests; Battle (50 v 50 instanced soldiers, Burst jobs, orders, formations, enemy sergeant AI, banners, selection rings); Game (player, horse, third-person camera, mode manager, bootstrap, perf probe); Combat (swing/block/wound melee); UI Toolkit HUD with death screen; editor bootstrap and scene builder; batch build scripts; PlayMode battle smoke test.
- Stubbed: Save, Trades assemblies (placeholders); calendar/bells; soldier animation (capsules); audio (none); blood decals (bodies stay, no decals); death only resets the scene (Phase 2 deletes the save).
- Not done here: opening in Unity, fps measurement, Blender pipeline.
