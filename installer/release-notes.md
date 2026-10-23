## Frankenstein 3.40.2

- **Dropping, and anything that needs to know what's in your hand, now reads your hand properly.** Before, the mod couldn't tell which hotbar slot you were holding, so drop said there was nothing in your hand.
  - The same problem also kept the own guns' models, crosshairs and the launcher's grenades from switching on.
  - Frankenstein now tries several ways to read the held slot: the inventory's own index, its equipped slot, then each slot. The way that worked is written to `drop-debug.txt`.
- Not tested in game yet.

### Also in 3.40.1

- **Own guns: a fuller check of why they still spawn as the base gun.** On 3.38 the sniper, AK, Uzi, RPG and launcher still came out as the revolver, M1911 and shotgun. That means the game didn't take Frankenstein's new items.
  - Frankenstein now finds the game's item registry more reliably and writes every step to `UserData\Frankenstein\ownitems-debug.txt`, including where it stopped.
  - Each time you take one of these guns from F6, it also writes what actually landed in your hotbar (item, name, icon).
- Not tested in game yet.

### Also in 3.40.0

- **Every merge is now checked against one minimum standard: everything the Schedule I merge has.** That means the main menu, loading screen, Profiles, in-game Creative, the item spawner, own weapons with real stats, a crosshair per weapon, own cars, gangs and NPCs, the controller, Save Game and debug logs.
  - Each merge card has a **Standard x/12** tag. Click it to see the checklist, with an honest note on each line: built by Frankenstein, done by the game itself, partly there, still to come, or not possible in that game.
  - Frankenstein installs and updates its own mod for each game by itself, so the lines it can build need nothing from you.
  - **Schedule I:** meets all 12.
  - **GTA V and Cyberpunk:** some lines are still partly there or to come, like own weapon models, own cars and gangs. GTA V's title screen can't be modded.
  - **Other games:** they get Frankenstein's own mod later. Until then the checklist says which lines are still to come.
