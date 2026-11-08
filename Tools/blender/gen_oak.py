"""English oak, about 11 m: a trunk, two boughs and three blocky canopy masses. Slot 1 = bark, slot 2 = leaves."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Nature/Oak.fbx")
reset_scene()
bark = material("Walls", (0.27, 0.22, 0.16)); leaf = material("Roof", (0.22, 0.28, 0.17))
b = MeshBuilder("Oak")
b.box(0, 0, 0.0, 0.9, 0.9, 4.5)
b.box(-1.2, 0.3, 4.0, 0.5, 0.5, 2.0)
b.box(1.1, -0.4, 4.2, 0.5, 0.5, 1.8)
b.box(0, 0, 5.5, 7.0, 6.5, 3.0, roof=True)
b.box(-1.5, 0.8, 7.5, 4.5, 4.0, 2.4, roof=True)
b.box(1.6, -0.6, 7.2, 4.0, 4.4, 2.6, roof=True)
b.pyramid(0.3, 0.2, 9.6, 3.2, 1.6)
obj = b.finish(bark, leaf)
export_fbx(obj, out)
