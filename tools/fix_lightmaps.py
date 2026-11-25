"""Brings the Unity 4 lightmaps back: injects the PcPortLightmaps component into the YAML of scenes and streaming blocks.

Unity 6 only applies lightmaps from a LightingDataAsset; without one, renderers keep lightmapIndex -1 and the scenery
has no baked light. The indices and scale/offsets (m_LightmapIndex, m_LightmapTilingOffset) are still in the original
YAML; here they become a component (Assets/PCPort/PcPortLightmaps.cs) that applies them again at runtime. It edits the
text, like fix_static_batch.py, so Unity does not rewrite the scenes in the new format.
- Scenes: a new GameObject "PcPortLightmaps" with the textures of LightmapSettings.m_Lightmaps.
- Prefabs in Resources/bundles/scenes (mission streaming): the component goes on the root, with the indices only; the
  lightmaps are the ones of the open scene.

  python -I -X utf8 tools/fix_lightmaps.py BountyHunterPC/Assets    idempotent: skips files that have the component
  python -I -X utf8 tools/fix_lightmaps.py --selftest
"""
import pathlib
import re
import sys

SCRIPT_GUID = "995a9842b1964cf19648111138472887"  # PcPortLightmaps.cs.meta
ANCHOR_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)", re.M)
INDEX_RE = re.compile(r"m_LightmapIndex: (\d+)")
OFFSET_RE = re.compile(r"m_LightmapTilingOffset: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}")
LIGHTMAP_RE = re.compile(r"- m_Lightmap: \{fileID: 2800000, guid: ([0-9a-f]{32}), type: 3\}")
COMPONENT_RE = re.compile(r"^  - \d+: \{fileID: -?\d+\}\r?\n", re.M)


def blocks(text):
    """(class, fileID, start, end) of each YAML object."""
    marks = list(ANCHOR_RE.finditer(text))
    return [(int(m[1]), int(m[2]), m.start(), marks[i + 1].start() if i + 1 < len(marks) else len(text))
            for i, m in enumerate(marks)]


def lit_renderers(text, bl):
    """(fileID, index, scale/offset) of the lightmapped MeshRenderers (254/255 = none)."""
    out = []
    for cls, fid, a, b in bl:
        i, o = INDEX_RE.search(text, a, b), OFFSET_RE.search(text, a, b)
        if cls == 23 and i and o and int(i[1]) < 254:
            out.append((fid, int(i[1]), o.groups()))
    return out


def component(mb, go, internal, lightmaps, lit):
    lines = [f"--- !u!114 &{mb}", "MonoBehaviour:", "  m_ObjectHideFlags: 0", "  m_PrefabParentObject: {fileID: 0}",
             f"  m_PrefabInternal: {{fileID: {internal}}}", f"  m_GameObject: {{fileID: {go}}}", "  m_Enabled: 1",
             "  m_EditorHideFlags: 0", f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}", "  m_Name:"]
    for name, items in (("lightmaps", [f"{{fileID: 2800000, guid: {g}, type: 3}}" for g in lightmaps]),
                        ("renderers", [f"{{fileID: {fid}}}" for fid, _, _ in lit]),
                        ("indices", [str(idx) for _, idx, _ in lit]),
                        ("scaleOffsets", ["{x: %s, y: %s, z: %s, w: %s}" % o for _, _, o in lit])):
        lines.append(f"  {name}:" + ("" if items else " []"))
        lines.extend(f"  - {x}" for x in items)
    return "\n".join(lines) + "\n"


def fix_scene(text):
    bl = blocks(text)
    lit, maps = lit_renderers(text, bl), LIGHTMAP_RE.findall(text)
    if not maps:  # with no lit renderers of its own (streaming missions) the scene still gets the textures, for the blocks
        return text, 0
    go, tr, mb = (max(fid for _, fid, _, _ in bl) + k for k in (1, 2, 3))
    new = ("--- !u!1 &%d\nGameObject:\n  serializedVersion: 4\n  m_ObjectHideFlags: 0\n  m_PrefabParentObject: {fileID: 0}\n"
           "  m_PrefabInternal: {fileID: 0}\n  m_Component:\n  - 4: {fileID: %d}\n  - 114: {fileID: %d}\n  m_Layer: 0\n"
           "  m_Name: PcPortLightmaps\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n"
           "  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
           "--- !u!4 &%d\nTransform:\n  m_ObjectHideFlags: 0\n  m_PrefabParentObject: {fileID: 0}\n"
           "  m_PrefabInternal: {fileID: 0}\n  m_GameObject: {fileID: %d}\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
           "  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_Children: []\n"
           "  m_Father: {fileID: 0}\n") % (go, tr, mb, tr, go) + component(mb, go, 0, maps, lit)
    return append(text, new), len(lit)


def fix_prefab(text):
    bl = blocks(text)
    lit = lit_renderers(text, bl)
    if not lit:
        return text, 0
    root_tr = next(b for b in bl if b[0] == 4 and "m_Father: {fileID: 0}" in text[b[2]:b[3]])
    root = int(re.search(r"m_GameObject: \{fileID: (-?\d+)\}", text[root_tr[2]:root_tr[3]])[1])
    _, _, a, b = next(x for x in bl if x[0] == 1 and x[1] == root)
    internal = re.search(r"m_PrefabInternal: \{fileID: (-?\d+)\}", text[a:b])[1]
    mb = max(fid for _, fid, _, _ in bl) + 1
    end = list(COMPONENT_RE.finditer(text, a, b))[-1].end()  # end of the root's m_Component list
    nl = "\r\n" if "\r\n" in text else "\n"
    text = text[:end] + f"  - 114: {{fileID: {mb}}}{nl}" + text[end:]
    return append(text, component(mb, root, internal, [], lit)), len(lit)


def append(text, new):
    nl = "\r\n" if "\r\n" in text else "\n"
    return text.rstrip("\r\n") + nl + new.replace("\n", nl)


def main(assets):
    assets = pathlib.Path(assets)
    files = sorted((assets / "Scenes").rglob("*.unity")) + sorted((assets / "Resources/bundles/scenes").rglob("*.prefab"))
    done = 0
    for path in files:
        text = path.read_text(encoding="utf-8", newline="")
        if SCRIPT_GUID in text:
            continue
        new, n = (fix_scene if path.suffix == ".unity" else fix_prefab)(text)
        if new != text:
            path.write_text(new, encoding="utf-8", newline="")
            print(f"  {path.relative_to(assets)}: {n} lightmapped renderers")
            done += 1
    print(f"{done} files got PcPortLightmaps")
    return 0


SCENE = """%YAML 1.1
--- !u!157 &4
LightmapSettings:
  m_Lightmaps:
  - m_Lightmap: {fileID: 2800000, guid: d9e644492159a2648b19a3baf9d21c42, type: 3}
    m_IndirectLightmap: {fileID: 0}
--- !u!1 &2
GameObject:
  m_Component:
  - 4: {fileID: 5}
  - 23: {fileID: 7}
  m_Name: rock
--- !u!4 &5
Transform:
  m_GameObject: {fileID: 2}
  m_Father: {fileID: 0}
--- !u!23 &7
MeshRenderer:
  m_GameObject: {fileID: 2}
  m_LightmapIndex: 1
  m_LightmapTilingOffset: {x: 0.083, y: 0.083, z: 0.42, w: -1.5E-05}
--- !u!23 &9
MeshRenderer:
  m_LightmapIndex: 255
  m_LightmapTilingOffset: {x: 1, y: 1, z: 0, w: 0}
"""


def selftest():
    new, n = fix_scene(SCENE)
    assert n == 1 and new.startswith(SCENE.rstrip("\n")), n
    streaming, n = fix_scene(SCENE.replace("m_LightmapIndex: 1\n", "m_LightmapIndex: 255\n"))
    assert n == 0 and "  lightmaps:\n  - {fileID: 2800000, guid: d9e6" in streaming and "  renderers: []\n" in streaming, streaming
    assert "m_Name: PcPortLightmaps" in new and "  renderers:\n  - {fileID: 7}\n  indices:\n  - 1\n" in new, new
    assert "{x: 0.083, y: 0.083, z: 0.42, w: -1.5E-05}" in new and "guid: d9e644492159a2648b19a3baf9d21c42" in new
    assert "--- !u!1 &10\n" in new and "m_GameObject: {fileID: 10}" in new, new  # new ids after the largest one (9)
    prefab = SCENE.replace("--- !u!157 &4\nLightmapSettings:\n  m_Lightmaps:\n  - m_Lightmap: {fileID: 2800000, guid: "
                           "d9e644492159a2648b19a3baf9d21c42, type: 3}\n    m_IndirectLightmap: {fileID: 0}\n", "")
    prefab = prefab.replace("  m_Component:", "  m_PrefabInternal: {fileID: 100100000}\n  m_Component:")
    new, n = fix_prefab(prefab.replace("\n", "\r\n"))
    assert n == 1 and "  - 23: {fileID: 7}\r\n  - 114: {fileID: 10}\r\n  m_Name: rock" in new, new
    assert "m_PrefabInternal: {fileID: 100100000}\r\n  m_GameObject: {fileID: 2}" in new and "lightmaps: []" in new
    assert "\n" not in new.replace("\r\n", ""), "mixed line endings"
    print("selftest ok")
    return 0


if __name__ == "__main__":
    if sys.argv[1:] == ["--selftest"]:
        sys.exit(selftest())
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    sys.exit(main(sys.argv[1]))
