# Changelog

Notable changes to Vaettir. Format follows [Keep a Changelog](https://keepachangelog.com),
and the mod uses [semantic versioning](https://semver.org).

## [Unreleased] - 1.1

The sapling half. Four things, and the first of them is the one that matters.

### The seed calls the forest to it

- **A planted sapling now draws greydwarfs to itself**, and calls harder the closer it is
  to opening. Without this it was entirely passive, and the quest it actually set was "go
  and find a place greydwarfs already walk through, then stand in it" - a scouting problem
  rather than a defending one, and the wrong half of what the piece is for. Now the place
  you chose is the thing that matters, and what it summons is what feeds it: a seed planted
  in an empty meadow is slow rather than impossible, and one planted beside a real camp is
  still faster, because the camp counts too.
- It ramps, and it is a raid rather than wildlife. The interval falls from 20 seconds at an
  unfed seed to 6 at one about to open, so the loudest part of the fight is the end of it. A
  constant rate is a wave you learn to stand in; a rising one can be heard without looking
  at anything. 90 and 30 were the first numbers and they were far too polite - one greydwarf
  every minute and a half is something you deal with between other jobs.
- Ridden on vanilla's own `SpawnArea`, which is the component a greydwarf nest is made of.
  That buys the near and total caps, the floor-finding, the spawn effect, and - the part
  that matters most - its own guards: owner only, inside the active area, with a player in
  range. Nothing happens while you are asleep, which was the sapling's first principle and
  is the one thing a spawner could most easily have broken.
- **They arrive from a distance and run in** rather than appearing around the seed. Found in
  play: they were materialising on top of it. Widening the radius does not fix that, because
  vanilla's FindSpawnPoint draws its distance from Random.Range(0, radius) - uniform across
  a disc, so most points land near the middle however wide it is. A prefix replaces that
  search with one that draws from a band, 35 to 60 metres by default, making the same two
  ZoneSystem checks in the same order so nothing lands inside a rock. Every other SpawnArea
  in the game runs vanilla's own method untouched.
- Getting them to actually come is a second thing. SetPatrolSpawnPoint is off, so they do
  not treat the trees they appeared in as home, and a sweep every two seconds calls
  SetHuntPlayer on everything from the roster within reach - which is what a vanilla raid
  does and means "stop wandering, go and find someone". They hunt the player rather than
  the sapling: there is no vanilla "attack this object" to ride, the player is standing at
  the sapling anyway, and it puts the raid on the person holding the ground instead of on
  the thing with 500 health.
- **It says so.** "The forest is enraged." goes to everyone within BeckonRange, centre
  screen, once per sapling per load - fired from the first wave actually landing rather than
  from planting, so a sapling that can find nowhere to spawn never announces a siege that is
  not coming. Everyone nearby rather than whoever planted it, because it is a warning about
  a place and on that frame there is nothing on screen to account for the noise. The text is
  a config line and blank turns it off.
- **The first wave leaves immediately.** Vanilla counts its spawn timer up from zero on a
  two-second repeat and fires when it exceeds the interval, so an unfed seed produced
  nothing at all for 22 seconds and then the wave still had to walk in - half a minute of
  silence after planting, which reads as the mod not working. The timer now starts full, so
  the first wave goes out on the next tick. It does the same on every later load, and since
  the timer only advances with a player in range, walking back to a half-fed sapling
  restarts the siege as you arrive rather than after another silent interval.
- The band came in from 35-60m to 25-40m with it, so the first wave is on you in roughly
  fifteen seconds rather than forty. Still far enough to be out of sight in forest.
- **They arrive in waves, from one side.** Vanilla's spawner produces exactly one creature
  per interval, which is a queue rather than a raid - a greydwarf, a wait, another
  greydwarf, and nothing that has to be handled as a group. A wave is 2 at an unfed seed
  rising to 5 at one about to open, and the whole wave comes out of the trees within 40
  degrees of one bearing so it can be turned to face. The extra members go through the
  game's own SpawnOne, so MaxNear and MaxTotal police themselves and a wave that would
  breach them simply comes up short.
- **A sapling now costs 50 greydwarf deaths, not 30.** Thirty was chosen when the sapling
  was passive and thirty was roughly one raid happening to arrive. It does not wait for a
  raid any more, it makes one, so the number is the length of a fight you started rather
  than the odds of one finding you. Ten of them around you at a time, twenty-four alive in
  the area.
- The trigger range is 48m rather than vanilla's 256m. A nest filling a forest you are
  nowhere near is one thing; a sapling quietly getting itself killed by what it summoned
  while you are two zones away is another.
- Six standing around it at once, sixteen alive in the area. Deliberately more than the
  sapling survives being ignored for - it has 500 health and about ten brute hits in it -
  which is the trade the whole feature makes: the forest comes to you instead of you going
  out to find it, and the price is having to hold the ground.
- All of it is off with one setting, and every number above is config.

### Everyone defending it can see the count

- **Fixed: only whoever landed the killing blow saw the counter.** `Character.OnDeath` runs
  on the client that owns the creature and nowhere else, and the message went to
  `Player.m_localPlayer` - so with two players clearing greydwarfs around one sapling, each
  of them saw roughly half the kills register and neither could tell whether the other's
  were counting at all. They always were; only the message was missing. It now goes to every
  player within the sapling's own feed range, which needs no networking of ours because
  `Player.Message` already RPCs to a player this client does not own.

### A marker on the map

- **A planted sapling puts a pin on your map**, and takes it off again when it opens or is
  destroyed. This is the one piece in the mod you are meant to walk away from, and a seed in
  bare ground is not findable from fifty metres away - losing one to "I know it was around
  here" costs the same ancient seed as losing it to a brute.
- Client-side and per player, saved in your own profile like a pin you placed by hand.
  Nothing about it is networked and a server needs to know nothing about it.
- The pin comes off by reconciling against the world - a pin whose zone is loaded with no
  sapling under it is stale - rather than from the sapling's own OnDestroy. Found in play:
  the pins never went away. OnDestroy fires on a destroyed sapling and an unloaded one
  alike, and the test meant to tell them apart asked ZDOMan whether the ZDO was gone, which
  it never is on that frame - DestroyZDO only queues the uid on m_destroySendList and the
  ZDO leaves m_objectsByID some frames later. Both endings read as "merely unloaded".
  Asking whether a sapling is standing there needs no guess, covers every ending including
  one another player removed, and clears the pins the first version stranded.

### The spirit's parting has an effect again

- **The parting effect shipped blank in 1.0** because the name it wanted was a guess about
  the game and the honest answer was "not confirmed". It is now a list walked in order -
  `vfx_ghost_death`, then two fallbacks - and the first name that resolves is used, so the
  wrong ones cost nothing and are skipped in silence. Looked up through PropIndex as well as
  ZNetScene, because plenty of effect prefabs carry no ZNetView and are invisible to
  ZNetScene however loaded they are.

### Not in this yet

- The sapling's staged growth and the sowing of a rank of seeds are still on
  `v1.1-sowing`, cut from 1.0 for their art. They are 1.2 now: the roadmap in the 1.0 entry
  below was renumbered when this release took the 1.1 slot, and that branch keeps its old
  name. Nothing here touches it, and merging is a separate job - it predates the 1.0
  tidy-up and does not fast-forward.

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
- **A spirit carries ten items a trip**, so a stack of fifty wood leaves the post in five
  trips rather than crossing the room in one. A big load looks like a big load.
  `ItemsPerTrip` sets it and 0 restores the whole stack. This is the number the post
  upgrades are meant to raise.
- **The carrying spirit and the one you commune with are the same creature**, which took
  three passes to actually mean. The merge gave them one mesh; they still resolved their
  material through two different lookups against two different donor lists, and even once
  that was fixed only one of them glowed. The glow is a Light and an emission write, and
  the carrier had half of one, so the room around it lit up while its own mesh stayed
  flat. Colour, range, pulse depth and pulse period were each a second value too.
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

This list was renumbered after 1.0 shipped. 1.1 became the sapling half instead - the seed
calling greydwarfs to itself, the map pin, the co-op counter and the parting effect - which
is work on the piece the whole chain starts at rather than on the two halves either side of
it, and everything below moved down one. The branch names still carry their old numbers,
because a branch is renamed easily and remembered wrongly: `v1.1-sowing` is 1.2 here,
`v1.2-panel` is 1.3, and `v1.3-bonemeal` is 1.4.

- **1.2** - sowing a rank of seeds by Farming skill, and the sapling's staged growth. The
  staging works and has been played; three of its four models were not good enough. It sits
  directly behind 1.1 rather than ahead of it because the staged models are the visible half
  of the same piece, and it is worth knowing how a sapling plays once it is worth standing
  next to before choosing how it should look while you do.
- **1.3**, a refinement pass - better animation throughout, and the post's own panel:
  fetch, tidy and presence. All three were built and none of them were proven, and with
  the three of them out the panel had nothing left in it, so it goes whole.
- **1.4** - bonemeal, and the bone mill that grinds it.
- **1.5** - upgrades for the stowing post. It starts out carrying ten items a trip and each
  upgrade raises that, so a post becomes something you improve rather than something you
  finish. It also gives the heartwood somewhere to go after the post is built.
- **1.6** - an upgrade that houses a second spirit, so two stacks are in the air at once
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

