"""Fixes duplicated local ids in the prefab YAML exported by AssetRipper.

Unity 4 used fileID = class*100000 + 5 digits; in large prefabs (NGUI UI) two objects land on the same id and
Unity 6 rejects the file ("multiple objects with same identifiers").

Usage:
  python -I tools/fix_dup_ids.py <folders or files...>   fix in place, report per file
  python -I tools/fix_dup_ids.py --selftest

From the 2nd copy on, each id gets a new id. Each {fileID: id} reference in the same file is resolved by structure
(m_Component of the owner GameObject, m_Children/m_Father of the hierarchy, m_GameObject of the component); what is
left (script fields) goes to the nearest copy in the hierarchy, and counts as a heuristic in the report.
"""
import collections
import pathlib
import re
import sys

HDR = re.compile(r"^--- !u!(-?\d+) &(-?\d+)")
REF = re.compile(r"\{fileID: (-?\d+)\}")  # local references only (external ones have ", guid:")
KEY = re.compile(r"^  (\w+):")


class Doc:
    def __init__(self, index, cls, fid, lines):
        self.index, self.cls, self.fid, self.lines = index, cls, fid, lines
        self.owner = self.father = None
        self.children, self.components = [], []
        section = None
        for line in lines[1:]:
            m = KEY.match(line)
            if m:
                section = m.group(1) if m.group(1) in ("m_Component", "m_Children") else None
                ref = REF.search(line)
                if m.group(1) == "m_GameObject" and ref:
                    self.owner = int(ref.group(1))
                elif m.group(1) == "m_Father" and ref:
                    self.father = int(ref.group(1))
            elif section and line.startswith("  - "):
                ref = REF.search(line)
                if ref:
                    (self.components if section == "m_Component" else self.children).append(int(ref.group(1)))


def split_docs(text):
    lines = text.splitlines(keepends=True)
    head, docs, cur = [], [], None
    for line in lines:
        m = HDR.match(line)
        if m:
            cur = [line]
            docs.append((int(m.group(1)), int(m.group(2)), cur))
        elif cur is None:
            head.append(line)
        else:
            cur.append(line)
    return head, [Doc(i, c, f, l) for i, (c, f, l) in enumerate(docs)]


def fix_text(text):
    """Returns (new text, stats). With no duplicates, returns the text unchanged."""
    head, docs = split_docs(text)
    by_id = collections.defaultdict(list)
    for d in docs:
        by_id[d.fid].append(d)
    dup = {fid: ds for fid, ds in by_id.items() if len(ds) > 1}
    stats = collections.Counter()
    if not dup:
        return text, stats

    def single(fid):
        ds = by_id.get(fid)
        return ds[0] if ds and len(ds) == 1 else None

    # GameObject of each doc (for the proximity heuristic); takes the first candidate if the owner is ambiguous
    def go_of(d):
        if d.cls == 1:
            return d
        ds = by_id.get(d.owner) or []
        return ds[0] if ds else None

    def transform_of(go):
        for fid in go.components:
            for t in by_id.get(fid, []):
                if t.cls in (4, 224) and t.owner == go.fid:
                    return t
        return None

    def parent_go(go):
        t = transform_of(go)
        if t is None or not t.father:
            return None
        cands = [p for p in by_id.get(t.father, []) if t.fid in p.children] or by_id.get(t.father, [])
        return go_of(cands[0]) if cands else None

    def ancestors(go):
        chain, seen = [], set()
        while go is not None and go.index not in seen:
            seen.add(go.index)
            chain.append(go.index)
            go = parent_go(go)
        return chain

    def distance(a, b):
        ca, cb = ancestors(a), ancestors(b)
        pos = {g: i for i, g in enumerate(cb)}
        for i, g in enumerate(ca):
            if g in pos:
                return i + pos[g]
        return 10**6

    used = collections.defaultdict(set)  # (doc, context, id) -> candidates already used in this list

    def resolve(fid, doc, ctx):
        ds = dup[fid]
        if ctx == "m_Component":
            cands = [c for c in ds if c.owner == doc.fid]
        elif ctx == "m_Children":
            cands = [c for c in ds if c.father == doc.fid]
        elif ctx == "m_Father":
            cands = [c for c in ds if doc.fid in c.children]
        elif ctx == "m_GameObject":
            cands = [c for c in ds if doc.fid in c.components]
        else:
            cands = []
        key = (doc.index, ctx, fid)
        free = [c for c in cands if c.index not in used[key]]
        if free:
            used[key].add(free[0].index)
            stats["structure"] += 1
            return free[0]
        me = go_of(doc)
        scored = sorted(ds, key=lambda c: (distance(me, go_of(c)) if me and go_of(c) else 10**6, c.index))
        best = scored[0]
        tie = len(scored) > 1 and me and go_of(scored[1]) and distance(me, go_of(scored[1])) == distance(me, go_of(best))
        stats["heuristic tie" if tie else "heuristic"] += 1
        return best

    # new ids from the 2nd copy on, Unity 4 style (class*100000 + n) when there is room
    taken = set(by_id)
    new_id = {}
    for fid, ds in dup.items():
        for d in ds[1:]:
            base = d.cls * 100000
            nid = next((base + n for n in range(100000) if base + n not in taken), max(taken) + 1)
            taken.add(nid)
            new_id[d.index] = nid
            stats["ids renumbered"] += 1

    def final_id(d):
        return new_id.get(d.index, d.fid)

    out = list(head)
    for d in docs:
        section = None
        for n, line in enumerate(d.lines):
            if n == 0:
                if d.index in new_id:
                    line = line.replace(f"&{d.fid}", f"&{new_id[d.index]}", 1)
                out.append(line)
                continue
            m = KEY.match(line)
            if m:
                section = m.group(1) if m.group(1) in ("m_Component", "m_Children") else None
                ctx = m.group(1) if m.group(1) in ("m_Father", "m_GameObject") else "field"
            else:
                ctx = section if (section and line.startswith("  - ")) else "field"

            def sub(r):
                fid = int(r.group(1))
                return "{fileID: %d}" % final_id(resolve(fid, d, ctx)) if fid in dup else r.group(0)

            out.append(REF.sub(sub, line))
    return "".join(out), stats


def duplicated_ids(text):
    c = collections.Counter(int(m.group(2)) for m in map(HDR.match, text.splitlines()) if m)
    return [fid for fid, n in c.items() if n > 1]


SAMPLE = """%YAML 1.1
--- !u!1 &100
GameObject:
  m_Component:
  - 4: {fileID: 400}
  m_Name: A
--- !u!1 &101
GameObject:
  m_Component:
  - 4: {fileID: 401}
  - 114: {fileID: 11400}
  m_Name: B
--- !u!1 &102
GameObject:
  m_Component:
  - 4: {fileID: 401}
  m_Name: C
--- !u!4 &400
Transform:
  m_GameObject: {fileID: 100}
  m_Children:
  - {fileID: 401}
  - {fileID: 401}
  m_Father: {fileID: 0}
--- !u!4 &401
Transform:
  m_GameObject: {fileID: 101}
  m_Children: []
  m_Father: {fileID: 400}
--- !u!4 &401
Transform:
  m_GameObject: {fileID: 102}
  m_Children: []
  m_Father: {fileID: 400}
--- !u!114 &11400
MonoBehaviour:
  m_GameObject: {fileID: 101}
  target: {fileID: 401}
  m_Script: {fileID: 11500000, guid: abc, type: 3}
"""


def selftest():
    out, stats = fix_text(SAMPLE)
    assert duplicated_ids(out) == [], duplicated_ids(out)
    _, docs = split_docs(out)
    by_name = {}
    for d in docs:
        if d.cls == 1:
            by_name[[l for l in d.lines if "m_Name" in l][0].split(":")[1].strip()] = d
    t_b = [d for d in docs if d.cls == 4 and d.owner == 101][0]
    t_c = [d for d in docs if d.cls == 4 and d.owner == 102][0]
    assert t_b.fid != t_c.fid
    assert by_name["B"].components[0] == t_b.fid and by_name["C"].components[0] == t_c.fid  # right owner
    t_a = [d for d in docs if d.cls == 4 and d.owner == 100][0]
    assert sorted(t_a.children) == sorted([t_b.fid, t_c.fid])  # both children, one of each
    mb = [d for d in docs if d.cls == 114][0]
    assert f"target: {{fileID: {t_b.fid}}}" in "".join(mb.lines)  # the field goes to the object's own transform
    assert "guid: abc" in "".join(mb.lines)
    assert fix_text(out)[0] == out  # idempotent
    print("selftest ok", dict(stats))
    return 0


def main(args):
    if args == ["--selftest"]:
        return selftest()
    paths = []
    for a in args:
        p = pathlib.Path(a)
        paths += sorted(p.rglob("*.prefab")) if p.is_dir() else [p]
    total = collections.Counter()
    fixed = 0
    for p in paths:
        text = p.read_text(encoding="utf-8", newline="")
        if not duplicated_ids(text):
            continue
        out, stats = fix_text(text)
        assert not duplicated_ids(out), p
        p.write_text(out, encoding="utf-8", newline="")
        fixed += 1
        total += stats
        print(f"{p}: {dict(stats)}")
    print(f"{fixed} files fixed; total: {dict(total)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
