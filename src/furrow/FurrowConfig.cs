using BepInEx.Configuration;
using UnityEngine;

namespace Furrow
{
    internal enum SowShape
    {
        Row,
        Circle,
        Grid
    }

    internal static class FurrowConfig
    {
        public static ConfigEntry<int> GridLevel;
        public static ConfigEntry<bool> GridEnabled;
        public static ConfigEntry<float> GridCell;
        public static ConfigEntry<float> GridAngle;
        public static ConfigEntry<KeyCode> GridPinKey;
        public static ConfigEntry<KeyCode> GridTurnKey;
        public static ConfigEntry<bool> GridTurnScroll;
        public static ConfigEntry<float> GridTurnStep;
        public static ConfigEntry<bool> GridPreview;
        public static ConfigEntry<int> GridPreviewRings;
        public static ConfigEntry<bool> RoomPreview;

        public static ConfigEntry<bool> PickArea;
        public static ConfigEntry<int> PickLevel;
        public static ConfigEntry<int> PickAtLevel;
        public static ConfigEntry<float> PickRadiusMin;
        public static ConfigEntry<float> PickRadius;
        public static ConfigEntry<int> PickMax;
        public static ConfigEntry<bool> PickSameCropOnly;

        public static ConfigEntry<bool> Enabled;

        public static ConfigEntry<SowShape> Shape;
        public static ConfigEntry<bool> RowAcrossFacing;

        public static ConfigEntry<int> CropMaxSeeds;
        public static ConfigEntry<int> CropMaxAtLevel;
        public static ConfigEntry<int> TreeMaxSeeds;
        public static ConfigEntry<int> TreeMaxAtLevel;

        public static ConfigEntry<float> Spacing;

        public static ConfigEntry<KeyCode> IncreaseKey;
        public static ConfigEntry<KeyCode> DecreaseKey;
        public static ConfigEntry<KeyCode> ShapeKey;

        public static ConfigEntry<bool> Verbose;

        public static void Bind(ConfigFile config)
        {
            GridEnabled = config.Bind("Furrow", "GridEnabled", true,
                "From GridLevel up, the cultivator's ghost snaps onto a lattice "
                + "anchored on the nearest plant of the same kind, so hand-placed "
                + "plants land in rows and columns. One seed per press stays vanilla; "
                + "the skill unlock is the alignment.");

            GridLevel = config.Bind("Furrow", "GridLevel", 10,
                "The Farming level that unlocks the grid shape. Below it the shape "
                + "cycle offers row and circle only; a rank of seeds is a hand skill, "
                + "a whole field laid out at once is what the levels are for.");

            GridCell = config.Bind("Furrow", "GridCell", 0f,
                "Metres between plants on the grid. Leave it at 0 - that keeps EVERY "
                + "PLANT ON ITS OWN SPACING, taken from its grow radius, which is the "
                + "distance the game itself needs clear around it. A carrot wants "
                + "centimetres and an oak wants metres, so one fixed number for both "
                + "either spaces carrots out like trees or packs trees in like carrots. "
                + "A number here overrides all of them, and setting it below a tree's "
                + "own radius is exactly how a sapling gets planted with no room and is "
                + "wasted. Watch the ring, not this line.");

            GridAngle = config.Bind("Furrow", "GridAngle", 0f,
                "Which way the rows run, in degrees. The lattice is world-aligned at 0, "
                + "so rows run north-south whatever direction you approach from. Turn it "
                + "to match a building that does not sit square to the world.");

            GridPinKey = config.Bind("Keys", "GridPinKey", KeyCode.KeypadPeriod,
                "Pin the grid to where the ghost is standing, so a row starts exactly "
                + "there. Without a pin the lattice lines up on the nearest plant of the "
                + "same crop, which is right for extending a bed and wrong for lining one "
                + "up with a floor you just laid. Press again to unpin. Only works with "
                + "the cultivator out and a plant selected, since the ghost is what says "
                + "where 'here' is.");

            GridTurnKey = config.Bind("Keys", "GridTurnKey", KeyCode.None,
                "An OPTIONAL key that turns the grid by GridTurnStep, for anyone who "
                + "would rather not use the wheel. None by default, because "
                + "GridTurnScroll below is the gesture. Setting this to Mouse2 also "
                + "stops middle click removing the piece under your cursor while a "
                + "plant is selected, since one click cannot do both.");

            GridTurnScroll = config.Bind("Keys", "GridTurnScroll", true,
                "Turn the grid with the mouse wheel while the cultivator is up with a "
                + "plant selected and the grid is running. The wheel is nearly free "
                + "here: it is vanilla's rotate gesture, and a crop's own facing is "
                + "re-randomised by the game after every single placement, so the yaw "
                + "you scroll to is thrown away as soon as the seed goes in. Turning "
                + "the rows instead spends it on the one thing that does last.");

            GridTurnStep = config.Bind("Keys", "GridTurnStep", 22.5f,
                "Degrees per press of GridTurnKey. 22.5 is the step vanilla turns a "
                + "building by, so anything you built square is reachable exactly.");

            GridPreview = config.Bind("Furrow", "GridPreview", true,
                "Draw the lattice on the ground under the ghost, so the rows, the "
                + "spacing and the angle can be seen before anything is planted rather "
                + "than worked out from where the last seed landed.");

            GridPreviewRings = config.Bind("Furrow", "GridPreviewRings", 3,
                "How many cells the drawn lattice reaches in each direction. 3 draws a "
                + "seven by seven patch, which is enough to see the rows without "
                + "covering the ground you are trying to look at.");

            RoomPreview = config.Bind("Furrow", "RoomPreview", true,
                "Draw a ring at the plant's grow radius, green when it would have room "
                + "and red when it would not. The game does not check this when you "
                + "place: it checks ten seconds later, and a sapling that fails simply "
                + "turns unhealthy or deletes itself, so the seed is gone before "
                + "anything says no. Shown whatever the grid is doing.");

            PickArea = config.Bind("Harvest", "PickArea", true,
                "Shift+E on a ripe crop harvests its neighbours too. Plain E still picks "
                + "exactly one and is untouched - an area harvest you cannot switch off "
                + "is a mod deciding for you when you wanted the whole bed.");

            PickLevel = config.Bind("Harvest", "PickLevel", 15,
                "The Farming level that unlocks the area harvest. Below it Shift+E is an "
                + "ordinary single pick.");

            PickAtLevel = config.Bind("Harvest", "PickAtLevel", 80,
                "The Farming level at which the harvest reaches PickRadius. Between the "
                + "unlock and here it grows smoothly, so the reward arrives over the "
                + "whole skill rather than all at one threshold.");

            PickRadiusMin = config.Bind("Harvest", "PickRadiusMin", 2f,
                "Metres reached at PickLevel, the moment it unlocks. Small on purpose: a "
                + "couple of neighbours, not a field.");

            PickRadius = config.Bind("Harvest", "PickRadius", 8f,
                "Metres reached at PickAtLevel, and the most it ever reaches.");

            PickMax = config.Bind("Harvest", "PickMax", 50,
                "Most crops one Shift+E may take. The radius already limits this; the cap "
                + "is what stops a very dense field emptying an inventory into items on "
                + "the ground in one press.");

            PickSameCropOnly = config.Bind("Harvest", "PickSameCropOnly", true,
                "Take only the crop you clicked, so a mixed bed is harvested a kind at a "
                + "time instead of stripped in one press. Turn it off to take every ripe "
                + "crop in reach. Either way it is ONLY crops - the grown stage of "
                + "something plantable, read from the game rather than listed here - so "
                + "wild berries, mushrooms, thistle and dandelion are never touched.");

            Enabled = config.Bind("Sowing", "Enabled", false,
                "Sow more than one seed per click. Turn this off and the mod does nothing, "
                + "which is also what it does at Farming level 0.");

            Shape = config.Bind("Sowing", "Shape", SowShape.Row,
                "Row lays the seeds in a line. Circle rings them around the one under your "
                + "cursor. Switchable in game with ShapeKey; this is only the starting value.");

            RowAcrossFacing = config.Bind("Sowing", "RowAcrossFacing", true,
                "Row runs left to right across your facing, so you sow a rank and step "
                + "forward. False runs it away from you instead, which plants the far end on "
                + "ground you cannot see clearly.");

            // The curve is two numbers rather than a table of twenty thresholds, because a
            // table that long is not something anyone edits - it is something they give up on.
            // Count is 1 at level 0, MaxSeeds at MaxAtLevel, and floors in between:
            //   count = floor(1 + (MaxSeeds - 1) * min(level / MaxAtLevel, 1))
            CropMaxSeeds = config.Bind("Crops", "MaxSeeds", 20,
                "Most seeds a single click can sow, once Farming reaches MaxAtLevel. "
                + "Set to 1 to switch crops off entirely.");

            CropMaxAtLevel = config.Bind("Crops", "MaxAtLevel", 80,
                "Farming level at which MaxSeeds is reached. Levels above this add nothing. "
                + "With the defaults: 1 seed at level 0, 5 at 20, 10 at 40, 15 at 60, 20 at 80.");

            // Trees get their own pair because they are not crops wearing a different mesh.
            // A sapling's m_growRadius is several times a carrot's, so twenty of them is a
            // stand of forest per click and most of it lands somewhere it cannot grow. The
            // small number here is the whole reason the two are configured separately.
            TreeMaxSeeds = config.Bind("Trees", "MaxSeeds", 5,
                "Most saplings a single click can sow. Deliberately far below the crop "
                + "number: a sapling needs several metres of clear ground where a carrot "
                + "needs centimetres, so the same count covers an enormous area.");

            TreeMaxAtLevel = config.Bind("Trees", "MaxAtLevel", 80,
                "Farming level at which the tree MaxSeeds is reached.");

            Spacing = config.Bind("Sowing", "Spacing", 1f,
                "Multiplier on the gap between sown seeds. 1 uses the plant's own grow "
                + "radius, which is the same distance the game itself refuses to plant "
                + "inside, so neighbours never choke each other. Below 1 will drop seeds.");

            // Scroll would be the obvious binding and is deliberately not used: vanilla
            // already spends the wheel on rotating the placement ghost, and fighting it
            // means the count changes whenever you meant to turn a sapling.
            IncreaseKey = config.Bind("Keys", "IncreaseKey", KeyCode.KeypadPlus,
                "Sow one more seed per click, up to what your Farming level allows.");

            DecreaseKey = config.Bind("Keys", "DecreaseKey", KeyCode.KeypadMinus,
                "Sow one fewer seed per click, down to one.");

            ShapeKey = config.Bind("Keys", "ShapeKey", KeyCode.KeypadMultiply,
                "Switch between row and circle.");

            Verbose = config.Bind("Diagnostics", "Verbose", false,
                "Log every sown position and why any of them were refused.");
        }

        /// <summary>
        /// How many seeds this click may sow, before the inventory is consulted.
        ///
        /// Floor rather than round, so a rank is only reached when the level has actually
        /// been earned: with the defaults the fifth seed arrives at 20 and not at 17.
        /// </summary>
        public static int AllowedFor(float skillLevel, bool isTree)
        {
            var max = isTree ? TreeMaxSeeds.Value : CropMaxSeeds.Value;
            var at = isTree ? TreeMaxAtLevel.Value : CropMaxAtLevel.Value;

            if (max <= 1) return 1;
            if (at <= 0) return max;

            var progress = Mathf.Clamp01(skillLevel / at);
            return Mathf.Clamp(Mathf.FloorToInt(1f + (max - 1) * progress), 1, max);
        }
    }
}
