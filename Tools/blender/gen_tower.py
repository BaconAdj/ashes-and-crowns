"""Square wall tower, 7 x 7 m footprint, 10 m high, with a pyramid roof. Slot 1 = stone, slot 2 = roof slates."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Siege/Tower.fbx")
reset_scene()
stone = material("Walls", (0.40, 0.40, 0.39)); slate = material("Roof", (0.20, 0.21, 0.24))
b = MeshBuilder("Tower")
b.box(0, 0, 0.0, 7.0, 7.0, 10.0)
for x in (-3.0, -1.0, 1.0, 3.0):
    b.box(x, -3.6, 10.0, 1.0, 0.6, 0.9)
    b.box(x, 3.6, 10.0, 1.0, 0.6, 0.9)
b.pyramid(0, 0, 10.0, 7.6, 3.5)
obj = b.finish(stone, slate)
export_fbx(obj, out)
