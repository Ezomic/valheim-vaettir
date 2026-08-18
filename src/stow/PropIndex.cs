using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stow
{
    /// <summary>
    /// Finds a loaded prefab by name, so a material can be lifted off it.
    ///
    /// This began life as Stoker's PropGraft, which borrows a whole vanilla prop and wears
    /// it. That is not what happens here: the post's shape is hand-modelled, and the only
    /// thing borrowed is the *surface* - one real material per group, so the piece is made
    /// of the game's own wood and the game's own iron rather than an approximation of them.
    /// A grafted prop would match the art and still be a crate the game already has.
    ///
    /// The grafting half was deleted rather than left dormant, because dead code that
    /// embodies a rejected approach is an invitation to take it again.
    /// </summary>
    internal static class PropIndex
    {
        private static Dictionary<string, GameObject> _index;

        /// <summary>
        /// ZNetScene only knows prefabs that carry a ZNetView, and most dressing props do
        /// not - they live as children inside location prefabs. So the scene is asked
        /// first, and everything Unity currently has loaded is searched second.
        /// </summary>
        public static GameObject Find(string name)
        {
            if (ZNetScene.instance != null)
            {
                var registered = ZNetScene.instance.GetPrefab(name);
                if (registered != null) return registered;
            }

            if (_index == null) BuildIndex();

            GameObject found;
            return _index.TryGetValue(name, out found) ? found : null;
        }

        private static void BuildIndex()
        {
            _index = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

            // Expensive, so it happens once. Prefabs are preferred over scene instances:
            // an instance carries whatever state the world has put on it.
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate == null || candidate.transform.parent != null) continue;
                if (candidate.GetComponentInChildren<MeshRenderer>(true) == null) continue;

                var inScene = candidate.scene.IsValid();
                GameObject existing;

                if (!_index.TryGetValue(candidate.name, out existing) || (!inScene && existing.scene.IsValid()))
                    _index[candidate.name] = candidate;
            }

            StowRuntime.Log.LogInfo("Prop index built: " + _index.Count + " candidates with meshes.");
        }

        /// <summary>
        /// Lists everything loaded whose name contains one of the given words, which is how
        /// to find a prefab worth borrowing a material from.
        ///
        /// Asking the index rather than the asset manifest, because the manifest catalogues
        /// every asset that exists rather than every asset that is loaded - Stoker's first
        /// candidate list came off the manifest and two of sixteen names resolved.
        /// </summary>
        public static void Search(string keywords)
        {
            if (string.IsNullOrEmpty(keywords)) return;
            if (_index == null) BuildIndex();

            foreach (var raw in keywords.Split(','))
            {
                var word = raw.Trim();
                if (word.Length == 0) continue;

                var hits = new List<string>();
                foreach (var pair in _index)
                {
                    if (pair.Key.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    // Broken and destruction variants are the same prop in pieces.
                    var lower = pair.Key.ToLowerInvariant();
                    if (lower.Contains("destruction") || lower.Contains("broken")
                        || lower.Contains("lod") || lower.Contains("vfx")
                        || lower.Contains("sfx")) continue;

                    hits.Add(pair.Key);
                }

                hits.Sort();
                StowRuntime.Log.LogInfo(
                    "Prefabs matching '" + word + "' (" + hits.Count + "): "
                    + string.Join(", ", hits.GetRange(0, Math.Min(40, hits.Count)).ToArray()));
            }
        }
    }
}
