using BepInEx.Configuration;
using UnityEngine;

namespace Stow
{
    internal static class StowConfig
    {
        public static ConfigEntry<bool> PostEnabled;
        public static ConfigEntry<string> PostName;
        public static ConfigEntry<string> PostCost;
        public static ConfigEntry<int> PostWidth;
        public static ConfigEntry<int> PostHeight;

        public static ConfigEntry<string> PostDonor;
        public static ConfigEntry<string> PostModelFile;
        public static ConfigEntry<float> PostScale;
        public static ConfigEntry<string> LookForProps;

        public static ConfigEntry<float> Range;
        public static ConfigEntry<bool> MatchContents;
        public static ConfigEntry<bool> Messages;

        public static ConfigEntry<bool> CarrierEnabled;
        public static ConfigEntry<int> Couriers;
        public static ConfigEntry<float> CarrierSpeed;
        public static ConfigEntry<float> CarrierPause;
        public static ConfigEntry<float> CarrierCruise;
        public static ConfigEntry<float> CarrierScale;
        public static ConfigEntry<string> CarrierGlowDonors;
        public static ConfigEntry<string> FlareDonors;

        public static ConfigEntry<float> PostLightRange;
        public static ConfigEntry<float> PostLightIntensity;
        public static ConfigEntry<float> PostFlareScale;

        public static ConfigEntry<KeyboardShortcut> KeyStow;
        public static ConfigEntry<KeyboardShortcut> KeyConfigure;
        public static ConfigEntry<bool> KeepHotbar;
        public static ConfigEntry<string> NeverStow;

        public static ConfigEntry<bool> Verbose;

        public static void Bind(ConfigFile config)
        {
            // ---------------------------------------------------------- the post

            PostEnabled = config.Bind("Post", "PostEnabled", true,
                "Add the stowing post to the hammer.");

            PostName = config.Bind("Post", "PostName", "Stowing post",
                "What the piece and its window are called.");

            PostCost = config.Bind("Post", "PostCost", "FineWood:20,IronNails:20",
                "What it costs to build, as Item:Amount pairs. Nails put it past the "
                + "forge rather than in the first camp: a post that sorts a storage room "
                + "should arrive when there is a storage room to sort. An item name that "
                + "does not resolve is logged and skipped, which makes the post cheaper "
                + "rather than unbuildable - check the log after editing this.");

            PostWidth = config.Bind("Post", "PostWidth", 6,
                "Slots across. Wide and shallow on purpose: the post is a table you pass "
                + "things over, not somewhere to keep them.");

            PostHeight = config.Bind("Post", "PostHeight", 2,
                "Slots down.");

            // ---------------------------------------------------------- its look

            PostDonor = config.Bind("Post", "PostDonor", "piece_chest_wood",
                "The vanilla piece the post is cloned from. It keeps the donor's Container, "
                + "Piece and WearNTear - only the label and the visuals change.");

            // A config entry rather than a constant, so a rejected shape is swapped by
            // editing a line instead of rebuilding. Stoker's hopper spent a while wearing
            // a bin that had already been turned down, purely because the filename was
            // baked into the source.
            PostModelFile = config.Bind("Post", "PostModelFile", "stow_post_canopy.obj",
                "The hand-built mesh the post wears, beside the dll. Its .col sidecar and "
                + "its _icon.png are picked up automatically, and wherever its `core` group "
                + "sits is where the post is lit and where the spirit is born - so a "
                + "different model moves all three without anything here changing. The "
                + "plain shapes from tools/post_designs.py ship alongside it: "
                + "stow_post_rack.obj, stow_post_chute.obj, stow_post_table.obj, "
                + "stow_post_barrow.obj - none of those carry a heartwood.");

            PostScale = config.Bind("Post", "PostScale", 1f,
                "Scale of the whole piece.");

            LookForProps = config.Bind("Post", "LookForProps", "",
                "Comma-separated words. On startup, logs every loaded prop whose name "
                + "contains one. Nothing is grafted from these - the post's shape is its "
                + "own - but each material group is skinned from a real vanilla prefab, "
                + "and this is how to find one worth borrowing from. Try: wood, forge, "
                + "stone.");

            // ---------------------------------------------------------- sorting

            Range = config.Bind("Stow", "Range", 12f,
                "How far a chest may be from the post and still be stowed into. Measured "
                + "from the post, through walls - a storage room is still one room when "
                + "the shelves are behind a pillar.");

            MatchContents = config.Bind("Stow", "MatchContents", true,
                "Let a chest that has been given no rules still take more of what it "
                + "already holds. This is what makes the mod useful before you have "
                + "configured anything, and it is the lowest-priority match: any chest "
                + "that actually asked for the item wins over one that merely has some.");

            Messages = config.Bind("Stow", "Messages", true,
                "Corner message summarising what went where. With the carrier on this "
                + "arrives when the last trip lands, not when you close the post.");

            // ---------------------------------------------------------- the carrier

            CarrierEnabled = config.Bind("Carrier", "CarrierEnabled", true,
                "A spirit carries the items to the chests, one stack per trip, instead "
                + "of the post emptying itself the instant you close it. Turn this off "
                + "and the post goes back to moving everything at once - the sorting is "
                + "identical either way, only the waiting changes.");

            Couriers = config.Bind("Carrier", "Couriers", 1,
                "How many spirits a post flies at once. One is the deliberate default: "
                + "the point is watching a thing carry a thing, and three of them make "
                + "it a conveyor belt. Raise it if a post that serves twenty chests "
                + "takes longer to clear than you want to stand there.");

            CarrierSpeed = config.Bind("Carrier", "CarrierSpeed", 2.6f,
                "Metres per second in the air. A brisk walk. Faster than a run and the "
                + "trip is over before you have looked at it.");

            CarrierPause = config.Bind("Carrier", "CarrierPause", 0.5f,
                "Seconds spent hovering at each end, loading and unloading. Without a "
                + "pause the spirit bounces off the chest and the delivery is invisible.");

            CarrierCruise = config.Bind("Carrier", "CarrierCruise", 1.1f,
                "How high above the higher end it arcs. This is what carries it over "
                + "furniture and walls: it flies rather than walks, so nothing here has "
                + "to know what pathing is.");

            CarrierScale = config.Bind("Carrier", "CarrierScale", 0.62f,
                "Scale of the spirit.\n"
                + "The carrier is the Vaettir spirit and now wears its mesh exactly, "
                + "which is built knee-to-chest rather than the forearm-across copy Stow "
                + "used to generate for itself. 0.62 is that ratio, so the carrier is the "
                + "size it has always been on screen and the only thing that changed is "
                + "the model underneath being the right one.\n"
                + "Set it to 1 for a full-size spirit. That is the honest reading - it is "
                + "the same being either way - but it is a large thing to have crossing a "
                + "storage room with a crate under it, so the default keeps what was "
                + "already there rather than resizing anyone's post without asking.");

            PostLightRange = config.Bind("Post", "PostLightRange", 7f,
                "How far the heartwood in the post throws light. It is a lamp in a "
                + "storage room as well as a component, and this is the number that "
                + "decides which.");

            PostLightIntensity = config.Bind("Post", "PostLightIntensity", 1.15f,
                "Brightness of that light. The mesh itself does not glow - vanilla paints "
                + "emission into a texture and a borrowed material lands our UVs on the "
                + "black part of it - so this and the flare are the whole effect.");

            PostFlareScale = config.Bind("Post", "PostFlareScale", 0.75f,
                "Size of the halo on the post's heartwood, relative to the donor's own. "
                + "The dvergr lantern it is lifted from is a metre and a bit across.");

            FlareDonors = config.Bind("Carrier", "FlareDonors",
                "piece_dvergr_lantern,guard_stone,piece_walltorch,fire_pit",
                "Prefabs to lift the halo off, best first. What is taken is the child "
                + "named flare - a ParticleSystem wearing light_glow - which is how every "
                + "glowing thing in the game actually reads as glowing. The mesh's own "
                + "emission is a painted texture that is almost entirely black, so a "
                + "borrowed emissive material lands our UVs on the black part and does "
                + "nothing. The lantern is first because its flare is a single soft halo; "
                + "guard_stone works but hangs four other billboards off itself.");

            CarrierGlowDonors = config.Bind("Carrier", "CarrierGlowDonors",
                "piece_dvergr_lantern,guard_stone,piece_walltorch,fire_pit",
                "Prefabs to lift the spirit's *surface* from, best first - the first that "
                + "resolves and has an albedo wins. This is only the material on the mesh; "
                + "what makes it read as lit is its Light and its flare, so a poor choice "
                + "here is dull rather than invisible. fire_pit led this list and was a "
                + "real bug: the first material with an albedo on a fire pit is its "
                + "stones, so the spirit flew around wearing rock. The dvergr lantern is "
                + "warm worked metal, which is what a heartwood should look like unlit.");

            // ---------------------------------------------------------- optional keys

            // Both off by default. The post replaced them: it is a thing in the world
            // rather than a binding to remember, and the numpad was already crowded -
            // Thralls holds the number row, Tether has plus, Devkit has F6.
            KeyStow = config.Bind("Keys", "KeyStow",
                new KeyboardShortcut(KeyCode.None),
                "Optional. Empties your pack into the chests around you from wherever you "
                + "stand, skipping the post entirely. Unbound by default.");

            KeyConfigure = config.Bind("Keys", "KeyConfigure",
                new KeyboardShortcut(KeyCode.None),
                "Optional. Look at a chest and press this to say what it holds. Unbound by "
                + "default because the chest window has a button for it.");

            KeepHotbar = config.Bind("Keys", "KeepHotbar", true,
                "For KeyStow only: leave the hotbar row alone. Your axe and your food live "
                + "there on purpose. A post takes whatever you put in it, hotbar or not, "
                + "because putting it in was already the decision.");

            NeverStow = config.Bind("Keys", "NeverStow",
                "Hammer,Hoe,Cultivator",
                "For KeyStow only: prefab names that never leave your pack. Comma-separated.");

            Verbose = config.Bind("Diagnostics", "Verbose", false,
                "Log every item moved and the chest it chose.");
        }
    }
}
