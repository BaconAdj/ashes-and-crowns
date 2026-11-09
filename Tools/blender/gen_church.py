"""Norman parish church: nave, lower chancel, square west tower with string courses and a pyramid cap, stepped buttresses,
lancet windows and a round-headed door (dark slot). Mirrors ProceduralBuildings.Church (nave 9 x 18 m, walls 6 m, tower 16 m). East is +Y."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Buildings/Church.fbx")
reset_scene()
walls = material("Walls", (0.42, 0.41, 0.38)); roof = material("Roof", (0.20, 0.21, 0.24))
b = MeshBuilder("Church")
NW, NH, NL, TH = 9.0, 6.0, 18.0, 16.0
tower = NW * 0.75; ty = -NL / 2 - tower / 2
b.box(0, 0, 0.0, NW + 0.5, NL + 0.5, 0.4)                               # plinth
b.frustum(0, 0, 0.4, NW, NL, NW - 0.2, NL - 0.1, NH - 0.4)
b.gable(0, 0, NH, NW, NL, NW * 0.55, 0.45, drop=0.25)
b.box(0, 0, NH + NW * 0.55 - 0.1, 0.3, NL + 0.9, 0.3, roof=True)         # ridge
cw, cl, ch = NW * 0.7, NL * 0.35, NH * 0.8
b.box(0, NL / 2 + cl / 2, 0.0, cw + 0.4, cl + 0.4, 0.4)
b.frustum(0, NL / 2 + cl / 2, 0.4, cw, cl, cw - 0.15, cl - 0.1, ch - 0.4)
b.gable(0, NL / 2 + cl / 2, ch, cw, cl, cw * 0.5, 0.4, drop=0.2)
# tower
b.frustum(0, ty, 0.0, tower + 0.6, tower + 0.6, tower, tower, TH)
for z in (5.5, 11.0):
    b.box(0, ty, z, tower + 0.35, tower + 0.35, 0.3)                      # string courses
b.box(0, ty, TH, tower + 0.5, tower + 0.5, 0.5)
for i in range(4):                                                        # battlements on all sides
    s = -tower / 2 + 0.7 + i * (tower - 1.4) / 3
    for sx, sy in ((s, -tower / 2 - 0.1), (s, tower / 2 + 0.1), (-tower / 2 - 0.1, s), (tower / 2 + 0.1, s)):
        b.box(sx, ty + sy, TH + 0.5, 0.9, 0.9, 0.9)
b.pyramid(0, ty, TH + 0.5, tower + 0.5, tower * 0.55)
for z in (8.0, 13.0):                                                     # belfry openings, dark
    for dx, dy in ((0, -tower / 2 - 0.05), (0, tower / 2 + 0.05), (-tower / 2 - 0.05, 0), (tower / 2 + 0.05, 0)):
        b.box(dx, ty + dy, z, 0.6 if dy else 0.12, 0.12 if dy else 0.6, 1.8, roof=True)
# stepped buttresses
for i in (-1, 0, 1):
    y = i * NL / 3
    for sgn in (-1, 1):
        b.box(sgn * (NW / 2 + 0.35), y, 0, 0.7, 1.0, NH * 0.55)
        b.box(sgn * (NW / 2 + 0.22), y, NH * 0.55, 0.45, 0.9, NH * 0.3)
# lancets (dark slabs) on both long walls and the chancel
for i in (-1.5, -0.5, 0.5, 1.5):
    for sgn in (-1, 1):
        b.box(sgn * (NW / 2 + 0.04), i * NL / 4, 2.4, 0.1, 0.55, 2.4, roof=True)
b.box(0, NL / 2 + cl + 0.05, 1.8, 1.2, 0.1, 2.6, roof=True)
# round-headed south door
b.box(NW / 2 + 0.06, -NL / 4 + 2.2, 0.4, 0.12, 1.6, 2.2, roof=True)
b.tube((NW / 2 + 0.0, -NL / 4 + 2.2, 2.6), (NW / 2 + 0.12, -NL / 4 + 2.2, 2.6), 0.8, 0.8, n=10, roof=True)
export_fbx(b.finish(walls, roof), out)
