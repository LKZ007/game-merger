## Frankenstein 3.36.0

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

### Also in 3.35.0

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

### Also in 3.34.0

- **Schedule I has a new main menu,** drawn by Frankenstein like the picture you sent. It has big cards for Continue ("Load your world"), New Game ("Start a new world"), **Creative ("Unlimited resources")**, Settings and Quit, with the merge's picture, its name and your saves (with their game modes) on the right.
  - Creative starts a new world in Creative.
  - Use the mouse, the arrow keys and Enter, or a controller (d-pad or stick, then A).
  - The game's own buttons still do the work behind each card, so loading, new games and settings work as before.
  - Copying a row into the game's own list never worked on this build, which is why Creative didn't show before. This menu doesn't depend on that.
- **Drop with a controller:** press d-pad down to drop what's in your hand ("Drop item" is on the wheel too). It uses the game's own drop. If this build doesn't have one, it tells you to drag the item out of the inventory instead.
- Not tested in game yet.

### Also in 3.33.0

- **See your other PCs.** Every PC signed in to the same Frankenstein account now shows the others in Settings > Accounts > Your devices, with what's installed and merged on each one, when it was last seen and which Frankenstein version it runs. PCs find each other on the same Wi-Fi, and through your cloud folder (OneDrive, Google Drive or Dropbox) when they're apart. A PC only shares its list with PCs on the same account.
- **Put a PC on your account in two clicks.** Open Frankenstein on both PCs on the same Wi-Fi. On the new PC, go to Settings > Accounts and press **Use levy's account on this PC** next to the other PC's name. Then press **Yes, it's mine** on the other PC. If the account has a PIN, you have to be signed in to it to say yes. The link code still works too.
- Windows may ask once whether Frankenstein can use the network. Allow it on private networks.

### Also in 3.32.1

- **Melee swings hit harder: the bat, knife, machete and the rest.**
  - The camera pulls back while you wind up and whips through as you swing.
  - When you hit someone, the moment freezes for a split second, the screen shakes, they're knocked back and they take extra damage on top of the game's own.
  - Hold attack (left mouse, or RT on a controller) for half a second before letting go for a heavy swing: twice the damage and a bigger knock.
  - There's no new sound yet: the game's own hit sound plays. Not tested in game yet.

### Also in 3.32.0

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

### Also in 3.31.3

- **The desktop and Start menu icon is the new hooded-figure logo,** and so are the app's .exe and the installer. Updating rewrites your existing desktop shortcut and tells Windows to refresh its icon cache, so the old head-and-brain icon goes away. If Windows still shows the old one, restart the PC once.

### Also in 3.31.2

- **Fixed: Play failing on a packed merge with "Not enough disk space".** Rebuilding the copy asked for 5% more room than it needed. Now it asks for the copy's real size plus 0.3 GB. If the drive is still too full, you're told exactly how much to free (empty the Recycle Bin, or run Disk Cleanup). You also get a button to rebuild the copy on another drive that has room, or to pick a folder, for example on a USB or external drive. A copy that runs out of room part way through is removed instead of being left half made. Your packed merge and its saves are never touched by any of this.
- **Packing after you play is skipped when your drive has less than 3 GB free.** Otherwise anything that fills the drive next (an update, a download) could leave no room to rebuild the merge. The card says why it wasn't packed, and you can still pack it yourself from ⋯ More.
- **Creative is gone from the app's ⋯ More menu.** It now lives only inside the game: in Schedule I it's in the main menu under New Game (and F8 while you play), in GTA V it's F10, and in Cyberpunk it's the ~ menu. Games Frankenstein can't add a menu to keep their Creative button on the card.

### Also in 3.31.1

- **Fixed: new Frankenstein features weren't reaching Schedule I.** If you switched between Creative and your normal game, Frankenstein rebuilt the merge from the copy of its mod it kept the first time, so the game kept running an old mod. That's why F6 had no Weapons or Vehicles tabs and no item icons. Now every Play compares the mod in the game with this app's mod, file by file, and replaces it if it differs, and the kept copy is refreshed too.
- **You can see which mod is running.** F6's title now reads "FRANKENSTEIN 3.31.1". After you play, the merge's card says "Frankenstein mod 3.31.1 ran last time you played", or warns you if an older one ran.
- **Schedule I, experimental: more map.** Six new districts east of the town, one per gang (Eastside Projects, Red Hill Estates, Peckham Estate, Little Tokyo, Little Italy, Bow Road), at the town's own scale. Each has ground you can walk and drive on, a crossroads as wide as the town's roads, 32 buildings copied from the town, and flags in the gang's colours. A road links them. They're built about 25 seconds after the town loads. Get there with F8, then Travel.
  - Limits: the buildings are shells with no doors or insides, the game's map doesn't show them, and the town's people and the gangs' crews don't walk there. Not tested in game yet.
