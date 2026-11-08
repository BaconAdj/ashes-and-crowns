"""Two-wheeled farm cart, 3 m, shafts forward (+Y). Slot 1 = timber, slot 2 = iron rims."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Props/Cart.fbx")
reset_scene()
wood = material("Walls", (0.36, 0.28, 0.18)); iron = material("Roof", (0.30, 0.30, 0.32))
b = MeshBuilder("Cart")
b.box(0, 0, 0.75, 1.4, 2.2, 0.12)
for x in (-0.68, 0.68):
    b.box(x, 0, 0.87, 0.06, 2.2, 0.5)
b.box(0, -1.08, 0.87, 1.4, 0.06, 0.5)
for x in (-0.80, 0.80):
    b.box(x, 0, 0.0, 0.10, 1.3, 1.3, roof=True)   # wheels as thick slabs
for x in (-0.45, 0.45):
    b.box(x, 1.8, 0.80, 0.08, 1.6, 0.08)         # shafts
obj = b.finish(wood, iron)
export_fbx(obj, out)
