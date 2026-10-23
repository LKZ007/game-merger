## Frankenstein 3.47.0

- **A new merge with GTA V as the Host: Schedule I x Hood Warfare in Los Santos (milestone 1).** Make it from a GTA V merge by picking "Schedule I x Hood Warfare: business, gangs and guns" as the Extension. Story mode only, offline, on Frankenstein's own copy of the game.
  - **Labs, farms and production.** Buy four properties (Grapeseed farm, Sandy Shores lab, Strawberry grow house, Docks lab), buy supplies, start a batch of weed, meth or coke and collect it when it's ready. Hire a worker and the lab runs by itself.
  - **Street dealing.** Walk up to someone and press E (D-pad right) to offer product. Prices are better on your own turf, and some buyers are undercover cops. Dealers you hire sell your stock every in-game hour.
  - **Gangs and turf wars.** GTA V's own gangs hold their real neighbourhoods. Hold E in a gang's zone to start a four-minute turf war; win it and the zone is yours and pays every hour. Gangs try to take turf back, or you can buy a truce.
  - **Heat and raids.** Dealing and fighting raise heat; high heat brings police raids on your labs.
  - **Empire menu and HUD.** F5 (or hold D-pad left on a controller) opens the empire menu with waypoints to every lab. The HUD shows the territory you're in, who controls it, your product and your heat. Saved to scripts/Frankenstein/empire.txt, logged to empire-debug.txt.
- **Delete all merges** in Settings: removes every merge's game copy, its packed files and its own saves in one go, and says how much space it freed. Your installed games and their own saves are never touched.

### Also in 3.46.1

- **The free 3D model packs now download by themselves when you press Play** on the Schedule I merge, the first time, with the download shown in a window and in the merge terminal. No button needed. If a pack can't download, the game still starts, and Play tries again the next time you open the app. The button is still in Settings > Graphics and models, under Performance.

### Also in 3.46.0

- **The Schedule I x Hood Warfare restart, milestone 1: the foundation.** The old work is kept on its own branch and in release 3.45.1.
  - **Real 3D models in the game.** Frankenstein's mod now reads standard .glb and .gltf models (meshes, textures, colours, glowing lights) and puts them in the game. Models are read in the background, so the game doesn't stall.
  - **Free model packs, downloaded by your PC.** The app gets Kenney's CC0 car and city packs from kenney.nl the first time you press Play, or from Settings > Graphics and models. It keeps them on your PC and puts them into the merge with a licence list. You can also drop your own .glb files into the merge's UserData\Frankenstein\Models\Mine folder.
  - **Cars wear real models.** The F3 cars (and any you spawn from the sheet) wear a 3D car model fitted to the game's car. The model's own wheels turn, and the headlights and tail lights really light up. Until the packs are on your PC, a car keeps the game's own body, painted.
  - **The look, towards the concept art.** There's a new Graphics setting: Cinematic, High (the default), Balanced or Performance. It adds bloom, cinematic tone mapping, warm and teal colour grading, a vignette, longer shadows and sharper far detail. A golden-hour grade follows the game's clock at sunset and a blue grade at night. If the game drops under 40 FPS, the look steps down.
  - **A test city block** (F8 > Test city block: build here) built from real city models with collision. It streams out when you're far away and shows simpler buildings at a distance. It also checks whether the game's NPCs could walk there. It stays with your save.
  - **selftest.txt** in UserData\Frankenstein says what loaded: models, graphics, FPS and the test block.
- **Retired in the restart:** the code-built city island, Hood City, the F4 island map, the island gangs (Cartel, Outlaw MC) and the code-built car bodies. Guns, gangs, Creative, the menus and the controller stay until their milestones rebuild them.
- Honest limits: Kenney's models are clean low-poly ones, not the photoreal cars from your sheet. Those need licensed models, and they come in milestone 2. Schedule I's own characters and terrain keep their look. Not tested in game yet.

### Also in 3.45.1

- **Fixed: Play on a packed merge stopped with "...is in the way and isn't a Frankenstein copy".** An earlier rebuild that was cut off left a half-made copy behind without Frankenstein's marker, so the next Play didn't recognise it. Now Frankenstein marks a rebuild as its own before it starts copying. When it finds a leftover with only the game's files and the merge's own files in it, it clears that leftover and rebuilds. If the folder has anything else in it, the folder is moved aside (renamed, nothing deleted) and the copy is rebuilt. The installed game is still only read.
- **Fixed: your own guns registering (the AK-47 "Ambiguous match found" error).** The game has two members whose names differ only in case ("ID" and "id"), and Frankenstein's lookup tripped over that, so every gun fell back to its base gun. The lookup now picks the exact name and the nearest type. Singletons are found the same safe way everywhere.
- Not tested in game yet: please press Play, open F6 and spawn a gun. If one still comes out as a base gun, send UserData\Frankenstein\ownitems-debug.txt.

### Also in 3.45.0

- **Hood City: your map becomes the town itself (pick it per save).** On the main menu, the "World" entry switches between Hood City and Schedule I's original town for the save you load, and F8 has a World button to switch a save you're already in (your save is backed up first). The choice is kept in the save's Modded\Frankenstein\world.txt.
  - In Hood City, your map is laid over the town instead of off the coast. Every district, road and gang HQ goes where the picture has it.
  - **The original town's real places stay and keep working.** Shops, properties, enterable buildings and their doors are kept where they stand, and the new districts are built around them, so the spots that would have been empty shells are the town's own working buildings.
  - The town's scenery that would sit under the new districts is hidden while you play Hood City and comes back when you switch to the original town.
- Honest limits: the town's own ground, roads and hills stay underneath, so the layout is as close to your map as the land allows. New buildings are still Frankenstein's code-built shells with no insides, and the town's shops can't be moved, so they keep their own spots inside the new layout.
- Not tested in game yet.

### Also in 3.44.0

- **Cars like your sheet.** There are twelve lookalikes now, with made-up names and no real brands. Each one has the sheet's top speed and 0-60:
  - Supercars: Velocci P-One (240 mph, 2.4 s), Toro Furioso (217, 2.9), Rosso Novanta (211, 2.5), Ventara Aria (230, 3.0), Toro Uragano (202, 3.2) and Vitesse Centaur (261, 2.3).
  - Sports Cars: Stoccarda 9 Turbo (205, 2.7), Ingolf R-Ten (205, 3.1) and Kentucky Eight (194, 2.9).
  - SUVs: Gelände G63 (137, 4.5), Spirit Royale (155, 4.8) and Highland Sport (150, 5.0).
  - Two new body shapes go with them: a luxury SUV and a rear-engined sports car. The Notte GT and the Desert G are in Custom.
- **They drive to those numbers.** The game's engine does the driving. When it pulls slower than the car should, Frankenstein makes up the difference, and it never lets a car go past its own top speed. drive-debug.txt says what it measured.
- **The Vehicle Spawner from your sheet** (F3, the controller wheel, or the button in F6's Vehicles tab). Categories run down the left: Supercars, Sports Cars, SUVs, Trucks, Motorcycles and Custom. There's a grid of colour thumbnails, the car's top speed and 0-60, and a Spawn button. Schedule I has no motorbikes to build on, and Motorcycles says so.
- **Driving HUD:** a speedometer (MPH) bottom right and a round minimap that turns with the car bottom left, on any car.
- **The car prompt now reads like the sheet:** "X / E to Enter Vehicle".
- Honest limits: the bodies are still Frankenstein's own code-built shapes until lookalike models can be fetched (see the card about the model sites), and they won't be photoreal in Schedule I's style. A walk-in garage showroom isn't built yet: the spawner brings any car to you instead.
- Not tested in game yet.

### Also in 3.43.1

- **Getting to the city island:** a "Go to the city island" button at the top of F6, "City island" on the controller wheel, and a "Go to the island" button on the F4 map.
- **Fixed:** gang names and HQ labels in the world no longer draw over F6, the map or other menus.
- **The island moves past Schedule I's hills and mountains** if they rise where it would go (the town's terrain wasn't counted before, so the island could end up buried). Then it lays its own sea there.
- **island-debug.txt** (in the merge's UserData\Frankenstein folder) says where the island was built, the town's measured size, the terrain it checked, and what was built.
- Not tested in game yet.

### Also in 3.43.0

- **A whole new city island, laid out from your map (experimental).** It sits off the east coast of Schedule I's town, about 1.3 by 1 km, and a highway bridge crosses the water to it.
  - **Districts:** Downtown with its towers, Uptown Hills with mansions, Lake Park, the Suburbs, the Airport (runway, terminal, control tower, planes), Shopping & Nightlife with neon, the Construction Zone with cranes, the Trailer Park, the Farms (fields, barn, silo), and the Port and South Docks (containers, gantry cranes, piers and ships).
  - **Every gang HQ is where your map has it:** London, Mafia, Crips, Bloods and Yakuza, plus the East London Firm in the Trailer Park. Each HQ is a walled compound in the gang's colours with a glowing crown sign, and the gang's name shows over it when you're near.
  - **Two new gangs:** the Cartel (Cartel Point, with its tanks) and the Outlaw MC (up on the bluff by the lighthouse). Each has six ranks, its own prices and guns, and four named crew members who hold their HQ. Their districts are real turf in F9.
  - **F4 is the city map** (also on the controller wheel and in F8). It's your map with you on it. Click a district, or pick one with the d-pad and press A, to go there.
  - **No lag:** the island is built a few milliseconds a frame, each district's pieces are batched together, and districts far from you switch off. They switch off sooner when Frankenstein turns its extras down. Fall in the water and you're put back on the nearest road.
  - Honest limits: Schedule I has no skyscraper models, so the towers are blocks with lit windows. The buildings copied from town are shells with no insides. The game's townspeople have no walking paths on the island, so only the gangs' crews are there. It replaces the old row of districts east of town.
- Not tested in game yet.

### Also in 3.42.2

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
