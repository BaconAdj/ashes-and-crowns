"""An unarmed man, 1.75 m, in tunic and hose: the player's body, villagers, duelists. Slot 1 = cloth, slot 2 = skin."""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from common import *

from body import *

out = parse_out("UnityProject/Assets/Art/Characters/Man.fbx")
reset_scene()
cloth = material("Walls", (0.30, 0.26, 0.20)); skin = material("Roof", (0.62, 0.50, 0.40))
b = MeshBuilder("Man")
legs_and_tunic(b)
head(b)
arm(b, -1, (-0.25, 0.0, 1.43), (-0.30, 0.03, 1.16), (-0.31, 0.09, 0.92))
arm(b, +1, (0.25, 0.0, 1.43), (0.30, 0.03, 1.16), (0.31, 0.09, 0.92))
export_fbx(b.finish(cloth, skin), out)
