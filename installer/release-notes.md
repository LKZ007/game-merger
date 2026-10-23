## Frankenstein 3.42.2

- **Merged games shouldn't lag. "No lag" is now part of the minimum standard for every merge.**
  - **A budget for Frankenstein's own Schedule I mod:** everything it does in a frame (its updates, menus and HUD) should take under 2 ms. It times itself, and the player's inventory is now found once and kept instead of being searched for several times a second.
  - **An in-game FPS check.** When the game drops under 40 FPS, or the mod goes over its budget, for a few seconds, Frankenstein turns its own extras down by itself. The crews, gangs, car and gun checks run less often, far-off crew members think less, and the launcher arc uses fewer dots. You get a short message when this happens. When the game has run smoothly for a while, the extras go back up. perf-debug.txt records the FPS and the mod's own cost every 30 seconds.
  - **Settings > Performance:** Automatic (recommended), Always full, Lighter, or Lightest for slower PCs.
  - **The app steps back while a game runs.** Its animations and videos stop, it drops to below-normal priority, and the merge terminal closes the moment a game starts (it never opens over one).
  - Honest limit: this only lightens Frankenstein's own work. If the game itself is heavy on your PC, its own graphics settings help more. The merge card's checklist shows how far each Host game gets.
- Not tested in game yet.

### Also in 3.42.1

- **Creative is never in the app.** The "✦ Creative" button is gone from every merge card, and a test keeps it gone. Creative lives inside each game.
- **Elden Ring: Creative is in the game on every Play.** Press both sticks for the Creative wheel, or 0 for its menu, on any character. It has the item spawner, fly, Torrent as a broom, no damage and unlimited spells. Elden Ring's own menus are locked in its archives, so there's no Creative entry in them, and the mode isn't remembered per character. The merge card says so.
- Not tested in game yet.

### Also in 3.42.0

- **Elden Ring is now a Host game with Frankenstein's own mod, starting with Elden Ring x Hogwarts Legacy.**
  - **Offline only, always.** Play starts the merge copy through Mod Engine 2 (downloaded by itself), without Easy Anti-Cheat, so it can't go online. The original game and your saves aren't touched: the merge has its own saves, starting as a copy of your characters.
  - **House pick at character creation.** Four starting classes become the houses, each with a wand (the Astrologer's Staff), robes from Elden Ring's closest sets, its own stats and a house spell: Hero = Gryffindor (Incendio), Prisoner = Slytherin (Expelliarmus), Astrologer = Ravenclaw (Glacius), Confessor = Hufflepuff (Protego).
  - **Hogwarts spellbook as sorceries:** Stupefy, Lumos, Protego, Expelliarmus, Depulso, Incendio, Accio, Glacius and Bombarda, each one Elden Ring's closest sorcery. The full list is on the merge card and in the rundown.
  - These kits are built on every Play from the copy's own regulation.bin into Frankenstein's mod folder. Every step is checked first, and if anything doesn't match, the game starts with its own classes and `frankenstein\frankenstein-debug.txt` says why.
  - **Creative tools** (the Elden Ring Practice Tool, set up by Frankenstein): item spawner, no damage, unlimited FP, fly, Torrent flying like a broom, speed and runes. They work on your existing characters too. Press 0 for the menu, or both sticks for the controller wheel.
  - **Your Hogwarts Legacy character:** Carry over my save reads their name and house from your Hogwarts Legacy save (read only). The rundown then tells you which class to pick and to make a lookalike in Elden Ring's creator.
  - Honest limits, shown on the merge card's minimum-standard checklist: Elden Ring's text, menus, HUD and models are locked in its encrypted archives. So items keep their Elden Ring names, the robes aren't recoloured, Torrent keeps his look, and the creatures are Elden Ring's own.
  - **Elden Ring merges made with an older version** (like one made today) move onto the new recipe in place on the first start of 3.42.0: same game copy, no second copy, a backup of the merge's record first.
- Not tested in game yet.

### Also in 3.41.1

- **Merge terminal.** While a merge is made, filled in, upgraded or unpacked, a small terminal window opens beside Frankenstein and shows the game being rebuilt live: each file copied or linked with its size, each mod installed and each part filled.
  - It's black and Frankenstein purple, with a progress bar, and closes itself a few seconds after the merge finishes.
  - The usual merge dialog stays as it is.

### Also in 3.41.0

- **Your Creative character.** When the game's character creator opens in a new Creative world, Frankenstein asks first:
  - **Quick start:** skips character creation and uses your latest story character's look, or the game's default if you have none.
  - **Use another story character:** one option for each story save that has a character. Its look is copied, and that save is only read, never changed.
  - **Make a new character:** opens the game's own creator.
  - Works with mouse, keyboard or controller (d-pad and A). What happened is written to `UserData\Frankenstein\character-debug.txt`.
- **"Creative character" is now part of the minimum standard,** so it's checked on every merge card.
- Not tested in game yet.

### Also in 3.40.2

- **Dropping, and anything that needs to know what's in your hand, now reads your hand properly.** Before, the mod couldn't tell which hotbar slot you were holding, so drop said there was nothing in your hand.
  - The same problem also kept the own guns' models, crosshairs and the launcher's grenades from switching on.
  - Frankenstein now tries several ways to read the held slot: the inventory's own index, its equipped slot, then each slot. The way that worked is written to `drop-debug.txt`.
- Not tested in game yet.
