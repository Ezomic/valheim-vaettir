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
    ///
    /// sold= is the biome of the earliest trader that sells the item, or none (LHM-63), and biome=
    /// is the one the gate uses: the earlier of that and the index's own answer. `hod traders`
    /// prints how many items the traders of each biome sell, as biome=count tokens.
    ///
    /// boss= is the key the biome's BossBiomes row waits on. It reads boss=none when no row
    /// names the biome, which is why such a biome is open, and boss=unresolved when the row
    /// reads its key off a boss prefab and has not found one in this world, which is why such a
    /// biome is shut. The Deep North's row is the one that reads a prefab, and this token is
    /// how its key is learned without the log.
    ///
    /// <b>`hodkey` is the one command here that changes anything</b>, and it is a cheat for
    /// that reason: it sets or removes the key a biome's row waits on. A scenario needs it
    /// because the Deep North's key is read at run time and cannot be written into a test as a
    /// string, so Devkit's own `unkey` step has nothing to name. Devkit reaches it with its
    /// `mod` step, which runs a mod's command without the cheat mark. It can do nothing
    /// `setkey` and `removekey` cannot; it only looks the key up.
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

            // Failable, so a refusal reaches Devkit's `mod` step as a failed step rather than
            // as a line of text a scenario would have to think to check.
            new Terminal.ConsoleCommand("hodkey",
                "<biome> on|off: set or remove the global key the hod jib's BossBiomes row for that "
                + "biome waits on, including a key read off a boss prefab",
                new Terminal.ConsoleEventFailable(OnKeyCommand), isCheat: true);
        }

        private static object OnKeyCommand(Terminal.ConsoleEventArgs args)
        {
            if (args.Length < 3) return "hodkey <biome> on|off, for example hodkey deepnorth off";

            var biome = args[1].ToLowerInvariant();
            var mode = args[2].ToLowerInvariant();
            if (mode != "on" && mode != "off") return "say on or off, not " + args[2];

            var zone = ZoneSystem.instance;
            if (zone == null) return "no world, so no keys. Load one first";

            bool named;
            var keys = HodGate.KeysOf(biome, out named);
            if (!named) return "no BossBiomes row names '" + biome + "', so nothing opens or shuts it";

            if (keys.Count == 0)
                return "the " + biome + " row has not found its key in this world, so there is "
                       + "nothing to " + (mode == "on" ? "set" : "remove") + ". The log says which "
                       + "prefabs it tried";

            foreach (var key in keys)
            {
                if (mode == "on") zone.SetGlobalKey(key);
                else zone.RemoveGlobalKey(key);
            }

            var term = args.Context;
            if (term != null)
                term.AddString("hodkey biome=" + biome + " key=" + string.Join(",", keys.ToArray()) + " " + mode);

            return true;
        }

        private static void OnCommand(Terminal.ConsoleEventArgs args)
        {
            var term = args.Context;
            if (term == null) return;

            if (args.Length >= 2 && args[1].ToLowerInvariant() == "traders")
            {
                term.AddString(HodTraders.Describe());
                return;
            }

            if (args.Length < 3 || args[1].ToLowerInvariant() != "item")
            {
                term.AddString("hod item <prefab>, for example hod item Wood, standing at a station a hod jib serves. "
                               + "hod traders says how many items each biome's traders sell.");
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
            var biome = HodGate.BiomeOf(prefabName);

            // A key, HodGate.Unresolved, or null for no row. See the class docstring.
            var boss = HodGate.BossOf(biome);

            return "hod item=" + prefabName
                   + " index=" + (BiomeIndex.Complete ? "complete" : "incomplete")
                   + " scope=" + (HodScope.IsOpen ? "open" : "shut")
                   + " biome=" + biome
                   + " boss=" + (boss ?? "none")
                   + " reach=" + Reach(sharedName)
                   + " offered=" + HodChests.CountAllowed(sharedName, -1, true)
                   + " gate=" + (HodGate.Allows(prefabName) ? "open" : "shut")
                   + " sold=" + HodTraders.BiomeOf(prefabName);
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
