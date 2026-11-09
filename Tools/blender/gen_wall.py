"""Curtain wall segment, 10 m long, 6 m high, battered, with walkway, parapet, merlons and arrow slits; tiles along +X.
Slot 1 = stone, slot 2 = capstones and slits."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Siege/WallSegment.fbx")
reset_scene()
stone = material("Walls", (0.40, 0.40, 0.39)); cap = material("Roof", (0.33, 0.33, 0.33))
b = MeshBuilder("WallSegment")
L, T, H = 10.0, 1.6, 6.0
b.frustum(0, 0, 0.0, L, T + 0.5, L, T, H)                                   # battered wall
b.box(0, 0, H - 0.05, L, T, 0.12, roof=True)                                # walkway stones
b.box(0, -T / 2 + 0.25, H, L, 0.5, 1.2)                                     # parapet, field side
for i in range(5):
    x = -L / 2 + 1.1 + i * 2.2
    b.box(x, -T / 2 + 0.25, H + 1.2, 1.1, 0.5, 0.7)                        # merlons
    b.box(x, -T / 2 + 0.25, H + 1.9, 1.1, 0.6, 0.12, roof=True)             # merlon caps
for x in (-3.4, 0.0, 3.4):
    b.box(x, -T / 2 - 0.28, 3.0, 0.14, 0.1, 1.4, roof=True)                 # arrow slits
export_fbx(b.finish(stone, cap), out)
