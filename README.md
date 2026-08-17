# Vaettir

Something grows where you have killed enough. Speak to it once, and it comes home with you.

Plant an ancient seed in the Black Forest. It does not grow on time, it grows on death, and
only on the deaths of greydwarfs, and only within sight of it. About thirty of them and it
opens into a forest spirit, which is one raid if the forest comes to you. Press use once and
the spirit folds itself into a piece of heartwood for you to carry, and you go and build it
somewhere to live.

## Why a seed that ignores time

Valheim's plants are timers. You put a carrot in the ground and the world's clock does the
rest, which is the right design for a carrot and an empty one for a ritual. A thing that
happens whether or not you are there has not been earned by anybody.

So the ancient sapling keeps a count instead of a clock. It is fed by kills near it, which
makes the whole thing a place you have to keep coming back to and keep clearing out. The
work is the point. Nothing about it happens while you are asleep.

Greydwarfs specifically, not "forest creatures". An early version used the game's own forest
faction and it swept up trolls, boars and the Elder along with them, which turned a quest
about clearing out a nest into "kill anything, anywhere near here". A named list is duller to
write and much better to play. Greylings are on the list at zero, so it is visible that they
were considered and refused. They are the free kill, and letting them count would make the
whole thing a matter of standing still.

## Why it can be destroyed

Because you are being asked to fight for an hour beside it, and something you cannot lose is
not something you are defending.

It has real health, about ten hits from a brute, so a fight happening around it is survivable
and a mob left to work on it is not. Losing it costs the ancient seed, which is not refunded,
and however long you had already spent. It tells you when something is hitting it and it
tells you when it is gone, because losing an hour in silence is a mystery rather than a
difficulty.

The first version inherited the donor's health, which is a carrot's, and the first greydwarf
that swung anywhere near one ended the ritual. That was found by playing.

## The heartwood is a home, not a heart

The spirit does not die and hand over a piece of itself. It folds itself into the heartwood
and you carry it somewhere.

This started as "it gives up its heart", which was wrong in a way that took a while to name.
The whole chain up to that point is violent, an hour of killing to summon something, and
ending it by taking the heart out of what you summoned makes the spirit one more thing in the
forest with loot in it. One press instead of a fight was already trying to avoid that, and
the item's own description was undoing it.

Read as a home, the rest of the chain falls into place without a line of code changing:

- The stowing post is not built out of a spirit, it is built for one. That is the answer to
  why a post that sorts your chests is worth an hour of greydwarfs when a chest is worth ten
  wood. Something is doing the sorting.
- Taking that post down gives the heartwood back. You have not destroyed a home, you have
  moved out.
- One spirit makes exactly one heartwood, and always will. A spirit with five homes is in
  none of them.

## What it costs you

| | |
| --- | --- |
| Plant | One ancient seed, not refunded |
| Feed | About 30 greydwarfs, or fewer at a nest. Elites are worth four, shamans three |
| Range | Kills count within 24m, and only the nearest sapling is fed |
| Yield | One heartwood |

Everything above is configurable, including which creatures count and what each is worth. A
mod that adds a new greydwarf can be added to the list without touching this one.

## The stowing post

Drop things into the stowing post, close it, and a spirit carries them to the chests that
asked for them. That post is the home the whole chain is building toward.

It costs 20 fine wood, 20 iron nails and one heartwood. The nails put it past the forge
rather than in the first camp: a post that sorts a storage room should arrive when there is a
storage room to sort.

It is a real container, six slots by two, and that one decision removes most of the surface.
There is no panel listing your pack to tick through, no selection to remember and no keybind.
The interface is the chest window you already know, and "which items" is answered by which
items you dropped in. It empties on close rather than continuously, so two half-stacks can be
dropped in and merge; otherwise the first would be gone before you let go of the second.
Anything with no home stays in the post, and the hover text says how many. A post holding six
things is a post telling you six things need a chest.

The heartwood is visible in the piece, housed at the top behind three walls with the front
open, so its light is thrown forward and the post reads from across a room as a lit opening.
That is also where the spirit comes from. None of that is fixed to one model: the runtime
finds the glowing part of whatever mesh the post is wearing and hangs the light, the halo and
the spirit's home on it.

### The spirit carries it, stack by stack

Closing the post does not teleport its contents. The spirit rises off it, takes one stack,
carries it to the chest that asked for it, drops it in, and comes back for the next. A full
post takes a minute or two to clear and you can stand and watch it. It flies, in an arc, over
whatever is in the way. A courier that walked would need to know about doorways, stairs and
the chest being behind a pillar, and *it is a spirit* is a better answer to why it does not
than any amount of pathfinding.

The stack never leaves the post until the trip lands. The spirit carries a reservation and a
picture of the item, not the item. That is not a detail, it is what makes the whole feature
safe: a run is entirely in memory, so anything can end it. The zone unloads, you log out, the
game is killed, and in every one of those cases the stack is still sitting in the post where
you dropped it, because it was never anywhere else. The alternative is simpler by a dozen
lines and loses a stack of black metal to a crash.

Every trip re-decides on landing, because in the seconds a run takes a chest can fill up, be
torn down, be warded, or be opened by somebody else. A post also resumes on its own, looking
for work every few seconds, so quitting halfway through and coming back later carries on.

Everyone sees it, and it costs no networked prefab. Registering a spirit with the game's
object scene would mean a name frozen forever, and renaming it later destroys every one in
every save, for something that needs no persistence at all. So what travels is the trip:
where from, where to, what is being carried, when it started and how long it takes. Every
client draws the arc locally from the game's shared clock, which is one network write per leg
rather than one per frame, and smooth everywhere because nothing is interpolated across the
wire.

Turn off the carrier and the post moves everything the instant you close it. The sorting is
identical either way; only the waiting changes.

### Telling a chest what it holds

Open the chest. The window has a "Holds…" button under the game's own Stack all, cloned from
it so it carries the same skin, font, hover states and click sound rather than being a
rectangle a mod drew.

A chest holds groups, like ore, fuel, seeds or building materials, and where a group will not
do, single items. Chests with a rule say so in their hover text, in gold.

A rule can also refuse. Every group cell cycles ignored, holds, refused, and a search result
is shift-clicked to refuse rather than hold, so "ore, but never tin" is two presses. Refusals
are resolved before everything else, including before the contents fallback below, otherwise
the one case you would reach for it in is the case it would not work. An exclusion subtracts
but never configures: a chest whose only rule is "not tin" still matches on what it already
holds, because flipping it to configured would quietly mean "and nothing else either".

### Groups, not lists

The groups are not written down anywhere in this mod. They are read off the game at runtime,
from the thing that already defines each one:

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
knowing it exists. Ticking "ore" is one press. Naming eleven ores by hand is eleven presses
and is wrong the moment there are twelve.

Wood is the one that needs explaining. A charcoal kiln is a smelter as far as the game is
concerned, so its wood-to-coal recipes had already made every log an "ore". Technically true
and useless to someone deciding what a chest holds. Anything whose smelted output is a fuel
is firewood, not ore.

### Which chest wins

Every item asks which chest wants it most, rather than every chest asking what it wants.
Walking the chests is the obvious loop and it is the wrong one: whichever chest is asked
first gets first refusal, so the answer would depend on the order chests happened to be found
in. Asking the item makes the outcome a property of your rules.

In order:

1. a chest that names that exact item
2. a chest that holds a group it belongs to
3. a chest set to anything else
4. a chest with no rule at all that already holds some

Ties go to the nearer chest, and the most specific matches are placed first, so the chest
that actually asked for a thing is filled before a catch-all eats the shelf space it needed.

Tier 4 is what makes the mod useful the moment it is installed, before anything is
configured. A configured chest never gets it: saying "this one holds ore" and then finding
nails in it because nails happened to be in there already would make labelling a chest worse
than not labelling it.

### What it will not touch

Wards, privacy locks, chests someone else has open, and chests on carts and ships are all
skipped. A shortcut that quietly ignores a ward is a duplication exploit, not a convenience.
Other posts are skipped too. A post is a sorting table, not a destination.

### The post's own panel

The same button that opens a chest's rule opens the post's settings, in two tabs.

Fetch is stowing run backwards: name what this post should have, and spirits bring it in from
the chests around it. It takes the same rule format, refusals included. A post asking for ore
and a chest offering to hold ore are the same sentence pointed in opposite directions, so
they get the same controls.

Tidy moves items that are sitting in a chest whose rule actively refuses them. It only ever
corrects a mistake. An item nobody refuses is left exactly where it is, however untidy it
looks, so it cannot churn a room that is already right.

Presence decides whether the spirit only appears when there is work, or lives at the
heartwood and rests there between runs.

All three are stored on the post itself rather than in config, so two posts in two rooms can
be set differently, the settings travel to everyone on a server without any syncing of our
own, and a post that is torn down takes them with it.

Errands are done in order: stow, then fetch, then tidy. A post that spent its spirits tidying
while a full post sat waiting would feel broken even though every trip was useful.

### Where the rule lives

On the chest itself, as a plain string like `@ore,@bars,Coal`.

That is the only place it can live and still be true. It saves with the world rather than
with a config file, it travels to everyone on a server without any syncing of our own, and a
chest that gets torn down takes its rule with it. A config file keyed by position would have
to guess at all three.

### How the post is made

Cloned from the vanilla wood chest, because the clone is what carries the container, the
placement rules and the wear that make it a real buildable piece. It is then given a
hand-modelled body in place of the donor's own. Four designs were built and rendered, and
config names which one it wears, so swapping it costs an edit rather than a rebuild.

Nothing vanilla is grafted on. Only the materials are borrowed, group by group. The mesh is
ours and the surfaces are the game's, so texel density and palette match by construction
without the piece being a crate the game already has.

There are keybinds for stowing and configuring in the config for anyone who would rather not
build anything, both unbound by default.

### Upgrading from Stow

Stow used to be a separate mod and now ships inside this one. Its post, its spirit and its
sorting are unchanged, and so is everything you have built: the piece keeps its internal
name, so posts already standing survive. It also keeps its own settings file, so the rules
and options you had are the ones you still have.

Delete `BepInEx/plugins/Stow` if you are upgrading by hand. Two copies of it loaded at once
is the one thing that will go wrong.

If you want the sorting without the ritual, one setting drops the heartwood from the recipe
and the post goes back to costing wood and nails.

## Sowing a rank at a time

Sow a whole rank of seeds in one click, as many as your Farming skill has earned. At level 0
it does nothing at all: one click, one seed, exactly as vanilla. The second seed arrives when
the skill does, and the twentieth only near the top of it.

```
level    0   20   40   60   80  100
seeds    1    5   10   15   20   20
```

There are already mods that put a grid over your field. They solve placement and hand you the
whole thing from your first carrot. This gives you nothing you have not farmed for, which is
the entire point. Valheim's Farming skill raises steadily and then pays out nothing.

Both numbers are config: the ceiling, and the level where the ceiling is reached. Two numbers
rather than a table of twenty thresholds, because a table that long is not something anyone
edits, it is something they give up on.

Trees are configured separately. A tree sapling is not a carrot with a different mesh. It
needs several times the room, so twenty saplings is a stand of forest per click, most of which
lands somewhere it cannot grow. Trees get their own ceiling, defaulting to five. A sapling is
recognised by what it grows into rather than by a list of names, so a modded one is covered
the day it is added and a renamed vanilla one never falls through.

Two shapes, switchable in game. Row lays the seeds in a line across your facing, so you sow a
rank and step forward. Circle rings them around the seed under your cursor, with the radius
taken from the count so neighbours always sit one spacing apart along the arc. Three seeds
make a tight triangle, twenty make a wide ring, and in neither case do two land on top of
each other. Spacing is the plant's own growing room, doubled, which is the same distance the
game itself refuses to plant inside. Carrots pack tight, firs stand well apart, and no single
number in config has to suit both.

### What it does not do

It does not skip a rule. Every extra seed is checked for no-build zones, other players'
wards, biome, cultivated ground and space, against the game's own logic with the same numbers.
Anything that fails is quietly dropped rather than placed badly, so a click near the edge of a
field sows the part of the rank that fits.

Roof, heat and cold are deliberately not checked, because vanilla does not check them at
placement either. They are growth statuses a planted seed reports for itself, and leaving them
out is what makes a sown plant behave identically to a hand-planted one put somewhere shady.

It does not make seeds cheaper. Twenty seeds costs twenty seeds, paid through the game's own
accounting, and it stops early if the pack runs out. Nor is skill cheaper: each seed raises
Farming once, exactly as if you had placed them one at a time. That does compound, since more
skill means more seeds per click and more seeds per click means more skill, and the counter is
simply that seeds are a real cost.

### Keys

All rebindable, defaults on the numpad, and only read while the cultivator is actually out.

| Key | What it does |
| --- | --- |
| Numpad + | One more seed per click, up to what your level allows |
| Numpad - | One fewer, down to one |
| Numpad * | Switch between row and circle |

The scroll wheel would be the obvious binding and is deliberately not used. Vanilla already
spends it rotating the placement ghost, and fighting it means the count changes whenever you
meant to turn a sapling.

This half is client-side. Nothing in it changes a piece, an item or anything saved, so the
server sees ordinary plants and needs to know nothing about it. A player without the mod
simply plants one seed at a time.

This was Furrow, which is folded in here for a plainer reason than the stowing post was.
Sowing by skill has no heartwood in it and no spirit. Four source files and no art do not earn
a package and a release cycle of their own. It keeps its own settings file.

One click never plants several ancient seeds, and no special case was needed to arrange that.
The sapling deliberately has no growth timer, and a growth timer is exactly what sowing looks
for before it multiplies anything.

## Bonemeal

Ground bone, crafted from 10 bone fragments and 2 entrails at a workbench. Entrails rather
than bone alone so it is a craft rather than a free conversion of the drop everybody is
drowning in by the Black Forest.

Used on the crop under your crosshair it brings a third of that plant's growing time forward
and doubles what it yields when picked. Three of them will finish anything.

It moves the plant's own planted moment rather than running a timer of its own, so growth
stays the game's: a modded crop is advanced by a third of its own season without this mod
knowing it exists, and the effect survives a reload because it was written to the plant
rather than held in memory. The extra harvest travels down the same channel the Farming
skill's own bonus yield uses, so world drop scaling and anything else a plant drops keep
working.

It refuses rather than being wasted. If there is no plant under the crosshair, or the one
there cannot grow where it is standing, the bonemeal is not spent.

Feeding one plant twice brings more time forward but does not raise the harvest again, so the
bounty cannot be farmed by standing over a single carrot.

## Everything is a text file

No asset bundle and no Unity editor. The models are `.obj` files read at runtime, the icons
are `.png` beside the dll, and the colliders are a text sidecar. The surfaces are borrowed off
vanilla prefabs rather than authored, so they match the game by construction and survive its
updates.

That constraint is why the spirit has no body. A creature needs a rig and an animation
controller and neither can be authored here, so the spirit is light and motion instead: four
things reading one number, which is how awake it is. It notices you coming.

`tools/` holds the Blender scripts that produce every model, including the ones that lost.

## Core is optional

Vaettir installs and runs on its own. [Core](https://github.com/Ezomic/valheim-core) is a soft
dependency. Present, it is used; absent, nothing here is degraded.

What Core adds is the version gate, a handshake that compares mod versions and build ids on
connect and refuses a client that does not match. Without it nothing refuses a client that
lacks the mod, and that matters here: the sapling, the spirit, the heartwood and the post are
all registered pieces, and a client that cannot resolve one throws the object away rather than
erroring. A server without this mod silently destroys every sapling and every post already
standing in the world.

Solo, none of that applies and Core is not needed at all.

## Configuration

`BepInEx/config/ezomic.valheim.vaettir.cfg`, with the stowing post and the sowing keeping
their own files beside it.

BepInEx writes those files on first run, and from then on the saved value beats any new
default in code. Changing a default here does nothing on a machine that has already run the
mod, so edit the cfg.

`TestMode` under `[Diagnostics]` drops a sapling's cost to three greydwarfs so the whole chain
can be walked in a minute. It announces itself in the log on every startup, because it is the
setting most likely to be left on by accident.

## What is not in it yet

A spirit has exactly one place to live, which means the choice this whole document argues for,
a home built for a spirit rather than out of one, is not much of a choice yet.

The warden post is the second one. A spirit housed in it stops things spawning around it, the
way a workbench does, and for the same reason: it uses the game's own rule about ground you
have claimed rather than inventing one.

The point is that you would no longer have to litter your land with workbenches. That is
vanilla's answer to keeping spawns off ground you have claimed and it is a miserable one. A
bench every twenty metres, each wanting a roof so it does not rot, each a crafting station you
never wanted there, the whole lot shoved into bushes and hidden under floors. Everybody does
it. Nobody thinks it is good. One post would cover what a scatter of benches covers, and asks
to be looked at rather than hidden.

That is also what keeps it narrow. It would be easy, and much worse, to ship a setting that
suppresses spawns near anything you built. This would cost an hour of greydwarfs and a spirit,
per post, in one place. Quiet ground exactly where you were willing to earn it and nowhere
else.

It would upgrade on death, the way the sapling grows on death, fed by the same kills at the
same weights. The mod has one verb and that is the verb again, pointed at a different thing.

Two more things were built and then cut back out rather than shipped half finished. A vaettr
visitor who comes to your door selling ancient seeds once you have housed a spirit, so a
second grove is something the first one earned you rather than another wander through the
Black Forest hoping for a seed. And somewhere to station them. Both are shelved rather than
abandoned, because shelving something without saying so is how it gets rebuilt from scratch a
year later.

## Multiplayer

The mod is needed at both ends. This is not caution: the sapling, the spirit, the heartwood
and the post are registered pieces, and a client that cannot resolve one throws the object
away rather than erroring, so a server without Vaettir silently destroys everything already
standing in the world.
