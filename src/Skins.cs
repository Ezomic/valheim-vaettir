using System;
using System.Collections.Generic;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// Lends each material group a real material off a vanilla prefab.
    ///
    /// Nothing here paints anything. The meshes are ours and the surfaces are the
    /// game's, which is what keeps a hand-built model looking like it belongs -
    /// texel density, palette and weathering all come along because they are the
    /// game's own. It also sidesteps swapping _MainTex on a borrowed material, which
    /// keeps the donor's normal map and leaves the surface lit for a shape it no
    /// longer has.
    ///
    /// Ported from Stow, which got it from Stoker. The atlas measuring is the part
    /// that took several attempts and is worth not re-deriving.
    /// </summary>
    internal static class Skins
    {
        /// <summary>
        /// The one group that has to glow, and the property that decides whether it can.
        ///
        /// Named here rather than spelled twice: the same string is what
        /// ForestSpirit.Pulse writes to, and a material chosen without it is a material
        /// the breathing silently does nothing on.
        /// </summary>
        private const string GlowGroup = "core";
        private const string Emission = "_EmissionColor";

        /// <summary>Prefabs to lift each group's material from, best first.</summary>
        private static readonly Dictionary<string, string[]> Donors =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "bark",  new[] { "wood_wall", "wood_beam", "piece_chest_wood" } },
                { "wood",  new[] { "wood_wall", "wood_beam", "piece_chest_wood" } },
                { "seed",  new[] { "wood_beam", "wood_wall" } },
                { "stone", new[] { "stone_wall_2x1", "piece_stonecutter", "smelter" } },

                // Anything green and growing. A sapling skinned in plank would be a
                // stick with a plank on top of it.
                { "moss",  new[] { "sapling_carrot", "sapling_turnip", "Bush01",
                                   "shrub_2", "piece_beehive" } },

                // A mushroom cap, for Thicket's seedlings. Its own group rather than moss
                // because a green mushroom reads as a leaf on a stick, and the donors are
                // the mushrooms themselves - they are the one vanilla surface that is
                // neither bark, stone nor foliage.
                { "cap",   new[] { "Pickable_Mushroom", "Pickable_Mushroom_yellow",
                                   "Pickable_Mushroom_blue", "sapling_carrot" } },

                // Two flower heads, not one shared one. Thistle and dandelion are both
                // mostly leaf with one coloured tuft, and that tuft is the only thing
                // telling them apart at seedling size - so a single group would cache one
                // material for both and hand the thistle a dandelion's yellow. The group
                // name is the cache key, which is exactly why they cannot share it.
                { "bloom", new[] { "Pickable_Dandelion", "sapling_carrot" } },

                // The bonemeal sack. Cloth from the DEER RUG, not the hide items:
                // LeatherScraps wears Custom/Creature, an alpha-cutout shader over a
                // ragged atlas whose measured rect is mostly transparent pixels - the
                // sack body rendered fully invisible while its opaque-donored rope
                // and meal showed, which cost three rounds of winding archaeology
                // before the texture was suspected. The rug is opaque hide on
                // Custom/Piece. The meal keeps BoneFragments - its rect lands on
                // solid bone - and the rope rides plank.
                { "cloth", new[] { "rug_lox", "rug_deer", "rug_fur", "wood_wall" } },
                { "meal",  new[] { "BoneFragments", "stone_wall_2x1" } },
                { "rope",  new[] { "wood_wall", "wood_beam" } },
                { "bud",   new[] { "Pickable_Thistle", "Pickable_Dandelion",
                                   "sapling_carrot" } },
            };

        private static readonly Dictionary<string, Material> Cache =
            new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Where in its texture each borrowed material actually lives.
        ///
        /// Valheim's piece textures are atlases: a material does not use the whole
        /// image, it uses a strip of one. UVs running 0..1 therefore sample the entire
        /// sheet and pick up whatever the neighbouring tiles are.
        /// </summary>
        private static readonly Dictionary<string, Rect> Atlas =
            new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Each borrowed sheet's width in pixels. Density is texels per metre, so
        /// without the pixel count there is no way to turn "42 texels" into a UV
        /// scale - a 64px flower sheet and a 256px village sheet need a fourfold
        /// different scale for the same visual grain size.
        /// </summary>
        private static readonly Dictionary<string, int> TexPx =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public static void Invalidate()
        {
            Cache.Clear();
            Atlas.Clear();
            TexPx.Clear();
        }

        public static Material For(string group)
        {
            Material cached;
            if (Cache.TryGetValue(group, out cached)) return cached;

            // The glow group is asked twice: once demanding a material that actually
            // emits, and only then falling back to any material at all.
            //
            // Insurance rather than a fix, and worth being honest about because the
            // reasoning that produced it was wrong. The theory was that fire_pit's
            // stone ring - Custom/StaticRock, the first material with an albedo and so
            // the one this was picking - had no _EmissionColor, and that
            // ForestSpirit.Pulse had therefore been writing its breathing into a
            // property nothing read. DumpMaterials says otherwise:
            //
            //   donor fire_pit: stone shader=Custom/StaticRock emission=True albedo=True
            //
            // So the property was always there and this pass picks the very same
            // material. It stays because the donor list is a guess about the game
            // rather than a fact about it, and the day a first hit genuinely has no
            // emission the symptom would be a spirit that does not breathe and nothing
            // in the log to say why. It is not load-bearing today.
            if (group == GlowGroup)
            {
                var lit = Pick(group, true);
                if (lit != null) return lit;
            }

            return Pick(group, false);
        }

        /// <summary>
        /// One walk through the donors. With mustEmit, only materials whose shader
        /// exposes _EmissionColor are accepted.
        /// </summary>
        private static Material Pick(string group, bool mustEmit)
        {
            foreach (var name in DonorsFor(group))
            {
                // Via PropIndex rather than ZNetScene directly: many dressing prefabs
                // carry no ZNetView and so are invisible to ZNetScene however loaded.
                var donor = PropIndex.Find(name);
                if (donor == null) continue;

                // The renderer that IS the object, not whichever renderer happens to
                // come back first - vanilla prefabs carry Worn and Broken copies,
                // destruction chunks and LODs, and hierarchy order is an accident.
                // Falls back to the first textured renderer when nothing is readable,
                // because an unmeasurable donor is still a usable material.
                var renderer = MainRenderer(donor);
                var measured = renderer != null;
                if (renderer == null) renderer = FirstTextured(donor);
                if (renderer == null) continue;

                var material = renderer.sharedMaterial;
                if (mustEmit && !material.HasProperty(Emission)) continue;

                // A material whose shader applies its own texture transform samples
                // somewhere other than where the mesh UVs point - woodwall carries
                // scale (-0.56, 0.12), which crushes anything mapped through it into
                // a 15-pixel band along the sheet's bottom edge. That band is why the
                // stow post's timber shipped as a featureless black smear. Measuring
                // around the transform would need per-axis density, which stretches
                // grain, so such a donor is passed over while the list has more.
                var st = material.mainTextureScale;
                if ((Mathf.Abs(st.x - 1f) > 0.001f || Mathf.Abs(st.y - 1f) > 0.001f))
                {
                    GrovePlugin.Log.LogInfo(string.Format(
                        "'{0}': skipping {1} from {2} - its material scales its own "
                        + "texture by {3:0.00}x{4:0.00}.",
                        group, material.name, name, st.x, st.y));
                    continue;
                }

                Cache[group] = material;
                Atlas[group] = measured ? SideRegion(renderer) : new Rect(0f, 0f, 1f, 1f);

                var sheet = material.GetTexture("_MainTex");
                TexPx[group] = Mathf.Max(1, sheet != null ? sheet.width : 1);

                GrovePlugin.Log.LogInfo(string.Format(
                    "'{0}' skinned with {1} from {2} (shader {3}){4}, atlas {5}, {6}px.",
                    group, material.name, name, material.shader.name,
                    mustEmit ? " [emissive]" : "", Atlas[group], TexPx[group]));
                return material;
            }

            // Only a failure on the second pass. The first is allowed to come up empty -
            // that is what the second is for - and warning about it would report a
            // working fallback as a fault.
            if (mustEmit) return null;

            GrovePlugin.Log.LogWarning("No material found for group '" + group + "'.");
            Cache[group] = null;
            return null;
        }

        /// <summary>
        /// The renderer carrying the most readable geometry - the one that is actually
        /// the object. Taking the first renderer with an albedo was wrong on almost
        /// every vanilla prefab: on wood_wall_log the accidental winner's UVs covered
        /// a slice 0.151 wide where the wall's real bark field is 0.424.
        /// </summary>
        internal static MeshRenderer MainRenderer(GameObject donor)
        {
            MeshRenderer best = null;
            var most = 0;

            foreach (var renderer in donor.GetComponentsInChildren<MeshRenderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (material == null || material.shader == null) continue;
                if (!material.HasProperty("_MainTex")
                    || material.GetTexture("_MainTex") == null) continue;

                MeshFilter filter;
                if (!renderer.TryGetComponent(out filter)) continue;
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                int count;
                try
                {
                    // Imported meshes are frequently upload-only; reading throws.
                    if (!mesh.isReadable) continue;
                    count = mesh.triangles.Length;
                }
                catch { continue; }

                if (count <= most) continue;
                most = count;
                best = renderer;
            }

            return best;
        }

        private static MeshRenderer FirstTextured(GameObject donor)
        {
            foreach (var renderer in donor.GetComponentsInChildren<MeshRenderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (material == null || material.shader == null) continue;
                if (!material.HasProperty("_MainTex")
                    || material.GetTexture("_MainTex") == null) continue;
                return renderer;
            }
            return null;
        }

        private static string[] DonorsFor(string group)
        {
            // The glow donor is configurable because which vanilla prefab yields a
            // material that reads as lit is a question for the game, not a guess.
            if (string.Equals(group, "core", StringComparison.OrdinalIgnoreCase))
                return (GroveConfig.GlowDonors.Value ?? "").Split(',');

            string[] donors;
            return Donors.TryGetValue(group, out donors) ? donors : Donors["wood"];
        }

        public static Material[] Skin(string[] groups)
        {
            var skins = new Material[groups.Length];
            for (var i = 0; i < groups.Length; i++) skins[i] = For(groups[i]);
            return skins;
        }

        /// <summary>
        /// The slice of texture the donor's broad faces sit on.
        ///
        /// Neither the whole mesh's extent nor one triangle - both were shipped and
        /// both are wrong in opposite directions. Min/max across every vertex spans
        /// every tile the donor touches (71% of the sheet on stone_wall_2x1); the
        /// largest single triangle, which this used until the stow post showed
        /// otherwise, is far too small - stone_mat's biggest triangle was a 12x9
        /// texel patch of gravel, and the whole plinth was squeezed into it.
        ///
        /// So: cluster the triangles by whether their UV rectangles touch, and take
        /// the biggest cluster. Adjacent strips of one painted field merge into that
        /// field; a log-end disc off in a corner has nothing adjoining it and stays
        /// its own cluster. Ported from Kynda, where it replaced the same bug.
        /// </summary>
        internal static Rect SideRegion(Renderer renderer)
        {
            var whole = new Rect(0f, 0f, 1f, 1f);

            var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return whole;

            Vector3[] verts;
            Vector2[] uv;
            int[] tris;
            try
            {
                // Imported meshes are frequently upload-only; reading them then throws.
                if (!mesh.isReadable) return whole;
                verts = mesh.vertices;
                uv = mesh.uv;
                tris = mesh.triangles;
            }
            catch { return whole; }

            if (uv == null || uv.Length == 0 || tris == null || tris.Length < 3) return whole;
            if (verts == null || verts.Length == 0) return whole;

            // Bucketed by the dominant axis of the geometric face normal - not the
            // shading normal, which leans around a smooth-shaded curve. Clustering
            // every triangle regardless of facing merges every painted region on a
            // sheet like barrell's into one rectangle covering 98% of it; per axis,
            // the side grain and the painted end discs stay separate features.
            var rects = new[] { new List<Rect>(), new List<Rect>(), new List<Rect>() };

            for (var i = 0; i + 2 < tris.Length; i += 3)
            {
                var a = tris[i];
                var b = tris[i + 1];
                var c = tris[i + 2];
                if (a >= uv.Length || b >= uv.Length || c >= uv.Length) continue;
                if (a >= verts.Length || b >= verts.Length || c >= verts.Length) continue;

                var cross = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                if (cross.magnitude <= 1e-9f) continue;
                var n = cross.normalized;
                var axis = Mathf.Abs(n.x) >= Mathf.Abs(n.y) && Mathf.Abs(n.x) >= Mathf.Abs(n.z)
                    ? 0
                    : (Mathf.Abs(n.y) >= Mathf.Abs(n.z) ? 1 : 2);

                var minX = Mathf.Min(uv[a].x, Mathf.Min(uv[b].x, uv[c].x));
                var maxX = Mathf.Max(uv[a].x, Mathf.Max(uv[b].x, uv[c].x));
                var minY = Mathf.Min(uv[a].y, Mathf.Min(uv[b].y, uv[c].y));
                var maxY = Mathf.Max(uv[a].y, Mathf.Max(uv[b].y, uv[c].y));

                var width = maxX - minX;
                var height = maxY - minY;

                // A face that itself tiles past the sheet edge tells us nothing useful.
                if (width <= 0.005f || height <= 0.005f) continue;
                if (width > 1f || height > 1f) continue;

                rects[axis].Add(new Rect(minX, minY, width, height));
            }

            // The biggest cluster on any axis, not on whichever axis carries the most
            // surface - Kynda tried tying it to the surface leader and it turned on
            // the mesh's local orientation rather than on anything about the texture.
            var best = new Rect(0f, 0f, 0f, 0f);
            for (var axis = 0; axis < 3; axis++)
            {
                var field = Cluster(rects[axis]);
                if (field.width * field.height > best.width * best.height) best = field;
            }

            return best.width > 0f ? best : whole;
        }

        /// <summary>
        /// The bounding rectangle of the largest group of triangle-rects that touch
        /// each other - the shape of one painted feature on the sheet. A field is
        /// covered by triangles that necessarily abut; a disc or a knot painted off
        /// on its own has nothing adjoining it and stays separate. No threshold to
        /// tune and no assumption about where a donor keeps things.
        /// </summary>
        private static Rect Cluster(List<Rect> rects)
        {
            var none = new Rect(0f, 0f, 0f, 0f);
            if (rects == null || rects.Count == 0) return none;

            var merged = new List<Rect>(rects);

            // Touching, not merely overlapping: adjacent strips of one field share an
            // edge exactly, and floating point means "exactly" needs slack - under
            // half a texel on the smallest sheet vanilla ships.
            const float touch = 1f / 128f;

            bool changed;
            var guard = 0;
            do
            {
                changed = false;
                guard++;

                for (var i = 0; i < merged.Count && !changed; i++)
                {
                    for (var j = i + 1; j < merged.Count; j++)
                    {
                        var a = merged[i];
                        var b = merged[j];

                        if (a.xMin > b.xMax + touch || b.xMin > a.xMax + touch) continue;
                        if (a.yMin > b.yMax + touch || b.yMin > a.yMax + touch) continue;

                        var x0 = Mathf.Min(a.xMin, b.xMin);
                        var y0 = Mathf.Min(a.yMin, b.yMin);
                        merged[i] = new Rect(x0, y0,
                            Mathf.Max(a.xMax, b.xMax) - x0,
                            Mathf.Max(a.yMax, b.yMax) - y0);
                        merged.RemoveAt(j);
                        changed = true;
                        break;
                    }
                }
            }
            while (changed && guard < 4096);

            var best = none;
            foreach (var rect in merged)
            {
                if (rect.width * rect.height > best.width * best.height) best = rect;
            }

            return best;
        }

        /// <summary>
        /// Places each submesh's UVs inside its material's slice of the atlas, at a
        /// texel density chosen rather than inherited.
        /// </summary>
        public static void Remap(Mesh mesh, string[] groups)
        {
            Fit(mesh, groups, Atlas, TexPx);
        }

        /// <summary>
        /// The density fit itself, over whichever caches the caller keeps - Grove's
        /// pieces and Stow's post each have their own donor tables, and this is the
        /// half they share.
        ///
        /// The exported UVs are in METRES - vhbuild cube-projects every part at a
        /// cube size of 1 before the join - so the wanted scale is target texels
        /// divided by the sheet's width in pixels, reduced until the part fits its
        /// rect. Uniform on both axes, or grain smears in one direction.
        ///
        /// Per island (one island per part, connected through shared vertices), not
        /// per group: a group's extent is every part laid side by side, which is a
        /// surface no single part has. Kynda measured six hoops as 2.72m of UV where
        /// one hoop is 1.9m round - fitting the group cost them 7 texels/m and they
        /// rendered as thin white wires. Islands may overlap inside the rect; that is
        /// what tiling means, and it is safe because a part is placed whole.
        ///
        /// Clamped, never wrapped. Repeat() here was the bug Stow paid for: it wraps
        /// per vertex, so a face crossing 1.0 got vertices at 0.9 and 0.2 and the GPU
        /// interpolated backwards across the whole tile between them.
        /// </summary>
        internal static void Fit(Mesh mesh, string[] groups,
                                 Dictionary<string, Rect> atlas,
                                 Dictionary<string, int> texPx)
        {
            if (mesh == null || groups == null) return;

            var uv = mesh.uv;
            if (uv == null || uv.Length == 0) return;

            var count = Mathf.Min(groups.Length, mesh.subMeshCount);
            var target = Mathf.Max(1f, GroveConfig.TexelsPerMetre.Value);

            // A vertex on the seam between two groups appears in both submeshes, and
            // mapping it twice would place it relative to an already-placed position.
            var done = new bool[uv.Length];

            for (var i = 0; i < count; i++)
            {
                Rect rect;
                int px;
                if (!atlas.TryGetValue(groups[i], out rect)) continue;
                if (!texPx.TryGetValue(groups[i], out px) || px <= 0) px = 64;

                var indices = mesh.GetTriangles(i);
                if (indices.Length == 0) continue;

                var islands = Islands(indices, uv);
                var coarsest = float.MaxValue;
                var finest = 0f;

                foreach (var island in islands)
                {
                    var min = new Vector2(float.MaxValue, float.MaxValue);
                    var max = new Vector2(float.MinValue, float.MinValue);
                    foreach (var index in island)
                    {
                        min = Vector2.Min(min, uv[index]);
                        max = Vector2.Max(max, uv[index]);
                    }

                    var span = max - min;
                    if (span.x <= 0f || span.y <= 0f) continue;

                    var scale = target / px;
                    scale = Mathf.Min(scale, rect.width / span.x);
                    scale = Mathf.Min(scale, rect.height / span.y);

                    coarsest = Mathf.Min(coarsest, scale * px);
                    finest = Mathf.Max(finest, scale * px);

                    // Centred in the rect, so a part that does fit samples the middle
                    // of the patch rather than its edge, where the neighbour bleeds.
                    var centre = (min + max) * 0.5f;
                    var offset = new Vector2(rect.x + rect.width * 0.5f,
                                             rect.y + rect.height * 0.5f) - centre * scale;

                    foreach (var index in island)
                    {
                        if (done[index]) continue;
                        done[index] = true;

                        uv[index] = new Vector2(
                            Mathf.Clamp(uv[index].x * scale + offset.x, rect.xMin, rect.xMax),
                            Mathf.Clamp(uv[index].y * scale + offset.y, rect.yMin, rect.yMax));
                    }
                }

                if (finest <= 0f) continue;

                GrovePlugin.Log.LogInfo(string.Format(
                    "'{0}' laid out at {1:0}-{2:0} texels/m (wanted {3:0}) across {4} "
                    + "part(s) in a {5:0.000}x{6:0.000} rect.",
                    groups[i], coarsest, finest, target, islands.Count,
                    rect.width, rect.height));
            }

            mesh.uv = uv;
        }

        /// <summary>
        /// The submesh's vertices split into connected parts - one per plank, band or
        /// orb. Connectivity is through shared vertex indices, which is exactly right
        /// for these models: every part is built as its own object and only joined at
        /// the end, so no two parts ever share a vertex. The seams a projection
        /// creates split a part further, and that is fine - the pieces of one plank
        /// all want the same treatment and get it, being the same size.
        /// </summary>
        private static List<List<int>> Islands(int[] indices, Vector2[] uv)
        {
            var parent = new Dictionary<int, int>();

            for (var i = 0; i < indices.Length; i++)
            {
                var v = indices[i];
                if (v >= 0 && v < uv.Length && !parent.ContainsKey(v)) parent[v] = v;
            }

            for (var i = 0; i + 2 < indices.Length; i += 3)
            {
                var a = indices[i];
                var b = indices[i + 1];
                var c = indices[i + 2];
                if (!parent.ContainsKey(a) || !parent.ContainsKey(b)
                    || !parent.ContainsKey(c)) continue;

                Join(parent, a, b);
                Join(parent, a, c);
            }

            // Snapshot the keys: Find compresses paths as it goes, and .NET
            // Framework's Dictionary bumps its version even on an overwrite, so
            // walking parent.Keys directly throws part way through.
            var keys = new List<int>(parent.Keys);

            var groups = new Dictionary<int, List<int>>();
            foreach (var v in keys)
            {
                var root = Find(parent, v);

                List<int> island;
                if (!groups.TryGetValue(root, out island))
                {
                    island = new List<int>();
                    groups[root] = island;
                }
                island.Add(v);
            }

            return new List<List<int>>(groups.Values);
        }

        private static int Find(Dictionary<int, int> parent, int v)
        {
            while (parent[v] != v)
            {
                parent[v] = parent[parent[v]];
                v = parent[v];
            }
            return v;
        }

        private static void Join(Dictionary<int, int> parent, int a, int b)
        {
            var ra = Find(parent, a);
            var rb = Find(parent, b);
            if (ra != rb) parent[ra] = rb;
        }
    }
}
