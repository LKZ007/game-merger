## Frankenstein 3.17.0

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

Not tested in game yet: the Cyberpunk sandbox tabs, the Schedule I gun shop and the typing search. If a button doesn't work, it says so instead of breaking the game.

Already installed? Frankenstein will offer this update when you open the app.
