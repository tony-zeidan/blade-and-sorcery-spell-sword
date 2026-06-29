# Spell Sword — Blade & Sorcery mod

**Click the imbue/spell button** while gripping an eligible item → it fires a **clone** of that
item, fast and straight (no spin):

- **Swords / weapons** fly out along the **blade tip** (toward whatever the tip points at).
- **Shields** fly out **perpendicular to their face** (the direction they defend).

The clone carries an imbue if available: the held item's **active blade imbue**, or failing
that the **spell currently selected on that hand** (so a non-glowing sword with a spell
equipped still throws an imbued clone; no spell → plain clone). Clones are flagged thrown so
they penetrate, and travel at a fixed speed regardless of weight. Up to **30** clones persist
at once — beyond that, the oldest despawn first.

By default the mod only responds to one sword (`targetSwordId`, the short sword
`SwordShortCommon`); change the scope in the in-game menu. No custom assets / AssetBundle are
needed — the clone is a duplicate of your own item.

Built for **Blade & Sorcery PCVR, game version 1.0.0.0** (ThunderRoad framework).

---

## How it works (the short version)

The whole mod is a single C# class, [`src/SpellSwordScript.cs`](src/SpellSwordScript.cs), that
derives from `ThunderRoad.ThunderScript`. ThunderRoad automatically discovers and runs
`ThunderScript` classes in any loaded mod, so there is **no custom item, prefab, or
AssetBundle** — just a DLL plus a `manifest.json`.

Every frame it:
1. Watches each hand's imbue/spell button for a fresh **click** (rising edge of
   `playerHand.controlHand.castPressed`).
2. On a click, finds the gripped item (`ragdollHand.grabbedHandle.item`) and checks it's
   eligible (per the menu scope).
3. Picks a launch direction: `item.flyDirRef.forward` (tip) for weapons, or the shield's
   thinnest-collider-axis normal for shields. Picks an imbue: `item.imbues[…].spellCastBase`
   if the blade is imbued, else `hand.caster.spellInstance` (the hand's selected spell).
4. Spawns a clone (`item.data.SpawnAsync`), flags it thrown (`item.Throw`), zeroes its drag,
   sets a straight velocity (zero angular velocity), copies the imbue (`imbue.Transfer`), and
   plays a whoosh.
5. Tracks live clones and despawns the oldest once more than `maxActiveClones` (30) exist.

---

## Prerequisites (one-time setup)

You're already set up — here's what's used and why.

1. **The game** — installed at:
   `S:\game_installations\SteamLibrary\steamapps\common\Blade & Sorcery`
   (If you move it, update the one `GameManaged` line in `SpellSword.csproj` **and** the
   `modsRoot` line in `build.ps1`.)

2. **Visual Studio 2019** — already installed. The project is a classic **.NET Framework
   4.7.1** library, so VS 2019's built-in MSBuild compiles it with **no extra .NET SDK**.
   `build.ps1` finds MSBuild automatically (via `vswhere`), so you can build from the command
   line *or* open `SpellSword.csproj` in the VS IDE — whichever you prefer. The IDE gives you
   IntelliSense and red-squiggle error checking, which is handy while you learn the API.

You do **not** need Unity or the Blade & Sorcery SDK for this version of the mod, because we
reuse the game's existing sword prefabs as the projectiles.

---

## Build & install

From this folder, in PowerShell:

```powershell
./build.ps1
```

This compiles the DLL and copies it together with `manifest.json` into:
`...\Blade & Sorcery\BladeAndSorcery_Data\StreamingAssets\Mods\SpellSword\`

Then **restart the game**. In a sandbox level, grab a sword, hold the spell button, and
thrust or slash.

To build without installing: `./build.ps1 -NoDeploy`.

---

## In-game options menu

Once the mod is loaded, open **Options → Mods** (in the book) to change these without
rebuilding:

- **Spell Sword Enabled** — master on/off.
- **Active On** — which held items respond to the gestures:
  *Short sword only · All swords · All daggers · Swords & daggers · All weapons ·
  All weapons & tools · Any held item*. (Swords/daggers are told apart by the game's item
  `category`; weapons/tools by item `type`.)

These are driven by `[ModOption]` attributes in `SpellSwordScript.cs`. Note the required
shape (this is what makes them load — a missing piece will hang the game at *Applying
Options*): every `[ModOption]` needs **(1)** an interaction attribute (`[ModOptionButton]`),
and **(2)** a `valueSourceName` pointing at a `public static` array of value wrappers
(`ModOptionBool[]` / `ModOptionInt[]`). The field is set to the picked entry's value.

## Tuning

The rest of the feel knobs are `public static` fields at the top of `SpellSwordScript.cs`:

| Field                    | Meaning                                                  | Default |
|--------------------------|----------------------------------------------------------|---------|
| `targetSwordId`          | Item id for the "Short sword only" scope                 | SwordShortCommon |
| `cloneSpeed`             | Launch speed of the clone (m/s)                          | 40      |
| `spawnForwardOffset`     | How far past the tip / shield face the clone spawns (m)  | 0.3     |
| `maxActiveClones`        | Live clones before the oldest despawn (FIFO)             | 30      |
| `thrownWhooshIntensity`  | Volume (0..1) of the clone whoosh sound                  | 1.0     |

The in-game **Active On** menu now also has an **All shields** option.

Change a value, re-run `./build.ps1`, restart the game.

---

## Troubleshooting

- **Nothing happens in game.** Check the player log for the `[SpellSword] ThunderScript
  loaded.` line:
  `...\Blade & Sorcery\BladeAndSorcery_Data\StreamingAssets\Logs` (or the Unity
  `Player.log`). If it's missing, the DLL didn't load — confirm the files are in the
  `Mods\SpellSword\` folder and the `GameVersion` in `manifest.json` matches the game.
- **The clone flies sideways, not tip-first.** It launches along `item.flyDirRef.forward`. If a
  particular weapon's fly reference is oriented oddly, that axis may be off — tell me and we can
  switch to a different reference (e.g. a blade damager transform).
- **Clone has no imbue even though my blade glowed.** The imbue is read at the instant you
  click; make sure the blade actually has imbue energy (`imbue.energy > 0`) when you click.
- **A build error after editing code.** The project compiles cleanly as-is against your
  game's `ThunderRoad.dll`; if you change an API call and it errors, open the project in the
  VS IDE and let IntelliSense suggest the correct member name.

---

## Roadmap / upgrade path

1. **Done** — clone fired on imbue-button click, straight along the blade tip, carrying the
   held sword's current imbue. Scope chosen from the in-game menu.
2. **Aim assist** — optionally raycast along the tip direction and steer the clone toward the
   exact creature/collider it's pointing at.
3. **Charge mechanic** — hold the button to scale clone speed/size by charge time.
4. **Cost** — drain a bit of the held sword's imbue energy per clone so it's not free.
