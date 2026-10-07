using System;
using System.Collections.Generic;
using SoftReferenceableAssets;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// Loads one vanilla material by asset name, straight out of the game's own bundles,
    /// whether or not anything in the world has it loaded.
    ///
    /// The jib's wicker and cord wear the village container sheet, and the props that carry
    /// it are children of a location nobody is standing in, so walking loaded prefabs for it
    /// finds nothing. Kynda met the same wall with fi_village_wood and wrote the longer
    /// version of this; this is the part Vaettir needs, kept in step with it by reading, not
    /// by sharing a type, because the two mods cannot depend on each other.
    ///
    /// Nothing is ever released, and that is a correctness requirement. When a soft
    /// reference's count reaches zero its bundle is unloaded with unloadAllLoadedObjects, and
    /// a bundle that only the world held is unloaded at logout, destroying the material under
    /// a piece that is still drawing with it. A held reference is what keeps this one alive.
    /// </summary>
    internal static class SoftAssets
    {
        /// <summary>
        /// Asks the loader to read the extended manifest, which is where materials are
        /// listed. Plugin Awake and nowhere else: the flag is read once, when the loader is
        /// built on first use, and a later call logs an error and does nothing.
        /// </summary>
        public static void MakeEverythingLoadable()
        {
            try
            {
                Runtime.MakeAllAssetsLoadable();
            }
            catch (Exception e)
            {
                GrovePlugin.Log.LogWarning("Could not enable the extended asset manifest: "
                    + e.Message + ". Materials that only a location carries will fall back.");
            }
        }

        private static Dictionary<string, AssetID> _paths;
        private static bool _pathsFailed;

        private static readonly Dictionary<string, Material> Loaded =
            new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<SoftReference<Material>> Held =
            new List<SoftReference<Material>>();

        public static Material LoadMaterial(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            Material already;
            if (Loaded.TryGetValue(name, out already) && already != null) return already;

            try
            {
                AssetID id;
                if (!TryFindId(name, ".mat", out id)) return null;

                var reference = new SoftReference<Material>(id);
                var result = reference.Load();
                if (result != LoadResult.Succeeded)
                {
                    GrovePlugin.Log.LogWarning("The asset '" + name + "' is listed but "
                        + "would not load (" + result + ").");
                    return null;
                }

                var material = reference.Asset;
                if (material == null) return null;

                Held.Add(reference);
                Loaded[name] = material;
                GrovePlugin.Log.LogInfo("Loaded the material '" + name + "' straight from its bundle.");
                return material;
            }
            catch (Exception e)
            {
                GrovePlugin.Log.LogWarning("Could not load '" + name
                    + "' through the asset loader: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// The id of the one asset whose manifest path ends in /name.extension. By path
        /// suffix rather than a stored id, so a game update that moves the asset shows up
        /// here as "not listed" and the caller falls back, instead of resolving a stale id
        /// to nothing.
        /// </summary>
        private static bool TryFindId(string name, string extension, out AssetID id)
        {
            id = default(AssetID);

            if (_paths == null && !_pathsFailed)
            {
                try
                {
                    _paths = Runtime.GetAllAssetPathsInBundleMappedToAssetID();
                }
                catch (Exception e)
                {
                    _pathsFailed = true;
                    GrovePlugin.Log.LogWarning("Could not read the asset manifest: " + e.Message);
                }
            }
            if (_paths == null) return false;

            var suffix = "/" + name + extension;

            foreach (var entry in _paths)
            {
                if (entry.Key == null) continue;
                if (!entry.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                if (!entry.Value.IsValid) continue;

                id = entry.Value;
                return true;
            }

            return false;
        }
    }
}
