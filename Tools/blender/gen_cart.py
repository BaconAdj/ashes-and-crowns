"""Two-wheeled farm cart, 3 m, shafts forward (+Y). Slot 1 = timber, slot 2 = iron rims and hubs."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Props/Cart.fbx")
reset_scene()
wood = material("Walls", (0.36, 0.28, 0.18)); iron = material("Roof", (0.30, 0.30, 0.32))
b = MeshBuilder("Cart")
b.box(0, 0, 0.72, 1.4, 2.2, 0.10)                                             # bed
for x in (-0.68, 0.68):
    for z in (0.86, 1.06, 1.26):
        b.box(x, 0, z, 0.05, 2.2, 0.10)                                       # side slats
    for y in (-1.05, 0.0, 1.05):
        b.box(x, y, 0.82, 0.07, 0.07, 0.55)                                   # uprights
b.box(0, -1.08, 0.82, 1.4, 0.05, 0.5)                                         # tailboard
b.tube((-0.95, 0, 0.65), (0.95, 0, 0.65), 0.05, 0.05, n=6)                   # axle
for x in (-0.82, 0.82):
    b.tube((x - 0.04, 0, 0.65), (x + 0.04, 0, 0.65), 0.66, 0.66, n=14, cap=False, roof=True)   # rim
    b.tube((x - 0.05, 0, 0.65), (x + 0.05, 0, 0.65), 0.12, 0.12, n=8, roof=True)               # hub
    for i in range(8):
        a = i / 8 * 6.2832
        b.tube((x, 0.10 * math.cos(a), 0.65 + 0.10 * math.sin(a)), (x, 0.62 * math.cos(a), 0.65 + 0.62 * math.sin(a)), 0.03, 0.025, n=4)   # spokes
for x in (-0.45, 0.45):
    b.tube((x, 1.1, 0.78), (x, 2.7, 0.9), 0.045, 0.035, n=6)                   # shafts
export_fbx(b.finish(wood, iron), out)
