# Changelog

Every release, newest first. Downloads are on the [releases page](https://github.com/LKZ007/game-merger/releases).
The app was called Game Merger before 3.8.0.

## v3.54.3 (2026-10-28)
- **Clear message for Rockstar error 7002.1 ("No compatible sound playback device").** GTA V won't start when Windows has no sound output switched on. Play now checks this first and tells you how to pick one, instead of letting the Rockstar launcher fail.

## v3.54.2 (2026-10-28)
- **Script Hook V in one click.** When you press Play and Script Hook V is missing, its page opens in your browser. Click Download there and that's it: as soon as the file lands in Downloads, Frankenstein installs it and starts GTA V by itself. Script Hook V's site only lets a person download it, so this one click can't be skipped.

## v3.54.1 (2026-10-28)
- **Fixed: "Script Hook V isn't installed yet ... Couldn't find the download link".** Script Hook V's site only lets a person download it in a browser, so Frankenstein can't fetch it by itself. Play now opens its page in your browser for you. Click Download there and press Play again. Frankenstein finds the file in Downloads (any name, like "ScriptHookV_3889.0 (1).zip", or a folder you already unzipped), installs it into the merge and starts GTA V.

## v3.54.0 (2026-10-28)
- **No more North Yankton start mission.** If GTA V starts the prologue (the bank job in the snow), the empire stops it straight away and drops you into free roam as Franklin outside the Strawberry stash house, with the empire running. Untested in game yet.
- **Play checks the script hooks first.** GTA V only runs Frankenstein's scripts through Script Hook V and Script Hook V .NET. If either didn't come in when the merge was made, Play gets it now. If Script Hook V's site won't hand over the file, put the ScriptHookV zip from dev-c.com in your Downloads folder and Frankenstein picks it up. It no longer starts GTA V without them and says nothing.
- **The merge card says whether the empire ran.** After you close GTA V, Frankenstein reads Script Hook V's, Script Hook V .NET's and the empire's own logs and says where it stopped if it didn't load (hook missing, GTA V updated past Script Hook V, .NET hook not running, or GTA V Enhanced).

## v3.53.1 (2026-10-27)
- **Fixed: GTA V said "Please run Grand Theft Auto V using Rockstar Games Launcher" (ERR_NO_LAUNCHER).** Play now starts GTA V through its own launcher (PlayGTAV.exe), still offline in Story mode, and the mods still come back out of your GTA V folder when the game closes.

## v3.53.0 (2026-10-27)
- **GTA V x Schedule I x Hood Warfare comes ready made.** Once Frankenstein finds GTA V, it makes the merge by itself and puts it in My merges, ready to play.
  - **It plays in your own GTA V, with no 100 GB copy.** The mods (Script Hook V, Script Hook V .NET and Frankenstein's scripts) go into your GTA V folder only while you play from Frankenstein. When the game closes, they come back out and every file they covered is put back, so your folder is exactly as before. Normal GTA V and GTA Online never see them. If Frankenstein closes while the game runs, it cleans up the next time it starts. Your Story saves are backed up first.
  - **Straight into Story mode, offline.** The Rockstar intro movie is skipped and the game starts offline, so it never goes near GTA Online.
- **Fixed: picking Schedule I next to GTA V made Schedule I the Host.** Clicking another game's tile now always picks it as the Extension. Only Make host changes the Host.
- **The city from the concept map.** The districts on the map are laid over Los Santos and Blaine County, and each appears on GTA V's map once you discover it: Downtown, Shopping & Nightlife, Uptown Hills, Lake Park, the Suburbs, the Construction Zone, Port, Airport, the Industrial District, Trailer Park, Farms and the Countryside. Every gang's HQ is marked: Crips HQ (the Families), Bloods HQ (the Ballas), Yakuza HQ (Kkangpae), Outlaw HQ (the Lost MC), and two new crews, the Mafia and the London Firm. Cartel HQ is La Sombra's ranch.
- **Houses and hidden places.** Four houses to buy that pay rent every hour, with a bed and a raid-proof stash. Eight hidden places, each with a locked cache to pick open.
- **The supercar sheet.** Premium Deluxe Motorsport sells 30 of GTA V's own cars, chosen as the closest lookalikes of the sheet and painted in its colours: the T20 (P1-style), Zentorno (Aventador-style), Itali GTB (Ferrari-style), Osiris (Huayra-style), Tempesta (Huracán-style), Thrax (Chiron-style), Comet (911-style), 9F (R8-style), Coquette D10 (C8-style), Dubsta (G-Wagon-style), Jubilee (Cullinan-style) and Baller ST (Range Rover-style), plus luxury saloons, muscle cars, lowriders, vans, trucks, bikes and armoured cars. Each shows its top speed and 0-60 time. You can customise the car you're in (performance, wheels, spoiler, bumpers, skirts, exhaust, bonnet, paint, tint, violet neon) and repair it. There's a speedometer while you drive.
- **Gangs fight smarter.** Gang crews take cover and push forward, and backup arrives in their own cars. Gangs at war with you jump your dealers.
- **More survival.** A body temperature bar: cold nights, rain, snow, water and mountains chill you, the desert sun overheats you, and cars, buildings and campfires warm you up. Search dumpsters for scrap, cloth and food, and hunt animals for meat. Craft bandages, lockpicks and campfires, and cook on the campfire (F5 > Crafting).
- **More story.** Three new objectives before Kingpin: buy a house, buy a car at the dealership, and find three hidden places. Existing saves move along. There's also a new cutscene for your first house.
- **Ragdoll:** punches and melee hits now knock loose props flying too.
- Not yet tested in the game: map positions (houses, HQs, hidden places), balance and car stats may need tuning.

## v3.52.0 (2026-10-27)
- **The GTA V Schedule I x Hood Warfare merge, built out.** Existing empires carry over (your story progress moves to the matching step of the longer story).
  - **Mixing.** At a property, add up to three ingredients to weed, meth or coke (cucumber, banana, energy drink, paracetamol, motor oil, chili, battery, glow sticks, gasoline). Each gives an effect that raises the price, changes how hooked people get, and costs a bit more per batch.
  - **Customers and addiction.** 14 named customers across five areas that open as you rank up. Give one a free sample; once hooked, they text orders every in-game hour, bigger and better paid the more hooked they are. F5 > Customers.
  - **Specialist workers.** Besides the botanist, chemist or gunsmith who runs a place, hire a handler (25% more product) and a cleaner (half the heat, and raids skip that property).
  - **A longer Story.** 18 objectives with Uncle Ray phoning in each one, from your first sample, resting and mixing up to your crew, La Sombra, guns, coke and Kingpin.
  - **La Sombra's cartel.** A meeting at the Sandy Shores airfield: pay 15% and they keep the gangs off your turf, or refuse and fight them (ambushes, three stashes, then their boss at a ranch).
  - **Guns.** The La Mesa gun workshop makes crates you sell to GTA V's gangs, and F5 > Arsenal sells weapon kits by rank, plus body armour.
  - **Your crew.** Hire crew, gear them up (Street to Elite), call them to fight beside you, or leave them guarding your turf.
  - **Vehicles.** F5 > Vehicles: 13 GTA V rides to buy, call to you, upgrade and paint.
  - **Survival.** Hunger, thirst and energy in Story, with vending machines and food stands, snacks from your properties, and sleeping at a property.
  - **Gang Beasts-style ragdoll.** J (double-tap D-pad down) flops you, punches and melee hits send people flying, and sprinting into people knocks them over. Shift+J turns launches off.
  - **New cutscenes** for your first customer, La Sombra's meeting, your first stash taken, your first gun crates and beating La Sombra. Creative has test tools for all of it.
  - Not yet tested in the game: positions, balance and cutscene cameras may need tuning.

## v3.51.0 (2026-10-27)
- **GTA V from the Rockstar Games Launcher shows up now.** Frankenstein finds games installed through the Rockstar Games Launcher (GTA V, GTA V Enhanced and others) and through Ubisoft Connect, as well as Steam, Epic, GOG and the Xbox app. It checks the launchers' own records, Windows' Apps list, and the usual install folders on every drive. Settings > Your game accounts lists what it found from each one. A game it still can't see can be added with Library > Add game folder.
- **Fixed "certificate verify failed" when checking for updates or downloading.** Frankenstein now checks secure connections with Windows' own certificate store, the same way your browser does, so antivirus HTTPS scanning no longer blocks it. Check for updates also retries, falls back to GitHub's releases page and then to Windows' own curl, and if it still can't get through, it says why and offers a button to download the newest version.

## v3.50.0 (2026-10-23)
- **Cutscenes in the GTA V Schedule I x Hood Warfare Story.** Story's big moments now play as short cutscenes, filmed with GTA V's own cameras, letterboxed, with your Uncle Ray on the phone: the start in Strawberry, every rank-up, every property you buy, your first meth and first coke, your first turf war won and lost, the first police raid, and Kingpin, with a sweep over the city. Each one plays once per save, and only when you're safe (no wanted level, no turf war). Enter, Space or A skips one. In Creative, the F5 test tools can replay any of them.

## v3.49.0 (2026-10-23)
- **The GTA V Schedule I x Hood Warfare merge, closer to the concept art.**
  - **The empire map in game.** GTA V's own map now shows each of your four properties with its icon (stash house, weed farm, meth lab, warehouse), the rank it needs and its price. Yours are violet, the ones for sale are white and the locked ones are grey. Each gang's turf shows as a coloured ring (Families green, Ballas purple, Vagos yellow, Aztecas light blue, Marabunta blue, Lost MC white, Kkangpae red), and a ring turns violet when the turf is yours.
  - **The film look.** One of GTA V's own film filters goes over the game: strong on Cinematic, lighter on High and Balanced, and off on Performance (Settings > Graphics). F5 > Look picks another filter or GTA V's own look, and can set golden hour or night for screenshots and testing.

## v3.48.0 (2026-10-23)
- **Story and Creative in the GTA V Schedule I x Hood Warfare merge.**
  - **Story plays like Schedule I.** You start with a small stash house in Strawberry and a few supplies. Batches, sales, turf wars and objectives give XP, and you climb ten ranks from Street Rat to Kingpin. The ranks unlock the Grapeseed farm, meth and the Sandy Shores lab, workers, dealers, turf wars, truces, and finally coke and the Elysian Island warehouse. The current objective, your rank and an XP bar are on screen, with a cash reward and a waypoint for each step. Locked properties only show on the map once they're for sale.
  - **Creative is for testing.** Everything is owned and unlocked, money and supplies are unlimited, batches take 30 seconds, and heat is off unless you turn it on. The F5 menu has test tools: +50 product, finish batches, heat and raid tests, start or win a turf war, take or clear turf, and teleports to every property.
  - **Each mode keeps its own saves and its own empire.** Switch in game from the F5 menu (it works on a controller) or with Shift+F10.

## v3.47.1 (2026-10-23)
- **Delete works on older merge copies.** Copies made by older versions of Frankenstein (like the Elden Ring x Hogwarts Legacy one) didn't carry its marker, so Delete left them alone. Now Delete and Delete all merges ask "Delete anyway?" for those, since they're listed in Frankenstein's own records as merge copies. Even then, Frankenstein refuses any folder that is, or holds, an installed game or a merge's original, a top-level folder, a link, or anything inside Steam's or Windows' folders. Your installed games and their real saves are never touched.

## v3.47.0 (2026-10-23)
- **A new merge with GTA V as the Host: Schedule I x Hood Warfare in Los Santos (milestone 1).** Make it from a GTA V merge by picking "Schedule I x Hood Warfare: business, gangs and guns" as the Extension. Story mode only, offline, on Frankenstein's own copy of the game.
  - **Labs, farms and production.** Buy four properties (Grapeseed farm, Sandy Shores lab, Strawberry grow house, Docks lab), buy supplies, start a batch of weed, meth or coke and collect it when it's ready. Hire a worker and the lab runs by itself.
  - **Street dealing.** Walk up to someone and press E (D-pad right) to offer product. Prices are better on your own turf, and some buyers are undercover cops. Dealers you hire sell your stock every in-game hour.
  - **Gangs and turf wars.** GTA V's own gangs hold their real neighbourhoods. Hold E in a gang's zone to start a four-minute turf war; win it and the zone is yours and pays every hour. Gangs try to take turf back, or you can buy a truce.
  - **Heat and raids.** Dealing and fighting raise heat; high heat brings police raids on your labs.
  - **Empire menu and HUD.** F5 (or hold D-pad left on a controller) opens the empire menu with waypoints to every lab. The HUD shows the territory you're in, who controls it, your product and your heat. Saved to scripts/Frankenstein/empire.txt, logged to empire-debug.txt.
- **Delete all merges** in Settings: removes every merge's game copy, its packed files and its own saves in one go, and says how much space it freed. Your installed games and their own saves are never touched.

## v3.46.1 (2026-10-23)
- **The free 3D model packs now download by themselves when you press Play** on the Schedule I merge, the first time, with the download shown in a window and in the merge terminal. No button needed. If a pack can't download, the game still starts, and Play tries again the next time you open the app. The button is still in Settings > Graphics and models, under Performance.

## v3.46.0 (2026-10-23)
- **The Schedule I x Hood Warfare restart, milestone 1: the foundation.** The old work is kept on its own branch and in release 3.45.1.
  - **Real 3D models in the game.** Frankenstein's mod now reads standard .glb and .gltf models (meshes, textures, colours, glowing lights) and puts them in the game. Models are read in the background, so the game doesn't stall.
  - **Free model packs, downloaded by your PC.** The app gets Kenney's CC0 car and city packs from kenney.nl the first time you press Play, or from Settings > Graphics and models. It keeps them on your PC and puts them into the merge with a licence list. You can also drop your own .glb files into the merge's UserData\Frankenstein\Models\Mine folder.
  - **Cars wear real models.** The F3 cars (and any you spawn from the sheet) wear a 3D car model fitted to the game's car. The model's own wheels turn, and the headlights and tail lights really light up. Until the packs are on your PC, a car keeps the game's own body, painted.
  - **The look, towards the concept art.** There's a new Graphics setting: Cinematic, High (the default), Balanced or Performance. It adds bloom, cinematic tone mapping, warm and teal colour grading, a vignette, longer shadows and sharper far detail. A golden-hour grade follows the game's clock at sunset and a blue grade at night. If the game drops under 40 FPS, the look steps down.
  - **A test city block** (F8 > Test city block: build here) built from real city models with collision. It streams out when you're far away and shows simpler buildings at a distance. It also checks whether the game's NPCs could walk there. It stays with your save.
  - **selftest.txt** in UserData\Frankenstein says what loaded: models, graphics, FPS and the test block.
- **Retired in the restart:** the code-built city island, Hood City, the F4 island map, the island gangs (Cartel, Outlaw MC) and the code-built car bodies. Guns, gangs, Creative, the menus and the controller stay until their milestones rebuild them.
- Honest limits: Kenney's models are clean low-poly ones, not the photoreal cars from your sheet. Those need licensed models, and they come in milestone 2. Schedule I's own characters and terrain keep their look. Not tested in game yet.

## v3.45.1 (2026-10-23)
- **Fixed: Play on a packed merge stopped with "...is in the way and isn't a Frankenstein copy".** An earlier rebuild that was cut off left a half-made copy behind without Frankenstein's marker, so the next Play didn't recognise it. Now Frankenstein marks a rebuild as its own before it starts copying. When it finds a leftover with only the game's files and the merge's own files in it, it clears that leftover and rebuilds. If the folder has anything else in it, the folder is moved aside (renamed, nothing deleted) and the copy is rebuilt. The installed game is still only read.
- **Fixed: your own guns registering (the AK-47 "Ambiguous match found" error).** The game has two members whose names differ only in case ("ID" and "id"), and Frankenstein's lookup tripped over that, so every gun fell back to its base gun. The lookup now picks the exact name and the nearest type. Singletons are found the same safe way everywhere.
- Not tested in game yet: please press Play, open F6 and spawn a gun. If one still comes out as a base gun, send UserData\Frankenstein\ownitems-debug.txt.

## v3.45.0 (2026-10-23)
- **Hood City: your map becomes the town itself (pick it per save).** On the main menu, the "World" entry switches between Hood City and Schedule I's original town for the save you load, and F8 has a World button to switch a save you're already in (your save is backed up first). The choice is kept in the save's Modded\Frankenstein\world.txt.
  - In Hood City, your map is laid over the town instead of off the coast. Every district, road and gang HQ goes where the picture has it.
  - **The original town's real places stay and keep working.** Shops, properties, enterable buildings and their doors are kept where they stand, and the new districts are built around them, so the spots that would have been empty shells are the town's own working buildings.
  - The town's scenery that would sit under the new districts is hidden while you play Hood City and comes back when you switch to the original town.
- Honest limits: the town's own ground, roads and hills stay underneath, so the layout is as close to your map as the land allows. New buildings are still Frankenstein's code-built shells with no insides, and the town's shops can't be moved, so they keep their own spots inside the new layout.
- Not tested in game yet.

## v3.44.0 (2026-10-23)
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

## v3.43.1 (2026-10-23)
- **Getting to the city island:** a "Go to the city island" button at the top of F6, "City island" on the controller wheel, and a "Go to the island" button on the F4 map.
- **Fixed:** gang names and HQ labels in the world no longer draw over F6, the map or other menus.
- **The island moves past Schedule I's hills and mountains** if they rise where it would go (the town's terrain wasn't counted before, so the island could end up buried). Then it lays its own sea there.
- **island-debug.txt** (in the merge's UserData\Frankenstein folder) says where the island was built, the town's measured size, the terrain it checked, and what was built.
- Not tested in game yet.

## v3.43.0 (2026-10-23)
- **A whole new city island, laid out from your map (experimental).** It sits off the east coast of Schedule I's town, about 1.3 by 1 km, and a highway bridge crosses the water to it.
  - **Districts:** Downtown with its towers, Uptown Hills with mansions, Lake Park, the Suburbs, the Airport (runway, terminal, control tower, planes), Shopping & Nightlife with neon, the Construction Zone with cranes, the Trailer Park, the Farms (fields, barn, silo), and the Port and South Docks (containers, gantry cranes, piers and ships).
  - **Every gang HQ is where your map has it:** London, Mafia, Crips, Bloods and Yakuza, plus the East London Firm in the Trailer Park. Each HQ is a walled compound in the gang's colours with a glowing crown sign, and the gang's name shows over it when you're near.
  - **Two new gangs:** the Cartel (Cartel Point, with its tanks) and the Outlaw MC (up on the bluff by the lighthouse). Each has six ranks, its own prices and guns, and four named crew members who hold their HQ. Their districts are real turf in F9.
  - **F4 is the city map** (also on the controller wheel and in F8). It's your map with you on it. Click a district, or pick one with the d-pad and press A, to go there.
  - **No lag:** the island is built a few milliseconds a frame, each district's pieces are batched together, and districts far from you switch off. They switch off sooner when Frankenstein turns its extras down. Fall in the water and you're put back on the nearest road.
  - Honest limits: Schedule I has no skyscraper models, so the towers are blocks with lit windows. The buildings copied from town are shells with no insides. The game's townspeople have no walking paths on the island, so only the gangs' crews are there. It replaces the old row of districts east of town.
- Not tested in game yet.

## v3.42.2 (2026-10-23)
- **Merged games shouldn't lag. "No lag" is now part of the minimum standard for every merge.**
  - **A budget for Frankenstein's own Schedule I mod:** everything it does in a frame (its updates, menus and HUD) should take under 2 ms. It times itself, and the player's inventory is now found once and kept instead of being searched for several times a second.
  - **An in-game FPS check.** When the game drops under 40 FPS, or the mod goes over its budget, for a few seconds, Frankenstein turns its own extras down by itself. The crews, gangs, car and gun checks run less often, far-off crew members think less, and the launcher arc uses fewer dots. You get a short message when this happens. When the game has run smoothly for a while, the extras go back up. perf-debug.txt records the FPS and the mod's own cost every 30 seconds.
  - **Settings > Performance:** Automatic (recommended), Always full, Lighter, or Lightest for slower PCs.
  - **The app steps back while a game runs.** Its animations and videos stop, it drops to below-normal priority, and the merge terminal closes the moment a game starts (it never opens over one).
  - Honest limit: this only lightens Frankenstein's own work. If the game itself is heavy on your PC, its own graphics settings help more. The merge card's checklist shows how far each Host game gets.
- Not tested in game yet.

## v3.42.1 (2026-10-23)
- **Creative is never in the app.** The "✦ Creative" button is gone from every merge card, and a test keeps it gone. Creative lives inside each game.
- **Elden Ring: Creative is in the game on every Play.** Press both sticks for the Creative wheel, or 0 for its menu, on any character. It has the item spawner, fly, Torrent as a broom, no damage and unlimited spells. Elden Ring's own menus are locked in its archives, so there's no Creative entry in them, and the mode isn't remembered per character. The merge card says so.
- Not tested in game yet.

## v3.42.0 (2026-10-23)
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

## v3.41.1 (2026-10-23)
- **Merge terminal.** While a merge is made, filled in, upgraded or unpacked, a small terminal window opens beside Frankenstein and shows the game being rebuilt live: each file copied or linked with its size, each mod installed and each part filled.
  - It's black and Frankenstein purple, with a progress bar, and closes itself a few seconds after the merge finishes.
  - The usual merge dialog stays as it is.

## v3.41.0 (2026-10-23)
- **Your Creative character.** When the game's character creator opens in a new Creative world, Frankenstein asks first:
  - **Quick start:** skips character creation and uses your latest story character's look, or the game's default if you have none.
  - **Use another story character:** one option for each story save that has a character. Its look is copied, and that save is only read, never changed.
  - **Make a new character:** opens the game's own creator.
  - Works with mouse, keyboard or controller (d-pad and A). What happened is written to `UserData\Frankenstein\character-debug.txt`.
- **"Creative character" is now part of the minimum standard,** so it's checked on every merge card.
- Not tested in game yet.

## v3.40.2 (2026-10-23)
- **Dropping, and anything that needs to know what's in your hand, now reads your hand properly.** Before, the mod couldn't tell which hotbar slot you were holding, so drop said there was nothing in your hand.
  - The same problem also kept the own guns' models, crosshairs and the launcher's grenades from switching on.
  - Frankenstein now tries several ways to read the held slot: the inventory's own index, its equipped slot, then each slot. The way that worked is written to `drop-debug.txt`.
- Not tested in game yet.

## v3.40.1 (2026-10-23)
- **Own guns: a fuller check of why they still spawn as the base gun.** On 3.38 the sniper, AK, Uzi, RPG and launcher still came out as the revolver, M1911 and shotgun. That means the game didn't take Frankenstein's new items.
  - Frankenstein now finds the game's item registry more reliably and writes every step to `UserData\Frankenstein\ownitems-debug.txt`, including where it stopped.
  - Each time you take one of these guns from F6, it also writes what actually landed in your hotbar (item, name, icon).
- Not tested in game yet.

## v3.40.0 (2026-10-23)
- **Every merge is now checked against one minimum standard: everything the Schedule I merge has.** That means the main menu, loading screen, Profiles, in-game Creative, the item spawner, own weapons with real stats, a crosshair per weapon, own cars, gangs and NPCs, the controller, Save Game and debug logs.
  - Each merge card has a **Standard x/12** tag. Click it to see the checklist, with an honest note on each line: built by Frankenstein, done by the game itself, partly there, still to come, or not possible in that game.
  - Frankenstein installs and updates its own mod for each game by itself, so the lines it can build need nothing from you.
  - **Schedule I:** meets all 12.
  - **GTA V and Cyberpunk:** some lines are still partly there or to come, like own weapon models, own cars and gangs. GTA V's title screen can't be modded.
  - **Other games:** they get Frankenstein's own mod later. Until then the checklist says which lines are still to come.

## v3.39.0 (2026-10-23)
- **Save Game in the pause menu.** It sits under the game's own list. Click it, press Y on a controller or Ctrl+S. The game saves the same way it does when you sleep, and "Game saved" shows when it's done.
- **The Creative hint bar isn't cut off any more.** It's sized to fit its text, and it's hidden while the pause menu is open.
- **The base car's body is now really hidden under the new cars.** A name check treated parts like "Trim" and "Primary" as wheel rims, so they stayed visible. The game's own list of body panels is now hidden too, and kept hidden if the game shows it again. The G-wagon's roof rails now sit on the roof. What was hidden and kept for each car is written to `UserData\Frankenstein\garage-debug.txt`.
- More realistic models for all five cars are coming next.
- Not tested in game yet.

## v3.38.0 (2026-10-23)
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

## v3.37.1 (2026-10-23)
- **Dropping works for weapons too.** Press G or d-pad down to drop what's in your hand. With the inventory open, LB/RB pick a slot first. Frankenstein tries the game's own drop and checks the item really left your hand. The game doesn't let some things (like guns) be dropped, so for those the slot is just emptied: get them back from F6 any time. What happened is written to `UserData\Frankenstein\drop-debug.txt`.
- **A new Creative world starts empty.** No starter pistol, mags or shells: your hotbar is cleared once when the world starts, and the merge's starter kit is only given in Survival.
- Not tested in game yet.

## v3.37.0 (2026-10-23)
- **The new cars are fully remodelled.** Each now has a whole new body of its own instead of the base car with parts stuck on: the side profile, hood, cabin, roof, tail, wheel arches cut around the wheels, tinted glass and a painted roof.
  - **Velocci 720:** low and curved, with a teardrop cabin, fenders that rise over the wheels and a small ducktail.
  - **Toro Furioso:** a sharp wedge with a flat roof and a long windshield.
  - **Notte GT:** a long hood, the cabin set back and a fastback.
  - **Gelände G63 and G Desert:** the upright box: flat roof, near-vertical windshield, round headlights, grille bars, spare wheel, step bars, roof rails and a light bar.
  - The base car's body is hidden and its wheels, seats and driving stay, so they drive just like before. The floating spoiler parts are gone.
- The car prompt shows the whole name now (it was cut off at "Get in the").
- Not tested in game yet.

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
