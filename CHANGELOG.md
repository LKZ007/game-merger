# Changelog

Every release, newest first. Downloads are on the [releases page](https://github.com/LKZ007/game-merger/releases).
The app was called Game Merger before 3.8.0.

## v3.13.1 (2026-10-22)
- **A colour on every game, every time**: every tile in the Library now shows a colour, whether it's installed, only owned, added by hand, from Xbox or Minecraft. Before you pick a main game, each tile shows how moddable it is on its own; once you pick one, each tile shows how well it pairs. A tile that's still being rated shows a pulsing grey dot ("Rating…") instead of nothing.
- Stardew Valley, Terraria and other XNA / MonoGame games, and ELDEN RING and other FromSoftware games, are now recognised (limited mod support) instead of showing as unknown.

## v3.13.0 (2026-10-22)
- **A rundown while your merge loads**: press Play and, while the game starts, Frankenstein shows what's new in this merge, things to try first, the controls (keyboard and controller), your current quest and a few tips. It's written by the AI in the background when you have a key, and built from the mod list when you don't. Skip it with a click or Esc, turn it off per merge, or bring it back from My merges (Rundown). It can open on your second monitor, or as a small window that stays on top.
- **Creative mode on every game**: Play now asks Normal or Creative. Creative is a sandbox with its own save slot (your normal save is copied in once and never touched), sandbox tools added for that game (free camera, spawners, cheat menus, the game's own console where it has one), and online play blocked. Each game says honestly what its creative mode can do, and a Creative tag shows on merges that have it.
- Works for merges you already have, too.

## v3.12.0 (2026-10-22)
- **Mods from CurseForge, Modrinth and any link**: Find mods now searches every site that has your game at once (Nexus Mods, CurseForge, Modrinth), and each result shows where it's from. You can also paste a download link or pick a file. Mods that need other mods bring them along automatically. CurseForge needs a free API key (Settings > CurseForge); Modrinth needs nothing.
- **Minecraft: Java Edition as a main game**: each merge becomes its own profile in the Minecraft Launcher with Fabric, Quilt, Forge or NeoForge, and its own mods and worlds. Your normal Minecraft stays as it is. Play opens the launcher with the merge picked.
- **A controller on every game**: every tile shows a 🎮 badge. Games with full controller support use it as normal; for the rest, Frankenstein's key mapper runs while you play (left stick walks, right stick looks, triggers click, buttons press keys) and only while the game is in front. Change the buttons per merge from the 🎮 tag in My merges. Merge setup also suggests controller-support mods for games that need them.
- Works for merges you already have, too.

## v3.11.0 (2026-10-22)
- **Any game can be the main game.** Frankenstein reads the game's folder, works out its engine and the mod loader it needs (Unity: BepInEx or MelonLoader; Unreal: .pak mods in ~mods; Bethesda: the Data folder; Source and more), and builds a recipe on the spot. Every other game you have, installed or owned, can be merged in as a style. Games with little or no mod support can still be picked; they say so plainly and their pairings are rated lower.
- **Franklin is gone; the AI stays in the background.** No chat panel, voice or push-to-talk. With your Claude API key (Settings > AI features), the AI quietly rates pairings, works out mod searches for any pairing, names merges, fixes crashes and writes questlines.
- **A colour on every game**: green, yellow or red on every tile, including games you own but haven't installed and the main game itself (how moddable it is). Every pairing gets rated in the background and is remembered. Without an AI key you get a rough guess from the games' Steam genres.
- **Your existing merges are upgraded** the first time this version opens (their records are backed up first; mods and saves aren't touched). If one is missed, it shows an **Upgrade** button in My merges.
- New look for the logo: the head with the brain of dots, next to the name.

## v3.10.0 (2026-10-22)
- **Accounts**: everyone on the PC gets their own merges, saves, Franklin memory, achievements and voice settings, with a "Who's playing?" screen and an optional PIN. The first account (yours) is the admin: add or remove accounts, and choose what each one can do (Franklin's 18+ mode, online parties and sharing, installing mods). Shared keys are admin-only. Settings > Accounts.
- **Questlines**: Franklin writes a questline for any merge (tell him the vibe if you want), using what's really in your game. Tick off objectives as you go; press Brief me and he reads you the next one. In-game quest markers come next, starting with Schedule I.
- **Push-to-talk**: hold F8 (even mid-game) or the mic button, ask Franklin anything, let go. Uses Windows' own speech recognition, offline.
- **Controller support**: D-pad or stick to move, A to pick, B to go back, LB/RB to switch pages, Start for Franklin, hold Y to talk.
- **Achievements**: 16 to unlock, from "It's alive" to "Couch mode". Franklin calls them out.
- **Discord status**: shows the merge you're playing on your Discord profile (one-time setup in Settings > Discord status).

## v3.9.1 (2026-10-21)
- **It's alive!** When a merge finishes, the two games' boxes spin in, fuse in a violet flash and come out as one new box. Skip it with a click or Esc.
- **Every merge is a new game**: Franklin names it and gives it a tagline (press New name for another, or type your own). The name and tagline go on its new cover art and show in My merges.

## v3.9.0 (2026-10-21)
- **Every game you own, not just what's installed**: connect Steam, Epic and GOG in Settings > Your game accounts. Games that aren't on this PC show greyed out under "Not on this PC", with an Install button that opens the right launcher. With cloud backup on, it even says which games are on your other PC.
  - Steam: found from the Steam app; your game list needs to be public, or add a free Steam Web API key.
  - Epic: one sign-in in a small Epic window (uses the same unofficial launcher service as Legendary and Heroic).
  - GOG: read from GOG Galaxy, no sign-in.
  - Xbox / Microsoft Store: Microsoft doesn't allow it, so only installed games show.
  - Sign-ins are kept in Windows Credential Manager.
- **Fun rating**: pick a base game and Franklin colour-codes your other games by how fun they'd be to merge in. Green is a great pairing, yellow is OK, red is doable but not as great. Hover for his reason. Dot or glow, your choice in Settings.
- **Preview before you merge**: trailers of both games and pictures of popular mods for each part of the merge, streamed at low quality so nothing is downloaded, plus Franklin's quick pitch. It's a preview of the parts, not a render of your merged game.

## v3.8.2 (2026-10-21)
- **Only one Frankenstein at a time**: opening it again (or a double launch) brings the open window to the front instead of starting another copy.
- **Franklin's cloned voice stays off unless you pick it**: a voice server left running by an older version is closed on start, and closing the app now fully stops it.

## v3.8.1 (2026-10-21)
- **Borderless full screen**: Frankenstein now fills the whole screen like a game launcher, with its own minimise, full screen and close buttons at the top right. Press F11 (or Esc) to switch to a window and back; it remembers your choice.
- **Franklin's voice is Andrew by default** (deep, laid-back). Your own cloned voice is still in Settings > Voice, but it only loads when you pick it, so the app opens fast and doesn't max out your CPU.

## v3.8.0 (2026-10-21)

**Game Merger is now Frankenstein.** Same app, new name. Your merges, saves, settings and Franklin's memory all carry over. Updating swaps the desktop and Start menu shortcuts for new Frankenstein ones; if you pinned the old one to your taskbar it keeps working.

- **Get mods from Nexus Mods inside the app**: add your Nexus API key in Settings, then use Find mods on any merge. Premium members get one-click downloads; free members click "Mod Manager Download" on Nexus and the file lands straight in the merge.
- **One window only**: opening a shared .gmerge or a Nexus link while Frankenstein is open hands it to the open window.
- Fresh installer art and a couple of new Franklin lines.

## v3.7.0 (2026-10-21)
- **Gallery**: press Ctrl+Alt+S while playing to grab a screenshot. Game Bar clips (Win+Alt+R) show up too.
- **Cloud backup of saves**: pick OneDrive or Google Drive in Settings and your merge saves follow you between PC and laptop.
- **Franklin remembers you**: your favourite merges, mods and playstyle, between sessions (wipe it any time in Settings).
- **"Will my PC run it?"** Franklin checks your specs and picks lighter mods if needed.
- **Merge ideas**: Franklin suggests new combos from the games you own.
- **Fix crash**, **Defender mod scan**, **online lock for GTA V**, **mods & profiles** from 3.6.0.

## v3.4.2 (2026-10-21)
- **Fewer antivirus false alarms**: the app is now built with its own compiled launcher, carries proper version info, and no compression packer. If Defender still flags it, see the README for how to restore it and report the false positive.
- **Franklin's vibe**: cool and laid-back, mixes up his phrases so he doesn't repeat himself, cusses now and then in 18+ mode (switch it off in Settings for clean mode).
- **Voice**: Andrew, pitched deeper.
- Franklin's go-to lines: "yo, what's up", "aight, got 'em", "good choice", "let's mix some shit up", rotated so he doesn't repeat himself.
- Plus: Play together (your own private network), sharing and auto-update.

## v3.4.0 (2026-10-21)
- **Fewer antivirus false alarms**: the app is now built with its own compiled launcher, carries proper version info, and no compression packer. If Defender still flags it, see the README for how to restore it and report the false positive.
- **Franklin's vibe**: cool and laid-back, mixes up his phrases so he doesn't repeat himself, cusses now and then in 18+ mode (switch it off in Settings for clean mode).
- **Voice**: Andrew, deeper and a touch quicker.
- Plus: Play together (your own private network), sharing and auto-update.

## v3.2.0 (2026-10-21)
- **Play together**: your own private network, like Hamachi. Host a party and send the code; friends join and you let them in with one click. Shows what works with friends for each game.
- **Franklin's voice**: a warm, smooth radio-host style voice by default (Andrew, Brian, Christopher or Roger in Settings). Speaks a sentence at a time so he starts talking faster.
- **Your own cloned voice** (XTTS) shows up in Settings when it's set up on your PC.
- Share and auto-update from 3.1.0.

## v3.1.0 (2026-10-21)
First installable release of Game Merger.

- Installer with desktop and Start menu shortcuts (pin to taskbar from either), uninstaller in Settings > Apps
- Updates itself: Franklin offers new versions and installs them in one click
- Dark violet launcher with real 3D cover art, combined covers for merges
- Franklin, your Claude-powered merge assistant, with a natural British voice and a mute button
- Asks Windows for permission once, never again
- Every merge has its own save games
- Share a merge with friends as a .gmerge file (double-click to import)
- Script Hook V downloads automatically for GTA V

Download GameMergerSetup.exe below. If Windows says it protected your PC, click More info, then Run anyway.
