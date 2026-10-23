## Frankenstein 3.41.1

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
