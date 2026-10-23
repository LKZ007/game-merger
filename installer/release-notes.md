## Frankenstein 3.31.3

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
