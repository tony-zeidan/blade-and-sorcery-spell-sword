# mod.io upload guide

Blade & Sorcery's **in-game mod browser** is powered by mod.io, so publishing here makes the
mod installable from inside the game. Unlike NexusMods, mod.io has a **public REST API**, so
uploads can be scripted later (see the bottom).

## The file to upload

```powershell
./build.ps1 -Package      # -> dist/SpellSword_v1.0.0.zip
```

Same drop-in zip as everywhere else: a `SpellSword/` folder (DLL + manifest).

## Option A — website (simplest)

1. Sign in at <https://mod.io> and open the **Blade & Sorcery** game page (or
   <https://bladeandsorcery.mod.io>).
2. **Add mod**, then fill in:

   | Field | Value |
   |---|---|
   | **Name** | `Spell Sword` |
   | **Summary** | paste `SUMMARY.txt` (≤ 250 chars) |
   | **Description** | paste `DESCRIPTION.md` (mod.io accepts Markdown) |
   | **Homepage** | `https://github.com/tony-zeidan/blade-and-sorcery-spell-sword` |
   | **Maturity** | None |
   | **Visibility** | Public |

3. **Logo/image** — required. A ready-made 1280×720 logo is included at
   [`assets/icon.png`](../assets/icon.png) — upload that, or swap in an in-game screenshot
   (16:9 works best for the tile).
4. **Tags** — pick the Blade & Sorcery tagging that fits: e.g. **U12 / 1.0**, **Weapon**,
   **Spell/Magic**, **Gameplay**. (Tag options are defined by the game on mod.io.)
5. **Upload file** — `dist/SpellSword_v1.0.0.zip`, version `1.0.0`. Mark it the active release.
6. Save / publish.

## Option B — API (scriptable, for later automation)

mod.io exposes a REST API that can create the mod and upload files without the website:

- Create an account + **API key** and an **OAuth2 access token** at
  <https://mod.io> → *Settings → API access*.
- Relevant endpoints (game id = Blade & Sorcery's mod.io game id):
  - `POST /games/{game-id}/mods` — create the mod profile (name, summary, description, logo).
  - `POST /games/{game-id}/mods/{mod-id}/files` — upload `SpellSword_v1.0.0.zip` as a modfile.
- Docs: <https://docs.mod.io/>

This is worth wiring into `build.ps1` (e.g. a `-PublishModio` switch) once the mod profile
exists and you've stored a token. Ask and I can add that script — keep the token in an
environment variable, never commit it.

## Keep versions in sync

`manifest.json` `ModVersion` == GitHub tag == Nexus version == mod.io file version.
Bumping: edit `manifest.json`, `./build.ps1 -Package`, upload the new zip, add a changelog note.
