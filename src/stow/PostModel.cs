using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Puts the hand-modelled post onto the cloned chest, in place of the donor's own
    /// look.
    ///
    /// Nothing here paints anything. Each material group in the OBJ - wood, iron, stone -
    /// is skinned with a real material lifted off a vanilla prefab, so the piece is made of
    /// the game's own wood and the game's own iron rather than an approximation of them.
    /// That also sidesteps the trap of swapping _MainTex on a borrowed material, which
    /// keeps the donor's normal map and leaves the surface lit for a shape it no longer has.
    /// </summary>
    internal static class PostModel
    {
        /// <summary>
        /// Which mesh to wear. Config rather than a constant so a rejected shape can be
        /// swapped for an approved one by editing a line, not by rebuilding - the piece
        /// spent a while wearing a tapered bin that had already been turned down simply
        /// because the filename was baked in here.
        /// </summary>
        private static string ModelFile
        {
            get { return StowConfig.PostModelFile.Value; }
        }

        private static string ColliderFile
        {
            get { return System.IO.Path.ChangeExtension(ModelFile, ".col"); }
        }

        /// <summary>
        /// The group the carrier's meshes are built in. Not in the table below: its
        /// donors come from config, because "which vanilla prefab has a material that
        /// glows" is a question the game has to answer and not one to be confident about
        /// in a constant.
        /// </summary>
        public const string GlowGroup = "core";

        /// <summary>Prefabs to lift each group's material from, best first.</summary>
        private static readonly Dictionary<string, string[]> Donors =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "wood",  new[] { "wood_wall", "wood_beam", "piece_chest_wood" } },
                // Metal, not "a station that happens to have metal on it somewhere".
                // piece_artisanstation was first and its first textured renderer is
                // ArtisanTable_Mat - a wooden bench top - so every iron band on the trough
                // came out looking like planks.
                { "iron",  new[] { "piece_cauldron", "piece_chest_blackmetal", "forge",
                                   "blackforge", "piece_stonecutter" } },
                { "stone", new[] { "stone_wall_2x1", "piece_stonecutter", "smelter" } },
            };

        private static readonly Dictionary<string, Material> Cache =
            new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Where in its texture each borrowed material actually lives.
        ///
        /// Valheim's piece textures are atlases: stone_mat does not use the whole image,
        /// it uses a strip of one. UVs running 0..1 therefore sample the entire sheet and
        /// pick up whatever the neighbouring tiles are - which is why the trough came out
        /// striped, and why its charcoal came out bright green. Measuring the donor's own
        /// UV bounds gives the rectangle to squeeze our coordinates into.
        /// </summary>
        private static readonly Dictionary<string, Rect> Atlas =
            new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Swaps the donor's visuals for ours. Returns false if the model is missing, in
        /// which case the caller keeps the donor's look rather than shipping an invisible
        /// piece.
        /// </summary>
        public static bool Apply(GameObject prefab)
        {
            return Apply(prefab, ModelFile, "post_visual", true);
        }

        /// <summary>
        /// The same work for any piece, not just the post.
        ///
        /// Everything below is general: strip the donor's renderers, hang our mesh on a
        /// square child, skin each material group off a vanilla prefab, remap the UVs into
        /// each group's atlas rect, and swap the colliders for the ones in the sidecar. Only
        /// two things were ever Stow's, and they are now arguments: which file to load, and
        /// whether to look for a heartwood in it.
        ///
        /// Generalised for the bone mill, which needs all of the above and has no heartwood.
        /// A model loader keyed to one config entry is one piece's loader; this one is the
        /// repo's.
        /// </summary>
        public static bool Apply(GameObject prefab, string modelFile, string visualName,
                                 bool heartwood)
        {
            var dir = Path.GetDirectoryName(typeof(PostModel).Assembly.Location);
            var model = ObjMesh.Load(Path.Combine(dir, modelFile));

            if (model == null || model.Mesh == null)
            {
                StowPlugin.Log.LogWarning(
                    "No " + modelFile + " beside the dll - falling back to the donor's own "
                    + "look.");
                return false;
            }

            // The donor's renderers go, but its ZNetView, Piece and WearNTear stay: those
            // are the machinery that makes it a buildable, damageable, networked object,
            // and rebuilding them by hand is exactly the work cloning avoids.
            // Null-checked: destroying a renderer's GameObject takes its children with it, and
            // GetComponentsInChildren lists parents first, so a nested renderer is already
            // destroyed when the loop reaches it and asking it for its gameObject throws.
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null) continue;
                UnityEngine.Object.DestroyImmediate(renderer.gameObject);
            }

            var visual = new GameObject(visualName);
            visual.transform.SetParent(prefab.transform, false);

            // Explicitly square, not merely assumed square. The donor is a barrel, and
            // dressing props are often modelled with a lean baked into the transform so
            // they look casually dropped - inheriting that tips the whole trough over.
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            var filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = model.Mesh;

            var meshRenderer = visual.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = SkinsFor(model.Groups);

            // After SkinsFor, because that is what learns each group's atlas rectangle.
            Remap(model.Mesh, model.Groups);

            if (heartwood) Heartwood(prefab, visual.transform, model);

            ReplaceColliders(prefab, Path.Combine(dir, Path.ChangeExtension(modelFile, ".col")));

            StowPlugin.Log.LogInfo(string.Format(
                "{0} loaded: {1} verts, {2} tris, groups [{3}].",
                modelFile, model.Mesh.vertexCount, model.Mesh.triangles.Length / 3,
                string.Join(", ", model.Groups)));

            return true;
        }

        /// <summary>The child a post hangs its light, its flare and its spirit off.</summary>
        public const string HeartwoodAnchor = "heartwood";

        /// <summary>
        /// Finds the heartwood in the model and puts a marker on it.
        ///
        /// Derived from the mesh rather than configured as an offset, because it has to
        /// stay right when the model changes. The heartwood is wherever the `core` group's
        /// triangles are - up in the rail on the canopy, down in the plinth on the hearth,
        /// up the middle on the spine - and a hand-typed offset would be correct for
        /// exactly one of those and silently wrong for the rest.
        ///
        /// The marker is what the light and the flare hang off, and it is where the spirit
        /// is born. Before this, CarryRun spawned the spirit at the collider's top plus
        /// 45cm, which on the canopy is above its roof - so the spirit would have appeared
        /// three quarters of a metre above its own source with a roof in between, undoing
        /// the entire reason for putting the heartwood on the post.
        /// </summary>
        private static void Heartwood(GameObject prefab, Transform visual, ModelData model)
        {
            var index = System.Array.FindIndex(
                model.Groups,
                group => string.Equals(group, GlowGroup, StringComparison.OrdinalIgnoreCase));

            if (index < 0) return;

            var triangles = model.Mesh.GetTriangles(index);
            if (triangles == null || triangles.Length == 0) return;

            var vertices = model.Mesh.vertices;

            // Bounds of the group's own vertices, not the whole mesh. An average would be
            // pulled off centre by whichever end of the lump happens to carry more
            // triangles, and on a two-orb heartwood that is the denser inner one.
            var min = vertices[triangles[0]];
            var max = min;

            foreach (var vertex in triangles)
            {
                min = Vector3.Min(min, vertices[vertex]);
                max = Vector3.Max(max, vertices[vertex]);
            }

            var anchor = new GameObject(HeartwoodAnchor);
            anchor.transform.SetParent(visual, false);
            anchor.transform.localPosition = (min + max) * 0.5f;

            var light = anchor.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = StowConfig.PostLightRange.Value;
            light.color = Carrier.LightColour;
            light.intensity = StowConfig.PostLightIntensity.Value;

            // No shadows, same as the carrier: a light inside the thing that is the light
            // source casts its own geometry across the room for nothing.
            light.shadows = LightShadows.None;

            Flare.Attach(anchor.transform, StowConfig.PostFlareScale.Value);

            StowPlugin.Log.LogInfo("Heartwood found at " + anchor.transform.localPosition
                                   + " - the post is lit and the spirit starts there.");
        }

        /// <summary>
        /// One borrowed material per OBJ group. Public because the carrier is skinned the
        /// same way the post is - it is a different mesh wearing a different group, not a
        /// different idea.
        /// </summary>
        public static Material[] SkinsFor(string[] groups)
        {
            var skins = new Material[groups.Length];

            for (var i = 0; i < groups.Length; i++)
            {
                skins[i] = Borrow(groups[i]);
                if (skins[i] == null)
                    StowPlugin.Log.LogWarning(
                        "No material found for group '" + groups[i] + "'.");
            }

            return skins;
        }

        /// <summary>
        /// Forgets every borrowed material.
        ///
        /// Called on both ObjectDB entry points, because a different world may have a
        /// different set of prefabs loaded and these are lifted off loaded prefabs rather
        /// than off the item database. Without this the second world of a session skins
        /// itself from materials belonging to the first, which survives as long as the
        /// process does and looks like a texture bug.
        ///
        /// The post prefab keeps whatever it was built with - it is built once. This
        /// matters for the carrier, which borrows afresh every time one is built, well
        /// after startup.
        /// </summary>
        public static void Invalidate()
        {
            Cache.Clear();
            Atlas.Clear();
        }

        private static Material Borrow(string group)
        {
            Material cached;
            if (Cache.TryGetValue(group, out cached)) return cached;

            foreach (var raw in DonorsFor(group))
            {
                // Trimmed, because the glow list comes out of a config string and
                // "a, b, c" is how a person writes one.
                var name = raw.Trim();
                if (name.Length == 0) continue;

                // Via PropIndex rather than ZNetScene directly: many dressing prefabs carry
                // no ZNetView and so are invisible to ZNetScene however loaded they are.
                var donor = PropIndex.Find(name);
                if (donor == null) continue;

                foreach (var renderer in donor.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var material = renderer.sharedMaterial;
                    if (material == null || material.shader == null) continue;

                    // A material with no albedo renders flat and grey, which looks like a
                    // bug rather than a choice.
                    if (!material.HasProperty("_MainTex") || material.GetTexture("_MainTex") == null)
                        continue;

                    Cache[group] = material;
                    Atlas[group] = UvRegion(renderer);

                    StowPlugin.Log.LogInfo(string.Format(
                        "Group '{0}' skinned with {1} from {2} (shader {3}), atlas {4}.",
                        group, material.name, name, material.shader.name, Atlas[group]));
                    return material;
                }
            }

            Cache[group] = null;
            return null;
        }

        /// <summary>
        /// Which prefabs to try for a group, best first.
        ///
        /// The glow group reads its list out of config while the rest are a table here,
        /// and that asymmetry is the point: wood is wood and a wooden wall will always
        /// have a wooden material on it, whereas "something in this game that glows" is a
        /// guess until the game has been asked. Set LookForProps and read the log to find
        /// a better one than the default.
        /// </summary>
        private static string[] DonorsFor(string group)
        {
            if (string.Equals(group, GlowGroup, StringComparison.OrdinalIgnoreCase))
                return (StowConfig.CarrierGlowDonors.Value ?? "").Split(',');

            string[] donors;
            return Donors.TryGetValue(group, out donors) ? donors : Donors["wood"];
        }

        /// <summary>
        /// The slice of texture one face of the donor uses.
        ///
        /// Deliberately one face, not the whole mesh. Measuring min/max across every
        /// vertex gives a rectangle spanning every tile the donor touches - for
        /// stone_wall_2x1 that was 71% of the sheet - and squeezing our coordinates into
        /// that still walks across tile boundaries, which is why the trough stayed striped
        /// and its charcoal stayed green after the first attempt at this.
        ///
        /// The largest single triangle is used because area is a good proxy for "a plain
        /// wall face" rather than a trim detail, and a triangle cannot straddle two tiles
        /// without the donor itself looking wrong.
        /// </summary>
        private static Rect UvRegion(Renderer renderer)
        {
            var whole = new Rect(0f, 0f, 1f, 1f);

            var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return whole;

            Vector2[] uv;
            int[] tris;
            try
            {
                // Imported meshes are frequently upload-only; reading them then throws.
                if (!mesh.isReadable) return whole;
                uv = mesh.uv;
                tris = mesh.triangles;
            }
            catch { return whole; }

            if (uv == null || uv.Length == 0 || tris == null || tris.Length < 3) return whole;

            var bestArea = 0f;
            var best = whole;

            for (var i = 0; i + 2 < tris.Length; i += 3)
            {
                var a = tris[i];
                var b = tris[i + 1];
                var c = tris[i + 2];
                if (a >= uv.Length || b >= uv.Length || c >= uv.Length) continue;

                var minX = Mathf.Min(uv[a].x, Mathf.Min(uv[b].x, uv[c].x));
                var maxX = Mathf.Max(uv[a].x, Mathf.Max(uv[b].x, uv[c].x));
                var minY = Mathf.Min(uv[a].y, Mathf.Min(uv[b].y, uv[c].y));
                var maxY = Mathf.Max(uv[a].y, Mathf.Max(uv[b].y, uv[c].y));

                var width = maxX - minX;
                var height = maxY - minY;

                // A face that itself tiles past the sheet edge tells us nothing useful.
                if (width <= 0.005f || height <= 0.005f) continue;
                if (width > 1f || height > 1f) continue;

                var area = width * height;
                if (area <= bestArea) continue;

                bestArea = area;
                best = new Rect(minX, minY, width, height);
            }

            return bestArea > 0f ? best : whole;
        }

        /// <summary>
        /// Squeezes each submesh's UVs into its material's slice of the atlas.
        ///
        /// Wrapped first, then mapped: the mesh is unwrapped at world scale so its
        /// coordinates run well past 1, and mapping those directly would walk straight
        /// out of the region again. Repeat brings them back into 0..1 so the tiling
        /// survives, then the rect places that tile where the texture actually is.
        /// </summary>
        public static void Remap(Mesh mesh, string[] groups)
        {
            if (mesh == null || groups == null) return;

            var uv = mesh.uv;
            if (uv == null || uv.Length == 0) return;

            var count = Mathf.Min(groups.Length, mesh.subMeshCount);
            var moved = 0;

            // A vertex sitting on the seam between two groups appears in both submeshes,
            // and mapping it twice would squeeze it into a rectangle inside a rectangle -
            // a sliver of a texel, stretched across the face.
            var done = new bool[uv.Length];

            for (var i = 0; i < count; i++)
            {
                Rect rect;
                if (!Atlas.TryGetValue(groups[i], out rect)) continue;
                if (rect.width >= 0.999f && rect.height >= 0.999f) continue;

                foreach (var index in mesh.GetTriangles(i))
                {
                    if (index < 0 || index >= uv.Length || done[index]) continue;
                    done[index] = true;

                    // Clamped, never wrapped. Repeat() here was the bug: it wraps per
                    // vertex, so a face crossing 1.0 got vertices at 0.9 and 0.2 and the
                    // GPU interpolated backwards across the whole tile between them - the
                    // smeared diagonal banding that made a square model look crooked. The
                    // mesh is now unwrapped inside 0..1, so a straight map is enough.
                    uv[index] = new Vector2(
                        rect.x + Mathf.Clamp01(uv[index].x) * rect.width,
                        rect.y + Mathf.Clamp01(uv[index].y) * rect.height);
                }

                moved++;
            }

            if (moved == 0) return;

            mesh.uv = uv;
            StowPlugin.Log.LogInfo("Remapped UVs into the atlas for " + moved + " group(s).");
        }

        /// <summary>
        /// Boxes from the sidecar, replacing whatever shape the donor had. A barrel's
        /// capsule around a square bin leaves you bumping into air at the corners.
        /// </summary>
        private static void ReplaceColliders(GameObject prefab, string path)
        {
            if (!File.Exists(path))
            {
                StowPlugin.Log.LogWarning(
                    "No collider file beside the dll - keeping the donor's collision.");
                return;
            }

            var boxes = new List<string[]>();
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;

                var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 7 && parts[0] == "box") boxes.Add(parts);
            }

            if (boxes.Count == 0) return;

            foreach (var collider in prefab.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);

            var culture = CultureInfo.InvariantCulture;
            var host = new GameObject("post_collision");
            host.transform.SetParent(prefab.transform, false);

            foreach (var parts in boxes)
            {
                var box = host.AddComponent<BoxCollider>();
                box.center = new Vector3(
                    float.Parse(parts[1], culture),
                    float.Parse(parts[2], culture),
                    float.Parse(parts[3], culture));
                box.size = new Vector3(
                    float.Parse(parts[4], culture),
                    float.Parse(parts[5], culture),
                    float.Parse(parts[6], culture));
            }

            StowPlugin.Log.LogInfo("Post collision: " + boxes.Count + " boxes.");
        }
    }
}
