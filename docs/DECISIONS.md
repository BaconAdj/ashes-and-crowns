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

## 2026-11-08 — Phase 4 static review applied
- No compile errors. Applied: Esc no longer closes a dialogue and opens the quit screen in one frame (Q closes talk); dialogue hotkeys are inert while the market or the hands-on moment is open; `VillagerTag` moved to its own file (Unity drops a second MonoBehaviour per file on reload); no dialogue over the death screen after sleeping or working; dead feast line removed; goods unknown to a market are recreated on load; haggle rng seeded from a stable hash; village keys ignored during the opening.

## 2026-11-08 — Phase 5 England
- Settlements: the real places from `world.json` plus eight procedural villages per run, placed on land away from the coast and each other, named from the place lists, held by the nearest minor lord. Procedural villages are built at play time by `SettlementSpawner` (huts, alehouse, church, flattened disc) because they change with the seed; fixed places keep their keep stubs and get a site marker.
- Markets differ by region: coastal and river places have cheap fish, uplands cheap wool and cheese, villages cheap grain and timber, towns and cities cheap tools, weapons and (cities) armour. Supply scales stock and demand runs inversely, so carrying goods between regions pays (`BestCargo`).
- Travel: 28 km a day on foot, 48 mounted; a job's deadline is travel days × 1.8 + 2.
- Jobs at the alehouse: message, caravan guard (weapon needed), escort (warriors and above), garrison duty and labour (served day by day on the spot), bounty (Phase 6 supplies the fight). Delivery happens when you walk into the destination settlement. Deadlines fail quietly and cost standing and reputation.
- Global reputation with Crown, Angevins, Church and Commons plus notoriety; deeds move pairs in opposite directions; slow decay.
- Crime: theft at the alehouse (witnesses roll to report), wanted level per settlement, arrest rolls on arrival and daily while you stay, trial by rank and standing: fine, flogging, mutilation (poaching), outlawry, hanging (commoners), beheading (nobles). Forty days of sanctuary in a church, once per crime, then it runs out. Execution ends the run through the body ("the noose", "the block").
- Save format v5: settlements with markets and boards, reputation, crimes, sanctuary, active job, home settlement. Older saves rebuild England from the seed.
- The map (M) is drawn at runtime from the coast polygon; dots coloured by allegiance; gold is the job destination.

## Phase 5 summary (what exists, what is stubbed)
- Built: settlement generation, regional markets and trade opportunities, travel maths, job board and contracts, global reputation, crime/arrest/trial/execution, runtime village spawner, settlement sites with arrival toasts, map screen, info panel additions, save v5. 9 new tests (105 under .NET).
- Stubbed: roads and trade routes as visible paths, caravans and escorts as travelling NPCs (guard and escort jobs pay on arrival with no fight yet), bounties without a target (Phase 6), town interiors and markets as places (a site marker stands in), outlaw hunters, bailiffs as NPCs.

## 2026-11-08 — Phase 5 static review applied
- No compile errors. Applied: other settlements' alehouses and churches are usable and trading uses the market you stand in (the trade loop works); arrests are deferred out of the clock's day event and an execution kills before its dialogue; runtime village flattening works on an instantiated copy of the terrain data (the asset no longer gets dented); the map draws at 384 with a neighbour-based coastline (no distance pass); sanctuary and the daily law check use whichever church you are in; map side length cached; stable string hash in Sim.

## 2026-11-08 — Phase 6 personal melee
- One fight model for everyone (`FighterState`): windup, strike, recover, guard with a parry window at its start, dodge with invulnerable frames, stagger, yield, down. Stamina gates swings, guards and dodges; an empty guard breaks. Timings and costs in `balance.duel`.
- Direction: four lines (high, left, right, low) chosen by the mouse's motion as you press. A guard in the same line blocks; a shield also covers the other side. High blows find the head more often, low blows the legs; armour per region from the items carried (mail, gambeson, helm, shield).
- Damage = weapon × (1 + skill × 0.006) × part multiplier, reduced by armour; 40 raw is a killing blow to the head. Spears reach further.
- Opponents (`FighterBrain`) read your windup with a skill-based chance and guard the right line, dodge sometimes, attack in reach, and may yield below 30% blood. Striking a man who has yielded kills him and costs reputation with the Church and the Commons and standing.
- Encounters: ambushes on dangerous jobs (roll every six hours on the road), hunters after outlaws, the bounty's quarry when you reach the destination, the festival melee at the alehouse on fair days (three bouts, prize twelve times the stake). Bandits may rob and leave you when you are nearly down. Loot from the dead.
- No Animator or root motion yet: the placeholder weapon pivot poses per phase. Hit detection is rule-based on reach and arc, not weapon colliders; both move to the animation pass once real models exist.

## Phase 6 summary (what exists, what is stubbed)
- Built: directional melee with parry, block, dodge, stamina and armour by region; NPC duellists; four encounter kinds; yield and mercy; weapon skill growth per kill; HUD fight line. 7 new tests (114 under .NET).
- Stubbed: animations and weapon colliders, duels of honour by challenge, assassinations, mounted combat (no dodge in the saddle, no lance), ambush terrain and bandit camps, loot beyond coins.

## 2026-11-08 — Phase 6 static review applied
- One compile error (nullable tuple member access in the loadout refresh). Applied: the fighter state is the single owner of the player's stamina (sprint drain honoured, hunger slows recovery, no double regen); dead duellists fall; a yielded man can only be killed within reach; encounter rng is one stream advanced per use, with the bandits' robbery rolled per second; the robbery path no longer double-ends the fight; bodies are cleared without leaking registrations.

## 2026-11-08 — Phase 7 battle engine at scale
- Default armies are now 1,000 a side in 20 units of 50 (`battle.soldiersPerSide`, `battle.unitSize`); `soldiersPerUnit` stays the escape hatch. The jobs and the spatial hash were built for this; the rendering is one instanced draw per 1,023 soldiers.
- Morale runs four times a second per unit on the main thread (40 units: trivial). Perception is unit-to-unit: enemy centroids within 40 m are classed front, flank or rear by the unit's facing; flanked = anything on a flank or behind; surrounded = front and rear both occupied. Commander presence applies to the player's side when commanding within range, or fighting personally anywhere.
- Routing units take a flee point away from the nearest enemy and ignore orders; soldiers in routing or surrendered units neither target nor strike; pursuers who reach a running man kill him with a per-attack chance (run-down). Surrendered units stand still and are not targets: prisoners. Routers rally to shaken when left alone.
- The battle ends when one side has no steady or shaken unit left. The report names the dead among the sergeants and veterans (`UnitRoster`), counts the fled and the taken, and shows on the chronicle overlay.
- Save format v6 adds unit stance and morale to the battle snapshot.
- Not measured here: fps. The PlayMode scale test logs the mean frame time for 2,000 soldiers; the user's machine supplies the number.

## Phase 7 summary (what exists, what is stubbed)
- Built: morale, rout, rally, surrender, run-down, prisoners, battle end and report with named dead, commander presence, 1,000-a-side default, scale smoke test, stance in the command summary and the save.
- Stubbed: vertex-animation textures (capsules still), cavalry as a unit type (run-down is done by whoever reaches the router), flow fields (formation steering only), LOD meshes, profiler numbers (the user runs the scale test and reads the log), ransom values for prisoners (Phase 10).

## 2026-11-08 — Phase 8 the soldier's war
- Service (`ServiceRecord`, `ServiceRules`): levy demands arrive at your home village (a daily roll while you are a commoner and not already serving); comply, pay scutage, or run and become a deserter (a known crime). Mercenary companies appear in towns and cities with a captain, an employer and a contract; signing pays three pennies a day when the paymaster has silver. Camp days: late pay and short rations roll; camp fever through the disease model's camp multiplier.
- Marching to battle loads the battlefield scene as a grunt: the sergeant AI commands your side, you are placed in the front rank of the first unit with a soft ring on your slot, the banner (HOLD, ADVANCE, CHARGE, RETREAT) comes from your unit's order and stance with a shouted line and a named audio stub. No command; ModeManager's vantage is disabled.
- Discipline accrues while you are out of your slot and your unit still stands (doubled in contact). The provost afterwards: praise if the sergeant saw deeds, flogging above twenty points, the rope above ninety. Deeds: witnessed kills near your unit, holding the line thirty seconds in contact, standing while your unit ran. Renown counts toward knighthood.
- If your side breaks you are captured when your unit surrendered or you were caught among enemies while routing: ransom for nobles or anyone with twenty pennies, mercy sometimes, the sword otherwise, escape rarely.
- Knighthood needs all three: renown, a warhorse and mail, and a patron whose favour (from renown, reputation with his side and standing) is high enough; the audience at a castle or town dubs you and makes you a household knight on two pennies a day.
- Return from the battlefield lands you at home with a chronicle of the day. The run context persists across scene loads; the save carries service and companies (format v7).

## Phase 8 summary (what exists, what is stubbed)
- Built: levy, enlistment, companies, camp days, desertion, grunt mode with banner, shouts, slot ring, discipline, deeds, provost, loot share, capture, knighthood audience, save v7. 7 new tests (127 under .NET).
- Stubbed: horns and drums (audio stubs only), the player's unit chosen by role (always the first unit), travel to the battle (instant scene change), archers and "LOOSE", purchased titles (Phase 10), warhorse purchase only through the market when a city stocks it.

## 2026-11-08 — Phase 7 review fixes
- Run-down kill chance is now per attack (1 - exp(-rate * attack interval)) computed once in Setup, not per frame; it was framerate-dependent and about ten times weaker than the balance value.
- Setup resets morale-side state (named dead lists, morale timer, commander flags, weather) so a second battle in the same process starts clean.
- A unit wiped out in one morale step now reports its final named dead before being marked Destroyed.
- Rosters carry a PlayerSide flag and plain unit names ("first spears"); the report lists the player's dead by side instead of sniffing strings.
- Byte-array codec helper is null-safe; ImportSnapshot raises UnitStanceChanged for restored non-steady units; the 2,000-soldier PlayMode test runs at 4x time scale with a ten-minute timeout.

## 2026-11-08 — Phase 9 sieges
- `Sim.SiegeRules` runs the blockade as arithmetic per day: defenders' food, sickness (hunger and rain multiply it), morale; attackers' camp fever, coin per man per day, desertion when unpaid; sorties, engines rising (ladders daily, a ram in five days, a tower in twelve), relief as a daily chance scaled by the defending lord's strength; surrender when morale or food runs out. Terms (march out, prisoners) are a chance from their morale; "no quarter" is never accepted.
- The storm is the real battle: the siege scene is the Phase 0 field with a terrain plateau and a curtain wall (parapet, towers, gate) at a fixed z. Attackers may enter the wall band only at ladder columns or through a broken gate (`SiegeLayout` in the move job); men on the climb take double damage and deal half. Defenders hold the wall (AI) and never climb down. The ram: enough attackers at the gate foot for long enough breaks the gate (`StepRam`), which the scene hides.
- `BattleDirector.Setup` now takes per-side counts and a layout; units deploy in rows that fit the field width (fixes the Phase 7 one-row deployment of twenty units across a 400 m field).
- `Game.SiegeDirector` is the daily menu from either side: the commander waits, offers terms, storms or abandons; the grunt watches the captain decide (storm above 55 in a hundred odds, terms when weak, abandon when outnumbered) and may desert; the castle-builder holds, sorties or yields. Taken castles: hold for your lord, garrison, or sack (loot and a stain). Captured defenders of your own walls go through the Phase 8 capture roll.
- Adulterine castles: at a village, with a name men follow and 120 pennies, you start a motte; palisade and tower follow over two months of paid days while you are present. A nearby lord of the other side may object: pull it down, pay him off, or hold what you built (a siege against you with your hired garrison). Completion makes a Castle settlement with the player as lord (`LordId = -2`) and `Rank.Lord`.
- Save format v8 carries the siege, the castle build, the garrison and the gate state of a storm in progress.

## Phase 9 summary (what exists, what is stubbed)
- Built: blockade model, terms, engines, relief, sorties, storm resolution; the siege scene with wall, gate, ladders and ram; commander, grunt and defender flows; captured-castle fates; adulterine castle building with objections; save v8. 9 new tests (138 under .NET).
- Stubbed: siege towers are two extra ladders; defenders do not sortie in real time; "LADDERS" is the Charge banner (no separate audio line); the besieging army is abstract men until the storm; no travel time to the castle; captured castles are not yet estates (Phase 10).

## 2026-11-08 — Phase 10 lordship and crown
- Three paths to lordship, all through `LordshipDirector.Grant`: service (patron favour 0.9 and renown 60 at an audience: a village near the lord's seat), marriage (a suit to a lord's daughter: visits roll on Charisma and standing; consent needs favour 0.7 and a knight's rank or half the renown; the dowry manor comes with her), seizure (the Phase 9 adulterine castle becomes the estate the day the tower is roofed).
- The estate is daily arithmetic (`EstateRules.DayTick`): tax policy sets income per head and unrest drift; the steward's honesty decides how much reaches the chest (less skimmed when you are present); neglecting the manor court raises unrest; an empty granary too. Revolt above 0.85 and calm below 0.5. Crises arrive at 2.5% a day with three choices each, resolved by text with costs to chest, granary, population and reputation.
- Manor court sits on three cases drawn from six kinds; verdicts move unrest, the chest and the Church. Hanging a poacher is remembered.
- Allegiance in the murk: declare for either side or stand neutral; treat secretly with the other side for silver, which raises suspicion; discovery (a captured courier) is treason, a known crime, and the shire's law turns on you. Both sides send demands for silver; refusing costs their regard.
- The realm runs in seasons of 91 days (`RealmRules.Season`): lords drift to the stronger side by disloyalty and ambition (double-dealers faster); one campaign a season flips a town or fails for lack of money; the Church leans toward the winner. The crown treasury takes income and pays campaigns. The king's council (rank Monarch) grants land, takes hostages, pardons, debases the coin, levies tallage or appeases the bishops, each with a loyalty or Church cost. Everything the player does not touch still moves: divergent history.
- Save format v9 carries the estate, allegiance, suit, realm and the chronicle.

## Phase 10 summary (what exists, what is stubbed)
- Built: estate with steward, policy, granary, crises and revolt; manor court; three paths to lordship; secret dealing and discovery; demands; realm seasons with lord defections, campaigns and the Church; the king's council. 7 new tests (147 under .NET).
- Stubbed: household knight duties (escort, patrol, messages) are not separate activities, only the Household wage and the audience; court intrigue is the favour number; only one estate at a time; the monarch rank is reachable only through a fixed-figure start; the heiress is never seen in the world; the crown's treasury and the Winchester mint are numbers.

## 2026-11-08 — Phase 10 review fixes
- Losing your own castle clears the estate; crisis texts clamp unrest, granary and garrison afterwards; a lawful hanging records no deed; month-end for the estate is counted in estate days, not read off the caught-up calendar.

## 2026-11-08 — Phase 11 betrayal and polish
- Plots (`IntrigueRules`): rolled daily from power, notoriety and hostile relatives; a relative (poison, knife, or an heir usurping a lord), a disloyal vassal (open the gate, ambush), a feuding rival, or a stranger. Each plot has six to twenty days and gives up to three warnings, more often to the cunning, shown as a quiet italic line in the vitals panel (the paranoia UI) with a persistent "You are watched." after two. Counters: double the guard, a taster, confront a relative (right: exposed; wrong: a household insult), send one away. Resolution: death, a wound, a fight at the ford (the encounter system), a hall lost, or a foiled plotter marked for life.
- War comes to the village (`VillageEvents`): refugees, foragers, raids (real raiders in the lanes through the encounter director) and billets, each a market shock; the chance rises with last season's war heat from the realm chronicle.
- Polish: blood decals as a ring of instanced quads fed by the combat job's death positions (bounded native list, no allocation), the player's wounds and downed duelists; named audio stubs through `AudioStubs.Play` with a rate limit; siege horns and the gate crash.
- French: `L.T(english)` looks the English text up in `strings.fr.json` (parallel arrays) and falls back to English. HUD modes, hints, bars, market, quit screen, map caption and the paranoia line are translated; dialogue bodies stay English for now (ship English first, per the GDD). Menu `Ashes and Crowns > Language`.
- Balance pass across the five starts: a harness test eats two pottage a day at base prices for sixty days with sleep, work and the rank's living (sellsword wage, household keep, manor rent). Found and fixed: the smith's apprentice started with 9 pennies and could not buy a 12-penny bar of iron, so he starved; now 16. Result table (coins at day 60): farmer 98, smith alive, poacher 4 (thin, as intended), miller 74, warriors 9–24, knights flat, lords 420–520, monarchs 480–3880.

## Phase 11 summary (what exists, what is stubbed)
- Built: plots with warnings and counters, five outcomes; village war events with real raiders; blood decals; audio stub registry; French HUD strings and a language menu; start-balance harness; save v10. 8 new tests (156 under .NET).
- Stubbed: no NPC schemers you can watch in the world (plots are text and a fight); no fog or film-grain retune beyond the Phase 1 look; dialogue bodies untranslated; no audio files (stubs only); the balance pass covers survival and purse, not combat difficulty by start.

## 2026-11-08 — Phase 9 review fixes
- A cold launch straight into a saved storm now reads the siege from the pending save (the context is restored later), so the wall, ladders and army sizes match the save instead of a plain thousand-a-side field.
- Attackers below the wall who have a target above it steer to the nearest ladder or the open gate instead of freezing at the stone (`SiegeLayout.NearestCrossingX`); storms no longer stall.
- Defending your own walls: the besiegers giving up or being driven off by a relieving force now ends the siege instead of re-opening the menu forever; misleading mirrored log lines are filtered.
- `PendingBattle` is cleared once at battlefield start (`LastBattleLaunch` keeps it for the grunt's company size); an open objection survives save and load; a finished adulterine castle is a fixed site, not a procedural village, on reload.

## 2026-11-09 — Phase 12 the household and bodies that move
- Household knight duties (`Sim.HouseholdRules`): at your patron's seat you report for duty and roll one of five: escort a merchant to a town within six days, carry a sealed message through hostile country within five, ride the bounds for a day, hold the hall door for a night, stand behind the chair at a feast. Escorts and messages are real rides across England with ambushes on arrival at waypoints and a capture roll at a hostile gate (the Phase 8 capture rule, including the sword). Patrol may turn into a bandit fight; guard duty may surface a plot warning; the feast is a Charisma check against a rival's insult. Done duties raise patron favour and renown and pay; failing or refusing costs favour and marks discipline. Save format v11.
- Procedural motion: no rigs yet, so `ProceduralGait` bobs and leans the visual child of the player, horse and duelists from measured speed, with a standing breath; the soldier matrices job adds a march bob and sway to men who moved since the last frame. Zero allocations, no animator.
- Art without Blender: the generator scripts write OBJ when `bpy` is missing; the eleven OBJ twins are committed and the builders accept either extension.

## Phase 12 summary (what exists, what is stubbed)
- Built: five duty kinds with assignment, completion, failure, refusal and the feast; the household director in England with arrival hooks; procedural gait for bodies and the march bob for the ranks; the F10 shortcut to become a household knight. 3 new tests (159 under .NET).
- Stubbed: the escorted merchant and his carts are not visible on the road; the feast is text; no rigged animation; the courier's letter has no content that matters to the realm.

## 2026-11-09 — First open in Unity: compile fixes and the look pass
- Unity's compiler is stricter than the .NET harness about reused local names (CS0136) and about `Cursor` when UI Toolkit is imported; three such errors were fixed and a parser-based sweep (`Microsoft.CodeAnalysis`) over all scripts found no others. Rule going forward: never reuse a local's name in a nested block.
- Particle systems: all three velocity-over-lifetime curves must share one mode or Unity warns every frame; the weather now sets the emission rate only when it changes.
- Terrain paint: Unity 6 drops alpha maps assigned before the `TerrainData` exists as an asset. Both terrain builders now save the asset first; England gets its grass, mud and rock back.
- The look: every OBJ was inside-out (the writer flipped handedness after orienting faces), so models rendered as dark blocks. The generator was rewritten in pure Python with tubes, ellipsoids, battered walls, noise canopies and real normals; the eleven models are now low-poly but legible (timber posts, thatch hanging to head height, lancets, battlements, a palisade on the motte, a jointed man, a horse with a neck). Blender is optional. Wattle, thatch, stone, slate and the ground layers get generated normal maps. The grade is re-applied from `balance.json` on each rebuild (it used to be baked once), fog thinned from 0.008 to 0.0035, exposure raised from -0.6 to -0.25, saturation from -42 to -28, ambient lifted a little. Leaves and bark brightened (`OakLeaf`, `Bark`).
- Still boxy by intent: no rigs or textures per model yet; that is the next art step if the owner wants it (UV unwrap in the generator plus a shared palette texture).
- Start-of-run hang ("can't get up"): the HUD and the character screen share one `UIDocument`, and `HudController.Build` cleared the whole panel root. When `SaveManager.Start` ran before `Phase1Scene.Start`, the creation overlay was wiped, `_creationOpen` stayed true, body control stayed off and nothing was on screen. The HUD now builds inside its own container and never clears the root; the creation overlay re-attaches and comes to the front on `Show`; the SaveManager re-shows a lost creation screen; every state change (creation, narration, control, death) logs a `[run]` line so a stuck start is readable from the console.
- `PlayerAppearance.Apply` assumed a capsule body (0.75 scale, lifted 0.95 m). With the modelled figure it now scales uniformly by height and leaves the feet at the origin.
- Stale local FBX files (from an older Blender run) shadowed the freshly pulled OBJ models, so the town still looked boxy after a rebuild. `SceneKit.ArtMesh` now takes whichever twin was written last and warns about the skipped one. Panel settings moved to `Assets/UI/Resources` so the HUD can recover a lost reference at runtime; the HUD logs its panel and theme, and the bootstrap logs whether the Input System sees a keyboard. The tester's menu also opens on the backquote key for MacBooks without F-keys.
- Too dark on the owner's screen: exposure raised from -0.25 to +0.2, ambient and night floors lifted, vignette and grain eased, cloud dimming softened, the low-sun floor raised from 0.15 to 0.3. Still grey and cold, but readable. `Game.BrightnessControl` (= / -, Shift+0 resets) shifts the grade's exposure per player and remembers it in PlayerPrefs; it is a comfort setting, not a balance value.

## 2026-11-10 — Phase 13: hands and steel (owner's redirection after the first play)
- The owner's verdict after playing: pressing E to pray is not a medieval sim. Every action should be simmed and, where it can be, interactive; time may run faster so nobody is stuck at the anvil for real hours. Hence `ActivityDirector`: the body is posed at the place, `WorldClock.timeScale` rises (12x kneeling, 24x working) so needs, bells and weather tick honestly, and the player either stays as long as they choose (prayer: favour by `ActivityRules.PrayerGain`, geometric decay by the hour, capped per day, a token kneel earns nothing) or works through beats (`X` on a sweeping marker; six per day; misses cost). The old three-beat "moment" stays as a fallback when no director is wired. Prayer hours per day are tracked in the director, not the save (a reload resets them; accepted for now).
- Melee moved from a two-button Assassin's Creed sketch to the For Honor layout the owner specified: guard mode on Ctrl with lock-on, three stances from mouse motion (no low guard: `AttackDir.Low` survives for saves and the mass model, and a side guard takes a low blow), light/heavy, feint, guard break with counter and attack-beats-grab, dodge, revenge. Parry is a heavy in the same line inside the window, which replaces the old "first instant of a block" rule. NPC brains hold a stance, parry well-read heavies, feint into a committed guard and break a guard held too long. The rules stay pure and tested in `Tools/dotnet-tests` (167 tests).
- Command mode takes Total War keys. Formations moved from 1-4 to Shift/Alt+1-4 because digits now recall groups. Ctrl+M/C/B select nothing yet: there are no missile, horse or engine units in the ranks; that is a later phase. Alt+drag sets facing on a move; Ctrl+arrows wheel; U withdraws to the side's starting centre (`BattleDirector.RallyPoint`).
- Night: the owner wants dark but never blind, and a torch to be the real answer. Day grade restored to the grim numbers; moon raised to 0.35, night ambient and fog floors lifted; `TorchLight` on T burns a `torch` item (a penny at market, two game hours each). The live brightness keys were removed at the owner's request.
- Stale local FBX twins hid the new OBJ models after a rebuild: `ArtMesh` now takes whichever was written last and warns about the other.

## 2026-11-10 — Phase 14: a day in the life (owner's second redirection)
- The owner, after kneeling "outside nowhere": buildings must be entered and lived in, NPCs must have lives, a peasant must not start with a horse, the player must jump and climb, arriving somewhere must say where and whose. All of it is the sim the owner wanted from the start; the menus were scaffolding.
- Buildings: the generator grew a `room()` primitive (four slabs with real openings), so hut, alehouse and church are hollow with doorways and windows that are holes. Interiors are primitives placed by `BuildingKit` in local space from the same numbers the generator uses, with named markers the game and the villagers steer by. One kit serves the baked Ashby and the runtime villages.
- People: `VillageDay` is a pure timetable (tested); `Villager` walks it with a CharacterController, door-in/door-out routing and a raycast side-step; beyond 160 m they are placed, not walked, so a county of villages costs nothing. Talking to someone tells you what they are doing; the asleep will not talk. The priest keeps his altar.
- Horse: a rouncey is an item (120 pennies); knights and above ride by right. The scene horse is hidden until owned.
- Movement: Space jumps outside guard mode (it dodges inside it); a ledge between 0.9 and 2.3 m with headroom is mantled over half a second.
- Known gaps, by choice: villagers do not yet trade, gossip between themselves or react to crimes; the keep has no interior; the mantle has no animation beyond the slide. Save format unchanged; villagers are regenerated from the seed.
- Night work after the owner slept: rest-like spans (`ActivityDirector.BeginRest`) make sleep, the midday meal, mass and a pot of ale happen in place at the bed, hearth, nave and bench with a pose and the clock running fast (40x, 8x, 12x, 8x); leaving mass before the elevation counts as missed. Camera pulls in on walls so interiors can be seen. A woman model (`gen_woman.py`) for wives, daughters and the alewife. Firewood: any oak, an axe, two hours of beats, bundles by quality, a penny each. HUD panels take a hairline of old gold and small-caps headers. Tester's menu gained a rouncey, jumps to dusk, night and Sunday mass, and a bandit. The keep's hall interior is deferred: the motte keep's door is on the first floor and needs the stair.
