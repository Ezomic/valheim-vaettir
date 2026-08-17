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

        public static ConfigEntry<string> BonemealName;
        public static ConfigEntry<string> BonemealDonor;
        public static ConfigEntry<string> BonemealModel;
        public static ConfigEntry<string> BonemealIcon;
        public static ConfigEntry<int> BonemealStack;
        public static ConfigEntry<string> BonemealCost;
        public static ConfigEntry<int> BonemealYield;
        public static ConfigEntry<string> BonemealStation;
        public static ConfigEntry<float> BonemealAdvance;
        public static ConfigEntry<float> BonemealHarvest;
        public static ConfigEntry<float> BonemealRadius;

        public static ConfigEntry<string> SaplingName;
        public static ConfigEntry<string> SaplingCost;
        public static ConfigEntry<string> SaplingDonor;
        public static ConfigEntry<float> SaplingScale;
        public static ConfigEntry<float> SaplingHealth;
        public static ConfigEntry<string> SaplingIcon;
        public static ConfigEntry<bool> NeedsCultivated;

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

            PartingEffect = config.Bind("Spirit", "PartingEffect", "",
                "A vanilla effect played once, where the spirit stood, at the moment it "
                + "folds into the heartwood and goes. Blank for none.\n"
                + "Not the glow: the spirit's breathing is a light and an emission "
                + "colour driven in code, always on, and nothing to do with this "
                + "setting. This was called FadeEffect and that name cost a "
                + "conversation, because the spirit visibly fades in and out on its own "
                + "and the two are unrelated.\n"
                + "Named rather than built, because a particle system authored here "
                + "would look like a mod and the game's own does not - but which effect "
                + "is right is a question for the game rather than a guess made here, so "
                + "a name that does not resolve costs the moment its flourish and does "
                + "not break it. Empty by default until a name is confirmed loaded: "
                + "vfx_prayer was the guess and it does not exist, which is a warning "
                + "in the log on every commune and nothing on screen.");

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

            // ---------------------------------------------------------- bonemeal

            BonemealName = config.Bind("Bonemeal", "BonemealName", "Bonemeal",
                "What the item is called.");

            BonemealDonor = config.Bind("Bonemeal", "BonemealDonor", "BoneFragments",
                "The vanilla item it is cloned from, for its ItemDrop, Rigidbody, colliders "
                + "and float-in-water behaviour. Only the mesh, the icon, the name and the "
                + "item type change.");

            BonemealModel = config.Bind("Bonemeal", "BonemealModel", "grove_bonemeal.obj",
                "The mesh, read from beside the dll. Optional: without it the item keeps "
                + "the donor's, which looks wrong rather than being broken - the mechanic "
                + "is playable before the model pass has happened.");

            BonemealIcon = config.Bind("Bonemeal", "BonemealIcon", "grove_bonemeal_icon.png",
                "The inventory picture, read from beside the dll. Optional in the same way.");

            BonemealStack = config.Bind("Bonemeal", "BonemealStack", 50,
                "How many fit in a slot. Generous, because the whole point is using several "
                + "on a field and a stack of ten would mean a trip back to a chest mid-row.");

            BonemealCost = config.Bind("Bonemeal", "BonemealCost",
                "BoneFragments:10,Entrails:2",
                "What one craft costs, as Item:Amount. Entrails rather than bone alone so it "
                + "is a craft rather than a free conversion of the drop everybody is "
                + "drowning in by the Black Forest. An ingredient that does not resolve "
                + "abandons the whole recipe rather than quietly cheapening it.");

            BonemealYield = config.Bind("Bonemeal", "BonemealYield", 5,
                "How many one craft produces.");

            BonemealStation = config.Bind("Bonemeal", "BonemealStation", "piece_workbench",
                "Where it is crafted. Blank, or a name that does not resolve, makes it "
                + "craftable by hand.");

            BonemealAdvance = config.Bind("Bonemeal", "BonemealAdvance", 0.34f,
                "How much of a plant's own growth one use brings forward, as a fraction. A "
                + "third, so three uses mature anything.\n"
                + "A share of the plant's time rather than a flat number of seconds, because "
                + "grow times differ enormously and a modded crop should be advanced by a "
                + "third of *its* season without this mod knowing it exists. It is applied "
                + "by moving the planted moment earlier on the plant's own ZDO, which is why "
                + "it survives a reload and why the growth is still the game's rather than "
                + "a timer of ours.\n"
                + "Set it to 1 and one use matures a crop outright. That is deliberately not "
                + "the default: a fertiliser that finishes the job is a harvest button, and "
                + "it makes the Farming skill Furrow exists to reward moot.");

            BonemealHarvest = config.Bind("Bonemeal", "BonemealHarvest", 2f,
                "What a fertilised crop yields when picked, as a multiplier. Two, so a fed "
                + "carrot gives two.\n"
                + "It travels down RPC_Pick's own bonus argument - the same channel the "
                + "Farming skill's max-level bonus already uses - so the extra goes through "
                + "the game's own drop loop and world drop scaling, extra drops and the way "
                + "pickups spread out all still apply. The base it multiplies is recomputed "
                + "exactly as the game recomputes it, so doubling doubles what you would "
                + "really have got rather than what the prefab says.\n"
                + "The mark is set, not counted. A second bonemeal on the same plant brings "
                + "more time forward but does not stack the harvest, so this cannot be "
                + "farmed by standing over one carrot. It is also spent on picking, which "
                + "matters for anything that respawns rather than being consumed.\n"
                + "Set it to 1 to keep the speed and drop the bounty.");

            BonemealRadius = config.Bind("Bonemeal", "BonemealRadius", 0f,
                "Metres around the plant you used it on that are fertilised too. Zero, so "
                + "one press feeds one plant.\n"
                + "Off by default because the constrained reading is the honest one, and a "
                + "single press advancing a whole field is a different and much larger mod. "
                + "It exists because somebody farming at Furrow's twenty-seeds-a-click will "
                + "want it, and would otherwise go and install something that does far more.");

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

            // ---------------------------------------------------------- feeding

            BloodNeeded = config.Bind("Sapling", "BloodNeeded", 30f,
                "How much death it takes. With the default weights that is about thirty "
                + "greydwarfs, or rather fewer if you go and find a nest - elites are "
                + "worth four and shamans three.\n"
                + "Thirty, so that one greydwarf raid arriving on a planted sapling is "
                + "enough to finish it on its own. Sixty was two or three evenings of "
                + "going out to look for kills, which made the sapling a chore you "
                + "topped up. A raid turning up at a grove you planted is the best "
                + "thing that happens in this whole chain - the forest coming to you "
                + "instead - and it should be the payoff rather than a fifth of one.");

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

            // ---------------------------------------------------------- stow

            CoupleToStow = config.Bind("Stow", "CoupleToStow", true,
                "If the Stow mod is installed, make its stowing post cost heartwood - "
                + "which is to say, make the post the place the spirit ends up living. "
                + "That is where the sorting comes from, and it is why the post is "
                + "worth an hour of greydwarfs when a chest is worth ten wood. "
                + "Off leaves Stow's own recipe alone. Nothing here touches Stow when it "
                + "is absent, and Stow is never told about this mod - it stays a mod that "
                + "works on its own and still builds its post out of wood and fine wood.");

            StowPostCost = config.Bind("Stow", "StowPostCost", "GroveHeartwood:1",
                "What is *added* to the stowing post's cost, as Item:Amount. Added rather "
                + "than replacing, because the rest of that recipe is Stow's setting and "
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
