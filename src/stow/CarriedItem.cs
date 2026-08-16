using System.Collections.Generic;
using UnityEngine;

namespace Stow
{
    /// <summary>
    /// A display-only copy of an item's own model, for the carrier to hold.
    ///
    /// The real item is never handed to the spirit - it stays in the post until the trip
    /// lands, which is the whole reason nothing can be lost mid-flight. So what flies is
    /// a picture of it, and everything that makes an item prefab an *item* has to come
    /// off: its ItemDrop, its ZNetView, its collider, its rigidbody, and its pick-me-up
    /// sparkle. What is left is a mesh.
    ///
    /// Using the item's own model rather than a generic crate is most of what makes the
    /// trip worth watching. "Something is being carried" is a nice animation; "my ore is
    /// being carried, and now my seeds are" is the mod telling you what it is doing.
    /// </summary>
    internal static class CarriedItem
    {
        /// <summary>Longest dimension, in metres, once scaled.</summary>
        private const float CarrySize = 0.17f;

        private static readonly Dictionary<string, GameObject> Templates =
            new Dictionary<string, GameObject>();

        private static GameObject _holder;

        /// <summary>
        /// Forgets every stripped template.
        ///
        /// Same reasoning as PostModel.Invalidate: a new world may have a different item
        /// set, and a template built from the last world's prefab is a reference into a
        /// scene that has been torn down.
        /// </summary>
        public static void Invalidate()
        {
            foreach (var template in Templates.Values)
                if (template != null) Object.Destroy(template);

            Templates.Clear();
        }

        /// <summary>A fresh model for this item, or null if it has no usable one.</summary>
        public static GameObject For(ItemDrop.ItemData item)
        {
            return For(ItemGroups.PrefabNameOf(item));
        }

        /// <summary>
        /// The same, from a prefab name alone.
        ///
        /// This is the overload the multiplayer path needs. The item never leaves the
        /// owner's post, so a remote client has no ItemData to ask - only the name that
        /// came over the wire.
        /// </summary>
        public static GameObject For(string prefabName)
        {
            var template = Template(prefabName);
            if (template == null) return null;

            var copy = Object.Instantiate(template);
            copy.SetActive(true);
            return copy;
        }

        private static GameObject Template(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return null;

            GameObject cached;
            if (Templates.TryGetValue(prefabName, out cached)) return cached;

            // Cached even when it fails. A stack the game has no model for would
            // otherwise be looked up on every trip for the life of the session.
            Templates[prefabName] = null;

            if (ObjectDB.instance == null) return null;

            var source = ObjectDB.instance.GetItemPrefab(prefabName);
            if (source == null) return null;

            if (_holder == null)
            {
                _holder = new GameObject("StowCarriedItems");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }

            // Inside a disabled holder with init suppressed, or the clone tries to
            // network-register itself while it is still an item.
            var previous = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;

            GameObject clone;
            try { clone = Object.Instantiate(source, _holder.transform); }
            finally { ZNetView.m_forceDisableInit = previous; }

            clone.name = "carried_" + prefabName;

            Strip(clone);

            var display = Fit(clone);
            if (display == null)
            {
                Object.DestroyImmediate(clone);
                return null;
            }

            Templates[prefabName] = display;
            return display;
        }

        /// <summary>
        /// Everything that is not geometry, removed.
        ///
        /// DestroyImmediate rather than Destroy: Destroy is deferred to the end of the
        /// frame, and this template is instantiated from within the same frame it is
        /// built in - so a deferred strip would be copied before it happened.
        ///
        /// Collected first and destroyed second because destroying as we walk invalidates
        /// the arrays underneath. ParticleSystems get their own pass: stripping
        /// MonoBehaviours does not touch them, and an item model that keeps its pick-me-up
        /// sparkle would trail glitter across the room behind the spirit.
        /// </summary>
        private static void Strip(GameObject clone)
        {
            var doomed = new List<Component>();

            foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null) doomed.Add(behaviour);

            foreach (var collider in clone.GetComponentsInChildren<Collider>(true))
                if (collider != null) doomed.Add(collider);

            foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true))
                if (body != null) doomed.Add(body);

            foreach (var particles in clone.GetComponentsInChildren<ParticleSystem>(true))
                if (particles != null) doomed.Add(particles);

            foreach (var trail in clone.GetComponentsInChildren<TrailRenderer>(true))
                if (trail != null) doomed.Add(trail);

            // A torch model brings a real light with it. One per trip, moving, is a
            // lighting change the player did not ask for - and the carrier has its own.
            foreach (var light in clone.GetComponentsInChildren<Light>(true))
                if (light != null) doomed.Add(light);

            foreach (var audio in clone.GetComponentsInChildren<AudioSource>(true))
                if (audio != null) doomed.Add(audio);

            foreach (var component in doomed)
                if (component != null) Object.DestroyImmediate(component);

            // The renderer survives its particle system; the pair has to go together or
            // the leftover renderer draws nothing and costs a draw call anyway.
            foreach (var renderer in clone.GetComponentsInChildren<ParticleSystemRenderer>(true))
                if (renderer != null) Object.DestroyImmediate(renderer);

            foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>
        /// Scales the model down to something a spirit could plausibly be holding, and
        /// tips it out of true.
        ///
        /// Measured off the meshes rather than off Renderer.bounds, because the template
        /// is built inside a disabled holder and an inactive renderer's world bounds are
        /// not filled in - every item came out the same size when that was the source.
        ///
        /// The tilt matters more than it sounds. Item and trophy prefabs are modelled
        /// face-up rather than standing, so hung level every load reads as a sticker
        /// lying flat in the air; a few degrees off every axis is what makes it an object.
        ///
        /// Returns the object to hand out - an outer wrapper holding the scaled, centred,
        /// tilted model - or null when there is nothing to draw, which is a real case: a
        /// few items are pure logic with a placeholder model.
        ///
        /// Two levels rather than one, because Carrier.Carry squares the thing it is
        /// given up against the hook - it sets localPosition and localRotation on
        /// whatever it is handed. A tilt applied to the outer object would be wiped by
        /// that. The outer object is therefore left at identity and everything is done to
        /// the model inside it.
        /// </summary>
        private static GameObject Fit(GameObject clone)
        {
            var bounds = new Bounds();
            var found = false;

            var root = clone.transform.worldToLocalMatrix;

            foreach (var filter in clone.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter == null || filter.sharedMesh == null) continue;
                Grow(ref bounds, ref found, root * filter.transform.localToWorldMatrix,
                     filter.sharedMesh.bounds);
            }

            foreach (var skinned in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned == null || skinned.sharedMesh == null) continue;
                Grow(ref bounds, ref found, root * skinned.transform.localToWorldMatrix,
                     skinned.sharedMesh.bounds);
            }

            if (!found) return null;

            var longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (longest <= 0.0001f) return null;

            var scale = CarrySize / longest;
            var tilt = Quaternion.Euler(-24f, 17f, 9f);

            var pivot = new GameObject(clone.name + "_display");
            pivot.transform.SetParent(_holder.transform, false);

            clone.transform.SetParent(pivot.transform, false);

            // Recentred as well as rescaled, and in that order: a model whose mesh sits
            // off its own origin - most of them - would otherwise hang beside the sling
            // rather than on it. The offset is put through the tilt as well, or rotating
            // an off-centre model swings it back out of the sling it was just centred in.
            clone.transform.localRotation = tilt;
            clone.transform.localScale = new Vector3(scale, scale, scale);
            clone.transform.localPosition = -(tilt * (bounds.center * scale));

            return pivot;
        }

        private static void Grow(ref Bounds bounds, ref bool found, Matrix4x4 local,
                                 Bounds mesh)
        {
            // Both corners, not the centre: a bounding box is defined by its extremes,
            // and encapsulating centres gives a box that fits none of the meshes.
            var min = local.MultiplyPoint3x4(mesh.min);
            var max = local.MultiplyPoint3x4(mesh.max);

            if (!found)
            {
                bounds = new Bounds(min, Vector3.zero);
                found = true;
            }

            bounds.Encapsulate(min);
            bounds.Encapsulate(max);
        }
    }
}
