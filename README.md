# Ashes and Crowns: The Anarchy

A 3D open-world medieval permadeath simulation set in England, 1135. One life in a collapsing kingdom. No magic.
Unity 6 (URP). Design document: `docs/GDD.md`.

## Opening the project (Phase 0)
1. Install Unity 6000.0 LTS via Unity Hub (any 6000.0.x).
2. Unity Hub > Add > select the `UnityProject/` folder, open it. First import takes a few minutes.
3. On load the project configures itself (URP, Input System, UI Toolkit) and builds `Assets/Scenes/Phase0_Battlefield.unity`. If the console says the Input System was enabled, close and reopen the project once.
4. Press Play. If the scene isn't open: menu `Ashes and Crowns > Open Phase 0 Scene`.

## Controls (Phase 0)
| Action | Keys |
|---|---|
| Walk / sprint | WASD / Shift |
| Look | Mouse (Esc frees the cursor) |
| Mount / dismount horse | E (within 3 m) |
| Ride | W/S speed, A/D steer, Shift gallop |
| Swing / block | Left mouse / hold right mouse |
| Command (only inside the ring on the hill) | LMB select unit banner, RMB move, C charge, H hold, 1–4 formation, arrows pan, wheel zoom |
| Begin again after death | R |

## The command position
You command your army only while you stand inside the ring at the standard on the hill. Ride down into the fight and you lose the tactical camera and every order you could give; the army keeps its last orders and the sergeants fight on. To command again, ride back up. As a soldier in another's army you never command.

## Soldier service, permadeath, the living world
These arrive in Phases 2 and 8 (see `docs/GDD.md`): one hidden ironman save per run, deleted on death; the simulation catches up for real time spent away; conscription, diegetic orders and discipline.

## Development
- `CLAUDE.md` holds the working rules. `docs/DECISIONS.md` logs decisions.
- Sim tests outside Unity: `dotnet test Tools/dotnet-tests`.
- The old Three.js prototype is in `legacy-threejs/` and at tag `threejs-prototype`.
