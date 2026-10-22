## Frankenstein 3.30.0

- **Merges take up far less space.** After you close a merged game, Frankenstein packs it away:
  - It keeps only the files the merge changed or added (mods, their settings, anything the game wrote into its folder) in one zip.
  - It zips the merge's saves (normal and Creative).
  - Then it deletes the copy.
  - Your original game is only ever read, and nothing is deleted until the zip has been checked.
- **Play rebuilds it first.** Frankenstein makes a fresh full copy of your own installed game, puts the merge's files and saves back, then starts it. That takes about as long as copying the game: seconds for small games like Schedule I, several minutes for big ones like Cyberpunk. The card says how long, and after the first time it's the measured time.
- **On each merge card:** a 📦 Packed tag with the space saved, plus **Pack away to save space** and **Unpack now** under More. Adding mods, fixing crashes and the other buttons unpack first.
- **Settings > Save space** turns automatic packing off.
- If a Steam update changes the game, the rebuilt copy uses the updated game, with the merge's own files on top. If you uninstall the original game, a packed merge can't be rebuilt until you reinstall it.
