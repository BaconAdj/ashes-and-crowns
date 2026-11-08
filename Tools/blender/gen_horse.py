"""Riding horse, 1.55 m at the withers, head forward (+Y). Slot 1 = coat, slot 2 = saddle and tack."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Characters/Horse.fbx")
reset_scene()
coat = material("Walls", (0.30, 0.20, 0.13)); tack = material("Roof", (0.22, 0.16, 0.11))
b = MeshBuilder("Horse")
b.box(0, 0, 0.80, 0.62, 1.70, 0.70)             # barrel
b.box(0, 0.95, 1.05, 0.30, 0.55, 0.75)          # neck, rising
b.box(0, 1.40, 1.55, 0.26, 0.55, 0.30)          # head
b.box(0, 1.70, 1.45, 0.20, 0.25, 0.22)          # muzzle
for x, y in ((-0.20, 0.60), (0.20, 0.60), (-0.20, -0.60), (0.20, -0.60)):
    b.box(x, y, 0.0, 0.14, 0.16, 0.82)          # legs
b.box(0, -0.90, 0.95, 0.10, 0.20, 0.55)         # tail
b.box(0, -0.05, 1.50, 0.50, 0.60, 0.12, roof=True)   # saddle
b.box(0, -0.05, 1.20, 0.68, 0.10, 0.34, roof=True)   # girth strap
b.box(0, 1.40, 1.72, 0.08, 0.30, 0.10, roof=True)    # bridle line
obj = b.finish(coat, tack)
export_fbx(obj, out)
