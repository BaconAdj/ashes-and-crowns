"""A woman, 1.65 m, in a long kirtle and wimple: villagers' wives and daughters, the alewife. Slot 1 = cloth, slot 2 = skin."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

from body import arm

out = parse_out("UnityProject/Assets/Art/Characters/Woman.fbx")
reset_scene()
cloth = material("Walls", (0.34, 0.30, 0.26)); skin = material("Roof", (0.64, 0.52, 0.42))
b = MeshBuilder("Woman")
for x in (-0.08, 0.08):
    b.box(x, 0.03, 0.0, 0.13, 0.24, 0.08)                                       # shoes, just showing under the hem
b.frustum(0, 0, 0.06, 0.54, 0.40, 0.38, 0.26, 0.98)                              # kirtle, full at the hem (to 1.04)
b.frustum(0, 0, 1.04, 0.38, 0.26, 0.40, 0.26, 0.36)                              # bodice to shoulders (1.40)
b.box(0, 0, 1.02, 0.40, 0.28, 0.04)                                              # girdle
b.tube((0, 0, 1.38), (0, 0, 1.47), 0.06, 0.055, n=7, roof=True)                  # neck
b.ellipsoid(0, 0.01, 1.56, 0.09, 0.105, 0.115, seg=9, rings=6, roof=True)        # face
b.ellipsoid(0, -0.03, 1.58, 0.11, 0.12, 0.13, seg=9, rings=6)                    # wimple over hair and ears
b.frustum(0, -0.02, 1.30, 0.24, 0.20, 0.16, 0.14, 0.26)                          # veil falling to the shoulders
arm(b, -1, (-0.23, 0.0, 1.36), (-0.27, 0.05, 1.10), (-0.14, 0.14, 0.98), r=0.055)   # hands held at the waist
arm(b, +1, (0.23, 0.0, 1.36), (0.27, 0.05, 1.10), (0.14, 0.14, 0.98), r=0.055)
export_fbx(b.finish(cloth, skin), out)
