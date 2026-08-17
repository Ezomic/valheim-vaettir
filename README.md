# Vaettir

Something grows where you have killed enough. Speak to it once, and it comes home with you.

Plant an ancient seed in the Black Forest. It does not grow on time, it grows on death, and
only on the deaths of greydwarfs, and only within sight of it. About thirty of them and it
opens into a forest spirit, which is one raid if the forest comes to you. Press use once and
the spirit folds itself into a piece of heartwood for you to carry, and you go and build it
somewhere to live.

Where it lives is a stowing post. Drop things in, close it, and the spirit carries them to
the chests that asked for them.

## Quick start

1. Kill greydwarf brutes and shamans until one drops an **ancient seed**. This is vanilla
   loot and needs nothing from this mod.
2. Take out the **cultivator** and plant the seed somewhere greydwarfs will come. It goes in
   bare ground; no tilled soil needed.
3. Kill greydwarfs **within 24 metres of it**. It grows on those deaths and nothing else.
   Roughly thirty ordinary ones, or fewer if you find a nest, since elites count as four and
   shamans as three. A "Forest is moving" raid arriving on top of it can finish it alone.
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
| To open it | 30 points of greydwarf death. Greydwarf 1, shaman 3, elite 4, greyling 0 |
| Range | 24m, and only the nearest sapling is fed by a kill |
| Sapling health | 500, about ten hits from a brute |
| Yield | One heartwood per spirit, always |
| Stowing post | 20 fine wood, 20 iron nails, 1 heartwood. 12 slots, 6 across by 2 down |
| Bonemeal | 10 bone fragments and 2 entrails at a workbench, makes 5 |
| Bonemeal effect | Brings a third of a crop's growing time forward, doubles its harvest |
| Sowing | Up to 20 seeds a click, or 5 for trees, reached at Farming 80 |

Every number above is a config default and can be changed.

## Installation

Install [BepInEx for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
(5.4.2333 is what this is built against), then put the `Vaettir` folder from the release into
`BepInEx/plugins/`. The mod is a single DLL plus the `.obj`, `.png` and `.col` files that sit
beside it, and it reads those at runtime, so they all have to stay in that folder together.

[Longhouse Core](https://github.com/Ezomic/valheim-core) is optional and not installed for
you. Solo you do not need it at all. On a server it adds a version gate that turns away a
client whose build does not match, which matters here for a reason worth reading in
[Multiplayer](#multiplayer).

## Configuration

Three files under `BepInEx/config/`, written on first run:

| File | Covers |
| --- | --- |
| `ezomic.valheim.vaettir.cfg` | The sapling, the spirit, the heartwood, bonemeal |
| `ezomic.valheim.stow.cfg` | The stowing post, the carrying spirit, sorting |
| `ezomic.valheim.furrow.cfg` | Sowing several seeds at once |

Three files because the stowing post and the sowing used to be separate mods and keep their
own settings, so an upgrade does not lose what you had.

Almost everything is adjustable: what each creature is worth, how far a kill counts, the
sapling's health, the post's size and recipe, how much of a crop bonemeal advances and
whether it doubles the harvest at all, the sowing curve, and the keys.

BepInEx writes every entry on first run, and from then on the saved value beats any new
default in code. Changing a default in a later version does nothing on a machine that has
already run the mod, so edit the cfg.

`TestMode` under `[Diagnostics]` drops a sapling's cost to three greydwarfs so the whole
chain can be walked in a minute. It announces itself in the log on every startup, because it
is the setting most likely to be left on by accident.

## Mechanics

### The sapling

It keeps a count, not a clock. Only the creatures on its list feed it, only kills within
range count, and only the nearest sapling is fed, so a heap of them planted together does not
all grow off the same work. Greylings are on the list at zero.

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

The chest window has a **Holds…** button under the game's own Stack all. A chest holds
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
rule at all that already holds some. Ties go to the nearer chest.

That last tier is what makes the mod useful before you have configured anything. A chest you
have given a rule never gets it.

### The post's panel

The same button opens the post's own settings, in two tabs.

- **Fetch** is stowing run backwards: name what the post should have and spirits bring it in
  from the chests around it.
- **Tidy** moves items sitting in a chest whose rule actively refuses them. It only ever
  corrects a mistake, so it cannot churn a room that is already right.
- **Presence** decides whether the spirit only appears when there is work, or lives at the
  heartwood between runs.

Errands run in order: stow, then fetch, then tidy.

### Bonemeal

Crafted at a workbench from 10 bone fragments and 2 entrails, five at a time.

Use it on the crop under your crosshair and it brings a third of that plant's growing time
forward and doubles what it yields when picked. Three will finish anything. It refuses rather
than being spent if there is no plant under the crosshair, or if the one there cannot grow
where it stands.

Feeding one plant twice brings more time forward but does not raise the harvest again.

### Sowing a rank at a time

Sow a whole rank of seeds in one click, as many as your Farming skill has earned. At level 0
it does nothing at all: one click, one seed, exactly as vanilla. Twenty is reached at Farming
80, and trees have their own ceiling of five because a sapling needs several times a crop's
room.

Two shapes, switchable in game. Row lays the seeds in a line across your facing; circle rings
them around the seed under your cursor.

| Key | What it does |
| --- | --- |
| Numpad + | One more seed per click, up to what your level allows |
| Numpad - | One fewer, down to one |
| Numpad * | Switch between row and circle |

Only read while the cultivator is out, and all rebindable. The scroll wheel is deliberately
not used, because vanilla already spends it rotating the placement ghost.

It does not skip a rule. Every extra seed is checked for no-build zones, other players'
wards, biome, cultivated ground and space. Anything that fails is quietly dropped, so a click
near the edge of a field sows the part of the rank that fits. It does not make seeds cheaper
either: twenty seeds costs twenty seeds, and each one raises Farming exactly as if you had
placed them by hand.

## Multiplayer

The mod is needed at both ends. This is not caution. The sapling, the spirit, the heartwood
and the post are all registered pieces, and a client that cannot resolve one throws the
object away rather than erroring, so a server without Vaettir silently destroys everything
already standing in the world.

Longhouse Core is what turns that into a refused connection instead of a loss. Without it
nothing checks. Solo, none of it applies.

The sowing half is client-side and needs nothing of anyone: a player without the mod simply
plants one seed at a time.

## Design notes

### Why a seed that ignores time

Valheim's plants are timers. You put a carrot in the ground and the world's clock does the
rest, which is the right design for a carrot and an empty one for a ritual. A thing that
happens whether or not you are there has not been earned by anybody.

So the ancient sapling keeps a count instead of a clock. It is fed by kills near it, which
makes the whole thing a place you have to keep coming back to and keep clearing out. The work
is the point. Nothing about it happens while you are asleep.

### Greydwarfs specifically, not "forest creatures"

An early version used the game's own forest faction and it swept up trolls, boars and the
Elder along with them, which turned a quest about clearing out a nest into "kill anything,
anywhere near here". A named list is duller to write and much better to play.

Greylings are on the list at zero, so it is visible that they were considered and refused.
They are the free kill, and letting them count would make the whole thing a matter of
standing still.

### Why it can be destroyed

Because you are being asked to fight for an hour beside it, and something you cannot lose is
not something you are defending.

Ten hits from a brute means a fight happening around it is survivable and a mob left to work
on it is not. It tells you when something is hitting it and when it is gone, because losing
an hour in silence is a mystery rather than a difficulty.

The first version inherited the donor's health, which is a carrot's, and the first greydwarf
that swung anywhere near one ended the ritual. That was found by playing.

### The heartwood is a home, not a heart

The spirit does not die and hand over a piece of itself. It folds itself into the heartwood
and you carry it somewhere.

This started as "it gives up its heart", which was wrong in a way that took a while to name.
The whole chain up to that point is violent, an hour of killing to summon something, and
ending it by taking the heart out of what you summoned makes the spirit one more thing in the
forest with loot in it. One press instead of a fight was already trying to avoid that, and
the item's own description was undoing it.

Read as a home, the rest falls into place without a line of code changing. The stowing post
is not built out of a spirit, it is built for one, which is the answer to why a post that
sorts your chests is worth an hour of greydwarfs when a chest is worth ten wood. Something is
doing the sorting. Taking the post down gives the heartwood back, because you have not
destroyed a home, you have moved out. And one spirit makes exactly one heartwood, always,
because a spirit with five homes is in none of them.

### Why the spirit carries things instead of teleporting them

Because you can stand and watch it, and because a courier that walked would need to know
about doorways, stairs and the chest being behind a pillar. It is a spirit is a better answer
to why it does not than any amount of pathfinding.

The important part is that the stack never leaves the post until the trip lands. The spirit
carries a reservation and a picture of the item, not the item. A run is entirely in memory,
so anything can end it: the zone unloads, you log out, the game is killed. In every one of
those cases the stack is still sitting in the post where you dropped it, because it was never
anywhere else. The alternative is simpler by a dozen lines and loses a stack of black metal
to a crash.

### Why the rules live on the chest

On the chest itself, as a plain string. That is the only place it can live and still be true:
it saves with the world rather than with a config file, it travels to everyone on a server
without any syncing of our own, and a chest that gets torn down takes its rule with it. A
config file keyed by position would have to guess at all three.

### Why every item asks, rather than every chest

Walking the chests is the obvious loop and it is the wrong one. Whichever chest is asked
first gets first refusal, so the answer would depend on the order chests happened to be found
in. Asking the item makes the outcome a property of your rules.

### Why groups are read from the game

Ticking "ore" is one press. Naming eleven ores by hand is eleven presses and is wrong the
moment there are twelve.

Wood is the one that needs explaining. A charcoal kiln is a smelter as far as the game is
concerned, so its wood-to-coal recipes had already made every log an "ore". Technically true
and useless to someone deciding what a chest holds. Anything whose smelted output is a fuel
is firewood, not ore.

### Why sowing is tied to the skill

There are already mods that put a grid over your field. They solve placement and hand you the
whole thing from your first carrot. This gives you nothing you have not farmed for, which is
the entire point: Valheim's Farming skill raises steadily and then pays out nothing.

Roof, heat and cold are deliberately not checked when sowing, because vanilla does not check
them at placement either. Leaving them out is what makes a sown plant behave identically to a
hand-planted one put somewhere shady.

## Technical notes

Everything is a text file. No asset bundle and no Unity editor: the models are `.obj` read at
runtime, the icons are `.png` beside the dll, and the colliders are a text sidecar. The
surfaces are borrowed off vanilla prefabs rather than authored, so they match the game by
construction and survive its updates.

That constraint is why the spirit has no body. A creature needs a rig and an animation
controller and neither can be authored here, so the spirit is light and motion instead: four
things reading one number, which is how awake it is.

The carrying spirit costs no networked object. Registering one would mean a name frozen
forever, and renaming it later destroys every one in every save, for something that needs no
persistence at all. What travels instead is the trip: where from, where to, what is being
carried, when it started and how long it takes. Every client draws the arc locally from the
game's shared clock, which is one network write per leg rather than one per frame.

Bonemeal moves the plant's own planted moment rather than running a timer of its own, so
growth stays the game's. A modded crop is advanced by a third of its own season without this
mod knowing it exists, and the effect survives a reload because it was written to the plant
rather than held in memory. The extra harvest travels down the same channel the Farming
skill's own bonus yield uses, so world drop scaling and anything else a plant drops keep
working.

The stowing post is cloned from the vanilla wood chest, which is what carries the container,
the placement rules and the wear that make it a real buildable piece, and then given a
hand-modelled body. Nothing vanilla is grafted on; only the materials are borrowed, group by
group, so the mesh is ours and the surfaces are the game's.

`tools/` holds the Blender scripts that produce every model, including the ones that lost.

### Upgrading from Stow or Furrow

Both used to be separate mods and now ship inside this one, unchanged. Pieces keep their
internal names, so posts already standing survive, and both keep their own settings files, so
the rules and options you had are the ones you still have.

Delete `BepInEx/plugins/Stow` and `BepInEx/plugins/Furrow` if you are upgrading by hand. Two
copies loaded at once is the one thing that will go wrong.

Their history is in `archive/`, one git bundle each. See `archive/README.md`.
