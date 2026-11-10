#!/usr/bin/env bash
# Generates every model. Plain Python writes the OBJ twins (what Unity uses); if Blender is on PATH or in $BLENDER it
# also writes FBX next to each OBJ. Run from the repo root.
set -euo pipefail
BLENDER="${BLENDER:-blender}"
if command -v "$BLENDER" >/dev/null 2>&1; then
  run() { "$BLENDER" -b -P "Tools/blender/$1.py" -- --out "UnityProject/Assets/Art/$2"; }
else
  echo "Blender not found; writing OBJ only (that is all Unity needs)."
  run() { python3 "Tools/blender/$1.py" -- --out "UnityProject/Assets/Art/${2%.fbx}.obj"; }
fi
run gen_hut      Buildings/Hut.fbx
run gen_church   Buildings/Church.fbx
run gen_alehouse Buildings/Alehouse.fbx
run gen_keep     Buildings/Keep.fbx
run gen_soldier  Characters/Soldier.fbx
run gen_man      Characters/Man.fbx
run gen_woman    Characters/Woman.fbx
run gen_horse    Characters/Horse.fbx
run gen_deer     Characters/Deer.fbx
run gen_oak      Nature/Oak.fbx
run gen_wall     Siege/WallSegment.fbx
run gen_tower    Siege/Tower.fbx
run gen_cart     Props/Cart.fbx
echo "Done. Back in Unity: Ashes and Crowns > Rebuild > (England, Battlefield, Siege) picks the models up automatically."
