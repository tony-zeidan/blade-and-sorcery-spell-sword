# Changelog

All notable changes to this mod. Built for Blade & Sorcery PCVR 1.0.0.0.

## [Unreleased]

### Fixed
- **Mod options no longer overlap another mod's menu** — the options are now grouped under a
  dedicated "Spell Sword" category (`[ModOptionCategory]`) instead of the shared default group.
- **Weapons fire straight along how they're held** — the launch direction is grip → the
  weapon's far end (farthest collider), so arrows, swords, greatswords, shovels, and hammers
  all fly business-end first in the direction you're pointing, instead of out a face
  (`flyDirRef`).
- **Clicking to fire no longer slides your hand down the hilt** — the grip position is captured
  when you press and snapped back after the shot if it moved, so a fire tap can't slide the
  hand (a real hold is left alone for intentional sliding).
- **No longer fires when clicking UI** (books, menus, dialogs) — firing is gated on the hand's
  UI pointer (`Pointer.isPointingUI`), which is only set when actually pointing at UI, so it
  doesn't block normal gameplay (unlike the earlier `uiClickDown` attempt).

## [1.2.0] - 2026-06-29

### Added
- **Projectile speed** is now a menu slider (25–80 m/s, default 45 — a bit faster than before).
- **Max active clones** is now a menu setting (5–50, default 20).

### Changed
- Clones now **despawn after 2s even if you're holding one** (previously they lingered in hand
  until released).

### Fixed
- **Items (e.g. arrows) becoming un-throwable after a while** — the clone's controller could
  linger on a pooled item's GameObject after despawn and then despawn a *recycled* item. It now
  tears itself down on the item's cull event, and the clone registry is kept clean of pooled
  references.

### Known limitation
- Point-blank shots have a small minimum distance before they penetrate (they bounce if the
  target is almost touching). This is the game's thrown-weapon velocity check needing a frame of
  real travel; higher projectile speed shrinks the dead zone.

## [1.1.0] - 2026-06-29

### Changed
- **Fires on a quick click, not a hold** — firing happens on button release within
  `clickMaxDuration`. Holding the button no longer fires, so you can still hold it to slide
  your grip along a weapon.
- Thrown clones now despawn after **2s** (`projectileLifetime`), unless you're holding one.

### Fixed
- **Imbue carries to the clone reliably** — re-applied over the clone's first moment (its imbue
  points aren't ready the frame it spawns); falls back to the spell selected on that hand.
- **Close-range penetration** — the clone arms its thrown/penetration state at spawn (velocity
  is set before `Throw()`) and spawns at the weapon's own position, so point-blank shots stab
  instead of bouncing off.
- **Right hand fires again** — the menu gate no longer trips on the UI-pointer (right) hand.
- **Shield aim** uses the hand's pointing direction with a downward pitch correction
  (`shieldAimPitchCorrection`) so it tracks where your fist points.
- **No firing while a menu/dialog is open** (`PlayerControl.systemMenuActive`).

## [1.0.0] - 2026-06-29

First public release.

### Added
- **Shield support** — shields fire a clone in the direction the hand is pointing
  (`RagdollHand.PointDir`). New **All shields** menu scope.
- **30-clone cap** — up to `maxActiveClones` (30) clones persist; the oldest despawn first.
- In-game **Mod Options** menu: master toggle + **Active On** scope (short sword only / all
  swords / daggers / weapons / weapons & tools / shields / any held item).
- Flight sound on the fired clone (`WhooshSpin` by default, configurable) that stops on first
  impact via a small `CloneFlightSound` component.

### Changed
- Reworked to a **single behavior**: click the imbue button to fire a clone straight out the
  blade tip (`flyDirRef.forward`), no spin, carrying the held item's current imbue.
- Clones are flagged thrown (`Item.Throw`) and have zeroed drag so they **penetrate** and
  travel **weight-independently**; `cloneSpeed` raised to 40 m/s.
- Imbue carry-over now falls back to the hand's selected spell when the blade isn't imbued
  (fixes imbue only working for pre-imbued items like arrows).

### Removed
- Lightning slash arc, blade spin, and forced fire imbue (earlier prototypes).

### Hardened
- Per-frame logic and the spawn callback are wrapped in try/catch with throttled logging, so
  a failure can't spam the log or break the session.
- Clones ignore the caster's own body; the FIFO cull keeps clones the player is holding;
  clones are cleaned up on mod unload.

## Notes
- This project intentionally uses only stock game assets (no Unity project / AssetBundle).
- See [CLAUDE.md](CLAUDE.md) for architecture and the ThunderRoad API/build notes.
