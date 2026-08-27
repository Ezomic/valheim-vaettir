# Vaettir

Raise a forest spirit by killing greydwarfs beside a planted seed, then house it in a post
that sorts your storage.

## Quick start

1. Kill greydwarf brutes and shamans until one drops an **ancient seed**. Vanilla loot; it
   needs nothing from this mod.
2. Plant it with the **cultivator**, in bare ground, well inside the Black Forest and
   away from any base.
3. Kill greydwarfs **within 24 metres of it**. It grows on those deaths and nothing else.
   You do not have to go looking for them: **a planted seed calls them to it** in waves,
   which get bigger and closer together the nearer it is to opening.
   Roughly fifty ordinary ones, and elites count as four and shamans as three.
4. **Defend it.** It has real health and can be destroyed, and the seed is not refunded. It
   tells you when something is hitting it.
5. When it opens, a **forest spirit** rises out of it. Press use once and it folds itself
   into a **heartwood**.
6. Build a **stowing post** with that heartwood: 20 fine wood, 20 iron nails and the
   heartwood, on the hammer's Furniture tab. That is where the spirit now lives.
7. Tell your chests what they hold with the **Holds…** button in any chest window, then drop
   things in the post and close it.

One spirit gives exactly one heartwood, and one heartwood builds one post. Taking the post
down gives the heartwood back.

## At a glance

| | |
| --- | --- |
| Seed | One ancient seed, not refunded if the sapling dies |
| Where | Anywhere you like; greydwarfs are the requirement, not the biome |
| To open it | 50 points of greydwarf death. Greydwarf 1, shaman 3, elite 4, greyling 0 |
| Range | 24m, and only the nearest sapling is fed by a kill |
| Calling | The first wave leaves at once; waves of 2-5 arrive together from 25-40m out and come running, every 20s falling to 6s as it fills. Ten at once, twenty-four in the area, and only while you are within 48m |
| On the map | Pinned where you planted it, unpinned when it opens or dies |
| Sapling health | 500, about ten hits from a brute |
| Yield | One heartwood per spirit, always |
| Stowing post | 20 fine wood, 20 iron nails, 1 heartwood. 12 slots, 6 across by 2 down |

Every number above is a config default and can be changed.

## Installation

Install [BepInEx for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
(5.4.2333 is what this is built against), then put the `Vaettir` folder from the release into
`BepInEx/plugins/`. It is a single DLL plus the `.obj`, `.png` and `.col` files that sit
beside it, read at runtime, so they all have to stay in that folder together.

[Longhouse Core](https://github.com/Ezomic/valheim-core) is optional and not installed for
you. Solo you do not need it at all. On a server it matters, for the reason in
[Multiplayer](#multiplayer).

## Configuration

One file, `BepInEx/config/ezomic.valheim.vaettir.cfg`, written on first run. The sapling and
the spirit are under `[Sapling]`, `[Spirit]` and `[Heartwood]`; the stowing post is under
`[Post]`, `[Sorting]`, `[Carrier]` and `[Keys]`.

Almost everything is adjustable: what each creature is worth, how far a kill counts, the
sapling's health, and the post's size and recipe. BepInEx writes every entry on first run and
from then on the saved value beats any new default in code, so changing a default in a later
version does nothing on a machine that has already run the mod. Edit the cfg.

`TestMode` under `[Diagnostics]` drops a sapling's cost to three greydwarfs so the whole chain
can be walked in a minute. It announces itself in the log on every startup, because it is the
setting most likely to be left on by accident.

## Mechanics

### The sapling

**It calls the forest to it.** A planted seed draws greydwarfs out of the woods around it,
faster as it fills, and what it summons is what feeds it. They come from 25 to 40 metres
away and run in, so you hear and see them coming rather than finding them already on top of
you, and the first wave leaves the moment you plant. Everyone
nearby is told once that the forest is enraged, because on the frame it starts there is
nothing on screen to account for the noise coming out of the trees. They arrive in waves, out of the trees on one side rather than scattered around you, and
the waves get bigger and closer together as it fills. It is a siege rather than wildlife
wandering past -
and that is what the sapling's health is for: you are not looking for a fight, you are
holding a place, and a raid you walk away from halfway through takes the seed with it. It
only ever calls while a player is within 48 metres, so nothing happens while you are away.
Turn it off with `Beckon` and the sapling goes back to waiting for kills you bring it.

**Black Forest only, and not on the edge of it.** The seed is a greydwarf ritual - what it
calls and what feeds it both live there - so it refuses anywhere else, and refuses the last
five metres before the boundary as well. Planting one step inside the treeline would put half
the fight in the meadow.

**It will not go in anybody's base.** A ward already refuses it, because it is an ordinary
piece - and beyond that it cannot be planted inside the area a workbench or fire radiates,
which is the same test the game uses to keep creatures from spawning in your house. One
already standing goes quiet if a base grows around it. That matters because of what the seed
does now: a dozen of these around a stranger's longhouse would be a siege engine, and the
counter-play to one planted next to you is to put a workbench down rather than to fight it.

**It goes on your map** when you plant it, and the pin comes off once there is no longer a
sapling standing there. That pin is yours alone and is saved in your own profile.

It keeps a count, not a clock. Only the creatures on its list feed it, only kills within range
count, and only the nearest sapling is fed, so a heap of them planted together does not all
grow off the same work. Greylings are on the list at zero.

It can be destroyed, and it says so when something is hitting it and when it is gone. Losing
it costs the seed and the time.

### The stowing post

A real container, 12 slots, wide and shallow. There is no panel listing your pack to tick
through, no selection to remember and no keybind. The interface is the chest window you
already know, and "which items" is answered by which items you dropped in.

It empties on close rather than continuously, so two half-stacks can be dropped in and merge.
Anything with no home stays in the post and the hover text says how many, so a post holding
six things is a post telling you six things need a chest.

The spirit carries the contents out one stack at a time, flying in an arc over whatever is in
the way. A full post takes a minute or two to clear. Turn the carrier off in config and the
post moves everything the instant you close it; the sorting is identical either way.

It skips wards, privacy locks, chests someone else has open, chests on carts and ships, and
other posts.

### Telling a chest what it holds

A chest window has a **Holds…** button under the game's own Stack all. The post has no button
of its own: it distributes to the chests around it, so the rules live on them. A chest holds
groups, like ore, fuel, seeds or building materials, and where a group will not do, single
items. Chests with a rule say so in their hover text, in gold.

A rule can also refuse. Group cells cycle ignored, holds, refused, and a search result is
shift-clicked to refuse rather than hold, so "ore, but never tin" is two presses.

The groups are read off the game at runtime rather than written down here:

| Group | Is whatever… |
| --- | --- |
| Ore | any smelter accepts |
| Bars and ingots | any smelter produces |
| Fuel | burns in a smelter, kiln or fireplace |
| Wood | a charcoal kiln eats |
| Raw food | a cooking station accepts |
| Cooked food | fills you up |
| Mead and potions | a fermenter turns out |
| Seeds and crops | the cultivator can plant |
| Building materials | the hammer asks for |
| Trophies, ammo, fish, gear | the item says it is |

So a mod that adds black metal ore, or a new crop, lands in the right group without this mod
knowing it exists.

Every item asks which chest wants it most, in this order: a chest that names that exact item,
then one that holds a group it belongs to, then one set to anything else, then one with no
rule at all that already holds some. Ties go to the nearer chest. That last tier is what makes
the mod useful before you have configured anything, and a chest you have given a rule never
gets it.

## Multiplayer

The mod is needed at both ends. This is not caution. The sapling, the spirit, the heartwood
and the post are all registered pieces, and a client that cannot resolve one throws the object
away rather than erroring, so a server without Vaettir silently destroys everything already
standing in the world.

Longhouse Core is what turns that into a refused connection instead of a loss. Without it
nothing checks. Solo, none of it applies.

## Design notes

Why the seed counts kills instead of ticking a clock, why the heartwood is a home rather than
a heart, and why the sorting rules live on the chest: [DESIGN.md](https://github.com/Ezomic/valheim-vaettir/blob/main/DESIGN.md).

## Upgrading from Stow

Stow used to be a separate mod and now ships inside this one as part of it, not beside it.
The post keeps its internal name, so every post already standing survives untouched.

Its settings moved into this mod's config file. If you had tuned `ezomic.valheim.stow.cfg`,
copy those values across once and delete it; nothing reads it any more.

Delete `BepInEx/plugins/Stow` if you are upgrading by hand. Two copies loaded at once is the
one thing that will go wrong.

Their history is in `archive/`, one git bundle each. See `archive/README.md`.

## Reporting bugs

[The Discord](https://discord.gg/hJzAVaZ5wb) is the fastest route, and the right one if
you are not sure whether what you are seeing is a bug at all. Issues on
[the repo](https://github.com/Ezomic/valheim-vaettir) work too and suit anything long.

Bring `BepInEx\LogOutput.log` if you can, and say whether you were on a server or your
own world. The log is most of the difference between a fix and a guess, and it is written
every session whether or not anything went wrong.

## Part of the Longhouse pack

This is one of [the Longhouse pack](https://thunderstore.io/c/valheim/p/Ezomic/Longhouse/),
a pinned set of my mods that installs in one click and is what the Longhouse server runs. You
do not need the pack to use this on its own, and nothing here behaves differently outside it.

[The Discord](https://discord.gg/hJzAVaZ5wb) is where the server lives if you want to play on
it: small, EU, hard combat difficulty and everything else vanilla.
