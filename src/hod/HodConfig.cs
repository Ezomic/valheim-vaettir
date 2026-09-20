using BepInEx.Configuration;

namespace Hod
{
    /// <summary>
    /// Everything the bench service can be told, in one section of Vaettir's own .cfg.
    ///
    /// This is Hirsla's config file folded into a section rather than rewritten. The prose
    /// is kept deliberately: those comments are documentation somebody reads in the file
    /// instead of the README, they carry the reasoning and the consequences - including the
    /// ones that will look like a bug - and re-deriving them in shorter words would lose
    /// exactly the half that is worth having.
    ///
    /// Two things did not come across, and both were Hirsla being its own mod rather than a
    /// feature of this one:
    ///
    ///   RequireOnClients   Vaettir already registers with Core at Requirement.Everyone,
    ///                      because it declares prefabs and a client that cannot resolve one
    ///                      discards the ZDO rather than erroring. There is no weaker setting
    ///                      available to this mod, so a switch offering one would be a lie.
    ///   Verbose            Diagnostics/Verbose belongs to GroveConfig. One mod, one switch -
    ///                      and binding the same section and key twice throws.
    ///
    /// The standing BepInEx trap applies to every line of it: every entry is written to disk
    /// on first run and the saved value beats a new default in code. Changing a default here
    /// does nothing on a machine that has already run Vaettir - edit
    /// <c>&lt;profile&gt;\BepInEx\config\ezomic.valheim.vaettir.cfg</c> as part of the same
    /// change. When a config-driven change appears to do nothing in game, read the cfg before
    /// reading any code.
    /// </summary>
    internal static class HodConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> Range;

        public static ConfigEntry<string> BossBiomes;
        public static ConfigEntry<bool> AllowUnclassified;
        public static ConfigEntry<string> BiomeOverrides;

        public static ConfigEntry<bool> ShowChestTotals;
        public static ConfigEntry<string> ChestTotalFormat;
        public static ConfigEntry<string> ShortMessage;

        public static ConfigEntry<float> RequestTimeout;

        public static ConfigEntry<bool> ShowFlight;

        /// <summary>
        /// Not bound here. Vaettir has one diagnostics switch and this points at it, the
        /// same way StowConfig does - see the class docstring.
        /// </summary>
        public static ConfigEntry<bool> Verbose;

        public static void Bind(ConfigFile config)
        {
            // Every mod in the suite has one of these and it means the same thing every
            // time: loaded, bound, patched, and deciding nothing. Not "unloaded" - a plugin
            // cannot unload itself, and a switch that pretends otherwise is a lie somebody
            // will spend an evening debugging.
            Enabled = config.Bind("Hod", "HodEnabled", true,
                "Off leaves the bench service loaded and changing nothing. The crafting "
                + "panel goes back to counting only what you are carrying, and every "
                + "requirement number with it. A hod jib already built stands where it is "
                + "and does nothing, which is the safe direction: the piece is not "
                + "un-declared, so no ZDO is at risk.");

            // Twenty metres, and measured from the POST rather than from the player. That
            // is the one substantive change the fold made to Hirsla's rules, and it is not
            // a tuning choice - it is what makes the jib a piece rather than a passive mod.
            //
            // Hirsla measured from the player because there was nothing else to measure
            // from: it was a mod you installed, and the sphere had to hang off the only
            // thing that moves. Here there is a fixed object in the world that paid a
            // heartwood for the privilege, so the sphere hangs off that - which also means
            // the set of chests being counted does not change as the player shuffles at the
            // bench, and a bench that worked a moment ago cannot stop working because
            // somebody took half a step.
            Range = config.Bind("Hod", "HodRange", 20f,
                "How far the bench service reaches, in metres, measured FROM THE STOWING "
                + "POST that carries the hod jib. It is one number doing two jobs, and they "
                + "are the same sphere by design: the crafting station you are standing at "
                + "must be within this of such a post, and the chests that serve it are the "
                + "ones within this of that same post.\n"
                + "Not measured from you. The post is the fixed thing - it is what was built "
                + "and what was paid for - so a bench either is in range of it or is not, "
                + "and that answer does not change while you stand there deciding what to "
                + "make. It also means a jib serves the room it was built in rather than "
                + "following whoever walks through.\n"
                + "Bigger is not free: every chest inside this sphere is read on every "
                + "requirement check, and the crafting panel checks every recipe in the game "
                + "once a frame.\n"
                + "Deliberately separate from Sorting/Range, which is how far a spirit will "
                + "fly to put something away, and from Upgrades/UpgradeRange, which is what "
                + "counts as \"beside\" for the piece itself.");

            // ---------------------------------------------------------------- the gate

            // The whole argument of the feature, and the same shape as Yoke's
            // ProgressionTiers minus the multiplier - it is the same question asked for a
            // different purpose: which biome has the world earned. Two mods disagreeing
            // about which boss owns which biome would be two mods giving one player
            // different answers out of one set of keys.
            BossBiomes = config.Bind("Hod", "BossBiomes",
                "defeated_eikthyr:meadows, defeated_gdking:blackforest, "
                + "defeated_bonemass:swamp, defeated_bonemass:ocean, "
                + "defeated_dragon:mountain, defeated_goblinking:plains, "
                + "defeated_queen:mistlands, defeated_fader:ashlands",
                "boss:biome, comma separated. Kill a biome's boss and the materials that "
                + "biome gives you start coming out of the chests around the post. Before "
                + "Eikthyr the jib does nothing at all; by Fader it does everything, and "
                + "that is the point - the convenience is the reward for finishing the "
                + "game, not a thing you install to skip it.\n"
                + "One boss may name several biomes. Bonemass carries the Ocean as well as "
                + "the Swamp, because no boss lives in the water and without a row naming it "
                + "every fish, and chitin, and bait would be locked out for the whole game. "
                + "Bonemass rather than an earlier one because the longship is the boat that "
                + "makes fishing worth hauling.\n"
                + "Deep North is deliberately absent: it has no boss and no items of its "
                + "own, and a biome no row names is left open rather than shut forever - see "
                + "AllowUnclassified for the same argument made about items.\n"
                + "Which biome an item belongs to is worked out from the game's own tables, "
                + "not a list here: the vegetation table places a copper deposit in the "
                + "Black Forest and the deposit says it drops copper ore, so copper ore is a "
                + "Black Forest item. Creatures come through the spawn table and their "
                + "drops, and bars and cooked food inherit from what they are made of.\n"
                + "Any global key works, not only boss keys, so a modded key can be named.\n"
                + "The names are read as strings, never as the game's GlobalKeys enum. That "
                + "enum has no defeated_queen and no defeated_fader member in this build, so "
                + "reading it would answer 'not yet' for the Mistlands and the Ashlands "
                + "forever - which from inside the game looks exactly like nobody having "
                + "killed them.");

            // Fail open, and this is a decision rather than an oversight. About six vanilla
            // items have no world source and no recipe, and any content mod can add more. A
            // false block reads as a broken mod - the wood is right there in the chest and
            // the bench refuses it - while a false allow reads as an ordinary
            // craft-from-container mod, which is what somebody who built the jib already
            // expects.
            AllowUnclassified = config.Bind("Hod", "AllowUnclassified", true,
                "Whether an item the mod cannot place in any biome may still be drawn from "
                + "a chest. On, deliberately. A handful of vanilla items are neither found "
                + "in the world nor made from anything - and every item a content mod adds "
                + "without a world source is one too - so the honest choice is between "
                + "letting them through and having a bench refuse material that is sitting "
                + "in the chest beside it for no reason a player can see. Turn it off for a "
                + "strict run and expect to fill in BiomeOverrides.");

            // Yoke's list verbatim, which is where it was measured and corrected. It is not
            // decoration: without it iron, silver and flametal are all "no biome",
            // AllowUnclassified lets them through, and the gate leaks exactly where the
            // game's pacing matters most.
            BiomeOverrides = config.Bind("Hod", "BiomeOverrides",
                // Roots. Everything made from these is placed by the recipe pass, so this
                // list is far shorter than the number of items it ends up accounting for.
                "CopperOre:blackforest, SilverOre:mountain, IronScrap:swamp, IronOre:swamp, "
                + "BlackMetalScrap:plains, FlametalOre:ashlands, FlametalOreNew:ashlands, "
                + "Flametal:ashlands, FlametalNew:ashlands, CopperScrap:blackforest, "
                + "BronzeScrap:blackforest, MoltenCore:swamp, YmirRemains:blackforest, "
                + "FineWood:meadows, RoundLog:meadows, SurtlingCore:blackforest, "
                // Foraged and farmed. The seeds and the cooked forms follow from these.
                + "Carrot:blackforest, Turnip:swamp, Onion:mountain, Barley:plains, "
                + "Flax:plains, Vineberry:mistlands, Honey:meadows, QueenBee:meadows, "
                + "MushroomYellow:blackforest, MushroomBlue:mistlands, "
                + "MushroomBzerker:mistlands, Fiddleheadfern:mistlands, Sap:mistlands, "
                + "Tar:plains, WitheredBone:swamp, Wisp:mistlands, Larva:mistlands, "
                // Chest and dungeon loot.
                + "Amber:blackforest, AmberPearl:blackforest, Ruby:blackforest, "
                + "SilverNecklace:mountain, GemstoneBlue:mistlands, GemstoneGreen:mistlands, "
                + "GemstoneRed:mistlands, BlackCore:mistlands, DvergrKeyFragment:mistlands, "
                + "DvergrNeedle:mistlands, Ectoplasm:mistlands, GroveHeartwood:mistlands, "
                + "Thunderstone:mistlands, VegvisirShard_Bonemass:swamp, "
                // Water. Fish are placed rather than derived; nothing spawns them from a table.
                + "Fish1:ocean, Fish2:ocean, Fish3:ocean, Fish5:ocean, Fish6:ocean, "
                + "Fish7:ocean, Fish8:ocean, Fish9:ocean, Fish4_cave:mountain, "
                + "Fish10:ashlands, Fish11:ashlands, Fish12:ashlands, FishRaw:ocean, "
                + "FishAnglerRaw:ocean, FreshSeaweed:ocean, Chitin:ocean, "
                + "BonemawSerpentScale:ashlands, "
                // Creatures that only come out of a location, so their drops are invisible.
                + "TrophyForestTroll:blackforest, TrophySkeletonHildir:blackforest, "
                + "TrophyGhost:swamp, TrophyDraugrFem:swamp, TrophySkeletonPoison:swamp, "
                + "TrophySurtling:swamp, BlobVial:swamp, draugr_arrow:swamp, "
                + "TrophyCultist:mountain, TrophyCultist_Hildir:mountain, TrophyUlv:mountain, "
                + "WolfClaw:mountain, WolfHairBundle:mountain, "
                + "TrophyGoblinShaman:plains, TrophyGoblinBruteBrosBrute:plains, "
                + "TrophyGoblinBruteBrosShaman:plains, GoblinSpear:plains, JuteBlue:plains, "
                + "JuteRed:plains, BombBlob_Tar:plains, "
                + "TrophyGrowth:mistlands, TrophyKvastur:ashlands, TrophyCharredMage:ashlands, "
                + "CuredSquirrelHamstring:mistlands, TurretBoltBone:mistlands, "
                // Ashlands, which is almost entirely location work.
                + "AsksvinEgg:ashlands, AsksvinCarrionNeck:ashlands, "
                + "AsksvinCarrionPelvic:ashlands, AsksvinCarrionRibcage:ashlands, "
                + "AsksvinCarrionSkull:ashlands, ChickenEgg:ashlands, ChickenMeat:ashlands, "
                + "CharredCogwheel:ashlands, BellFragment:ashlands, Pot_Shard_Red:ashlands, "
                + "PungentPebbles:ashlands, CandleWick:ashlands, FragrantBundle:ashlands, "
                + "PowderedDragonEgg:ashlands, "
                // Spices, each named after where it comes from.
                + "SpiceForests:blackforest, SpiceMountains:mountain, SpicePlains:plains, "
                + "SpiceOceans:ocean, SpiceMistlands:mistlands, SpiceAshlands:ashlands, "
                // Seeds the crop recipes do not run backwards to, and the two odd consumables.
                + "OnionSeeds:mountain, VineberrySeeds:mistlands, VineGreenSeeds:mistlands, "
                + "FishingBait:ocean, MeadTrollPheromones:blackforest",
                "prefab:biome, comma separated, for items the game's tables cannot place.\n"
                + "Iron is the reason this exists. Iron scrap is not placed in the world and "
                + "is not dropped by anything that spawns in one - it is inside Sunken "
                + "Crypts, and a location holds its prefab as a soft reference that is not "
                + "loaded until the game wants it. Forcing dungeon interiors to load on the "
                + "way into a world, to learn something that fits on this line, is not a "
                + "trade worth making.\n"
                + "An item with no entry here and no place in the game's tables is decided "
                + "by AllowUnclassified, which lets it through by default - so this list is "
                + "the difference between a gate that holds and a gate that leaks around "
                + "iron, silver and flametal.\n"
                + "This is Yoke's list verbatim. The two mods ask the same question of the "
                + "same tables and a divergence between them would be one player getting two "
                + "different answers about one item.");

            // ---------------------------------------------------------------- interface

            // The build menu is deliberately absent from this, and from the feature. Both
            // panels draw their requirement lines through one method,
            // InventoryGui.SetupRequirement, so an earlier version of Hirsla changed the
            // numbers in both for free - and free was the problem. The build HUD's numbers
            // say exactly what vanilla says, because building does not draw on a chest and a
            // line promising material the hammer will not spend is worse than no line at all.
            ShowChestTotals = config.Bind("Hod", "ShowChestTotals", true,
                "Add what the post's chests hold to the amount shown beside each requirement "
                + "in the crafting panel. The build menu is untouched - the hammer does not "
                + "draw on chests, so its numbers are vanilla's.\n"
                + "Worth having on: without it the panel says 20 wood and gives no hint that "
                + "the 400 in the chest wall behind you is the reason the recipe is no longer "
                + "greyed out, which reads as the number being wrong.");

            // A format string rather than a fixed layout, because the one thing certain
            // about somebody else's HUD taste is that it is not this one. Applied only when
            // the chests actually hold some, so an ordinary recipe still reads exactly like
            // vanilla.
            ChestTotalFormat = config.Bind("Hod", "ChestTotalFormat",
                "{need} <color=#88CCFF>(+{chest})</color>",
                "How the requirement amount is written when the post's chests hold some of "
                + "it. Left alone entirely when they hold none, so a normal craft looks "
                + "exactly like vanilla.\n"
                + "Tokens: {need} what the recipe costs, {have} what you are carrying, "
                + "{chest} what the chests can supply, {total} the two added up.\n"
                + "TextMeshPro rich text works here, which is what the colour tag is. Set it "
                + "to {need} to keep vanilla's text and still get the un-greying.");

            // Deliberately NOT $msg_missingrequirement, which is what this used to be. That
            // is the string vanilla shows for "you simply do not have this", and showing it
            // after the requirement line drew the chest total in blue and the craft bar ran
            // for two seconds reads as a bug rather than as an explanation - which is the one
            // thing the whole display-versus-payment split was arranged to avoid.
            ShortMessage = config.Bind("Hod", "ShortMessage",
                "The chests could not supply it",
                "Shown when a craft was allowed on material the chests then could not supply "
                + "- the fetch came back short, somebody emptied the chest during the craft "
                + "timer, or the game has not yet handed this client the chests standing "
                + "around the post after a world load.\n"
                + "The craft does not happen. That is the whole point of the line: the "
                + "requirement numbers say what this client's copy of each chest holds, and a "
                + "copy is refreshed once a second, so a recipe can look available and then "
                + "decline. The alternative is a craft paid for with material that is not "
                + "there.\n"
                + "Plain text by default so it cannot be mistaken for vanilla's own \"you are "
                + "missing some requirements\", which is what it used to say and which sent "
                + "people looking for a missing material rather than a busy chest. Set it to "
                + "$msg_missingrequirement to have the vanilla token and its translations "
                + "back, or to an empty string for silence - the refusal stands either way, "
                + "the setting is about the line and not about the rule.");

            // ---------------------------------------------------------------- the flight

            // The half of this feature that is Vaettir's rather than Hirsla's, and the whole
            // reason the jib is a piece with a spirit in it instead of a config entry.
            ShowFlight = config.Bind("Hod", "ShowFlight", true,
                "Send a spirit from the chest to the bench when a craft spends something out "
                + "of one.\n"
                + "IT IS ENTIRELY FOR SHOW AND IT CARRIES NOTHING. The material is already in "
                + "the recipe by the time the spirit leaves - crafting is instant, exactly as "
                + "it is in every mod of this kind - and the flight is started afterwards, "
                + "from a chest position and an item NAME, with no way back to the stack it "
                + "is a picture of. What flies is a local effect on your own screen: it has "
                + "no inventory, it is not on the network, and nothing anywhere reads it.\n"
                + "Turn it off if a busy bench turns your storage room into weather. Nothing "
                + "about what may be crafted changes either way.");

            // ---------------------------------------------------------------- the rest

            // Five seconds is long for a game whose round trips are tens of milliseconds, and
            // that is deliberate: the cost of waiting too long is one chest being skipped for
            // one material for a few seconds, and the cost of giving up too early is asking
            // again for material that is already on its way. Neither can duplicate anything -
            // nothing is credited until a reply says so - so the timeout is about feel.
            RequestTimeout = config.Bind("Hod", "RequestTimeout", 5f,
                "How long to wait for the owner of a chest to answer a withdrawal, in "
                + "seconds. Nothing is credited when it expires, because nothing is credited "
                + "until the owner says what it really took - all the timeout does is allow "
                + "the same material to be asked for again.\n"
                + "The usual reason for silence is that the player who owns that chest does "
                + "not have Vaettir installed. That is completely quiet on both machines - "
                + "the game drops a message nobody registered without saying so - and the "
                + "timeout here is the only sign of it. Turn Diagnostics/Verbose on and it "
                + "writes a line naming the material. Vaettir registers with Core at the "
                + "strict setting, so on a gated server this cannot happen at all.\n"
                + "What a timeout costs, in practice, is one craft. The prefetch goes out "
                + "when the craft button is pressed and the craft timer is under two seconds, "
                + "so a reply that takes five has already missed it - the craft declines with "
                + "ShortMessage and the next press asks again.\n"
                + "Only ever reached on a chest owned by somebody else. In singleplayer and "
                + "on a listen host every chest is yours and no request is sent at all.");

            // One mod, one diagnostics switch - see the class docstring. Bound by
            // GroveConfig, pointed at here so every file in this folder reads it the same way
            // the stow files do.
            Verbose = Grove.GroveConfig.Verbose;
        }
    }
}
