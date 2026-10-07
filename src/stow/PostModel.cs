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
        /// Groups skinned from the workbench's atlas, mapped by position in metres rather
        /// than from the OBJ's own UVs, and the width of the sheet each one samples.
        /// </summary>
        private static readonly HashSet<string> Metric =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, int> TexPx =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Each kind of part wears a vanilla material that suits what it IS, borrowed whole and
        // never with its _MainTex swapped, and is placed inside a rect of that donor's sheet.
        // The rects were measured from the Devkit rips in own-profile/BepInEx/rips and are inset
        // past each painted island's worn rim; a skin is used only if the donor's material name,
        // sheet name and sheet width match what was measured, otherwise the group keeps the
        // classic donors.
        //
        //   wood            the workbench's own planks, a weighted pick of four plank islands
        //   iron            the stonecutter bench's grey metal, its darkest patch: forged hardware
        //   stone, clay     stone_wall_2x1's dark rubble (there is no clay donor on disk)
        //   wicker, cord    the village container sheet's golden strand weave
        //   frond           the fiddlehead fern's leaf, stretched to fill the part
        //
        // Deliberately NOT the workbench's hide, stones or leather straps: those are the bench's
        // own details, and the Vaettir pieces carry details of their own.
        private sealed class DonorSkin
        {
            public string Prefab, Material, Sheet;

            /// <summary>
            /// The material's own asset name, loaded straight from the game's bundles when
            /// the prefab cannot be found. The village container sheet is carried by props
            /// that are children of a location, so nothing in a world has them loaded.
            /// </summary>
            public string Asset;
            public int Px;
            public Rect[] Rects;
            public bool Stretch;
        }

        private static readonly Rect[] BenchPlanks =
        {
            new Rect(0.06f, 0.15f, 0.84f, 0.31f),   // the big plank field
            new Rect(0.06f, 0.15f, 0.84f, 0.31f),
            new Rect(0.33f, 0.60f, 0.25f, 0.15f),   // the plank with horizontal grain
            new Rect(0.57f, 0.78f, 0.16f, 0.18f),   // the upright plank
            new Rect(0.07f, 0.58f, 0.15f, 0.30f),   // the darker plank
        };

        private static readonly DonorSkin WoodSkin = new DonorSkin
            { Prefab = "piece_workbench", Material = "Workbench_mat", Sheet = "WorkBench_d", Px = 256, Rects = BenchPlanks };

        private static readonly DonorSkin IronSkin = new DonorSkin
            { Prefab = "piece_stonecutter", Material = "StoneCutterBench_mat", Sheet = "StoneCutterBench_d", Px = 256,
              Rects = new[] { new Rect(0.00f, 0.70f, 0.12f, 0.14f) } };

        private static readonly DonorSkin StoneSkin = new DonorSkin
            { Prefab = "stone_wall_2x1", Material = "stone_mat", Sheet = "stone", Px = 128,
              Rects = new[] { new Rect(0.00f, 0.10f, 0.55f, 0.30f) } };

        private static readonly DonorSkin WeaveSkin = new DonorSkin
            { Prefab = "fi_vil_container_basket02_closed", Material = "fi_village_containers",
              Asset = "fi_village_containers", Sheet = "fi_village_containers_hd", Px = 256,
              Rects = new[] { new Rect(0.62f, 0.86f, 0.36f, 0.12f) } };

        private static readonly DonorSkin FrondSkin = new DonorSkin
            { Prefab = "Pickable_Fiddlehead", Material = "FernAshlands_mat", Sheet = "Ashlandsvegetation_d", Px = 64,
              Rects = new[] { new Rect(0.02f, 0.05f, 0.30f, 0.90f) }, Stretch = true };

        // A patch of the same fern sheet for the moss tufts on the roost cage: the sheet has no
        // moss, and the leaf's green island is the nearest thing in the game that is not a plank.
        private static readonly DonorSkin MossSkin = new DonorSkin
            { Prefab = "Pickable_Fiddlehead", Material = "FernAshlands_mat", Sheet = "Ashlandsvegetation_d", Px = 64,
              Rects = new[] { new Rect(0.12f, 0.07f, 0.16f, 0.08f) } };

        private static readonly HashSet<string> Stretch =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Rect[]> MetricRects =
            new Dictionary<string, Rect[]>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Which donor a model group is skinned from, by the group's name. Unknown groups are
        /// wood: a group with no suitable donor must still be drawn.
        /// </summary>
        private static DonorSkin SkinFor(string group)
        {
            switch ((group ?? "").ToLowerInvariant())
            {
                case "iron":
                case "strap":
                case "band":
                    return IronSkin;
                case "stone":
                case "clay":
                case "rock":
                    return StoneSkin;
                case "wicker":
                case "cord":
                case "rope":
                    return WeaveSkin;
                case "frond":
                    return FrondSkin;
                case "moss":
                    return MossSkin;
                default:
                    return WoodSkin;
            }
        }

        // Vanilla furniture measures 40 to 45 texels per metre across the chest, the
        // workbench and the shelf; the round-log props sit near 28.
        private const float TexelsPerMetre = 42f;

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
                                 bool heartwood, bool modern = false)
        {
            var dir = Path.GetDirectoryName(typeof(PostModel).Assembly.Location);
            var model = ObjMesh.Load(Path.Combine(dir, modelFile));

            if (model == null || model.Mesh == null)
            {
                StowRuntime.Log.LogWarning(
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
            meshRenderer.sharedMaterials = SkinsFor(model.Groups, modern);

            // Taken before Remap: a re-skin in a later world starts from the OBJ's own UVs,
            // not from coordinates already squeezed into the last world's rects.
            if (modern) Track(meshRenderer, model, visualName);

            // After SkinsFor, because that is what learns each group's atlas rectangle.
            Remap(model.Mesh, model.Groups, modern);

            if (heartwood) Heartwood(prefab, visual.transform, model);

            ReplaceColliders(prefab, Path.Combine(dir, Path.ChangeExtension(modelFile, ".col")));

            StowRuntime.Log.LogInfo(string.Format(
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

            StowRuntime.Log.LogInfo("Heartwood found at " + anchor.transform.localPosition
                                   + " - the post is lit and the spirit starts there.");
        }

        /// <summary>
        /// One borrowed material per OBJ group. Public because the carrier is skinned the
        /// same way the post is - it is a different mesh wearing a different group, not a
        /// different idea.
        /// </summary>
        public static Material[] SkinsFor(string[] groups, bool modern = false)
        {
            var skins = new Material[groups.Length];

            for (var i = 0; i < groups.Length; i++)
            {
                skins[i] = Borrow(groups[i], modern);
                if (skins[i] == null)
                    StowRuntime.Log.LogWarning(
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
            Epoch++;
            Cache.Clear();
            Atlas.Clear();
            Metric.Clear();
            MetricRects.Clear();
            Stretch.Clear();
            TexPx.Clear();
            Notes.Clear();
        }

        private static Material Borrow(string group, bool modern = false)
        {
            var key = Key(group, modern);
            Material cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            if (modern)
            {
                var skinned = BorrowSkin(group);
                if (skinned != null) return skinned;
            }

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

                    Cache[key] = material;
                    Atlas[key] = UvRegion(renderer);
                    if (modern)
                        Notes[key] = new SkinNote
                        {
                            Donor = name, Material = material.name, Via = "classic",
                            Status = string.Equals(group, GlowGroup, StringComparison.OrdinalIgnoreCase)
                                ? "glow" : "classic",
                        };

                    StowRuntime.Log.LogInfo(string.Format(
                        "Group '{0}' skinned with {1} from {2} (shader {3}), atlas {4}.",
                        group, material.name, name, material.shader.name, Atlas[key]));
                    return material;
                }
            }

            Cache[key] = null;
            if (modern) Notes[key] = new SkinNote();
            return null;
        }

        /// <summary>
        /// The role-appropriate vanilla material for a group (see SkinFor), or null so the
        /// classic donors run. A skin is used only if the donor's material name, sheet name and
        /// sheet width match what the rects were measured on: the rects are only right for that
        /// exact sheet, and a patch that changes it must fall back rather than paint from the
        /// gutters. The glow group is never touched.
        ///
        /// A group whose own donor cannot be found is skinned from the workbench planks instead
        /// ("falls back to wood"), and only if the workbench is missing too does it reach the
        /// classic donors. Each miss is said once per session, since this runs for every group
        /// of every world.
        ///
        /// The material is borrowed whole and never has its _MainTex swapped, so its normal map,
        /// smoothness and noise come with it exactly as the donor has them.
        /// </summary>
        private static Material BorrowSkin(string group)
        {
            if (string.Equals(group, GlowGroup, StringComparison.OrdinalIgnoreCase)) return null;

            var own = SkinFor(group);
            var material = TrySkin(group, own, false);
            if (material != null || own == WoodSkin) return material;

            return TrySkin(group, WoodSkin, true);
        }

        private static Material TrySkin(string group, DonorSkin skin, bool fallback)
        {
            string via;
            var material = FindSkinMaterial(skin, out via);

            if (material == null)
            {
                SayOnce("Group '" + group + "': " + skin.Prefab + " did not offer " + skin.Material
                    + " on " + skin.Sheet + " (" + skin.Px + " px) in this world"
                    + (skin == WoodSkin ? ", so it keeps the classic donors."
                                        : ", so it wears the workbench planks instead."));
                return null;
            }

            var key = Key(group, true);
            Notes[key] = new SkinNote
            {
                Donor = skin.Prefab, Material = material.name, Via = via,
                Status = fallback ? "fallback" : "ok",
            };
            Cache[key] = material;
            Atlas[key] = skin.Rects[0];
            MetricRects[key] = skin.Rects;
            TexPx[key] = skin.Px;
            Metric.Add(key);
            if (skin.Stretch) Stretch.Add(key);

            StowRuntime.Log.LogInfo(string.Format(
                "Group '{0}' skinned with {1} from {2}, {3} rect(s), first {4}.",
                group, material.name, skin.Prefab, skin.Rects.Length, skin.Rects[0]));
            return material;
        }

        private static Material FindSkinMaterial(DonorSkin skin, out string via)
        {
            via = "none";
            var donor = PropIndex.Find(skin.Prefab);

            if (donor != null)
            {
                foreach (var renderer in donor.GetComponentsInChildren<Renderer>(true))
                {
                    var material = renderer.sharedMaterial;
                    if (!Matches(material, skin)) continue;

                    via = "prefab";
                    return material;
                }
            }

            // Held by a soft reference that is never released, so unlike a material found by
            // walking a prefab it survives the bundle unload at logout.
            if (skin.Asset != null)
            {
                var direct = SoftAssets.LoadMaterial(skin.Asset);
                if (Matches(direct, skin))
                {
                    via = "asset";
                    return direct;
                }
            }

            return null;
        }

        /// <summary>
        /// What the `hod skin` readout prints: for the newest standing piece of that name, one
        /// line per OBJ group saying which donor it was skinned from and how, then a summary.
        /// A line holds name=value tokens with no spaces inside a token, in a fixed order, so
        /// Devkit's `printed` can pin "group=wicker status=ok" as one substring.
        ///
        /// <b>status</b> is the part that matters. ok is the group's own donor. fallback is a
        /// group whose own donor was missing in this world and which is wearing the workbench
        /// planks instead: it renders, so nothing looks broken, and that is exactly why it is
        /// reported. classic is the earlier donor list (only if the workbench was missing as
        /// well), glow is the heartwood group, which is deliberately not a donor skin, and
        /// missing is no material at all. live=no is a renderer slot whose material has been
        /// destroyed under it, which is what a bundle unloaded at logout does.
        /// </summary>
        public static List<string> SkinReport(string name)
        {
            var lines = new List<string>();

            Tracked tracked = null;
            for (var i = Standing.Count - 1; i >= 0 && tracked == null; i--)
                if (Standing[i].Renderer != null
                    && string.Equals(Standing[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    tracked = Standing[i];

            if (tracked == null)
            {
                lines.Add("hod skin piece=" + name + " standing=no groups=0");
                return lines;
            }

            var materials = tracked.Renderer.sharedMaterials;
            var ok = 0;
            var glow = 0;
            var fallback = 0;
            var missing = 0;
            var dead = 0;

            for (var i = 0; i < tracked.Groups.Length; i++)
            {
                var group = tracked.Groups[i];
                SkinNote note;
                if (!Notes.TryGetValue(Key(group, true), out note)) note = new SkinNote();

                var live = i < materials.Length && materials[i] != null;
                if (!live) dead++;

                switch (note.Status)
                {
                    case "ok": ok++; break;
                    case "glow": glow++; break;
                    case "missing": missing++; break;
                    default: fallback++; break;
                }

                lines.Add("hod skin piece=" + name + " group=" + group + " status=" + note.Status
                          + " donor=" + note.Donor + " via=" + note.Via + " material=" + note.Material
                          + " live=" + (live ? "yes" : "no"));
            }

            lines.Add("hod skin piece=" + name + " standing=yes groups=" + tracked.Groups.Length
                      + " ok=" + ok + " glow=" + glow + " fallback=" + fallback
                      + " missing=" + missing + " dead=" + dead);
            return lines;
        }

        private static bool Matches(Material material, DonorSkin skin)
        {
            if (material == null || material.shader == null) return false;

            // Exact name, so the worn and broken variants that share a prefix are skipped.
            var name = material.name.Replace(" (Instance)", "");
            if (!string.Equals(name, skin.Material, StringComparison.Ordinal)) return false;

            var sheet = material.mainTexture;
            if (sheet == null || sheet.name != skin.Sheet || sheet.width != skin.Px) return false;

            var st = material.mainTextureScale;
            return Mathf.Abs(st.x - 1f) <= 0.001f && Mathf.Abs(st.y - 1f) <= 0.001f;
        }

        private static readonly HashSet<string> Said = new HashSet<string>();

        /// <summary>
        /// How each modern group was actually skinned in this world, for <see cref="SkinReport"/>.
        /// The log says it once per session, which is no use to a scenario that runs a world
        /// later; this is rebuilt with the cache and read on demand.
        /// </summary>
        private sealed class SkinNote
        {
            public string Donor = "none";
            public string Material = "none";
            public string Via = "none";

            /// <summary>ok, fallback (wears the workbench planks), classic, glow or missing.</summary>
            public string Status = "missing";
        }

        private static readonly Dictionary<string, SkinNote> Notes =
            new Dictionary<string, SkinNote>(StringComparer.OrdinalIgnoreCase);

        private static void SayOnce(string line)
        {
            if (Said.Add(line)) StowRuntime.Log.LogWarning(line);
        }

        /// <summary>
        /// The state of a group is kept per mode: the post and its upgrades wear the classic
        /// donors, the jib wears the workbench skins, and both are standing in one world with
        /// a group called "wood" apiece.
        /// </summary>
        private static string Key(string group, bool modern)
        {
            return modern ? "m:" + group : group;
        }

        // ------------------------------------------------------------------ re-skinning

        /// <summary>
        /// A piece that wears workbench skins, and what it needs to wear them again.
        ///
        /// Valheim 1.0 unloads the soft-reference bundles when a world is left, and a material
        /// lifted off a prefab is destroyed with its bundle. The prefab we built keeps living,
        /// holding references that are now null, so the piece would come back invisible or
        /// magenta in the second world of a session. The renderer, the mesh and the OBJ's own
        /// UVs are all ours and survive; the materials are re-borrowed and the UVs re-fitted.
        /// </summary>
        private sealed class Tracked
        {
            public string Name;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public string[] Groups;
            public Vector2[] Uv;
            public int Epoch;
        }

        private static readonly List<Tracked> Standing = new List<Tracked>();

        /// <summary>Bumped by Invalidate, so a piece knows its skins were borrowed in an earlier world.</summary>
        private static int Epoch;

        private static void Track(MeshRenderer renderer, ModelData model, string name)
        {
            Standing.Add(new Tracked
            {
                Name = name,
                Renderer = renderer,
                Mesh = model.Mesh,
                Groups = model.Groups,
                Uv = model.Mesh.uv,
                Epoch = Epoch,
            });
        }

        /// <summary>
        /// Re-borrows the skins of every tracked piece borrowed in an earlier world. Cheap
        /// when there is nothing to do, so it is safe to call from an Update. Waits for the
        /// scene and a real item database, which are what the donors are looked up through.
        /// </summary>
        public static void Refresh()
        {
            if (Standing.Count == 0) return;

            var stale = false;
            foreach (var piece in Standing) stale |= piece.Epoch != Epoch;
            if (!stale) return;

            var db = ObjectDB.instance;
            if (ZNetScene.instance == null || db == null || db.m_items == null
                || db.m_items.Count == 0) return;

            // The prop index maps names to prefabs found in an earlier world.
            PropIndex.Invalidate();

            foreach (var piece in Standing)
            {
                if (piece.Epoch == Epoch) continue;
                piece.Epoch = Epoch;

                if (piece.Renderer == null || piece.Mesh == null) continue;

                piece.Renderer.sharedMaterials = SkinsFor(piece.Groups, true);
                piece.Mesh.uv = piece.Uv;
                Remap(piece.Mesh, piece.Groups, true);
            }

            StowRuntime.Log.LogInfo("Re-skinned the workbench-skinned pieces for this world.");
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
                return (StowConfig.PostGlowDonors.Value ?? "").Split(',');

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

        private sealed class Island
        {
            public readonly List<int> Verts = new List<int>();
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static long Quantise(Vector3 v)
        {
            long x = Mathf.RoundToInt(v.x * 1000f), y = Mathf.RoundToInt(v.y * 1000f),
                 z = Mathf.RoundToInt(v.z * 1000f);
            return (x & 0x1FFFFF) | ((y & 0x1FFFFF) << 21) | ((z & 0x1FFFFF) << 42);
        }

        private static float Hash01(int a, int b)
        {
            unchecked
            {
                var h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }

        /// <summary>
        /// The island a part is cut from: one the part fits inside at full density, chosen by
        /// the part's hash so neighbours differ, and only if none is big enough the one it
        /// fits best.
        ///
        /// Choosing blindly was the bug. A 2.7 m mast timber that landed in a small plank
        /// island was scaled down to fit it, and the workbench's density of about 40 texels
        /// a metre became 14: a part with grain three times coarser than the bench beside it.
        /// </summary>
        private static Rect PickRect(Rect[] choices, float hash, float needS, float needT)
        {
            var fitting = 0;
            foreach (var c in choices)
                if (c.width >= needS && c.height >= needT) fitting++;

            if (fitting > 0)
            {
                var pick = Mathf.Min(fitting - 1, (int)(hash * fitting));
                foreach (var c in choices)
                {
                    if (c.width < needS || c.height < needT) continue;
                    if (pick-- == 0) return c;
                }
            }

            var best = choices[0];
            var bestFit = -1f;
            foreach (var c in choices)
            {
                var fit = Mathf.Min(c.width / Mathf.Max(needS, 1e-5f), c.height / Mathf.Max(needT, 1e-5f));
                if (fit <= bestFit) continue;
                bestFit = fit;
                best = c;
            }
            return best;
        }

        private static void Project(Vector3 v, int axis, out float p, out float q)
        {
            if (axis == 0) { p = v.y; q = v.z; }
            else if (axis == 1) { p = v.x; q = v.z; }
            else { p = v.x; q = v.y; }
        }

        /// <summary>
        /// Maps the groups skinned from the workbench atlas by where their triangles are,
        /// ignoring the UVs in the OBJ.
        ///
        /// Those UVs are Blender primitive defaults, where a 3cm strap and a 1.2m plank
        /// claim the same patch of texture. Rather than re-export the meshes, the unwrap is
        /// done here from positions: parts are found by welding vertices that sit at the
        /// same point, each part is cube-projected one island per facing, scaled to
        /// TexelsPerMetre, its longer side laid along the sheet vertical because that is
        /// the way the bench grain runs, and the island is placed inside the group rect and
        /// never allowed to leave it (clamped by scaling down, not wrapped). Where in the
        /// rect an island lands is a hash of which part it is, so neighbouring boards do
        /// not repeat each other.
        ///
        /// Returns how many groups were placed.
        /// </summary>
        private static int FitMetric(Mesh mesh, string[] groups, int count, Vector2[] uv, bool[] done,
                                     bool modern)
        {
            var any = false;
            for (var i = 0; i < count; i++) any |= Metric.Contains(Key(groups[i], modern));
            if (!any) return 0;

            var verts = mesh.vertices;
            if (verts == null || verts.Length != uv.Length) return 0;

            var parent = new int[verts.Length];
            for (var i = 0; i < parent.Length; i++) parent[i] = i;

            var byPoint = new Dictionary<long, int>();
            for (var i = 0; i < verts.Length; i++)
            {
                var key = Quantise(verts[i]);
                int other;
                if (byPoint.TryGetValue(key, out other)) parent[Find(parent, i)] = Find(parent, other);
                else byPoint[key] = i;
            }

            // A triangle also welds its own corners: bevel vertices can differ in the third
            // decimal and the part must still come out as one part.
            for (var g = 0; g < count; g++)
            {
                if (!Metric.Contains(Key(groups[g], modern))) continue;

                var tris = mesh.GetTriangles(g);
                for (var t = 0; t + 2 < tris.Length; t += 3)
                {
                    parent[Find(parent, tris[t + 1])] = Find(parent, tris[t]);
                    parent[Find(parent, tris[t + 2])] = Find(parent, tris[t]);
                }
            }

            var placed = 0;

            for (var g = 0; g < count; g++)
            {
                if (!Metric.Contains(Key(groups[g], modern))) continue;

                Rect[] choices;
                int px;
                if (!MetricRects.TryGetValue(Key(groups[g], modern), out choices)
                    || !TexPx.TryGetValue(Key(groups[g], modern), out px))
                    continue;

                var tris = mesh.GetTriangles(g);
                var islands = new Dictionary<long, Island>();
                var taken = new HashSet<int>();

                for (var t = 0; t + 2 < tris.Length; t += 3)
                {
                    var a = tris[t];
                    var b = tris[t + 1];
                    var c = tris[t + 2];
                    if (done[a] && done[b] && done[c]) continue;

                    var n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                    var ax = Mathf.Abs(n.x) >= Mathf.Abs(n.y) && Mathf.Abs(n.x) >= Mathf.Abs(n.z)
                        ? 0 : (Mathf.Abs(n.y) >= Mathf.Abs(n.z) ? 1 : 2);

                    var id = ((long)Find(parent, a) << 2) | (long)ax;
                    Island isl;
                    if (!islands.TryGetValue(id, out isl)) islands[id] = isl = new Island();

                    var corners = new[] { a, b, c };
                    foreach (var v in corners)
                    {
                        if (done[v] || !taken.Add(v)) continue;
                        isl.Verts.Add(v);
                    }
                }

                var scale = TexelsPerMetre / Mathf.Max(1, px);

                foreach (var pair in islands)
                {
                    var isl = pair.Value;
                    if (isl.Verts.Count == 0) continue;
                    var ax = (int)(pair.Key & 3);

                    var minA = float.MaxValue; var maxA = float.MinValue;
                    var minB = float.MaxValue; var maxB = float.MinValue;
                    foreach (var v in isl.Verts)
                    {
                        float p, q;
                        Project(verts[v], ax, out p, out q);
                        minA = Mathf.Min(minA, p); maxA = Mathf.Max(maxA, p);
                        minB = Mathf.Min(minB, q); maxB = Mathf.Max(maxB, q);
                    }

                    // The longer side runs along the sheet vertical, which is the grain.
                    var swap = (maxA - minA) > (maxB - minB);
                    var extS = swap ? maxB - minB : maxA - minA;
                    var extT = swap ? maxA - minA : maxB - minB;

                    float k;

                    var seed = (int)(pair.Key & 0x7FFFFFFF);

                    // A part keeps one island for all its faces, and neighbouring parts
                    // differ: that is the board-to-board variation the bench has.
                    var rect = PickRect(choices, Hash01(seed >> 2, 7), extS * scale, extT * scale);
                    if (Stretch.Contains(Key(groups[g], modern)))
                    {
                        // A cutout leaf: the sheet's silhouette must fill the part, so the
                        // island is stretched to the rect with its long side up.
                        foreach (var v in isl.Verts)
                        {
                            float p0, q0;
                            Project(verts[v], ax, out p0, out q0);
                            var sOff0 = swap ? q0 - minB : p0 - minA;
                            var tOff0 = swap ? p0 - minA : q0 - minB;
                            uv[v] = new Vector2(rect.x + sOff0 / Mathf.Max(extS, 1e-5f) * rect.width,
                                                rect.y + tOff0 / Mathf.Max(extT, 1e-5f) * rect.height);
                            done[v] = true;
                        }
                        continue;
                    }

                    var fit0 = Mathf.Min(1f, Mathf.Min(
                        rect.width / Mathf.Max(extS * scale, 1e-5f),
                        rect.height / Mathf.Max(extT * scale, 1e-5f)));
                    k = scale * fit0;
                    var ox = rect.x + Hash01(seed, 1) * Mathf.Max(0f, rect.width - extS * k);
                    var oy = rect.y + Hash01(seed, 2) * Mathf.Max(0f, rect.height - extT * k);

                    foreach (var v in isl.Verts)
                    {
                        float p, q;
                        Project(verts[v], ax, out p, out q);
                        var sOff = swap ? q - minB : p - minA;
                        var tOff = swap ? p - minA : q - minB;
                        uv[v] = new Vector2(ox + sOff * k, oy + tOff * k);
                        done[v] = true;
                    }
                }

                placed++;
            }

            return placed;
        }

        /// <summary>
        /// Squeezes each submesh's UVs into its material's slice of the atlas.
        ///
        /// Wrapped first, then mapped: the mesh is unwrapped at world scale so its
        /// coordinates run well past 1, and mapping those directly would walk straight
        /// out of the region again. Repeat brings them back into 0..1 so the tiling
        /// survives, then the rect places that tile where the texture actually is.
        /// </summary>
        public static void Remap(Mesh mesh, string[] groups, bool modern = false)
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

            moved += FitMetric(mesh, groups, count, uv, done, modern);

            for (var i = 0; i < count; i++)
            {
                Rect rect;
                if (Metric.Contains(Key(groups[i], modern))) continue;
                if (!Atlas.TryGetValue(Key(groups[i], modern), out rect)) continue;
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
            StowRuntime.Log.LogInfo("Remapped UVs into the atlas for " + moved + " group(s).");
        }

        /// <summary>
        /// Boxes from the sidecar, replacing whatever shape the donor had. A barrel's
        /// capsule around a square bin leaves you bumping into air at the corners.
        /// </summary>
        private static void ReplaceColliders(GameObject prefab, string path)
        {
            if (!File.Exists(path))
            {
                StowRuntime.Log.LogWarning(
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

            StowRuntime.Log.LogInfo("Post collision: " + boxes.Count + " boxes.");
        }
    }
}
