"""One 4 m run of a town palisade: split oak stakes set close on the crest of the rampart, pointed, with a rail behind.
The run is along X; the field is +Y."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Siege/Palisade.fbx")
reset_scene()
walls = material("Walls", (0.30, 0.25, 0.19)); roof = material("Roof", (0.20, 0.17, 0.13))
b = MeshBuilder("Palisade")
n = 10
for i in range(n):
    x = -2.0 + (i + 0.5) * 4.0 / n
    h = 2.5 + (0.14 if i % 3 == 0 else 0.0) - (0.1 if i % 4 == 1 else 0.0)
    b.box(x, 0, -0.6, 0.37, 0.26, h + 0.6)                                # the stake, sunk into the bank
    b.pyramid(x, 0, h, 0.3, 0.42, roof=False)                           # the point
b.box(0, -0.2, 1.55, 4.0, 0.12, 0.16, roof=True)                          # rail
b.box(0, -0.2, 0.5, 4.0, 0.12, 0.16, roof=True)
export_fbx(b.finish(walls, roof), out)
