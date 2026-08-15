using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// The visitor: a vaettr that turns up once you have given one a home.
    ///
    /// Cloned from Haldor rather than built, and that is not the usual reluctant clone -
    /// it is the only option there is. A talking NPC needs a rig, an idle, a talk
    /// animation and a store window, and this repo has no Unity editor and therefore no
    /// animation controller. The Surtling rip made the same point for creatures: the
    /// geometry comes out fine and the animation never can.
    ///
    /// What is ours is the part that matters anyway - who it is, why it came, and what
    /// it will sell you.
    ///
    /// Trader turns out to carry far more than a shop: conditional dialogue keyed on
    /// global keys, item turn-ins, idle chatter, greetings. Only the shop is wired up
    /// here. The rest is sitting there for free and is worth coming back to, because
    /// this mod family writes global keys all over the place and an NPC that remarks on
    /// them costs config rather than code.
    /// </summary>
    internal static class TraderPrefab
    {
        /// <summary>
        /// Permanent, like every other prefab name here. ZNetScene keys on
        /// name.GetStableHashCode() and a saved ZDO stores that hash, so renaming this
        /// deletes any visitor already standing in a world.
        /// </summary>
        public const string Name = "GroveVisitor";

        private static GameObject _prefab;
        private static GameObject _holder;

        public static bool Ready
        {
            get
            {
                return ZNetScene.instance != null
                       && ZNetScene.instance.GetPrefab(Name) != null;
            }
        }

        /// <summary>Idempotent, and safe to call every frame until it takes.</summary>
        public static bool Register()
        {
            if (!GroveConfig.TraderEnabled.Value) return true;
            if (Ready) return true;
            if (ZNetScene.instance == null || ObjectDB.instance == null) return false;

            // After the heartwood, because the stock list is resolved through ObjectDB
            // and a price list naming an item that is not registered yet would quietly
            // come out empty.
            if (!HeartwoodPrefab.Ready) return false;

            if (_prefab == null)
            {
                _prefab = Build();
                if (_prefab == null) return false;
            }

            AddToScene();
            return Ready;
        }

        private static GameObject Build()
        {
            var source = ZNetScene.instance.GetPrefab(GroveConfig.TraderDonor.Value);
            if (source == null)
            {
                GrovePlugin.LogOnce("Trader donor '" + GroveConfig.TraderDonor.Value
                                    + "' does not exist - no visitor will come.");
                return null;
            }

            if (_holder == null)
            {
                _holder = new GameObject("GroveVisitorHolder");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }

            var previous = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;

            GameObject clone;
            try { clone = Object.Instantiate(source, _holder.transform); }
            finally { ZNetView.m_forceDisableInit = previous; }

            clone.name = Name;

            var trader = clone.GetComponent<Trader>();
            if (trader == null)
            {
                GrovePlugin.LogOnce("Donor " + source.name + " has no Trader component - "
                                    + "it is not an NPC that can be talked to.");
                return null;
            }

            Dress(trader);
            Describe(clone, trader);

            return clone;
        }

        private static void Dress(Trader trader)
        {
            trader.m_name = GroveConfig.TraderName.Value;

            // Its own stock, not Haldor's. Instantiate deep-copies the list, so clearing
            // it cannot reach through to the real trader and empty his shop - but it is
            // worth saying out loud, because the day that assumption is wrong the
            // symptom is a Haldor who sells nothing and no obvious reason why.
            trader.m_items = Stock();

            // Haldor's own lines are about Haldor. Left empty rather than written,
            // because a line is either right or it is filler, and the conditional
            // dialogue this component supports deserves being done properly rather than
            // padded out now.
            trader.m_randomTalk = new List<string>();
            trader.m_randomGreets = new List<string>();
            trader.m_randomStartTrade = new List<string>();
            trader.m_randomSell = new List<string>();
            trader.m_randomBuy = new List<string>();
        }

        /// <summary>
        /// Reads Prefab:Stack:Price entries into Trader's own list type.
        ///
        /// Prices are in coins, because StoreGui counts them against a single
        /// m_coinPrefab and there is no seam for a second currency that does not mean
        /// rewriting the window.
        /// </summary>
        private static List<Trader.TradeItem> Stock()
        {
            var list = new List<Trader.TradeItem>();

            foreach (var entry in (GroveConfig.TraderStock.Value ?? "").Split(','))
            {
                var parts = entry.Split(':');
                if (parts.Length < 2) continue;

                var itemName = parts[0].Trim();
                if (itemName.Length == 0) continue;

                var prefab = ObjectDB.instance.GetItemPrefab(itemName);
                var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    GrovePlugin.LogOnce("Trader stock mentions unknown item '"
                                        + itemName + "'.");
                    continue;
                }

                int stack;
                if (!int.TryParse(parts[1].Trim(), out stack) || stack <= 0) stack = 1;

                int price;
                if (parts.Length < 3 || !int.TryParse(parts[2].Trim(), out price) || price <= 0)
                    price = 100;

                list.Add(new Trader.TradeItem
                {
                    m_prefab = drop,
                    m_stack = stack,
                    m_price = price,
                    m_requiredGlobalKey = ""
                });
            }

            return list;
        }

        /// <summary>
        /// Says what it built, and what the donor turned out to be made of.
        ///
        /// The same trick Fortify used on the sapling: rather than rip Haldor first and
        /// design against the report, build it defensively and let the log answer the
        /// question. Whether this thing is a Character - and therefore whether a
        /// greydwarf can kill your trader - is the answer that matters most, and it is
        /// one line here instead of a round trip.
        /// </summary>
        private static void Describe(GameObject clone, Trader trader)
        {
            var character = clone.GetComponent<Character>();
            var nview = clone.GetComponent<ZNetView>();

            GrovePlugin.Log.LogInfo(
                "Built " + Name + " from " + GroveConfig.TraderDonor.Value + ": "
                + trader.m_items.Count + " item(s) for sale, "
                + (character != null
                    ? "is a Character with " + character.GetMaxHealth() + " health"
                    : "is not a Character")
                + ", " + (nview != null && nview.m_persistent
                    ? "persistent" : "NOT persistent") + ".");
        }

        /// <summary>
        /// ZNetScene needs both the list and the private dictionary. The dictionary is
        /// built once in Awake and never rebuilt, so adding to the list alone leaves the
        /// prefab unresolvable - which is indistinguishable from never registering it.
        /// </summary>
        private static void AddToScene()
        {
            var scene = ZNetScene.instance;
            if (_prefab == null || scene == null) return;

            if (!scene.m_prefabs.Contains(_prefab)) scene.m_prefabs.Add(_prefab);

            try
            {
                var named = (Dictionary<int, GameObject>)
                    AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs").GetValue(scene);
                named[Name.GetStableHashCode()] = _prefab;
            }
            catch (System.Exception e)
            {
                GrovePlugin.Log.LogError("Could not register " + Name + ": " + e.Message);
                return;
            }

            GrovePlugin.Log.LogInfo("Registered " + Name + " with ZNetScene.");
        }
    }
}
