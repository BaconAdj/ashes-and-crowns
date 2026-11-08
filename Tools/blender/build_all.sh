#!/usr/bin/env bash
# Generates every FBX headlessly. Run from the repo root. Needs `blender` on PATH
# (on macOS: BLENDER=/Applications/Blender.app/Contents/MacOS/Blender Tools/blender/build_all.sh).
set -euo pipefail
BLENDER="${BLENDER:-blender}"
run() { "$BLENDER" -b -P "Tools/blender/$1.py" -- --out "UnityProject/Assets/Art/$2"; }
run gen_hut      Buildings/Hut.fbx
run gen_church   Buildings/Church.fbx
run gen_alehouse Buildings/Alehouse.fbx
run gen_keep     Buildings/Keep.fbx
run gen_soldier  Characters/Soldier.fbx
run gen_man      Characters/Man.fbx
run gen_horse    Characters/Horse.fbx
run gen_oak      Nature/Oak.fbx
run gen_wall     Siege/WallSegment.fbx
run gen_tower    Siege/Tower.fbx
run gen_cart     Props/Cart.fbx
echo "Done. Back in Unity: Ashes and Crowns > Rebuild > (England, Battlefield, Siege) picks the FBX up automatically."
