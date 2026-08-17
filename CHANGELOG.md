# Changelog

Notable changes to Vaettir. Format follows [Keep a Changelog](https://keepachangelog.com),
and the mod uses [semantic versioning](https://semver.org).

## [0.6.0] - unreleased

Not 1.0, and it was wrong to call it that. The number 1.0 says a thing is finished and
public, and this has an animation nobody has seen and a merge nobody has played. It went to 1.0 when it was one
chain that had been played end to end, and then three more features and two whole mods
arrived on top of it.

What has been played, in a real world: the seed planted, fed on greydwarf deaths through
all four stages, opened into a spirit, communed with, and the heartwood carried away and
dropped. That part works. Everything added since is built and not proven.

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
- Four growth stages, hand-built, with its own icon so the cultivator stops offering a
  carrot. Built to the game's own measurements rather than to taste, because smoothness is
  most of what reads as modded and nothing else in the game at that size is round.

### The spirit and the heartwood

- The sapling opens into a forest spirit. Light with no body, bobbing and pulsing, and
  waking as you approach. Two rings of beads turn around it in step with each other, with no
  visible hoop, so it reads as one thing turning rather than as a swarm.
- Press use once and it folds itself into a piece of heartwood you carry away. It is a home
  rather than a heart: the spirit does not die and hand over a piece of itself.
- Build the heartwood somewhere and you have housed it.

### The stowing post

Stow is no longer a separate mod. Its post, its spirit and its sorting ship here unchanged.

- Nothing you have built is affected. The piece keeps its internal name, so posts already
  standing survive, and it keeps its own settings file, so the rules and options you had are
  the ones you still have. Only the file it lives in moved.
- Delete `BepInEx/plugins/Stow` if you are upgrading by hand. Two copies loaded at once is
  the one thing that will go wrong.
- One spirit, not two. Each mod used to build its own from its own script and they had
  drifted into visibly different creatures. There is one now, and the carrier is that one at
  0.62 scale so it is the size it has always been on screen. Set the scale to 1 for a
  full-size spirit.
- One setting still drops the heartwood from the recipe and puts the post back to wood and
  nails, for anyone who wants the sorting without the ritual.

### Sowing a rank at a time

Furrow is no longer a separate mod either. It keeps its own settings file.

- Sow a rank of seeds per click, scaled by Farming: nothing at level 0, twenty near the top.
- One click never plants several ancient seeds. The sapling has no growth timer, and a
  growth timer is what sowing looks for before it multiplies anything.


### Requires

Longhouse Core is optional. Without it nothing refuses a client that lacks this mod, and
that matters: the sapling, the spirit, the heartwood and the post are registered pieces, and
a client that cannot resolve one throws the object away rather than erroring. A server
without Vaettir silently destroys everything already standing. Solo, none of that applies.

### What has not been proven

Worth reading before treating any of this as done.

- **The sapling's stirring** has never been seen running.
- **The merge itself.** Stow and Furrow ship inside this now, and no world has been played
  with the merged assembly. Each was working separately before it moved.
- **Dedicated servers.** Everything here has been exercised in single-player. Each spirit,
  sapling and post is driven by whoever owns it, so co-op should be fine, and should is
  doing real work in that sentence.
- The spirit's parting has no effect on it yet. The setting names a vanilla particle to play
  where it stood and ships blank, because a wrong name costs the moment its flourish rather
  than breaking anything.

### Not in this release

The vaettr visitor and the market they were to stand in, both cut and shelved rather than
shipped half finished.

### Late fixes

- A dropped heartwood no longer falls through the floor. Stripping the donor's mesh took the
  collider with it, because on that donor both live on the same object, and an item with
  weight and nothing to rest on simply keeps going. Found by throwing one away.
- The spirit's parting effect setting was renamed before release rather than after, when it
  would have stranded the old key in other people's configs.

### Archived history

Stow and Furrow were separate repos before this. Their history is in `archive/`, one git
bundle each, clonable with `git clone archive/stow.bundle`. See `archive/README.md`.
