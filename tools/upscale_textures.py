"""Texture remaster: AI upscale (Real-ESRGAN x4plus) of the game's 3D textures.

  python -I -X utf8 tools/upscale_textures.py --all        upscale every 3D texture into build/upscaled-textures/
  python -I -X utf8 tools/upscale_textures.py --apply      copy build/upscaled-textures/ into Assets
  python -I -X utf8 tools/upscale_textures.py --restore    put the original textures back from the backup
  python -I -X utf8 tools/upscale_textures.py --selftest

"3D textures" = PNGs used by materials whose shader is not an NGUI one (Resources/shaders), outside the UI folders
(Resources/*, except Resources/bundles), 128 to 1024 px. The UI is left alone: NGUI sizes widgets from the atlas size
in pixels. --all writes outside Assets (Unity may be running meanwhile) and resumes where it stopped. Before a texture
is replaced, the original (PNG + .meta) goes to build/texture-backup. Unity caps each texture at 2048 px on import
(maxTextureSize in the .meta): 512 becomes 2048, 1024 becomes 4096 and is capped to 2048. Real-ESRGAN lives in
tools/realesrgan/ (tools/setup.py downloads it).
"""
import pathlib
import re
import shutil
import struct
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
ASSETS = ROOT / "BountyHunterPC" / "Assets"
BACKUP = ROOT / "build" / "texture-backup"
STAGE = ROOT / "build" / "upscaled-textures"
EXE = ROOT / "tools" / "realesrgan" / "realesrgan-ncnn-vulkan.exe"
GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
SHADER_RE = re.compile(r"m_Shader: \{fileID: \d+, guid: ([0-9a-f]{32})")
TEX_RE = re.compile(r"m_Texture: \{fileID: 2800000, guid: ([0-9a-f]{32})")


def parse_material(text):
    """(shader guid, texture guids) of a .mat."""
    shader = SHADER_RE.search(text)
    return (shader[1] if shader else None), set(TEX_RE.findall(text))


def png_size(path):
    with open(path, "rb") as f:
        return struct.unpack(">II", f.read(24)[16:24])


def material_textures():
    """3D PNGs to upscale (see the module docstring), judged by their original size."""
    guid_path = {}
    for meta in ASSETS.rglob("*.meta"):
        m = GUID_RE.search(meta.read_text(encoding="utf-8", errors="replace"))
        if m:
            guid_path[m[1]] = meta.with_suffix("")
    ngui = {g for g, p in guid_path.items() if p.suffix == ".shader" and p.parent == ASSETS / "Resources" / "shaders"}
    used, ui = set(), set()
    for mat in ASSETS.rglob("*.mat"):
        shader, textures = parse_material(mat.read_text(encoding="utf-8", errors="replace"))
        (ui if shader in ngui else used).update(textures)
    files = []
    for g in used - ui:
        p = guid_path.get(g)
        if p is None or p.suffix.lower() != ".png":
            continue
        rel = p.relative_to(ASSETS)
        if rel.parts[0] == "Resources" and rel.parts[1] != "bundles":
            continue
        orig = BACKUP / rel if (BACKUP / rel).exists() else p
        if 128 <= max(png_size(orig)) <= 1024:
            files.append(p)
    return sorted(files)


def backup(src):
    """Keep the original (PNG + .meta) once and return its path."""
    orig = BACKUP / src.relative_to(ASSETS)
    if not orig.exists():  # on a second run the backup already holds the original: never overwrite it
        orig.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, orig)
        shutil.copy2(src.with_name(src.name + ".meta"), orig.with_name(src.name + ".meta"))
    return orig


def enlarge(orig, dst):
    dst.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run([str(EXE), "-i", str(orig), "-o", str(dst), "-n", "realesrgan-x4plus"], check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def upscale_all():
    files = material_textures()
    print(f"{len(files)} 3D textures", flush=True)
    for i, src in enumerate(files, 1):
        rel = src.relative_to(ASSETS)
        if (BACKUP / rel).exists() and png_size(src) != png_size(BACKUP / rel):
            continue  # already upscaled in Assets
        dst = STAGE / rel
        if not dst.exists():
            enlarge(backup(src), dst)
        if i % 25 == 0 or i == len(files):
            print(f"  {i}/{len(files)}", flush=True)
    return 0


def apply():
    n = 0
    for f in STAGE.rglob("*.png"):
        backup(ASSETS / f.relative_to(STAGE))
        shutil.copy2(f, ASSETS / f.relative_to(STAGE))
        n += 1
    print(f"{n} textures copied from {STAGE} into Assets")
    return 0


def restore():
    n = 0
    for b in BACKUP.rglob("*"):
        if b.is_file():
            shutil.copy2(b, ASSETS / b.relative_to(BACKUP))
            n += 1
    print(f"{n} files restored from {BACKUP}")
    return 0


def selftest():
    mat = ("  m_Shader: {fileID: 4800000, guid: 22a23ebc775ccb04d96d0929f041d661, type: 3}\n"
           "    - _MainTex:\n        m_Texture: {fileID: 2800000, guid: 0123456789abcdef0123456789abcdef, type: 3}\n"
           "    - _Mask:\n        m_Texture: {fileID: 0}\n")
    assert parse_material(mat) == ("22a23ebc775ccb04d96d0929f041d661", {"0123456789abcdef0123456789abcdef"}), parse_material(mat)
    print("selftest ok")
    return 0


if __name__ == "__main__":
    modes = {"--all": upscale_all, "--apply": apply, "--restore": restore, "--selftest": selftest}
    arg = sys.argv[1] if len(sys.argv) > 1 else ""
    if arg not in modes:
        sys.exit(__doc__)
    sys.exit(modes[arg]())
