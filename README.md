# Frankenstein

<p align="center">
  <a href="https://github.com/LKZ007/game-merger/releases/latest/download/FrankensteinSetup.exe"><img alt="Download Frankenstein for Windows" src="https://img.shields.io/badge/Download-Frankenstein%20for%20Windows-8b5cf6?style=for-the-badge&logo=windows&logoColor=white" height="56"></a>
</p>
<p align="center">Windows 10/11 · free · always the newest version · updates itself after that</p>

A Windows game launcher that "merges" games the realistic way. You pick any game you
actually play as the main game, pick the style of another game to bring in, and Frankenstein makes a safe
copy, works out the game's engine and mod loader, and installs the mods you choose. Optional AI
features (your own Claude API key) work quietly in the background: fun ratings, mod searches,
merge names and one-click crash repair.

![Frankenstein's library: pick a main game, pick a style to merge in, and see how fun each pairing would be](docs/screenshot.png)

## Features

- **One-click merges**: safe copy of the game (fast copy uses almost no extra space), mod frameworks downloaded for you, undo any time.
- **Any game can be the main game**: Unity (BepInEx / MelonLoader), Unreal (.pak mods), Bethesda (Data folder), Source and more are detected from the game's files; games without real mod support can still be picked and say so honestly.
- **AI in the background** (optional Claude API key): fun ratings, mod searches for any pairing, merge names and taglines, one-click crash repair, questlines.
- **Mods from Nexus Mods, CurseForge, Modrinth or any link** in one search, with required mods pulled in automatically. Minecraft: Java Edition merges get their own launcher profile with Fabric, Quilt, Forge or NeoForge.
- **A controller on every game**: native where the game supports it, Frankenstein's key mapper everywhere else (buttons editable per merge).
- **Own saves per merge**, with optional cloud backup to OneDrive or Google Drive.
- **Mod profiles** (chill / chaos), **Defender scan** of every mod, **online-safety lock** for GTA V.
- **Play together**: a private network for you and your friends, like Hamachi (built on ZeroTier).
- **Share a merge** as a `.gmerge` file your friend double-clicks.
- **Gallery**: Ctrl+Alt+S screenshots plus Game Bar clips.
- **Every game you own**: connect Steam, Epic and GOG to see games that aren't installed here, and which ones are on your other PC.
- **Fun rating**: green / yellow / red on every game, owned ones and the main game included, for how fun the merge would be. Rough genre-based guesses without an AI key.
- **Preview before merging**: streamed trailers and mod pictures with a short pitch, nothing downloaded.
- **Accounts** with a "Who's playing?" screen, PINs, and an admin who decides what each account can do.
- **Questlines** written for each merge, **controller support**, **achievements** and **Discord status**.
- **Borderless full screen** launcher (F11 or Esc for a window), dark violet look, auto-updates.

## Hand-made recipes

Every game can be the main game. These three have hand-tuned recipes:

| Main game | Merge with |
|---|---|
| Cyberpunk 2077 | Forza style (cars, handling, racing), GTA style (police, traffic, radio) |
| Schedule I | Hood Warfare style (guns, gang fights), GTA style |
| GTA V (Story Mode only) | Minecraft style (skins, mobs, building) |

No files are ever copied out of the "merged with" game. Different engines can't share files,
and copying them would break those games' terms.

## Getting started

1. Download **FrankensteinSetup.exe** from the latest release:
   https://github.com/LKZ007/game-merger/releases/latest
2. Double-click it and press **Install**. No admin prompt, no Python. You get a desktop
   shortcut and a Start menu entry; right-click either and choose **Pin to taskbar**.
3. The first time it opens, Frankenstein asks once for Windows permission to change your game
   folders. Press **Sort it**, then **Yes**. You won't be asked again.
4. Updates install themselves: when a new version is out, Frankenstein offers it and one click
   updates the app. Your merges, saves and settings are never touched.

Windows may show "Windows protected your PC" because the installer isn't signed. Click
**More info**, then **Run anyway**. For `.7z` and `.rar` mods, install 7-Zip from https://www.7-zip.org.

**If Windows Defender blocks or quarantines it:** that's a false positive. Small unsigned apps
built with Python and PyInstaller get flagged by Defender's machine-learning check
(names like `Trojan:Win32/Bearfoos.A!ml` or `Wacatac`). From 3.4.0 the app is built with its own
compiled launcher, which avoids most of these. If it still happens:
1. Open **Windows Security > Virus & threat protection > Protection history**, pick the item and
   choose **Restore**.
2. Report it to Microsoft so it's fixed for everyone: https://www.microsoft.com/en-us/wdsi/filesubmission
   (choose "Software developer" or "Home customer", "Incorrectly detected as malware").

Code signing would remove the warnings completely. Free signing for open-source projects is
available from SignPath Foundation (https://signpath.org), which needs an application.

Uninstall from **Settings > Apps**. Your merged copies, their saves and your settings
(in `%LOCALAPPDATA%\GameMerger`) are kept.

## AI features (optional)

Add your own Anthropic API key in **Settings > AI features** (get one at https://console.anthropic.com).
The AI only sees game names, mod file names, the paths inside your mods, and log files, never game
files. Without a key everything works, with simpler guesses.

## How to use it

1. **Library**: your games from Steam, Epic, GOG and the Xbox app are shown as 3D boxes.
   Click a game with a glowing edge to make it the base game, choose **Merge with**, then **Merge**.
2. **Mod frameworks** download automatically.
3. **Mods**: click **Find mods** (opens Nexus Mods), download what you like, then **Add file**.
   Frankenstein shows where each mod goes and warns you when two mods clash.
4. **Where to put it**: Fast copy (default, almost no extra space), Full copy, or Mod the original (with backups).
5. **Own saves**: every merge gets its own save games. When you press Play they're swapped in,
   and your normal saves come back when the game closes.
6. **My merges**: Play, Add mods, Fix crash (reads the logs and switches off the culprit), Undo all mods, Delete.
7. **Backups**: restore any original install you modded directly.

## Sharing a merge

In **My merges**, press **Share**. Frankenstein saves a `.gmerge` file in your Downloads folder
with the mods in it (never the game itself). Your friend double-clicks it and gets the same
merge built from their own copy of the game, with its own saves.

## Repository layout

| Folder | What's in it |
|---|---|
| `gamemerger/` | The app (Python). `api.py` is everything the UI can call; `ai.py`, `autorecipe.py`, `nexus.py`, `network.py`, `installer.py` (merges and undo), `saves.py`, `updater.py` |
| `gamemerger/web/` | The UI (HTML, CSS, JS) shown in the app window |
| `gamemerger/recipes/` | One JSON recipe per base game: frameworks, styles, where mods go |
| `installer/` | NSIS installer script, icon and art, release notes for the next release |
| `tests/` | Unit tests |
| `docs/` | Screenshots for this page |
| `.github/workflows/release.yml` | Builds the installer on Windows and publishes a release |

The `gamemerger` folder keeps its old name on purpose: the app was called Game Merger before 3.8.0,
and renaming the package would break installed copies and their data folder.

## For developers

- Run from source: `pip install -r requirements.txt`, then `Run Frankenstein.bat` (or `python Frankenstein.pyw`; add `--browser` to open it in a browser).
- Tests: `python -m unittest discover -s tests`.
- Local build: `Build exe.bat` makes `dist\Frankenstein\Frankenstein.exe`; then
  `makensis -DVER=x.y.z installer\Frankenstein.nsi` makes `dist\FrankensteinSetup.exe`.
- **Releasing**: bump `gamemerger/version.py`, write `installer/release-notes.md`, add the notes to
  `CHANGELOG.md`, and push to `main`. The workflow builds the installer and publishes release `vX.Y.Z`
  with two copies of it: `FrankensteinSetup.exe`, and `GameMergerSetup.exe` for installs from before
  the rename (their updater looks for that name). Installed apps offer the update on their next start.
- Add a base game by dropping a recipe `.json` (same format as `gamemerger\recipes`) into
  `%LOCALAPPDATA%\GameMerger\recipes`.

## Licences and credits

Frankenstein uses, downloads or works with these projects, each under its own licence:

- [pywebview](https://github.com/r0x0r/pywebview) (BSD-3-Clause) for the app window, on Microsoft Edge WebView2.
- [ZeroTier One](https://www.zerotier.com/) for Play together, downloaded from ZeroTier when you set it up (ZeroTier's own licence).
- [Anthropic Claude API](https://www.anthropic.com/) for the AI features, with your own API key; [Nexus Mods API](https://www.nexusmods.com/) with your own key.
- [Pillow](https://python-pillow.org/), [icoextract](https://github.com/jlu5/icoextract), [PyInstaller](https://pyinstaller.org/) (GPL with the bootloader exception) and [NSIS](https://nsis.sourceforge.io/) (zlib) to build it.

Mods, frameworks and games belong to their authors and publishers. Frankenstein never copies files
out of the game whose style you merge in.
