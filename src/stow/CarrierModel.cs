using System.IO;
using UnityEngine;
// ObjMesh and ModelData come from the Grove namespace. There used to be a byte-identical
// copy of both in this folder, from when Stow was its own repository and could not
// reach across to a sibling checkout. It ships in the same assembly now, so the copy
// was two files drifting apart for no reason. Types in this namespace still win over
// this import, so Stow's own Icons and PropIndex are unaffected.
using Grove;

namespace Stow
{
    /// <summary>
    /// Assembles a carrier out of the three meshes beside the dll.
    ///
    /// Built from nothing rather than cloned from a vanilla prefab, which is the opposite
    /// of the choice the post makes. The post clones a chest because it wants a chest's
    /// machinery - Container, Piece, WearNTear, placement rules - and would have to
    /// rebuild all of it by hand otherwise. The carrier wants none of that: no health, no
    /// AI, no death, no interaction, not even a collider. Cloning a creature here would
    /// inherit a whole creature just to spend the next hour tearing it back out.
    ///
    /// It is also not registered with ZNetScene and carries no ZNetView. See Carrier for
    /// what that costs on a server and why it is the right first pass.
    /// </summary>
    internal static class CarrierModel
    {
        // The carrier is the Vaettir spirit. Not a spirit like it - the same one, raised
        // at the sapling and folded into the heartwood this post is built around, coming
        // back out to do a job. So it wears those meshes byte for byte rather than a copy
        // of its own.
        //
        // Stow used to ship stow_carrier_*.obj from a second Blender script, and the
        // comment there priced the copy at "two places to change if the spirit is ever
        // restyled". That undersold it badly. The two were never identical to begin with:
        // one ring of seven beads at a 0.21 orbit here against two crossed rings of six at
        // 0.34 there, each separately jittered, so the same character was visibly two
        // different creatures depending on which mod happened to draw it. Nobody restyled
        // anything - it shipped wrong.
        //
        // One source now: vaettir/tools/spirit_core.py. These files are copies of its
        // output. Copy them across rather than regenerating them here, or the seeded
        // jitter alone will pull them apart again.
        private const string HeartMesh = "grove_spirit_heart.obj";
        private const string HoopMesh = "grove_spirit_hoop.obj";
        private const string MoteMesh = "grove_spirit_mote.obj";

        /// <summary>
        /// The arrangement, and it must match Vaettir's spirit.
        ///
        /// Two crossed rings of six. Six rather than seven because an even count divides
        /// more cleanly, and because the reason seven was picked in the first place - so no
        /// two beads sit opposite each other on a single circle - stops applying the moment
        /// there is more than one circle.
        ///
        /// Consts rather than config: two mods each exposing their own ring count is how
        /// the same being ends up looking like two again, which is the bug this replaced.
        /// </summary>
        private const int Rings = 2;
        private const int Motes = 6;

        /// <summary>
        /// Whether to draw the torus the beads ride on. Off, matching Vaettir: the circle
        /// is implied by where the beads are and by their moving together, and with two
        /// rings crossing, the actual hoop meshes turn the whole shape into a ball of wire.
        /// The mesh is still loaded, because turning this on is the fastest way to see
        /// what the beads are supposed to be riding when the arrangement looks wrong.
        /// </summary>
        /// Static rather than const so the branch below stays compiled: a const false folds
        /// it away and the compiler reports the diagnostic path as unreachable code.
        private static readonly bool ShowHoop = false;

        /// <summary>How far below the hoop the load hangs, and where the rope beads sit.</summary>
        private const float SlingDrop = 0.155f;
        private const float TetherHigh = 0.035f;
        private const float TetherLow = 0.090f;

        private static ModelData _heart;
        private static ModelData _hoop;
        private static ModelData _mote;
        private static bool _missing;

        /// <summary>
        /// Forgets the meshes so they are read from disk again.
        ///
        /// This is not about the files changing - it is about the UVs. Remapping a mesh
        /// into a borrowed material's atlas slice rewrites the mesh in place, and the
        /// original coordinates are gone once it has happened. So when the borrowed
        /// material may change - a new world, a different set of loaded prefabs - the
        /// only way back to a clean mapping is a clean mesh.
        /// </summary>
        public static void Invalidate()
        {
            _heart = null;
            _hoop = null;
            _mote = null;
            _missing = false;
        }

        /// <summary>
        /// A carrier, hovering at the given point, or null if the meshes are not there.
        ///
        /// Returns null rather than throwing, and says so once rather than every frame: a
        /// post whose spirit cannot be built has to fall back to moving things instantly,
        /// not stop working.
        /// </summary>
        public static Carrier Build(Vector3 at)
        {
            if (!LoadMeshes()) return null;

            var scale = Mathf.Max(0.05f, StowConfig.CarrierScale.Value);

            var root = new GameObject("stow_carrier");
            root.transform.position = at;
            root.transform.localScale = new Vector3(scale, scale, scale);

            var body = new GameObject("body");
            body.transform.SetParent(root.transform, false);

            Part(body.transform, "heart", _heart);

            // An empty holding the rings, so the tumble is one Rotate on this and each
            // ring's own turn is a second one on itself. The old single-hoop layout made
            // the drawn torus double as the pivot, which stops working the moment there
            // is more than one of them.
            var hoop = new GameObject("rings");
            hoop.transform.SetParent(body.transform, false);
            BuildRings(hoop.transform);

            // The pivot sits at the bottom of the hoop rather than at the load, so the
            // sway swings the rope and the load together from the point they hang from.
            // Pivoting at the load instead spins the crate on the spot, which reads as a
            // thing being fiddled with rather than a thing swinging.
            var sling = new GameObject("sling");
            sling.transform.SetParent(body.transform, false);
            sling.transform.localPosition = new Vector3(0f, -Carrier.Orbit, 0f);

            Bead(sling.transform, "tether_high", TetherHigh, 1.2f);
            Bead(sling.transform, "tether_low", TetherLow, 0.92f);

            var hook = new GameObject("hook");
            hook.transform.SetParent(sling.transform, false);
            hook.transform.localPosition = new Vector3(0f, -SlingDrop, 0f);

            // The halo, on the body so it bobs with the heart rather than sitting
            // still in the middle of a moving light. Scaled to the hoop rather than to
            // the donor, whose lantern is a metre and a bit across.
            Flare.Attach(body.transform, Carrier.Orbit * 2.6f);

            var carrier = root.AddComponent<Carrier>();
            carrier.Body = body.transform;
            carrier.Hoop = hoop.transform;
            carrier.Sling = sling.transform;
            carrier.Hook = hook.transform;

            return carrier;
        }

        /// <summary>
        /// The rings and their beads, built exactly as Vaettir builds them.
        ///
        /// Kept line for line in step with SpiritPrefab.Rings on the other side. Rings share
        /// 180 degrees between them rather than 360, because a circle rotated half a turn is
        /// the same circle - four rings at 0/90/180/270 would draw two of them twice and
        /// read as two. The first starts at 90 rather than 0 because a ring at zero tilt
        /// lies flat, and a flat circle seen from eye height is a line.
        /// </summary>
        private static void BuildRings(Transform parent)
        {
            for (var r = 0; r < Rings; r++)
            {
                var ring = new GameObject("ring" + r);
                ring.transform.SetParent(parent, false);
                ring.transform.localRotation =
                    Quaternion.Euler(90f + 180f / Rings * r, 0f, 0f);

                if (ShowHoop) Part(ring.transform, "hoop", _hoop);

                for (var i = 0; i < Motes; i++)
                {
                    // Offset per ring so beads on crossing circles do not all arrive at the
                    // same point at the same moment, which reads as a seam.
                    var radians = (360f / Motes * i + 12f * r) * Mathf.Deg2Rad;

                    // A ring is built in its own local XY plane and then tilted, so a bead
                    // rides (cos, sin, 0) within it. If that is ever wrong the symptom is
                    // unmistakable: the beads orbit through the hoop rather than along it.
                    var mote = Part(ring.transform, "mote" + r + "_" + i, _mote);
                    mote.transform.localPosition = new Vector3(
                        Mathf.Cos(radians) * Carrier.Orbit,
                        Mathf.Sin(radians) * Carrier.Orbit, 0f);
                }
            }
        }

        private static GameObject Bead(Transform parent, string name, float drop, float size)
        {
            var bead = Part(parent, name, _mote);
            bead.transform.localPosition = new Vector3(0f, -drop, 0f);
            bead.transform.localScale = new Vector3(size, size, size);
            return bead;
        }

        private static GameObject Part(Transform parent, string name, ModelData model)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            go.AddComponent<MeshFilter>().sharedMesh = model.Mesh;

            var renderer = go.AddComponent<MeshRenderer>();

            // One material per submesh. The carrier is a single group, but the OBJ loader
            // emits one submesh per usemtl either way and a short array leaves the extras
            // rendering with whatever was last bound.
            //
            // Skinned here but never remapped here - see Dress(). The UV remap happens
            // once per mesh, at load.
            // Grove's Skins, not PostModel's. Both meshes here are usemtl core and both
            // asked for a material for that group, but through two different lookups with
            // two different donor lists - so the spirit you commune with wore the fire
            // pit's material and the one carrying your ore wore the dvergr lantern's. The
            // merge made them one creature and left them wearing two skins.
            //
            // PostModel's borrow is right for the post, which is timber and iron and
            // genuinely wants a material per group. A spirit is one surface.
            renderer.sharedMaterials = Skins.Skin(model.Groups);

            // Nothing here should darken anything. The carrier is a light source, and one
            // that casts its own hoop across the floor looks like a bug.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go;
        }

        /// <summary>
        /// Loaded once and kept. Three meshes read off disk per trip would be a file read
        /// every few seconds for the life of the session.
        ///
        /// Note that this is a static cache and so lives as long as the process: editing
        /// one of these .obj files needs a full game restart, not a world reload, or you
        /// iterate on the old one.
        /// </summary>
        private static bool LoadMeshes()
        {
            if (_heart != null && _hoop != null && _mote != null) return true;
            if (_missing) return false;

            var directory = Path.GetDirectoryName(typeof(CarrierModel).Assembly.Location);

            _heart = ObjMesh.Load(Path.Combine(directory, HeartMesh));
            _hoop = ObjMesh.Load(Path.Combine(directory, HoopMesh));
            _mote = ObjMesh.Load(Path.Combine(directory, MoteMesh));

            if (_heart != null && _hoop != null && _mote != null)
            {
                Dress(_heart);
                Dress(_hoop);
                Dress(_mote);
                return true;
            }

            // Once, not every frame. A post retries this on every trip it wants to make.
            _missing = true;

            StowRuntime.Log.LogError(
                "Carrier meshes are missing from beside the dll - the post will move "
                + "things instantly instead. Expected " + HeartMesh + ", " + HoopMesh
                + " and " + MoteMesh + ".");

            return false;
        }

        /// <summary>
        /// Squeezes one mesh's UVs into the borrowed material's slice of its atlas, once.
        ///
        /// Once is the whole point. Remap rewrites the mesh's UVs in place, and these
        /// three meshes are shared by every carrier ever built - so doing it per part, as
        /// the post does, would map an already-mapped mesh into a rectangle inside a
        /// rectangle on the second trip and into a sliver of a texel by the tenth. The
        /// symptom would have been a spirit that dimmed a little every time it flew.
        ///
        /// SkinsFor first, because that is what learns the atlas rectangle; asking for
        /// the remap before anything has borrowed a material remaps into nothing.
        /// </summary>
        private static void Dress(ModelData model)
        {
            Skins.Skin(model.Groups);
            Skins.Remap(model.Mesh, model.Groups);
        }
    }
}
