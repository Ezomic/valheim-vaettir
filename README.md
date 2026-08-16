# Vaettir

Something grows where you have killed enough. Speak to it once, and it comes home with you.

Plant an ancient seed in the Black Forest. It does not grow on time — it grows on death,
and only on the deaths of greydwarfs, and only within sight of it. About forty of them and
it opens into a forest spirit. Press use once and the spirit folds itself into a piece of
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
| Feed | ~40 greydwarfs, or fewer at a nest — elites are worth four, shamans three |
| Range | Kills count within 24m, and only the nearest sapling is fed |
| Yield | One heartwood |

Everything above is configurable, including which creatures count and what each is worth.
A mod that adds a new greydwarf can be added to the list without touching this one.

## With Stow

If [Stow](https://github.com/Ezomic/valheim-stow) is installed, its stowing post gains
heartwood in its recipe — which is to say, the post becomes the place the spirit lives.

The coupling lives entirely on this side. Stow is never told about Vaettir, is not
referenced as an assembly, and is found by prefab name, so Stow remains a mod that works on
its own and still builds its post out of wood and fine wood. Installing Vaettir is what
raises the price. The heartwood is *added* to that recipe rather than replacing it, because
the rest of it is Stow's setting and someone may have changed it deliberately.

Turn it off with `CoupleToStow` and Stow's recipe is left alone.

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

v1 is one chain and nothing beside it: plant, feed, commune, carry it home. Two pieces were
built and then cut back out rather than shipped half-finished, and they are the roadmap.

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
