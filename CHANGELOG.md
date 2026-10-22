# Changelog

Every release, newest first. Downloads are on the [releases page](https://github.com/LKZ007/game-merger/releases).
The app was called Game Merger before 3.8.0.

## v3.26.0 (2026-10-22)
- **Carry over my save.** On any merge, open ⋯ More, then **Carry over my save**. Frankenstein reads the Extension game's newest save and makes a character card with name, money, level, stats and abilities. It only reads the save, never changes it. Edit anything, tick abilities, then press **Bring them over**. Your character arrives the next time you press Play. Money comes once, and the powers stay on.
  - **What can be read:** Schedule I saves are plain files, so cash, bank, rank and XP are read. For Cyberpunk, the save's summary gives level, street cred and attributes. Cyberware and money are packed inside the save, so tick those by hand. Other games (GTA V, Elden Ring and more) have locked saves, so you fill in the card by hand.
  - **Abilities become powers in the Host game,** each marked Built, Stand-in or Not possible yet.
    - GTA V gets the most: F6 slows time, Tough (double health and armour), Agile (super jump and fast sprint), Stealth (police ignore you on foot), Hacker (any car unlocks as you get in), Strength (explosive punches), Magic (fire punches and fire ammo) and your money as cash.
    - Cyberpunk: Slow time, Tough and Lose the police buttons (press ~, This merge), plus your money as eddies.
    - Schedule I: your money as cash and a bat for Strength. The rest shows on your character card (F8), marked as not possible there yet.
- F8 in Schedule I is one menu: Creative is at the top, and the Hood Warfare Block and your character are below it.

## v3.25.0 (2026-10-22)
- **Switch to Creative while you play, in every game.** Schedule I: press **F8** anywhere in town for Frankenstein's menu, then **Switch to Creative** (or **Back to my normal game**), then confirm. Cyberpunk: press **~**, then the This merge tab, then **Switch to Creative**, then confirm. GTA V: **Shift+F10**, then Shift+F10 again to confirm. Save first. The game closes and Frankenstein starts it again in Creative with its own saves, and the same way back, so normal and Creative saves never mix. Each game shows a hint for the first few seconds saying how.
- **Play together setup fixed.** Setup was meant to wait for ZeroTier to start, but in the hidden window the wait ended straight away, so it stopped with "Setup didn't finish". It now waits properly (up to 90 seconds), starts ZeroTier's service if it's stopped, and waits for its installer to finish. If it still fails, it says why in plain words (the Windows prompt was declined, another installer was running, the installer's error code, or the service didn't start) instead of showing a traceback. There's also a link to install ZeroTier yourself.

## v3.24.0 (2026-10-22)
- **Host game and Extension game.** The two games in a merge are now called the Host game (the one you play, on its own engine) and the Extension game (the one coded onto it). This is the wording everywhere in the app.
- **A Merge plan before you merge.** The merge step now shows what the Host game becomes once the Extension is coded onto it. It covers the world (which areas take on which look, the zone Frankenstein builds, lighting and weather, terrain and foliage), game modes, HUD, loading screens and menus, characters, and items and vehicles. Every line is marked **Built by Frankenstein**, **Mod**, **Stand-in** or **Not possible yet**, so you can see what each Host's engine really allows. With a Claude key, the AI writes a fuller plan for any pair. You can open any merge's plan again from ⋯ More.
- **World, on every merge.** A new standard part. Frankenstein builds the Extension's zone where you stand from the Host's own props and sets the Extension's light and weather. Map mods that name the Extension are installed when they exist. Merges you already have get it too.
- **Cyberpunk × Forza, the first full build.** On the This merge tab (press ~), **Build it here** places a Horizon Festival site: a ring of Night City's fastest cars, which comes back every session, plus **Go there**. A **festival golden hour** light button. New events start from the festival: a **Speed trap** (stars for speed), a 30-second **Drift zone** and a 2-lap **Circuit**, on top of the street races. There's also a **Forza-style speedometer** with big numbers, a speed bar and the event's timer, score and laps.
- **Schedule I × Hood Warfare, the second.** Press **F8** in town and **Build the block here**. It makes a Hood Warfare street corner from Schedule I's own dumpsters, fences, crates and barriers, and rebuilds it every session. **Hold the block** means staying on it for 3 minutes for $1,500. Also a **Hood night** light (purple haze), a Hood Warfare HUD (turf and mode timer), and a merged **title card** when the town loads.
- **GTA V gets the World too.** Ctrl+F10 builds the Extension's zone from GTA V's own props, and the Extension's light is set when the game loads.
- **A merged loading screen while the Host starts.** Pressing Play shows both games fused, with the merge's name.
- Honest limits: no mod can reshape terrain in Cyberpunk or Schedule I, and Schedule I's own HUD can't be swapped out yet. The plan says so line by line. None of the in-game parts have been tested in a real game yet.

## v3.23.0 (2026-10-22)
- **Every preview slide is now one picture of the merged game.** There's no more split screen and no box in the corner (that box was also showing up black). When a mod for the main game has screenshots, the slide shows those: the two games really merged, like a Forza-style car in Night City. They play as a slow moving gallery, marked "Mod screenshot". When no mod has pictures, Frankenstein blends both trailers into one frame. The main game is the scene, the other game is melted into its middle with soft edges, and one colour grade and light sweep cover both. The guns, items and characters it brings over still pop up by name. It is still labelled as a preview, not footage of your merge.

## v3.22.0 (2026-10-22)
- **Covers that move.** The picture inside each game's box now plays a short muted loop from the game's Steam trailer, like a GIF, while the box keeps floating in 3D. Merged games show both games moving side by side. Only the boxes on screen play, and the one under your mouse always does. Games without a trailer get a slow pan across their art. With Windows' reduce animations setting, everything stays still. Clips stream from Steam and aren't saved.
- **Preview slides move too.** Each slide plays a different moment of the main game's trailer, with the other game's trailer (or the mod's own picture) in the corner. The things it brings over pop up one by one, for example each Hood Warfare gun and what it is in Schedule I ("Choppa (AK) → AK47"), the gun shop prices, the starter kit and the rival boss, weapons dealer and crooked cop. These are trailers and labels, not footage of your merged game.
- **Play as the other game's character.** Every merge has a new part, Play as. In GTA V, Frankenstein's own script swaps you into the closest GTA V character and gives them the other game's moves: a wobbly Gang Beasts walk that flops over when hit, superhero super jump and sprint (as Impotent Rage), a knight as the Juggernaut, and more. F11 switches back. In Cyberpunk, V keeps their body but gets the other character's toughness and name (This merge tab). In Schedule I it's your name and the story for now. For other games, Frankenstein looks for a character mod that names the other game, and offers one-click Nexus picks. When the other game is all about its characters (Gang Beasts, Stick Fight, DC, Elden Ring), Play as becomes the ★ Hero part. What it adds says how close each game gets.
- **Frankenstein inside the game.** Creative and the rundown now live in the games themselves. Schedule I has a Frankenstein panel on its main menu under Continue and New game. Cyberpunk has a "This merge" tab when you press ~, even on the main menu. GTA V has F10 for the rundown and Shift+F10 to switch. Pressing Creative there closes the game and Frankenstein starts it again in Creative with its own saves, and the same way back.
- **A roomier merge card.** Play is big. What it adds and Share sit under it, and everything else (Creative, Rundown, Spawner, Auto-fill, Mods & profiles, Add mods, Fix crash, Open folder, Undo, Delete) is in a ⋯ More menu. The card also says what's inside the game itself.
- Merges you already have get Play as the first time this version opens.

## v3.21.0 (2026-10-22)
- **Preview shows both games merged, as a slideshow.** It opens on one picture of the two games fused together (a screenshot from each, split by a glowing seam). Then it plays slides of what you can actually do in the merge, one for each part, with a picture and a caption, for example "Buy Hood Warfare's guns at Handy Hank's hardware store" or "Races and street racing: Street races". The trailers for both games are still below it. It works for every merge, and it's still labelled as a preview, not your merged game.

## v3.20.0 (2026-10-22)
- **No blank covers.** A game with no art anywhere gets its own cover, made from its name in Frankenstein's style. Minecraft gets blocks.
- **Covers are 3D and move.** Every cover floats gently in 3D with a light sweep across it, and tilts toward your mouse when you point at it. Turn on Windows' reduce animations setting to keep them still.
- **Tools aren't shown as games any more**, for example Steamworks Common Redistributables, GameSave, Proton or dedicated servers.

## v3.19.0 (2026-10-22)
- **A self-check after every update.** When Frankenstein opens it checks its own files, its mods for each game and every merge. It backs up each merge first, then brings anything older up to date: Frankenstein's own mods and the loaders they need, filling empty parts, splitting off shared files, and putting back mod files that went missing. After an update it tells you "Everything's up to date" or "Fixed N things". You can run it any time from Settings > **Check everything**. If Frankenstein's own install is damaged, it offers **Repair**.
- **Nothing from an earlier update goes missing.** Every release now runs a feature checklist covering everything Frankenstein has shipped, and the release won't build if anything is gone.
- **In-game stories for GTA V and Cyberpunk.** A questline you make for a GTA V or Cyberpunk merge now plays inside the game. In GTA V, quests arrive as texts from the merge's characters, the objective shows on screen (F9 repeats it), and you get cash and weapon rewards. In Cyberpunk the objective shows on the speedometer HUD, and there's a Story tab (press ~) with an "I did it" button for steps the game can't detect.
- **Hero item first.** Every merge brings the other game's most iconic thing over first, like Forza's cars, Hood Warfare's guns or Minecraft's skins. It's marked ★ Hero on the merge step and in What it adds. New styles that the AI writes start with the hero too.

## v3.18.0 (2026-10-22)
- **No part of a merge is left empty, for every merge.** "What it adds" never says "couldn't bring over" again. For each part, Frankenstein downloads a mod when it can. If it can't, its own mod for that game covers it. If no mod exists at all, the part becomes a quest in the merge's story, marked "story stand-in". Mods that only Nexus Premium can download show as one-click picks next to the stand-in. Merges you already have are filled in the first time this version opens.
- **Spawner, inside Frankenstein.** Press **Spawner** on a merge and click any item or car: it appears in the running game within a second. You can keep it open on a second screen. Right-click gives 10. It works for Schedule I (every item, mods' items too), Cyberpunk 2077 (every item and car) and GTA V (cars, aircraft and weapons). The Schedule I and Cyberpunk lists fill in after you play the merge once.
- **Cyberpunk Forza merges get Frankenstein's own car features** (press ~): a **Garage** tab (the real Porsche 911 Turbo and the quickest cars, one click into your garage), **Tuning** (power and brakes for every car), **Races** (timed street sprints with cash prizes, double for beating par), and a **speedometer** with the race arrow and timer while you drive.
- **GTA V gets Frankenstein's own spawner script** (Script Hook V .NET): cars and weapons from the Spawner, and F7 in game gives every weapon.
- **Check for updates** in the sidebar asks GitHub right away and shows your version.
- Frankenstein's own mod in each merge now updates itself when you press Play, so new features reach old merges.

## v3.17.0 (2026-10-22)
- **Your original games and saves stay untouched.** New merges are full, separate copies by default, so Frankenstein can change them freely. "Mod the original" is gone. Older "Fast copy" merges are fixed when this version opens: every file the game or a mod could write gets its own copy, so nothing reaches the original install.
- **Backups of your backups.** Before every play, Frankenstein backs up your own saves and the merge's saves. After every play it checks that your originals didn't change, and puts them back from the backup if anything touched them (for example Steam Cloud). Each merge shows "✓ Originals untouched" or a warning that says what to do.
- **Damaged saves fixed automatically.** A merge save the game would call "damaged" (cut off half way, empty or broken) is swapped for its last good copy after you play. The broken one is kept aside, never deleted. Crash repair now checks saves first. Frankenstein also waits for the game to fully close before it swaps saves back, including games that restart through Steam.
- **Accounts stay signed in.** Pick your account once and that PC opens straight into it until you press Switch. Your account (admin rights, PIN, achievements) follows you to your other PCs through OneDrive or Google Drive. Without either, use a link code: Settings > Accounts > Get my link code, then paste it on the other PC.
- **Play together fixed.** Fix connection opens the Windows firewall for the party and restarts ZeroTier. The host can press **Share with the party**, and friends press **Get the host's merge**, so everyone has exactly the same mods.
- **Cyberpunk sandbox built in, no keys needed.** Press ~ in game for every item, **every car into your garage** (then press V to call it), god mode, +100,000 eddies, heal, lose the police, time of day, weather and a fly tool. Mods' items and cars show up there too. Free camera is the game's own Photo mode.
- **The merge step picks mods for you.** Every part shows "Auto". Find mods and Add file are only there if you want a specific mod.
- **Real-life cars for the Forza merge**: these are only on Nexus Mods, and Nexus only lets apps download for Premium members. "What it adds" has a hand-picked list (Porsche, Skyline, Lamborghini, BMW, Supra, Ferrari, Mustang, Audi). Press **Mod Manager Download** on a mod's page and Frankenstein installs it.
- **Schedule I gun shop.** Handy Hank's hardware store sells the Hood Warfare arsenal (Choppa, Uzi, Glock, Pump Shotty, .308 Sniper, Blade, ammo, plus the minigun and RPG).
- **Item menu search works.** Just start typing while it's open (the game pauses so keys don't move you). Back on a controller clears the search.
- **Library**: the two games in the merge glow purple, every game has a **Make host** button, and the top right shows this merge with the host on the left and the game merging in on the right. Click a game that one of the host's styles uses to make it the second game.

## v3.16.0 (2026-10-22)
- **Hood Warfare's guns in Schedule I.** Schedule I × Hood Warfare merges now install **More Guns** (the AK47, SMG, sniper, RPG and minigun mod on Thunderstore; the right build for your copy of the game, normal or beta). The first time you load a save you get the Hood Warfare arsenal, with each gun showing Hood Warfare's name: Choppa (AK47), Uzi (SMG), Glock (M1911), Pump Shotty, .308 Sniper and Blade (machete), plus ammo.
- **What it adds** lists every Hood Warfare gun and what stands in for it, marked "close match" (same kind of gun) or "rough stand-in" (the nearest thing Schedule I has; for example, The Nine has no burst fire). Nothing is copied from Hood Warfare.
- **Police that shoot back**: the merge also adds Police Response Overhaul when it's available.
- **The item menu is now a grid of tiles with each item's real icon.** Click a tile to grab one, right-click for a full stack. With a controller: the d-pad or left stick moves, A grabs one, X grabs 10, Y grabs a stack, LB/RB change category, B closes. Search is still at the top.
- Press **Auto-fill** on an existing Hood Warfare merge to get all of this.

## v3.15.0 (2026-10-22)
- **The merge standard, for every merge.** Every merge now gets the same four layers: gameplay from the other game (auto-installed mods), its signature gear (a starter kit and shops), a story like a DLC that continues the main game's world, and characters with new roles (weapons dealer, rival boss, crooked cop, fixer, getaway driver, insider).
- **Questlines write that story**: acts and quests, with phone messages from the characters, what each one sells, and rewards. They use the real items and people from your game (Frankenstein's Schedule I mod lists them the first time you play).
- **How deep each game goes is shown honestly**: on the main game's colour and as a tag on each merge. Schedule I: in-game story. GTA V and Cyberpunk: a companion story you follow from Frankenstein for now. Games with little mod support: light.
- The story's starter kit is handed over in game (Schedule I).
- The in-game quests and texts in Schedule I are built and ready; they switch on after a test run on a real PC.

## v3.14.0 (2026-10-22)
- **Frankenstein does the merging itself.** When you press Merge, it looks up the host-game mods that bring each part of the other game over (weapons, vehicles, gangs, police, gameplay and so on) on Thunderstore, Modrinth, CurseForge and Nexus Mods (with Premium), then installs the best one for each part, plus anything those mods need. You don't have to search for anything.
- **What it adds**: every merge lists what it brought over and from where, plus anything it couldn't find and why. Merges you already have get an **Auto-fill** button that does the same for them.
- **Thunderstore** is now a mod source (no account needed). That's where most Schedule I, Valheim, Lethal Company and R.E.P.O. mods live.
- **Schedule I x Hood Warfare**: the merge now pulls in Schedule I gun mods automatically, and the first time you load a save it hands you a starter kit with a pistol, a revolver, a shotgun and ammo, built from Schedule I's own guns. Nothing is copied from Hood Warfare.
- **An item menu for every item in the game, mods' items included.** In Schedule I press F6 (or Back + Y on a controller) for a searchable menu with categories and icons. In Cyberpunk 2077 open the Cyber Engine Tweaks overlay (~). In GTA V, Menyoo (F8) spawns any weapon or vehicle. In Minecraft use the Creative inventory plus Just Enough Items. It's always on in Creative; Settings > Item menu turns it on for normal play too.
- **Creative on every merge, with sandbox tools in one click**: press ✦ Creative, then **Add sandbox tools for me**.
- Existing Schedule I and Cyberpunk merges get the item menu the first time this version opens.

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
