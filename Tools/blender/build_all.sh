#!/usr/bin/env bash
# Generates every building FBX headlessly. Run from the repo root. Needs `blender` on PATH
# (on macOS: /Applications/Blender.app/Contents/MacOS/Blender).
set -euo pipefail
BLENDER="${BLENDER:-blender}"
for s in gen_hut gen_church gen_alehouse; do
  "$BLENDER" -b -P "Tools/blender/$s.py" -- --out "UnityProject/Assets/Art/Buildings/$(echo "${s#gen_}" | sed 's/./\U&/').fbx"
done
echo "Done. Back in Unity: Ashes and Crowns > Rebuild > England Scene picks the FBX up automatically."
