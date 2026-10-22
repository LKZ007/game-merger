## Frankenstein 3.27.0

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

Already installed? Press Check for updates in the sidebar.
