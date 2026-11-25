"""Patches for the decompiled game code.

The decompiled code (BountyHunterPC/Assets/Scripts and Assets/Plugins) is never committed. It comes from the
AssetRipper export of your own copy of the game, and our changes to it live in patches/:
  patches/<Scripts|Plugins>/<path>.patch   one unified diff per changed file
  patches/deleted.txt                       files removed from the export
  patches/renamed.txt                       "old -> new" renames (content unchanged)
Paths are relative to Assets/. The export has CRLF line endings; both sides are compared and written as LF.

  python -I -X utf8 tools/patches.py apply [--export DIR]   copy the export's code into the project, apply patches/
  python -I -X utf8 tools/patches.py save [--export DIR]    rewrite patches/ from the project (after editing that code)
  python -I -X utf8 tools/patches.py --selftest

--export points to the Assets folder of an export (default: ripped/ExportedProject/Assets).
"""
import argparse
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOTS = ("Scripts", "Plugins")
REPO = Path(__file__).resolve().parent.parent
# Git for Windows ships core.autocrlf=true, and git apply would then write CRLF: keep everything LF.
GIT = ["git", "-c", "core.autocrlf=false", "-c", "core.eol=lf", "-c", "core.quotepath=off"]


def lf(data):
    return data.replace(b"\r\n", b"\n")


def layout(root, export):
    """(export Assets, project Assets, patches folder) for a repo root."""
    return (Path(export) if export else root / "ripped" / "ExportedProject" / "Assets",
            root / "BountyHunterPC" / "Assets", root / "patches")


def tree(assets):
    """{"Scripts/x/y.cs": Path} for every file under the decompiled roots."""
    return {p.relative_to(assets).as_posix(): p
            for r in ROOTS if (assets / r).is_dir() for p in (assets / r).rglob("*") if p.is_file()}


def read_list(path):
    return [line.strip() for line in path.read_text(encoding="utf-8").splitlines() if line.strip()] \
        if path.exists() else []


def write_list(path, items):
    if items:
        path.write_text("".join(f"{item}\n" for item in items), encoding="utf-8", newline="\n")
    elif path.exists():
        path.unlink()


def apply(root=REPO, export=None):
    export, project, patches = layout(root, export)
    busy = [f"Assets/{r}" for r in ROOTS if (project / r).exists()]
    if busy:
        sys.exit(f"{' and '.join(busy)} already exist. If you changed that code, run `tools/patches.py save` first; "
                 "then delete the folder(s) and run apply again.")
    source = tree(export)
    if not source:
        sys.exit(f"no decompiled code in {export}: run tools/setup.py first")
    for rel, src in source.items():
        dst = project / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        dst.write_bytes(lf(src.read_bytes()))
    deleted, renamed = read_list(patches / "deleted.txt"), read_list(patches / "renamed.txt")
    for rel in deleted:
        if not (project / rel).exists():
            sys.exit(f"{rel} (patches/deleted.txt) is not in the export: is it the game version the patches expect?")
        (project / rel).unlink()
        parent = (project / rel).parent
        while parent != project and not any(parent.iterdir()):  # Unity would give the empty folder a .meta
            parent.rmdir()
            parent = parent.parent
    for line in renamed:
        old, new = line.split(" -> ")
        (project / old).rename(project / new)
    files = sorted(patches.rglob("*.patch"))
    if files:
        cmd = GIT + ["apply", "--whitespace=nowarn", f"--directory={project.relative_to(root).as_posix()}", "-"]
        if subprocess.run(cmd, cwd=root, input=b"".join(lf(p.read_bytes()) for p in files)).returncode:
            sys.exit("git apply failed (nothing was patched): the export does not match the patches. "
                     f"Delete Assets/{' and Assets/'.join(ROOTS)} before trying again.")
    print(f"apply: {len(source)} files from the export, {len(files)} patched, {len(deleted)} deleted, "
          f"{len(renamed)} renamed")


def save(root=REPO, export=None):
    export, project, patches = layout(root, export)
    old, new = tree(export), tree(project)
    if not old or not new:
        sys.exit(f"nothing to compare: {export if not old else project} has no {' or '.join(ROOTS)}")
    gone, added = sorted(old.keys() - new.keys()), sorted(new.keys() - old.keys())
    by_content = {}
    for rel in gone:
        by_content.setdefault(lf(old[rel].read_bytes()), []).append(rel)
    renamed, extra, used, generated = [], [], set(), 0
    for rel in added:
        twin = next((g for g in by_content.get(lf(new[rel].read_bytes()), []) if g not in used), None)
        if twin:
            used.add(twin)
            renamed.append(f"{twin} -> {rel}")
        elif rel.endswith(".meta"):
            generated += 1  # Unity writes a .meta for each script the export left without one; it does it again
        else:
            extra.append(rel)
    if extra:
        sys.exit("new files in the decompiled code are not saved; put new code in Assets/PCPort:\n  " + "\n  ".join(extra))
    deleted = [g for g in gone if g not in used]
    changed = [rel for rel in sorted(old.keys() & new.keys()) if lf(old[rel].read_bytes()) != lf(new[rel].read_bytes())]

    chunks = []
    if changed:
        with tempfile.TemporaryDirectory() as tmp:
            for rel in changed:
                for side, src in (("a", old[rel]), ("b", new[rel])):
                    dst = Path(tmp, side, rel)
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    dst.write_bytes(lf(src.read_bytes()))
            out = subprocess.run(GIT + ["diff", "--no-index", "--no-prefix", "--no-color", "--no-ext-diff",
                                        "--no-renames", "a", "b"], cwd=tmp, capture_output=True)
        if out.returncode not in (0, 1):
            sys.exit(out.stderr.decode(errors="replace"))
        chunks = [c for c in re.split(rb"(?m)^(?=diff --git )", out.stdout) if c]
        if len(chunks) != len(changed):
            sys.exit(f"git diff returned {len(chunks)} files for {len(changed)} changed ones")

    for p in patches.rglob("*.patch"):
        p.unlink()
    for d in sorted((d for d in patches.rglob("*") if d.is_dir()), reverse=True):
        if not any(d.iterdir()):
            d.rmdir()
    patches.mkdir(exist_ok=True)
    for chunk in chunks:
        rel = chunk.split(b"\n", 1)[0].split(b" ")[2].decode()[2:]  # "diff --git a/<rel> b/<rel>"
        dst = patches / f"{rel}.patch"
        dst.parent.mkdir(parents=True, exist_ok=True)
        dst.write_bytes(chunk)
    write_list(patches / "deleted.txt", deleted)
    write_list(patches / "renamed.txt", renamed)
    print(f"save: {len(chunks)} patches, {len(deleted)} deleted, {len(renamed)} renamed "
          f"({generated} Unity-generated .meta files ignored)")


def selftest():
    def write(path, data):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def refused(fn):
        try:
            fn()
        except SystemExit:
            return True
        return False

    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        export, project, patches = layout(root, None)
        for rel, data in {
            "Scripts/A/Edited.cs": b"class A {\r\n  int x = 1;\r\n}\r\n",
            "Scripts/A/Edited.cs.meta": b"guid: 1\r\n",
            "Scripts/A/NoNewline.cs": b"class B {}",
            "Scripts/A/Gone.cs": b"class C {}\r\n",
            "Scripts/A/Gone.cs.meta": b"guid: 3\r\n",
            "Scripts/A/OLDName.cs": b"class D {}\r\n",
            "Scripts/A/OLDName.cs.meta": b"guid: 4\r\n",
            "Plugins/P.cs": b"class P {}\r\n",
        }.items():
            write(export / rel, data)

        apply(root)  # no patches yet: a plain copy, in LF
        assert (project / "Scripts/A/Edited.cs").read_bytes() == b"class A {\n  int x = 1;\n}\n"

        # a contributor edits the code (the editor writes CRLF), deletes, renames, and Unity adds a .meta
        (project / "Scripts/A/Edited.cs").write_bytes(b"class A {\r\n  int x = 2;\r\n}\r\n")
        (project / "Scripts/A/NoNewline.cs").write_bytes(b"class B { }")
        (project / "Scripts/A/Gone.cs").unlink()
        (project / "Scripts/A/Gone.cs.meta").unlink()
        for ext in ("", ".meta"):
            (project / f"Scripts/A/OLDName.cs{ext}").rename(project / f"Scripts/A/NewName.cs{ext}")
        write(project / "Scripts/A/Generated.cs.meta", b"guid: 9\n")
        save(root)
        assert sorted(p.relative_to(patches).as_posix() for p in patches.rglob("*.patch")) == \
            ["Scripts/A/Edited.cs.patch", "Scripts/A/NoNewline.cs.patch"]
        assert read_list(patches / "deleted.txt") == ["Scripts/A/Gone.cs", "Scripts/A/Gone.cs.meta"]
        assert read_list(patches / "renamed.txt") == ["Scripts/A/OLDName.cs -> Scripts/A/NewName.cs",
                                                      "Scripts/A/OLDName.cs.meta -> Scripts/A/NewName.cs.meta"]
        expected = {rel: lf(p.read_bytes()) for rel, p in tree(project).items() if not rel.endswith("Generated.cs.meta")}

        assert refused(lambda: apply(root)), "apply must not overwrite existing code"
        for r in ROOTS:
            shutil.rmtree(project / r)
        apply(root)
        assert {rel: p.read_bytes() for rel, p in tree(project).items()} == expected

        write(project / "Scripts/A/New.cs", b"class N {}\n")
        assert refused(lambda: save(root)), "a new .cs in the decompiled code must be refused"
    print("selftest ok")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description="Patches for the decompiled game code (see the module docstring).")
    ap.add_argument("command", nargs="?", choices=["apply", "save"])
    ap.add_argument("--export", help="Assets folder of an AssetRipper export")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    if args.selftest:
        selftest()
    elif args.command:
        (apply if args.command == "apply" else save)(export=args.export)
    else:
        ap.print_help()
