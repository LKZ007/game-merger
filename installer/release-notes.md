## Frankenstein 3.32.1

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
