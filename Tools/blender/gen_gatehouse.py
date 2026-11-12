"""A town gate: two squat towers either side of a cart-wide passage, a chamber over it, a shingled roof. The road runs
along Y through the middle. 9 x 6 m, passage 3.4 m wide and 3.5 m high."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Siege/Gatehouse.fbx")
reset_scene()
walls = material("Walls", (0.42, 0.41, 0.38)); roof = material("Roof", (0.20, 0.17, 0.13))
b = MeshBuilder("Gatehouse")
PW, PH, TW, D, H = 3.4, 3.5, 2.8, 6.0, 7.2
for sgn in (-1, 1):
    b.frustum(sgn * (PW / 2 + TW / 2), 0, 0.0, TW + 0.3, D + 0.3, TW, D, H)   # the two towers, battered at the foot
    b.box(sgn * (PW / 2 + TW / 2), 0, H, TW + 0.2, D + 0.2, 0.3)
    for y in (-D / 2 + 0.6, 0.0, D / 2 - 0.6):
        b.box(sgn * (PW / 2 + TW - 0.25), y, H + 0.3, 0.5, 0.9, 0.8)           # merlons on the outer edges
    for y in (-D / 2 - 0.02, D / 2 + 0.02):
        b.box(sgn * (PW / 2 + TW / 2), y, 4.4, 0.25, 0.1, 1.1, roof=True)       # loops, dark
b.box(0, 0, PH, PW + 0.2, D, H - PH)                                           # chamber over the passage
b.box(0, 0, PH - 0.25, PW, D + 0.4, 0.25, roof=True)                           # the timber soffit
for y in (-D / 2 - 0.05, D / 2 + 0.05):
    b.box(0, y, PH + 1.4, 0.3, 0.12, 1.0, roof=True)                           # window over the arch
b.pyramid(0, 0, H + 0.3, 5.0, 2.4)                                           # shingled cap over the chamber
check_walk(b, [(0, D / 2 + 4.0, 0.0), (0, 0, 0.0), (0, -D / 2 - 4.0, 0.0)], width=2.4, height=3.2, label="the passage")
export_fbx(b.finish(walls, roof), out)
