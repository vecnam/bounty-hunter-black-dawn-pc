"""Builds the PC port from your own copy of Bounty Hunter: Black Dawn v1.25.01 for Android.

  python -I -X utf8 tools/setup.py <file.xapk | main.9.com.ifreyrgames.blackdawn.obb> [--no-upscale]
  python -I -X utf8 tools/setup.py [--from STEP] [--no-upscale]    resume, or redo from STEP onwards
  python -I -X utf8 tools/setup.py --selftest

Steps, in order: extract, export, populate, fixes, particles, static-batch, navmesh, lightmaps, upscale, release.
Progress is kept in build/setup-state.txt: running again resumes at the first unfinished step. The game ends up in
game/BountyHunter.exe. --no-upscale skips the AI texture upscale (the slowest step; it needs a Vulkan GPU); to add it
later, run --from upscale.
"""
import argparse
import hashlib
import shutil
import subprocess
import sys
import time
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TOOLS = ROOT / "tools"
BUILD = ROOT / "build"
EXTRACTED = ROOT / "extracted"
ASSETS = ROOT / "BountyHunterPC" / "Assets"
EXPORT = ROOT / "ripped" / "ExportedProject" / "Assets"
STATE = BUILD / "setup-state.txt"
OBB_NAME = "main.9.com.ifreyrgames.blackdawn.obb"
OBB_SHA256 = "9553549644578258388e3fd0a73b2855411a8f4bfa5aa8e045e15a378dc0aaac"
DOWNLOADS = {  # folder in tools/: (url, sha256, file that shows it is installed)
    "AssetRipper": ("https://github.com/AssetRipper/AssetRipper/releases/download/2.0.0/AssetRipper_win_x64.zip",
                    "9a7ef0e7c5c3ea5b90b4e6d855e2d98d5f7ec8c3f9e26fccbc194c6a7b01baf7", "AssetRipper.GUI.Free.exe"),
    "realesrgan": ("https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesrgan-ncnn-vulkan-20220424-windows.zip",
                   "abc02804e17982a3be33675e4d471e91ea374e65b70167abc09e31acb412802d", "realesrgan-ncnn-vulkan.exe"),
}
STEPS = ["extract", "export", "populate", "fixes", "particles", "static-batch", "navmesh", "lightmaps", "upscale", "release"]


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def run(*cmd):
    cmd = [str(c) for c in cmd]
    print(">", " ".join(cmd), flush=True)
    if subprocess.run(cmd, cwd=ROOT).returncode:
        sys.exit(f"failed: {' '.join(cmd)} (Unity logs are in build/*.log)")


def py(script, *args):
    run(sys.executable, "-I", "-X", "utf8", TOOLS / script, *args)


def git_bash():
    """Git for Windows' bash.exe, which runs tools/rip.sh (the bash on PATH may be WSL's)."""
    git = shutil.which("git")
    for base in (Path(git).resolve().parents[1:3] if git else []):
        if (base / "bin" / "bash.exe").exists():
            return base / "bin" / "bash.exe"
    sys.exit("Git for Windows not found: install it from https://git-scm.com and run again")


def flat_name(rel):
    """enemy/human/x.assetbundle -> enemy__human__x.assetbundle (map_bundles.py reads the original path back)."""
    return "__".join(Path(rel).parts)


def ensure_tool(name):
    url, digest, exe = DOWNLOADS[name]
    folder = TOOLS / name
    if (folder / exe).exists():
        return
    folder.mkdir(exist_ok=True)
    archive = folder / url.rsplit("/", 1)[1]
    if not archive.exists() or sha256(archive) != digest:
        print(f"downloading {url}", flush=True)
        urllib.request.urlretrieve(url, archive)
    if sha256(archive) != digest:
        archive.unlink()
        sys.exit(f"{archive.name} does not have the expected sha256; run again to download it again")
    with zipfile.ZipFile(archive) as z:
        z.extractall(folder)


def step_extract(source):
    if not source:
        sys.exit(f"give the game file: python tools/setup.py <file.xapk or {OBB_NAME}>")
    source = Path(source)
    EXTRACTED.mkdir(exist_ok=True)
    obb = source
    if source.suffix.lower() == ".xapk":
        obb = EXTRACTED / OBB_NAME
        with zipfile.ZipFile(source) as z:
            member = next((n for n in z.namelist() if n.endswith(OBB_NAME)), None)
            if member is None:
                sys.exit(f"{source} has no {OBB_NAME}: is it Bounty Hunter: Black Dawn 1.25.01?")
            with z.open(member) as src, open(obb, "wb") as dst:
                shutil.copyfileobj(src, dst, 1 << 20)
    if sha256(obb) != OBB_SHA256:
        sys.exit(f"{obb} is not the expected game data: the port needs v1.25.01 ({OBB_NAME}, sha256 {OBB_SHA256})")
    for folder in (EXTRACTED / "obb", EXTRACTED / "bundles-flat"):
        shutil.rmtree(folder, ignore_errors=True)
    with zipfile.ZipFile(obb) as z:
        z.extractall(EXTRACTED / "obb")
    if obb != source:
        obb.unlink()
    bundles = EXTRACTED / "obb" / "assets" / "assetbundles_android"
    (EXTRACTED / "bundles-flat").mkdir()
    for f in bundles.rglob("*"):
        if f.is_file():
            shutil.copyfile(f, EXTRACTED / "bundles-flat" / flat_name(f.relative_to(bundles)))


def step_export():
    ensure_tool("AssetRipper")
    for out in (ROOT / "ripped", BUILD / "ripped-bundles"):  # leftovers of an interrupted export
        shutil.rmtree(out, ignore_errors=True)
    bash = git_bash()
    run(bash, "tools/rip.sh")
    run(bash, "tools/rip.sh", "extracted/bundles-flat", "build/ripped-bundles")
    py("check_rip.py", "ripped")


def step_populate():
    if (ASSETS / "Scripts").exists() or (ASSETS / "Plugins").exists():
        sys.exit("BountyHunterPC/Assets already has the game in it. To rebuild it, first save any change to the game "
                 "code (tools/patches.py save), delete everything in BountyHunterPC/Assets except PCPort and Shader "
                 "(and their .meta files), then run: python tools/setup.py --from populate")
    for entry in EXPORT.iterdir():
        if entry.name.split(".")[0] in ("Scripts", "Plugins", "Shader"):  # code comes from patches/, shaders from git
            continue
        if entry.is_dir():
            shutil.copytree(entry, ASSETS / entry.name, dirs_exist_ok=True)
        else:
            shutil.copy2(entry, ASSETS / entry.name)
    py("patches.py", "apply")


def step_upscale(enabled):
    if not enabled:
        print("skipped (--no-upscale); add it later with: python tools/setup.py --from upscale")
        return
    ensure_tool("realesrgan")
    py("upscale_textures.py", "--all")
    py("upscale_textures.py", "--apply")


def start_index(done, from_step):
    if from_step:
        return STEPS.index(from_step)
    return next((i for i, s in enumerate(STEPS) if i >= len(done) or done[i] != s), len(STEPS))


def selftest():
    assert flat_name("enemy/human/android_prefab_mob_smg.assetbundle") == "enemy__human__android_prefab_mob_smg.assetbundle"
    assert start_index([], None) == 0
    assert start_index(["extract", "export"], None) == 2
    assert start_index(["extract", "export"], "extract") == 0
    assert start_index(STEPS, None) == len(STEPS)
    print("selftest ok")


def main():
    ap = argparse.ArgumentParser(description="Builds the PC port from your copy of the game (see tools/setup.py).")
    ap.add_argument("game_file", nargs="?", help=".xapk or .obb of Bounty Hunter: Black Dawn 1.25.01")
    ap.add_argument("--from", dest="from_step", choices=STEPS, help="redo from this step onwards")
    ap.add_argument("--no-upscale", action="store_true", help="skip the AI texture upscale")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    if args.selftest:
        return selftest()

    sys.path.insert(0, str(TOOLS))
    import unity  # noqa: E402  (tools/unity.py knows where Unity is)
    if not Path(unity.UNITY).exists():
        sys.exit(f"Unity not found at {unity.UNITY}. Install Unity 6000.0.85f2 with Unity Hub (and a free Personal "
                 "license), or set UNITY_EXE to its Unity.exe.")
    git_bash()

    BUILD.mkdir(exist_ok=True)
    done = STATE.read_text(encoding="utf-8").split() if STATE.exists() else []
    start = start_index(done, args.from_step)
    if start == len(STEPS):
        print("Everything is done: play game/BountyHunter.exe (use --from STEP to redo a step).")
        return
    actions = {
        "extract": lambda: step_extract(args.game_file),
        "export": step_export,
        "populate": step_populate,
        "fixes": lambda: (py("fix_dup_ids.py", "BountyHunterPC/Assets/Resources"), py("port_shaders.py"),
                          py("map_bundles.py", "--apply")),
        "particles": lambda: py("unity.py", "exec", "PcPortParticleConverter.ConvertAll"),
        "static-batch": lambda: py("fix_static_batch.py", "BountyHunterPC/Assets/Scenes"),
        "navmesh": lambda: py("unity.py", "exec", "PcPortNavMeshBaker.BakeAll"),
        "lightmaps": lambda: py("fix_lightmaps.py", "BountyHunterPC/Assets"),
        "upscale": lambda: step_upscale(not args.no_upscale),
        "release": lambda: py("unity.py", "release"),
    }
    for i in range(start, len(STEPS)):
        began = time.monotonic()
        print(f"\n== step {i + 1}/{len(STEPS)}: {STEPS[i]}", flush=True)
        actions[STEPS[i]]()
        STATE.write_text("".join(f"{s}\n" for s in STEPS[:i + 1]), encoding="utf-8")
        print(f"== {STEPS[i]} done in {(time.monotonic() - began) / 60:.1f} min", flush=True)
    print("\nDone: play game/BountyHunter.exe")


if __name__ == "__main__":
    main()
