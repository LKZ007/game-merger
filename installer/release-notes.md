## Frankenstein 3.54.3

- **Clear message for Rockstar error 7002.1 ("No compatible sound playback device").** GTA V won't start when Windows has no sound output switched on. Play now checks this first and tells you how to pick one, instead of letting the Rockstar launcher fail.

### Also in 3.54.2

- **Script Hook V in one click.** When you press Play and Script Hook V is missing, its page opens in your browser. Click Download there and that's it: as soon as the file lands in Downloads, Frankenstein installs it and starts GTA V by itself. Script Hook V's site only lets a person download it, so this one click can't be skipped.

### Also in 3.54.1

- **Fixed: "Script Hook V isn't installed yet ... Couldn't find the download link".** Script Hook V's site only lets a person download it in a browser, so Frankenstein can't fetch it by itself. Play now opens its page in your browser for you. Click Download there and press Play again. Frankenstein finds the file in Downloads (any name, like "ScriptHookV_3889.0 (1).zip", or a folder you already unzipped), installs it into the merge and starts GTA V.

### Also in 3.54.0

- **No more North Yankton start mission.** If GTA V starts the prologue (the bank job in the snow), the empire stops it straight away and drops you into free roam as Franklin outside the Strawberry stash house, with the empire running. Untested in game yet.
- **Play checks the script hooks first.** GTA V only runs Frankenstein's scripts through Script Hook V and Script Hook V .NET. If either didn't come in when the merge was made, Play gets it now. If Script Hook V's site won't hand over the file, put the ScriptHookV zip from dev-c.com in your Downloads folder and Frankenstein picks it up. It no longer starts GTA V without them and says nothing.
- **The merge card says whether the empire ran.** After you close GTA V, Frankenstein reads Script Hook V's, Script Hook V .NET's and the empire's own logs and says where it stopped if it didn't load (hook missing, GTA V updated past Script Hook V, .NET hook not running, or GTA V Enhanced).
