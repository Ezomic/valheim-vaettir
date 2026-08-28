using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using Ezomic.Shared;

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

        private static Material _glow;

        /// <summary>
        /// Builds the spirit once. Getting it into the scene, and back into every scene
        /// after that, belongs to Ezomic.Shared.Prefabs - see the header of that file for
        /// why "have I registered yet" must never be answered from a field of ours.
        /// </summary>
        // ------------------------------------------------------------------ building

        internal static GameObject Build()
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

            // Built inside a disabled holder with init suppressed, so the ZNetView
            // cannot try to register itself on the network while it is half-assembled.
            //
            // Reach() is what makes it touchable at all - see below. Building the
            // creature from scratch rather than cloning one is right, and this is the
            // bill for it: a clone would have arrived with a collider.
            var previous = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;

            GameObject root;
            try
            {
                root = new GameObject(Name);
                root.transform.SetParent(Prefabs.Holder, false);

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

                // Once per mesh, here, not in Part. Rings hands the same mote
                // ModelData to Part twelve times, and the fit is not idempotent:
                // every pass after the first shrinks the already-placed UVs by
                // texels-per-metre over sheet width again, so by the twelfth the
                // beads sample a single texel while the heart, fitted once, does
                // not - one material reading as two different surfaces. Glow() has
                // to run first, because it is what fills the atlas caches.
                Skins.Remap(heart.Mesh, heart.Groups);
                Skins.Remap(hoop.Mesh, hoop.Groups);
                Skins.Remap(mote.Mesh, mote.Groups);

                var heartGo = Part(root.transform, "heart", heart, glow);

                // An empty carrier rather than the hoop mesh. It is what tumbles, and
                // the rings hang under it - so the hoop being drawn or not is now just
                // one more part rather than the thing the structure is built on.
                var hoopGo = new GameObject("rings");
                hoopGo.transform.SetParent(root.transform, false);

                Rings(hoopGo.transform, hoop, mote, glow);

                Reach(root);

                var spirit = root.AddComponent<ForestSpirit>();
                spirit.Heart = heartGo.transform;
                spirit.Hoop = hoopGo.transform;
            }
            finally
            {
                ZNetView.m_forceDisableInit = previous;
            }

            var rings = Mathf.Max(1, GroveConfig.RingCount.Value);
            var beads = Mathf.Max(0, GroveConfig.MoteCount.Value);

            GrovePlugin.Log.LogInfo(
                "Built " + Name + ": heart " + heart.Mesh.vertexCount + " verts, "
                + rings + " ring(s) of " + beads + " = " + (rings * beads) + " beads, "
                + (GroveConfig.ShowHoop.Value ? "hoop drawn" : "hoop hidden") + ".");

            return root;
        }

        /// <summary>
        /// The circles of beads, each in its own plane.
        ///
        /// Each ring is an empty transform holding its beads at fixed local positions
        /// on a circle in its local XY plane, then tilted. That split is what lets the
        /// drift be one Rotate per ring instead of a repositioning per bead, and it is
        /// what keeps every bead in step - there is no per-bead state left to drift out
        /// of sync.
        ///
        /// Rings share 180 degrees between them rather than 360, because a circle
        /// rotated half a turn is the same circle: four rings at 0/90/180/270 would
        /// draw two of them twice and look like two. And the first ring starts at 90
        /// rather than 0, because a ring at zero tilt lies flat and a flat circle seen
        /// from eye height is a row of beads with no circle in it at all.
        /// </summary>
        private static void Rings(Transform parent, ModelData hoop, ModelData mote,
                                  Material glow)
        {
            var rings = Mathf.Max(1, GroveConfig.RingCount.Value);
            var beads = Mathf.Max(0, GroveConfig.MoteCount.Value);

            for (var r = 0; r < rings; r++)
            {
                var ring = new GameObject("ring" + r);
                ring.transform.SetParent(parent, false);
                ring.transform.localRotation =
                    Quaternion.Euler(90f + 180f / rings * r, 0f, 0f);

                if (GroveConfig.ShowHoop.Value)
                    Part(ring.transform, "hoop", hoop, glow);

                for (var i = 0; i < beads; i++)
                {
                    // Offset per ring so beads on crossing circles do not all arrive at
                    // the same point at the same moment, which reads as a seam.
                    var angle = (360f / beads * i + 12f * r) * Mathf.Deg2Rad;

                    var beadGo = Part(ring.transform, "mote" + r + "_" + i, mote, glow);
                    beadGo.transform.localPosition = new Vector3(
                        Mathf.Cos(angle) * ForestSpirit.Orbit,
                        Mathf.Sin(angle) * ForestSpirit.Orbit, 0f);
                }
            }
        }

        /// <summary>
        /// Gives it something a raycast can hit, which is the whole of being touchable.
        ///
        /// Found by playing: the spirit grew, stood there, glowed, and could not be
        /// spoken to at all - no hover text, no prompt, nothing. Player.FindHoverObject
        /// raycasts against m_interactMask and asks whatever collider it hit for a
        /// Hoverable. With no collider there is no hit, so a component implementing
        /// Hoverable and Interactable is never consulted and nothing is logged, because
        /// from the game's side nothing happened. Building the creature from scratch
        /// rather than cloning one is still right; this is the bill for it, since a
        /// clone would have arrived carrying a collider.
        ///
        /// On the root, not on a child, and that is load-bearing: FindHoverObject calls
        /// GetComponent&lt;Hoverable&gt; on the collider's own GameObject, while Interact
        /// uses GetComponentInParent. A collider one level down would therefore have
        /// produced the worst symptom available - a thing you can use but that never
        /// tells you it is there.
        ///
        /// The kinematic Rigidbody is not decoration. The spirit bobs, so the root moves
        /// every frame, and moving a collider with no Rigidbody attached moves a *static*
        /// collider - which makes Unity rebuild the static physics tree each time.
        /// </summary>
        private static void Reach(GameObject root)
        {
            // Sized to the hoop rather than to the whole silhouette. A sphere that
            // swallowed the outermost bead would be a metre of invisible wall around a
            // thing the size of a lantern.
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = ForestSpirit.Orbit + 0.16f;
            sphere.isTrigger = false;

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            // Default is one of the layers in Player.m_interactMask. Set rather than
            // left to chance: a new GameObject happening to default to a layer that
            // works is luck, and the failure it produces is this same silent one.
            root.layer = LayerMask.NameToLayer("Default");
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

            _glow = Skins.For("core");
            if (_glow != null) return _glow;

            // Any material at all, rather than none. A wrong-looking spirit is one
            // config line away from fixed; an invisible one looks exactly like a
            // prefab that failed to register, and that is an hour of looking in the
            // wrong place.
            GrovePlugin.Log.LogWarning(
                "No GlowDonor resolved - falling back to any material. The spirit will "
                + "be visible but will not glow. Turn on DumpMaterials to find a donor "
                + "worth naming.");

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

    }
}
