# Ashes and Crowns: The Anarchy

A 3D open-world medieval permadeath simulation set in England, 1135. One life in a collapsing kingdom. No magic.
Unity 6 (URP). Design document: `docs/GDD.md`.

## Opening the project (Phase 0)
1. Install Unity 6000.0 LTS via Unity Hub (any 6000.0.x).
2. Unity Hub > Add > select the `UnityProject/` folder, open it. First import takes a few minutes.
3. On load the project configures itself (URP, Input System, UI Toolkit) and builds `Assets/Scenes/Phase0_Battlefield.unity`. If the console says the Input System was enabled, close and reopen the project once.
4. Press Play. The England scene opens by default; the Phase 0 battlefield is under `Ashes and Crowns > Open Battlefield Scene`.

## Controls (Phase 0)
| Action | Keys |
|---|---|
| Walk / sprint | WASD / Shift |
| Look | Mouse (Esc frees the cursor) |
| Mount / dismount horse | E (within 3 m) |
| Ride | W/S speed, A/D steer, Shift gallop |
| Swing / block | Left mouse / hold right mouse |
| Command (only inside the ring on the hill) | LMB select unit banner, RMB move, C charge, H hold, 1–4 formation, arrows pan, wheel zoom |
| Leave your affairs (quit) | Esc, then 1 to 3 or click |
| Begin again after death | R |
| Debug time (England scene) | `]` skips an hour, `[` skips a day |

## The world clock
Church bells are the clock: Prime, Terce, Sext, None, Vespers and Compline ring from the village tower and the HUD names the next bell. A game day lasts 36 real minutes. The sun follows real solar geometry for southern England; weather changes in spells of a few hours, with rain, snow and fog by season.

## The command position
You command your army only while you stand inside the ring at the standard on the hill. Ride down into the fight and you lose the tactical camera and every order you could give; the army keeps its last orders and the sergeants fight on. To command again, ride back up. As a soldier in another's army you never command.

## Permadeath and the living world
There is one hidden save per run and no way to load, copy or reload it from inside the game. It is written every 30 seconds, on every wound, bell, kill and mount, and when you quit, including in the middle of a battle. When you die the save is deleted before the death screen appears. R begins a new life with a new seed.

Press Esc to leave. You choose how you leave your affairs: sleep at home, keep travelling, or hide; in a battle, stand your ground or run for it. The world keeps moving while the game is closed: one real hour is two game hours, capped at ten game days. Dangers roll against your choice and where you were. On return a short "While you slept..." chronicle tells you what happened. Logging off carelessly in enemy country can mean waking wounded, robbed, or not at all. Quitting mid-fight is resolved as if you stood still.

Soldier service (conscription, diegetic orders, discipline) arrives in Phase 8.

## Development
- `CLAUDE.md` holds the working rules. `docs/DECISIONS.md` logs decisions.
- Sim tests outside Unity: `dotnet test Tools/dotnet-tests`.
- The old Three.js prototype is in `legacy-threejs/` and at tag `threejs-prototype`.
