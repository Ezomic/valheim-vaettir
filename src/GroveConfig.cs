using BepInEx.Configuration;
using UnityEngine;

namespace Grove
{
    internal static class GroveConfig
    {
        public static ConfigEntry<bool> TestMode;

        public static ConfigEntry<string> SpiritName;
        public static ConfigEntry<float> SpiritScale;
        public static ConfigEntry<int> MoteCount;
        public static ConfigEntry<int> RingCount;
        public static ConfigEntry<bool> ShowHoop;

        public static ConfigEntry<float> SpiritRise;

        public static ConfigEntry<string> PartingEffect;

        public static ConfigEntry<string> HeartwoodName;
        public static ConfigEntry<string> HeartwoodDonor;
        public static ConfigEntry<int> HeartwoodStack;
        public static ConfigEntry<int> HeartwoodGiven;



        public static ConfigEntry<string> SaplingName;
        public static ConfigEntry<string> SaplingCost;
        public static ConfigEntry<string> SaplingDonor;
        public static ConfigEntry<float> SaplingScale;
        public static ConfigEntry<float> SaplingHealth;
        public static ConfigEntry<string> SaplingIcon;
        public static ConfigEntry<bool> NeedsCultivated;

        public static ConfigEntry<bool> PinSaplings;
        public static ConfigEntry<Minimap.PinType> PinIcon;

        public static ConfigEntry<bool> Beckon;
        public static ConfigEntry<string> BeckonRoster;
        public static ConfigEntry<string> BeckonInterval;
        public static ConfigEntry<string> BeckonPack;
        public static ConfigEntry<string> BeckonMessage;
        public static ConfigEntry<bool> NotInBases;
        public static ConfigEntry<float> BaseMargin;
        public static ConfigEntry<string> BaseRefusal;
        public static ConfigEntry<string> BeckonDistance;
        public static ConfigEntry<float> BeckonRange;
        public static ConfigEntry<float> BeckonArea;
        public static ConfigEntry<int> BeckonMaxNear;
        public static ConfigEntry<int> BeckonMaxTotal;

        public static ConfigEntry<float> BloodNeeded;
        public static ConfigEntry<float> FeedRange;
        public static ConfigEntry<string> FeedWeights;
        public static ConfigEntry<bool> Messages;
        public static ConfigEntry<bool> Verbose;

        public static ConfigEntry<bool> CoupleToStow;
        public static ConfigEntry<string> StowPostCost;

        public static ConfigEntry<string> GlowDonors;
        public static ConfigEntry<bool> DumpMaterials;
        public static ConfigEntry<string> LookForPrefabs;

        /// <summary>
        /// Three greydwarfs, not forty.
        ///
        /// Sixty is the right price for the feature and completely wrong for checking
        /// that it works at all: every pass over the sapling's stages, the spirit, the
        /// commune and the heartwood costs an hour of killing first. A switch rather
        /// than a number to edit and remember to put back - the number is the thing
        /// most likely to be left at 3 and then played on.
        /// </summary>
        private const float TestBlood = 3f;

        /// <summary>
        /// What one sapling actually needs, honouring TestMode.
        ///
        /// Read live rather than cached: lowering this under a sapling that is already
        /// part-fed leaves it over the line, and the next kill opens it. That is the
        /// wanted behaviour for a test and harmless otherwise, since Feed is the only
        /// caller that can open one.
        /// </summary>
        public static float BloodNeededNow()
        {
            return TestMode.Value ? TestBlood : Mathf.Max(1f, BloodNeeded.Value);
        }

        public static void Bind(ConfigFile config)
        {
            TestMode = config.Bind("Diagnostics", "TestMode", false,
                "Drops what a sapling needs to three greydwarfs, so the whole chain - "
                + "stages, spirit, commune, heartwood - can be walked in a minute "
                + "instead of an hour. Announced in the log on startup so it is hard "
                + "to leave on.");

            SpiritName = config.Bind("Spirit", "SpiritName", "Forest spirit",
                "What it is called when you look at it.");

            SpiritScale = config.Bind("Spirit", "SpiritScale", 1f,
                "Scale of the whole thing. The mesh is built knee-to-chest height.");

            MoteCount = config.Bind("Spirit", "MoteCount", 6,
                "How many beads ride each ring. Six rather than the original seven: "
                + "seven was chosen so no two beads sat opposite each other on a single "
                + "circle, and with several circles crossing that buys nothing while an "
                + "even count divides more cleanly.");

            RingCount = config.Bind("Spirit", "RingCount", 2,
                "How many circles of beads. One is the original single hoop. Two crossed "
                + "circles is the default: it reads as an orbit rather than a swarm and "
                + "keeps both rings separable, which three only just manages and four "
                + "does not - at four the beads stop reading as being on circles at all "
                + "and become a cloud.");

            ShowHoop = config.Bind("Spirit", "ShowHoop", false,
                "Draw the torus the beads sit on. Off: the circle is implied by where "
                + "the beads are and by their moving together, which is a lighter and "
                + "stranger thing than a visible ring - and with several rings crossing, "
                + "the torus meshes turn the whole shape into a ball of wire.");

            SpiritRise = config.Bind("Spirit", "SpiritRise", 0.4f,
                "How far above the sapling the spirit appears.");

            PartingEffect = config.Bind("Spirit", "PartingEffect",
                "vfx_ghost_death,vfx_HealthUpgrade,vfx_DraugrSpawn",
                "Vanilla effects played once, where the spirit stood, at the moment it "
                + "folds into the heartwood and goes. A list, best first: the first name "
                + "that resolves in this world is used and the rest are ignored.\n"
                + "A list rather than one name because which prefab is actually loaded is "
                + "a question for the game rather than a guess made here - the shipped "
                + "manifest lists what exists on disk, not what a session has - and the "
                + "cost of guessing wrong used to be that this shipped blank for a whole "
                + "release. vfx_ghost_death leads because a wisp coming apart is the "
                + "nearest thing vanilla has to a spirit folding itself away.\n"
                + "Not the glow: the spirit's breathing is a light and an emission colour "
                + "driven in code, always on, and nothing to do with this setting. This "
                + "was called FadeEffect and that name cost a conversation, because the "
                + "spirit visibly fades in and out on its own and the two are unrelated.\n"
                + "Blank for none.");

            // ---------------------------------------------------------- heartwood

            HeartwoodName = config.Bind("Heartwood", "HeartwoodName", "Heartwood",
                "What the material is called.");

            HeartwoodDonor = config.Bind("Heartwood", "HeartwoodDonor", "SurtlingCore",
                "The vanilla item it is cloned from, for its ItemDrop, Rigidbody, "
                + "colliders and float-in-water behaviour. Only the mesh, the name and "
                + "the icon change.");

            HeartwoodStack = config.Bind("Heartwood", "HeartwoodStack", 10,
                "How many fit in a slot.");

            HeartwoodGiven = config.Bind("Heartwood", "HeartwoodGiven", 1,
                "How many a spirit folds itself into. One, and really only one: the "
                + "heartwood is where the spirit lives, so a spirit that produced five "
                + "of them would have five homes and be in none of them. It is also "
                + "the economy - the whole chain exists to make a single stowing post "
                + "cost something real, and five would make the rest free - but the "
                + "story is the harder constraint of the two.");

            // ---------------------------------------------------------- the sapling

            SaplingName = config.Bind("Sapling", "SaplingName", "Ancient sapling",
                "What the planted seed is called.");

            SaplingCost = config.Bind("Sapling", "SaplingCost", "AncientSeed:1",
                "What planting one costs. Not refunded when you take it back up - a "
                + "seed put into the ground is spent.");

            SaplingDonor = config.Bind("Sapling", "SaplingDonor", "sapling_carrot",
                "The vanilla piece it is cloned from, for its placement rules and its "
                + "Piece and WearNTear. The donor's own Plant component is torn out: it "
                + "grows on a timer, which is exactly what must not happen here.");

            SaplingScale = config.Bind("Sapling", "SaplingScale", 1f,
                "Scale of the planted piece.");

            SaplingIcon = config.Bind("Sapling", "SaplingIcon", "grove_sapling_icon.png",
                "The picture on the cultivator, read from beside the dll. Without one "
                + "the piece keeps the donor's icon and the cultivator offers you a "
                + "carrot. Rendered from stage one - the seed you actually place - "
                + "because the grown stages are two metres of mostly trunk and at "
                + "inventory size they come out as a brown stick. tools/sapling_icon.py "
                + "renders the alternatives if you want a different one.");

            SaplingHealth = config.Bind("Sapling", "SaplingHealth", 500f,
                "How much punishment it takes before it is destroyed. The donor is a "
                + "carrot and carries a carrot's health, which is a few points - so a "
                + "single greydwarf swinging near it ended the whole ritual, and the "
                + "seed is not refunded. Five hundred is roughly ten hits from a brute: "
                + "it survives a fight happening around it and does not survive a mob "
                + "left to work on it, which is the point. Defending it is meant to be "
                + "the hard part of the hour, not a formality and not a coin flip.");

            NeedsCultivated = config.Bind("Sapling", "NeedsCultivated", false,
                "Whether it must go in tilled soil. Off by default: an ancient seed "
                + "answering to a forest is a stranger idea than a crop, and making it "
                + "want a vegetable patch shrinks it.");

            // ---------------------------------------------------------- finding it again

            PinSaplings = config.Bind("Sapling", "PinSaplings", true,
                "Put a marker on the map where a sapling is standing, and take it off "
                + "again when the sapling opens or is destroyed.\n"
                + "The sapling is the one piece here you are meant to walk away from - it "
                + "grows on kills rather than on a clock - and a seed in bare ground is "
                + "not findable from fifty metres away. The pin is yours alone, saved in "
                + "your own profile like any pin you place by hand, and nothing about it "
                + "is networked.");

            PinIcon = config.Bind("Sapling", "PinIcon", Minimap.PinType.Icon3,
                "Which of the game's own map icons to use.");

            // ---------------------------------------------------------- calling them in

            Beckon = config.Bind("Sapling", "Beckon", true,
                "Whether a planted seed draws greydwarfs to itself.\n"
                + "Off, the sapling is passive and the quest is really 'find somewhere "
                + "greydwarfs already walk, then wait there', which is a scouting problem "
                + "rather than a defending one. On, the place you chose becomes the thing "
                + "that matters, and what it calls is what feeds it.\n"
                + "It rides vanilla's own SpawnArea - the component a greydwarf nest is "
                + "made of - so it only ever runs on the owner, inside the active area, "
                + "with a player in range. Nothing happens while you are asleep.");

            BeckonRoster = config.Bind("Sapling", "BeckonRoster",
                "Greydwarf:10,Greydwarf_Shaman:3,Greydwarf_Elite:2",
                "What it calls, as Prefab:Weight, comma separated. Weights are relative, "
                + "so these are ten ordinary ones to three shamans to two elites.\n"
                + "Deliberately a separate list from FeedWeights, which says what a death "
                + "is worth. Tying the two together would mean a shaman worth three points "
                + "had to arrive three times as often as it should.");

            BeckonInterval = config.Bind("Sapling", "BeckonInterval", "20-6",
                "Seconds between arrivals, slowest first: the first number is a seed "
                + "nobody has fed and the second is one about to open.\n"
                + "This is meant to read as a raid on the clearing rather than as wildlife "
                + "wandering past it. 90 and 30 were the first numbers here and they were "
                + "far too polite: one greydwarf every minute and a half is something you "
                + "deal with between other jobs, not something you hold ground against.\n"
                + "It still ramps, because a constant rate is a wave you learn to stand in, "
                + "and getting louder as it fills means you can hear how close it is without "
                + "looking at anything. Both ends simply moved.\n"
                + "Below about two seconds nothing more happens whatever is written here: "
                + "SpawnArea wakes on its own two-second repeat and spawns at most one "
                + "creature each time, so two is the floor.");

            BeckonPack = config.Bind("Sapling", "BeckonPack", "2-5",
                "How many arrive together, fewest first: the first number is a seed nobody "
                + "has fed and the second is one about to open.\n"
                + "Vanilla's spawner produces exactly one creature per interval, which is a "
                + "queue rather than a raid - you fight a greydwarf, then wait, then fight "
                + "another greydwarf. A wave arrives from one direction together and has to "
                + "be handled as a group, which is the difference between a chore and a "
                + "fight.\n"
                + "The caps still apply. Each of a wave is spawned through the game's own "
                + "SpawnOne, which checks MaxNear and MaxTotal for itself, so a wave that "
                + "would breach them simply comes up short.");

            NotInBases = config.Bind("Sapling", "NotInBases", true,
                "A sapling cannot be planted in anybody's base, and one already standing "
                + "goes quiet if a base grows around it.\n"
                + "This is the anti-grief. A passive sapling planted in someone else's "
                + "home was rude; one that summons waves of greydwarfs at it is a weapon, "
                + "and planting a dozen around a stranger's longhouse is the obvious grief "
                + "on a public server.\n"
                + "A base is the game's own EffectArea.PlayerBase - what a workbench or a "
                + "fire radiates, and the same test vanilla uses to keep creatures from "
                + "spawning in your house. So the counter-play to a sapling planted next to "
                + "you is to put a workbench down rather than to fight it.\n"
                + "Any base, including your own. Working out whose it is would mean reading "
                + "Piece.m_creator off whatever happens to be nearby, which is more code for "
                + "a worse answer - it would still stop you at a friend's base in co-op - "
                + "and a wilderness ritual has no business in your own hall either.\n"
                + "A ward already refuses the sapling with no help from this: it is an "
                + "ordinary piece, so PrivateArea turns it down like anything else.");

            BaseMargin = config.Bind("Sapling", "BaseMargin", 8f,
                "Extra metres on top of the game's own base radius. Nought means exactly "
                + "the area a workbench already covers; larger pushes saplings further out "
                + "than the ground a base actually protects.");

            BaseRefusal = config.Bind("Sapling", "BaseRefusal",
                "Too close to a hearth. This belongs in the wild.",
                "Said when a sapling is refused for being in a base. Vanilla would say "
                + "'invalid placement', which is true and useless - somebody standing in "
                + "their own garden needs to be told it is the workbench, because that is "
                + "also how they defend against one.");

            BeckonMessage = config.Bind("Sapling", "BeckonMessage",
                "The forest is enraged.",
                "Said once, centre screen, when a sapling starts calling. Blank for "
                + "nothing.\n"
                + "Everyone within BeckonRange is told, not only whoever planted it, "
                + "because it is a warning about a place rather than a note to an owner - "
                + "and on the frame it appears there is nothing on screen yet to account "
                + "for the noise coming out of the trees.\n"
                + "Said again if you leave and come back, since the sapling goes quiet "
                + "while nobody is near it and starts over when someone is.");

            BeckonDistance = config.Bind("Sapling", "BeckonDistance", "25-40",
                "How far out they appear, nearest first. They walk in from there.\n"
                + "A band rather than one radius, and that is the whole of why they arrive "
                + "instead of materialising: vanilla picks its spawn point at a random "
                + "distance between nought and the radius, which is uniform across a disc, "
                + "so most of them land near the middle however wide it is set. This was 12 "
                + "metres and they appeared in your face.\n"
                + "Kept under about 70. Past that the ground at the spawn point may not be "
                + "loaded yet, the floor test simply fails, and the sapling reads as having "
                + "stopped calling.");

            BeckonRange = config.Bind("Sapling", "BeckonRange", 48f,
                "How close you have to be for it to call at all. Vanilla nests use 256m, "
                + "which would have a sapling filling a forest you are nowhere near - and "
                + "quietly getting itself killed by what it summoned. Close enough to hear "
                + "is the intent.");

            BeckonArea = config.Bind("Sapling", "BeckonArea", 96f,
                "How wide the neighbourhood is that BeckonMaxTotal counts inside.\n"
                + "This was effectively the whole loaded world, and it broke the feature "
                + "outright: the count matches creatures by prefab name, so every wild "
                + "greydwarf for a kilometre filled the cap and a sapling in the Black "
                + "Forest called nothing at all, silently and for ever.\n"
                + "The cap should mean this clearing is already crowded, not this half of "
                + "the map contains greydwarfs.");

            BeckonMaxNear = config.Bind("Sapling", "BeckonMaxNear", 10,
                "Most it will have standing around it at once.\n"
                + "Ten is a fight rather than an encounter, and it is deliberately more "
                + "than the sapling survives being ignored for: it has 500 health and about "
                + "ten brute hits in it, so a raid you wander off halfway through takes the "
                + "seed with it. That is the trade - the forest comes to you instead of you "
                + "going out to find it, and the price is having to hold the ground.\n"
                + "Three was the first number here and it was tuned for a sapling that "
                + "trickled. Put it back if a raid is more than you wanted.");

            BeckonMaxTotal = config.Bind("Sapling", "BeckonMaxTotal", 24,
                "Most it will have alive in the wider area. The ceiling that stops a "
                + "sapling left alone from being the reason a whole zone is full of "
                + "greydwarfs - it is a raid on a clearing, and a raid ends.");

            // ---------------------------------------------------------- feeding

            BloodNeeded = config.Bind("Sapling", "BloodNeeded", 50f,
                "How much death it takes. With the default weights that is about fifty "
                + "greydwarfs - rather fewer if elites turn up, since they are worth four "
                + "and shamans three.\n"
                + "This was thirty, chosen when the sapling was passive and thirty was "
                + "roughly one raid arriving on its own. It does not wait for a raid any "
                + "more, it makes one: the seed calls the forest to itself in waves that "
                + "get heavier as it fills, so the number is now the length of a fight you "
                + "have started rather than the odds of one finding you. Fifty is a siege "
                + "you have to hold rather than an errand you complete.");

            FeedRange = config.Bind("Sapling", "FeedRange", 24f,
                "How close a kill must be to count. Only the nearest sapling is fed - "
                + "otherwise a heap of saplings planted together would all grow at once "
                + "off the same work.");

            FeedWeights = config.Bind("Sapling", "FeedWeights",
                "Greydwarf:1,Greydwarf_Elite:4,Greydwarf_Shaman:3,Greyling:0",
                "What each death is worth, as Prefab:Amount. Anything not listed is "
                + "worth nothing. A list rather than a faction check, because "
                + "ForestMonsters would also catch trolls, boars and the Elder, and "
                + "'kill anything in the forest' is a duller quest than 'clear out the "
                + "greydwarfs'. Greylings are listed at zero so it is visible that they "
                + "were considered and refused.");

            Messages = config.Bind("Sapling", "Messages", true,
                "Corner counter each time a kill feeds a sapling.");

            Verbose = config.Bind("Diagnostics", "Verbose", false,
                "Log every feed.");

            // ------------------------------------------- the post, from the grove side

            CoupleToStow = config.Bind("Post", "CoupleToStow", true,
                "Make the stowing post cost heartwood - which is to say, make the post the "
                + "place the spirit ends up living. That is where the sorting comes from, "
                + "and it is why the post is worth an hour of greydwarfs when a chest is "
                + "worth ten wood. Off puts the post back to plain wood and nails, for "
                + "anyone who wants the sorting without the ritual.");

            StowPostCost = config.Bind("Post", "StowPostCost", "GroveHeartwood:1",
                "What is *added* to the stowing post's cost, as Item:Amount. Added rather "
                + "than replacing, because the rest of that recipe is Post/PostCost and "
                + "someone may have changed it deliberately. An ingredient already there "
                + "is raised to whichever amount is higher rather than counted twice.");

            // ---------------------------------------------------------- surfaces

            // Deliberately a list and deliberately unconfirmed. Valheim's own shaders
            // are custom, so which prefab yields a material that actually reads as lit
            // is a question for the game rather than for a guess made here - hence
            // DumpMaterials below, and hence a fallback rather than a hard failure.
            GlowDonors = config.Bind("Spirit", "GlowDonors",
                "fire_pit,piece_walltorch,bonfire,Ember,piece_groundtorch_green,guard_stone",
                "Prefabs to try to lift the glowing material from, best first. The first "
                + "one that resolves and has an albedo wins.");

            DumpMaterials = config.Bind("Diagnostics", "DumpMaterials", false,
                "On startup, log every material on each donor with its shader and "
                + "whether it exposes an emission property. This is how to find a donor "
                + "worth naming rather than guessing at one.");

            LookForPrefabs = config.Bind("Diagnostics", "LookForPrefabs", "",
                "Comma-separated words. Logs every loaded prefab whose name contains "
                + "one. The asset manifest lists what exists on disk, most of which is "
                + "never loaded; this lists what is genuinely there.");
        }
    }
}
