# CLAUDE.md

Guidance for Claude Code (and humans) working in this repository.

## What this is

**Spell Sword** — a code mod for **Blade & Sorcery** (PCVR, Steam, game version **1.0.0.0**),
built on the **ThunderRoad** framework. Holding an eligible item and clicking the imbue/spell
button fires a clone of that item:

- swords/weapons fly out along the blade tip (`item.flyDirRef.forward`),
- shields fly out perpendicular to their face,
- the clone carries the held item's active imbue, or the hand's selected spell, if any.

The whole mod is one `ThunderRoad.ThunderScript` in [`src/SpellSwordScript.cs`](src/SpellSwordScript.cs).
It uses **only stock game assets** — no Unity project, prefab, or AssetBundle. See
[`README.md`](README.md) for the player-facing description and tuning table.

## Repo layout

```
src/SpellSwordScript.cs   The entire mod (a ThunderScript, auto-discovered by ThunderRoad)
SpellSword.csproj         Legacy .NET Framework 4.7.1 project (builds in VS 2019 MSBuild)
build.ps1                 Build (MSBuild via vswhere) + deploy into the game's Mods folder
ModFiles/manifest.json    Mod manifest the game reads
README.md                 Player/setup docs
CHANGELOG.md              Feature history
```

## Build & deploy

```powershell
./build.ps1            # build Release + copy DLL + manifest into the game
./build.ps1 -NoDeploy  # build only
```

- There is **no .NET SDK** on this machine (only the .NET 6 runtime). The project is therefore
  a **legacy (non-SDK) .NET Framework 4.7.1** csproj so **Visual Studio 2019's bundled MSBuild**
  can build it. `build.ps1` locates MSBuild via `vswhere`. Do **not** convert it to an SDK-style
  project or `dotnet build` — that needs an SDK we don't have.
- It references the game's own assemblies by `HintPath` (see `GameManaged` in the csproj),
  including `netstandard.dll` so net471 can consume the netstandard2.1 game DLLs.
- Mods load at game **startup**; a running game must be restarted to pick up a new build.

### Key machine paths
- Game: `S:\game_installations\SteamLibrary\steamapps\common\Blade & Sorcery`
- Managed DLLs: `…\BladeAndSorcery_Data\Managed`
- Mods folder (deploy target): `…\BladeAndSorcery_Data\StreamingAssets\Mods\SpellSword`
- Base-game catalog (item/effect/spell ids): `…\StreamingAssets\Default\bas.jsondb` — a **ZIP of
  JSON**. Item id = `Items/Item_*.json` filename minus the `Item_<Type>_` prefix; an item's
  `category`/`type`/`id` are inside the JSON.
- Launch the game with: `Start-Process "steam://rungameid/629730"`.

## How to discover ThunderRoad APIs (important)

This repo has no decompiler. To find/confirm API shapes, reflect `ThunderRoad.dll` from
Windows PowerShell:

1. `LoadFrom` every DLL in the Managed folder, then `GetTypes()` on `ThunderRoad`.
2. `GetTypes()` throws `ReflectionTypeLoadException`; unwrap it and keep the non-null
   `.Types` (≈3200 usable types).
3. **`Item` and `Creature` will NOT load** via reflection here (a default-interface-method /
   `ValueTask` limitation in .NET Framework). For members on those types, either:
   - ASCII-grep the raw `ThunderRoad.dll` bytes for the member name, or
   - **just write the call and build** — the compiler validates it against the real DLL. This
     is the most reliable check and is how every `Item` member in this mod was confirmed.

Confirmed/relevant API used here:
- Input: `playerHand.controlHand.castPressed` (the imbue/spell button); detect a **click** as a
  rising edge.
- Held item: `ragdollHand.grabbedHandle.item`. Hands: `Player.local.handRight/handLeft`.
- Spawn a clone: `item.data.SpawnAsync(Action<Item>, position, rotation, …)`.
- Tip direction: `item.flyDirRef.forward`. Imbue: `item.imbues[i].spellCastBase` /
  `.energy` / `.maxEnergy` / `.Transfer(spell, energy)`. Hand's selected spell:
  `hand.caster.spellInstance as SpellCastCharge`.
- Make a thrown item penetrate: `item.Throw(1f, Item.FlyDetection.Forced)` + set `rb.drag = 0`.
- Despawn: `item.Despawn()`.

## Conventions / gotchas

- **ModOption menu** (`[ModOption]`): every option **must** have (1) an interaction attribute
  (`[ModOptionButton]` / `[ModOptionSlider]` / `[ModOptionArrows]`) **and** (2) a
  `valueSourceName` naming a `public static` array of value wrappers (`ModOptionBool[]`,
  `ModOptionInt[]`, `ModOptionFloat[]`). Missing either piece **hangs the game at "Applying
  Options".** Method-based "action button" options are not supported. (Pattern verified by
  reflecting working mods' `GetCustomAttributesData()`.)
- Tunables are `public static` fields at the top of the script.
- Item classification: `item.data.type` (Weapon/Tool/Shield/…) and `item.data.category`
  ("Swords"/"Daggers"/"Shields"/…).
- After a code change, **always `./build.ps1`** and (if testing) restart the game.
- Keep the mod asset-free unless explicitly asked — reuse stock items/effects/spells by id.

## Out of scope / do not

- Don't add a CI workflow that builds the mod — it requires the local game DLLs and can't run
  on a hosted runner.
- Don't push to a remote or commit unless asked.
- Don't target Nomad (Quest) — this is PCVR/Mono only.
