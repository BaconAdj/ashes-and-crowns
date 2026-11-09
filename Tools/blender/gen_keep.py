"""Motte and keep: a truncated earth mound with a palisade, a battered square keep with pilasters, battlements and a
pyramid roof, a timber gate at the foot. Slot 1 = stone and earth, slot 2 = roof, timber and openings."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Buildings/Keep.fbx")
reset_scene()
stone = material("Walls", (0.42, 0.40, 0.37)); timber = material("Roof", (0.24, 0.20, 0.15))
b = MeshBuilder("Keep")
b.tube((0, 0, 0.0), (0, 0, 6.0), 13.5, 8.0, n=28)                                 # motte
b.frustum(0, 0, 5.8, 10.8, 10.8, 10.0, 10.0, 13.2)                                 # keep, battered
for x in (-5.0, 5.0):
    for y in (-5.0, 5.0):
        b.box(x, y, 5.8, 1.2, 1.2, 13.6)                                           # corner pilasters
b.box(0, 0, 12.0, 10.6, 10.6, 0.35)                                                # string course
b.box(0, 0, 19.0, 10.8, 10.8, 0.6)
for i in range(5):
    s = -4.6 + i * 2.3
    for sx, sy in ((s, -5.3), (s, 5.3), (-5.3, s), (5.3, s)):
        b.box(sx, sy, 19.6, 1.1, 1.1, 1.1)                                         # battlements
b.pyramid(0, 0, 19.6, 10.4, 3.8)
for z in (8.5, 14.5):
    for dx, dy in ((0, -5.05), (0, 5.05), (-5.05, 0), (5.05, 0)):
        b.box(dx, dy, z, 0.5 if dy else 0.14, 0.14 if dy else 0.5, 1.6, roof=True)  # slit windows
b.box(0, 5.1, 8.0, 1.4, 0.2, 2.4, roof=True)                                       # first-floor door
b.frustum(0, 6.2, 5.8, 2.4, 2.6, 2.0, 2.2, 2.2)                                    # forebuilding stair
for i in range(30):                                                                 # palisade on the mound's rim
    a = i / 30 * 6.2832
    x, y = 7.6 * math.cos(a), 7.6 * math.sin(a)
    if 1.2 < a < 1.9: continue                                                     # gap above the gate
    b.tube((x, y, 5.6), (x, y, 8.0), 0.13, 0.11, n=5, roof=True)
b.box(0, 13.0, 0.0, 3.4, 2.0, 5.0, roof=True)                                      # gate timber
b.box(0, 13.0, 1.0, 1.6, 2.4, 2.8)                                                 # gate opening (stone-grey, reads as passage)
export_fbx(b.finish(stone, timber), out)
