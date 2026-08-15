using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// The plantable ancient seed.
    ///
    /// Cloned from a vanilla crop sapling rather than assembled from nothing, because
    /// unlike the spirit this one genuinely wants a creature's worth of machinery: a
    /// Piece with placement rules, a WearNTear so it can be destroyed, a ZNetView, and
    /// the whole "can this go here" question that the cultivator already answers.
    ///
    /// What the donor's own `Plant` does is exactly what must not happen - it grows on
    /// a timer - so that component is the first thing torn out.
    /// </summary>
    internal static class SaplingPrefab
    {
        /// <summary>
        /// The network identity. ZNetScene keys prefabs by name.GetStableHashCode() and
        /// a saved ZDO stores that hash, so renaming this orphans every planted seed in
        /// a save. Treat it as permanent.
        /// </summary>
        public const string Name = "grove_ancient_sapling";

        private const string StageMesh = "grove_sapling_{0}.obj";
        private const int Stages = 4;

        private static GameObject _prefab;
        private static GameObject _holder;
        private static bool _addedToCultivator;

        public static bool Ready
        {
            get { return ZNetScene.instance != null && ZNetScene.instance.GetPrefab(Name) != null; }
        }

        /// <summary>Idempotent, and safe to call every frame until it takes.</summary>
        public static bool Register()
        {
            if (Ready && _addedToCultivator) return true;
            if (ZNetScene.instance == null || ObjectDB.instance == null) return false;

            if (_prefab == null)
            {
                _prefab = Build();
                if (_prefab == null) return false;
            }

            AddToScene();
            AddToCultivator();
            return Ready && _addedToCultivator;
        }

        // ------------------------------------------------------------------ building

        private static GameObject Donor()
        {
            var scene = ZNetScene.instance;

            foreach (var name in new[] { GroveConfig.SaplingDonor.Value, "sapling_carrot" })
            {
                if (string.IsNullOrEmpty(name)) continue;

                var found = scene.GetPrefab(name);
                if (found != null) return found;

                GrovePlugin.LogOnce("Sapling donor '" + name + "' does not exist.");
            }

            return null;
        }

        private static GameObject Build()
        {
            var source = Donor();
            if (source == null) return null;

            var directory = Path.GetDirectoryName(typeof(SaplingPrefab).Assembly.Location);
            var models = new ModelData[Stages];

            for (var i = 0; i < Stages; i++)
            {
                models[i] = ObjMesh.Load(
                    Path.Combine(directory, string.Format(StageMesh, i + 1)));

                if (models[i] != null) continue;

                GrovePlugin.Log.LogError(
                    "Sapling stage mesh " + (i + 1) + " is missing from beside the dll.");
                return null;
            }

            if (_holder == null)
            {
                _holder = new GameObject("GroveSaplingHolder");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }

            var previous = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;

            GameObject clone;
            try { clone = Object.Instantiate(source, _holder.transform); }
            finally { ZNetView.m_forceDisableInit = previous; }

            clone.name = Name;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one * GroveConfig.SaplingScale.Value;

            Strip(clone);
            Dress(clone);

            var stages = Visuals(clone, models);

            var sapling = clone.GetComponent<Sapling>();
            if (sapling == null) sapling = clone.AddComponent<Sapling>();
            sapling.Stages = stages;

            GrovePlugin.Log.LogInfo("Built " + Name + " from " + source.name + ".");
            return clone;
        }

        /// <summary>
        /// Everything the donor does that this must not.
        ///
        /// DestroyImmediate rather than Destroy: ordinary Destroy is deferred to the end
        /// of the frame, and this prefab is registered and can be planted within that
        /// frame - which would hand out a seed that really is a carrot.
        /// </summary>
        private static void Strip(GameObject clone)
        {
            // The timer. This is the whole reason the mod has its own component.
            foreach (var plant in clone.GetComponentsInChildren<Plant>(true))
                Object.DestroyImmediate(plant);

            // A crop you can pull up. The seed is not harvestable - the spirit is the
            // harvest, and leaving this on would let you pick the seed straight back up
            // for free the moment you planted it.
            foreach (var pickable in clone.GetComponentsInChildren<Pickable>(true))
                Object.DestroyImmediate(pickable);

            // Null-checked because destroying a renderer's GameObject takes its children with
            // it, and GetComponentsInChildren returns parents before descendants - so a
            // renderer deeper in the same branch is already gone by the time the loop reaches
            // it, and asking a destroyed Component for its gameObject throws. This surfaced as
            // an NRE every frame on a dedicated server, where the donor's hierarchy is nested
            // differently, but the bug was always here.
            foreach (var renderer in clone.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null) continue;
                Object.DestroyImmediate(renderer.gameObject);
            }
        }

        private static void Dress(GameObject clone)
        {
            var piece = clone.GetComponent<Piece>();
            if (piece == null) return;

            piece.m_name = GroveConfig.SaplingName.Value;
            piece.m_description = "It grows on what dies near it.";
            piece.m_resources = Requirements(GroveConfig.SaplingCost.Value);

            piece.m_groundPiece = true;
            piece.m_groundOnly = true;
            piece.m_cultivatedGroundOnly = GroveConfig.NeedsCultivated.Value;

            // Not comfort, not a crafting station, and emphatically not something a
            // raid should target ahead of your walls.
            piece.m_targetNonPlayerBuilt = false;
        }

        private static Transform[] Visuals(GameObject clone, ModelData[] models)
        {
            var stages = new Transform[models.Length];

            for (var i = 0; i < models.Length; i++)
            {
                var go = new GameObject("stage" + (i + 1));
                go.transform.SetParent(clone.transform, false);

                go.AddComponent<MeshFilter>().sharedMesh = models[i].Mesh;

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = Skins.Skin(models[i].Groups);

                // After Skin, because that is what measures each group's atlas rect.
                Skins.Remap(models[i].Mesh, models[i].Groups);

                // All off. Sapling.Show turns exactly one on, and leaving them all
                // enabled here would draw four saplings inside each other for the one
                // frame before Start runs.
                go.SetActive(false);

                stages[i] = go.transform;
            }

            return stages;
        }

        private static Piece.Requirement[] Requirements(string spec)
        {
            var list = new List<Piece.Requirement>();

            foreach (var entry in (spec ?? "").Split(','))
            {
                var parts = entry.Split(':');
                if (parts.Length != 2) continue;

                var itemName = parts[0].Trim();
                if (itemName.Length == 0) continue;

                int amount;
                if (!int.TryParse(parts[1].Trim(), out amount) || amount <= 0) continue;

                var prefab = ObjectDB.instance.GetItemPrefab(itemName);
                var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    GrovePlugin.LogOnce("Sapling cost mentions unknown item '" + itemName + "'.");
                    continue;
                }

                list.Add(new Piece.Requirement
                {
                    m_resItem = drop,
                    m_amount = amount,

                    // Not recoverable. An ancient seed put into the ground is spent -
                    // being able to plant one, change your mind, and get it back would
                    // make the whole ritual free to attempt.
                    m_recover = false
                });
            }

            return list.ToArray();
        }

        // ------------------------------------------------------------------ registering

        private static void AddToScene()
        {
            var scene = ZNetScene.instance;
            if (_prefab == null || scene.GetPrefab(Name) != null) return;

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
            }
        }

        /// <summary>
        /// Onto the cultivator, not the hammer.
        ///
        /// It is a seed. Putting it on the hammer would work and would be wrong - you
        /// would go looking for it under furniture, and the cultivator is already the
        /// tool you are holding when you think "I want to plant this".
        /// </summary>
        private static void AddToCultivator()
        {
            if (_addedToCultivator || _prefab == null) return;

            var tool = ObjectDB.instance.GetItemPrefab("Cultivator");
            var drop = tool != null ? tool.GetComponent<ItemDrop>() : null;
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) return;

            var table = drop.m_itemData.m_shared.m_buildPieces;
            if (table == null || table.m_pieces == null) return;

            if (!table.m_pieces.Contains(_prefab)) table.m_pieces.Add(_prefab);
            _addedToCultivator = true;

            GrovePlugin.Log.LogInfo("Ancient sapling added to the cultivator.");
        }
    }
}
