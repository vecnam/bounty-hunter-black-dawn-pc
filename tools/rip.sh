#!/usr/bin/env bash
# Exports the game's OBB to ripped/ through the local web API of AssetRipper 2.0.0.
# Usage: bash tools/rip.sh [input] [output]   (default: extracted/obb -> ripped; slow)
set -euo pipefail
cd "$(dirname "$0")/.."
ROOT="$(pwd -W)"                       # C:/Users/... (AssetRipper is a Windows .exe)
URL=http://127.0.0.1:5555
IN="${1:-extracted/obb}"; OUT="${2:-ripped}"
if [ -e "$OUT" ]; then echo "$OUT already exists; delete it before exporting again" >&2; exit 1; fi

tools/AssetRipper/AssetRipper.GUI.Free.exe --headless --port 5555 --log --log-path "$ROOT/tools/rip.log" &
AR_PID=$!
trap 'kill $AR_PID 2>/dev/null || true' EXIT
curl -sf -o /dev/null --retry 60 --retry-connrefused --retry-delay 1 "$URL/"

# SettingsPage.HandlePostRequest turns off every boolean missing from the POST, so EnableStaticMeshSeparation is explicit.
# Shaders as Yaml: keeps the original Unity 4 ShaderLab/GLSL text, the reference for the rewritten shaders in Assets/Shader.
curl -sf -o /dev/null "$URL/Settings/Update" \
  --data-urlencode ScriptExportMode=Decompiled \
  --data-urlencode ScriptLanguageVersion=CSharp4 \
  --data-urlencode ShaderExportMode=Yaml \
  --data-urlencode BundledAssetsExportMode=GroupByBundleName \
  --data-urlencode EnableStaticMeshSeparation=true

curl -sf -o /dev/null --max-time 0 "$URL/LoadFolder" --data-urlencode "Path=$ROOT/$IN"
curl -sf -o /dev/null --max-time 0 "$URL/Export/UnityProject" --data-urlencode "Path=$ROOT/$OUT"
echo "export finished"
