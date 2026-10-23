# Changelog

Every release, newest first. Downloads are on the [releases page](https://github.com/LKZ007/game-merger/releases).
The app was called Game Merger before 3.8.0.

## v3.36.0 (2026-10-23)
- **Schedule I's main menu, redone like your reference.**
  - A night city backdrop covers the whole screen, so the game's old title, Credits button and RV scene don't show through any more.
  - The "SCHEDULE 1 × HOOD WARFARE" title sits at the top left.
  - A clean list with icons: Continue, New Game, Creative, Multiplayer (the game's Invite Friends), Settings, Profiles and Quit. The chosen row gets a green glow.
  - Your latest save's cash and level show at the top right, with a settings button. The version is at the bottom right.
  - The backdrop and title are painted by Frankenstein, not copied from either game.
- **Profiles:** a card for each save slot, with its name, Creative or Survival, level, cash, properties, last played and a green Play button. Empty slots have a + to start a new world. On a controller: d-pad left and right, A to play, B to go back.
- **Loading screen like the reference:** the title in the middle of the night city, LOADING..., a green bar and "Build your empire. Run your block."
- **Storage moves items now.** Putting things in a storage rack (or a shop, or anywhere through a menu) in Creative used to leave a copy in your hotbar. Creative only refills what you use or place while playing. Dropped items don't come back either.
- **Drop from the inventory with a controller.** With the inventory or a storage rack open, LB and RB pick a hotbar slot (shown at the bottom of the screen) and d-pad down drops that slot's items. While playing, d-pad down still drops what's in your hand.
- **Creative skips the tutorial.** A new Creative world goes straight to town instead of the RV in the desert. If this build won't let it, it tells you and writes `UserData\Frankenstein\tutorial-debug.txt` so it can be fixed.
- Not tested in game yet.

## v3.35.0 (2026-10-23)
- **Every added weapon has its own look now,** in your hands and in the F6 Weapons tab:
  - AK-47: wooden stock and handguard, curved magazine, long barrel.
  - Uzi: boxy body, magazine in the grip, folded stock.
  - .308 Sniper: long barrel, scope, bolt handle, bipod.
  - RPG: green tube with the warhead on the front. The warhead disappears when you fire and comes back when you reload.
  - Grenade Launcher: six-shot drum with grenades in it, stock and front grip.
  - Schedule I can't load new gun models, so each one is built from simple shapes in the game's own materials. The gun it's built on (M1911, revolver or pump shotgun) is hidden.
- **Get in and out of cars with the controller.** Walk up to a car's door and press **X** (E on the keyboard). Press X again to get out. A prompt now shows by the car with its name. Frankenstein tries each of the game's ways in until one works. If none does, it says so and writes `UserData\Frankenstein\vehicles-debug.txt` for fixing it. "Car: in or out" is on the wheel too.
- **New cars in F6 > Vehicles,** designed by Frankenstein (unbranded lookalikes):
  - **Velocci 720:** papaya-orange supercar.
  - **Toro Furioso:** yellow supercar with a big wing.
  - **Notte GT:** purple GT with violet underglow.
  - **Gelände G63** and **Gelände G Desert:** boxy luxury off-roaders.
  - Each has its own paint, body kit (splitter, side skirts, wing and diffuser, or a spare wheel, roof light bar, grille and step bars), LED lights and more top speed.
  - Underneath, each is one of the game's own cars (Cheetah, Hounddog or Bruiser), so it drives, parks and saves like any car. Its kit comes back when you load the save.
- **Put your laptop and PC on one account with a code.** In Settings > Accounts > Your devices, press **Show a pairing code** on the PC that has your account. Type the code on the other PC and press **Pair**. There's no Yes to press on the other side.
  - Both PCs need Frankenstein open, on the same Wi-Fi or router. Nothing leaves your network.
- **Why the PCs couldn't see each other:**
  - Windows' firewall blocks Frankenstein on Wi-Fi marked Public. If a PC hasn't heard any other Frankenstein, a **Let Frankenstein through the firewall** link shows. It needs one Yes on Windows' prompt.
  - PCs with a VPN, Hyper-V or VirtualBox adapter sent their hello out of the wrong network. Hellos now go out on every network the PC is on.
  - Two PCs syncing through the cloud folder could swap accounts and stay apart. Now they both end up on the same one.
- The Weapons tab header no longer says "More Guns isn't loaded".
- Not tested in game yet.

## v3.34.0 (2026-10-23)
- **Schedule I has a new main menu,** drawn by Frankenstein like the picture you sent. It has big cards for Continue ("Load your world"), New Game ("Start a new world"), **Creative ("Unlimited resources")**, Settings and Quit, with the merge's picture, its name and your saves (with their game modes) on the right.
  - Creative starts a new world in Creative.
  - Use the mouse, the arrow keys and Enter, or a controller (d-pad or stick, then A).
  - The game's own buttons still do the work behind each card, so loading, new games and settings work as before.
  - Copying a row into the game's own list never worked on this build, which is why Creative didn't show before. This menu doesn't depend on that.
- **Drop with a controller:** press d-pad down to drop what's in your hand ("Drop item" is on the wheel too). It uses the game's own drop. If this build doesn't have one, it tells you to drag the item out of the inventory instead.
- Not tested in game yet.

## v3.33.0 (2026-10-23)
- **See your other PCs.** Every PC signed in to the same Frankenstein account now shows the others in Settings > Accounts > Your devices, with what's installed and merged on each one, when it was last seen and which Frankenstein version it runs. PCs find each other on the same Wi-Fi, and through your cloud folder (OneDrive, Google Drive or Dropbox) when they're apart. A PC only shares its list with PCs on the same account.
- **Put a PC on your account in two clicks.** Open Frankenstein on both PCs on the same Wi-Fi. On the new PC, go to Settings > Accounts and press **Use levy's account on this PC** next to the other PC's name. Then press **Yes, it's mine** on the other PC. If the account has a PIN, you have to be signed in to it to say yes. The link code still works too.
- Windows may ask once whether Frankenstein can use the network. Allow it on private networks.

## v3.32.1 (2026-10-23)
- **Melee swings hit harder: the bat, knife, machete and the rest.**
  - The camera pulls back while you wind up and whips through as you swing.
  - When you hit someone, the moment freezes for a split second, the screen shakes, they're knocked back and they take extra damage on top of the game's own.
  - Hold attack (left mouse, or RT on a controller) for half a second before letting go for a heavy swing: twice the damage and a bigger knock.
  - There's no new sound yet: the game's own hit sound plays. Not tested in game yet.

## v3.32.0 (2026-10-23)
- **Schedule I: Creative is now a game mode on each save, like Minecraft.** There's one set of saves now, not a separate Creative copy.
  - On the main menu, **Game Mode** (between New Game and Settings) picks Survival or Creative for the game you start or load next. Click it, or press Y on a controller.
  - While you play, F8 switches the save you're in, straight away with no restart.
  - Each save keeps its own mode. It's stored inside the save, so it stays with it through backups and packing.
  - The first time a save changes mode, a copy of it is kept in UserData/Frankenstein/save-backups.
  - The main menu's corner lists your saves with their modes. The game's own load list can't show them.
  - Your old Creative saves move in with your other saves the next time you press Play, already marked Creative. If there's no free save slot, they're kept safe, and the merge's card tells you.
  - Gang standing, crews and Frankenstein's guns are now kept per save.
- **Main menu entry, take two.** If this build of the game won't let Frankenstein strip the copied menu row, it copies just the row's words instead. Where "New Game" sits in the menu is written to UserData/Frankenstein/menu-debug.txt and to the log, so it can be fixed from one file if it still doesn't show.
- **Everything works with a controller.** Hold **View** (Back) to open a wheel, point the left stick at what you want and let go. The wheel has: Items, the Frankenstein menu, Gangs, Who's who, Throw grenade, Rob, Game Mode, Fly, Speed and Get in car. In every Frankenstein panel, the d-pad moves a highlight, A presses and B closes. Button prompts show when you're using the controller.
- **AK-47, Uzi, .308 sniper and RPG are always in F6's Weapons tab.** If the More Guns mod didn't load in your game, Frankenstein adds its own. The game can't be given new gun models, so each one looks like the gun it's built on (the M1911, revolver or pump shotgun), but it has the real gun's full auto, magazine, fire rate and damage. The RPG fires a rocket that explodes. The Weapons tab header and the merge's card say which guns loaded.
- **Creative: fly and speed.** Double-tap jump (Space, or A) to fly or land. While flying, jump goes up, and Ctrl or B goes down. F7 (or Speed on the wheel) steps through 1x, 2x, 3x and 5x.
- **Cars from F6 are yours and drivable.** They spawn on a clear spot on the ground, not inside walls, and are set as owned by you. Walk up to one and press **E**, or X on a controller, to get in. A prompt shows when you're next to a car.
- **Less lag.** Frankenstein's mod no longer reads your hotbar or searches the game's objects every frame. Its reflection lookups are cached, it searches for guns less often, and gang crews far away from you think less often.
- Not tested in game yet: the menu entry, flying, getting in with X, and the own guns' numbers. Send the MelonLoader log if anything doesn't work.

## v3.31.3 (2026-10-23)
- **The desktop and Start menu icon is the new hooded-figure logo,** and so are the app's .exe and the installer. Updating rewrites your existing desktop shortcut and tells Windows to refresh its icon cache, so the old head-and-brain icon goes away. If Windows still shows the old one, restart the PC once.

## v3.31.2 (2026-10-23)
- **Fixed: Play failing on a packed merge with "Not enough disk space".** Rebuilding the copy asked for 5% more room than it needed. Now it asks for the copy's real size plus 0.3 GB. If the drive is still too full, you're told exactly how much to free (empty the Recycle Bin, or run Disk Cleanup). You also get a button to rebuild the copy on another drive that has room, or to pick a folder, for example on a USB or external drive. A copy that runs out of room part way through is removed instead of being left half made. Your packed merge and its saves are never touched by any of this.
- **Packing after you play is skipped when your drive has less than 3 GB free.** Otherwise anything that fills the drive next (an update, a download) could leave no room to rebuild the merge. The card says why it wasn't packed, and you can still pack it yourself from ⋯ More.
- **Creative is gone from the app's ⋯ More menu.** It now lives only inside the game: in Schedule I it's in the main menu under New Game (and F8 while you play), in GTA V it's F10, and in Cyberpunk it's the ~ menu. Games Frankenstein can't add a menu to keep their Creative button on the card.

## v3.31.1 (2026-10-23)
- **Fixed: new Frankenstein features weren't reaching Schedule I.** If you switched between Creative and your normal game, Frankenstein rebuilt the merge from the copy of its mod it kept the first time, so the game kept running an old mod. That's why F6 had no Weapons or Vehicles tabs and no item icons. Now every Play compares the mod in the game with this app's mod, file by file, and replaces it if it differs, and the kept copy is refreshed too.
- **You can see which mod is running.** F6's title now reads "FRANKENSTEIN 3.31.1". After you play, the merge's card says "Frankenstein mod 3.31.1 ran last time you played", or warns you if an older one ran.
- **Schedule I, experimental: more map.** Six new districts east of the town, one per gang (Eastside Projects, Red Hill Estates, Peckham Estate, Little Tokyo, Little Italy, Bow Road), at the town's own scale. Each has ground you can walk and drive on, a crossroads as wide as the town's roads, 32 buildings copied from the town, and flags in the gang's colours. A road links them. They're built about 25 seconds after the town loads. Get there with F8, then Travel.
  - Limits: the buildings are shells with no doors or insides, the game's map doesn't show them, and the town's people and the gangs' crews don't walk there. Not tested in game yet.

## v3.31.0 (2026-10-22)
- **Schedule I: Creative is now an entry in the game's own main menu,** between New Game and Settings, in the same font and style as the rest of the list. In Creative it reads "Back to normal game". The 3.27 version never appeared on your PC: copying the New Game row worked, but hooking up its click failed on this build of the game, and one failure made it give up. Now:
  - The copy has every button and script taken off, so it can never start a new game.
  - Frankenstein watches for the click itself.
  - It keeps trying for 30 seconds instead of stopping at the first error.
  - Every reason it can't add the entry is written to the MelonLoader log.
- **The Frankenstein panel no longer covers the Schedule I title.** If the menu entry can't be added, a small panel shows in the top right corner instead.
- **Schedule I: the gangs have their own people now, 24 of them, who are always in the world.** They're real characters of the game (not the cartel's goons, so the cartel doesn't need to be active). Each gang has a boss, a lieutenant, an enforcer and a dealer or gun runner, all named, in the gang's colour and carrying its gun. For example, the Crips have Darnell "Big Loc" Hayes, the Mafia have Vincent "Don Vinnie" Moretti and the Yakuza have Kenji "Oyabun" Moriyama.
  - **What they do:**
    - Bosses hold their HQ.
    - Lieutenants and enforcers patrol their district.
    - Dealers and gun runners walk up to the town's people and do business, and the gang's money grows.
    - Rival crews fight when they meet.
    - Every few minutes one gang raids its rival's turf.
    - A crew you've robbed comes for you on its turf.
    - Anyone knocked down is back on the street a few minutes later.
  - **A story for every gang:** five parts, told by text from its lieutenant and boss. They introduce themselves when you walk onto their turf, send you to rob their rival's dealer, then send you after the rival's enforcer. Each part pays cash and rep.
  - **F10 shows who's who:** every gang, its people, what each one is doing right now, the gang's money and how far its story has got.
  - Patrol routes start at each district's own buildings and grow from where you walk there.
- Not tested in the game yet.

## v3.30.0 (2026-10-22)
- **Merges take up far less space.** After you close a merged game, Frankenstein packs it away:
  - It keeps only the files the merge changed or added (mods, their settings, anything the game wrote into its folder) in one zip.
  - It zips the merge's saves (normal and Creative).
  - Then it deletes the copy.
  - Your original game is only ever read, and nothing is deleted until the zip has been checked.
- **Play rebuilds it first.** Frankenstein makes a fresh full copy of your own installed game, puts the merge's files and saves back, then starts it. That takes about as long as copying the game: seconds for small games like Schedule I, several minutes for big ones like Cyberpunk. The card says how long, and after the first time it's the measured time.
- **On each merge card:** a 📦 Packed tag with the space saved, plus **Pack away to save space** and **Unpack now** under More. Adding mods, fixing crashes and the other buttons unpack first.
- **Settings > Save space** turns automatic packing off.
- If a Steam update changes the game, the rebuilt copy uses the updated game, with the merge's own files on top. If you uninstall the original game, a packed merge can't be rebuilt until you reinstall it.

## v3.29.0 (2026-10-22)
- **Schedule I guns sit in your hand properly.** The pistol was stretched and tilted out of the hand, and a gold block floated beside it. Frankenstein no longer stretches the held gun or sticks blocks onto it, and it cleans up any it added before. Replica guns keep their own numbers (damage, fire rate, magazine, reload, full auto); only the model is sized, a little and evenly, so the grip stays in the hand.
- **Every gun type in Creative's F6 menu, under a new Weapons tab:**
  - The game's own and More Guns' guns: M1911 (Glock), revolver, pump shotgun, AK47 (Choppa), SMG (Uzi), .308 sniper, minigun and RPG, with their ammo and melee weapons.
  - **Frankenstein's own grenade launcher.** Neither Schedule I nor any mod has one, so Frankenstein builds it. It's a pump shotgun that fires a grenade with every shot, and the grenade arcs, hits and explodes.
  - **Frag grenades.** Press T to throw one, and it explodes after about 2.5 seconds. Creative has unlimited grenades.
  - Explosions hurt everyone near them except you.
- **Cars in Creative:** a new **Vehicles** tab in the F6 menu with every vehicle in the game, including mods' cars. Each tile has a picture: the game has none, so Frankenstein draws each car's shape in its own colour. Click one and it's parked in front of you, facing where you look.
- **A bigger startup explosion:** the purple dotted burst at the end of the startup animation now fills the whole screen, corner to corner, with a flash and a shockwave.
- Not tested in the game yet. Gun numbers are estimates, and grenade damage uses the game's own NPC health.

## v3.28.1 (2026-10-22)
- **Fixed: Frankenstein could get stuck on the startup screen** with an empty loading bar, blank boxes and a dark brain. The window was running an older copy of the app's script next to the new page. Every start now loads this version's own files fresh, and a mixed-up install is caught, reported and repaired from Settings.
- **The startup screen can never keep you out.** A Skip button appears after a few seconds (Esc or a click also works), it closes by itself after 30 seconds, and a script error is shown and saved to a log instead of freezing.
- **Random games over the palms:** two random games from your library spin in the boxes, with their real covers once they load.
- The dot brain draws again on the startup screen, and slow steps (accounts, scanning, covers) no longer hold up the app.

## v3.28.0 (2026-10-22)
- **New logo:** the hooded figure holding a glowing dotted brain. It's the app's icon (desktop, Start menu, taskbar and installer) and the logo in the sidebar.
- **New startup animation:**
  - The figure's brain is Frankenstein's own moving dot brain. It faces forward, glows purple, and keeps collapsing inward and swelling back out.
  - Two of your games spin over the figure's palms in opposite directions.
  - As the loading bar fills, they spin faster and draw closer, and the hands move in.
  - At 100% the hands slap together, the games merge, and a purple dotted explosion opens the app.
  - With reduced motion turned on in Windows, it's just the bar.
- **Covers are your games' own cover art again, now moving.** No more trailer footage in the boxes. Each cover drifts and zooms slowly like a GIF of the art, with a light sweep and sparks rising in front.
- **No blank covers.** Games with no art anywhere (Minecraft Launcher, Minecraft: Java Edition and others) get a painted cover. Minecraft gets a pixel world of grass, dirt, stone and ores, and other games get a sky, a glow and hills in their own colours, with the name across the bottom. Forza Horizon 5's box was black because its trailer didn't play; it shows its cover now.
- **Schedule I item menu (F6):**
  - Every tile shows the item's picture from the game. If an item has no picture, it gets a coloured badge with its first letter.
  - The stray "12" category is now **Storage**.
  - The menu has a solid background, so the phone doesn't show through.
- **Creative is unlimited, like Minecraft's:**
  - **Schedule I:** the gun in your hand refills, and anything you use, place or sell from your hotbar comes straight back.
  - **GTA V:** your clip never empties.
  - **Cyberpunk:** every ammo type is topped back up the moment it runs low.
  - Your normal game isn't affected.

## v3.27.0 (2026-10-22)
- **Frankenstein's Schedule I mod now gets into your merge.** On the first real test, nothing of Frankenstein showed in Schedule I: no Creative on the main menu, no loading screen, and F6 didn't open in Creative. The mod was never added to that merge. Saving the merge's record could clash on Windows ("Access is denied" on manifest.json), and Play only refreshed the mod if it was already there. Now:
  - Every save uses its own temporary file, retries when Windows or a virus scanner holds the file, and clears a read-only flag. Pressing Creative no longer fails.
  - Play adds Frankenstein's own mod (and S1API) whenever it's missing, not only when it's already there.
  - After you play, Frankenstein reads MelonLoader's log. If its mod didn't load, the merge card says so in plain words and why.
  - The card no longer says "This game has no menu Frankenstein can add to" for Schedule I.
- **Creative in Schedule I's own main menu,** right under New Game, in the same font and style as the rest of the list (Back to normal game when you're in Creative). It's a copy of the game's own New Game button, so it looks native. If the game's menu can't be copied, Frankenstein's panel shows instead.
- **A Schedule I × Hood Warfare loading screen.** While a save loads, the merge has its own screen: both games' scenes cut together on a slant in Hood Warfare red, the title SCHEDULE I × HOOD WARFARE, Hood tips in turn, and the game's loading status.
- **Hood Warfare's guns, rebuilt.** When you hold a gun, Frankenstein sets it to the Hood Warfare replica's numbers and shape:
  - **Numbers:** damage, fire rate, magazine, reload and full auto. The Choppa hits hard and fast, the Uzi sprays (32 rounds), the Glock takes 17, and the .308 hits hardest.
  - **Shape:** longer or shorter and bigger or smaller, plus simple parts in the gun's own material: a curved mag on the Choppa, extended mags on the Uzi and Glock, a scope on the .308 and a drum on the Minigun.
  - **More Guns:** its own settings now give its guns Hood Warfare names (Choppa, Uzi, .308 Sniper) and numbers.
  - **Honest note:** Hood Warfare's real stats aren't published, so these are estimates. The models are Schedule I's and More Guns' with parts added, not exact replicas. What each gun got is written to UserData/Frankenstein/guns-report.txt.
- **Gangs (F9 in Schedule I × Hood Warfare).** Six gangs each run one district:
  - Crips (Northtown), Bloods (Westville), South London Mandem (Downtown), Yakuza (Docks), Mafia (Uptown) and East London Firm (Suburbia).
  - **Their HQ is built** in their district the first time you go there, from the town's own props with the gang's flag, and marked on screen.
  - **Rival dealers** work their gang's corners with guards. Knock the dealer out and press **G** to rob their cash and product (sometimes a gun). Rob a gang too often and they send hit squads after you on their turf.
  - **Join a gang** through its initiation: rob a dealer from its rivals, pay a $500 tribute, then survive a hit squad on its turf. Climb six ranks with rep (Mafia: Associate up to Don; Yakuza: Kobun up to Oyabun; Crips: Lil Homie up to Shot Caller, and so on). Your rank pays a daily cut.
  - **Or start your own gang:** name it and pick drugs, guns or both. Recruit a crew, and take districts from the other gangs in three-wave turf wars. Your districts pay out every game day and your crew makes stock.
  - **Trade:** buy and sell bricks of product and crates of guns with whichever gang runs the district. Each gang pays and charges differently, so buy cheap in one district and sell in another. You can also buy their guns and product straight into your pockets.
  - **Honest limits:** the fighters are the game's own cartel goons, so they look like Schedule I's people, and they only appear once the cartel is active in your save. Schedule I's land can't be made bigger, so the new places are built on top of the existing districts. Nothing here has been tested in game yet.

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
