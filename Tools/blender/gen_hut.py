"""Peasant hut: wattle-and-daub box, thatched gable roof. Mirrors ProceduralBuildings.Hut (5.5 x 4.0 m, walls 2.2 m, roof 2.4 m)."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Buildings/Hut.fbx")
reset_scene()
walls = material("Walls", (0.52, 0.46, 0.36)); roof = material("Roof", (0.36, 0.30, 0.18))
b = MeshBuilder("Hut")
W, D, H, R = 5.5, 4.0, 2.2, 2.4
b.box(0, 0, 0, W, D, H)
b.gable(0, 0, H, W, D, R, 0.45)
b.box(0, D / 2 + 0.05, 0, 1.0, 0.1, 1.7)          # door frame bump on the front (+Y)
b.box(-W / 2 - 0.1, -0.6, 0, 0.2, 0.9, 1.2)        # a low wood pile / lean-to on the side
obj = b.finish(walls, roof)
export_fbx(obj, out)
