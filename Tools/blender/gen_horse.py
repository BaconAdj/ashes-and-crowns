"""Riding horse, 1.55 m at the withers, head forward (+Y). Slot 1 = coat, slot 2 = saddle, tack, hooves and tail."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Characters/Horse.fbx")
reset_scene()
coat = material("Walls", (0.30, 0.20, 0.13)); tack = material("Roof", (0.22, 0.16, 0.11))
b = MeshBuilder("Horse")
b.ellipsoid(0, 0.0, 1.05, 0.34, 0.85, 0.38, seg=12, rings=7, bump=0.02, seed=3)   # barrel
b.ellipsoid(0, 0.62, 1.02, 0.30, 0.34, 0.36, seg=10, rings=6)                      # chest
b.ellipsoid(0, -0.62, 1.12, 0.32, 0.34, 0.36, seg=10, rings=6)                     # rump
b.tube((0, 0.72, 1.22), (0, 1.28, 1.66), 0.18, 0.13, n=9)                            # neck
b.tube((0, 1.28, 1.66), (0, 1.78, 1.50), 0.12, 0.085, n=8)                           # head
b.ellipsoid(0, 1.82, 1.48, 0.085, 0.10, 0.08, seg=8, rings=4)                        # muzzle
for x in (-0.06, 0.06):
    b.cone(x, 1.24, 1.76, 0.03, 0.13, n=5)                                             # ears
b.tube((0, 0.74, 1.42), (0, 1.26, 1.80), 0.05, 0.04, n=5, roof=True)                 # mane
for x, y in ((-0.18, 0.58), (0.18, 0.58), (-0.18, -0.60), (0.18, -0.60)):
    b.tube((x, y, 0.09), (x, y, 0.95), 0.055, 0.09, n=7)                             # legs
    b.tube((x, y, 0.0), (x, y, 0.10), 0.07, 0.065, n=7, roof=True)                   # hooves
b.tube((0, -0.95, 1.30), (0, -1.02, 0.62), 0.06, 0.03, n=6, roof=True)              # tail
b.ellipsoid(0, -0.02, 1.43, 0.30, 0.33, 0.08, seg=10, rings=4, roof=True)           # saddle seat
b.ellipsoid(0, 0.26, 1.50, 0.08, 0.08, 0.09, seg=6, rings=4, roof=True)              # pommel
b.tube((-0.05, -0.02, 1.05), (0.05, -0.02, 1.05), 0.40, 0.40, n=14, cap=False, roof=True)   # girth
b.tube((0, 1.46, 1.58), (0, 1.52, 1.56), 0.11, 0.10, n=8, cap=False, roof=True)      # noseband
export_fbx(b.finish(coat, tack), out)
