"""Motte and keep: an earth mound, a square stone keep with a pyramid roof and a palisade ring stub. Slot 1 = stone/earth, slot 2 = roof and timber."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Buildings/Keep.fbx")
reset_scene()
stone = material("Walls", (0.42, 0.40, 0.37)); timber = material("Roof", (0.24, 0.20, 0.15))
b = MeshBuilder("Keep")
b.pyramid(0, 0, 0.0, 26.0, 6.0)                 # the motte (truncated visually by the keep)
b.box(0, 0, 5.0, 10.0, 10.0, 14.0)             # keep
for x in (-4.0, 0.0, 4.0):
    b.box(x, -5.4, 19.0, 1.4, 0.8, 1.2)
    b.box(x, 5.4, 19.0, 1.4, 0.8, 1.2)
b.pyramid(0, 0, 19.0, 11.0, 4.0)
b.box(0, 7.0, 0.0, 3.0, 2.0, 5.0, roof=True)    # gate timber at the foot of the motte
obj = b.finish(stone, timber)
export_fbx(obj, out)
