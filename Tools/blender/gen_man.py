"""An unarmed man, 1.75 m, in a tunic and hose: the player's body, villagers, duelists. Slot 1 = cloth, slot 2 = skin."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Characters/Man.fbx")
reset_scene()
cloth = material("Walls", (0.30, 0.26, 0.20)); skin = material("Roof", (0.62, 0.50, 0.40))
b = MeshBuilder("Man")
for x in (-0.11, 0.11):
    b.box(x, 0.0, 0.0, 0.16, 0.2, 0.88)
b.box(0, 0, 0.88, 0.42, 0.25, 0.60)
b.box(-0.29, 0.0, 0.92, 0.12, 0.14, 0.56)
b.box(0.29, 0.0, 0.92, 0.12, 0.14, 0.56)
b.box(-0.29, 0.0, 0.84, 0.12, 0.14, 0.09, roof=True)   # hands
b.box(0.29, 0.0, 0.84, 0.12, 0.14, 0.09, roof=True)
b.box(0, 0, 1.48, 0.20, 0.22, 0.25, roof=True)         # head
b.box(0, -0.02, 1.70, 0.22, 0.24, 0.06)                 # hair as a dark cap
obj = b.finish(cloth, skin)
export_fbx(obj, out)
