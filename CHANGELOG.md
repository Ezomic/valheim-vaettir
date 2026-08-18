# Changelog

Notable changes to Vaettir. Format follows [Keep a Changelog](https://keepachangelog.com),
and the mod uses [semantic versioning](https://semver.org).

## [1.0.0] - 2026-08-18

First release. The number sat at 1.0 once before, early, and was taken back down when whole
mods arrived on top of it without it moving. This is the version that earns it: one chain,
finished, with everything that was not finished cut out and held on its own branch.

The chain, played end to end in a real world: an ancient seed planted, fed on greydwarf
deaths through its growth, opened into a forest spirit, communed with, and the heartwood
carried away and built into a stowing post that sorts the chests around it.

### The sapling

- Plant an ancient seed in the Black Forest. It does not grow on time, it grows on death,
  and only on the deaths of greydwarfs, and only within sight of it. About thirty of them
  and it opens, which is one raid if the forest comes to you.
- Greydwarfs specifically, not "forest creatures". An early version used the game's own
  forest faction and swept up trolls, boars and the Elder, which turned clearing out a nest
  into killing anything anywhere near here. Greylings are on the list at zero, so it is
  visible that they were considered and refused.
- It can be destroyed, because being asked to fight for an hour beside something you cannot
  lose is not defending it. Around ten hits from a brute: a fight happening nearby is
  survivable, a mob left to work on it is not. It says when something is chewing on it and
  when it is gone, because losing an hour in silence is a mystery rather than a difficulty.
- One hand-built model, with its own icon so the cultivator stops offering a carrot. Built
  to the game's own measurements rather than to taste, because smoothness is most of what
  reads as modded and nothing else in the game at that size is round.
- The sapling does not change shape as it fills, and the hover text is what tells you how
  far along it is. Four staged models exist and work, and three of them were not good
  enough to ship, so the staging is held for 1.1 rather than released and apologised for.

### The spirit and the heartwood

- The sapling opens into a forest spirit. Light with no body, bobbing and pulsing, and
  waking as you approach. Two rings of beads turn around it in step with each other, with no
  visible hoop, so it reads as one thing turning rather than as a swarm.
- Press use once and it folds itself into a piece of heartwood you carry away. It is a home
  rather than a heart: the spirit does not die and hand over a piece of itself.
- Build the heartwood somewhere and you have housed it.

### The stowing post

Stow is no longer a separate mod. Its post, its spirit and its sorting ship here unchanged.

- **One mod, not two shipped together.** Stow arrived carrying its own `[BepInPlugin]`, and
  leaving it there meant one DLL announcing two plugins in the log, writing two config
  files, registering with Core's version gate twice, and reporting two different version
  numbers. It reads as two mods in a trenchcoat, and it was one. The post's half is now
  driven by Vaettir's own plugin class: one GUID, one config file, one Harmony instance,
  one registration.
- Nothing you have built is affected. The piece keeps its internal name, so posts already
  standing survive.
- Its settings moved into `ezomic.valheim.vaettir.cfg`, under `[Post]`, `[Sorting]`,
  `[Carrier]` and `[Keys]`. Anyone who had tuned `ezomic.valheim.stow.cfg` copies those
  values across once; nothing reads that file any more.
- Delete `BepInEx/plugins/Stow` if you are upgrading by hand. Two copies loaded at once is
  the one thing that will go wrong.
- One spirit, not two. Each mod used to build its own from its own script and they had
  drifted into visibly different creatures. There is one now, and the carrier is that one at
  0.62 scale so it is the size it has always been on screen. Set the scale to 1 for a
  full-size spirit.
- One setting still drops the heartwood from the recipe and puts the post back to wood and
  nails, for anyone who wants the sorting without the ritual.
- The last of the seams from when these were two repos are gone. Icons and PropIndex each
  existed in two diverged copies, one per old namespace, and the comment justifying that
  said a copy was better than reaching into a sibling repo. There is no sibling repo.


### Requires

Longhouse Core is optional. Without it nothing refuses a client that lacks this mod, and
that matters: the sapling, the spirit, the heartwood and the post are registered pieces, and
a client that cannot resolve one throws the object away rather than erroring. A server
without Vaettir silently destroys everything already standing. Solo, none of that applies.

### Known gaps

Stated rather than quietly shipped.

- **Dedicated servers are untested.** Everything here has been exercised in single-player.
  Each spirit, sapling and post is driven by whoever owns it, so co-op should be fine, and
  should is doing real work in that sentence.
- **The spirit's parting has no effect on it.** The setting names a vanilla particle to play
  where it stood and ships blank, because a wrong name costs the moment its flourish rather
  than breaking anything. It is a flourish that is missing, not a step that fails.
- **The sapling stirs while it grows**, and that motion is new enough that it has had far
  less play than the rest of the chain.

### Held for later releases

Cut rather than shipped unproven or unfinished, each kept whole on its own branch so
continuing is a merge rather than a rebuild.

- **1.1** - sowing a rank of seeds by Farming skill, and the sapling's staged growth. The
  staging works and has been played; three of its four models were not good enough.
- **1.2**, a refinement pass - better animation throughout, and the post's own panel:
  fetch, tidy and presence. All three were built and none of them were proven, and with
  the three of them out the panel had nothing left in it, so it goes whole.
- **1.3** - bonemeal, and the bone mill that grinds it.
- **1.4** - upgrades for the stowing post. It starts out carrying ten items a trip and each
  upgrade raises that, so a post becomes something you improve rather than something you
  finish. It also gives the heartwood somewhere to go after the post is built.
- **1.5** - an upgrade that houses a second spirit, so two stacks are in the air at once
  rather than one moving faster. Two of them working is worth watching; one of them hurrying
  is a number.
- **Unscheduled** - the vaettr visitor and the market they were to stand in.

### Late fixes

- A dropped heartwood no longer falls through the floor. Stripping the donor's mesh took the
  collider with it, because on that donor both live on the same object, and an item with
  weight and nothing to rest on simply keeps going. Found by throwing one away.
- The spirit's parting effect setting was renamed before release rather than after, when it
  would have stranded the old key in other people's configs.

### Archived history

Stow and Furrow were separate repos before this. Their history is in `archive/`, one git
bundle each, clonable with `git clone archive/stow.bundle`. See `archive/README.md`.

