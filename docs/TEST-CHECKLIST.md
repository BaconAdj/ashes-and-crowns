# Morning test checklist (Phases 13–14)

Pull, let Unity compile, then `Ashes and Crowns > Setup Project` and the three `Rebuild` items. Delete the old save
(`~/Library/Application Support/BaconAdj/Ashes and Crowns/saves`) so you begin as a fresh peasant. Open the tester's menu
with the ` key (above Tab) whenever a step says "cheat".

Send a screenshot or a pasted red console line for anything that fails.

1. **Start.** Character screen appears; pick a peasant. Narration plays over the village; any key skips. No horse stands by you.
2. **Arrival banner.** "ASHBY" with its holder fades in and out when you walk in from outside the village (walk out 80 m and back).
3. **Walk into your hut.** The door opening on the front; inside a hearth with embers, a pallet, a chest. Prompt "Your hearth" by the fire. "Sit and eat" sits you by the fire for half an hour of game time in a few seconds. "Lie down until Prime" lies you on the pallet; the night passes; E wakes you early.
4. **The village lives.** Morning: people at anvils, benches and fields; the priest at the altar. E near someone: the first line says what they are doing. At Sext they go home; late afternoon the green; evening the alehouse. Cheat "Jump to dusk" and watch them move.
5. **Church.** Walk in by the south door. Prompt "Kneel at the altar" only at the altar. Kneel; the panel counts minutes; rise with E; the dialogue tells you who saw. Cheat "Jump to Sunday mass": everyone walks to the church; stand through mass or slip out early.
6. **Alehouse.** In through the porch. Prompt "The alewife's bar" at the counter. Ale sits you on a bench for an hour of game time.
7. **Horse.** No horse. Cheat "Give me a rouncey": a horse appears outside; E mounts.
8. **Jump and climb.** Space jumps. Run at the alehouse porch step or a cart and you mantle up. Try the church plinth.
9. **Night and torch.** Cheat "Jump to night": dark but legible. T lights a torch (the cheat gave you two); it gutters after two hours.
10. **Fight.** Cheat "A bandit on the road". Hold Ctrl: camera locks, plates appear. Move the mouse up/left/right to change stance; match his red flash to block. LMB light, RMB heavy, E guard break, Space dodge, R when the bar fills.
11. **Firewood.** Buy an axe at the market (or cheat coins), stand at an oak: "Cut firewood". X on the beats. Bundles appear in the inventory (I).
12. **Command mode (battlefield scene).** Open `Phase0_Battlefield`. At the vantage: Ctrl+A, Return, RMB move, Alt+drag, C charge, J about face, Q/E rotate camera, K hide HUD, P pause.

Known gaps tonight: the keep has no interior; villagers do not trade or gossip with each other; climbing has no arm animation; fighting has no body animation beyond the weapon.

## Phase 16: doors, locks, Wallingford

Setup: pull, then `Ashes and Crowns > Setup Project`, `Rebuild > England Scene`, `Rebuild > Wallingford (the town at full size)`. Delete the old save.

### Doors (England scene, Ashby)
1. Begin a life. Walk at your own door from inside: it swings outward and you walk out. Turn round and walk in: it swings inward.
2. Walk into the alehouse and the church the same way. Nothing blocks the doorway; a villager in your way steps aside.
3. Wait for night (the tester's menu on the ` key jumps to night). Walk at a neighbour's door: it stays shut. The prompt offers Knock, and with a knife, Slip the bar.
4. Slip the bar: move the mouse (or A and D) until holding E carries the bar all the way. Off the mark it binds and the bar reddens; ease off.
5. Inside, the chest offers Rifle the chest. A sleeper may wake.
6. Your own door never bars you out.

### Wallingford
1. `Ashes and Crowns > Open Wallingford`, press Play, make a character. You begin at a hearth on St Mary's Street.
2. Walk north to the crossroads, east along the High Street to the Bridge Gate and over the bridge.
3. Walk to the market place (stalls, well, cross), into St Mary-le-More, into an alehouse.
4. Walk up Castle Street to Castle Lane and the castle gate; look for the motte and keep.
5. Watch the people: houses near you have folk in them, they walk along the streets, not through walls.
6. Find a milestone outside a gate: E takes the road to England. At Wallingford's keep in England, F goes back down into the town.
7. Note the frame rate in the market and on the High Street.

### What to look at
- Do the textures (daub, thatch, stone, slate, boards) sit right on the buildings, or are any stretched or sideways?
- Is anything pink, or does the console show a red line about the renderer or ambient occlusion?
