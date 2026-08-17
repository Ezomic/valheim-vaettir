using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// What bonemeal does: pushes the plant you are looking at further along its own growth.
    ///
    /// The mechanism is not a new timer, it is the game's. Plant.SUpdate grows purely on
    /// TimeSincePlanted() measured against a per-plant grow time, and the planted moment
    /// lives on the ZDO as s_plantTime. So fertilising is moving that moment *earlier*, and
    /// everything else follows for free: the plant scales up as it always did, growth is
    /// still owned and driven by the owning client, it survives a reload because it was
    /// written to the ZDO rather than held in memory, and a modded crop with its own grow
    /// time is advanced by a share of *its* time rather than by a flat number of seconds.
    ///
    /// The alternative was calling Plant.Grow() outright. That is one line and wrong: it
    /// matures anything instantly, which turns a fertiliser into a harvest button and makes
    /// the whole Farming skill moot.
    /// </summary>
    [HarmonyPatch]
    internal static class Fertilise
    {
        /// <summary>Plant.GetGrowTime is private, and it is the only thing that knows how
        /// long *this* plant takes - the seeded lerp between m_growTime and m_growTimeMax.</summary>
        private static readonly MethodInfoCache GrowTime =
            new MethodInfoCache(typeof(Plant), "GetGrowTime");

        /// <summary>
        /// Remembered between the gate and the effect.
        ///
        /// CanConsumeItem is where a refusal has to happen, and ConsumeItem is where the
        /// item is actually spent, so the target is found once in the first and used in the
        /// second. Finding it twice would open a window where the gate says yes about one
        /// plant and the effect lands on another.
        /// </summary>
        private static Plant _target;

        // ------------------------------------------------------------------ the gate

        /// <summary>
        /// Refuse here, and only here.
        ///
        /// Player.ConsumeItem calls EatFood and then inventory.RemoveOneItem regardless of
        /// what EatFood returned, so refusing later in the chain destroys the item and gives
        /// nothing back. CanConsumeItem is the gate that path respects, and it is where
        /// vanilla puts its own $msg_cantconsume.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.CanConsumeItem))]
        private static bool Gate(Player __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!IsBonemeal(item)) return true;

            _target = FindTarget(__instance);

            if (_target == null)
            {
                __instance.Message(MessageHud.MessageType.Center, "$msg_cantconsume");
                __result = false;
                return false;
            }

            __result = true;
            return false;
        }

        // ------------------------------------------------------------------ the effect

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem))]
        private static void Apply(ItemDrop.ItemData item, bool __result)
        {
            if (!__result || !IsBonemeal(item)) return;

            var target = _target;
            _target = null;
            if (target == null) return;

            Advance(target);

            var radius = Mathf.Max(0f, GroveConfig.BonemealRadius.Value);
            if (radius <= 0f) return;

            // Optional, and off by default. One press per plant is the constrained reading
            // and the one that stays honest next to Furrow sowing twenty at a time; a radius
            // is here because somebody with a full field will want it and would otherwise
            // reach for a mod that does far more than this.
            var hits = Physics.OverlapSphere(target.transform.position, radius);
            for (var i = 0; i < hits.Length; i++)
            {
                var other = hits[i].GetComponentInParent<Plant>();
                if (other == null || other == target) continue;
                Advance(other);
            }
        }

        /// <summary>
        /// Moves the planted moment earlier by a share of this plant's own grow time.
        /// </summary>
        private static void Advance(Plant plant)
        {
            var view = plant.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return;

            // Writing to a ZDO you do not own is discarded silently, which would present as
            // bonemeal simply doing nothing on someone else's crop. This is what vanilla's
            // own Take All does before it writes to a container.
            view.ClaimOwnership();

            var zdo = view.GetZDO();
            if (zdo == null) return;

            var growTime = GrowTime.Invoke<float>(plant);
            if (growTime <= 0f) return;

            var share = Mathf.Clamp01(GroveConfig.BonemealAdvance.Value);
            var seconds = growTime * share;

            var planted = zdo.GetLong(ZDOVars.s_plantTime, ZNet.instance.GetTime().Ticks);
            var moved = planted - (long)(seconds * System.TimeSpan.TicksPerSecond);

            zdo.Set(ZDOVars.s_plantTime, moved);

            // And mark it, so the crop it grows into gives more when it is picked. Set rather
            // than counted: a second bonemeal on the same plant brings more time forward but
            // does not stack the harvest, so the yield cannot be farmed by standing there
            // feeding one carrot.
            zdo.Set(Fertilised, true);

            if (GroveConfig.Verbose.Value)
                GrovePlugin.Log.LogInfo("Fertilised " + plant.name + ": brought "
                                        + seconds.ToString("0") + "s of its "
                                        + growTime.ToString("0") + "s forward.");
        }

        // ------------------------------------------------------------------ the harvest

        /// <summary>
        /// The mark that says this thing was fed. Written on the plant, carried onto the crop
        /// it grows into, and read when that crop is picked.
        ///
        /// A ZDO value rather than a component, because the plant is destroyed the moment it
        /// grows and the crop that replaces it is a different object with a different ZDO.
        /// Anything held in memory would be lost across that hand-off, and everything held in
        /// memory is lost on a reload anyway - a field that has been fertilised and then slept
        /// through would quietly forget.
        /// </summary>
        private static readonly int Fertilised = "grove_fertilised".GetStableHashCode();

        private static bool _grewFertilised;

        /// <summary>
        /// Read the mark off the plant before it is gone. Grow() instantiates the crop and
        /// destroys the plant, so the postfix has nothing left to read it from.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Plant), nameof(Plant.Grow))]
        private static void RememberMark(Plant __instance)
        {
            _grewFertilised = false;

            var zdo = ZdoOf(__instance);
            if (zdo == null) return;

            _grewFertilised = zdo.GetBool(Fertilised, false);
        }

        /// <summary>
        /// ...and put it on the crop. Grow returns null when the plant could not grow after
        /// all, which is why this is guarded rather than assumed.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Plant), nameof(Plant.Grow))]
        private static void PlaceMark(GameObject __result)
        {
            if (!_grewFertilised) return;
            _grewFertilised = false;

            if (__result == null) return;

            var view = __result.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return;

            var zdo = view.GetZDO();
            if (zdo == null) return;

            zdo.Set(Fertilised, true);
        }

        /// <summary>
        /// A fertilised crop yields more when picked.
        ///
        /// Through RPC_Pick's own <c>bonus</c> argument, which is the game's existing channel
        /// for exactly this - it is what the Farming skill's max-level bonus yield already
        /// travels down. So the extra goes through vanilla's own drop loop: world drop
        /// scaling still applies, m_extraDrops still fire, the drops still spread out around
        /// the plant. Writing a second drop path beside it would have reimplemented all three
        /// and got at least one of them wrong.
        ///
        /// The base amount is recomputed exactly as RPC_Pick recomputes it a few lines later,
        /// rather than guessed at from m_amount, so a doubling is a doubling of what you
        /// would actually have received on this world's drop settings.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Pickable), "RPC_Pick")]
        private static void Bounty(Pickable __instance, ref int bonus)
        {
            var multiplier = Mathf.Max(1f, GroveConfig.BonemealHarvest.Value);
            if (multiplier <= 1f) return;

            var view = __instance.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner()) return;

            var zdo = view.GetZDO();
            if (zdo == null || !zdo.GetBool(Fertilised, false)) return;

            var amount = __instance.m_dontScale
                ? __instance.m_amount
                : Mathf.Max(__instance.m_minAmountScaled,
                            Game.instance.ScaleDrops(__instance.m_itemPrefab, __instance.m_amount));

            bonus += Mathf.RoundToInt(amount * (multiplier - 1f));

            // Spent. A pickable with a respawn time - a berry bush rather than a crop - would
            // otherwise stay doubled for the rest of the world's life off one use.
            zdo.Set(Fertilised, false);
        }

        private static ZDO ZdoOf(Component component)
        {
            if (component == null) return null;

            var view = component.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return null;

            return view.GetZDO();
        }

        // ------------------------------------------------------------------ finding it

        /// <summary>
        /// The plant under the crosshair, and only if it is actually able to grow.
        ///
        /// Status rather than a bare component check: a plant in the wrong biome, on
        /// uncultivated ground, or crowded by its neighbours will never grow however much
        /// time is brought forward, and spending the item on one would be a silent no-op -
        /// the exact failure shape this codebase keeps producing.
        /// </summary>
        private static Plant FindTarget(Player player)
        {
            var hovering = player.GetHoverObject();
            if (hovering == null) return null;

            var plant = hovering.GetComponentInParent<Plant>();
            if (plant == null) return null;

            return plant.GetStatus() == Plant.Status.Healthy ? plant : null;
        }

        private static bool IsBonemeal(ItemDrop.ItemData item)
        {
            if (item == null || item.m_dropPrefab == null) return false;
            return item.m_dropPrefab.name == BonemealPrefab.Name;
        }
    }

    /// <summary>
    /// A private method looked up once rather than on every call. Reflection per press would
    /// be fine here; caching it is habit worth keeping in something that could later run on
    /// every plant in a radius.
    /// </summary>
    internal sealed class MethodInfoCache
    {
        private readonly System.Reflection.MethodInfo _method;

        public MethodInfoCache(System.Type type, string name)
        {
            _method = AccessTools.Method(type, name);
            if (_method == null)
                GrovePlugin.Log.LogError("Could not find " + type.Name + "." + name
                                         + " - bonemeal will do nothing.");
        }

        public T Invoke<T>(object instance)
        {
            if (_method == null) return default(T);

            try { return (T)_method.Invoke(instance, null); }
            catch (System.Exception e)
            {
                GrovePlugin.Log.LogError("Calling " + _method.Name + " failed: " + e.Message);
                return default(T);
            }
        }
    }
}
