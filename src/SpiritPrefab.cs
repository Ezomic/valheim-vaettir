using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// Builds the forest spirit at runtime, from nothing.
    ///
    /// Every other creature in these mods clones a vanilla one, because cloning is what
    /// gets you a working Character, MonsterAI, ragdoll and network setup for free. This
    /// one is assembled by hand instead, and that is a choice rather than an oversight:
    /// the spirit has no AI, no health, no attacks and no death - it hangs in the air
    /// and it fades when you speak to it. Cloning a Greyling would inherit a whole
    /// creature's machinery to then spend the milestone tearing back out.
    ///
    /// What it does need is a ZNetView, so it survives a world reload and so `spawn`
    /// can find it. That is the entire plumbing risk in this milestone, which is why
    /// this milestone contains nothing else.
    /// </summary>
    internal static class SpiritPrefab
    {
        /// <summary>
        /// The network identity. ZNetScene keys every prefab by name.GetStableHashCode()
        /// and a saved ZDO stores that hash, so renaming this later orphans every spirit
        /// already in a save. Treat it as permanent.
        /// </summary>
        public const string Name = "ForestSpirit";

        private const string HeartMesh = "grove_spirit_heart.obj";
        private const string HoopMesh = "grove_spirit_hoop.obj";
        private const string MoteMesh = "grove_spirit_mote.obj";

        private static GameObject _prefab;
        private static GameObject _holder;
        private static Material _glow;

        public static bool Ready
        {
            get { return ZNetScene.instance != null && ZNetScene.instance.GetPrefab(Name) != null; }
        }

        /// <summary>Idempotent, and safe to call every frame until it takes.</summary>
        public static bool Register()
        {
            if (Ready) return true;

            var scene = ZNetScene.instance;
            if (scene == null) return false;

            if (_prefab == null)
            {
                _prefab = Build();
                if (_prefab == null) return false;
            }

            AddToScene(scene);
            return Ready;
        }

        // ------------------------------------------------------------------ building

        private static GameObject Build()
        {
            var directory = Path.GetDirectoryName(typeof(SpiritPrefab).Assembly.Location);

            var heart = ObjMesh.Load(Path.Combine(directory, HeartMesh));
            var hoop = ObjMesh.Load(Path.Combine(directory, HoopMesh));
            var mote = ObjMesh.Load(Path.Combine(directory, MoteMesh));

            if (heart == null || hoop == null || mote == null)
            {
                GrovePlugin.Log.LogError(
                    "Spirit meshes are missing from beside the dll - cannot build "
                    + Name + ". Expected " + HeartMesh + ", " + HoopMesh + " and "
                    + MoteMesh + ".");
                return null;
            }

            if (_holder == null)
            {
                _holder = new GameObject("GroveSpiritHolder");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }

            // Built inside a disabled holder with init suppressed, so the ZNetView
            // cannot try to register itself on the network while it is half-assembled.
            var previous = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;

            GameObject root;
            try
            {
                root = new GameObject(Name);
                root.transform.SetParent(_holder.transform, false);

                var scale = GroveConfig.SpiritScale.Value;
                root.transform.localScale = new Vector3(scale, scale, scale);

                var nview = root.AddComponent<ZNetView>();

                // Persistent, or it evaporates the moment its zone unloads - and a
                // spirit that vanishes when you walk away is indistinguishable from
                // one that was never saved, which is exactly the failure this
                // milestone exists to rule out.
                nview.m_persistent = true;
                nview.m_type = ZDO.ObjectType.Default;
                nview.m_distant = false;

                var glow = Glow();

                var heartGo = Part(root.transform, "heart", heart, glow);
                var hoopGo = Part(root.transform, "hoop", hoop, glow);

                var motes = Mathf.Max(0, GroveConfig.MoteCount.Value);
                for (var i = 0; i < motes; i++)
                {
                    var angle = 360f / motes * i + 12f;
                    var radians = angle * Mathf.Deg2Rad;

                    var moteGo = Part(hoopGo.transform, "mote" + i, mote, glow);

                    // The hoop mesh lies in its own local XY plane: the torus is built
                    // in Blender's XZ with its axis along +Y, and the exporter maps
                    // Blender Y to Unity Z. If that is wrong the symptom is
                    // unmistakable - the beads will orbit through the hoop, not along it.
                    moteGo.transform.localPosition = new Vector3(
                        Mathf.Cos(radians) * ForestSpirit.Orbit,
                        Mathf.Sin(radians) * ForestSpirit.Orbit, 0f);
                }

                var spirit = root.AddComponent<ForestSpirit>();
                spirit.Heart = heartGo.transform;
                spirit.Hoop = hoopGo.transform;
            }
            finally
            {
                ZNetView.m_forceDisableInit = previous;
            }

            GrovePlugin.Log.LogInfo(
                "Built " + Name + ": heart " + heart.Mesh.vertexCount + " verts, hoop "
                + hoop.Mesh.vertexCount + ", " + GroveConfig.MoteCount.Value + " motes.");

            return root;
        }

        private static GameObject Part(Transform parent, string name, ModelData model,
                                       Material skin)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            go.AddComponent<MeshFilter>().sharedMesh = model.Mesh;

            var renderer = go.AddComponent<MeshRenderer>();

            // One material per submesh. The spirit is a single group, but the OBJ
            // loader emits one submesh per usemtl either way and a short array leaves
            // the extras rendering with whatever was last bound.
            var skins = new Material[model.Groups.Length];
            for (var i = 0; i < skins.Length; i++) skins[i] = skin;
            renderer.sharedMaterials = skins;

            // Nothing here should darken anything. The creature is a light source, and
            // a light that casts its own geometry across the ground in seven directions
            // looks like a bug.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go;
        }

        /// <summary>
        /// A material that already glows, borrowed rather than built.
        ///
        /// Building one needs a shader, and Shader.Find on a stripped game build is a
        /// coin toss - so this takes the same route the rest of these mods take for
        /// surfaces and lifts a real one off a vanilla prefab. Which prefab is an open
        /// question that the game has to answer, hence the configured list and the
        /// DumpMaterials diagnostic rather than a confident constant.
        ///
        /// Falls back to the first material it can find anywhere rather than returning
        /// null: a wrong-looking spirit can be fixed by editing one config line, and an
        /// invisible one looks identical to a prefab that failed to register.
        /// </summary>
        private static Material Glow()
        {
            if (_glow != null) return _glow;

            foreach (var raw in (GroveConfig.GlowDonors.Value ?? "").Split(','))
            {
                var name = raw.Trim();
                if (name.Length == 0) continue;

                var donor = PropIndex.Find(name);
                if (donor == null) continue;

                foreach (var renderer in donor.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var material = renderer.sharedMaterial;
                    if (material == null || material.shader == null) continue;
                    if (!material.HasProperty("_MainTex")
                        || material.GetTexture("_MainTex") == null) continue;

                    _glow = material;
                    GrovePlugin.Log.LogInfo(
                        "Spirit glow borrowed from " + name + ": " + material.name
                        + " (shader " + material.shader.name + ").");
                    return _glow;
                }
            }

            GrovePlugin.Log.LogWarning(
                "None of the GlowDonors resolved. Falling back to any material at all - "
                + "the spirit will be visible but will not glow. Turn on DumpMaterials "
                + "to find a donor worth naming.");

            foreach (var renderer in Resources.FindObjectsOfTypeAll<MeshRenderer>())
            {
                if (renderer == null || renderer.sharedMaterial == null) continue;
                _glow = renderer.sharedMaterial;
                break;
            }

            return _glow;
        }

        /// <summary>Every material on each donor, with the questions worth asking of it.</summary>
        public static void DumpMaterials()
        {
            foreach (var raw in (GroveConfig.GlowDonors.Value ?? "").Split(','))
            {
                var name = raw.Trim();
                if (name.Length == 0) continue;

                var donor = PropIndex.Find(name);
                if (donor == null)
                {
                    GrovePlugin.Log.LogInfo("donor " + name + ": not loaded");
                    continue;
                }

                foreach (var renderer in donor.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null) continue;

                        GrovePlugin.Log.LogInfo(string.Format(
                            "donor {0}: {1} shader={2} emission={3} albedo={4}",
                            name, material.name,
                            material.shader != null ? material.shader.name : "none",
                            material.HasProperty("_EmissionColor"),
                            material.HasProperty("_MainTex")
                                && material.GetTexture("_MainTex") != null));
                    }
                }
            }
        }

        // ------------------------------------------------------------------ registering

        /// <summary>
        /// Adds the prefab to both places ZNetScene looks. m_prefabs alone is not enough
        /// once Awake has already run, because the lookup dictionary it feeds is built
        /// there and never rebuilt.
        /// </summary>
        private static void AddToScene(ZNetScene scene)
        {
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
                return;
            }

            GrovePlugin.Log.LogInfo("Registered " + Name + " with ZNetScene. Try: spawn "
                                    + Name);
        }
    }
}
