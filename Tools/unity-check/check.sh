#!/usr/bin/env bash
# Type-checks every game and editor script without Unity, so a compile error is caught before anyone opens the editor.
# It compiles against Unity 2021.3 reference assemblies from nuget.org (fetched once into refs/, not committed) plus
# small hand-written stubs for the packages (Input System, Mathematics, Collections, Burst, URP) in Stubs/.
# Limits: it is an older Unity than the project's, so an API newer than 2021.3 shows as a false error, and a package
# call the stubs do not know needs a line added to Stubs/. Run from anywhere: Tools/unity-check/check.sh
set -euo pipefail
cd "$(dirname "$0")"
if [ ! -d refs/um ]; then
  mkdir -p refs/um refs/sdk
  curl -sSL -o refs/um.nupkg https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg
  curl -sSL -o refs/sdk.nupkg https://api.nuget.org/v3-flatcontainer/unity3d.sdk/2021.1.14.1/unity3d.sdk.2021.1.14.1.nupkg
  (cd refs/um && unzip -q -o ../um.nupkg) && (cd refs/sdk && unzip -q -o ../sdk.nupkg)
fi
out=$(dotnet build check.csproj -nologo -v q 2>&1 | grep -E " error " | sed -E 's/.*Scripts\///; s/ \[.*//' | sort -u || true)
if [ -z "$out" ]; then echo "unity-check: no compile errors"; else echo "$out"; exit 1; fi
