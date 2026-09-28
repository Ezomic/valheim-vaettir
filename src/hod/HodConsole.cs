using Ezomic.Shared;
using HarmonyLib;

namespace Hod
{
    /// <summary>
    /// `hod item &lt;prefab&gt;` in the console: which biome the bench service files a material
    /// under, which boss opens that biome, how many of it the chests around the post hold, and how
    /// many of those the gate would hand to a craft right now.
    ///
    /// Written for LHM-36. The shared biome index read the Jotun invasion's spawn rows, which name
    /// every biome at once, as a Meadows habitat, so the Elaking and Jotun drops and the Vanguard
    /// chest family came out of a chest from Eikthyr on. No workbench recipe takes any of them,
    /// so `craftable` cannot be asked about them, and the requirement line only draws for a
    /// recipe that is selected. This is the question vaettir-jib-deep-north-items.txt asks
    /// instead.
    ///
    /// <b>The two counts are the crafting path's own, not copies of it.</b> offered is
    /// HodChests.CountAllowed, the number the counting patch adds to your pack's count inside a
    /// crafting question, with HodGate.AllowsStack applied per stack exactly as it is there and
    /// in the take. reach is the same chests with the gate left out, so a zero offered can be
    /// told apart from a chest the post does not reach. Both ask by the item's shared name,
    /// because that is how the game's own count asks.
    ///
    /// The chests are the ones HodScope opens, which needs you at a crafting station a jib-bearing
    /// post serves. Anywhere else scope=shut, and both counts are zero because nothing is looked at.
    ///
    /// <b>isCheat: false, because it only reads.</b> A cheat command would need devcommands typed
    /// first, and running any cheat marks the character as having cheated, which Dyrr reads at
    /// the dev server's door.
    ///
    /// The answer is name=value tokens with no spaces inside them, in a fixed order, because
    /// Devkit's `printed` step matches a substring and a scenario pins a count by asserting it
    /// together with its neighbour: "offered=5" alone is also true of "offered=50".
    /// </summary>
    internal static class HodConsole
    {
        /// <summary>
        /// Process-wide, not per world. Terminal's command table is a private static that nothing
        /// clears, so a second registration would leave a duplicate behind.
        /// </summary>
        private static bool _registered;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Terminal), "InitTerminal")]
        private static void Register()
        {
            if (_registered) return;
            _registered = true;

            new Terminal.ConsoleCommand("hod",
                "item <prefab>: which biome the hod jib files a material under, and how many of it the "
                + "chests around the post would hand to a craft. Use it at a station the jib serves",
                new Terminal.ConsoleEvent(OnCommand), isCheat: false);
        }

        private static void OnCommand(Terminal.ConsoleEventArgs args)
        {
            var term = args.Context;
            if (term == null) return;

            if (args.Length < 3 || args[1].ToLowerInvariant() != "item")
            {
                term.AddString("hod item <prefab>, for example hod item Wood, standing at a station a hod jib serves.");
                return;
            }

            // Refused on the stub. The first ObjectDB of a session has no items, and "no item
            // called Wood" from it would read as a typo.
            var db = ObjectDB.instance;
            if (db == null || db.m_items == null || db.m_items.Count == 0)
            {
                term.AddString("hod item: no item database yet, so load a world first");
                return;
            }

            var prefab = db.GetItemPrefab(args[2]);
            if (prefab == null)
            {
                term.AddString("hod item: no item called '" + args[2] + "'. Prefab names are case-sensitive.");
                return;
            }

            var drop = prefab.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
            {
                term.AddString("hod item: " + prefab.name + " has no item data");
                return;
            }

            term.AddString(Describe(prefab.name, drop.m_itemData.m_shared.m_name));
        }

        private static string Describe(string prefabName, string sharedName)
        {
            var biome = BiomeIndex.BiomeOf(prefabName);
            var boss = HodGate.BossOf(biome);

            return "hod item=" + prefabName
                   + " index=" + (BiomeIndex.Complete ? "complete" : "incomplete")
                   + " scope=" + (HodScope.IsOpen ? "open" : "shut")
                   + " biome=" + biome
                   + " boss=" + (boss ?? "none")
                   + " reach=" + Reach(sharedName)
                   + " offered=" + HodChests.CountAllowed(sharedName, -1, true)
                   + " gate=" + (HodGate.Allows(prefabName) ? "open" : "shut");
        }

        /// <summary>
        /// Everything of this name in the chests the post reaches, gate or no gate.
        ///
        /// Summed by hand rather than through Inventory.CountItems, for the reason HodChests
        /// gives for its own Matches: CountItems is the method this feature patches.
        /// </summary>
        private static int Reach(string sharedName)
        {
            var total = 0;
            var chests = HodChests.Near();

            for (var i = 0; i < chests.Count; i++)
            {
                var container = chests[i];
                if (container == null) continue;

                var inventory = container.GetInventory();
                if (inventory == null) continue;

                foreach (var item in inventory.GetAllItems())
                    if (item != null && item.m_shared != null && item.m_shared.m_name == sharedName)
                        total += item.m_stack;
            }

            return total;
        }
    }
}
