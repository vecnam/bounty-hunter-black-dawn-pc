"""Checks the AssetRipper export. Usage: python -I tools/check_rip.py ripped"""
import pathlib
import sys

assets = pathlib.Path(sys.argv[1]) / "ExportedProject" / "Assets"
files = [p for p in assets.rglob("*") if p.is_file()] if assets.is_dir() else []
names = {p.name for p in files}

def count(suffix):
    return sum(1 for p in files if p.suffix == suffix)

for label, suffix in [("scenes", ".unity"), ("prefabs", ".prefab"), ("scripts", ".cs"),
                      ("textures", ".png"), ("materials", ".mat"), ("yaml assets", ".asset")]:
    print(f"{label:12} {count(suffix)}")

problems = []
if count(".unity") < 18:
    problems.append(f"expected 18 scenes, found {count('.unity')}")
for script in ("JoystickManager.cs", "FirstPersonCameraScript.cs", "PlayMakerFSM.cs"):
    if script not in names:
        problems.append(f"missing script: {script}")
if not any("explore_block" in str(p).lower() for p in files):
    problems.append("map bundle content (explore_block) not found")
if not any("mercenary" in str(p).lower() for p in files):
    problems.append("enemy bundle content (mercenary) not found")

for p in problems:
    print("MISSING:", p)
sys.exit(1 if problems else 0)
