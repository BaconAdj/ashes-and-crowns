"""A red deer hind or stag, 1.2 m at the shoulder, head forward (+Y). Slot 1 = coat, slot 2 = antlers, hooves, muzzle."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Characters/Deer.fbx")
reset_scene()
coat = material("Walls", (0.42, 0.30, 0.19)); dark = material("Roof", (0.22, 0.17, 0.12))
b = MeshBuilder("Deer")
b.ellipsoid(0, 0.0, 0.88, 0.22, 0.58, 0.26, seg=11, rings=6, bump=0.015, seed=5)    # barrel
b.ellipsoid(0, 0.42, 0.90, 0.19, 0.22, 0.24, seg=9, rings=5)                          # chest
b.ellipsoid(0, -0.46, 0.92, 0.20, 0.22, 0.24, seg=9, rings=5)                         # rump
b.tube((0, 0.52, 1.02), (0, 0.92, 1.38), 0.11, 0.08, n=8)                               # neck
b.tube((0, 0.92, 1.38), (0, 1.22, 1.32), 0.075, 0.05, n=7)                              # head
b.ellipsoid(0, 1.25, 1.31, 0.05, 0.06, 0.045, seg=7, rings=4, roof=True)                # muzzle
for x in (-0.05, 0.05):
    b.cone(x, 0.90, 1.44, 0.03, 0.12, n=5)                                                 # ears
    b.tube((x * 1.4, 0.94, 1.46), (x * 3.2, 1.00, 1.78), 0.022, 0.015, n=5, roof=True)     # antler beam
    b.tube((x * 2.4, 0.97, 1.64), (x * 3.6, 1.14, 1.74), 0.015, 0.01, n=4, roof=True)      # tine
    b.tube((x * 2.9, 0.99, 1.72), (x * 3.0, 0.84, 1.92), 0.015, 0.01, n=4, roof=True)      # tine
for x, y in ((-0.11, 0.38), (0.11, 0.38), (-0.11, -0.42), (0.11, -0.42)):
    b.tube((x, y, 0.08), (x, y, 0.80), 0.032, 0.055, n=6)                                  # legs
    b.tube((x, y, 0.0), (x, y, 0.09), 0.04, 0.036, n=6, roof=True)                         # hooves
b.tube((0, -0.68, 1.02), (0, -0.78, 0.86), 0.035, 0.015, n=5)                             # tail
export_fbx(b.finish(coat, dark), out)
