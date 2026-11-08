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

## 2026-11-08 — Phase 2 save format and ironman rules
- Binary save (`SaveCodec`): magic `ASHC`, version, length, FNV-1a 64 checksum, then a fixed-order payload. Chosen over JSON so the Save assembly stays engine-free (no JsonUtility), stays small (a 2,000-soldier battle is under 100 KB) and encodes deterministically. The checksum discourages casual editing; it is not anti-cheat.
- `SaveStore` writes `run.tmp` with write-through and flush, renames the previous `run.sav` to `run.bak`, then renames the temp over `run.sav`. Load prefers main, falls back to backup, and always discards a leftover temp. Delete removes all three synchronously; the death screen is only shown after that returns.
- Files live in `Application.persistentDataPath/saves/`. One run per machine; no slots.
- Autosave: every `save.autosaveIntervalSeconds` (30), on wounds, bells, kills, mount changes, on pause, on quit. A run is written on its very first frame so a crash before the first autosave still leaves a run.
- Scenes are self-contained: each scene has a SaveManager. On launch the first SaveManager loads the save; if it belongs to another scene it hands the state to that scene through `RunSession.PendingResume` and loads it.
- Leaving your affairs (`AffairsChoice`): peasants get Sleep at home (within `absence.homeRadiusMetres` of the village), Keep travelling, Hide. Mid-battle: Stand or Run. Closing the window without choosing is `None` (careless) and mid-battle counts as standing still. Rank widens this list later.
- Catch-up (`AbsenceResolver`): elapsed real time × `simulation.realHoursToGameHours`, capped at `maxCatchUpGameDays`. Per game day a danger roll by choice × hostility; outcomes are an attack (wounds), a fever, or a robbery. Resting choices heal. Quitting mid-battle applies blows from the enemies that were within reach. Death during absence deletes the save and shows "You did not wake" with the chronicle.
- Clock moved backwards or no elapsed time: nothing happens. Very long absences hit the cap and the chronicle says so.

## 2026-11-08 — Phase 2 static review applied
- No compile errors. Applied: event saves coalesce into one write within `save.dirtySaveDelaySeconds` (no fsync per blow); dead-on-resume now puts the battlefield into Dead mode; a save for a scene missing from the build no longer gets applied to the wrong scene; the run seed is restored from the save so forked rng streams match; `RunSession` statics reset when domain reload is off; battle import validates every array length; PlayMode tests use DestroyImmediate and a frozen clock.

## Phase 2 summary (what exists, what is stubbed)
- Built: Save assembly (RunState, codec, store) with 15 EditMode tests; AbsenceResolver with 7 tests; snapshots for rng, weather, body; BattleDirector export/import; SaveManager (autosave, resume across scenes, death deletion, quit screen, chronicle, Begin Again with a fresh seed); HUD chronicle and quit overlays; 3 PlayMode tests. 71 tests run under .NET, plus 5 PlayMode in Unity.
- Stubbed: coins (robbery reports pennies but no wallet exists yet), steward/guards/fortify choices (need rank), named character, peak wealth, titles, "how history diverged" on the death screen.

## 2026-11-08 — Phase 3 run start
- Character creation is one UI Toolkit screen inside the England scene, shown by the SaveManager when no save exists. The battlefield test scene creates a default warrior silently.
- Backgrounds live in `backgrounds.json` as parallel key/value arrays (JsonUtility cannot read dictionaries). Attribute budget: base 4 in each of four attributes plus 10 free points, each between 3 and 9. Background bonuses add on top.
- Skills (18) grow with use through `CharacterSheet.Use`; the governing attribute scales the gain and gains diminish with level. Riding and one-handed are hooked up now; trades come with Phase 4. Aging erodes attributes past 50; maiming costs a point.
- Family is generated from the background's template words; relations, ages and two traits each. In-run only.
- The minor baronage (14 by default) is regenerated per run from `names.json`: house, seat, traits, declared and secret allegiance (west of the divide leans Matilda, east leans Stephen), loyalty, ambition, knights, mutual feuds. Major figures stay fixed from `figures.json`.
- Spawn: peasants and warriors in a village (only Ashby exists), knights and lords at a castle stub (Wallingford, Oxford, Lincoln, or a town), monarchs at London (Stephen), Gloucester (Matilda) or Bristol (the claimant). Every non-village location now has a procedural keep stub and a spawn point; Phase 5 replaces the stubs.
- Save format bumped to version 2 (character, family, baronage, spawn, opening seen). Version 1 files still load and get a default character.
- Opening: four narration lines over a slow push-in, skippable. Audio stubs are logged by name.

## Phase 3 summary (what exists, what is stubbed)
- Built: creation screen (name, sex, rank, background, attributes, presets and sliders), run factory, appearance applied to the placeholder body, narrated opening, info panels (C character, F family, B baronage), skill growth hooks, castle stubs and spawn points, save v2, 10 new tests (81 under .NET) plus an editor test that validates every committed JSON file through JsonUtility.
- Stubbed: real character model, NPC bodies for family and lords (they are records only), castle interiors, monarch court, coins as a usable wallet.

## 2026-11-08 — Phase 3 static review applied
- No compile errors. Applied: info panels stay closed in command mode (C is charge there) and close when the quit screen opens; quitting during character creation no longer writes a characterless save; a failed character creation re-opens the screen; the placeholder body scales from `player.height` in balance.

## 2026-11-08 — Phase 4 survival and the peasant core
- Hunger (`Needs`) drains per game hour and starvation drains blood; eating is any food item (G eats the best you carry). Rest is tracked for work output; sleeping at home until Prime restores it and lets wounds heal.
- Wounds can turn septic for six days after they are taken; septic wounds drain blood daily and may clear; binding wounds at home (medicine skill grows) cuts the risk to a quarter. Tuned so an untreated bad wound kills a few men in a hundred within a month.
- Disease (`DiseaseState`): flux and fever are rare in a village and three times likelier in a camp; plague follows the settlement's outbreak level. Recovery is better resting at home.
- Festivals (`Festivals`): Sundays, fixed feasts and Easter and Whitsun from the Julian computus (Easter 1136 is 22 March). Mass is sung between Terce and Sext; attending raises standing, a missed Sunday lowers it; working on a holy day costs standing.
- Market (`Market`): price = base × (demand × baseline / stock)^0.6 × food season × war shock × standing; merchants buy at 70%. War shocks: foraging triples grain and empties food stock; refugees make food dear and goods cheap; shocks decay daily. Haggling is a probability from skill, standing and greed. The village market is seeded from `items.json` categories.
- Trades (`trades.json`, `TradeWork`): output = base × skill × tool × season × hands-on moment × rest × hunger × weather; inputs consumed (iron, grain, malt, timber); skill grows per day; small injury chance. Apprenticeship: convince a master (charisma, their disposition, your standing), then two seasons at half output.
- Village masters are placeholder capsules by the green, one per trade, with names and dispositions; the workplace is one bench behind the player's hut for now.
- Save format v4 adds needs, sickness, inventory, standing, apprenticeship, market stocks and shock, outbreak and the last mass day. Older files load with defaults.

## Phase 4 summary (what exists, what is stubbed)
- Built: hunger, rest, starvation, sepsis and treatment, three diseases, church year and mass, items and inventory, village market with seasonal prices, shocks and haggling, eight trades with a daily routine and a timing-based hands-on moment, apprenticeship, village standing, starting kits per rank, HUD belly bar, standing line, prompts, dialogue, market and moment overlays, inventory (I), eat (G). 17 new Sim tests (98 under .NET).
- Stubbed: distinct hands-on activities per trade (one timing mini-game stands in for stalking, hammering and netting), per-trade workplaces, village events (war comes to the village: Phase 11), coins as physical loot, selling to other settlements (Phase 5).
