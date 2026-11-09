"""English oak, about 11 m: flared trunk, four boughs and five lumpy canopy masses. Slot 1 = bark, slot 2 = leaves."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Nature/Oak.fbx")
reset_scene()
bark = material("Walls", (0.33, 0.27, 0.20)); leaf = material("Roof", (0.30, 0.36, 0.20))
b = MeshBuilder("Oak")
b.tube((0, 0, 0.0), (0.05, 0.02, 1.0), 0.78, 0.52, n=10)                     # root flare
b.tube((0.05, 0.02, 0.9), (0.20, 0.10, 4.8), 0.52, 0.30, n=10, twist=0.3)     # trunk
for tx, ty in ((-2.1, 1.4), (2.2, -1.1), (0.5, -2.2), (-0.9, -0.3)):
    b.tube((0.20, 0.10, 4.2), (tx, ty, 6.6), 0.22, 0.09, n=6)                  # boughs
canopy = [(0.2, 0.1, 7.4, 4.3, 4.0, 2.7, 1), (-2.1, 1.3, 6.7, 2.9, 2.7, 2.1, 2), (2.2, -1.1, 6.9, 2.7, 2.9, 2.2, 3),
          (0.5, -2.0, 8.6, 2.3, 2.3, 1.7, 4), (-0.8, 0.5, 9.4, 2.1, 2.1, 1.5, 5)]
for cx, cy, cz, rx, ry, rz, seed in canopy:
    b.ellipsoid(cx, cy, cz, rx, ry, rz, seg=10, rings=6, roof=True, bump=0.22, seed=seed, smooth=False, freq=1.3)
export_fbx(b.finish(bark, leaf), out)
