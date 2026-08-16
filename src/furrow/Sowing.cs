using HarmonyLib;
using UnityEngine;

namespace Furrow
{
    /// <summary>
    /// The whole mod: when the game plants a seed, plant the rest of the rank around it.
    ///
    /// This hangs off a postfix on Player.TryPlacePiece rather than replacing any of the
    /// placement code, so the seed under the cursor is placed by vanilla, validated by
    /// vanilla, paid for by vanilla and credited by vanilla. Everything here is additional.
    /// </summary>
    internal static class Sowing
    {
        private static readonly AccessTools.FieldRef<Player, GameObject> GhostRef =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        private static readonly AccessTools.FieldRef<Player, PieceTable> BuildPiecesRef =
            AccessTools.FieldRefAccess<Player, PieceTable>("m_buildPieces");

        // Exactly the mask Plant.HaveGrowSpace builds. Copied rather than referenced because
        // the game's copy is a private static on Plant that is only filled in on first use.
        private static int _spaceMask;
        private static readonly Collider[] Overlaps = new Collider[32];

        /// <summary>Runtime count the player has dialled in, before the skill cap applies.</summary>
        private static int _wanted = int.MaxValue;

        private static SowShape _shape;
        private static bool _shapeLoaded;

        public static SowShape Shape
        {
            get
            {
                if (!_shapeLoaded) { _shape = FurrowConfig.Shape.Value; _shapeLoaded = true; }
                return _shape;
            }
        }

        // ------------------------------------------------------------------ input

        public static void HandleKeys(Player player)
        {
            if (player == null) return;

            if (Input.GetKeyDown(FurrowConfig.ShapeKey.Value))
            {
                _shape = Shape == SowShape.Row ? SowShape.Circle : SowShape.Row;
                _shapeLoaded = true;
                player.Message(MessageHud.MessageType.TopLeft, "Furrow: " + _shape.ToString().ToLower());
                return;
            }

            var up = Input.GetKeyDown(FurrowConfig.IncreaseKey.Value);
            var down = Input.GetKeyDown(FurrowConfig.DecreaseKey.Value);
            if (!up && !down) return;

            // The cap is read here only so the message can say something true. The real cap
            // is applied at sowing time, because the skill can rise between two keypresses.
            var cap = FurrowConfig.AllowedFor(player.GetSkillFactor(Skills.SkillType.Farming) * 100f, false);
            var current = Mathf.Clamp(_wanted == int.MaxValue ? cap : _wanted, 1, cap);

            _wanted = Mathf.Clamp(current + (up ? 1 : -1), 1, cap);
            player.Message(MessageHud.MessageType.TopLeft, "Furrow: " + _wanted + " of " + cap);
        }

        // ------------------------------------------------------------------ sowing

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        private static void SowTheRest(Player __instance, Piece piece, bool __result)
        {
            if (!__result || !FurrowConfig.Enabled.Value) return;
            if (piece == null) return;

            var plant = piece.GetComponent<Plant>();
            if (plant == null) return;

            var ghost = GhostRef(__instance);
            if (ghost == null) return;

            var table = BuildPiecesRef(__instance);

            // Vanilla credits the table's own skill, not Farming by name, and so does this.
            // If a mod ever puts plants on a table with a different skill, the extra seeds
            // train whatever the single seed would have trained.
            var skill = table != null && table.m_skill != Skills.SkillType.None
                ? table.m_skill
                : Skills.SkillType.Farming;

            var isTree = IsTree(plant);
            var level = __instance.GetSkillFactor(skill) * 100f;
            var cap = FurrowConfig.AllowedFor(level, isTree);
            var wanted = Mathf.Clamp(_wanted == int.MaxValue ? cap : _wanted, 1, cap);
            if (wanted <= 1) return;

            var affordable = Affordable(__instance, piece, wanted - 1);
            if (affordable <= 0) return;

            var centre = ghost.transform.position;
            var rot = ghost.transform.rotation;

            // The plant's own radius, which is the distance the game itself refuses to plant
            // inside. Using it means carrots pack tight and firs stand well apart without a
            // single number in config trying to suit both.
            var step = Mathf.Max(0.1f, plant.m_growRadius * 2f * Mathf.Max(0.1f, FurrowConfig.Spacing.Value));

            var offsets = Layout.Offsets(Shape, wanted, step, rot);

            var sown = 0;
            foreach (var offset in offsets)
            {
                if (sown >= affordable) break;

                var target = centre + offset;
                if (!Ground(ref target)) continue;
                if (!CanSow(plant, target)) continue;

                __instance.PlacePiece(plant.GetComponent<Piece>(), target, rot, doAttack: false);
                __instance.RaiseSkill(skill);
                sown++;
            }

            if (sown > 0)
                __instance.ConsumeResources(piece.m_resources, 0, -1, sown);

            if (FurrowConfig.Verbose.Value)
                FurrowPlugin.Log.LogInfo(
                    "Sowed " + sown + " extra " + (isTree ? "sapling" : "seed") + "(s) around "
                    + piece.name + " at skill " + level.ToString("F1") + " (cap " + cap + ").");
        }

        /// <summary>
        /// A sapling is a plant that grows into something with a TreeBase. Read off the
        /// component rather than a name list, so a modded sapling is covered the day it is
        /// added and a renamed vanilla one never falls through.
        /// </summary>
        private static bool IsTree(Plant plant)
        {
            if (plant.m_grownPrefabs == null) return false;

            foreach (var grown in plant.m_grownPrefabs)
            {
                if (grown == null) continue;
                if (grown.GetComponent<TreeBase>() != null) return true;
                if (grown.GetComponentInChildren<TreeBase>(true) != null) return true;
            }

            return false;
        }

        /// <summary>
        /// How many extra seeds the pack can pay for, leaving one set behind.
        ///
        /// The reserve matters: at postfix time vanilla has placed its seed but has not yet
        /// consumed anything for it - Player.Update does that after TryPlacePiece returns.
        /// Spending down to zero here would leave that consume to take a seed that is no
        /// longer there.
        /// </summary>
        private static int Affordable(Player player, Piece piece, int wanted)
        {
            if (piece.m_resources == null || piece.m_resources.Length == 0) return wanted;

            var inventory = player.GetInventory();
            if (inventory == null) return 0;

            var affordable = wanted;

            foreach (var req in piece.m_resources)
            {
                if (req == null || req.m_resItem == null || req.m_amount <= 0) continue;

                var held = inventory.CountItems(req.m_resItem.m_itemData.m_shared.m_name);
                var spare = held / req.m_amount - 1;   // -1 is vanilla's pending consume
                if (spare < affordable) affordable = spare;
            }

            return Mathf.Max(0, affordable);
        }

        /// <summary>Drop the position onto solid ground, or refuse it if there is none.</summary>
        private static bool Ground(ref Vector3 position)
        {
            if (ZoneSystem.instance == null) return false;

            if (!ZoneSystem.instance.GetSolidHeight(position, out var height, out _, out _))
                return false;

            position.y = height;
            return true;
        }

        /// <summary>
        /// The placement rules, for a position no ray was ever cast at.
        ///
        /// Player.UpdatePlacementGhost cannot be reused for this: it works from a camera ray
        /// through PieceRayTest, so it can only ever answer for the point under the cursor.
        /// These are therefore hand-checked, and every one of them mirrors the game's own -
        /// biome and cultivated ground read exactly as Plant.UpdateHealth reads them, and the
        /// space test is Plant.HaveGrowSpace with the same mask and the same radius.
        ///
        /// Roof, heat and cold are deliberately absent. Vanilla does not block placement on
        /// any of them either; they are growth statuses a planted seed reports for itself, so
        /// leaving them out is what keeps a Furrow-sown plant behave identically to a
        /// hand-sown one that was put somewhere shady.
        /// </summary>
        private static bool CanSow(Plant plant, Vector3 position)
        {
            if (Location.IsInsideNoBuildLocation(position)) return false;

            // flash: false - the ward effect firing twenty times in one click is a strobe.
            if (!PrivateArea.CheckAccess(position, 0f, false)) return false;

            var heightmap = Heightmap.FindHeightmap(position);
            if (heightmap == null) return false;

            var biome = heightmap.GetBiome(position);
            if ((biome & plant.m_biome) == 0) return false;

            if (plant.m_needCultivatedGround && !heightmap.IsCultivated(position)) return false;

            return HaveGrowSpace(plant, position);
        }

        private static bool HaveGrowSpace(Plant plant, Vector3 position)
        {
            if (_spaceMask == 0)
                _spaceMask = LayerMask.GetMask(
                    "Default", "static_solid", "Default_small", "piece", "piece_nonsolid");

            var hits = Physics.OverlapSphereNonAlloc(position, plant.m_growRadius, Overlaps, _spaceMask);

            for (var i = 0; i < hits; i++)
            {
                // Vanilla's rule exactly: anything that is not a plant blocks, and a plant
                // blocks only while it is itself healthy. A dying one is not competition.
                var other = Overlaps[i].GetComponent<Plant>();
                if (other == null || other.GetStatus() == Plant.Status.Healthy) return false;
            }

            return true;
        }
    }
}
