"""Curtain wall segment, 10 m long, 6 m high with a walkway and crenellations; tiles along +X. Slot 1 = stone, slot 2 = capstones."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Siege/WallSegment.fbx")
reset_scene()
stone = material("Walls", (0.40, 0.40, 0.39)); cap = material("Roof", (0.33, 0.33, 0.33))
b = MeshBuilder("WallSegment")
L, T, H = 10.0, 1.6, 6.0
b.box(0, 0, 0.0, L, T, H)                       # the wall
b.box(0, -T / 2 + 0.25, H, L, 0.5, 1.2)          # parapet on the field side
for i in range(5):
    x = -L / 2 + 1.0 + i * 2.25
    b.box(x, -T / 2 + 0.25, H + 1.2, 1.1, 0.5, 0.6, roof=True)   # merlons
b.box(0, 0, H - 0.05, L, T, 0.1, roof=True)      # walkway stones
obj = b.finish(stone, cap)
export_fbx(obj, out)
