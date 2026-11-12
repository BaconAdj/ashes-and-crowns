"""A small parish church of a twelfth-century town: one cell of rubble stone and a square chancel, no tower, a bellcote
on the west gable. A borough of two thousand souls had eleven of these. Nave 5.5 x 10 m, chancel 4 x 4.5 m on +Y (east
once placed), south door on +X."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Buildings/Chapel.fbx")
reset_scene()
walls = material("Walls", (0.42, 0.41, 0.38)); roof = material("Roof", (0.36, 0.30, 0.18))
b = MeshBuilder("Chapel")
NW, NL, NH = 5.5, 10.0, 4.2
b.box(0, 0, 0.0, NW + 0.4, NL + 0.4, 0.35)
b.room(0, 0, 0.35, NW, NL, NH - 0.35, 0.45, doors=(("E", -2.5, 1.3, 0.0, 2.3), ("N", 0.0, 2.6, 0.0, 3.2)),
       windows=(("E", 2.0, 0.4, 1.8, 1.2), ("W", -2.0, 0.4, 1.8, 1.2), ("W", 2.0, 0.4, 1.8, 1.2)), batter=0.02)
b.gable(0, 0, NH, NW, NL, 3.0, 0.4, drop=0.2)
b.box(0, 0, NH + 3.0 - 0.1, 0.28, NL + 0.7, 0.26, roof=True)
cw, cl, ch = 4.0, 4.5, 3.4
b.box(0, NL / 2 + cl / 2, 0.0, cw + 0.4, cl + 0.4, 0.35)
b.room(0, NL / 2 + cl / 2, 0.35, cw, cl, ch - 0.35, 0.4, doors=(("S", 0.0, 2.6, 0.0, 3.2),), windows=(("N", 0.0, 0.5, 1.5, 1.4),))
b.gable(0, NL / 2 + cl / 2, ch, cw, cl, 2.2, 0.35, drop=0.15)
b.box(0, -NL / 2, NH + 2.2, 1.5, 0.5, 1.9)                                # bellcote
b.box(0, -NL / 2, NH + 2.9, 0.5, 0.56, 0.8, roof=True)                    # the opening, dark
b.gable(0, -NL / 2, NH + 4.1, 1.5, 0.5, 0.6, 0.15)
for sgn in (-1, 1):
    b.box(NW / 2 + 0.12, -2.5 + sgn * 0.8, 0.35, 0.3, 0.28, 2.3)          # door jambs
b.box(NW / 2 + 0.12, -2.5, 2.65, 0.3, 1.9, 0.3)                           # lintel
for y in (-NL / 2 + 0.4, NL / 2 - 0.4):
    for sgn in (-1, 1):
        b.box(sgn * (NW / 2 + 0.25), y, 0, 0.5, 0.8, NH * 0.6)            # corner buttresses
check_walk(b, [(NW / 2 + 2.5, -2.5, 0.35), (NW / 2 - 1.3, -2.5, 0.35), (0, 0, 0.35), (0, NL / 2 + 1.9, 0.35)], label="south door to the altar")
export_fbx(b.finish(walls, roof), out)
