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
| Swing (direction from mouse motion: up high, down low, sideways) | Left mouse |
| Guard in a line (its first instant is a parry) | Hold right mouse |
| Dodge (on foot) | Space |
| Command (only inside the ring on the hill) | LMB select unit banner, RMB move, C charge, H hold, 1–4 formation, arrows pan, wheel zoom |
| Character / family / baronage | C / F / B |
| Map | M |
| Inventory / eat | I / G |
| Use a place or speak (church, alehouse, hut, work bench, villager) | E when the prompt shows |
| Hands-on moment | Space on the beat |
| Leave your affairs (quit) | Esc, then 1 to 3 or click |
| Begin again after death | R |
| Debug time (England scene) | `]` skips an hour, `[` skips a day |

## Starting a life
With no save on disk the England scene opens on character creation: name, sex, one of five ranks (the only difficulty setting), a background, ten attribute points, and a few appearance presets. A short narration follows, then the world. Peasants and warriors wake in the village; knights and lords at a castle; Stephen in London, Matilda in Gloucester, the claimant in Bristol.

## The peasant day
Dawn bells wake you. At the bench behind your hut, E works the day: a short hands-on moment sets the quality, ten hours pass, goods land in your pack. The alehouse buys and sells at prices that move with the season, stock and war; haggling is a skill. Sunday mass between Terce and Sext is seen by the village; standing opens doors and lowers prices. Wounds must be bound at home or they turn bad. Eat, or starve.

## Serving in the ranks
Armed men may come for you with a quota: go, pay, or run. In towns a mercenary company may take you on. At the muster you march to battle as a body in a line: a ring marks your place, the banner and the sergeant tell you what to do, and you do not command. Break ranks and the provost hears of it; hold the line and the sergeant names you. Lose, and you may be ransomed, let go, or killed where you kneel. Deeds, a warhorse and mail, and a lord who likes you make a knight.

## Battles
Units have morale. Losses, being flanked or surrounded, fatigue, rain and snow, and friends running all erode it; a commander on the hill or in the line, and enemies seen running, restore it. Below a threshold a unit is shaken; lower still it routs, flees the nearest enemy and hears no orders. Men who catch runners cut them down. A shaken unit that is surrounded surrenders and becomes prisoners. Battles end when one side has nothing steady left; the report names the dead the sergeants knew.

## Fighting
Every blow has a line: high, left, right or low, read from how the mouse moves as you press. Guard the same line to block; a guard raised just as the blow lands parries and staggers the attacker; a shield covers both sides. Dodge costs stamina and buys a quarter second. Mail and helm turn most of a sword; a bare head does not. Men who are losing may yield. Killing them then is murder in the eyes of the village and the Church.

## England
Eight villages beside the real towns and cities, each with its own market, lord and allegiance. Prices differ by region, so goods carried across the country sell. The alehouse offers work: letters, caravan guard, escort, garrison days, labour, bounties. Walk into the destination to be paid. Steal and be seen and the reeve will come for you; the Church gives forty days of sanctuary, no more. Commoners hang; nobles lose their heads.

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
