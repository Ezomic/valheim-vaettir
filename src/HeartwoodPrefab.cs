using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// Heartwood: what the spirit hands over, and what the stowing post is built from.
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
            shared.m_description = "Still warm. Something was living in it.";
            shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
            shared.m_maxStackSize = Mathf.Max(1, GroveConfig.HeartwoodStack.Value);
            shared.m_weight = 0.5f;
            shared.m_teleportable = true;
            shared.m_questItem = false;

            var icon = LoadIcon(Path.Combine(directory, Icon));
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
            foreach (var renderer in clone.GetComponentsInChildren<MeshRenderer>(true))
                Object.DestroyImmediate(renderer.gameObject);

            var visual = new GameObject("heartwood_visual");
            visual.transform.SetParent(clone.transform, false);

            visual.AddComponent<MeshFilter>().sharedMesh = model.Mesh;

            var renderer2 = visual.AddComponent<MeshRenderer>();
            renderer2.sharedMaterials = Skins.Skin(model.Groups);
            Skins.Remap(model.Mesh, model.Groups);
        }

        /// <summary>
        /// The icon, read off disk.
        ///
        /// Point filtering would be wrong here even though everything else in these
        /// mods wants it: the source is 128px and the inventory draws it smaller, so
        /// it is always being minified, and point-sampling a minified image is how you
        /// get a shimmering mess as the slot moves.
        /// </summary>
        private static Sprite LoadIcon(string path)
        {
            if (!File.Exists(path))
            {
                GrovePlugin.Log.LogWarning(
                    "No " + Icon + " beside the dll - " + Name + " will use the donor's "
                    + "icon, which is someone else's picture.");
                return null;
            }

            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                if (!LoadPng(texture, File.ReadAllBytes(path))) return null;

                texture.name = Name + "_icon";
                texture.hideFlags = HideFlags.HideAndDontSave;

                return Sprite.Create(texture,
                                     new Rect(0f, 0f, texture.width, texture.height),
                                     new Vector2(0.5f, 0.5f));
            }
            catch (System.Exception e)
            {
                GrovePlugin.Log.LogError("Could not read " + Icon + ": " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Texture2D.LoadImage, by reflection.
        ///
        /// It lives in UnityEngine.ImageConversionModule, which targets netstandard 2.1
        /// while this builds against net462 - referencing it outright fails the build
        /// with CS1705. The method is present at runtime regardless, so reaching it
        /// this way costs one lookup and removes the whole problem.
        /// </summary>
        private static bool LoadPng(Texture2D texture, byte[] data)
        {
            var type = AccessTools.TypeByName("UnityEngine.ImageConversion");
            if (type == null)
            {
                GrovePlugin.Log.LogWarning("UnityEngine.ImageConversion is missing - "
                                           + "cannot read the icon.");
                return false;
            }

            var method = AccessTools.Method(type, "LoadImage",
                                            new[] { typeof(Texture2D), typeof(byte[]) })
                         ?? AccessTools.Method(type, "LoadImage",
                                               new[] { typeof(Texture2D), typeof(byte[]),
                                                       typeof(bool) });

            if (method == null)
            {
                GrovePlugin.Log.LogWarning("No LoadImage overload found on "
                                           + "UnityEngine.ImageConversion.");
                return false;
            }

            var args = method.GetParameters().Length == 3
                ? new object[] { texture, data, false }
                : new object[] { texture, data };

            return (bool)method.Invoke(null, args);
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
