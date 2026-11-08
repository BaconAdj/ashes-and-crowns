"""Foot soldier, 1.75 m: a blocky man in a gambeson with a kettle helm, round shield on the left arm, spear in the right.
Instanced a thousand times, so it stays under 300 triangles. Slot 1 (Walls) = cloth and skin, slot 2 (Roof) = iron."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Characters/Soldier.fbx")
reset_scene()
cloth = material("Walls", (0.36, 0.30, 0.22)); iron = material("Roof", (0.45, 0.47, 0.50))
b = MeshBuilder("Soldier")
# legs
for x in (-0.11, 0.11):
    b.box(x, 0.0, 0.0, 0.16, 0.2, 0.85)
# torso (gambeson) and belt
b.box(0, 0, 0.85, 0.44, 0.26, 0.62)
b.box(0, 0, 1.10, 0.46, 0.28, 0.06, roof=True)
# arms
b.box(-0.30, 0.0, 0.90, 0.13, 0.15, 0.55)
b.box(0.30, 0.0, 0.90, 0.13, 0.15, 0.55)
# head and kettle helm
b.box(0, 0, 1.47, 0.20, 0.22, 0.24)
b.pyramid(0, 0, 1.68, 0.34, 0.12)
b.box(0, 0, 1.66, 0.34, 0.36, 0.03, roof=True)
# shield on the left arm, slightly forward
b.box(-0.40, 0.14, 0.78, 0.06, 0.62, 0.62, roof=True)
# spear in the right hand, 2.4 m, leaning a little forward
b.box(0.34, 0.12, 0.0, 0.04, 0.04, 2.4)
b.pyramid(0.34, 0.12, 2.4, 0.07, 0.22)
obj = b.finish(cloth, iron)
export_fbx(obj, out)
