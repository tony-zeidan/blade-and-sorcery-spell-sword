# Changelog

All notable changes to this mod. Built for Blade & Sorcery PCVR 1.0.0.0.

## [Unreleased]

### Added
- **Shield support** — shields fire a clone perpendicular to their face (the direction they
  defend), computed from the shield's flattest collider axis. New **All shields** menu scope.
- **30-clone cap** — up to `maxActiveClones` (30) clones persist; the oldest despawn first.
- In-game **Mod Options** menu: master toggle + **Active On** scope (short sword only / all
  swords / daggers / weapons / weapons & tools / shields / any held item).
- Whoosh sound on the fired clone (`WhooshSwordShort`).

### Changed
- Reworked to a **single behavior**: click the imbue button to fire a clone straight out the
  blade tip (`flyDirRef.forward`), no spin, carrying the held item's current imbue.
- Clones are flagged thrown (`Item.Throw`) and have zeroed drag so they **penetrate** and
  travel **weight-independently**; `cloneSpeed` raised to 40 m/s.
- Imbue carry-over now falls back to the hand's selected spell when the blade isn't imbued
  (fixes imbue only working for pre-imbued items like arrows).

### Removed
- Lightning slash arc, blade spin, and forced fire imbue (earlier prototypes).

## Notes
- This project intentionally uses only stock game assets (no Unity project / AssetBundle).
- See [CLAUDE.md](CLAUDE.md) for architecture and the ThunderRoad API/build notes.
