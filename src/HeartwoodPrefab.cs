using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using Ezomic.Shared;

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

        /// <summary>
        /// Builds the heartwood once. Everything that happens to it afterwards - the scene,
        /// ObjectDB, and being put back into both on every world load - belongs to
        /// Ezomic.Shared.Prefabs, which GrovePlugin hands this method to in Awake.
        ///
        /// Called with a scene and an item database in existence, so it may look donors and
        /// materials up freely.
        /// </summary>
        // ------------------------------------------------------------------ building

        internal static GameObject Build()
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

            // The hidden holder and the init suppression both come from Prefabs now. The
            // suppression is the load-bearing half: a clone taken under an active parent runs
            // its ZNetView's Awake and tries to network-register itself while half-built.
            var clone = Prefabs.Clone(source, Name);
            if (clone == null) return null;

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
            // The components, not the GameObjects they sit on.
            //
            // This used to destroy renderer.gameObject outright, and on this donor the
            // collider is on that same object - so stripping the donor's mesh took its
            // collision with it, and a dropped heartwood fell through the floor. An
            // ItemDrop carries a Rigidbody; with nothing to rest on it simply keeps
            // going. Found by throwing one away.
            //
            // Removing two components leaves the hierarchy, the colliders and anything
            // else the donor was carrying exactly where the ItemDrop expects them, and
            // is strictly less destructive for no cost. The old approach was inherited
            // from the pieces, where the donor's collider is elsewhere and taking the
            // whole object is harmless - which is exactly why it was never noticed.
            foreach (var renderer in clone.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null) continue;

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null) Object.DestroyImmediate(filter);

                Object.DestroyImmediate(renderer);
            }

            var visual = new GameObject("heartwood_visual");
            visual.transform.SetParent(clone.transform, false);

            visual.AddComponent<MeshFilter>().sharedMesh = model.Mesh;

            var renderer2 = visual.AddComponent<MeshRenderer>();
            renderer2.sharedMaterials = Skins.Skin(model.Groups);
            Skins.Remap(model.Mesh, model.Groups);

            Ground(clone, model);
        }

        /// <summary>
        /// Makes sure it has something to land on.
        ///
        /// Insurance behind the fix above rather than the fix itself: the donor's own
        /// collider should survive now that only components are stripped, and this
        /// catches the case where a donor never had one. The failure it guards against
        /// is unusually bad for an item - it falls through the world and is gone, with
        /// no message and nothing to pick back up, and the only clue is that the thing
        /// you dropped is not where you dropped it.
        ///
        /// Sized off the mesh rather than the donor, because the donor is a surtling
        /// core and this is a nest half again as wide.
        /// </summary>
        private static void Ground(GameObject clone, ModelData model)
        {
            if (clone.GetComponentInChildren<Collider>(true) != null) return;
            if (model.Mesh == null) return;

            var bounds = model.Mesh.bounds;

            var box = clone.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;

            GrovePlugin.LogOnce("Donor " + GroveConfig.HeartwoodDonor.Value + " left no "
                                + "collider on " + Name + " - added one from the mesh "
                                + "bounds so it does not fall through the world.");
        }

        // ------------------------------------------------------------------ registering

    }
}
