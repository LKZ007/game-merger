# Game Merger

A Windows game launcher that "merges" games the realistic way. You pick a base game you
actually play, pick the style of another game to bring in, and Game Merger makes a safe
copy, installs the mod frameworks, and installs the mods you choose. **Franklin**, the
built-in assistant, explains the merge, checks your mods for clashes and reads crash logs.

## What it can merge right now

| Base game | Merge with |
|---|---|
| Cyberpunk 2077 | Forza style (cars, handling, racing), GTA style (police, traffic, radio) |
| Schedule I | Hood Warfare style (guns, gang fights), GTA style |
| GTA V (Story Mode only) | Minecraft style (skins, mobs, building) |

No files are ever copied out of the "merged with" game. Different engines can't share files,
and copying them would break those games' terms.

## Getting started

1. Download **GameMergerSetup.exe** from the latest release:
   https://github.com/LKZ007/game-merger/releases/latest
2. Double-click it and press **Install**. No admin prompt, no Python. You get a desktop
   shortcut and a Start menu entry; right-click either and choose **Pin to taskbar**.
3. The first time it opens, Franklin asks once for Windows permission to change your game
   folders. Press **Sort it**, then **Yes**. You won't be asked again.
4. Updates install themselves: when a new version is out, Franklin offers it and one click
   updates the app. Your merges, saves and settings are never touched.

Windows may show "Windows protected your PC" because the installer isn't signed. Click
**More info**, then **Run anyway**. For `.7z` and `.rar` mods, install 7-Zip from https://www.7-zip.org.

Uninstall from **Settings > Apps**. Your merged copies, their saves and your settings
(in `%LOCALAPPDATA%\GameMerger`) are kept.

## Setting up Franklin

Franklin runs on Claude with your own Anthropic API key:

1. Create a key at https://console.anthropic.com (add a little credit; most questions cost a few cents).
2. In Game Merger, open **Settings** and paste the key.

Franklin sees only game names, mod file names, the paths inside your mods, and log files.
It never uploads your game files. The key is stored on your PC only, in
`%LOCALAPPDATA%\GameMerger\settings.json`.

## Franklin's voice

Franklin talks with a free, natural-sounding British voice (Ryan or Thomas, pick in Settings),
with a mute button at the top of his panel. Offline, he falls back to the voice built into
Windows. For a proper Manchester accent, paste an ElevenLabs key in Settings.

## How to use it

1. **Library**: your games from Steam, Epic, GOG and the Xbox app are shown as 3D boxes.
   Click a game with a glowing edge to make it the base game, choose **Merge with**, then **Merge**.
2. **Mod frameworks** download automatically.
3. **Mods**: click **Find mods** (opens Nexus Mods), download what you like, then **Add file**.
   Game Merger shows where each mod goes and warns you when two mods clash.
4. **Where to put it**: Fast copy (default, almost no extra space), Full copy, or Mod the original (with backups).
5. **Own saves**: every merge gets its own save games. When you press Play they're swapped in,
   and your normal saves come back when the game closes.
6. **My merges**: Play, Add mods, Why did it crash? (Franklin reads the logs), Undo all mods, Delete.
7. **Backups**: restore any original install you modded directly.

## Coming next

**Questlines**: Franklin writes new missions in the style of the merged game, and a small
quest-loader mod per base game plays them (GTA V first, then Schedule I, then Cyberpunk).

## Sharing a merge

In **My merges**, press **Share**. Game Merger saves a `.gmerge` file in your Downloads folder
with the mods in it (never the game itself). Your friend double-clicks it and gets the same
merge built from their own copy of the game, with its own saves.

## For developers

- Release build (Windows): `pip install pyinstaller -r requirements.txt`, then
  `pyinstaller --onedir --windowed --name GameMerger --icon installer\GameMerger.ico --collect-data edge_tts --add-data "gamemerger\recipes;gamemerger\recipes" --add-data "gamemerger\web;gamemerger\web" GameMerger.pyw`,
  then `makensis -DVER=x.y.z installer\GameMerger.nsi` makes `dist\GameMergerSetup.exe`.
- Bump `gamemerger/version.py` and publish the setup file as a GitHub release asset named
  `GameMergerSetup.exe` with tag `vX.Y.Z`; installed apps pick it up on their next start.

- `python -m unittest discover -s tests` runs the tests.
- Add a base game by dropping a recipe `.json` (same format as `gamemerger\recipes`) into
  `%LOCALAPPDATA%\GameMerger\recipes`.
