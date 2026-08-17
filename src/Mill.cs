using System.Collections.Generic;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// The bone mill. Press use and it takes bone out of your pack and starts turning.
    ///
    /// Not a Smelter, and that is a deliberate departure from riding a vanilla system.
    /// Smelter is one item in, one item out, added a single piece at a time through its own
    /// switch, and none of those three fit here: the recipe is two bones for one meal, the
    /// whole cost is meant to come out of your inventory in one press, and there is no fuel.
    /// Bending Smelter into that shape would have meant fighting every part of it.
    ///
    /// Not using Smelter also removes a problem it would have caused. Stow reads its item
    /// groups off Smelter.m_conversion, so a mill built on one would have quietly reclassified
    /// bone fragments as "ore" and bonemeal as a "bar", and a chest set to hold ore would have
    /// started collecting bones. That carve-out is now unnecessary rather than merely written.
    ///
    /// Everything that has to survive lives on the ZDO: how many batches are queued and when
    /// the current one started. So a mill keeps grinding across a reload, and every client
    /// sees the same state without any syncing of our own.
    /// </summary>
    internal class Mill : MonoBehaviour, Hoverable, Interactable
    {
        private const string ZQueue = "groveMillQueue";
        private const string ZStarted = "groveMillStarted";

        private ZNetView _nview;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
        }

        // ------------------------------------------------------------------ state

        /// <summary>Batches waiting, including the one being ground right now.</summary>
        private int Queue
        {
            get
            {
                return _nview != null && _nview.IsValid()
                    ? _nview.GetZDO().GetInt(ZQueue, 0) : 0;
            }
        }

        /// <summary>
        /// Seconds the current batch has been turning, or zero if nothing is.
        ///
        /// Measured against ZNet's clock rather than accumulated deltaTime, so it keeps
        /// running while the zone is unloaded and reads the same for everyone watching.
        /// </summary>
        private double Elapsed
        {
            get
            {
                if (_nview == null || !_nview.IsValid()) return 0.0;

                var started = _nview.GetZDO().GetLong(ZStarted, 0L);
                if (started == 0L) return 0.0;

                return (ZNet.instance.GetTime() - new System.DateTime(started)).TotalSeconds;
            }
        }

        private static float Seconds
        {
            get { return Mathf.Max(1f, GroveConfig.MillSeconds.Value); }
        }

        // ------------------------------------------------------------------ grinding

        /// <summary>
        /// Only the owner advances it, for the same reason only the owner grows a plant: two
        /// clients both finishing the same batch would spawn the output twice.
        /// </summary>
        private void Update()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;

            var queue = Queue;
            if (queue <= 0) return;

            var zdo = _nview.GetZDO();

            // Nothing started yet, so start it. This is also the path a mill takes after
            // finishing a batch with more still queued.
            if (zdo.GetLong(ZStarted, 0L) == 0L)
            {
                zdo.Set(ZStarted, ZNet.instance.GetTime().Ticks);
                return;
            }

            if (Elapsed < Seconds) return;

            zdo.Set(ZQueue, queue - 1);
            zdo.Set(ZStarted, 0L);

            Produce();
        }

        /// <summary>
        /// Drops what one batch is worth, above the mill so it falls clear rather than
        /// spawning inside the stone and being shoved somewhere by physics.
        /// </summary>
        private void Produce()
        {
            var prefab = ZNetScene.instance != null
                ? ZNetScene.instance.GetPrefab(BonemealPrefab.Name) : null;

            if (prefab == null)
            {
                GrovePlugin.LogOnce("The mill finished a batch but " + BonemealPrefab.Name
                                    + " is not registered, so it produced nothing.");
                return;
            }

            var yield = Mathf.Max(1, GroveConfig.MillYield.Value);
            var at = transform.position + Vector3.up * 1.15f + transform.forward * 0.35f;

            var drop = Object.Instantiate(prefab, at, Quaternion.identity);
            var item = drop.GetComponent<ItemDrop>();
            if (item == null) return;

            // SetStack rather than writing m_stack directly: it clamps to the item's own
            // max stack size and then writes the value through to the ZDO, so the pile on
            // the ground is still the right size after a reload. Setting the field alone
            // leaves the ZDO saying one.
            item.SetStack(yield);
        }

        // ------------------------------------------------------------------ interact

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            // Hold is how vanilla repeats an action, and repeating this one would empty a
            // pack of bone in a second without the player deciding to.
            if (hold) return false;

            var player = user as Player;
            if (player == null || _nview == null || !_nview.IsValid()) return false;

            var cost = Cost();
            if (cost == null || cost.Count == 0) return false;

            var space = Mathf.Max(1, GroveConfig.MillCapacity.Value) - Queue;
            if (space <= 0)
            {
                player.Message(MessageHud.MessageType.Center, Name() + " is full.");
                return false;
            }

            // How many batches the pack can pay for, capped by what will fit.
            var affordable = space;
            foreach (var need in cost)
            {
                var have = player.GetInventory().CountItems(need.Key);
                affordable = Mathf.Min(affordable, have / need.Value);
            }

            if (affordable <= 0)
            {
                player.Message(MessageHud.MessageType.Center,
                               "Nothing here to grind. " + CostText() + " for each.");
                return false;
            }

            // Claim before writing. A write to a ZDO you do not own is discarded in silence,
            // which would present as a mill that eats bone and never turns.
            _nview.ClaimOwnership();

            foreach (var need in cost)
                player.GetInventory().RemoveItem(need.Key, need.Value * affordable);

            _nview.GetZDO().Set(ZQueue, Queue + affordable);

            player.Message(MessageHud.MessageType.Center,
                           Name() + " begins to turn.");
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        // ------------------------------------------------------------------ hover

        public string GetHoverName()
        {
            return Name();
        }

        public string GetHoverText()
        {
            var queue = Queue;

            if (queue <= 0)
                return Localization.instance.Localize(
                    Name() + "\n[<color=yellow><b>$KEY_Use</b></color>] grind bone  ( "
                    + CostText() + " each )");

            var left = Mathf.Max(0f, Seconds - (float)Elapsed);

            return Localization.instance.Localize(string.Format(
                "{0}\ngrinding  ( {1} left, {2}s )\n"
                + "[<color=yellow><b>$KEY_Use</b></color>] add more",
                Name(), queue, Mathf.CeilToInt(left)));
        }

        private static string Name()
        {
            return GroveConfig.MillName.Value;
        }

        // ------------------------------------------------------------------ the recipe

        /// <summary>
        /// What one batch costs, as Item:Amount. Parsed rather than hardcoded so the mill can
        /// be repriced, and so a name that does not resolve is a readable warning rather than
        /// a mill that silently refuses everything.
        /// </summary>
        private static Dictionary<string, int> Cost()
        {
            var result = new Dictionary<string, int>();

            foreach (var part in GroveConfig.MillCost.Value.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0) continue;

                var split = trimmed.Split(':');
                int amount;
                if (split.Length != 2 || !int.TryParse(split[1].Trim(), out amount))
                {
                    GrovePlugin.LogOnce("Cannot read mill cost '" + trimmed
                                        + "' - expected Item:Amount.");
                    return null;
                }

                result[split[0].Trim()] = Mathf.Max(1, amount);
            }

            return result;
        }

        private static string CostText()
        {
            var cost = Cost();
            if (cost == null) return "?";

            var parts = new List<string>();
            foreach (var need in cost)
            {
                var prefab = ObjectDB.instance != null
                    ? ObjectDB.instance.GetItemPrefab(need.Key) : null;

                var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                var label = drop != null && drop.m_itemData != null
                    ? drop.m_itemData.m_shared.m_name : need.Key;

                parts.Add(need.Value + "x " + Localization.instance.Localize(label));
            }

            return string.Join(", ", parts.ToArray());
        }
    }
}
