# Changelog

All notable changes to this mod. Built for Blade & Sorcery PCVR 1.0.0.0.

## [Unreleased]

### Changed
- **Fires on a quick click, not a hold.** Firing now happens on button *release* if it was a
  short tap (< `clickMaxDuration`). Holding the button no longer fires, so you can still hold
  it to slide your grip along a weapon.

### Fixed
- **Close-range penetration** — the clone now spawns at the weapon's own position (like
  throwing it) with only a tiny lead, instead of 0.5 m past the tip. The big offset was
  materializing the clone on top of close targets, so they bounced; now it flies in clean.
  Reverted speculative CCD (which bounces off "air") to continuous-dynamic.
- **Shield aim** follows the hand's controller-forward instead of the upward-tilted `PointDir`.
- Menu/dialog gate also captures UI-click state at press time (`PlayerControl.uiClickDown`).
- **Imbue now carries to the clone** — the imbue is re-applied over the clone's first ~0.5s
  (its imbue points aren't ready the frame it spawns); falls back to the hand's selected spell.
- **Close-range penetration** — clones use speculative continuous collision detection, so they
  no longer bounce off / tunnel through targets at point-blank range.
- **No firing while a menu/dialog is open** (`PlayerControl.systemMenuActive`).
- **Clone spawns clear of the held item** (wider offset) so the weapon no longer gets nudged
  in your hand on click.
- **Shields** spawn further out along the hand's pointing direction so they clear the
  shield/arm.

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
