## Frankenstein 3.28.1

- **Fixed: Frankenstein could get stuck on the startup screen** with an empty loading bar, blank boxes and a dark brain. The window was running an older copy of the app's script next to the new page. Every start now loads this version's own files fresh, and a mixed-up install is caught, reported and repaired from Settings.
- **The startup screen can never keep you out.** A Skip button appears after a few seconds (Esc or a click also works), it closes by itself after 30 seconds, and a script error is shown and saved to a log instead of freezing.
- **Random games over the palms:** two random games from your library spin in the boxes, with their real covers once they load.
- The dot brain draws again on the startup screen, and slow steps (accounts, scanning, covers) no longer hold up the app.
