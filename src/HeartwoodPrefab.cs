using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// Heartwood: where the spirit goes, and what the stowing post is built from.
    ///
    /// Not a trophy and not a drop. The spirit does not die and hand over a piece of
    /// itself - it folds itself into this and you carry it, and building with it is
    /// how you put it somewhere. That reading costs nothing mechanically and fixes
    /// the one sour note in the chain: an hour of killing greydwarfs ending in you
    /// taking the heart out of the thing it summoned made the spirit another thing in
    /// the forest with loot in it, which is exactly what Interact was written to
    /// avoid. A home is the only version where the violence buys something that is
    /// not more violence.
    ///
    /// Cloned from a vanilla material item rather than assembled, because an item needs
    /// a great deal of machinery that has nothing to do with what it looks like - a
    /// ZNetView, an ItemDrop, a Rigidbody, colliders, the float-in-water behaviour and
    /// the auto-pickup radius. All of that comes across; only the mesh, the name and
    /// the icon change.
    ///
    /// The icon is a PNG rendered in Blender and read off disk. Valheim builds its own
    /// item icons from a camera rig in the editor and there is none of that at runtime,
    /// so Sprite.Create over a loaded texture is the whole of it.
    /// </summary>
    internal static class HeartwoodPrefab
    {
        /// <summary>
        /// The network identity, and what recipes name. ZNetScene and ObjectDB both key
        /// on name.GetStableHashCode(), and a saved ZDO or a saved inventory stores that
        /// hash - so renaming this orphans every one already in a world. Permanent.
        /// </summary>
        public const string Name = "GroveHeartwood";

        private const string Mesh = "grove_heartwood.obj";
        private const string Icon = "grove_heartwood_icon.png";

        private static GameObject _prefab;
        private static GameObject _holder;

        public static bool Ready
        {
            get
            {
                return ObjectDB.instance != null
                       && ObjectDB.instance.GetItemPrefab(Name) != null;
            }
        }

        /// <summary>Idempotent, and safe to call every frame until it takes.</summary>
        public static bool Register()
        {
            if (Ready) return true;
            if (ZNetScene.instance == null || ObjectDB.instance == null) return false;

            if (_prefab == null)
            {
                _prefab = Build();
                if (_prefab == null) return false;
            }

            AddToObjectDB();
            AddToScene();
            return Ready;
        }

        // ------------------------------------------------------------------ building

        private static GameObject Build()
        {
            var directory = Path.GetDirectoryName(typeof(HeartwoodPrefab).Assembly.Location);

            var model = ObjMesh.Load(Path.Combine(directory, Mesh));
            if (model == null)
            {
                GrovePlugin.Log.LogError("No " + Mesh + " beside the dll - cannot build "
                                         + Name + ".");
                return null;
            }

            var source = Donor();
            if (source == null) return null;

            if (_holder == null)
            {
                _holder = new GameObject("GroveHeartwoodHolder");
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

            Visual(clone, model);

            var drop = clone.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
            {
                GrovePlugin.Log.LogError("Donor " + source.name + " has no usable ItemDrop.");
                return null;
            }

            // Instantiate deep-copies serialized fields, and SharedData is [Serializable],
            // so this is our own copy rather than the donor's. Writing to the donor's
            // would rename every surtling core in the world.
            var shared = drop.m_itemData.m_shared;

            shared.m_name = GroveConfig.HeartwoodName.Value;
            // Reads as occupied rather than as salvage. "Still warm, something was
            // living in it" was the old line and it described a carcass - past tense,
            // and the spirit already gone. It is not gone. It is in there.
            shared.m_description = "Warm, and heavier than wood should be. The spirit "
                                   + "is folded up inside, waiting to be put somewhere.";
            shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
            shared.m_maxStackSize = Mathf.Max(1, GroveConfig.HeartwoodStack.Value);
            shared.m_weight = 0.5f;
            shared.m_teleportable = true;
            shared.m_questItem = false;

            var icon = Icons.Load(Icon, Name);
            if (icon != null) shared.m_icons = new[] { icon };

            drop.m_itemData.m_stack = 1;
            drop.m_itemData.m_dropPrefab = clone;

            GrovePlugin.Log.LogInfo("Built " + Name + " from " + source.name + ".");
            return clone;
        }

        private static GameObject Donor()
        {
            var scene = ZNetScene.instance;

            foreach (var name in new[] { GroveConfig.HeartwoodDonor.Value, "SurtlingCore", "Wood" })
            {
                if (string.IsNullOrEmpty(name)) continue;

                var found = scene.GetPrefab(name);
                if (found != null) return found;

                GrovePlugin.LogOnce("Heartwood donor '" + name + "' does not exist.");
            }

            return null;
        }

        private static void Visual(GameObject clone, ModelData model)
        {
            // Null-checked: destroying a renderer's GameObject takes its children with it, and
            // GetComponentsInChildren lists parents first, so a nested renderer is already
            // destroyed when the loop reaches it and asking it for its gameObject throws.
            foreach (var renderer in clone.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null) continue;
                Object.DestroyImmediate(renderer.gameObject);
            }

            var visual = new GameObject("heartwood_visual");
            visual.transform.SetParent(clone.transform, false);

            visual.AddComponent<MeshFilter>().sharedMesh = model.Mesh;

            var renderer2 = visual.AddComponent<MeshRenderer>();
            renderer2.sharedMaterials = Skins.Skin(model.Groups);
            Skins.Remap(model.Mesh, model.Groups);
        }

        // ------------------------------------------------------------------ registering

        /// <summary>
        /// Into ObjectDB, and then its lookup tables rebuilt.
        ///
        /// m_items alone is not enough: GetItemPrefab reads m_itemByHash, which is
        /// built once in UpdateRegisters and never again. Without the rebuild the item
        /// exists in the list and cannot be found by name - which looks exactly like it
        /// was never added.
        /// </summary>
        private static void AddToObjectDB()
        {
            var db = ObjectDB.instance;
            if (_prefab == null || db == null || db.GetItemPrefab(Name) != null) return;

            if (!db.m_items.Contains(_prefab)) db.m_items.Add(_prefab);

            try
            {
                AccessTools.Method(typeof(ObjectDB), "UpdateRegisters").Invoke(db, null);
            }
            catch (System.Exception e)
            {
                GrovePlugin.Log.LogError("Could not refresh ObjectDB for " + Name + ": "
                                         + e.Message);
            }
        }

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
    }
}
