## Frankenstein 3.40.1

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

### Also in 3.39.0

- **Save Game in the pause menu.** It sits under the game's own list. Click it, press Y on a controller or Ctrl+S. The game saves the same way it does when you sleep, and "Game saved" shows when it's done.
- **The Creative hint bar isn't cut off any more.** It's sized to fit its text, and it's hidden while the pause menu is open.
- **The base car's body is now really hidden under the new cars.** A name check treated parts like "Trim" and "Primary" as wheel rims, so they stayed visible. The game's own list of body panels is now hidden too, and kept hidden if the game shows it again. The G-wagon's roof rails now sit on the roof. What was hidden and kept for each car is written to `UserData\Frankenstein\garage-debug.txt`.
- More realistic models for all five cars are coming next.
- Not tested in game yet.
