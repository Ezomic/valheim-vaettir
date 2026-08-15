# Changelog

Notable changes to Vaettir. Format follows [Keep a Changelog](https://keepachangelog.com),
and the mod uses [semantic versioning](https://semver.org).

## [1.0.0] — 2026-08-16

First release. The chain has been played end to end in a real world: planted, fed on
greydwarf deaths through all four stages, opened into a spirit, communed with, and the
heartwood carried away and dropped.

### The sapling

- **Plant an ancient seed in the Black Forest.** It does not grow on time — it grows on
  death, and only on the deaths of greydwarfs, and only within sight of it. About forty of
  them and it opens.
- **Greydwarfs specifically, not "forest creatures".** An early version used the game's own
  `ForestMonsters` faction and swept up trolls, boars and the Elder, which turned clearing
  out a nest into killing anything anywhere near here. Greylings are on the list at zero, so
  it is visible that they were considered and refused.
- **It can be destroyed**, because being asked to fight for an hour beside something you
  cannot lose is not defending it. Around ten hits from a brute: a fight happening nearby is
  survivable, a mob left to work on it is not. It says when something is chewing on it and it
  says when it is gone, because losing an hour in silence is a mystery rather than a
  difficulty.
- Four growth stages, hand-built, with its own icon so the cultivator stops offering a
  carrot.

### The spirit and the heartwood

- The sapling opens into a **forest spirit** — light with no body, bobbing and pulsing, motes
  drifting on rings around it, waking as you approach.
- **Press use once** and it folds itself into a piece of heartwood you carry away. It is a
  home rather than a heart: the spirit does not die and hand over a piece of itself.
- **Build the heartwood somewhere** and you have housed it. A woven nest with the light kept
  inside.
- Once you have housed one, **a vaettr comes to your bed**.
- Stow's stowing post costs heartwood, so the two mods meet if you have both.

### Not in this release

- **The visitor and the market**, both cut from v1 and shelved unshipped rather than half
  finished.

### Known limits

- Exercised in single-player. Each spirit and sapling is driven by whoever owns its ZDO, so
  co-op should be fine, but **dedicated-server use is untested**.
- `TestMode` walks the whole chain without killing forty greydwarfs, which is how most of it
  was exercised before the full run.

### Late fixes

- **A dropped heartwood no longer falls through the floor.** Stripping the donor's mesh
  destroyed the whole renderer object, and on a surtling core the collider lives on that same
  object — so the strip took the collision with it, and an ItemDrop with a Rigidbody and
  nothing to rest on simply keeps going. Found by throwing one away.
- `FadeEffect` is now `PartingEffect` and defaults to blank. The old name read as the control
  for the spirit's visible glow, which is something else entirely, and its old default named
  a prefab that does not exist — so it bought a warning on every commune and nothing on
  screen. Renamed before release rather than after, when it would have stranded the key in
  other people's configs.
