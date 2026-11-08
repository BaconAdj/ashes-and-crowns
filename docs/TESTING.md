# Testing script (first open after Phases 1–11)

Everything below runs in the editor on the England scene unless it says otherwise. Delete old saves first:
`~/Library/Application Support/<company>/Ashes and Crowns/saves/` (Mac). Report console errors with the file and line.

## 0. Open and rebuild
1. Pull the branch. Open `UnityProject/` from Unity Hub (6000.x). Wait for the import and the bootstrap log `project setup verified`.
2. If the console shows compile errors, send them all before playing anything.
3. Menu `Ashes and Crowns > Rebuild > England Scene`, then `Battlefield Scene`, then `Siege Scene`. Each logs where it saved.
4. If Blender art exists (`Assets/Art/**/*.fbx`), the console lists `using Blender asset ...` lines during rebuild.

## 1. Run start (Phases 2–3)
- Play England. Create a character (any start). The opening narration plays; the HUD shows BLOOD, STAMINA, BELLY.
- Esc opens the "leave your affairs" screen; choose one, stop Play, Play again: the "While you slept" chronicle appears.

## 2. Village (Phases 4–5)
- E near the alehouse: dialogue, market (buy bread, eat), rumour. E near the workplace: a work day with the moment mini-game. Sunday mass at the church.
- M: the map. C / F / B / H / Alt+D: character, family, baronage, holdings, the war.
- Ride (E near the horse) to a town; trade and take a job.

## 3. Tester's menu (F10)
F10 opens a menu that jumps to late content. Use it to reach each phase quickly:
- "+100 pennies", "Skip a day", "Skip thirty days" (watch for chronicle notices, plots, raids, realm news).
- "Enlist with a company" then E at an alehouse: "Report to the muster: march to battle" (Phase 8 grunt mode: ring, banner, sergeant shouts, provost afterwards, R to march home).
- "Make me a knight" then at a town or castle: E, "Seek an audience": knighthood/command, land for service, court an heiress.
- "Besiege the nearest hostile castle": the daily siege menu; "Storm the walls" loads the siege scene (wall, ladders, gate, ram).
- "Make this settlement my estate" then E: "Go up to the hall" (policy, manor court, allegiance, council if monarch).
- "Raiders in the village now", "A plot against me", "The levy at my door".
- "Teleport to the nearest town or castle" / "Teleport home".

## 4. Battlefield scene (Phases 0, 6, 7)
- Play `Phase0_Battlefield`: ride to the hill for command, select units, charge; morale and rout; the battle report.
- Watch the fps counter with 1,000 a side; report the number.

## 5. What to report
- Console errors and warnings (copy the text).
- Anything that soft-locks (a dialogue that will not close, a menu that reopens forever).
- Numbers that feel wrong: prices, wages, how fast hunger bites, how often plots and raids happen.
- Looks: what reads as mud, iron and cold light, and what still reads as grey boxes.
