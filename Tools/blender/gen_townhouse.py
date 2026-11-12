"""A burgess's house of the 1130s: earthfast posts, wattle and daub, thatch, one storey, gable end to the street with
the shop window beside the door, a back door to the yard. 5 x 9 m on the narrow plot. Front (street) is +Y.
The door leaves are hung by the game (Game.Door), not modelled."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

out = parse_out("UnityProject/Assets/Art/Buildings/TownHouse.fbx")
reset_scene()
walls = material("Walls", (0.52, 0.46, 0.36)); roof = material("Roof", (0.36, 0.30, 0.18))
b = MeshBuilder("TownHouse")
W, D, H, R = 5.0, 9.0, 2.9, 2.8
b.box(0, 0, 0.0, W + 0.3, D + 0.3, 0.3)                                # sill beam and earth floor (top is the floor)
b.room(0, 0, 0.3, W, D, H - 0.3, 0.28,
       doors=(("N", 0.0, 1.2, 0.0, 2.45), ("S", 0.2, 1.0, 0.0, 2.45)),
       windows=(("N", 1.6, 0.9, 0.9, 0.8), ("E", -1.0, 0.6, 1.1, 0.6), ("W", 1.0, 0.6, 1.1, 0.6)), batter=0.03)
for y in (-D / 2, -D / 4, 0.0, D / 4, D / 2):                           # wall posts, proud of the daub
    for x in (-W / 2, W / 2):
        b.box(x, y, 0.25, 0.2, 0.2, H)
for x in (-0.72, 0.72):
    b.box(x, D / 2 + 0.06, 0.3, 0.16, 0.18, 2.45)                        # door posts
b.box(0, D / 2 + 0.06, 2.75, 1.62, 0.18, 0.16)                          # lintel
b.box(1.6, D / 2 + 0.3, 1.1, 1.0, 0.5, 0.06)                            # the shop board let down under the window
b.box(1.15, D / 2 + 0.5, 0.3, 0.07, 0.07, 0.8); b.box(2.05, D / 2 + 0.5, 0.3, 0.07, 0.07, 0.8)   # its legs
b.box(0, D / 2 + 0.02, H, W + 0.1, 0.1, 0.14)                           # tie beam across the gable
b.box(0, D / 2 + 0.02, H, 0.14, 0.1, R - 0.3)                           # king post
b.gable(0, 0, H, W, D, R, 0.5, drop=0.3)                                 # thatch
b.box(0, 0, H + R - 0.1, 0.3, D + 0.9, 0.22, roof=True)                  # ridge
b.box(0, -0.2, H + R - 0.25, 0.8, 1.1, 0.5)                              # smoke louvre
b.gable(0, -0.2, H + R + 0.2, 0.8, 1.1, 0.4, 0.15)
b.box(-W / 2 - 0.45, -D / 2 + 1.2, 0.0, 0.7, 1.6, 0.9)                   # water butt and a lean-to store on the yard side
check_walk(b, [(0, D / 2 + 2.5, 0.3), (0, D / 2 - 1.1, 0.3), (0.9, 0.0, 0.3), (0.2, -D / 2 + 1.0, 0.3), (0.2, -D / 2 - 2.0, 0.3)], label="street door to yard door")
export_fbx(b.finish(walls, roof), out)
