using UnityEngine;
using Grove;
using HarmonyLib;

namespace Thicket
{
    /// <summary>
    /// A dug-up plant is carried, not pocketed.
    ///
    /// The first transplant design dropped an "uprooted plant" item and priced the
    /// replanting piece with it. He reversed that on 2026-08-27: "I don't want when you
    /// uproot that the bush goes into the inventory. You hold it and u plant it back
    /// down. U cannot do anything else except walking when carrying the bush." So this
    /// file is that sentence: digging puts the plant in your arms, walking is the whole
    /// verb set, and E on open ground plants it - no item, no build menu, no cost row.
    ///
    /// What carrying forbids, and how: running (SetRun forced false), jumping (Jump
    /// swallowed), attacking (StartAttack refused), the hotbar (UseHotbarItem refused),
    /// equipping (EquipItem refused), and every ordinary interaction (Player.Interact
    /// swallowed) - which is also what frees E to mean "plant it here". Walking, turning,
    /// sneaking and opening the map all still work; the restriction is the hands, not
    /// the legs.
    ///
    /// The plant itself is the seedling piece, spawned directly where you press E and
    /// growing through vanilla's Plant exactly as a menu-placed one would have. Wrong
    /// biome is not checked at the moment of planting for the same reason the pieces
    /// never checked it: Plant's own hover text says "wrong biome" on the standing
    /// seedling, which can be read and acted on - dug up again and walked further.
    ///
    /// Dying or logging out while carrying plants it at your feet: clumsy, but the
    /// plant is never destroyed. Conservation is the whole design; losing the bush to
    /// a death would be the one generous exception, in the wrong direction.
    /// </summary>
    [HarmonyPatch]
    internal static class Carry
    {
        private static WildPlant _held;
        private static GameObject _visual;
        private static bool _hidTool;
        private static bool _clipsSaid;

        internal static bool Carrying
        {
            get { return _held != null; }
        }

        /// <summary>Called by the dig. The visual rides the player's transform - crude
        /// beside a bone attach, but visible, and local-only in this version: other
        /// players see the plant vanish and reappear, not the carry.</summary>
        internal static void Begin(Player player, WildPlant plant, bool picked)
        {
            if (player == null || plant == null) return;

            _held = plant;

            _visual = WildPrefab.CarryVisual(plant, picked);
            if (_visual != null)
            {
                // The chest bone, not the player root: the root does not move with
                // the walk cycle, so a root-parented bush hung in space while the
                // body swayed around it. Ride the torso and it bobs like a held
                // thing. World position computed first, then parented with the world
                // kept, because a bone's local axes are whatever the rig says.
                Transform mount = player.transform;
                var animator = player.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                    if (chest != null) mount = chest;

                    // One-shot survey for the next iteration: does this rig even
                    // carry a hold/carry clip a pose could borrow? Read once, said
                    // once, and the answer decides whether a true hands-on pose is
                    // a clip override or a custom animation nobody has.
                    if (!_clipsSaid && animator.runtimeAnimatorController != null)
                    {
                        _clipsSaid = true;
                        var names = new System.Collections.Generic.List<string>();
                        foreach (var clip in animator.runtimeAnimatorController.animationClips)
                        {
                            if (clip == null) continue;
                            var lower = clip.name.ToLowerInvariant();
                            if (lower.Contains("carry") || lower.Contains("hold")
                                || lower.Contains("lift") || lower.Contains("push")
                                || lower.Contains("haul"))
                                names.Add(clip.name);
                        }
                        GrovePlugin.Log.LogInfo("Carry-ish clips on the player rig: "
                            + (names.Count == 0 ? "(none)" : string.Join(", ", names.ToArray())));
                    }
                }

                _visual.transform.position = player.transform.position
                    + player.transform.forward * 0.45f + Vector3.up * 1.05f;
                _visual.transform.rotation = player.transform.rotation;
                _visual.transform.SetParent(mount, true);
            }

            // Empty hands sell the carry, and vanilla does everything: the sheathe
            // animation, the tool onto the back, other players seeing it - and
            // leaving place mode, which is why planting is a plain E press. The
            // restore goes through reflection because ShowHandItems is protected,
            // and it must run AFTER the carry clears or our own equip-block eats it.
            _hidTool = player.HideHandItems();

            player.Message(MessageHud.MessageType.Center,
                "Carrying " + plant.Title + " - walk it somewhere its kind grows and "
                + "press E on open ground to plant it");
        }

        /// <summary>Ticked from the plugin's Update, after the registrations.</summary>
        internal static void Tick()
        {
            if (_held == null) return;

            var player = Player.m_localPlayer;
            if (player == null)
            {
                // The world went away - menu, or a teardown mid-session. Best effort:
                // if a scene still exists the plant goes down where we stood; if not,
                // it is gone, and that is the one hole this version accepts.
                Drop(null);
                return;
            }

            if (player.IsDead())
            {
                PlantAt(player.transform.position);
                return;
            }

            if (ZInput.GetButtonDown("Use") || ZInput.GetButtonDown("JoyUse"))
            {
                var camera = GameCamera.instance;
                if (camera == null) return;

                RaycastHit hit;
                var ray = new Ray(camera.transform.position, camera.transform.forward);
                if (Physics.Raycast(ray, out hit, 8f,
                        LayerMask.GetMask("terrain", "Default", "static_solid"))
                    && Vector3.Distance(hit.point, player.transform.position) < 6f
                    && hit.normal.y > 0.5f)
                {
                    PlantAt(hit.point);
                }
                else
                {
                    player.Message(MessageHud.MessageType.Center,
                        "No open ground there");
                }
            }
        }

        /// <summary>
        /// The tool click while carrying: plant at the ground under the crosshair.
        /// The same ray the dig uses, because in place mode there is no hover.
        /// </summary>
        internal static void TryPlantFromTool(Player player)
        {
            var camera = GameCamera.instance;
            if (camera == null || player == null) return;

            RaycastHit hit;
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            if (Physics.Raycast(ray, out hit, 8f,
                    LayerMask.GetMask("terrain", "Default", "static_solid"))
                && Vector3.Distance(hit.point, player.transform.position) < 6f
                && hit.normal.y > 0.5f)
            {
                PlantAt(hit.point);
            }
            else
            {
                player.Message(MessageHud.MessageType.Center, "No open ground there");
            }
        }

        private static void PlantAt(Vector3 where)
        {
            // The biome gate survives the seedling it used to live on. Plant.m_biome
            // did this refusing for the old seedling path; with the grown bush going
            // straight down, the same rule is asked of the ground here - and refused
            // with the carry kept, so a wrong biome costs a walk, never the bush.
            var biome = Heightmap.FindBiome(where);
            if (_held.Biomes != 0 && (_held.Biomes & biome) == 0)
            {
                var player0 = Player.m_localPlayer;
                if (player0 != null)
                    player0.Message(MessageHud.MessageType.Center,
                        _held.Title + " does not grow here - it wants "
                        + _held.Biomes);
                return;
            }

            // The GROWN vanilla prefab, not the seedling piece. The seedling was the
            // "transplant recovery" design - dig, carry, a seedling regrows into the
            // bush - and he struck it: "you pickup a bush, it should show im carrying
            // a bush. when planting it back down it should be that same bush." So the
            // same bush goes down that came up, immediately, picked-empty because its
            // berries already dropped into your hands at the dig, and regrowing them
            // on vanilla's own timer. The seedling prefabs stay registered for any
            // still standing, but nothing new plants one.
            var prefab = ZNetScene.instance != null
                ? ZNetScene.instance.GetPrefab(_held.Grown)
                : null;

            if (prefab == null)
            {
                // Refuse to end the carry rather than eat the plant: not resolving is
                // a registration race, and the next press can succeed.
                GrovePlugin.LogOnce(_held.Grown + " did not resolve; still carrying.");
                return;
            }

            var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var planted = Object.Instantiate(prefab, where, rotation);

            Pickable pickable;
            if (planted.TryGetComponent(out pickable))
                pickable.SetPicked(true);

            var player = Player.m_localPlayer;
            if (player != null)
            {
                player.Message(MessageHud.MessageType.TopLeft, _held.Title + " planted");
                player.RaiseSkill(Skills.SkillType.Farming, 1f);
            }

            Drop(null);
        }

        private static void Drop(string reason)
        {
            if (_visual != null) Object.Destroy(_visual);
            _visual = null;
            _held = null;

            if (_hidTool)
            {
                _hidTool = false;
                var player = Player.m_localPlayer;
                if (player != null && !player.IsDead())
                {
                    try
                    {
                        AccessTools.Method(typeof(Humanoid), "ShowHandItems",
                                new[] { typeof(bool), typeof(bool) })
                            .Invoke(player, new object[] { false, true });
                    }
                    catch (System.Exception e)
                    {
                        GrovePlugin.Log.LogWarning("Could not unsheathe: " + e.Message);
                    }
                }
            }
        }

        // ------------------------------------------------------- what carrying forbids

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "SetRun")]
        private static void NoRunning(Character __instance, ref bool run)
        {
            if (Carrying && __instance == Player.m_localPlayer) run = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "Jump")]
        private static bool NoJumping(Character __instance)
        {
            return !(Carrying && __instance == Player.m_localPlayer);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Humanoid), "StartAttack")]
        private static bool NoAttacking(Humanoid __instance, ref bool __result)
        {
            if (!Carrying || __instance != (Humanoid)Player.m_localPlayer) return true;
            __result = false;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "UseHotbarItem")]
        private static bool NoHotbar(Player __instance)
        {
            return !(Carrying && __instance == Player.m_localPlayer);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Humanoid), "EquipItem",
            typeof(ItemDrop.ItemData), typeof(bool))]
        private static bool NoEquipping(Humanoid __instance, ref bool __result)
        {
            if (!Carrying || __instance != (Humanoid)Player.m_localPlayer) return true;
            __result = false;
            return false;
        }

        /// <summary>
        /// Every ordinary interaction is swallowed, which is also what frees the Use
        /// press for Tick's ground-planting: a hovered chest no longer answers it.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "Interact")]
        private static bool NoInteracting(Player __instance)
        {
            return !(Carrying && __instance == Player.m_localPlayer);
        }
    }
}
