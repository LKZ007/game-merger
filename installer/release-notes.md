## Frankenstein 3.25.0

- **Switch to Creative while you play, in every game.** Schedule I: press **F8** anywhere in town for Frankenstein's menu, then **Switch to Creative** (or **Back to my normal game**), then confirm. Cyberpunk: press **~**, then the This merge tab, then **Switch to Creative**, then confirm. GTA V: **Shift+F10**, then Shift+F10 again to confirm. Save first. The game closes and Frankenstein starts it again in Creative with its own saves, and the same way back, so normal and Creative saves never mix. Each game shows a hint for the first few seconds saying how.
- **Play together setup fixed.** Setup was meant to wait for ZeroTier to start, but in the hidden window the wait ended straight away, so it stopped with "Setup didn't finish". It now waits properly (up to 90 seconds), starts ZeroTier's service if it's stopped, and waits for its installer to finish. If it still fails, it says why in plain words (the Windows prompt was declined, another installer was running, the installer's error code, or the service didn't start) instead of showing a traceback. There's also a link to install ZeroTier yourself.

Already installed? Press Check for updates in the sidebar.
