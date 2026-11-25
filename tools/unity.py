"""Runs Unity 6 in batch mode, plus the port's screenshot harness.

  python -I tools/unity.py compile             import/compile BountyHunterPC and summarize the CSxxxx errors
  python -I tools/unity.py build               development build in build/BountyHunter.exe
  python -I tools/unity.py exec <Method>       run -executeMethod <Method> in batch mode (log: build/exec.log)
  python -I tools/unity.py shot <scene> <sec> [WxH]  run the dev build (WxH window, default 1280x720); saves
                                               build/shots/<name>.png and lists the log's exceptions
                                               (<scene> "-" = no scene switch, the game starts normally)
  python -I tools/unity.py release             final build (no Development) in game/, the one you play
  python -I tools/unity.py selftest            tests the error parser

Set UNITY_EXE if Unity 6000.0.85f2 is not installed in the default Unity Hub folder.
"""
import collections
import os
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
PROJECT = ROOT / "BountyHunterPC"
BUILD = ROOT / "build"
GAME = ROOT / "game"
UNITY = os.environ.get("UNITY_EXE", r"C:\Program Files\Unity\Hub\Editor\6000.0.85f2\Editor\Unity.exe")
ERROR_RE = re.compile(r"^(?P<file>[^\n(]+)\((?P<line>\d+),\d+\): error (?P<code>CS\d{4}): (?P<msg>.+)$", re.M)

SAMPLE = """Assets/Scripts/Assembly-CSharp/Foo.cs(10,5): error CS0103: The name `bar' does not exist in the current context
Assets/Scripts/Assembly-CSharp/Foo.cs(10,5): error CS0103: The name `bar' does not exist in the current context
Assets/Scripts/Assembly-CSharp/Baz.cs(3,1): error CS1061: Type `X' does not contain a definition for `y'
Assets/Scripts/Assembly-CSharp/Baz.cs(4,1): warning CS0618: `Old' is obsolete
"""


def parse_errors(log):
    """Unique CSxxxx errors as (file, line, code, message); Unity repeats each error in the log."""
    return sorted({(m["file"].strip(), int(m["line"]), m["code"], m["msg"].strip()) for m in ERROR_RE.finditer(log)})


SAVE_DIR = pathlib.Path(os.environ.get("USERPROFILE", "")) / "AppData/LocalLow/Freyr/Bounty Hunter Black Dawn"
LICENSES = pathlib.Path(os.environ.get("LOCALAPPDATA", "")) / "Unity/licenses"
HUB_LICENSES = pathlib.Path(os.environ.get("LOCALAPPDATA", "")) / "Packages/UnityTechnologies.UnityHub_2vrhnee42bhxm/LocalCache/Local/Unity/licenses"


def sync_license():
    """Unity Hub 4 installed as MSIX renews the license inside its package container; the editor only sees the copy
    outside it."""
    src, dst = HUB_LICENSES / "UnityEntitlementLicense.xml", LICENSES / "UnityEntitlementLicense.xml"
    if src.exists() and (not dst.exists() or src.stat().st_mtime > dst.stat().st_mtime):
        LICENSES.mkdir(parents=True, exist_ok=True)
        dst.write_bytes(src.read_bytes())


def run_unity(log_name, *args):
    if not pathlib.Path(UNITY).exists():
        sys.exit(f"Unity not found at {UNITY}. Install Unity 6000.0.85f2 with Unity Hub, or set UNITY_EXE to its "
                 "Unity.exe.")
    sync_license()
    BUILD.mkdir(exist_ok=True)
    log = BUILD / log_name
    code = subprocess.call([UNITY, "-batchmode", "-quit", "-accept-apiupdate", "-projectPath", str(PROJECT),
                            "-logFile", str(log), *args])
    text = log.read_text(encoding="utf-8", errors="replace") if log.exists() else ""
    if code and "licens" in text.lower():
        print("Unity stopped with a license message: open Unity Hub, sign in and activate a (free) Personal license, "
              f"then run again. Details in {log}.")
    return code, text


def running_games():
    """Folders of the running BountyHunter.exe processes. A build over a running game replaces its files, and the
    harness shares preferences (PlayerPrefs) and window focus with it."""
    out = subprocess.run(["powershell", "-NoProfile", "-Command", "(Get-Process BountyHunter -ErrorAction SilentlyContinue).Path"],
                         capture_output=True, text=True, errors="replace").stdout
    return [pathlib.Path(line.strip()).parent.resolve() for line in out.splitlines() if line.strip()]


def refuse_if_running(folder=None):
    """True (with a warning) if the game is running from folder, or from anywhere when folder is None."""
    hit = [p for p in running_games() if folder is None or p == folder.resolve()]
    if hit:
        print(f"BountyHunter.exe is running from {hit[0]}: close the game and run again")
    return bool(hit)


def cmd_compile():
    code, log = run_unity("compile.log")
    errors = parse_errors(log)
    print(f"{len(errors)} unique errors (Unity exit code: {code})")
    for cs, n in collections.Counter(e[2] for e in errors).most_common():
        print(f"  {cs}: {n}")
    for path, n in collections.Counter(e[0] for e in errors).most_common(15):
        print(f"  {n:5}  {path}")
    return 1 if errors or code else 0


def cmd_build():
    if refuse_if_running(BUILD):
        return 1
    code, log = run_unity("build.log", "-executeMethod", "PcPortBuild.BuildWindowsDev")
    exe = BUILD / "BountyHunter.exe"
    print(f"Unity exit code: {code}; exe: {'ok' if exe.exists() else 'NOT BUILT'} (log: build/build.log)")
    return 0 if code == 0 and exe.exists() else 1


def cmd_exec(method):
    code, log = run_unity("exec.log", "-executeMethod", method)
    print(f"Unity exit code ({method}): {code} (log: build/exec.log)")
    return 0 if code == 0 else 1


def cmd_shot(scene, secs, res="1280x720"):
    name = "start" if scene == "-" else scene.replace("/", "_")
    shots = BUILD / "shots"
    shots.mkdir(parents=True, exist_ok=True)
    png, log = shots / f"{name}.png", shots / f"{name}.log"
    png.unlink(missing_ok=True)
    if refuse_if_running():
        return 1
    # with -pcport-shot the game saves to <save>/pcport-test (GameApp.GetRecPath): start from a fresh copy of the
    # player's save, never the save itself
    test_save = SAVE_DIR / "pcport-test"
    test_save.mkdir(parents=True, exist_ok=True)
    for f in SAVE_DIR.glob("*_0[12]"):
        (test_save / f.name).write_bytes(f.read_bytes())
    try:
        width, height = res.split("x")
        subprocess.run([str(BUILD / "BountyHunter.exe"), "-screen-fullscreen", "0", "-screen-width", width,
                        "-screen-height", height, "-logFile", str(log), "-pcport-shot", scene, secs, str(png)],
                       timeout=float(secs) + 150)  # the harness waits up to 90 s for the HUD (PcPortShot.WaitForHud)
    except subprocess.TimeoutExpired:
        print("timeout: the game did not close by itself (process killed)")
    text = log.read_text(encoding="utf-8", errors="replace") if log.exists() else ""
    exceptions = sorted({line for line in text.splitlines() if "Exception" in line})
    for line in exceptions[:30]:
        print("  ", line)
    print(f"{len(exceptions)} distinct exceptions; screenshot: {png if png.exists() else 'NOT SAVED'}")
    return 0 if png.exists() else 1


def cmd_release():
    """Final build (no Development) in game/. A build over a running game replaces its files, so this refuses while
    the game runs from game/."""
    if refuse_if_running(GAME):
        return 1
    code, log = run_unity("release.log", "-executeMethod", "PcPortBuild.BuildWindowsRelease")
    ok = code == 0 and (GAME / "BountyHunter.exe").exists() and (GAME / "README.txt").exists()
    print(f"Unity exit code: {code}; game/: {'ok' if ok else 'FAILED'} (log: build/release.log)")
    return 0 if ok else 1


def cmd_selftest():
    errs = parse_errors(SAMPLE)
    assert len(errs) == 2, errs
    assert errs[0] == ("Assets/Scripts/Assembly-CSharp/Baz.cs", 3, "CS1061",
                       "Type `X' does not contain a definition for `y'"), errs[0]
    print("selftest ok")
    return 0


if __name__ == "__main__":
    cmd, *rest = sys.argv[1:] or ["help"]
    fn = {"compile": cmd_compile, "build": cmd_build, "exec": cmd_exec, "shot": cmd_shot, "release": cmd_release, "selftest": cmd_selftest}.get(cmd)
    if fn is None:
        sys.exit(__doc__)
    sys.exit(fn(*rest))
