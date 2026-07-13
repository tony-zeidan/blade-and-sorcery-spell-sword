# NexusMods upload guide

NexusMods has no public publish API, so the page itself is created by hand on the website.
Everything you need to paste/select is below. Time: ~5 minutes.

## 0. The file to upload

Build the distributable zip:

```powershell
./build.ps1 -Package      # -> dist/SpellSword_v<version>.zip
```

That zip contains a `SpellSword/` folder (DLL + manifest) that a player drops into their Mods
folder. It's the same artifact attached to the GitHub release.

## 1. Start the upload

- Go to the **Blade & Sorcery** game page on NexusMods → **Add mod / Upload**.
  (Direct: https://www.nexusmods.com/bladeandsorcery/mods/add )
- You must be logged in to your Nexus account.

## 2. Form fields

| Field | Value |
|---|---|
| **Name** | `Spell Sword` |
| **Summary** | (paste from `SUMMARY.txt`) |
| **Description** | (paste the contents of `DESCRIPTION.bbcode` — the editor accepts BBCode) |
| **Version** | `1.0.0` |
| **Category** | `Weapons` (or `Gameplay` if you prefer) |
| **Mod is for game version** | `1.0.0.0` |

### Suggested tags
`Magic`, `Weapons`, `Spells`, `Gameplay`, `Throwing`, `Lore-Friendly`, `Fair and balanced`

## 3. Images (required)

Nexus requires at least one image, and the first image becomes the thumbnail/tile.

- You'll need to capture **in-game screenshots or a short clip** (a clone firing out, a flaming
  clone, a shield throw). Nexus accepts JPG/PNG; a 16:9 hero shot (e.g. 1920×1080) works best
  for the tile.
- Optional but great: a short GIF/MP4 of it in action — Nexus supports video.
- (This mod ships no bundled thumbnail; `manifest.json`'s `Thumbnail` is intentionally empty.)

## 4. Files tab

- Upload `dist/SpellSword_v<version>.zip` as the **Main file**.
- File name: `Spell Sword 1.0.0`, version `1.0.0`.
- Tick "this is the main/primary file."

## 5. Permissions / misc

- License: the project is MIT — set Nexus permissions to match (allow modification/reuse with
  credit), or pick the preset closest to MIT.
- Link the source in the description (already included) and optionally in the "Source" field:
  `https://github.com/tony-zeidan/blade-and-sorcery-spell-sword`

## 6. After publishing

- Bumping versions later: build a new zip (`./build.ps1 -Package`), upload it as a new file,
  and update the Version + a changelog entry. Keep `manifest.json`'s `ModVersion`, the GitHub
  tag, and the Nexus version in sync.
