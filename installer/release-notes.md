## Frankenstein 3.40.0

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

### Also in 3.38.0

- **Schedule I's screens now match your Hood Warfare reference.** Your reference picture is used for the art: the SCHEDULE 1 × HOOD WARFARE lockup, the night city with the hooded figure and "Same city, different hustles", the loading scene and the avatar. Frankenstein draws the live parts in the same places as in the reference.
  - **Main menu:** the list sits where the reference has it (Creative is still in it), with the cash and level pill and the settings button at the top right and the version at the bottom right.
  - **Profiles:** the list stays on the left with Profiles lit up, next to four cards. A save's card shows the avatar, its name, level, Hood Reputation, Cash, Drug Empire, Properties and a green Play button. An empty slot shows "Empty Profile" with a +.
  - **Loading:** the reference's loading screen with the green bar filling under LOADING...
  - This is only in Hood Warfare merges. Other merges keep the painted menu.
- **The AK-47, Uzi, .308 Sniper, RPG and Grenade Launcher are real items of their own.** Each has its own name, icon and model in the hotbar, inventory and your hands, so taking the sniper no longer gives a revolver and the RPG is no longer a pump shotgun. Each one still fires using the game's own gun underneath. What happened is written to `UserData\Frankenstein\ownitems-debug.txt`.
- **Each gun has its own fire rate and power.**
  - **AK:** fast and fully automatic.
  - **Uzi:** fully automatic, faster and weaker than the AK.
  - **Sniper:** slow, with very high damage and long reach.
  - **RPG:** one slow shot with a bigger blast than a grenade.
  - **Grenade Launcher:** a slow six-shot drum.
- **Each gun has its own crosshair.**
  - **Sniper:** a tight dot, and a scope zoom while you aim (right mouse or left trigger).
  - **AK and Uzi:** the AK's crosshair is medium and the Uzi's is wider.
  - **Shotgun:** a circle showing where the pellets spread.
  - **Pistols:** short lines.
  - **RPG and Grenade Launcher:** a dotted arc showing where the shot will land.
  - Each gun's spread is set to match its crosshair.
- Not tested in game yet. Guns you got before this update stay as the base gun.
