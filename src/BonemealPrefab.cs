using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// Ground bone, and the one thing in this mod that is about the farm rather than the
    /// forest. It belongs beside Furrow: sow a rank of seeds in one click, then push them
    /// along.
    ///
    /// A Consumable rather than a Material, because Consumable is the item type the hotbar
    /// will actually *use* when you press its key - a Material sits there and does nothing.
    /// That normally drags an item into the food system, and here it does not: Player's
    /// CanConsumeItem only reaches CanEat when m_food is above zero, so a consumable with no
    /// food values never touches a food slot, never counts toward the three, and never shows
    /// up on the HUD. That one condition is the whole reason this can be a right-click item
    /// without pretending to be a meal.
    /// </summary>
    internal static class BonemealPrefab
    {
        /// <summary>
        /// The network identity, and what recipes name. ObjectDB keys on this, so it is
        /// permanent in the same way every other prefab name here is permanent.
        /// </summary>
        public const string Name = "GroveBonemeal";

        private static GameObject _prefab;
        private static GameObject _holder;
        private static bool _recipeAdded;

        /// <summary>
        /// Asked of the live ObjectDB rather than of a static field. Logging out to the menu
        /// and back in builds a new ObjectDB, and an answer cached in a field would say yes
        /// while the new one has never heard of this item.
        /// </summary>
        public static bool Ready
        {
            get
            {
                return ObjectDB.instance != null
                       && ObjectDB.instance.GetItemPrefab(Name) != null;
            }
        }

        public static bool Register()
        {
            if (Ready && _recipeAdded) return true;
            if (ZNetScene.instance == null || ObjectDB.instance == null) return false;

            if (_prefab == null)
            {
                _prefab = Build();
                if (_prefab == null) return false;
            }

            AddToObjectDB();
            AddToScene();
            AddRecipe();

            return Ready;
        }

        /// <summary>Called when ObjectDB is rebuilt, so the recipe is re-added to the new one.</summary>
        public static void Invalidate()
        {
            _recipeAdded = false;
        }

        // ------------------------------------------------------------------ building

        private static GameObject Build()
        {
            var source = Donor();
            if (source == null) return null;

            if (_holder == null)
            {
                _holder = new GameObject("GroveBonemealHolder");
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

            var drop = clone.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
            {
                GrovePlugin.Log.LogError("Donor " + source.name + " has no usable ItemDrop.");
                return null;
            }

            // Instantiate deep-copies serialized fields and SharedData is [Serializable], so
            // this is our own copy. Writing to the donor's would rename every bone fragment
            // in the world.
            var shared = drop.m_itemData.m_shared;

            shared.m_name = GroveConfig.BonemealName.Value;
            shared.m_description = "Bone ground fine. Worked into a growing crop it makes "
                                   + "the harvest richer, and the tending teaches.";

            // Consumable, and deliberately with no food values at all - see the class note.
            shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
            shared.m_food = 0f;
            shared.m_foodStamina = 0f;
            shared.m_foodEitr = 0f;
            shared.m_foodRegen = 0f;
            shared.m_foodBurnTime = 0f;
            shared.m_consumeStatusEffect = null;

            shared.m_maxStackSize = Mathf.Max(1, GroveConfig.BonemealStack.Value);
            shared.m_weight = 0.2f;
            shared.m_teleportable = true;
            shared.m_questItem = false;

            // Both optional. The model pass has not happened yet, and until it does the item
            // wears its donor's mesh and icon rather than failing to exist - a bag of ground
            // bone that looks like bone fragments is wrong, not broken, and being able to
            // play with the mechanic before the art is the point of loading these at runtime.
            // Blank model means the donor's own sack, deliberately and quietly - the
            // warning only earns its place when a configured override fails to load.
            if (!string.IsNullOrEmpty(GroveConfig.BonemealModel.Value))
            {
                var directory = Path.GetDirectoryName(typeof(BonemealPrefab).Assembly.Location);
                var model = ObjMesh.Load(Path.Combine(directory, GroveConfig.BonemealModel.Value));
                if (model != null) Visual(clone, model);
                else GrovePlugin.LogOnce("No " + GroveConfig.BonemealModel.Value
                                         + " beside the dll - " + Name + " keeps the donor's mesh.");
            }

            Tint(clone);

            var icon = Icons.Load(GroveConfig.BonemealIcon.Value, Name);
            if (icon != null) shared.m_icons = new[] { icon };

            drop.m_itemData.m_stack = 1;
            drop.m_itemData.m_dropPrefab = clone;

            GrovePlugin.Log.LogInfo("Built " + Name + " from " + source.name + ".");
            return clone;
        }

        private static GameObject Donor()
        {
            var scene = ZNetScene.instance;

            foreach (var name in new[] { GroveConfig.BonemealDonor.Value, "BarleyFlour", "Wood" })
            {
                if (string.IsNullOrEmpty(name)) continue;

                var found = scene.GetPrefab(name);
                if (found != null) return found;

                GrovePlugin.LogOnce("Bonemeal donor '" + name + "' does not exist.");
            }

            return null;
        }

        /// <summary>
        /// Swap the donor's mesh for ours, leaving every component alone. Same approach as
        /// the heartwood: the clone is kept for its ItemDrop, Rigidbody, colliders and
        /// float-in-water behaviour, and only what you look at changes.
        /// </summary>
        /// <summary>
        /// Bone-ivory into the sack, on OUR OWN material copies - never the donor's
        /// shared material, which every barley flour in the world draws from. The
        /// copies belong to this prefab; its instances share them with each other
        /// and with nothing else. Vanilla differentiates the meads exactly this way:
        /// one jug, tinted apart.
        /// </summary>
        private static void Tint(GameObject clone)
        {
            var spec = (GroveConfig.BonemealTint.Value ?? "").Trim();
            if (spec.Length == 0) return;

            var parts = spec.Split(',');
            if (parts.Length < 3) return;

            float r, g, b;
            if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out r)
                || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out g)
                || !float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out b))
                return;

            var tint = new Color(r, g, b, 1f);
            foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;

                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    materials[i] = new Material(materials[i]);

                    // Say what we are tinting, once: the first attempt changed
                    // nothing visible and gave no reason, which is exactly the
                    // silent-skip this mod keeps producing. The property list is
                    // the fact that decides the next move.
                    GrovePlugin.LogOnce("Bonemeal tint: material '" + materials[i].name
                        + "' shader '" + materials[i].shader.name
                        + "' _Color=" + materials[i].HasProperty("_Color")
                        + " _MainColor=" + materials[i].HasProperty("_MainColor"));

                    if (materials[i].HasProperty("_Color"))
                        materials[i].color = materials[i].color * tint;
                    else if (materials[i].HasProperty("_MainColor"))
                        materials[i].SetColor("_MainColor",
                            materials[i].GetColor("_MainColor") * tint);
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static Material[] FlatMaterials(string[] groups)
        {
            var shader = Shader.Find("Standard");
            var palette = new System.Collections.Generic.Dictionary<string, Color>(
                System.StringComparer.OrdinalIgnoreCase)
            {
                { "cloth", new Color(0.42f, 0.34f, 0.24f) },
                { "rope",  new Color(0.55f, 0.44f, 0.28f) },
                { "meal",  new Color(0.78f, 0.74f, 0.64f) },
            };

            var materials = new Material[groups.Length];
            for (var i = 0; i < groups.Length; i++)
            {
                Color colour;
                if (!palette.TryGetValue(groups[i], out colour))
                    colour = new Color(0.5f, 0.45f, 0.38f);

                var material = new Material(shader);
                material.color = colour;
                material.SetFloat("_Glossiness", 0f);
                materials[i] = material;
            }
            return materials;
        }

        private static void Visual(GameObject clone, ModelData model)
        {
            // The heartwood pattern, and for the heartwood's reasons: swapping only
            // the mesh keeps the donor's material, whose normal map sampled through
            // our UVs lit the dropped sack solid black. Components stripped, never
            // their GameObjects - the donor's collider lives beside its renderer,
            // and taking the object drops the item through the floor.
            foreach (var renderer in clone.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null) continue;

                var donorFilter = renderer.GetComponent<MeshFilter>();
                if (donorFilter != null) Object.DestroyImmediate(donorFilter);
                Object.DestroyImmediate(renderer);
            }

            var visual = new GameObject("bonemeal_visual");
            visual.transform.SetParent(clone.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = model.Mesh;

            // Flat colours, not borrowed textures - "to have it look like the icon",
            // his words, and the icon IS flat paint. Three donors were tried and each
            // failed its own way: leather scraps rendered transparent (alpha-cutout
            // shader), the deer rug went orange with black border chunks, and no rug
            // was ever going to look like posterised sackcloth. These are the design
            // script's own RGBs, so the item matches its icon by construction.
            visual.AddComponent<MeshRenderer>().sharedMaterials =
                FlatMaterials(model.Groups);

            // The heartwood's other lesson, kept as insurance: an item with no
            // collider falls through the world without a message.
            if (clone.GetComponentInChildren<Collider>(true) == null && model.Mesh != null)
            {
                var bounds = model.Mesh.bounds;
                var box = clone.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }
        }

        // ------------------------------------------------------------------ registration

        /// <summary>
        /// Into ObjectDB, and then its lookup tables rebuilt. UpdateRegisters is private and
        /// builds m_itemByHash once, so adding to m_items alone leaves the item unfindable
        /// by name - which is exactly what a recipe does when it resolves its ingredients.
        /// </summary>
        private static void AddToObjectDB()
        {
            var db = ObjectDB.instance;
            if (db == null || _prefab == null) return;
            if (db.GetItemPrefab(Name) != null) return;

            db.m_items.Add(_prefab);

            try
            {
                AccessTools.Method(typeof(ObjectDB), "UpdateRegisters").Invoke(db, null);
            }
            catch (System.Exception e)
            {
                GrovePlugin.Log.LogError("Could not refresh ObjectDB for " + Name + ": "
                                         + e.Message);
            }
        }

        private static void AddToScene()
        {
            var scene = ZNetScene.instance;
            if (scene == null || _prefab == null) return;
            if (scene.GetPrefab(Name) != null) return;

            scene.m_prefabs.Add(_prefab);

            // The list alone is not enough: m_namedPrefabs is built in Awake and never
            // rebuilt, so a prefab missing from the dictionary is a prefab ZNetScene cannot
            // resolve - and an unresolvable prefab has its ZDOs discarded rather than erroring.
            var named = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs")
                                   .GetValue(scene) as System.Collections.Generic.Dictionary<int, GameObject>;
            if (named != null) named[Name.GetStableHashCode()] = _prefab;
        }

        // ------------------------------------------------------------------ the recipe

        /// <summary>
        /// Bone fragments and entrails at a workbench.
        ///
        /// Entrails rather than bone alone, so it is a craft rather than a free conversion of
        /// the one drop everybody is drowning in by the Black Forest. It still lands early -
        /// which is right, because the thing it speeds up is the part of farming you do at
        /// the very start.
        /// </summary>
        private static void AddRecipe()
        {
            if (_recipeAdded) return;

            // Unconditional: the bone mill stayed on its branch this version, so the
            // workbench recipe IS the source. When the mill ships, this is where its
            // gate goes back.

            var db = ObjectDB.instance;
            if (db == null || _prefab == null) return;

            var drop = _prefab.GetComponent<ItemDrop>();
            if (drop == null) return;

            // Ask the live database rather than a flag: ObjectDB is rebuilt on every world
            // load, and a flag would report the recipe present in a database that has never
            // seen it.
            var recipeName = "Recipe_" + Name;
            foreach (var existing in db.m_recipes)
            {
                if (existing != null && existing.name == recipeName)
                {
                    _recipeAdded = true;
                    return;
                }
            }

            var requirements = Requirements(db);
            if (requirements == null) return;

            var recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = recipeName;
            recipe.m_item = drop;
            recipe.m_amount = Mathf.Max(1, GroveConfig.BonemealYield.Value);
            recipe.m_resources = requirements;
            recipe.m_enabled = true;
            recipe.m_minStationLevel = 1;

            var station = db.GetItemPrefab(GroveConfig.BonemealStation.Value);
            recipe.m_craftingStation = FindStation(GroveConfig.BonemealStation.Value);
            if (recipe.m_craftingStation == null)
                GrovePlugin.LogOnce("Crafting station '" + GroveConfig.BonemealStation.Value
                                    + "' not found - bonemeal will be craftable by hand.");

            db.m_recipes.Add(recipe);
            _recipeAdded = true;

            GrovePlugin.Log.LogInfo("Added the recipe for " + Name + ".");
        }

        /// <summary>
        /// Parses the configured cost, as Item:Amount pairs. A name that does not resolve is
        /// logged and the whole recipe abandoned rather than silently cheapened - an
        /// ingredient quietly missing is a recipe that costs less than it says it does.
        /// </summary>
        private static Piece.Requirement[] Requirements(ObjectDB db)
        {
            var parts = GroveConfig.BonemealCost.Value.Split(',');
            var list = new System.Collections.Generic.List<Piece.Requirement>();

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0) continue;

                var split = trimmed.Split(':');
                int amount;
                if (split.Length != 2 || !int.TryParse(split[1].Trim(), out amount))
                {
                    GrovePlugin.LogOnce("Cannot read bonemeal cost '" + trimmed
                                        + "' - expected Item:Amount.");
                    return null;
                }

                var item = db.GetItemPrefab(split[0].Trim());
                if (item == null)
                {
                    GrovePlugin.LogOnce("Bonemeal ingredient '" + split[0].Trim()
                                        + "' does not exist - recipe not added.");
                    return null;
                }

                var drop = item.GetComponent<ItemDrop>();
                if (drop == null) return null;

                list.Add(new Piece.Requirement { m_resItem = drop, m_amount = amount });
            }

            return list.Count > 0 ? list.ToArray() : null;
        }

        private static CraftingStation FindStation(string name)
        {
            if (string.IsNullOrEmpty(name) || ZNetScene.instance == null) return null;

            var prefab = ZNetScene.instance.GetPrefab(name);
            return prefab == null ? null : prefab.GetComponent<CraftingStation>();
        }
    }
}
