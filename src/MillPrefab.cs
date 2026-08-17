using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// Registers the bone mill as a buildable piece.
    ///
    /// Cloned rather than built from nothing, because the clone is what carries the Piece,
    /// the WearNTear and the ZNetView that make it a real buildable, damageable, networked
    /// object. Rebuilding those by hand is exactly the work cloning avoids. Everything the
    /// donor does beyond that is torn out, and the body is ours.
    /// </summary>
    internal static class MillPrefab
    {
        /// <summary>
        /// The network identity. ZNetScene keys on this and saved ZDOs store the hash, so it
        /// is permanent: rename it later and every mill already standing is discarded.
        /// </summary>
        public const string Name = "grove_mill";

        private const string Mesh = "grove_mill.obj";

        private static GameObject _prefab;
        private static GameObject _holder;

        /// <summary>
        /// Asked of the live scene rather than of a static field. Loading a second world,
        /// including logging out to the menu and back, builds a new ZNetScene whose named
        /// lookup has never heard of this prefab, and a Ready that answered from a field
        /// would say yes while every mill in the world was being discarded.
        /// </summary>
        public static bool Ready
        {
            get
            {
                return ZNetScene.instance != null
                       && ZNetScene.instance.GetPrefab(Name) != null;
            }
        }

        public static bool Register()
        {
            if (ZNetScene.instance == null || ObjectDB.instance == null) return false;
            if (Ready && InHammer()) return true;

            if (_prefab == null)
            {
                _prefab = Build();
                if (_prefab == null) return false;
            }

            AddToScene();
            AddToHammer();
            return Ready;
        }

        // ------------------------------------------------------------------ building

        private static GameObject Build()
        {
            var source = Donor();
            if (source == null) return null;

            if (_holder == null)
            {
                _holder = new GameObject("GroveMillHolder");
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

            Strip(clone);

            // The mill's shape is its own. Nothing vanilla is grafted on: only the materials
            // are borrowed, group by group, so the mesh is ours and the surfaces are the
            // game's. No heartwood in this one, so the anchor pass is skipped.
            if (!Stow.PostModel.Apply(clone, Mesh, "mill_visual", false))
                GrovePlugin.Log.LogWarning(
                    "The bone mill is wearing its donor's body - " + Mesh
                    + " was not found beside the dll.");

            var piece = clone.GetComponent<Piece>();
            if (piece != null)
            {
                piece.m_name = GroveConfig.MillName.Value;
                piece.m_description = "Grinds bone into meal. Press use and it takes what "
                                      + "it needs out of your pack.";
                piece.m_resources = Requirements(GroveConfig.MillBuildCost.Value);
                piece.m_category = Piece.PieceCategory.Crafting;
                piece.m_craftingStation = null;

                var icon = Icons.Load("grove_mill_icon.png", Name);
                if (icon != null) piece.m_icon = icon;
            }

            if (clone.GetComponent<Mill>() == null) clone.AddComponent<Mill>();

            GrovePlugin.Log.LogInfo("Built " + Name + " from " + source.name + ".");
            return clone;
        }

        /// <summary>
        /// Everything the donor does that the mill does not.
        ///
        /// DestroyImmediate rather than Destroy: ordinary Destroy is deferred to the end of
        /// the frame, and this prefab is registered and can be placed within that frame,
        /// which would hand out a mill that is still half a kiln.
        /// </summary>
        private static void Strip(GameObject clone)
        {
            // The kiln's own conversion, its switches and its smoke. Left on, the piece would
            // still accept wood through a switch and quietly make coal beside the bonemeal.
            foreach (var smelter in clone.GetComponentsInChildren<Smelter>(true))
                Object.DestroyImmediate(smelter);

            foreach (var sw in clone.GetComponentsInChildren<Switch>(true))
                Object.DestroyImmediate(sw);

            foreach (var smoke in clone.GetComponentsInChildren<SmokeSpawner>(true))
                Object.DestroyImmediate(smoke);

            // Particles survive a component strip and keep emitting on their own, so a
            // decorative kiln would go on smoking with nothing burning in it.
            foreach (var particles in clone.GetComponentsInChildren<ParticleSystem>(true))
                Object.DestroyImmediate(particles);
        }

        private static GameObject Donor()
        {
            var scene = ZNetScene.instance;

            foreach (var name in new[] { GroveConfig.MillDonor.Value, "charcoal_kiln",
                                         "piece_stonecutter", "piece_workbench" })
            {
                if (string.IsNullOrEmpty(name)) continue;

                var found = scene.GetPrefab(name);
                if (found != null) return found;

                GrovePlugin.LogOnce("Mill donor '" + name + "' does not exist.");
            }

            return null;
        }

        /// <summary>
        /// What building one costs. An ingredient that does not resolve abandons the whole
        /// list rather than quietly cheapening it.
        /// </summary>
        private static Piece.Requirement[] Requirements(string spec)
        {
            var db = ObjectDB.instance;
            if (db == null) return null;

            var list = new List<Piece.Requirement>();

            foreach (var part in spec.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0) continue;

                var split = trimmed.Split(':');
                int amount;
                if (split.Length != 2 || !int.TryParse(split[1].Trim(), out amount))
                {
                    GrovePlugin.LogOnce("Cannot read mill build cost '" + trimmed
                                        + "' - expected Item:Amount.");
                    return null;
                }

                var item = db.GetItemPrefab(split[0].Trim());
                var drop = item != null ? item.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    GrovePlugin.LogOnce("Mill build ingredient '" + split[0].Trim()
                                        + "' does not exist - cost not applied.");
                    return null;
                }

                list.Add(new Piece.Requirement { m_resItem = drop, m_amount = amount });
            }

            return list.Count > 0 ? list.ToArray() : null;
        }

        // ------------------------------------------------------------------ registration

        private static void AddToScene()
        {
            var scene = ZNetScene.instance;
            if (scene == null || _prefab == null) return;
            if (scene.GetPrefab(Name) != null) return;

            scene.m_prefabs.Add(_prefab);

            // The list alone is not enough. m_namedPrefabs is built in Awake and never
            // rebuilt, and a prefab missing from it is one ZNetScene cannot resolve - which
            // means every mill already placed has its ZDO discarded rather than erroring.
            var named = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs")
                                   .GetValue(scene) as Dictionary<int, GameObject>;
            if (named != null) named[Name.GetStableHashCode()] = _prefab;
        }

        /// <summary>
        /// Asked of the live table rather than a flag, because ObjectDB is rebuilt per world
        /// and a Hammer from the new one has never been told about this piece. Getting this
        /// wrong is milder than the scene equivalent: the mill survives in the world and
        /// merely goes missing from the build menu.
        /// </summary>
        private static bool InHammer()
        {
            var table = HammerPieces();
            return table != null && _prefab != null && table.m_pieces.Contains(_prefab);
        }

        private static void AddToHammer()
        {
            var table = HammerPieces();
            if (table == null || _prefab == null) return;
            if (table.m_pieces.Contains(_prefab)) return;

            table.m_pieces.Add(_prefab);
        }

        private static PieceTable HammerPieces()
        {
            var db = ObjectDB.instance;
            if (db == null) return null;

            var hammer = db.GetItemPrefab("Hammer");
            if (hammer == null) return null;

            var drop = hammer.GetComponent<ItemDrop>();
            return drop != null && drop.m_itemData != null
                ? drop.m_itemData.m_shared.m_buildPieces : null;
        }
    }
}
