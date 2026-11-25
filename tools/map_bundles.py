"""Links each original Android asset bundle to its asset in the project and moves it to Resources/bundles/<original path>.

Usage:
  python -I -X utf8 tools/map_bundles.py            report only (moves nothing)
  python -I -X utf8 tools/map_bundles.py --apply    move asset + .meta (the GUID is kept)

Needs build/ripped-bundles, made by `bash tools/rip.sh extracted/bundles-flat build/ripped-bundles`, where each
original bundle was exported to its own folder (name = original path with "/" replaced by "__"). The main export merged
bundles with the same file name (explore_block_1 from 4 missions became explore_block_1, _0, _1, _2), so the right pair
is found by content: GameObject names + positions for prefabs, bytes for textures.
Scope: map blocks (scenes/<mission>/android_prefab_*), enemies and enemy textures. Scenery shaders, materials and
textures are not needed: the prefabs already reference them directly (PcPortBundle returns a null mainAsset for them).
"""
import hashlib
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
REEXPORT = ROOT / "build/ripped-bundles/ExportedProject/Assets/AssetBundles"
PROJECT = ROOT / "BountyHunterPC/Assets/AssetBundles"
DEST = ROOT / "BountyHunterPC/Assets/Resources/bundles"


def fingerprint(path):
    if path.suffix == ".prefab":
        t = path.read_text(encoding="utf-8")
        key = (sorted(re.findall(r"^  m_Name: (.*)$", t, re.M)), sorted(re.findall(r"^  m_LocalPosition: (.*)$", t, re.M)))
        return hashlib.md5(repr(key).encode()).hexdigest()
    return hashlib.md5(path.read_bytes()).hexdigest()


def in_scope(orig):
    parts = orig.split("/")
    return parts[0] == "enemy" or (parts[0] == "scenes" and len(parts) == 3)


def plan():
    moves, problems, used = [], [], {}
    for folder in sorted(REEXPORT.glob("*.assetbundle")):
        orig = folder.name[: -len(".assetbundle")].replace("__", "/").lower()
        if not in_scope(orig) or list(DEST.glob(orig + ".*")):  # out of scope, or already moved by an earlier --apply
            continue
        stem = orig.rsplit("/", 1)[1].removeprefix("android_prefab_")
        mains = [f for f in folder.iterdir() if f.suffix != ".meta" and f.stem.lower() == stem]
        if len(mains) > 1:  # same name as prefab, mesh (.asset) and material: the bundle's main asset is the prefab
            mains = [f for f in mains if f.suffix == ".prefab"]
        if len(mains) != 1:
            problems.append(f"{orig}: main asset ambiguous or missing {[m.name for m in mains]}")
            continue
        main = mains[0]
        pfolder = PROJECT / (orig.rsplit("/", 1)[1] + ".assetbundle")
        pat = re.compile(re.escape(main.stem) + r"(_\d+)?" + re.escape(main.suffix) + "$", re.I)
        cands = [f for f in pfolder.glob("*" + main.suffix) if pat.fullmatch(f.name)] if pfolder.is_dir() else []
        fp = fingerprint(main)
        hits = [c for c in cands if fingerprint(c) == fp and c not in used]  # identical copies: any free one will do
        if not hits:
            problems.append(f"{orig}: {len(hits)} matches among {[c.name for c in cands]}")
            continue
        used[hits[0]] = orig
        moves.append((hits[0], DEST / (orig + main.suffix.lower())))
    return moves, problems


def main(args):
    moves, problems = plan()
    print(f"{len(moves)} bundles mapped; {len(problems)} problems")
    for p in problems:
        print("  PROBLEM:", p)
    if "--apply" in args:
        if problems:
            sys.exit("nothing moved while there are problems")
        for src, dst in moves:
            dst.parent.mkdir(parents=True, exist_ok=True)
            src.rename(dst)
            src.with_name(src.name + ".meta").rename(dst.with_name(dst.name + ".meta"))
        print(f"moved {len(moves)} assets to {DEST}")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
