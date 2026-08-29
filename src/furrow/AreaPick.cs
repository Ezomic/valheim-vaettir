using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Furrow
{
    /// <summary>
    /// Shift+E on a ripe crop harvests its neighbours too, out to a radius Farming
    /// earns you.
    ///
    /// **Why Shift+E and not a key.** Pickable.Interact already takes an `alt` argument
    /// and never once reads it - the same free gesture Container has - so the game hands
    /// this over without anything being taken away. Plain E is untouched and still picks
    /// exactly one, which matters: an area harvest you cannot switch off is a mod that
    /// decides when you wanted the whole bed.
    ///
    /// **What it will and will not take.** Only the grown stage of something you can
    /// plant. That set is not a list here - it is read off the game, by walking every
    /// prefab carrying a Plant and collecting its m_grownPrefabs. So a crop added by
    /// another mod is included the day it is added, and no wild berry, mushroom,
    /// thistle or dandelion ever is. Those are Thicket's business and picking a forest
    /// clean from one keypress is a different, far more generous mod than this one.
    ///
    /// By default it also takes only the crop you actually clicked, so a mixed bed is
    /// harvested a kind at a time rather than stripped in one press. PickSameCropOnly
    /// turns that off for anyone who wants the whole patch.
    ///
    /// **Every pick goes through vanilla's own Interact**, one call per crop, rather
    /// than reaching into RPC_Pick. That is what keeps the skill gain, the level bonus
    /// roll, the effects, the drop scaling and - most importantly - the ownership and
    /// networking identical to picking each one by hand. An area harvest that dropped
    /// items the server disagreed about would be the kind of bug that only appears with
    /// other people watching.
    /// </summary>
    [HarmonyPatch]
    internal static class AreaPick
    {
        /// <summary>
        /// Guards the recursion. Every neighbour is picked with alt = false, so their
        /// own postfix declines to spread - but a re-entrancy flag costs nothing and
        /// the failure it prevents is an infinite one.
        /// </summary>
        private static bool _running;

        /// <summary>Grown-crop prefab names, read from the game once per world.</summary>
        private static readonly HashSet<string> _crops = new HashSet<string>();
        private static int _cropsForScene;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
        private static void Spread(Pickable __instance, Humanoid character, bool alt)
        {
            if (!alt || _running) return;
            if (!FurrowConfig.PickArea.Value) return;

            var player = character as Player;
            if (player == null || player != Player.m_localPlayer) return;

            var level = player.GetSkillFactor(Skills.SkillType.Farming) * 100f;
            if (level < FurrowConfig.PickLevel.Value) return;

            var crop = Utils.GetPrefabName(__instance.gameObject.name);
            if (!IsCrop(crop)) return;

            var radius = Radius(level);
            if (radius <= 0f) return;

            var picked = 0;
            var cap = Mathf.Max(1, FurrowConfig.PickMax.Value);
            var centre = __instance.transform.position;
            var sameOnly = FurrowConfig.PickSameCropOnly.Value;

            _running = true;
            try
            {
                // FindObjectsOfType rather than an OverlapSphere, deliberately. This
                // runs once on a keypress and never per frame, and a sphere would need
                // a layer mask - which means guessing which layer a crop's collider
                // sits on, for every crop including ones other mods add. Asking for the
                // components themselves cannot be wrong about that.
                foreach (var other in Object.FindObjectsOfType<Pickable>())
                {
                    if (picked >= cap) break;
                    if (other == null || other == __instance) continue;

                    if ((other.transform.position - centre).sqrMagnitude > radius * radius)
                        continue;

                    var name = Utils.GetPrefabName(other.gameObject.name);
                    if (sameOnly ? name != crop : !IsCrop(name)) continue;

                    if (AlreadyPicked(other)) continue;

                    // alt: false - this is the flag that stops each neighbour spreading
                    // in turn. The re-entrancy guard above is the belt to its braces.
                    other.Interact(character, false, false);
                    picked++;
                }
            }
            finally { _running = false; }

            if (picked == 0) return;

            player.Message(MessageHud.MessageType.TopLeft,
                "Harvested " + (picked + 1) + " " + __instance.GetHoverName());
        }

        /// <summary>
        /// How far the harvest reaches, from the level that unlocks it to the level
        /// that maxes it. Below the unlock this is off entirely and Shift+E is an
        /// ordinary pick.
        /// </summary>
        private static float Radius(float level)
        {
            var from = FurrowConfig.PickLevel.Value;
            var to = Mathf.Max(from + 1, FurrowConfig.PickAtLevel.Value);

            var t = Mathf.Clamp01((level - from) / (to - from));
            return Mathf.Lerp(Mathf.Max(0f, FurrowConfig.PickRadiusMin.Value),
                              Mathf.Max(0f, FurrowConfig.PickRadius.Value), t);
        }

        /// <summary>
        /// Picked already? Read off the ZDO rather than through reflection: Pickable
        /// keeps m_picked private but loads it from ZDOVars.s_picked, which is the
        /// authoritative copy and the one a client that has not simulated the pick yet
        /// still agrees with.
        /// </summary>
        private static bool AlreadyPicked(Pickable pickable)
        {
            ZNetView nview;
            if (!pickable.TryGetComponent(out nview)) return true;
            if (!nview.IsValid()) return true;

            var zdo = nview.GetZDO();
            return zdo != null && zdo.GetBool(ZDOVars.s_picked, pickable.m_defaultPicked);
        }

        /// <summary>
        /// Is this the grown stage of a plantable crop?
        ///
        /// Built from the world rather than from a list, and rebuilt when the world
        /// changes - ZNetScene is torn down and remade on every world load, so a set
        /// answered from a stale flag would be a set describing the previous world.
        /// </summary>
        private static bool IsCrop(string name)
        {
            var scene = ZNetScene.instance;
            if (scene == null) return false;

            if (_crops.Count == 0 || _cropsForScene != scene.GetInstanceID())
            {
                _crops.Clear();
                _cropsForScene = scene.GetInstanceID();

                foreach (var prefab in scene.m_prefabs)
                {
                    if (prefab == null) continue;

                    Plant plant;
                    if (!prefab.TryGetComponent(out plant)) continue;
                    if (plant.m_grownPrefabs == null) continue;

                    foreach (var grown in plant.m_grownPrefabs)
                        if (grown != null) _crops.Add(grown.name);
                }

                Grove.GrovePlugin.Log.LogInfo(
                    "Area harvest knows " + _crops.Count + " grown crops in this world.");
            }

            return _crops.Contains(name);
        }
    }
}
