# Audio stubs needed (generate outside Claude Code, e.g. ElevenLabs / Freesound / recorded)

All files go in `UnityProject/Assets/Audio/` as `.ogg` (tracked by Git LFS). Names are final; code will reference them.

## Phase 0 / 1
- `bells_prime.ogg`, `bells_terce.ogg`, `bells_sext.ogg`, `bells_none.ogg`, `bells_vespers.ogg`, `bells_compline.ogg` — church bells, distant, 6–10 s, wet air.
- `amb_field_wind.ogg` — looping grey-day wind over open ground.
- `amb_rain_loop.ogg`, `amb_snow_loop.ogg`.
- `horse_walk_loop.ogg`, `horse_gallop_loop.ogg`, `horse_snort.ogg`.
- `sword_swing_01..03.ogg`, `sword_hit_flesh_01..03.ogg`, `sword_hit_shield_01..02.ogg`, `sword_block_01..02.ogg`.
- `player_hurt_01..03.ogg`, `player_death.ogg`.
- `battle_distant_loop.ogg` — layered shouting and steel, for the hill.
- `battle_close_loop.ogg` — screams, steel, mud.

## Narration (ElevenLabs, male, low, tired, English with a slight archaic cadence)
- `narr_opening_01.ogg`: "In the year of our Lord eleven hundred and thirty-five, the king died, and left no son."
- `narr_opening_02.ogg`: "His nephew took the crown. His daughter would not forgive it."
- `narr_opening_03.ogg`: "For nineteen winters the barons chose their sides, and changed them, and the land burned between."
- `narr_opening_04.ogg`: "Men said that Christ and his saints slept."
- `narr_death.ogg`: "Here ends one life. The war did not notice."

## Phase 8–11 (named in code via `AudioStubs.Play` / `[audio stub]` logs)
- `sergeant_hold.ogg`, `sergeant_advance.ogg`, `sergeant_charge.ogg`, `sergeant_retreat.ogg` — one hoarse line each; the text is logged next to the stub.
- `siege_horns.ogg` — horns before a storm, three long notes.  `siege_ram_thud.ogg` — oak on oak, looped while the ram works.  `siege_gate_crash.ogg` — the gate gives.
- `village_raid_screams.ogg` — a village under raid at a distance, fire and shouting.  `ambush_riders.ogg` — hooves at a ford, then nothing.
- `whisper_warning.ogg` — a short, low, indistinct whisper for the paranoia line.
- `sack_town_loop.ogg` — the sack: doors, screaming, bells rung wrong.
- `provost_lash_01..03.ogg`, `rope_creak.ogg` — the provost.
- `narr_death.ogg` is also used by the French build with `narr_death_fr.ogg` (same line in French: « Ici finit une vie. La guerre ne l'a pas remarqué. »).
