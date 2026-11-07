"""Alehouse: a long hall with a steeper thatched roof and a porch. Mirrors ProceduralBuildings.Alehouse (10 x 7 m, walls 3 m)."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Buildings/Alehouse.fbx")
reset_scene()
walls = material("Walls", (0.50, 0.44, 0.34)); roof = material("Roof", (0.34, 0.28, 0.17))
b = MeshBuilder("Alehouse")
W, D, H = 10.0, 7.0, 3.0
b.box(0, 0, 0, W, D, H)
b.gable(0, 0, H, W, D, H * 1.1, 0.5)
b.box(0, D / 2 + 0.8, 0, 2.4, 1.6, H * 0.8)
b.gable(0, D / 2 + 0.8, H * 0.8, 2.4, 1.6, 1.0, 0.3)
obj = b.finish(walls, roof)
export_fbx(obj, out)
