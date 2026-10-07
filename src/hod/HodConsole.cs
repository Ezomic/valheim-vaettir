using System.Collections.Generic;
using System.Globalization;
using Ezomic.Shared;
using HarmonyLib;
using Stow;
using UnityEngine;

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
                + "chests around the post would hand to a craft. Use it at a station the jib serves. "
                + "ring, skin, bounds, cost <piece> and build are read-only readouts for Devkit scenarios",
                new Terminal.ConsoleEvent(OnCommand), isCheat: false);

            // Failable, for the same reason as hodkey, and a cheat because it writes a setting.
            new Terminal.ConsoleCommand("hodbuild",
                "on|off: set BuildFromChests, so a scenario can prove the hammer is not served when it "
                + "is off. Put it back on afterwards",
                new Terminal.ConsoleEventFailable(OnBuildCommand), isCheat: true);

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

            var sub = args.Length >= 2 ? args[1].ToLowerInvariant() : "";

            if (sub == "ring") { term.AddString(HodRing.Describe()); return; }
            if (sub == "skin") { foreach (var line in PostModel.SkinReport("hod_jib_visual")) term.AddString(line); return; }
            if (sub == "bounds") { term.AddString(Bounds()); return; }
            if (sub == "build") { term.AddString(BuildState()); return; }
            if (sub == "cost") { term.AddString(Cost(args.Length >= 3 ? args[2] : "")); return; }

            if (args.Length < 3 || sub != "item")
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

        private static object OnBuildCommand(Terminal.ConsoleEventArgs args)
        {
            if (args.Length < 2) return "hodbuild on|off";

            var mode = args[1].ToLowerInvariant();
            if (mode != "on" && mode != "off") return "say on or off, not " + args[1];

            HodConfig.BuildFromChests.Value = mode == "on";

            var term = args.Context;
            if (term != null) term.AddString("hodbuild BuildFromChests=" + mode);

            return true;
        }

        /// <summary>
        /// `hod build`: the hammer service as the placement click would see it, in one line.
        ///
        /// scope is asked as a BUILD question (HodScope.Building raised around it), because the
        /// hammer measures from the player where a bench measures from its station, so the same
        /// question asked bare answers about a bench. chests is how many chests that scope
        /// reaches, gate is whether Wood may come out of a chest at all (the biome gate), yields
        /// names the other mod that makes the hammer step aside, or none. A scenario pins
        /// yields=none so that a profile carrying a chest-building mod fails loudly instead of
        /// passing every refusal for that reason.
        /// </summary>
        private static string BuildState()
        {
            string scope;
            int chests;

            HodScope.Building++;
            try
            {
                scope = HodScope.IsOpen ? "open" : "shut";
                chests = HodChests.Near().Count;
            }
            finally
            {
                if (HodScope.Building > 0) HodScope.Building--;
            }

            var placed = new List<PostUpgrade>();
            var ghosts = new List<PostUpgrade>();
            PostUpgrade.CollectJibs(placed, ghosts);

            return "hod build setting=" + (HodConfig.BuildFromChests.Value ? "on" : "off")
                   + " enabled=" + (HodConfig.Enabled.Value ? "on" : "off")
                   + " shut=" + (HodRuntime.Shut ? "yes" : "no")
                   + " yields=" + (HodRuntime.YieldsTo ?? "none")
                   + " scope=" + scope
                   + " chests=" + chests
                   + " gate=" + (HodGate.Allows("Wood") ? "open" : "shut")
                   + " jibs=" + placed.Count
                   + " nets=" + HodNetwork.All.Count;
        }

        /// <summary>
        /// `hod cost &lt;piece&gt;`: what a build piece costs in this game, off its own
        /// Piece.m_resources, so a scenario pins the price it assumed instead of trusting memory.
        /// Item prefab names as keys (Wood=2), one token each, then end, so "Wood=2 end" cannot be
        /// the front of "Wood=20".
        /// </summary>
        private static string Cost(string name)
        {
            if (name.Length == 0) return "hod cost: say which piece, for example hod cost woodwall";
            if (ZNetScene.instance == null) return "hod cost: no world yet";

            var prefab = ZNetScene.instance.GetPrefab(name);
            Piece piece = prefab == null ? null : prefab.GetComponent<Piece>();
            if (piece == null) return "hod cost: no piece called " + name;

            var tokens = new System.Text.StringBuilder();
            var lines = 0;

            foreach (var requirement in piece.m_resources)
            {
                if (requirement == null || requirement.m_resItem == null || requirement.m_amount <= 0) continue;

                lines++;
                tokens.Append(' ').Append(requirement.m_resItem.gameObject.name).Append('=')
                      .Append(requirement.m_amount.ToString(CultureInfo.InvariantCulture));
            }

            var station = piece.m_craftingStation == null ? "none" : piece.m_craftingStation.gameObject.name;

            return "hod cost=" + name + " station=" + station + " lines=" + lines + tokens + " end";
        }

        /// <summary>
        /// `hod bounds`: the collider and the mesh of the nearest standing jib against the ground
        /// under it, in metres. The questions are whether it floats (a collider that starts above
        /// the ground lets a player walk under the footing), whether it is buried deeper than its
        /// footing is meant to be, and whether the collider top is where the mesh top is.
        ///
        /// The footing is modelled 0.13 below the origin on purpose (the .col box and the mesh
        /// both reach -0.13), so that a jib on a slope has no gap under its downhill foot. So
        /// buried means deeper than 0.20 and floating means higher than 0.05; a gap between
        /// -0.20 and +0.05 is the model standing on the ground. grounded is the stricter reading,
        /// within 0.05 either way, which the footing does not meet: it is printed so the
        /// difference is visible rather than argued about.
        ///
        /// Trigger colliders are left out (the use area and the like), and only enabled ones count.
        /// </summary>
        private static string Bounds()
        {
            var player = Player.m_localPlayer;
            if (player == null) return "hod bounds: no player";

            var placed = new List<PostUpgrade>();
            var ghosts = new List<PostUpgrade>();
            PostUpgrade.CollectJibs(placed, ghosts);

            PostUpgrade jib = null;
            var nearest = float.MaxValue;
            foreach (var candidate in placed)
            {
                if (candidate == null) continue;

                var distance = (candidate.transform.position - player.transform.position).sqrMagnitude;
                if (distance >= nearest) continue;

                nearest = distance;
                jib = candidate;
            }

            if (jib == null) return "hod bounds standing=no";

            var colliders = 0;
            var box = new UnityEngine.Bounds();
            foreach (var collider in jib.GetComponentsInChildren<Collider>(false))
            {
                if (collider == null || !collider.enabled || collider.isTrigger) continue;

                if (colliders == 0) box = collider.bounds;
                else box.Encapsulate(collider.bounds);
                colliders++;
            }

            if (colliders == 0) return "hod bounds standing=yes colliders=0";

            var meshes = 0;
            var look = new UnityEngine.Bounds();
            foreach (var renderer in jib.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (renderer == null) continue;

                if (meshes == 0) look = renderer.bounds;
                else look.Encapsulate(renderer.bounds);
                meshes++;
            }

            float ground;
            if (ZoneSystem.instance == null || !ZoneSystem.instance.GetGroundHeight(jib.transform.position, out ground))
                return "hod bounds standing=yes colliders=" + colliders + " ground=unknown";

            var gap = box.min.y - ground;
            var top = box.max.y - ground;
            var lookTop = meshes == 0 ? float.NaN : look.max.y - ground;
            var invariant = CultureInfo.InvariantCulture;

            return "hod bounds standing=yes colliders=" + colliders
                   + " gap=" + gap.ToString("0.00", invariant)
                   + " top=" + top.ToString("0.00", invariant)
                   + " meshtop=" + lookTop.ToString("0.00", invariant)
                   + " floating=" + (gap > 0.05f ? "yes" : "no")
                   + " buried=" + (gap < -0.20f ? "yes" : "no")
                   + " grounded=" + (Mathf.Abs(gap) <= 0.05f ? "yes" : "no")
                   + " tops=" + (!float.IsNaN(lookTop) && Mathf.Abs(top - lookTop) <= 0.15f ? "match" : "differ")
                   + " tall=" + (top >= 2.5f && top <= 4.5f ? "yes" : "no");
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
