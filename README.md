# Vaettir

Something grows where you have killed enough. Speak to it once, and it comes home with you.

Plant an ancient seed in the Black Forest. It does not grow on time — it grows on death,
and only on the deaths of greydwarfs, and only within sight of it. About thirty of them and
it opens into a forest spirit — one raid, if the forest comes to you. Press use once and the spirit folds itself into a piece of
heartwood for you to carry, and you go and build it somewhere to live.

## Why a seed that ignores time

Valheim's plants are timers. You put a carrot in the ground and the world's clock does the
rest, which is the right design for a carrot and an empty one for a ritual — a thing that
happens whether or not you are there has not been earned by anybody.

So the ancient sapling keeps a count instead of a clock. It is fed by kills near it, which
makes the whole thing a place you have to keep coming back to and keep clearing out. The
work is the point. Nothing about it happens while you are asleep.

**Greydwarfs specifically, not "forest creatures".** An early version used the game's own
`ForestMonsters` faction and it swept up trolls, boars and the Elder along with them, which
turned a quest about clearing out a nest into "kill anything, anywhere near here". A named
list is duller to write and much better to play. Greylings are on the list at zero, so it
is visible that they were considered and refused — they are the free kill, and letting them
count would make the whole thing a matter of standing still.

## Why it can be destroyed

Because you are being asked to fight for an hour beside it, and something you cannot lose
is not something you are defending.

It has real health — about ten hits from a brute — so a fight happening around it is
survivable and a mob left to work on it is not. Losing it costs the ancient seed, which is
not refunded, and however long you had already spent. It tells you when something is
hitting it and it tells you when it is gone, because losing an hour in silence is a mystery
rather than a difficulty.

The first version inherited the donor's health, which is a carrot's, and the first
greydwarf that swung anywhere near one ended the ritual. That was found by playing.

## The heartwood is a home, not a heart

The spirit does not die and hand over a piece of itself. It folds itself into the heartwood
and you carry it somewhere.

This started as "it gives up its heart", which was wrong in a way that took a while to
name. The whole chain up to that point is violent — an hour of killing to summon something
— and ending it by taking the heart out of what you summoned makes the spirit one more
thing in the forest with loot in it. One press instead of a fight was already trying to
avoid that, and the item's own description was undoing it.

Read as a home, the rest of the chain falls into place without a line of code changing:

- The stowing post is not built **out of** a spirit, it is built **for** one. That is the
  answer to why a post that sorts your chests is worth an hour of greydwarfs when a chest
  is worth ten wood. Something is doing the sorting.
- Taking that post down gives the heartwood back. You have not destroyed a home, you have
  moved out.
- One spirit makes exactly one heartwood, and always will. A spirit with five homes is in
  none of them.

## What it costs you

| | |
| --- | --- |
| Plant | One ancient seed, not refunded |
| Feed | ~30 greydwarfs, or fewer at a nest — elites are worth four, shamans three |
| Range | Kills count within 24m, and only the nearest sapling is fed |
| Yield | One heartwood |

Everything above is configurable, including which creatures count and what each is worth.
A mod that adds a new greydwarf can be added to the list without touching this one.

## The stowing post

Drop things into the stowing post, close it, and a spirit carries them to the chests that
asked for them. That post is the home the whole chain is building toward, and it ships here.

It costs 20 fine wood, 20 iron nails and one heartwood. The nails put it past the forge
rather than in the first camp: a post that sorts a storage room should arrive when there is
a storage room to sort.

**It is a real container**, six slots by two, and that one decision removes most of the
surface. There is no panel listing your pack to tick through, no selection to remember and
no keybind — the interface is the chest window you already know, and "which items" is
answered by which items you dropped in. It empties on **close** rather than continuously, so
two half-stacks can be dropped in and merge; otherwise the first would be gone before you
let go of the second. Anything with no home **stays in the post**, and the hover text says
how many: a post holding six things is a post telling you six things need a chest.

**The heartwood is visible in the piece**, housed at the top behind three walls with the
front open, so its light is thrown forward and the post reads from across a room as a lit
opening. That is also where the spirit comes from. None of that is hardcoded to the model:
the runtime finds the `core` material group in whatever mesh the post is wearing, takes its
centre, and hangs the light, the halo and the spirit's home on that point.

### The spirit carries it, stack by stack

Closing the post does not teleport its contents. The spirit rises off it, takes one stack,
carries it to the chest that asked for it, drops it in, and comes back for the next. A full
post takes a minute or two to clear and you can stand and watch it. It flies, in an arc,
over whatever is in the way — a courier that walked would need to know about doorways,
stairs and the chest being behind a pillar, and *it is a spirit* is a better answer to why
it does not than any amount of pathfinding.

**The stack never leaves the post until the trip lands.** The spirit carries a reservation
and a picture of the item, not the item. That is not a detail, it is what makes the whole
feature safe: a run is entirely in memory, so anything can end it — the zone unloads, you
log out, the game is killed — and in every one of those cases the stack is still sitting in
the post where you dropped it, because it was never anywhere else. The alternative is
simpler by a dozen lines and loses a stack of black metal to a crash.

Every trip re-decides on landing, because in the seconds a run takes a chest can fill up, be
torn down, be warded, or be opened by somebody else. A post also resumes on its own, looking
for work every few seconds, so quitting halfway through and coming back later carries on.

**Everyone sees it, and it costs no networked prefab.** Registering a spirit with ZNetScene
would mean a name frozen forever — rename it later and every one in every save is destroyed
— and a spirit needs no persistence at all. So what travels is the *trip*: from, control,
to, cargo, start and duration, published on the post's own ZDO. Every client computes the
arc locally from the game's shared clock, which makes it one network write per leg rather
than per frame, and smooth everywhere because nothing is interpolated across the wire.

Turn `CarrierEnabled` off and the post moves everything the instant you close it. The
sorting is identical either way; only the waiting changes.

### Telling a chest what it holds

Open the chest. The window has a **Holds…** button under the game's own Stack all, cloned
from it so it carries the same skin, font, hover states and click sound rather than being a
rectangle a mod drew.

A chest holds *groups* — ore, fuel, seeds, building materials — and, where a group will not
do, single items. Chests with a rule say so in their hover text, in gold.

A rule can also **refuse**. Every group cell cycles ignored → holds → refused, and a search
result is shift-clicked to refuse rather than hold, so "ore, but never tin" is two presses.
Refusals are resolved before everything else, including before the contents fallback below,
otherwise the one case you would reach for it in is the case it would not work. An exclusion
subtracts but never *configures*: a chest whose only rule is "not tin" still matches on what
it already holds, because flipping it to configured would quietly mean "and nothing else
either".

### Groups, not lists

The groups are not written down anywhere in this mod. They are read off the game at runtime,
from the seam that already defines each one:

| Group | Is whatever… |
| --- | --- |
| Ore | any smelter accepts |
| Bars & ingots | any smelter produces |
| Fuel | burns in a smelter, kiln or fireplace |
| Wood | a charcoal kiln eats |
| Raw food | a cooking station accepts |
| Cooked food | fills you up (`m_food`) |
| Mead & potions | a fermenter turns out |
| Seeds & crops | the cultivator can plant |
| Building materials | the hammer asks for |
| Trophies, ammo, fish, gear | the item says it is |

So a mod that adds black metal ore, or a new crop, lands in the right group without this mod
knowing it exists. Ticking "ore" is one press; naming eleven ores by hand is eleven presses
and is wrong the moment there are twelve.

Wood is the one that needs explaining. A charcoal kiln *is* a Smelter, so its wood-to-coal
recipes had already made every log an "ore" — technically true and useless to someone
deciding what a chest holds. Anything whose smelted output is a fuel is firewood, not ore.

### Which chest wins

Every item asks which chest wants it most, rather than every chest asking what it wants.
Walking the chests is the obvious loop and it is the wrong one: whichever chest is asked
first gets first refusal, so the answer would depend on the order chests happened to be
found in. Asking the item makes the outcome a property of your rules.

In order:

1. a chest that names that exact item
2. a chest that holds a group it belongs to
3. a chest set to **anything else**
4. a chest with **no rule at all** that already holds some

Ties go to the nearer chest, and the most specific matches are placed first, so the chest
that actually asked for a thing is filled before a catch-all eats the shelf space it needed.

Tier 4 is what makes the mod useful the moment it is installed, before anything is
configured. A *configured* chest never gets it: saying "this one holds ore" and then finding
nails in it because nails happened to be in there already would make labelling a chest worse
than not labelling it.

### What it will not touch

Wards, privacy locks, chests someone else has open, and chests on carts and ships are all
skipped. A shortcut that quietly ignores a ward is a duplication exploit, not a convenience.
Other posts are skipped too — a post is a sorting table, not a destination.

### The post's own panel

The same button that opens a chest's rule opens the post's settings, in two tabs.

**Fetch** is stowing run backwards: name what this post should have, and spirits bring it in
from the chests around it. It takes the same rule format, refusals included. A post asking
for ore and a chest offering to hold ore are the same sentence pointed in opposite
directions, so they get the same controls.

**Tidy** moves items that are sitting in a chest whose rule actively refuses them. It only
ever corrects a mistake: an item nobody refuses is left exactly where it is, however untidy
it looks, so it cannot churn a room that is already right.

**Presence** decides whether the spirit only appears when there is work, or lives at the
heartwood and rests there between runs.

All three are stored on the post's own ZDO rather than in config, so two posts in two rooms
can be set differently, the settings travel to everyone on a server without any syncing of
our own, and a post that is torn down takes them with it.

Errands are done in order: stow, then fetch, then tidy. A post that spent its spirits
tidying while a full post sat waiting would feel broken even though every trip was useful.

### Where the rule lives

On the chest's own ZDO, as a plain string like `@ore,@bars,Coal`.

That is the only place it can live and still be true: it saves with the world rather than
with a config file, it travels to everyone on a server without any syncing of our own, and a
chest that gets torn down takes its rule with it. A config file keyed by position would have
to guess at all three.

### How the post is made

Cloned from `piece_chest_wood`, because the clone is what carries the Container, Piece,
WearNTear and placement rules that make it a real buildable chest. It is then given a
**hand-modelled body** in place of the donor's own: four designs were built and rendered,
and `PostModelFile` names which one it wears, so swapping it costs an edit rather than a
rebuild.

Nothing vanilla is grafted on. Only the *materials* are borrowed, group by group — the mesh
is ours and the surfaces are the game's, so texel density and palette match by construction
without the piece being a crate the game already has.

`KeyStow` and `KeyConfigure` still exist in the config for anyone who would rather not build
anything, both unbound.

### Stow used to be a separate mod

It was folded in because it was never really separate.
Its post has a socket in the model for a heartwood it could not obtain on its own, and the
spirit that does the carrying is born at that heartwood and returns to it — so Stow standing
alone was a post with a hole in it and a spirit with no origin. It built and ran; it did not
cohere.

The copy proved the point. Each mod generated its own spirit from its own Blender script,
and they had already drifted before anyone restyled anything: one ring of seven beads at a
0.21 orbit against two crossed rings of six at 0.34, separately jittered. The same
character — raised at your sapling, folded into the heartwood, coming back out to move a
crate — was visibly two different creatures depending on which mod happened to draw it.
There is one spirit now, from one script, and it is the same one throughout.

What did not change is anything that would cost you something. The piece is still
`stow_post` down to the byte, because ZNetScene keys on the prefab name and **discards any
ZDO whose name no longer resolves** — a rename would have silently destroyed every post
already standing in every world. Stow keeps its own plugin GUID and its own
`ezomic.valheim.stow.cfg`, so it still appears as its own entry in the mod list and reads
the settings you already have. Only the DLL it lives in moved.

`CoupleToStow` still turns the heartwood off, and the post goes back to costing wood and
nails. That option is kept deliberately: wanting the sorting without the ritual is a
reasonable thing to want, and it used to be answered by not installing a second mod.

## Sowing a rank at a time

Sow a whole row of seeds in one click, as many as your Farming skill has earned. At level 0
it does nothing at all — one click, one seed, exactly as vanilla. The second seed arrives
when the skill does, and the twentieth only near the top of it.

```
level    0   20   40   60   80  100
seeds    1    5   10   15   20   20
```

There are already mods that put a grid over your field. They solve placement and hand you
the whole thing from your first carrot. This gives you nothing you have not farmed for,
which is the entire point: Valheim's Farming skill raises steadily and then pays out
nothing.

Both numbers are config. `MaxSeeds` sets the ceiling, `MaxAtLevel` sets where it is reached,
and the count between is `floor(1 + (MaxSeeds - 1) * min(level / MaxAtLevel, 1))`. Two
numbers rather than a table of twenty thresholds, because a table that long is not something
anyone edits, it is something they give up on.

**Trees are configured separately.** A tree sapling is not a carrot with a different mesh —
its `m_growRadius` is several times a crop's, so twenty saplings is a stand of forest per
click, most of which lands somewhere it cannot grow. Trees get their own ceiling, defaulting
to five. A sapling is detected as any plant that grows into something carrying a `TreeBase`,
read off the component rather than a name list, so a modded sapling is covered the day it is
added and a renamed vanilla one never falls through.

**Two shapes**, switchable in game. *Row* lays the seeds in a line across your facing, so
you sow a rank and step forward. *Circle* rings them around the seed under your cursor, with
the radius derived from the count so neighbours always sit one spacing apart along the arc —
three seeds make a tight triangle, twenty make a wide ring, and in neither case do two land
on top of each other. Spacing is the plant's own `m_growRadius`, doubled: the same distance
the game itself refuses to plant inside, so carrots pack tight, firs stand well apart, and
no single number in config has to suit both.

### What it does not do

**It does not skip a rule.** Every extra seed is checked for no-build zones, other players'
wards, biome, cultivated ground and space, against the game's own `HaveGrowSpace` logic with
the same mask and the same radius. Anything that fails is quietly dropped rather than placed
badly, so a click near the edge of a field sows the part of the rank that fits.

Roof, heat and cold are deliberately **not** checked, because vanilla does not check them at
placement either. They are growth statuses a planted seed reports for itself, and leaving
them out is what makes a sown plant behave identically to a hand-planted one that was put
somewhere shady.

**It does not make seeds cheaper.** Twenty seeds costs twenty seeds, paid through the game's
own `ConsumeResources`, and it stops early if the pack runs out. Nor is skill cheaper: each
seed raises Farming once, exactly as if you had placed them one at a time. That does
compound — more skill means more seeds per click, and more seeds per click means more skill
— and the counter is simply that seeds are a real cost.

### Keys

All rebindable, defaults on the numpad, and only read while the cultivator is actually out.

| Key | What it does |
| --- | --- |
| Numpad + | One more seed per click, up to what your level allows |
| Numpad - | One fewer, down to one |
| Numpad * | Switch between row and circle |

The scroll wheel would be the obvious binding and is deliberately not used: vanilla already
spends it rotating the placement ghost, and fighting it means the count changes whenever you
meant to turn a sapling.

### How it works

One postfix, on `Player.TryPlacePiece`. The seed under your cursor is placed by vanilla,
validated by vanilla, paid for by vanilla and credited by vanilla; everything added here is
additional, and every extra seed goes in through `Player.PlacePiece`, the same call the game
makes for a hand-placed one.

The one place it cannot ride a vanilla seam is validation. `Player.UpdatePlacementGhost`
works from a camera ray through `PieceRayTest`, so it can only ever answer for the point
under the cursor, and there is no entry point that asks "is this arbitrary position valid".
Those checks are therefore hand-written in `Sowing.CanSow`, each one mirroring the game's
own: biome and cultivated ground read exactly as `Plant.UpdateHealth` reads them, and the
space test is `Plant.HaveGrowSpace` with the same mask and the same radius.

This half is client-side. Nothing in it changes a prefab, an item or a ZDO, so the server
sees ordinary plants and needs to know nothing about it. A player without the mod simply
plants one seed at a time.

**This one is here for a worse reason than the rest of the mod, and it is worth saying so.**
The stowing post belongs because it houses the spirit; the sapling and the spirit are one
chain. Sowing by skill has no heartwood in it and no spirit — it was a separate mod called
Furrow, and it is here because maintaining a package for four source files is not worth it,
not because the fiction asked for it. Its plugin GUID and `ezomic.valheim.furrow.cfg` are
unchanged, so it is still its own line in your mod list and its own settings file.

The one place the two halves touch is a happy accident. Furrow decides what may be
multi-sown by looking for a `Plant` component, and the ancient sapling has its `Plant`
destroyed on purpose — that timer is the whole reason this mod has its own growth
component. So one click never plants several ancient seeds, and no special case was needed
to arrange that.

## Everything is a text file

No asset bundle and no Unity editor. The models are `.obj` files read at runtime, the
sapling's icon is a `.png` beside the dll, and the colliders are a text sidecar. The
surfaces are borrowed off vanilla prefabs rather than authored, so they match the game by
construction and survive its updates.

That constraint is why the spirit has no body. A creature needs a rig and an animation
controller and neither can be authored here, so the spirit is light and motion instead —
four things reading one number, which is how awake it is. It notices you coming.

`tools/` holds the Blender scripts that produce every model, including the ones that lost.


## Core is optional

Vaettir installs and runs on its own. [Core](https://github.com/Ezomic/valheim-core) is a
**soft** dependency: present, it is used; absent, nothing here is degraded. Installing
Vaettir from Thunderstore no longer installs Core with it.

What Core adds is the **version gate** — a handshake that compares mod versions and build
ids on connect and refuses a client that does not match. Without it nothing refuses a client that lacks the mod, and this registers prefabs into `ZNetScene`: a client that cannot resolve one **discards the ZDO rather than erroring**, destroying what is already standing in the world.

Solo, none of that applies and Core is not needed at all.

## Configuration

`BepInEx/config/ezomic.valheim.vaettir.cfg`. BepInEx writes that file on first run, and
**from then on the saved value beats any new default in code** — so changing a default here
does nothing on a machine that has already run the mod. Edit the cfg.

`TestMode` under `[Diagnostics]` drops a sapling's cost to three greydwarfs so the whole
chain can be walked in a minute. It announces itself in the log on every startup, because
it is the setting most likely to be left on by accident.

## What is not in it yet

v1 is one chain and nothing beside it: plant, feed, commune, carry it home.

### The warden post

Right now a spirit has exactly one place to live, which means the choice this document
spends a whole section arguing for — a home built *for* a spirit rather than out of one —
is not yet a choice at all. There is one option. The warden post is the second, and having
two is the point of building it.

A spirit housed in a warden post stops things spawning around it, the way a workbench does.
Deliberately the same mechanism and not a new one: Valheim asks
`EffectArea.IsPointInsideArea(point, PlayerBase)` before it places anything, so a post
carrying that area suppresses spawns without patching a single method, and keeps working
through game updates for free. That is the seam the rest of this mod rides and there is no
reason to leave it here.

**The point being that you no longer have to litter your land with workbenches.** That is
vanilla's answer to keeping spawns off ground you have claimed, and it is a miserable one:
a bench every twenty metres, each wanting a roof so it does not rot, each a crafting station
you never wanted there, the whole lot shoved into bushes and hidden under floors. Everybody
does it. Nobody thinks it is good. It solves a real problem by cluttering the world you
solved it for. One post covers what a scatter of benches covers, and asks to be looked at
rather than hidden.

That is also what keeps this narrow rather than generous. It would be easy, and much worse,
to ship a setting that suppresses spawns near anything the player built. This costs an hour
of greydwarfs and a spirit, per post, in one place. You get quiet ground exactly where you
were willing to earn it and nowhere else.

**It upgrades on death, the way the sapling grew on death.** The post is fed by the same
kills at the same weights through the same code that feeds a sapling, so no second currency
is invented and no second list is maintained — clear greydwarfs around your grove and its
reach widens, stop and it stays where it is. The mod has exactly one verb and this is that
verb again, pointed at a different thing.

Three tiers, each costing more death than the last. The numbers are not settled, and the
first thing to measure is the vanilla workbench's own radius: the bottom tier has to be
worth more than the bench it replaces, or there is no reason to prefer it on the first day
you build one.

The rest follows rules already here. It costs the heartwood, so a spirit in a warden post is
a spirit not in a stowing post — that is the decision, and it is the first one this mod has
ever asked for. Taking the post down gives the heartwood back, because you have moved out
rather than destroyed a home.

Open question: whether the feeding survives being taken down and put back up. Carrying it
makes the post movable, which is kind. Losing it makes where you first put it matter, which
is more in keeping with everything else here.

### Shelved, not deleted

Two pieces were built and then cut back out rather than shipped half-finished.

- **A vaettr visitor.** Once you have housed a spirit, someone comes to your door selling
  ancient seeds — so a second grove is a thing the first one earned you, rather than a
  second wander through the Black Forest hoping for a seed. It is whole on the `visitor`
  branch. Vanilla's `Trader` turns out to be a dialogue system gated on global keys and not
  merely a shop, and these mods write global keys everywhere, so a visitor that reacts to
  what you have already done elsewhere is mostly configuration rather than code. That is
  what makes it worth finishing instead of deleting.
- **Somewhere to station them.** Eight market models are in `assets/variants/`, which the
  build deliberately does not copy, so they cost nothing while they wait.

Neither is scheduled. Both are on this list because shelving something without saying so is
how it gets rebuilt from scratch a year later.

The version stays at **1.0.0** until the mod actually ships. Fixes land under that number
rather than inflating it before anybody has it. Core compares build fingerprints rather than
version strings, so a stable number costs nothing in compatibility — see below.

## Multiplayer

The mod is required at both ends and registers with Core's version gate at
`Requirement.Everyone`. This is not caution: the sapling, the spirit and the heartwood are
registered prefabs, and a client that cannot resolve a prefab hash **discards the ZDO
rather than erroring** — so a server without Vaettir silently destroys every sapling
already standing in the world.
