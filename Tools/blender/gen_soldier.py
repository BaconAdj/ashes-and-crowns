"""Foot soldier, 1.75 m: gambeson, kettle helm, round shield on the left arm, spear in the right.
Instanced a thousand times, so it stays near 600 triangles. Slot 1 (Walls) = cloth and skin, slot 2 (Roof) = iron."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

from body import *

out = parse_out("UnityProject/Assets/Art/Characters/Soldier.fbx")
reset_scene()
cloth = material("Walls", (0.36, 0.30, 0.22)); iron = material("Roof", (0.45, 0.47, 0.50))
b = MeshBuilder("Soldier")
legs_and_tunic(b, bulk=1.08, skin_slot=False)
for z in (0.90, 1.00, 1.20, 1.30):                                              # quilting rows on the gambeson
    b.box(0, 0, z, 0.47, 0.30, 0.025)
head(b, skin_slot=False)
# left arm holds the shield forward, right arm grips the spear
arm(b, -1, (-0.25, 0.0, 1.43), (-0.33, 0.10, 1.18), (-0.34, 0.16, 1.00), skin_slot=False)
arm(b, +1, (0.25, 0.0, 1.43), (0.31, 0.06, 1.17), (0.33, 0.14, 1.12), skin_slot=False)
# kettle helm: brim and cone
b.tube((0, 0, 1.70), (0, 0, 1.73), 0.19, 0.19, n=12, roof=True)
b.cone(0, 0, 1.73, 0.13, 0.11, n=10, roof=True)
# round shield, boss in the middle, hung on the left forearm
b.tube((-0.40, 0.18, 1.00), (-0.37, 0.18, 1.00), 0.33, 0.33, n=14, roof=True)
b.ellipsoid(-0.41, 0.18, 1.00, 0.04, 0.08, 0.08, seg=6, rings=3, roof=True)
# spear, 2.4 m
b.tube((0.33, 0.14, 0.0), (0.33, 0.14, 2.3), 0.02, 0.02, n=6)
b.cone(0.33, 0.14, 2.3, 0.04, 0.25, n=6, roof=True)
export_fbx(b.finish(cloth, iron), out)
