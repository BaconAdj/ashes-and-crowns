# Ashes and Crowns: The Anarchy — Game Design Document (Master v2, Unity)

> This is the full master prompt, saved verbatim as the design source of truth. It supersedes all earlier versions, including the Three.js master prompt. Edit this file only when the design itself changes, and log the change in `DECISIONS.md`.

Build Ashes and Crowns: a 3D open-world medieval permadeath simulation set in England, 1135 — The Anarchy. Zero magic, zero fantasy — gritty historical realism.

This is a fresh start in Unity. The earlier Three.js + Vite prototype (a horse riding between two boxes) is being retired. Build in the phases listed at the end, keeping the project opening, compiling and playable after every phase.

## How we work (read first)

I'm relatively new to terminal and git workflows. Handle git for me and explain in one line what each git step did.

### Repo and git

* Before touching anything, create a git tag `threejs-prototype` on the current state, then create a branch `unity-rebuild`. Move the old Three.js code into `/legacy-threejs/` (or delete it from the branch — ask me which). Never force-push or rewrite history.
* Commit at the end of every working milestone with a clear message. Push to `BaconAdj/ashes-and-crowns` only when I say so.
* Add a Unity-appropriate `.gitignore` (Library/, Temp/, Obj/, Build/, Logs/, UserSettings/) and set up Git LFS for `.fbx`, `.blend`, `.png`, `.wav`, `.ogg` before the first asset lands.

### Project memory

* Create `CLAUDE.md` at the repo root with: the engine and versions, folder layout, coding standards, the rules in this section, and how to run tests and builds. Keep it updated as decisions change.
* Save this full prompt as `docs/GDD.md` (the design source of truth) and keep a short `docs/DECISIONS.md` log of anything we decide along the way.

Use the installed plugins and skills. Load the relevant skill before doing that kind of work:

* Unity (official plugin): `new-unity-project` to scaffold; `unity-cli` for batchmode builds, test runs and imports; `unity-package-management` for packages; `urp-postprocessing` for the grim look; `initialize-ai-navigation` for NavMesh; `physics-3d-collision` for melee hit detection, terrain and building collision; `ui-uitk` for all HUD and menus; `audio-setup-mixers` and `optimize-audio` for bells, battle audio and ambience; `localization` (we'll want English and French later — set it up early, ship English first); `asset-transformer-toolkit` for importing Blender models.
* unity-perf: `csharp-zero-gc` and `unity-performance` for anything that runs per frame, especially the battle engine; `code-standards` for reviews.
* superpowers: `brainstorming` before any phase whose design is still fuzzy; `writing-plans` → `executing-plans` for each phase; `test-driven-development` for the save system and simulation rules; `systematic-debugging` when something breaks; `verification-before-completion` before telling me a phase is done.
* engineering: `architecture` / `system-design` at the start of the battle engine and the world simulation; `code-review` at the end of each phase.
* Blender (local MCP): if Blender tools are available in this session, use them to model, preview and export assets. If they aren't, write Blender Python scripts in `/Tools/blender/` and run them headless (`blender -b -P script.py`) to generate and export assets.
* Context7: look up current Unity, URP, Burst, Jobs and package docs instead of relying on memory.
* Firecrawl: use it for historical research (people, places, castles, dates, customs of 1135–1154) when filling `/Data/` files. Cite sources in `docs/sources.md`.

Unity editor limits. You can't click inside the Unity editor. Prefer code-driven setup (editor scripts, `[MenuItem]` tools, ScriptableObject generators, batchmode). When a step truly needs the editor, stop and give me numbered click-by-click instructions, then wait.

After each phase: summarize what was built, how to test it (exact steps), what's stubbed, and what I should look at. Then stop and wait for me before continuing.

## Technical foundation

* Engine: Unity 6 LTS (check the installed version with `unity-cli` and use it), URP, C#. Target desktop (Windows and macOS), built for Steam later. No browser build.
* No backend. No Supabase, no accounts, no server. Everything is local. Steam Cloud can be added at release.
* Architecture: keep the simulation separate from the engine.
   * `Assets/Scripts/Sim/` — plain C# (no `UnityEngine` references where possible), its own assembly definition: calendar and bells, economy, disease, wounds, reputation, crime, conscription, diplomacy, war, court, baronage generation, history divergence. Fully unit-testable.
   * `Assets/Scripts/Game/` — Unity-side: world, player controller, horse, camera, interaction.
   * `Assets/Scripts/Battle/` — RTS command layer and soldier perspective.
   * `Assets/Scripts/Combat/` — personal melee.
   * `Assets/Scripts/Trades/` — the eight trades' hands-on activities.
   * `Assets/Scripts/Save/` — persistence and permadeath.
   * `Assets/Scripts/UI/` — UI Toolkit screens and HUD.
   * `Assets/Data/` — `world.json`, `figures.json`, `backgrounds.json`, `items.json`, and a single `balance.json` holding every tunable number. Load via a typed data layer; no magic numbers in code.
   * `Assets/Art/`, `Assets/Audio/`, `Tools/blender/`.
* Tests: Unity Test Framework. EditMode tests for every Sim rule and the entire save system; PlayMode tests for the death flow and battle smoke tests. Runnable from the command line via `unity-cli`.
* Deterministic simulation: seeded RNG per run so bugs are reproducible from a seed.
* Battle performance — architect for it from day one:
   * Up to ~1,000 soldiers per side; target 60 fps at ~2,000 units on a mid-range PC.
   * GPU-instanced rendering for rank-and-file (`RenderMeshInstanced` or Entities Graphics — choose with the `architecture` skill and record the decision), with vertex-animation textures instead of per-soldier skeletons.
   * Burst + Jobs for movement, morale and targeting; formation-block movement with per-soldier offsets; flow fields or formation steering for armies, NavMesh agents only for individuals near the player.
   * Full individual logic only near melee contact and near the player; spatial hashing; LOD and culling.
   * `soldiersPerUnit` in `balance.json` (1 rendered = N men) as an escape hatch.
   * Zero per-frame garbage in battle code (`csharp-zero-gc`).
* Art direction: grim and muted. Low-poly is fine but desaturated — mud, iron, blood, cold light. Fog, rain, snow, day/night driven by the bell system. No cartoon colours. Build a URP post-processing profile early (colour grading, fog, film grain, vignette).
* Asset pipeline: models made in Blender (via MCP or scripts) exported as FBX into `Assets/Art/`, with consistent scale (1 unit = 1 m), named materials and a shared muted palette texture. Placeholder primitives are fine until a phase needs real art. Narration and sound can be generated later outside Claude Code (ElevenLabs) — leave clearly named audio stubs and a `docs/audio-list.md` of what's needed.
* Controls: WASD + mouse third-person, horse riding, RTS camera in command mode. Design input with Unity's Input System so a controller can be added later.

## Setting: The Anarchy

King Henry I is dead with no legitimate son. His nephew Stephen of Blois has seized the crown; his daughter Empress Matilda claims it as her birthright. England slides into nineteen years of civil war — barons switching sides for profit, unlicensed castles rising everywhere, mercenaries roaming, law collapsing. The chroniclers wrote that Christ and his saints slept.

* Real geography: a scaled-down England. Key locations in `world.json`: London, Winchester (the treasury), Bristol and Gloucester (Angevin strongholds), Oxford, Lincoln, Wallingford, plus ~8 procedurally named villages, the Thames, forests, marsh and roads. Geography is fixed every run — players learn England across lives.
* Real major figures, present every run as full NPCs: King Stephen, Empress Matilda, Robert of Gloucester, Henry of Blois (Bishop of Winchester, kingmaker), Geoffrey de Mandeville (the great side-switcher), Ranulf of Chester, Queen Matilda of Boulogne, King David of Scotland. Each gets a personality profile and goals in `figures.json`.
* Procedural minor baronage: every run regenerates the minor lords, their houses, personalities, feuds and initial allegiances. The great conflict is fixed; the chessboard beneath it never repeats.
* History diverges. 1135 is the starting condition, not a script. Matilda may take London this life; Stephen may die at Lincoln; a fictional claimant may end them both. Nothing is fated.
* The Church shapes daily life: church bells are the game's clock (dawn, midday, evening, audible everywhere); Sunday mass matters (attendance affects standing; churches are sanctuary); festival days (Michaelmas, Christmas, Easter, harvest feasts) are real events with markets, melees and hiring fairs.

## Core fantasy

One life in a collapsing kingdom. Death — sword, fever, noose, knife, hunger — permanently ends the run and wipes everything. No win condition; endless. The player climbs as far as one life allows.

The central theme is experiencing both sides of war: as a peasant or sellsword you are the body sent up the ladder, at the whim of lords who'll never learn your name. Rise, and you become the one on the hilltop sending named men to their deaths. Same battle system, seen from below and above.

## Permadeath, saves and the living world (build first, make it airtight)

### Ironman save

* No manual saves, no save slots, no reload. One hidden save per run, stored locally in `Application.persistentDataPath`.
* Autosave silently on every significant state change, every 30 seconds, and on quit — including mid-battle, so quitting or crashing can never escape a losing fight. Reopening resumes exactly where the player was.
* Write saves atomically (write to a temp file, flush, then rename over the old one) so a crash or power cut never corrupts or loses the run. Keep one rolling backup that is only used if the main file fails to load.
* Add a checksum to discourage casual save editing or copying. Don't over-engineer this; a determined player can always copy files.
* On death, delete the save (and its backup) synchronously before the death screen renders.
* Death screen: the life lived — days survived, trade mastered, peak wealth, titles, battles, men lost under your command, how history diverged, cause of death — and one button: "Begin Again." Pure reset.

### The world keeps moving while the game is closed

* The save stores a real-world timestamp. On relaunch, the simulation catches up for the elapsed time at an abstracted rate defined in `balance.json` (e.g. one real hour ≈ N in-game hours, capped so a long absence can't simply erase a run).
* Harvests come in, prices move, armies march, raids happen, the steward acts, NPCs scheme — resolved in the abstract Sim layer, not real-time.
* Going to bed for real means your character goes to bed too. Quitting is a diegetic choice: before leaving, the player chooses how to leave their affairs — sleep at home, post guards, leave a steward in charge, fortify, keep travelling, hide. Rank changes the options. Dangers during the absence roll against those choices and where the player is. Logging off carelessly in hostile country can mean waking to bad news — or not waking at all (the death screen on relaunch).
* On relaunch, show a short "While you slept…" chronicle of what happened, in the same somber tone as the opening narration.
* Quitting mid-battle or mid-fight is not a safe exit: the fight is resolved against the player as if they stood still or fled, depending on context.
* Test all of this exhaustively: crash mid-write, crash mid-battle, clock changes, very long absences, death during absence.

### The start of each run

* Full character creation: name, appearance (simple presets and sliders), a background, attribute allocation (Strength, Endurance, Charisma, Cunning). Skills grow with use (one-handed, two-handed, polearm, bow, riding, trade skills, medicine, leadership); attributes gate skill growth. Aging and maiming erode attributes over a long life.
* Backgrounds (3–4 per rank in `backgrounds.json`) set trade or situation, skills, and family. Family is in-run only (no dynasty carryover): spouses, parents, siblings and heirs are real NPCs who can be married off, taken hostage, die of plague, or scheme against you.
* Random rank-appropriate spawn (peasants in a random village, lords in a random minor castle).
* Opening: 3–5 lines of somber narration over a slow establishing shot, then cold reality. No tutorial chain; minimal contextual hints.
* Five free starting ranks — rank is the only difficulty setting:

#### 1. Peasant — the ground floor

A free tenant (no serfdom in this game) with a hut, a few coins and a trade. Dawn bells wake you; your trade's routine runs semi-abstracted (choose the day's focus, time flows) but is punctuated by hands-on moments — stalking a deer through morning fog, hammering a blade at the forge, netting the weir. Market visits with real haggling; evening at the alehouse where rumour travels; Sunday mass where the village sees whether you came.

* Eight playable trades: farmer, blacksmith, miller, hunter, brewer, carpenter, shepherd, fisherman. Each has distinct hands-on activities, tools, price points and social standing. Hunting is open to all.
* Apprenticeship: change trades by convincing an NPC master to take you on — seasons of reduced income and obedience before you're competent.
* Living market economy: supply and demand per settlement, seasonal swings, war shocks (an army foraged the next valley → grain triples; refugees flood in → labour is cheap, food is not), haggling as a skill-driven interaction.
* Informal village standing: respected vs. despised, tracked through deeds, mass attendance, debts honoured, fights, generosity. No formal offices — reputation is the office.
* War comes to the village: foraging armies strip granaries, raiders burn what they can't carry, levy quotas drag men off (comply, buy your way out, or flee as a deserter), refugee columns carry news, disease and desperate bargains.

#### 2. Warrior — a free sword

Weapon, armour, small savings. Freelance contracts (caravan guard, garrison duty, bounties, escort) from notice boards and taverns, or sign with a mercenary company — a captain, comrades, shared loot, shared fate, and contracts you don't choose. Rare festival melees offer prize money, reputation and genuine injuries.

* Knighthood requires all three: deeds (conspicuous, witnessed valour), equipment (warhorse and mail), and a patron (a lord willing to dub you). Any two without the third leaves you a well-armed commoner.

#### 3. Knight — sworn and owned

A household knight: you live in your lord's hall, eat at his table, and are always on call — escort, patrol, castle guard, messages through hostile country, standing behind him at feasts. Court intrigue in balance — rival knights, the lord's favour, whispered factions — but war dominates.

* Three paths to lordship: service (a manor granted for loyalty), marriage (wed an heiress — the Charisma road), or seizure (build or take an unlicensed adulterine castle and claim land, gambling no one strong enough objects before you're entrenched).

#### 4. Lord — the murky middle

A small castle, a taxpaying village, a garrison, rivals. Declare for Stephen, declare for Matilda, stand neutral while both sides pressure you — or secretly deal with both, like the real barons did. Secret dealings can be discovered (a captured courier, a bribed servant) with catastrophic consequences.

* Estate: the steward handles routine; you set policy (tax rates, granary reserves, garrison size, building projects) and handle crises personally — failed harvests, plague, a foraging army at the border, a levy demand from your liege.
* Manor court: sit in judgment over generated cases — theft, land disputes, assault, a widow's petition. Verdicts shape loyalty, standing, and how your village behaves when raiders come.

#### 5. King or Queen — the contested crown

Play Stephen (crowned, doubted), Matilda (rightful, resisted), or a fictional third claimant (a bastard of Henry I with full character creation).

* Everything at once: baron loyalty (patronage, land grants, hostages, forgiving traitors you can't afford to punish), the war (mustering, campaigns, sieges), coin (the Winchester treasury, debasement, taxation that breeds revolt), and the Church (Henry of Blois can legitimise or excommunicate; bishops are kingmakers).
* A monarch who loses everything becomes a fugitive — hunted, with the block likely if caught.

## Death comes from everywhere

Combat (battles, duels, ambushes, sieges, being run down while routing) — Disease (plague spreads between settlements; wounds infect; camps breed fever) — Starvation (hunger meter; winter prices; foraged-out villages) — Execution (theft, murder, desertion, treason, unpaid debts → arrest, trial, the block; deposed lords and kings included) — Assassination and betrayal (rivals, scheming relatives, disloyal vassals; subtle warnings exist for attentive players) — and dangers while you're away (see the living world above).

## Wounds and the body

Wounds persist, heal slowly with rest and treatment, and can infect and kill days later. Severe wounds can permanently maim — lost eye, limp, ruined arm, facial scars (feared by some, respected by soldiers). Track injuries per body part. No health regen in combat, ever.

## Serving in war — the grunt's side (core system)

* Conscription: peasants can be forcibly levied (armed men arrive with a quota — comply, pay scutage, or flee as a wanted deserter). Warriors enlist voluntarily for wages and loot shares.
* In battle as a soldier you hold a place in a formation and do not command. Orders arrive diegetically — horns, drums, your sergeant screaming, the banner moving — with a subtle UI backup (a small order banner: "HOLD", "ADVANCE", "LOOSE", soft waypoint). You see enemy cavalry wheel toward your flank and can only hope the man on the hill saw it too.
* Free to disobey — but discipline is brutal. Break formation, hide or flee, and you may survive the battle only to face flogging or the noose. Refusing a suicidal charge is a real dilemma: the charge might kill you; the provost definitely will.
* Deeds are witnessed. Kills, holding the line, first over the wall — commanders remember names. This feeds the deeds requirement for knighthood.
* If your side breaks, you rout with the rest — a terrifying playable pursuit. Captured commoners face mercy, ransom (if anyone would pay), or the sword. Nobles are usually worth ransom; you may not be.
* Between battles: camp life, late wages, short rations, camp fever, loot division.

## Climbing — blood or gold

Blood: valour gets you noticed — knighted on the field, granted manors, raised high. Fastest ladder, deadliest. Gold: wealth buys the gear that upgrades your enlistment, buys land, influence, and titles from crowns that need money. Rising flips which side of the battle system you play.

## Waging war — the commander's side

* Formal war: allegiance and claims justify campaigns; declared wars preserve reputation. Vassals can be called; refusing your liege has consequences.
* Raiding and banditry: burn, rob, kidnap — fast money, outlaw status, bounties, the noose.
* Logistics (moderate): armies eat food and wages daily; unpaid or starving troops desert or mutiny; rain and snow slow marches; winter campaigns bleed armies.
* Diplomacy (deep): ransom captured nobles (major income; killing valuable prisoners ruins you), truces, tribute, territorial concessions, vassalage, marriage alliances — including marrying off your own family.
* The weight of command: post-battle reports name the dead, including veterans you've fought beside. Levied villages empty. Widows petition your manor court. Sending men to die is mechanically easy and narratively heavy — that contrast is the game.

## Battles — Total War command, bound to your body

Real-time 3D battles on terrain, up to ~1,000 soldiers per side. Two command states only:

* In command (you lead this army and you're at a command position — a vantage away from the fighting): full RTS keyed like Total War — click banners (Shift adds), Ctrl+A all, Return none, Ctrl+0..9 saves a group and 0..9 recalls it; RMB move, Alt+drag move facing the drag, C/F charge, H hold, Backspace halt, J about face, U withdraw to the rally point, Ctrl+Left/Right wheel, Shift+1..4 line / wedge / square / shieldwall; WASD or arrows pan, Q/E rotate, wheel or Z/X zoom, Home finds your body, K hides the HUD, P pauses. Tactical camera.
* Ride into the fight and you lose all command. Camera drops to third-person melee; the army runs on its last orders via sergeant AI. Fighting personally raises army-wide morale, but the army is tactically headless. To command again, physically ride back through a moving battle to a safe vantage.
* As a soldier in someone else's army you never command — you live or die by the AI commander's competence.
* Death on the field, in either role, ends the run.
* Morale and routing (full realism): per-unit morale (casualties, flanking, fatigue, weather, commander presence, allies fleeing). Broken units rout; cavalry runs down routers (brutal, effective, reputation-staining); surrounded units surrender into prisoners. Battles end in morale collapse, not annihilation.
* Brutality: blood decals, bodies stay, screams and steel (audio stubs fine), named casualty lists.

## Personal melee — For Honor on a keyboard

Third-person real-time melee for all personal fights: in the line, duels, ambushes, assassinations, brawls, breaches, festival melees. The layout is For Honor's:

* Hold **Ctrl** for guard mode: lock on to the nearest opponent, the camera holds them in frame, the body squares up. Push the mouse **up, left or right** to set the stance (three guards; there is no low guard).
* **LMB** light attack (quick, cheap). **RMB** heavy attack (slow, hard, can be feinted). A heavy begun in the attacker's line just before the blow lands is a **parry** (long stagger).
* **E** or middle mouse: **guard break** when free; **feint** while a heavy winds up. Guard break beats a standing guard (opponent staggered and open to one free blow), loses to an attack, and is countered by a guard break pressed as it lands.
* **Space** dodge (invulnerable frames). **R** revenge when the meter fills from blocks and hits taken: stamina refilled, lights do not stagger you, blows hit harder, everyone in reach is knocked back. **Shift** sprint.
* Outside guard mode E talks, uses and mounts, and LMB still swings for the press of battle.
* Stamina, weight and wounds: weapon vs. armour matters; spears have reach; skills grow with use. Every hit taken is a persistent wound. Killing the surrendered stains you. The HUD shows three plates (the held stance bright, an incoming blow red, orange for a heavy) and the revenge bar.

## Places you can enter, people with lives

Every hut, alehouse and church is hollow with a real door. Inside: hearth, bed and chest; bar, casks and trestles; altar, font and benches. The things you do happen at those places: kneel at the altar, buy at the bar, sleep at your own hearth. Villagers keep a timetable by the bells (asleep until Prime, work until Vespers with dinner at Sext, the green, the alehouse or home, mass on Sundays and feasts) and walk it through the doors; each family has a station in its yard or out on the land, and the priest his altar. Ask anyone and you learn what they are doing; the asleep do not answer. Arriving anywhere shows its name and who holds it for whom. A peasant walks; a horse is bought or inherited with rank. You can jump, and climb a ledge you can reach.

## Hands-on living — nothing is a menu

Every activity is done at the place, in the body, with the clock running faster while you do it. Prayer: walk to the altar and **kneel**; favour grows the longer you stay (diminishing by the hour, capped per day) and whoever is in the nave carries the tale. Work: a day at the trade runs at speed with hands-on **beats** — a marker sweeps a band, press **X** on the moment: strike while the iron is hot, loose when the deer stills, judge the brew. The quality of your beats is the quality of the day. Esc abandons the day with nothing to show. Sleeping, eating and talking stay quick; smithing, praying, hunting, brewing are things you do.

## Sieges — both paths

* Blockade: weeks of game time; defenders starve, sicken, sortie, negotiate, surrender — while your army eats money in the rain.
* Assault: real-time battle on castle terrain — ladders, ram, siege towers built over days; walls, gates, chokepoints. As a grunt, being ordered up a ladder is the most lethal order in the game — and refusing has its price too.
* Captured castles: hold, garrison, or sack. Adulterine castles can be built on seized land during the chaos.

## HUD and screens

HUD (UI Toolkit): health, hunger, wounds, gold, date / season / next bell, rank or title, threat indicator; grunt-mode order banner in battle. Screens: map (M), character and skills, family, holdings and troops ledger, diplomacy, manor court (lord and above), the "leave your affairs" screen on quit, and the "While you slept…" chronicle on return.

README: controls, the command-position mechanic, the soldier-service system, permadeath and the living world.

## Build phases (keep the project playable after each; stop after each)

**Phase 0 — Vertical-slice test (prove Unity is the right call).** Scaffold the Unity URP project with `new-unity-project`, set up git and LFS, create `CLAUDE.md` and `docs/`. Build one small battlefield on terrain, a rideable horse with a third-person controller, ~50 instanced soldiers per side that take simple move/charge orders from a command vantage, and the switch into personal melee (basic swing, block, hit, wound) when you ride in. Muted URP look. Measure fps and report it. Stop and let me play it before Phase 1.

**Phase 1 — World foundation.** Terrain from an England heightmap (scaled), day/night, weather (fog, rain, snow), church bells as the clock, one village with a church, alehouse and huts. Blender pipeline proven with a first real asset (a hut or the church).

**Phase 2 — Save system and living world.** Ironman autosave, atomic writes and backup, checksum, mid-battle persistence, synchronous death deletion, death screen, real-time catch-up simulation, "leave your affairs" on quit, "While you slept…" on return. Test exhaustively (EditMode + PlayMode).

**Phase 3 — Run start.** Character creation, backgrounds and family generation, rank selection (including Stephen, Matilda, fictional claimant), random spawn, procedural minor-lord generation over the fixed major figures, narrated opening.

**Phase 4 — Survival and the peasant core.** Hunger, health, disease, seasons, wounds / infection / maiming, skills by use; the eight trades (abstract routine + hands-on moments), apprenticeship, living market with haggling and war shocks, village standing, mass and festivals.

**Phase 5 — England.** Full map, settlements, trade routes, jobs, global reputation, crime and execution.

**Phase 6 — Personal melee, full depth.** Directional combat, duels, ambushes, festival melees.

**Phase 7 — Battle engine, full scale.** 1,000-per-side instanced battles, RTS layer, positional command, morale / rout / run-down / surrender, prisoners. Hit the 60 fps target at ~2,000 units and report profiler numbers.

**Phase 8 — The soldier's war.** Conscription and enlistment, mercenary companies, AI-commanded battles from the ranks (diegetic orders + UI backup), discipline and desertion, camp life, capture outcomes, witnessed deeds → knighthood (deeds + equipment + patron) and purchased ascent.

**Phase 9 — Sieges.** Blockade and real-time assault, both perspectives; adulterine castle building.

**Phase 10 — Lordship and crown.** Household knight service and court intrigue, three paths to lordship, estate policy + steward + crises, manor court, full-murk allegiance (including secret double-dealing and discovery), the monarch layer (baron loyalty, treasury, Church, campaigns), divergent-history simulation of the civil war.

**Phase 11 — Betrayal and polish.** Assassination and betrayal (including scheming relatives), refugee / foraging / raid events on villages, polish (blood, fog, bells, audio, paranoia UI), French localization pass, and a balance pass across all five starts.

**Phase 12 — The household and motion.** Duties under a patron; procedural gait; the eleven generated models.

**Phase 13 — Hands and steel (after the owner's first play).** For Honor melee on a keyboard; hands-on prayer and work with the clock sped up; Total War command keys; legible nights and torches.

**Phase 14 — A day in the life (after the owner's second play).** Enterable buildings with interiors; villagers with timetables who walk their day; rest, meals, mass and ale done in place; a peasant without a horse; jump and climb; the arrival banner; women in the village; firewood as a chore.

**Next — Phase 15, to be agreed in the morning.** Candidates: the keep's hall and a town around each castle; villagers trading and gossiping with each other and reacting to crimes; body animation for fighting and climbing; a shared palette texture pass; sound.

Start with Phase 0 now. Begin with the git safety steps, then scaffold. Stop at the end of Phase 0 with the summary and test steps.
