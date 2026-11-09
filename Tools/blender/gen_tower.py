"""Square wall tower, 7 x 7 m footprint, 10 m high, battered, with battlements, slits and a pyramid roof. Slot 1 = stone, slot 2 = slates and openings."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Siege/Tower.fbx")
reset_scene()
stone = material("Walls", (0.40, 0.40, 0.39)); slate = material("Roof", (0.20, 0.21, 0.24))
b = MeshBuilder("Tower")
b.frustum(0, 0, 0.0, 7.8, 7.8, 7.0, 7.0, 10.0)
b.box(0, 0, 4.5, 7.5, 7.5, 0.3)
b.box(0, 0, 10.0, 7.6, 7.6, 0.5)
for i in range(4):
    s = -3.0 + i * 2.0
    for sx, sy in ((s, -3.6), (s, 3.6), (-3.6, s), (3.6, s)):
        b.box(sx, sy, 10.5, 1.0, 1.0, 0.9)
b.pyramid(0, 0, 10.5, 7.2, 3.6)
for z in (3.0, 7.0):
    for dx, dy in ((0, -3.7), (0, 3.7), (-3.7, 0), (3.7, 0)):
        b.box(dx, dy, z, 0.5 if dy else 0.14, 0.14 if dy else 0.5, 1.4, roof=True)
b.box(0, -3.75, 0.0, 1.3, 0.14, 2.3, roof=True)                               # door on the field side
export_fbx(b.finish(stone, slate), out)
