using UnityEngine;

namespace Stow
{
    /// <summary>
    /// The halo, borrowed off a vanilla lamp.
    ///
    /// This exists because the obvious approach does not work, and it took a rip to find
    /// out. Valheim's glowing things are `Standard` materials with an `_EmissionMap` and
    /// an `_EmissionColor` used as a multiplier - and that map is a texture, almost
    /// entirely black, with the glow painted onto a handful of texels.
    /// DvergrTownLantern_e is 64x64 and black apart from one small grid of orange squares
    /// where the window panes are.
    ///
    /// So borrowing an emissive material and remapping our UVs into its *albedo* atlas
    /// rect - which is what PostModel does, correctly, for wood and iron - lands the
    /// heartwood's UVs on black. It would not glow at all, or would glow by accident.
    ///
    /// What every glowing prefab in the game actually carries is a child
    /// `flare [ParticleSystem, ParticleSystemRenderer]` wearing `light_glow`, next to a
    /// point light. So that is what gets cloned. Grafting an *effect* is riding a vanilla
    /// system, which is the thing this repo does everywhere; it is not the same as wearing
    /// a vanilla prop, which is the thing it does not do.
    /// </summary>
    internal static class Flare
    {
        /// <summary>
        /// The material name that identifies a flare, as opposed to the three or four
        /// other billboards a prefab like guard_stone hangs off itself - it also carries
        /// `glow`, `glow_pulse` and two `gnista` spark emitters, and none of those is the
        /// soft halo wanted here.
        /// </summary>
        private const string FlareMaterial = "light_glow";

        private static GameObject _template;
        private static bool _searched;

        public static void Invalidate()
        {
            _template = null;
            _searched = false;
        }

        /// <summary>
        /// Hangs a flare under a transform. Null if no donor has one, and the caller is
        /// expected to carry on - the Light is what makes a spirit readable, and the flare
        /// is what makes it look like the game's own.
        /// </summary>
        public static GameObject Attach(Transform parent, float scale)
        {
            var template = Template();
            if (template == null || parent == null) return null;

            var flare = Object.Instantiate(template, parent, false);
            flare.name = "flare";
            flare.transform.localPosition = Vector3.zero;
            flare.transform.localRotation = Quaternion.identity;
            flare.transform.localScale = new Vector3(scale, scale, scale);
            flare.SetActive(true);

            return flare;
        }

        private static GameObject Template()
        {
            if (_template != null || _searched) return _template;
            _searched = true;

            foreach (var raw in (StowConfig.FlareDonors.Value ?? "").Split(','))
            {
                var name = raw.Trim();
                if (name.Length == 0) continue;

                var donor = PropIndex.Find(name);
                if (donor == null) continue;

                var found = FindFlare(donor);
                if (found == null) continue;

                _template = Clone(found);
                if (_template == null) continue;

                StowRuntime.Log.LogInfo("Flare borrowed from " + name + "/" + found.name + ".");
                return _template;
            }

            StowRuntime.Log.LogWarning(
                "No flare donor resolved - spirits will be lit but will not halo. Set "
                + "FlareDonors to a prefab that has one.");

            return null;
        }

        private static GameObject FindFlare(GameObject donor)
        {
            foreach (var renderer in donor.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;

                var material = renderer.sharedMaterial;
                if (material == null) continue;

                // StartsWith, because Unity appends " (Instance)" to a material the
                // moment anything has touched it.
                if (!material.name.StartsWith(FlareMaterial)) continue;

                return renderer.gameObject;
            }

            return null;
        }

        /// <summary>
        /// A copy of the flare object, kept in a disabled holder, with the game's own
        /// scripts taken off it.
        ///
        /// The ParticleSystem and its renderer are exactly what is wanted and are left
        /// alone - unlike a carried item's model, where the particles are the pick-me-up
        /// sparkle and have to go. What comes off is MonoBehaviours: a donor's flare may
        /// carry LightLod or an effect script that expects a parent this clone will not
        /// have.
        /// </summary>
        private static GameObject Clone(GameObject source)
        {
            var holder = new GameObject("StowFlareTemplate");
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);

            var previous = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;

            GameObject clone;
            try { clone = Object.Instantiate(source, holder.transform); }
            finally { ZNetView.m_forceDisableInit = previous; }

            foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null) Object.DestroyImmediate(behaviour);

            return clone;
        }
    }
}
