"""Peasant hut: stone footing, battered wattle walls with posts, a steep thatched roof hanging to head height, a door frame.
Mirrors ProceduralBuildings.Hut (5.5 x 4.0 m, walls 2.85 m, door 2.45 m clear). Front (door) is +Y. The door leaf is hung by the game (Game.Door), not modelled."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Buildings/Hut.fbx")
reset_scene()
walls = material("Walls", (0.52, 0.46, 0.36)); roof = material("Roof", (0.36, 0.30, 0.18))
b = MeshBuilder("Hut")
W, D, H, R = 5.5, 4.0, 2.85, 2.2
b.box(0, 0, 0.0, W + 0.3, D + 0.3, 0.3)                           # footing (its top is the floor)
b.room(0, 0, 0.3, W, D, H - 0.3, 0.28, doors=(("N", 0.0, 1.2, 0.0, 2.45),), windows=(("E", 0.9, 0.6, 1.0, 0.5),), batter=0.04)   # hollow daub walls
for x in (-W / 2, 0.0, W / 2):
    for y in (-D / 2, D / 2):
        if x == 0.0 and y > 0: continue                            # no post in the doorway
        b.box(x, y, 0.25, 0.2, 0.2, H)                             # posts
for y in (0.0,):
    b.box(-W / 2, y, 0.25, 0.2, 0.2, H); b.box(W / 2, y, 0.25, 0.2, 0.2, H)
for x in (-0.67, 0.67):
    b.box(x, D / 2 + 0.08, 0.3, 0.14, 0.16, 2.45)                   # door posts
b.box(0, D / 2 + 0.08, 2.75, 1.5, 0.16, 0.14)                       # lintel
b.gable(0, 0, H, W, D, R, 0.6, drop=0.35)                           # thatch, eaves at 2.5 m
b.box(0, 0, H + R - 0.1, 0.3, D + 1.1, 0.22, roof=True)             # ridge cap
for i, (y, r) in enumerate(((-0.9, 0.16), (-0.5, 0.14), (-0.7, 0.13))):
    b.tube((-W / 2 - 0.5, y - 0.6, 0.15 + 0.3 * (i // 2)), (-W / 2 - 0.5, y + 0.6, 0.15 + 0.3 * (i // 2)), r, r, n=6)   # woodpile
check_walk(b, [(0, D / 2 + 3.0, 0.3), (0, D / 2 - 1.0, 0.3), (0.9, -0.2, 0.3)], label="front door")
export_fbx(b.finish(walls, roof), out)
