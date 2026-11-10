"""Alehouse: a long hall, battered walls with posts, steep thatch with a ridge louvre, a gabled porch and a sign.
Hollow hall: in through the porch on +Y; floor at 0.35. Windows are real openings."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Buildings/Alehouse.fbx")
reset_scene()
walls = material("Walls", (0.50, 0.44, 0.34)); roof = material("Roof", (0.34, 0.28, 0.17))
b = MeshBuilder("Alehouse")
W, D, H = 10.0, 7.0, 3.0
b.box(0, 0, 0.0, W + 0.4, D + 0.4, 0.35)
b.room(0, 0, 0.35, W, D, H - 0.35, 0.3, doors=(("N", 0.0, 1.3, 0.0, 2.1),), windows=(("N", -3.2, 0.7, 1.15, 0.8), ("N", -1.8, 0.7, 1.15, 0.8), ("N", 1.8, 0.7, 1.15, 0.8), ("N", 3.2, 0.7, 1.15, 0.8), ("S", -2.5, 0.7, 1.15, 0.8), ("S", 2.5, 0.7, 1.15, 0.8)), batter=0.03)
for x in (-W / 2, -W / 4, 0.0, W / 4, W / 2):
    for y in (-D / 2, D / 2):
        b.box(x, y, 0.3, 0.22, 0.22, H)
for y in (-D / 4, 0.0, D / 4):
    b.box(-W / 2, y, 0.3, 0.22, 0.22, H); b.box(W / 2, y, 0.3, 0.22, 0.22, H)
b.box(0, 0, 1.6, W + 0.1, D + 0.1, 0.12)                              # mid rail
b.gable(0, 0, H, W, D, H * 1.1, 0.7, drop=0.5)                        # thatch
b.box(0, 0, H + H * 1.1 - 0.1, 0.35, D + 1.3, 0.25, roof=True)         # ridge
b.box(0, 0, H + H * 1.1 - 0.3, 1.0, 1.4, 0.7)                          # smoke louvre
b.gable(0, 0, H + H * 1.1 + 0.4, 1.0, 1.4, 0.5, 0.2)
# porch: two side walls and a gable, open front and back so the door is a real passage
b.box(0, D / 2 + 0.9, 0.0, 2.8, 1.9, 0.35)                              # porch step
b.box(-1.3, D / 2 + 0.9, 0.35, 0.2, 1.9, H * 0.78 - 0.35)
b.box(1.3, D / 2 + 0.9, 0.35, 0.2, 1.9, H * 0.78 - 0.35)
b.box(0, D / 2 + 0.9, H * 0.78 - 0.25, 2.8, 1.9, 0.25)
b.box(0.55, D / 2 + 1.82, 0.35, 0.08, 0.1, 2.05, roof=True)             # door leaf, open
b.gable(0, D / 2 + 0.9, H * 0.78, 2.8, 1.9, 1.1, 0.35, drop=0.2)
# ale-stake and sign
b.tube((W / 2 + 0.9, D / 2 + 0.4, 0.0), (W / 2 + 0.9, D / 2 + 0.4, 3.6), 0.09, 0.08, n=6)
b.box(W / 2 + 0.9, D / 2 + 0.4, 3.4, 0.08, 1.2, 0.08)
b.box(W / 2 + 0.9, D / 2 + 0.9, 2.6, 0.05, 0.7, 0.75, roof=True)
export_fbx(b.finish(walls, roof), out)
