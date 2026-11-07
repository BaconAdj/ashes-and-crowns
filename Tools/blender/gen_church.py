"""Norman parish church: nave with gable roof, lower chancel, square west tower with a pyramid cap, buttresses.
Mirrors ProceduralBuildings.Church (nave 9 x 18 m, walls 6 m, tower 16 m). Front (+Y here, +Z in Unity) is east."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Buildings/Church.fbx")
reset_scene()
walls = material("Walls", (0.42, 0.41, 0.38)); roof = material("Roof", (0.20, 0.21, 0.24))
b = MeshBuilder("Church")
NW, NH, NL, TH = 9.0, 6.0, 18.0, 16.0
tower = NW * 0.75; tower_y = -NL / 2 - tower / 2
b.box(0, 0, 0, NW, NL, NH)
b.gable(0, 0, NH, NW, NL, NW * 0.55, 0.4)
cw, cl, ch = NW * 0.7, NL * 0.35, NH * 0.8
b.box(0, NL / 2 + cl / 2, 0, cw, cl, ch)
b.gable(0, NL / 2 + cl / 2, ch, cw, cl, cw * 0.5, 0.35)
b.box(0, tower_y, 0, tower, tower, TH)
b.box(0, tower_y, TH, tower + 0.3, tower + 0.3, 0.6)
b.pyramid(0, tower_y, TH + 0.6, tower + 0.3, tower * 0.45)
for i in (-1, 0, 1):
    y = i * NL / 3
    b.box(-NW / 2 - 0.3, y, 0, 0.6, 0.8, NH * 0.85)
    b.box(NW / 2 + 0.3, y, 0, 0.6, 0.8, NH * 0.85)
# lancet windows as shallow recesses on the south wall
for i in (-1.5, -0.5, 0.5, 1.5):
    b.box(NW / 2 + 0.02, i * NL / 4, 2.2, 0.08, 0.5, 2.2)
obj = b.finish(walls, roof)
export_fbx(obj, out)
