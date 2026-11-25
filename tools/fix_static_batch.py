"""Converts the Unity 4 static batching of the exported scenes to the format Unity 6 reads.

In the Android build, Unity 4 merged the static scenery into "Combined Mesh (root: scene)", already in world
coordinates, and each MeshRenderer keeps in m_SubsetIndices which submeshes of that mesh are its own. Unity 6 ignores
that field: each object drew the start of the combined mesh with its own transform applied again (Instance0 was black),
and in the smaller scenes the build batched everything again on top of that (City1-3).

Here m_SubsetIndices becomes m_StaticBatchInfo (firstSubMesh, subMeshCount) with no root, i.e. identity (as in
Unity 4), and the GameObject loses the BatchingStatic flag so the build does not batch it again.

Usage:
  python -I -X utf8 tools/fix_static_batch.py <.unity scenes or folders...>   fix in place, report per file
  python -I -X utf8 tools/fix_static_batch.py --selftest
"""
import pathlib
import re
import struct
import sys

HDR = re.compile(r"^--- !u!(\d+) &(\d+)", re.M)
SUBSET = re.compile(r"m_SubsetIndices: ([0-9a-f]+)")
FLAGS = re.compile(r"m_StaticEditorFlags: (\d+)")
BATCHING_STATIC = 4  # UnityEditor.StaticEditorFlags.BatchingStatic


def fix_text(text):
    """Returns (new text, number of renderers converted)."""
    nl = "\r\n" if "\r\n" in text else "\n"
    parts = HDR.split(text)  # [before, class, id, body, class, id, body, ...]
    body_of = {parts[i + 1]: i + 2 for i in range(1, len(parts), 3)}
    count = 0
    for i in range(1, len(parts), 3):
        m = SUBSET.search(parts[i + 2]) if parts[i] == "23" else None
        if not m:
            continue
        idx = struct.unpack(f"<{len(m.group(1)) // 8}I", bytes.fromhex(m.group(1)))
        # the new format only stores one contiguous range; Unity 4 batching gives each renderer consecutive submeshes
        assert list(idx) == list(range(idx[0], idx[0] + len(idx))), f"subsets not contiguous: {idx}"
        info = f"m_StaticBatchInfo:{nl}    firstSubMesh: {idx[0]}{nl}    subMeshCount: {len(idx)}"
        parts[i + 2] = parts[i + 2][:m.start()] + info + parts[i + 2][m.end():]
        go = body_of[re.search(r"m_GameObject: \{fileID: (\d+)\}", parts[i + 2]).group(1)]
        parts[go] = FLAGS.sub(lambda f: f"m_StaticEditorFlags: {int(f.group(1)) & ~BATCHING_STATIC}", parts[go], count=1)
        count += 1
    out = parts[0] + "".join(f"--- !u!{parts[i]} &{parts[i + 1]}{parts[i + 2]}" for i in range(1, len(parts), 3))
    return out, count


SAMPLE = """%YAML 1.1
--- !u!1 &10
GameObject:
  m_Component:
  - 23: {fileID: 20}
  m_StaticEditorFlags: 4294967295
--- !u!23 &20
MeshRenderer:
  m_GameObject: {fileID: 10}
  m_SubsetIndices: 0300000004000000
  m_StaticBatchRoot: {fileID: 0}
--- !u!1 &30 stripped
GameObject:
  m_StaticEditorFlags: 4294967295
--- !u!23 &40
MeshRenderer:
  m_GameObject: {fileID: 30}
  m_SubsetIndices:
"""


def selftest():
    out, n = fix_text(SAMPLE)
    assert n == 1, n
    assert "m_StaticBatchInfo:\n    firstSubMesh: 3\n    subMeshCount: 2\n  m_StaticBatchRoot" in out, out
    assert out.count("m_StaticEditorFlags: 4294967291") == 1 and "m_StaticEditorFlags: 4294967295" in out, out
    assert "--- !u!1 &30 stripped\n" in out and out.count("m_SubsetIndices:") == 1, out
    assert fix_text(out) == (out, 0)  # idempotent
    crlf, n = fix_text(SAMPLE.replace("\n", "\r\n"))
    assert n == 1 and "\n" not in crlf.replace("\r\n", ""), "mixed line endings"
    try:
        fix_text(SAMPLE.replace("0300000004000000", "0300000005000000"))
        raise AssertionError("accepted non-contiguous subsets")
    except AssertionError as e:
        assert "not contiguous" in str(e), e
    print("selftest ok")
    return 0


def main(args):
    if args == ["--selftest"]:
        return selftest()
    if not args:
        sys.exit(__doc__)
    files = [f for a in map(pathlib.Path, args) for f in (sorted(a.rglob("*.unity")) if a.is_dir() else [a])]
    total = 0
    for f in files:
        text = f.read_bytes().decode("utf-8")
        out, n = fix_text(text)
        if n:
            f.write_bytes(out.encode("utf-8"))
            print(f"{n:4}  {f}")
            total += n
    print(f"{total} renderers converted in {len(files)} scenes")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
