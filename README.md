# Vaettir

Plant an ancient seed in the Black Forest and it grows on greydwarf deaths instead of on a
timer. When it opens, a forest spirit rises out of it and folds itself into a heartwood you
carry away and build into a stowing post: a shallow container that hands its contents to the
chests around it, sorted by rules you set on each chest.

The same DLL also carries two mods that used to ship separately: Stow, which is the post and
the chest sorting, and Furrow, which is the cultivator's planting grid and area harvest. Wild
plant transplanting and a bonemeal recipe are here too.

## Features

- **Ancient sapling.** Planted with the cultivator, fed by greydwarf kills within 24m, and it
  calls greydwarfs to itself in waves while a player is nearby. It has 500 health and can be
  destroyed.
- **Forest spirit and heartwood.** One spirit per sapling, one heartwood per spirit.
- **Stowing post.** A 6x2 container on the hammer's Furniture tab. Drop things in, close it,
  and a spirit flies them to the chests that asked for them.
- **Chest rules.** A `Holds…` button in every chest window. Chests hold groups (ore, fuel,
  seeds, building materials) or single items, and can refuse things.
- **Planting grid.** From Farming 10 the cultivator's ghost snaps to a lattice so hand-placed
  beds come out in rows. A ring shows whether a sapling will actually have room.
- **Area harvest.** From Farming 15, Shift+E on a ripe crop picks its neighbours too, reaching
  further as Farming rises. Plain E still picks one.
- **Transplanting.** Dig up wild berry bushes, thistle, dandelion and mushrooms with the
  cultivator and carry them somewhere else. Gated on Farming, one plant at a time, and the
  world's plant count never changes.
- **Bonemeal.** Two bone fragments and an entrail make five at the workbench. A fed crop
  yields three times when picked and trains Farming.
- **Multi-seed sowing.** Sow a row or a circle per click, scaled by Farming. Off by default.

## The ritual

1. Kill greydwarf brutes and shamans until one drops an **ancient seed**. Vanilla loot, and
   nothing here changes the drop.
2. Plant it with the **cultivator**, in the Black Forest, at least 5m inside the biome and
   outside anybody's base. The refusal message says which of the two stopped you.
3. Kill greydwarfs **within 24m of it**. Greydwarf 1 point, shaman 3, brute 4, greyling 0, and
   50 points opens it. Only the nearest sapling is fed by a kill.
4. You do not have to go looking for them. A planted seed calls greydwarfs in from 25 to 40m
   out, 2 to 5 at a time, every 20 seconds falling to every 6 as it fills. Everyone within 48m
   is told once that the forest is enraged. It only calls while a player is within 48m, and it
   stops calling if a base grows around it.
5. **Defend it.** 500 health is about ten hits from a brute, and the seed is not refunded if it
   dies. It says when something is hitting it and when it is gone.
6. When it opens a **forest spirit** rises out of it. Press use once and it folds itself into a
   **heartwood**.
7. Build a **stowing post**: 20 fine wood, 20 iron nails and 1 heartwood, hammer, Furniture tab.
   Taking the post down gives the heartwood back.

The sapling's hover text carries its stage and its exact count, so `rooting ( 18 / 50 )` tells
you what is left. It looks the same the whole way through, so the hover text is the only thing
that reports progress. It is pinned on your own map while it stands, and the pin comes off when
it opens or dies.

## The stowing post

The post is a real container, 12 slots, and it empties when you close its window rather than
continuously, so two half-stacks dropped in can merge first. A spirit then carries the contents
out 10 items per trip, flying in an arc over whatever is in the way, and re-checks every three
seconds until the post is clear. Turn `CarrierEnabled` off and the post moves everything the
instant you close it. The sorting is identical either way.

Anything with no home stays in the post. Its hover text separates the two cases: `waiting`
means a home exists and the spirit has not got there yet, `with nowhere to go` means no chest
in range wants it.

### Telling a chest what it holds

Chest windows get a `Holds…` button under the game's own Stack all. The post has no button of
its own because it distributes outward, so the rules live on the chests. The panel offers
groups and single items, a search box for naming an exact item, and a `Learn from contents`
button that reads the chest's current contents into a rule. Clicking a group cycles it through
ignored, holds and refused, and shift-clicking a search result refuses that item instead of
holding it, so "ore, but never tin" is two presses. A chest with a rule says so in gold in its
hover text.

The rule is stored on the chest itself, so it saves with the world, travels to everyone on a
server without any syncing of ours, and disappears when the chest is torn down.

Groups are derived from the game's own data at runtime, not from a list in the mod:

| Group | Members |
| --- | --- |
| Ore | whatever any smelter accepts |
| Bars & ingots | whatever any smelter produces |
| Fuel | whatever burns in a smelter, kiln, cooking station or fireplace |
| Wood | whatever a charcoal kiln eats |
| Raw food | whatever a cooking station accepts |
| Cooked food | anything that fills you up |
| Mead & potions | whatever a fermenter takes or produces |
| Seeds & crops | whatever the cultivator's pieces cost |
| Building materials | whatever the hammer's pieces cost |
| Crafting materials | item type Material |
| Weapons & armour | equippable gear, tools and torches |
| Ammo, Trophies, Fish, Valuables | by item type |

A mod that adds black metal ore or a new crop lands in the right group without this mod knowing
it exists. Groups that end up empty are not drawn.

### How an item picks its chest

Every item asks which chest wants it most, rather than every chest being asked what it wants.
The order is: a chest naming that exact item, then one holding a group it belongs to, then one
set to "Anything else", then a chest with no rule at all that already holds some of that item
(`MatchContents`, which is what makes the post useful before you have configured anything).
Ties go to the nearer chest, and a chest you have given a rule never falls into that last tier.

The post skips chests that are out of range, warded against you, set to anything but Public,
open by anyone, on a cart or a ship, and other stowing posts.

## Farming

**Grid.** With the cultivator out and a plant selected, from Farming 10, the ghost snaps to a
lattice spaced by the plant's own grow radius. The lattice is drawn on the ground before you
plant. The mouse wheel turns it in 22.5 degree steps, `GridPinKey` (numpad period) anchors it
where you are standing instead of on the nearest plant of the same kind, and holding the game's
AltPlace key (Shift unless you rebound it) plants free of the grid entirely. One seed per press
throughout: the skill unlock is alignment, not quantity.

A ring at the plant's grow radius shows green when it would have room and red when it would
not. The game itself does not check this at placement time. It checks ten seconds later, and a
sapling with no room turns unhealthy or deletes itself with the seed already spent.

**Area harvest.** Shift+E on a ripe crop from Farming 15, reaching 2m at 15 and 8m at 80, up to
50 crops per press. Only crops are taken, meaning the grown stage of something plantable, read
off the game rather than listed in the mod. Wild berries, mushrooms, thistle and dandelion are
never touched. By default only the crop you clicked is taken, so a mixed bed comes off a kind at
a time. Every neighbour goes through vanilla's own `Interact`, so skill gain, the level bonus
roll, drop scaling and ownership are identical to picking each by hand.

**Transplanting.** Select **Transplant** on the cultivator (it sits beside the crops, and works
like the hammer's repair entry) and click a wild plant. It comes up into your arms, no item and
no inventory slot. While carrying you can walk but not run, jump, attack, use the hotbar or
equip anything. Click open ground where that kind grows to plant it, or press R to set it down
where you stand regardless of biome. Dying or logging out plants it at your feet. The same
grown bush goes back down, picked-empty because its berries dropped into your hands at the dig,
and it regrows on vanilla's own timer.

Each plant has a Farming level and a biome list:

| Plant | Farming | Grows in |
| --- | --- | --- |
| Dandelion | 5 | Meadows |
| Raspberry bush | 10 | Meadows, Black Forest |
| Mushrooms | 15 | Meadows, Black Forest |
| Blueberry bush | 25 | Black Forest |
| Thistle | 35 | Black Forest, Swamp |
| Yellow mushrooms | 45 | Black Forest |
| Cloudberry bush | 60 | Plains |
| Blue mushrooms | 75 | Mountain |

Yellow mushrooms are a burial chamber crop and `Plant` refuses anything under a roof, so they
can only be farmed in the forest above the crypt.

**Bonemeal.** `BoneFragments:2, Entrails:1` at the workbench makes 5. Use it on a growing crop
(crops only, never trees) and it marks the plant: picking the crop it becomes yields three times
and grants 5 Farming skill, once, and the mark is spent. A fed plant says "Fertilised" on hover
and a second bonemeal cannot stack the bonus. It does not speed growth up.

**Sowing.** `Sowing/Enabled` is false by default. Turned on, one click sows a row or circle of
seeds, 1 at Farming 0 rising to 20 at Farming 80 for crops and 5 for trees, spaced by each
plant's own grow radius. Numpad plus and minus change the count and numpad star cycles the shape.

## Installation

1. Install [BepInEx for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/),
   version 5.4.2350. This is BepInEx 5, not 6.
2. Put the `Vaettir` folder from the release into `BepInEx/plugins/`.

The mod is one DLL plus the `.obj`, `.col` and `.png` files beside it, which are read at
runtime. They have to stay in the same folder together. There is no asset bundle.

[Longhouse Core](https://thunderstore.io/c/valheim/p/Ezomic/Longhouse_Core/) is an optional
soft dependency. It is not needed in single player, and on a server it does the version check
described under [Multiplayer](#multiplayer).

Built against Valheim 1.0.7, Unity 6000.0.75, BepInEx 5.4.23.5 and Harmony 2.9. Version 1.5.0
and later require Valheim 1.0; 1.4.2 and earlier do not run on it.

## Configuration

One file, `BepInEx/config/ezomic.valheim.vaettir.cfg`, written on first run. Every entry carries
its reasoning as a comment in the file.

BepInEx writes every entry to disk on first run and the saved value beats any new default in
code, so changing a default in a later version does nothing on a machine that has already run
the mod. Edit the cfg.

### [Sapling]

| Key | Default | Effect |
| --- | --- | --- |
| `SaplingName` | Ancient sapling | What the planted seed is called |
| `SaplingCost` | `AncientSeed:1` | What planting costs. Not refunded |
| `SaplingDonor` | sapling_carrot | Vanilla piece it is cloned from |
| `SaplingScale` | 1 | Scale of the planted piece |
| `SaplingIcon` | grove_sapling_icon.png | Cultivator icon, read from beside the DLL |
| `SaplingHealth` | 500 | Damage it takes before it is destroyed |
| `NeedsCultivated` | false | Whether it must go in tilled soil |
| `PinSaplings` | true | Put a marker on your own map while it stands |
| `PinIcon` | Icon3 | Which vanilla map icon to use |
| `BloodNeeded` | 50 | Points of greydwarf death needed to open it |
| `FeedRange` | 24 | How close a kill must be to count, in metres |
| `FeedWeights` | `Greydwarf:1,Greydwarf_Elite:4,Greydwarf_Shaman:3,Greyling:0` | What each death is worth. Anything unlisted is worth nothing |
| `Messages` | true | Corner counter each time a kill feeds a sapling |
| `SaplingBiomes` | BlackForest | Biomes that will take a seed. Blank for anywhere |
| `BiomeMargin` | 5 | Metres inside the biome required, checked on a ring |
| `BiomeRefusal` | Too far from its own wood to grow. | Message when refused for biome |
| `NotInBases` | true | Refuse planting inside a base, and stop calling if one grows around it |
| `BaseMargin` | 8 | Extra metres on top of the game's own base radius |
| `BaseRefusal` | Too close to a hearth. This belongs in the wild. | Message when refused for a base |
| `Beckon` | true | Whether a planted seed draws greydwarfs to itself |
| `BeckonRoster` | `Greydwarf:10,Greydwarf_Shaman:3,Greydwarf_Elite:2` | What it calls, as prefab:weight |
| `BeckonInterval` | 20-6 | Seconds between waves, unfed to nearly open. Two is the floor |
| `BeckonPack` | 2-5 | How many arrive together, unfed to nearly open |
| `BeckonDistance` | 25-40 | Metres out they appear before walking in. Keep under about 70 |
| `BeckonRange` | 48 | How close a player must be for it to call at all |
| `BeckonArea` | 96 | Neighbourhood width that `BeckonMaxTotal` counts inside |
| `BeckonMaxNear` | 10 | Most it will have standing around it at once |
| `BeckonMaxTotal` | 24 | Most it will have alive in the wider area |
| `BeckonMessage` | The forest is enraged. | Said centre screen to everyone in range. Blank for none |

### [Spirit] and [Heartwood]

| Key | Default | Effect |
| --- | --- | --- |
| `SpiritName` | Forest spirit | What it is called when you look at it |
| `SpiritScale` | 1 | Scale of the whole thing |
| `SpiritRise` | 0.4 | How far above the sapling it appears |
| `MoteCount` | 6 | Beads per ring |
| `RingCount` | 2 | How many circles of beads |
| `ShowHoop` | false | Draw the torus the beads ride on |
| `PartingEffect` | `vfx_ghost_death,vfx_HealthUpgrade,vfx_DraugrSpawn` | Vanilla effect played where it stood when it goes. First name that resolves wins. Blank for none |
| `GlowDonors` | `fire_pit,piece_walltorch,bonfire,Ember,piece_groundtorch_green,guard_stone` | Prefabs to lift the glowing material from, best first |
| `HeartwoodName` | Heartwood | What the material is called |
| `HeartwoodDonor` | SurtlingCore | Vanilla item it is cloned from |
| `HeartwoodStack` | 10 | How many fit in a slot |
| `HeartwoodGiven` | 1 | How many a spirit folds itself into |

### [Post], [Sorting] and [Carrier]

| Key | Default | Effect |
| --- | --- | --- |
| `PostEnabled` | true | Put the post in the hammer's menu. The prefab is registered either way, so turning it off never deletes a post already built |
| `PostName` | Stowing post | Name of the piece and its window |
| `PostCost` | `FineWood:20,IronNails:20` | Build cost, before the heartwood is added |
| `PostWidth` / `PostHeight` | 6 / 2 | Slots across and down |
| `CoupleToStow` | true | Add the heartwood to the post's cost. Off puts it back to wood and nails |
| `StowPostCost` | `GroveHeartwood:1` | What is added to that cost. An ingredient already there is raised, not counted twice |
| `PostDonor` | piece_chest_wood | Vanilla piece the post is cloned from |
| `PostModelFile` | stow_post_canopy.obj | Mesh beside the DLL. Its `.col` and `_icon.png` are picked up automatically |
| `PostScale` | 1 | Scale of the whole piece |
| `PostLightRange` | 7 | How far the heartwood in the post throws light |
| `PostLightIntensity` | 1.15 | Brightness of that light |
| `PostFlareScale` | 0.75 | Size of the halo on the post's heartwood |
| `PostGlowDonors` | `piece_dvergr_lantern,guard_stone,piece_walltorch,fire_pit` | Prefabs to lift the post's glowing recess from |
| `LookForProps` | (blank) | Comma-separated words. Logs every loaded prop whose name contains one |
| `Range` | 12 | How far a chest may be from the post, measured through walls |
| `MatchContents` | true | Let a chest with no rule take more of what it already holds |
| `Messages` (Sorting) | true | Corner message summarising what went where |
| `CarrierEnabled` | true | Fly items out one stack at a time instead of moving everything on close |
| `Couriers` | 1 | How many spirits a post flies at once |
| `CarrierSpeed` | 2.6 | Metres per second in the air |
| `CarrierPause` | 0.5 | Seconds hovering at each end |
| `ItemsPerTrip` | 10 | Items carried per trip. 0 carries the whole stack |
| `CarrierCruise` | 1.1 | How high above the higher end it arcs |
| `CarrierScale` | 0.62 | Scale of the carrying spirit. 1 is a full-size one |
| `FlareDonors` | `piece_dvergr_lantern,guard_stone,piece_walltorch,fire_pit` | Prefabs to lift the halo off, best first |

### [Furrow], [Harvest], [Sowing], [Crops], [Trees]

| Key | Default | Effect |
| --- | --- | --- |
| `GridEnabled` | true | Snap the cultivator's ghost to a lattice |
| `GridLevel` | 10 | Farming level that unlocks the grid |
| `GridCell` | 0 | Metres between plants. 0 uses each plant's own grow radius |
| `GridAngle` | 0 | Which way the rows run, in degrees |
| `GridPreview` | true | Draw the lattice on the ground under the ghost |
| `GridPreviewRings` | 3 | How many cells the drawing reaches in each direction |
| `RoomPreview` | true | Ring at the plant's grow radius, green or red |
| `PickArea` | true | Shift+E on a ripe crop harvests its neighbours |
| `PickLevel` | 15 | Farming level that unlocks the area harvest |
| `PickAtLevel` | 80 | Farming level at which it reaches `PickRadius` |
| `PickRadiusMin` | 2 | Metres reached at `PickLevel` |
| `PickRadius` | 8 | Metres reached at `PickAtLevel`, and the most it ever reaches |
| `PickMax` | 50 | Most crops one press may take |
| `PickSameCropOnly` | true | Take only the crop you clicked |
| `Enabled` (Sowing) | false | Sow more than one seed per click |
| `Shape` | Row | Starting shape: Row, Circle or Grid |
| `RowAcrossFacing` | true | Row runs left to right across your facing |
| `Spacing` | 1 | Multiplier on the gap between sown seeds. Below 1 drops seeds |
| `Crops/MaxSeeds` | 20 | Most seeds a click can sow. 1 switches crops off |
| `Crops/MaxAtLevel` | 80 | Farming level at which `MaxSeeds` is reached |
| `Trees/MaxSeeds` | 5 | Most saplings a click can sow |
| `Trees/MaxAtLevel` | 80 | Farming level at which the tree maximum is reached |

### [Thicket] and [Plants]

| Key | Default | Effect |
| --- | --- | --- |
| `Enabled` | true | Register the wild plants and the Transplant entry |
| `Donor` | sapling_carrot | Vanilla plant each seedling prefab is cloned from |
| `Scale` | 1 | Scale of the seedling in the ground |
| `SpacingScale` | 1 | Multiplier on how much clear ground each seedling demands |
| `DigReach` | 6 | Metres a wild plant can be dug from, measured from you |
| `DigAssist` | 12 | Degrees off the crosshair a plant may sit and still be dug. 0 turns it off |
| `SayTheLevel` | true | Write the required Farming level into the menu entry and the refusal |
| `Verbose` (Thicket) | false | Log every row as it is parsed and every prefab not found |

`[Plants]` holds one row per plant, formatted
`Farming level | cost | biomes | seconds to take root (min-max)`. The level and the biome list
are live. The cost and grow time only configure the seedling prefabs, which are still
registered for anything planted before 1.2.0 but are never planted new, since a dug plant now
goes straight back down grown.

### [Bonemeal]

| Key | Default | Effect |
| --- | --- | --- |
| `BonemealName` | Bonemeal | What the item is called |
| `BonemealCost` | `BoneFragments:2,Entrails:1` | What one craft costs |
| `BonemealYield` | 5 | How many one craft produces |
| `BonemealStation` | piece_workbench | Where it is crafted. Blank makes it craftable by hand |
| `BonemealStack` | 50 | How many fit in a slot |
| `BonemealHarvest` | 3 | Multiplier on what a fertilised crop yields when picked. 1 drops the bonus |
| `BonemealSkillGain` | 5 | Farming skill granted when a fertilised crop is picked |
| `BonemealRadius` | 0 | Feed every plant within this many metres in the same press. Off at 0 |
| `BonemealAdvance` | 0.34 | Growth advance. **Not read by the code.** Its comment describes a growth speed-up that was cut before release |
| `BonemealDonor` | BarleyFlour | Vanilla item it is cloned from |
| `BonemealModel` | grove_bonemeal.obj | Mesh beside the DLL. Blank keeps the donor's model |
| `BonemealTint` | (blank) | Colour multiplied into the sack, as r,g,b |
| `BonemealIcon` | grove_bonemeal_icon.png | Inventory picture, read from beside the DLL |

### [Keys]

Every key is read through the game's own input layer, so gamepads work and a key typed into
chat, the console or a text box is ignored.

| Key | Default | Effect |
| --- | --- | --- |
| `GridFreeKey` | None | Hold to plant free of the grid. At None this is the game's AltPlace key, Shift unless rebound |
| `GridPinKey` | KeypadPeriod | Pin the lattice where the ghost is standing. Press again to unpin |
| `GridTurnKey` | None | Optional key that turns the grid by `GridTurnStep`. Setting it to Mouse2 stops middle click removing pieces while a plant is selected |
| `GridTurnScroll` | true | Turn the grid with the mouse wheel while planting |
| `GridTurnStep` | 22.5 | Degrees per press of `GridTurnKey` |
| `IncreaseKey` / `DecreaseKey` | KeypadPlus / KeypadMinus | Sow one more or one fewer seed per click |
| `ShapeKey` | KeypadMultiply | Cycle the sowing shape |
| `KeyStow` | None | Empty your pack into the chests around you, skipping the post |
| `KeyConfigure` | None | Look at a chest and press to open its rules panel |
| `KeepHotbar` | true | `KeyStow` only: leave the hotbar row alone |
| `NeverStow` | `Hammer,Hoe,Cultivator` | `KeyStow` only: prefab names that never leave your pack |

### [Diagnostics]

| Key | Default | Effect |
| --- | --- | --- |
| `TestMode` | false | Drops what a sapling needs to three greydwarfs. Announced in the log on every startup |
| `Verbose` | false | Log every feed, every sown position, and one line per chest the post skipped with the reason |
| `DumpMaterials` | false | Log every material on each glow donor with its shader |
| `LookForPrefabs` | (blank) | Comma-separated words. Logs every loaded prefab whose name contains one |

## Multiplayer

**Install it on the server and on every client.** The sapling, the spirit, the heartwood and
the post are registered prefabs, and the game discards any saved object whose prefab name does
not resolve rather than erroring. A server without Vaettir silently destroys everything already
standing, and so does a client that joins without it.

[Longhouse Core](https://thunderstore.io/c/valheim/p/Ezomic/Longhouse_Core/) is what turns that
into a refused connection. It checks each Ezomic mod's version and build id when a client
connects and the server rejects mismatches. Without Core nothing checks. In single player none
of this applies.

Core also applies the host's config values on connected clients, in memory, without writing the
client's own config file. Thirty-three entries are per-player and never imposed by the host: the
grid and sowing gestures and the grid angle, your map pins, the biome and base refusal messages,
the sorting summary, the spirit's and post's local rendering (`MoteCount`, `RingCount`,
`ShowHoop`, `PartingEffect`, `CarrierScale`, `Thicket/Scale`, the post light and flare settings
and their donor lists), `SayTheLevel`, `KeepHotbar`, `NeverStow`, and every verbose or
diagnostic toggle. Costs, ranges, health, rosters, caps and shared prefab facts stay the host's
decision, and so do three that look personal: `BeckonMessage` and the sapling's `Messages` are
read on the machine that owns the sapling and decide what other players see, and `SpiritScale`
is baked into a networked object's transform.

Saplings, spirits and posts are each driven by whoever owns them. Map pins are local to you and
saved in your own profile.

## Compatibility

- BepInEx 5 only.
- Item groups are read off the game at runtime, so a mod that adds an ore, a crop or a
  building material lands in the right group with no work.
- The area harvest rides `Pickable.Interact`'s `alt` argument, which vanilla never reads.
  Another mod that uses Shift+E on pickables will clash with it. Set `PickArea` to false.
- Setting `GridTurnKey` to Mouse2 suppresses middle-click removal while the cultivator has a
  plant selected. It is None by default, so removal is vanilla.
- A patch group that fails to apply costs one feature and names it in the log. The prefabs are
  declared before any patching, so a failure there cannot destroy anything built.

## Troubleshooting

**The post says it has nowhere to go, or the spirit never sets off.** Three separate bugs
caused this and all three are fixed in 1.5.1 through 1.5.3. Update first. The last one was a
fixed collider buffer that silently truncated the search, which meant the post could not see
any chest in a base with more than about 256 colliders within range. It passed in a test world
and failed in a real base.

If it persists, check the chest: it must be Public rather than private, within `Range` (12m by
default), not open by anybody, not warded against you, and not on a cart or ship. Turn
`Diagnostics/Verbose` on and the log names every chest it skipped and why, then counts the
usable ones.

**Items multiplied in a chest.** Fixed in 1.5.1. It affected partial takes, meaning any stack
larger than `ItemsPerTrip`, and only once trips were landing at all.

**A config change did nothing.** BepInEx wrote the file on first run and the saved value wins.
Edit `BepInEx/config/ezomic.valheim.vaettir.cfg` rather than expecting a new default to apply.

**The sapling refuses to plant.** Black Forest only by default, at least 5m inside the biome,
and outside the area any workbench or fire radiates. The message says which of the two it was.
A ward refuses it like any other piece.

**A sapling stopped calling greydwarfs.** It goes quiet while no player is within 48m, and while
it sits inside a base. Kills still feed it either way.

**The Transplant entry is missing from the cultivator.** The build menu only shows known pieces
and that list is rebuilt when you learn a recipe or a station, so a piece registered seconds
after you spawned can stay hidden. The mod nudges that update itself. If the log says it could
not read `Player.m_knownRecipes`, learning anything new will bring it back.

**A sapling needs only three greydwarfs.** `TestMode` is on. It warns on every startup.

**A vanilla mechanic broke and LogOutput.log is clean.** Gameplay exceptions land in
`AppData\LocalLow\IronGate\Valheim\Player.log`, not in the BepInEx log.

## Bug reports

[The Discord](https://discord.gg/hJzAVaZ5wb) is the fastest route and the right one if you are
not sure whether what you are seeing is a bug. Issues on
[the repo](https://github.com/Ezomic/valheim-vaettir) work too and suit anything long.

Attach `BepInEx\LogOutput.log` and say whether you were on a server or in single player. For
anything about the post, turn `Diagnostics/Verbose` on first and include the lines naming the
chests it skipped. For a vanilla mechanic breaking, include
`AppData\LocalLow\IronGate\Valheim\Player.log` as well. Your config file helps when a number
looks wrong.

## Discord

[discord.gg/hJzAVaZ5wb](https://discord.gg/hJzAVaZ5wb) is where updates, support, bug reports
and compatibility questions go.

There's also a small EU server running the pack if you want somewhere to play: hard combat
difficulty, resources at 1x, everything else vanilla, no application and no activity
requirements. Details are in the Discord.

## Upgrading from Stow

Stow shipped as a separate mod until 1.0.0 and is part of this one now. The post keeps its
internal name, so every post already standing survives. Its settings moved into this mod's
config file under `[Post]`, `[Sorting]`, `[Carrier]` and `[Keys]`. If you had tuned
`ezomic.valheim.stow.cfg`, copy those values across once and delete it, and delete
`BepInEx/plugins/Stow` if you are upgrading by hand. Two copies loaded at once is the thing
that will go wrong.

Stow's and Furrow's history is in `archive/`, one git bundle each. See `archive/README.md`.

## Design notes

Why the seed counts kills instead of ticking a clock, why the heartwood is a home rather than a
heart, why the sorting rules live on the chest, and why the spirit carries things instead of
teleporting them: [DESIGN.md](DESIGN.md).

## Building

Target is net462 against the game's own managed assemblies, no NuGet. `ValheimDir` defaults to
the usual Steam path and `ProfileDir` to the repo's own `testprofile\`, both overridable:

```
dotnet build Vaettir.csproj -p:ProfileDir=<your BepInEx profile>
```

The build copies the DLL and `assets\*.obj`, `*.col` and `*.png` into
`<ProfileDir>\BepInEx\plugins\Vaettir`. `tools/` holds the Blender scripts that produce every
model, including the rejected ones.

## Licence

MIT. Robbin Thijssen (Thijssen Software), GitHub [Ezomic](https://github.com/Ezomic).
Published on [Thunderstore](https://thunderstore.io/c/valheim/p/Ezomic/Vaettir/).

## Part of Longhouse

Vaettir is in the [Longhouse pack](https://thunderstore.io/c/valheim/p/Ezomic/Longhouse/),
which pins exact versions of the Ezomic mods and installs in one click. You do not need the
pack to use this, and nothing here behaves differently on its own.
