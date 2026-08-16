# Changelog

Notable changes to Vaettir. Format follows [Keep a Changelog](https://keepachangelog.com),
and the mod uses [semantic versioning](https://semver.org).

## [1.0.0] — 2026-08-16

First release. The chain has been played end to end in a real world: planted, fed on
greydwarf deaths through all four stages, opened into a spirit, communed with, and the
heartwood carried away and dropped.

### The sapling

- **Plant an ancient seed in the Black Forest.** It does not grow on time — it grows on
  death, and only on the deaths of greydwarfs, and only within sight of it. About thirty of
  them and it opens — one greydwarf raid, if the forest comes to you, which is the best
  thing that happens in the chain and so is priced to be enough on its own.
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
- **Built to vanilla's own measurements rather than to taste.** Valheim's planted seed is
  0.10m wide and 0.39m tall, spends 48 triangles on it, and gives it a flat colour with no
  texture at all. The first version here was a 0.25m sphere at several hundred triangles on
  a disc of moss — and smoothness is most of what reads as modded, because nothing else in
  the game at that size is round.

### The spirit and the heartwood

- The sapling opens into a **forest spirit** — light with no body, bobbing and pulsing, and
  waking as you approach. Two rings of beads turn around it in step with each other, with no
  visible hoop: the circles are implied by where the beads are and by their moving together,
  which reads as one thing turning rather than as a swarm.
- **Press use once** and it folds itself into a piece of heartwood you carry away. It is a
  home rather than a heart: the spirit does not die and hand over a piece of itself.
- **Build the heartwood somewhere** and you have housed it. A woven nest with the light kept
  inside.
- The stowing post costs heartwood, which is what the whole chain is paying for.

### The stowing post — Stow, folded in

- **Stow is no longer a separate mod.** It ships in this assembly. Its post has a socket in
  the model for a heartwood it could not obtain alone, and the spirit that carries your
  items is born at that heartwood and returns to it — so standing alone it was a post with
  a hole in it and a spirit with no origin.
- **One spirit, not two.** Each mod used to generate its own from its own Blender script,
  and they had drifted before anyone restyled anything: one ring of seven beads at a 0.21
  orbit against two crossed rings of six at 0.34, separately jittered. The same character
  was visibly two creatures depending on which mod drew it. There is one source now,
  `tools/spirit_core.py`, and `Carrier.Orbit` follows the mesh rather than taste.
- **Nothing you have built is affected.** The piece is still `stow_post` to the byte —
  ZNetScene discards any ZDO whose prefab name stops resolving, so a rename would have
  destroyed every post standing in every world, silently. Stow keeps its plugin GUID and
  its own `ezomic.valheim.stow.cfg`, so it stays its own entry in the mod list and reads
  the settings you already have. Only the DLL moved.
- `CarrierScale` defaults to 0.62, which is the ratio between the two old spirit meshes, so
  the carrier is the size it has always been on screen. Set it to 1 for a full-size spirit.
- `CoupleToStow` still turns the heartwood off and puts the post back to wood and nails.

### Sowing by skill — Furrow, folded in

- **Sow a rank of seeds per click**, scaled by Farming: nothing at level 0, twenty near the
  top. `MaxSeeds` and `MaxAtLevel` are the two numbers; the count between them is
  `floor(1 + (MaxSeeds - 1) * min(level / MaxAtLevel, 1))`.
- Folded in for maintenance rather than for fiction, and the README says so. Four source
  files, no assets, no prefabs — so unlike Stow there was never anything here that a
  missing DLL could destroy.
- Keeps its plugin GUID and `ezomic.valheim.furrow.cfg`.
- **No special case was needed for the ancient sapling.** Furrow gates multi-sowing on a
  `Plant` component and the sapling has its `Plant` destroyed deliberately, so one click
  cannot plant several ancient seeds.

### Not in this release

- **The visitor and the market**, both cut from v1 and shelved unshipped rather than half
  finished.

### Requires

- **Longhouse Core**, which is installed for you as a dependency. Vaettir registers with its
  version gate at `Everyone`, so a server and its clients must all have the mod at a matching
  build. This is not caution: the sapling, the spirit and the heartwood are registered
  prefabs, and a client that cannot resolve a prefab hash **discards the ZDO rather than
  erroring** — so a server without Vaettir silently destroys every sapling already standing.

### Known limits

- Exercised in single-player. Each spirit and sapling is driven by whoever owns its ZDO, so
  co-op should be fine, but **dedicated-server use is untested**.
- The spirit's parting has no effect on it. `PartingEffect` names a vanilla particle to play
  where it stood, and ships blank because no name has been confirmed loaded yet — a wrong one
  costs the moment its flourish rather than breaking anything.
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
