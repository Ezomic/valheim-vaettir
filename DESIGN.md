# Vaettir design notes

Why it works the way it does. For what it does and how to use it, see the [README](README.md).

## A seed that ignores time

Valheim's plants are timers, which is the right design for a carrot and an empty one for a
ritual: a thing that happens whether or not you are there has not been earned by anybody. So the
sapling keeps a count instead of a clock, fed by kills near it, which makes it a place you have
to keep coming back to and keep clearing out. Nothing about it happens while you are asleep.

## Greydwarfs specifically, not "forest creatures"

An early version used the game's own forest faction and swept up trolls, boars and the Elder,
which turned a quest about clearing out a nest into "kill anything, anywhere near here". A named
list is duller to write and much better to play. Greylings sit on it at zero so it is visible
that they were considered and refused; letting the free kill count would make the whole thing a
matter of standing still.

## Why it can be destroyed

Because you are being asked to fight for an hour beside it, and something you cannot lose is not
something you are defending. Ten hits from a brute means a fight happening around it is
survivable and a mob left to work on it is not. The first version inherited the donor's health,
which is a carrot's, and the first greydwarf that swung anywhere near one ended the ritual. That
was found by playing.

## The heartwood is a home, not a heart

The spirit does not die and hand over a piece of itself; it folds itself into the heartwood and
you carry it somewhere. This started as "it gives up its heart", which after an hour of killing
to summon something made the spirit one more thing in the forest with loot in it. Read as a
home, the rest falls into place without a line of code changing. The post is not built out of a
spirit, it is built for one, which is the answer to why a post that sorts your chests is worth
an hour of greydwarfs when a chest is worth ten wood. Taking it down gives the heartwood back
because you have moved out rather than destroyed a home. And one spirit makes exactly one
heartwood, because a spirit with five homes is in none of them.

## Why the spirit carries things instead of teleporting them

Because you can stand and watch it, and because a courier that walked would need to know about
doorways, stairs and the chest being behind a pillar. The important part is that the stack never
leaves the post until the trip lands: the spirit carries a reservation and a picture of the
item, not the item. So anything that ends a run, the zone unloading, you logging out, the game
being killed, leaves the stack sitting in the post where you dropped it, because it was never
anywhere else. The alternative is simpler by a dozen lines and loses a stack of black metal to a
crash.

## Why the rules live on the chest

On the chest itself, as a plain string. That is the only place they can live and still be true:
they save with the world rather than with a config file, they travel to everyone on a server
without any syncing of our own, and a chest that gets torn down takes its rule with it. A config
file keyed by position would have to guess at all three.

## Why every item asks, rather than every chest

Walking the chests is the obvious loop and it is the wrong one. Whichever chest is asked first
gets first refusal, so the answer would depend on the order chests happened to be found in.
Asking the item makes the outcome a property of your rules.

## Why groups are read from the game

Ticking "ore" is one press. Naming eleven ores by hand is eleven presses and is wrong the moment
there are twelve. Wood is the one that needs explaining: a charcoal kiln is a smelter as far as
the game is concerned, so its wood-to-coal recipes had already made every log an "ore", which is
technically true and useless to someone deciding what a chest holds. Anything whose smelted
output is a fuel is firewood, not ore.

## Technical notes

Everything is a text file. No asset bundle and no Unity editor: the models are `.obj` read at
runtime, the icons are `.png` beside the dll, and the colliders are a text sidecar. The
surfaces are borrowed off vanilla prefabs rather than authored, so they match the game by
construction and survive its updates. That constraint is why the spirit has no body. A
creature needs a rig and an animation controller and neither can be authored here, so the
spirit is light and motion instead: four things reading one number, which is how awake it is.

The carrying spirit costs no networked object. Registering one would mean a name frozen
forever, and renaming it later destroys every one in every save, for something that needs no
persistence at all. What travels instead is the trip: where from, where to, what is being
carried, when it started and how long it takes. Every client draws the arc locally from the
game's shared clock, which is one network write per leg rather than one per frame.

The stowing post is cloned from the vanilla wood chest, which is what carries the container,
the placement rules and the wear that make it a real buildable piece, and then given a
hand-modelled body. Nothing vanilla is grafted on; only the materials are borrowed, group by
group, so the mesh is ours and the surfaces are the game's.

`tools/` holds the Blender scripts that produce every model, including the ones that lost.
