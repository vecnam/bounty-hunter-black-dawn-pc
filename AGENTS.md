# AGENTS.md

This is the guide for anyone changing this repository, whether a person or an AI coding agent. [README.md](README.md) says what the project is and how to build it; build it once before you change anything.

## Rules

1. **Never commit game content.** That means no assets, no decompiled code (`BountyHunterPC/Assets/Scripts` and `Assets/Plugins`), no shader dumps, no builds and no `.apk`/`.xapk`/`.obb`. `.gitignore` covers these folders; never force-add them.
2. **Changes to the decompiled game code live only in `patches/`.** Edit the code in place, then run `python -I -X utf8 tools/patches.py save` and commit `patches/`.
3. **New code goes in `BountyHunterPC/Assets/PCPort`**, in a `PcPort*` class. `patches.py save` refuses new files under `Scripts/` or `Plugins/`.
4. **Keep the pipeline reproducible from the game file.** A change made by hand in the Unity editor gets lost. Anything that edits assets must be a script (in `tools/`, or an editor method run with `tools/unity.py exec`) and a step in `tools/setup.py`.
5. **Don't change `productName` ("Bounty Hunter Black Dawn") or `companyName` ("Freyr")**, because they decide the save folder.
6. **Write everything in English**: code, comments, logs, docs and commit messages.

## Layout

```
BountyHunterPC/            Unity 6000.0.85f2 project
  Assets/PCPort/           the port's own code: compatibility layer, HUD, minimap, harness, editor tools
  Assets/Shader/           the game's custom shaders, rewritten in HLSL (GUIDs kept)
  Assets/*                 everything else comes from the export (git-ignored)
  Packages/, ProjectSettings/
patches/                   our changes to the decompiled code: one .patch per file, deleted.txt, renamed.txt
tools/                     the pipeline (Python 3, standard library only, plus bash for rip.sh)
docs/README.txt            the readme that ships next to BountyHunter.exe
```

These folders are generated and git-ignored:
- `extracted/`: the unzipped OBB;
- `ripped/`: the raw AssetRipper export. Never edit it; it is the base of the patches;
- `build/`: dev build, logs, screenshots and setup state;
- `game/`: the final build.

## Commands

Run Python as `python -I -X utf8 tools/<script>`. Unity steps take minutes, so run them in the background.

| What | Command |
|---|---|
| Build everything from the game file (resumable) | `tools/setup.py <file.xapk or .obb>` |
| Redo from a step | `tools/setup.py --from <step>` |
| Compile and summarize CS errors | `tools/unity.py compile` (log: `build/compile.log`) |
| Development build in `build/` | `tools/unity.py build` |
| Final build in `game/` | `tools/unity.py release` |
| Screenshot harness | `tools/unity.py shot <mode> <seconds> [WxH]` → `build/shots/<mode>.png` + `.log` |
| Save your changes to the game code | `tools/patches.py save` |
| Rebuild the game code from the export + patches | `tools/patches.py apply` (only when `Assets/Scripts` and `Assets/Plugins` don't exist) |
| Self-tests | `--selftest` on `setup.py`, `patches.py`, `fix_dup_ids.py`, `port_shaders.py`, `fix_static_batch.py`, `fix_lightmaps.py`, `upscale_textures.py`, and `unity.py selftest` |

Harness modes (`PcPortShot.cs`):
- `-`: normal start;
- `hud`, `hud-tab`: the HUD and minimap;
- `menu-N`: opens menu N;
- `reopen-N`: opens menu N, closes it and opens it again;
- `fruit-N`: the roulette;
- `portal-<Scene>`: enters a mission through the portal, wakes the enemies and logs render, NavMesh, chest and jump checks;
- `iap`: the store;
- `ui-msg`: character creation dialog.

## Testing a change

1. `tools/unity.py compile` must report 0 errors. The player build is stricter than the editor: mobile-only and editor-only APIs fail there.
2. `tools/unity.py build`, then the harness modes that touch your change, usually `portal-Instance0 12`, `hud 8` and `iap 6`.
3. In the `.log`, the only expected noise is:
   - network exceptions (the old servers point to `127.0.0.1` and are refused at once);
   - `Enemy already exist: E_0_N` in mission 3;
   - four "referenced script is missing" warnings (`MithrilRewards`).

   Agent counts (`agents=… onNavMesh=…`) vary between runs. The real NavMesh problems are `Failed to create agent` or `placed on a NavMesh`.
4. The harness opens a game window. Don't click it or type in it, and don't run it while the game is open, because they share PlayerPrefs. It plays on a copy of your save (`<save>\pcport-test`), never the save itself.
5. The dev build generates `Assets/PCPort/Scenes/ShaderGallery.unity`, a scene to look at every ported shader. It is git-ignored because it points to assets of your own export.

## How the pipeline works

`tools/setup.py` runs these steps in order:

1. **extract**: checks the OBB hash, unzips it and flattens the asset bundles (`a/b/c` becomes `a__b__c`).
2. **export**: AssetRipper decompiles the game into `ripped/`; the bundles are exported separately so they can be matched later.
3. **populate**: copies the export into the project (minus `Scripts`, `Plugins` and `Shader`), then `patches.py apply` writes the decompiled code with our patches.
4. **fixes**:
   - `fix_dup_ids.py`: the Unity 4 fileIDs collide in 88 UI prefabs;
   - `port_shaders.py`: NGUI shaders to HLSL, built-in shaders remapped;
   - `map_bundles.py`: bundle content goes to `Resources/bundles`, loaded by `PcPortBundle`.
5. **particles**: `PcPortParticleConverter` turns the legacy emitters into `ParticleSystem`s.
6. **static-batch**: `fix_static_batch.py` converts the Unity 4 static batching, which Unity 6 ignores.
7. **navmesh**: `PcPortNavMeshBaker` decodes the Unity 4 Detour tiles and rebakes one NavMesh per area (ground 3, jump links 2, flying 4).
8. **lightmaps**: `fix_lightmaps.py` injects `PcPortLightmaps`, which reapplies the Unity 4 lightmaps at runtime.
9. **upscale**: Real-ESRGAN x4plus on the 3D textures, outside the UI (`upscale_textures.py`).
10. **release**: `PcPortBuild.BuildWindowsRelease` writes `game/`.

## Unity 4 to Unity 6 gotchas

- **`Resources.UnloadAsset` on a shared asset**: in Unity 6 the asset does not come back when something else uses it, and the screens lost their atlases. `MemoryManager.FreeNGUI` now only destroys.
- **`LayerMask.NameToLayer("UI")`** returns Unity 6's built-in layer 5, which no game camera draws. Use `PcPortLayer.NameToLayer`.
- **Parsing numbers**: the game parses floats without a culture. `PcPortCulture` forces the invariant culture, and old saves' dates are still read.
- **File names**: a `MonoBehaviour` binds to prefabs by file name, and NTFS is case-insensitive. `IAPItem.cs` holds two classes; `IapItem.cs` is the component, so `patches/renamed.txt` renames it.
- **Static batching** must stay converted (`fix_static_batch.py`), or Instance0 renders black.
- **NavMesh bake**: Unity's bake loses the bottom of a very tall source, so the baker uses 20 m cells and short triangles. The rebuilt NavMesh is about 0.2 m smaller at the edges than the original.
- **Lightmaps**: without a `LightingDataAsset`, Unity strips `LIGHTMAP_ON`. `GraphicsSettings` uses custom lightmap stripping that keeps it.
- **Duplicated ids**: `fix_dup_ids.py` breaks 15 ties by taking the first copy. If a UI widget points to the wrong thing, suspect those.
- **PlayMaker**: the legacy networking actions were removed. Before deleting an action, `grep -rlE "Actions\.<Name>\b"` over the `.unity`/`.prefab`/`.asset` files must find nothing.
- **Bundle prefabs**: Unity names a prefab root after its file (`android_prefab_<name>`). `PcPortBundle` strips the prefix because the game finds objects by name (chests).
- **Insecure HTTP**: Unity 6 blocks `http://` by default; `insecureHttpOption: 2` allows it.
- **CS0436 on `SocialNetworkingManager`**: the class exists in two assemblies, as in the original game. It is harmless.
- **Unity license in batch mode**: Unity Hub from the Microsoft Store keeps the license inside its package; `tools/unity.py` copies it to `%LOCALAPPDATA%\Unity\licenses` before each run.

## Commits and pull requests

- Keep commits small and focused, with English messages.
- In the pull request, say what you tested: compile, and which harness modes.
- Never attach game files.
