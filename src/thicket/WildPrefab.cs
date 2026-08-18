using System.Collections.Generic;
using System.IO;
using Grove;
using HarmonyLib;
using UnityEngine;

namespace Thicket
{
    /// <summary>
    /// Builds one seedling prefab out of a vanilla plant and one of our meshes.
    ///
    /// The ancient sapling tears the donor's `Plant` out because it must not grow on a
    /// timer. This is the opposite case and keeps it: a berry bush growing on a clock is
    /// exactly right, and the game's own component already answers sunlight, spacing,
    /// biome, cultivation, cold and the growing itself. Reconfiguring five fields on a
    /// vanilla component is a great deal less code than a custom one, and it is the half
    /// that keeps working when the game updates.
    ///
    /// The one thing that has to be done by hand is the visuals. Plant.SUpdate calls
    /// m_healthy.SetActive and m_unhealthy.SetActive with no null check of any kind - only
    /// m_healthyGrown is tested - so tearing the donor's meshes out and leaving those
    /// private fields pointing at destroyed objects throws a MissingReferenceException
    /// every ten seconds, per plant, forever. They are assigned below.
    /// </summary>
    internal static class WildPrefab
    {
        private static readonly AccessTools.FieldRef<Plant, GameObject> HealthyRef =
            AccessTools.FieldRefAccess<Plant, GameObject>("m_healthy");

        private static readonly AccessTools.FieldRef<Plant, GameObject> UnhealthyRef =
            AccessTools.FieldRefAccess<Plant, GameObject>("m_unhealthy");

        private static readonly AccessTools.FieldRef<Plant, GameObject> HealthyGrownRef =
            AccessTools.FieldRefAccess<Plant, GameObject>("m_healthyGrown");

        private static readonly AccessTools.FieldRef<Plant, GameObject> UnhealthyGrownRef =
            AccessTools.FieldRefAccess<Plant, GameObject>("m_unhealthyGrown");

        private static GameObject _holder;

        /// <summary>
        /// One shared cache of loaded meshes, because four of the eight plants wear the
        /// same one. Loading thicket_bush.obj three times would parse the same file three
        /// times and hand out three copies of a mesh that is never written to.
        /// </summary>
        private static readonly Dictionary<string, ModelData> Meshes =
            new Dictionary<string, ModelData>();

        public static GameObject Build(WildPlant plant)
        {
            var scene = ZNetScene.instance;
            if (scene == null || ObjectDB.instance == null) return null;

            var donor = Donor(scene);
            if (donor == null) return null;

            var grown = scene.GetPrefab(plant.Grown);
            if (grown == null)
            {
                // Skipped rather than thrown. The prefab list this roster was written from
                // is the manifest on disk, which says what exists rather than what is
                // loaded - two of Stoker's sixteen candidates resolved off that same
                // source. So a name that is not there costs one plant and says which.
                WildPlants.Warn(plant.Id + ": there is no prefab called " + plant.Grown
                             + " in this world, so it cannot be planted.");
                return null;
            }

            // Plant.Grow does grown.GetComponent<ZNetView>().SetLocalScale(...) with no
            // check, so a grown prefab without one is a null dereference at the moment of
            // growing - an hour after planting, in someone else's session, with nothing to
            // connect it to this mod. Refused here instead.
            if (grown.GetComponent<ZNetView>() == null)
            {
                WildPlants.Warn(plant.Id + ": " + plant.Grown + " has no ZNetView, so growing "
                             + "one would throw inside Plant.Grow. Skipped.");
                return null;
            }

            var model = Mesh(plant);
            if (model == null) return null;

            if (_holder == null)
            {
                _holder = new GameObject("ThicketHolder");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }

            // Inside an inactive holder with init suppressed, or the clone tries to
            // network-register itself while it is still half-built.
            var previous = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;

            GameObject clone;
            try { clone = Object.Instantiate(donor, _holder.transform); }
            finally { ZNetView.m_forceDisableInit = previous; }

            // Instantiate appends "(Clone)", and the name is the hash is the identity.
            clone.name = plant.PieceName;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one * Mathf.Max(0.1f, ThicketConfig.Scale.Value);

            Strip(clone);
            var stages = Visuals(clone, model);
            Sow(clone, plant, grown, stages);
            Dress(clone, plant);

            GrovePlugin.Log.LogInfo("Thicket built " + plant.PieceName + " from " + donor.name
                                    + ", growing into " + plant.Grown + ".");
            return clone;
        }

        private static GameObject Donor(ZNetScene scene)
        {
            foreach (var name in new[] { ThicketConfig.Donor.Value, "sapling_carrot" })
            {
                if (string.IsNullOrEmpty(name)) continue;

                var found = scene.GetPrefab(name);
                if (found != null) return found;

                GrovePlugin.LogOnce("Thicket donor " + name + " does not exist.");
            }

            return null;
        }

        private static ModelData Mesh(WildPlant plant)
        {
            ModelData cached;
            if (Meshes.TryGetValue(plant.Model, out cached)) return cached;

            var directory = Path.GetDirectoryName(typeof(WildPrefab).Assembly.Location);
            var loaded = ObjMesh.Load(Path.Combine(directory, plant.Model));

            if (loaded == null)
                GrovePlugin.LogOnce("Thicket model " + plant.Model + " is missing from beside "
                                    + "the dll, so " + plant.Id + " cannot be built.");

            Meshes[plant.Model] = loaded;
            return loaded;
        }

        /// <summary>
        /// Everything the donor looks like, and the one thing it does that this must not.
        ///
        /// The Pickable goes because the donor is a crop you can pull up for the vegetable,
        /// and leaving it on would let you plant five raspberries and immediately pick the
        /// seedling back for a carrot.
        ///
        /// Null-checked in the renderer loop because destroying a renderer's GameObject
        /// takes its children with it, and GetComponentsInChildren returns parents before
        /// descendants - so a renderer deeper in the same branch is already gone by the time
        /// the loop reaches it, and asking a destroyed Component for its gameObject throws.
        /// This is the sapling's bug, which cost 46,000 stack traces in one session.
        /// </summary>
        private static void Strip(GameObject clone)
        {
            foreach (var pickable in clone.GetComponentsInChildren<Pickable>(true))
                Object.DestroyImmediate(pickable);

            foreach (var renderer in clone.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null) continue;
                Object.DestroyImmediate(renderer.gameObject);
            }
        }

        /// <summary>
        /// Four visuals from one mesh: young and half-grown, each healthy and wilting.
        ///
        /// Plant already swaps between them - m_healthy and m_unhealthy on status, and
        /// m_healthyGrown at the halfway mark if it is set - so a second stage costs a
        /// scale factor rather than a second model. That is worth having where the sapling's
        /// four hand-built stages were not: the same shape at 60% and at 100% reads as
        /// something growing, where four different silhouettes read as four different
        /// plants unless every one of them is good.
        ///
        /// The wilting pair is the same mesh leaned over. A plant in the wrong biome says so
        /// in its hover text and vanilla also *shows* it, and a seedling that looks identical
        /// whether or not it will ever grow throws that away for nothing.
        /// </summary>
        private static GameObject[] Visuals(GameObject clone, ModelData model)
        {
            var young = Stage(clone, model, "healthy", 0.6f, 0f);
            var youngSick = Stage(clone, model, "unhealthy", 0.6f, 22f);
            var half = Stage(clone, model, "healthy_grown", 1f, 0f);
            var halfSick = Stage(clone, model, "unhealthy_grown", 1f, 22f);

            return new[] { young, youngSick, half, halfSick };
        }

        private static GameObject Stage(GameObject clone, ModelData model, string name,
                                       float scale, float lean)
        {
            var go = new GameObject(name);
            go.transform.SetParent(clone.transform, false);
            go.transform.localScale = Vector3.one * scale;
            go.transform.localRotation = Quaternion.Euler(lean, 0f, 0f);

            go.AddComponent<MeshFilter>().sharedMesh = model.Mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = Skins.Skin(model.Groups);

            // After Skin, which is what measures each group's rect in the donor's atlas.
            // Once per mesh rather than once per stage: the four stages share one Mesh
            // object, so remapping four times would squeeze its UVs into the same
            // rectangle four times over and collapse the texture to a point.
            if (Remapped.Add(model.Mesh.GetInstanceID()))
                Skins.Remap(model.Mesh, model.Groups);

            // All four off. Plant.SUpdate turns exactly one on, and leaving them enabled
            // would draw four seedlings inside each other until its first slow update -
            // which is up to ten seconds after planting.
            go.SetActive(false);

            return go;
        }

        private static readonly HashSet<int> Remapped = new HashSet<int>();

        /// <summary>
        /// The plant itself: what it becomes, where, and how long it takes.
        /// </summary>
        private static void Sow(GameObject clone, WildPlant plant, GameObject grown,
                                GameObject[] stages)
        {
            var component = clone.GetComponent<Plant>();
            if (component == null) component = clone.AddComponent<Plant>();

            component.m_name = plant.Title;
            component.m_grownPrefabs = new[] { grown };

            component.m_growTime = plant.GrowMin;
            component.m_growTimeMax = plant.GrowMax;

            // The grown bush at its own size. The donor lerps between these to vary its
            // vegetables, and a raspberry bush that came up at 70% of a raspberry bush
            // would read as a different, smaller plant rather than as variety.
            component.m_minScale = 1f;
            component.m_maxScale = 1f;

            component.m_growRadius = Mathf.Max(0.1f,
                plant.Radius * Mathf.Max(0.1f, ThicketConfig.SpacingScale.Value));

            component.m_biome = plant.Biomes;
            component.m_tolerateCold = plant.TolerateCold;

            // Wild ground, by the call that was made: no tilling. A hedge along a path
            // without turning the path to dirt is the point of it.
            component.m_needCultivatedGround = false;

            // Left standing rather than deleted when it cannot grow. m_destroyIfCantGrow
            // would quietly remove a seedling planted a metre inside the wrong biome, an
            // hour later, with no message - and the player's five raspberries with it. It
            // sits there saying "wrong biome" in its hover text instead, which can be read
            // and acted on.
            component.m_destroyIfCantGrow = false;

            // Not a vine. A non-zero attach distance sends UpdateHealth looking for a wall
            // to cling to and reports NoAttachPiece when there is none.
            component.m_attachDistance = 0f;

            HealthyRef(component) = stages[0];
            UnhealthyRef(component) = stages[1];
            HealthyGrownRef(component) = stages[2];
            UnhealthyGrownRef(component) = stages[3];
        }

        private static void Dress(GameObject clone, WildPlant plant)
        {
            var piece = clone.GetComponent<Piece>();
            if (piece == null) return;

            piece.m_name = plant.Title;
            piece.m_description = Describe(plant);
            piece.m_resources = Requirements(plant);

            var icon = Icons.Load(plant.Icon, plant.PieceName);
            if (icon != null) piece.m_icon = icon;

            piece.m_groundPiece = true;
            piece.m_groundOnly = true;

            // No tilling, by the call that was made. The donor is a crop and demands it.
            piece.m_cultivatedGroundOnly = false;

            // The biome gate at placement time rather than an hour later.
            //
            // Plant.m_biome already refuses to grow outside it, but the only thing that
            // reports is hover text on a seedling you have already paid for. m_onlyInBiome
            // is the same bitmask read by Player.UpdatePlacementGhost, which turns the ghost
            // red and says "wrong biome" before the berries leave your inventory. Both are
            // set, from the one list, because they answer at different moments.
            piece.m_onlyInBiome = plant.Biomes;

            // Not something a raid should walk past your walls to get to.
            piece.m_targetNonPlayerBuilt = false;
        }

        /// <summary>
        /// The build-menu description, with the level in it.
        ///
        /// Without this the only sign of a locked plant is that its entry is greyed out,
        /// which is the same thing the game shows for missing materials - so it reads as
        /// "find more berries" and sends you through your chests for berries you are already
        /// carrying. The level is the one fact that cannot be guessed from anywhere else.
        /// </summary>
        private static string Describe(WildPlant plant)
        {
            var text = "Plant it where it grows wild, and it will come up in its own time.";
            if (!ThicketConfig.SayTheLevel.Value || plant.Level <= 0) return text;

            return text + "\nNeeds Farming " + plant.Level + ".";
        }

        private static Piece.Requirement[] Requirements(WildPlant plant)
        {
            var prefab = ObjectDB.instance.GetItemPrefab(plant.CostItem);
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;

            if (drop == null)
            {
                WildPlants.Warn(plant.Id + ": there is no item called " + plant.CostItem
                             + ", so it would cost nothing at all. Left free - fix the row.");
                return new Piece.Requirement[0];
            }

            return new[]
            {
                new Piece.Requirement
                {
                    m_resItem = drop,
                    m_amount = plant.CostAmount,

                    // Spent, like the ancient seed. Being able to plant a bush, change your
                    // mind and get the berries back would make siting one free to attempt,
                    // and the siting - biome, sunlight, room to grow - is the whole of the
                    // decision being made.
                    m_recover = false
                }
            };
        }
    }
}
