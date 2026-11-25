# Bounty Hunter: Black Dawn: PC port

An unofficial, native Windows port of *Bounty Hunter: Black Dawn*, the mobile shooter by Freyr Games for Android and iOS. It is rebuilt as a Unity 6 project, with keyboard and mouse controls and an optional remaster.

**This repository contains no game files.** You need your own copy of the Android version 1.25.01. `tools/setup.py` turns it into a Windows game on your own PC.

## What you get

- A native Windows build (`BountyHunter.exe`), full screen at your monitor's resolution.
- Keyboard and mouse: WASD, mouse look, run, jump, E to interact, a PC-style HUD and a rotating minimap.
- The offline campaign. The online servers are long gone, so multiplayer is not supported.
- A free in-game store: mithril packs cost nothing, and you can trade mithril for gold.
- A remaster:
  - the original baked lighting is restored;
  - 8x MSAA;
  - light post-processing (toggle it with F9);
  - AI-upscaled (4x) 3D textures.

## What you need

- Windows 10 or 11, 64-bit.
- The game data of the Android version **1.25.01**: either an `.xapk` of that version or the file `main.9.com.ifreyrgames.blackdawn.obb`. The OBB is in `Android/obb/com.ifreyrgames.blackdawn/` on a phone that has the game. Setup checks its SHA-256: `9553549644578258388e3fd0a73b2855411a8f4bfa5aa8e045e15a378dc0aaac`.
- [Unity Hub](https://unity.com/download) with Unity **6000.0.85f2**, signed in with a free Personal license.
- [Python 3.10+](https://www.python.org/downloads/) and [Git for Windows](https://git-scm.com/download/win).
- About 20 GB of free disk space.
- A GPU with Vulkan for the texture upscale. If you don't have one, skip that step with `--no-upscale`.

Setup downloads [AssetRipper](https://github.com/AssetRipper/AssetRipper) 2.0.0 and [Real-ESRGAN](https://github.com/xinntao/Real-ESRGAN) (ncnn-vulkan) from their official GitHub releases and checks their hashes.

## Build it

```
git clone https://github.com/vecnam/bounty-hunter-black-dawn-pc
cd bounty-hunter-black-dawn-pc
python tools/setup.py C:\path\to\your-game-file.xapk
```

The first build takes a few hours: the export, the Unity import, the NavMesh rebuild and the texture upscale. If anything stops it, run `python tools/setup.py` again and it resumes where it stopped. When it finishes, play `game\BountyHunter.exe`; the keys are also in `game\README.txt`.

Options:
- `--no-upscale` skips the AI texture upscale. To add it later, run `--from upscale`.
- `--from STEP` redoes everything from that step on. The steps are `extract`, `export`, `populate`, `fixes`, `particles`, `static-batch`, `navmesh`, `lightmaps`, `upscale` and `release`.
- If Unity is not in `C:\Program Files\Unity\Hub\Editor\6000.0.85f2`, set the `UNITY_EXE` environment variable to its `Unity.exe`.

## Keys

| Action | Key |
|---|---|
| Move | W A S D (or arrow keys) |
| Run | hold left Shift |
| Look / aim | mouse |
| Shoot | left mouse button (or left Ctrl) |
| Aim / zoom | right mouse button |
| Reload | R |
| Switch weapon | 1, 2, 3, 4 |
| Jump | Space |
| Grenade | G |
| Melee | V |
| Power 1 / 2 | F / C |
| Interact (talk, open chests, pick up items, advance dialog) | E |
| Backpack / map / quests | I / M / Q (or U) |
| Minimap on/off | Tab |
| Achievements / skill tree | Y / K |
| Options, pause | O, or Esc in combat |
| Free the cursor to click the HUD | hold left Alt |
| Post-processing on/off | F9 |
| Full screen / window | Alt+Enter |

Saves are in `%USERPROFILE%\AppData\LocalLow\Freyr\Bounty Hunter Black Dawn`.

## Troubleshooting

- **"not the expected game data"**: the file is not version 1.25.01, and the patches only fit that version.
- **Unity stops with a license message**: open Unity Hub, sign in and activate a Personal license, then run setup again. With the Microsoft Store version of Unity Hub, `tools/unity.py` copies the license to where batch mode looks for it.
- **Logs**: the setup and Unity logs are in `build\*.log`, and the game's own log is `Player.log` in the save folder.

## Contributing

Read [AGENTS.md](AGENTS.md). It is the guide for people and AI coding agents alike.

## Legal

This is an unofficial fan project, not affiliated with or endorsed by Freyr Games. *Bounty Hunter: Black Dawn* and all of its content belong to their owners.

This repository has no game assets and no game code: the port is built on your computer from your own copy. The files in `patches/` show a few lines of the decompiled game code as diff context. Everything else is under the [MIT license](LICENSE).

Please don't upload game files or builds to this repository, its issues or its pull requests.
