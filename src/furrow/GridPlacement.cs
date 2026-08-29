using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Furrow
{
    /// <summary>
    /// The grid he actually asked for: the cultivator's ghost snaps so hand-placed
    /// plants land in rows and columns.
    ///
    /// The first reading of "from level 10 farming there is a grid" was a grid-shaped
    /// MULTI-sow - one press planting a square - and he struck it the moment he met
    /// it: "when planting carrot it plants 3. i just want a grid so i can place them
    /// myself in proper grid rows and columns." So sowing is one seed per press, as
    /// vanilla, and the skill unlock is alignment: from GridLevel up, the ghost pulls
    /// onto a lattice anchored on a nearby plant of the same crop, spaced by the
    /// plant's own grow radius. The first plant of a bed goes wherever you like and
    /// becomes the anchor; every later one clicks into its rows. No nearby kin, no
    /// snap - so the feature never fights you on a fresh patch.
    ///
    /// World-aligned axes rather than anchor-rotated: rows running north-south are
    /// predictable from any approach angle, which is what a grid is for.
    ///
    /// Two rules here are the fix for "rows drift out of line when planting", which
    /// only ever showed on the live server. With world axes the lattice's PHASE is
    /// the anchor's position mod the step, so every distinct free-placed plant seeds
    /// a different phase - and the first version re-picked "nearest plant" every
    /// frame, shifting the whole grid by the phase difference each time the anchor
    /// flipped to an off-lattice plant. Worse, it matched the sapling prefab only:
    /// on a server crops have time to GROW, and a grown carrot is a different prefab
    /// carrying Pickable, not Plant - so replanting a half-harvested field found no
    /// kin at all, free-placed, and every new row seeded its own phase. Hence:
    ///   - the anchor is HELD once found, and only re-derived when it falls out of
    ///     range, the crop changes, or the tool goes away - one bed, one phase;
    ///   - kin is the crop, not the prefab: the sapling AND everything in its
    ///     m_grownPrefabs, which stand exactly where the sapling did because
    ///     Plant.Grow spawns them in place.
    /// (FarmGrid and PlantEasily solve the same drift with on-grid pair detection
    /// and snap hysteresis; the held anchor buys the same stability inside this
    /// design's world-aligned axes without either.)
    ///
    /// All three properties of a lattice are now the player's, because "it does not
    /// line up with my build" is not answerable by any default. Its SPACING is
    /// GridCell, absolute metres, overriding the crop's own grow radius - which is
    /// per-crop and so can never match a floor. Its ANGLE is GridAngle, for a
    /// building that does not sit square to the world. And its ORIGIN is GridPinKey:
    /// anchoring on the nearest kin is exactly right for extending an existing bed
    /// and cannot be right for starting one, since the phase then comes from wherever
    /// the first plant happened to land. Pin a corner against the floor instead. A
    /// pin outranks the found anchor and survives a change of crop, so one pinned bed
    /// takes carrots and turnips in the same rows.
    /// </summary>
    [HarmonyPatch]
    internal static class GridPlacement
    {
        private static readonly AccessTools.FieldRef<Player, GameObject> GhostRef =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        // The held lattice phase. A position, deliberately not the plant itself -
        // the phase stays valid after the anchor plant is harvested or destroyed,
        // and a Vector3 cannot become a dead UnityEngine.Object mid-frame.
        private static Vector3? _anchor;
        private static string _anchorCrop;

        // A pinned phase outranks the found one and survives a change of crop, because
        // it is about the ground rather than about the plant: you pin a corner of the
        // bed you are laying out, then plant carrots and turnips into the same rows.
        // Not held across a session - a pin is for the bed being worked on.
        private static Vector3? _pin;

        // Kin lookup per crop, so the grown-prefab names are not re-read per frame.
        private static string _kinCrop;
        private static readonly HashSet<string> _kin = new HashSet<string>();

        private const float SearchRadius = 4f;   // past that you are starting a new bed
        private const float HoldRadius = 8f;     // hold the phase across a whole bed:
                                                 // re-picking at the search edge is what
                                                 // let the phase churn in the first place

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static void Snap(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;

            var ghost = GhostRef(__instance);
            if (ghost == null || !ghost.activeSelf)
            {
                // Tool away is the end of the planting sweep. Dropping the anchor
                // here is safe because every plant this grid placed shares its
                // phase, so the re-pick on the next sweep lands on the same lattice.
                _anchor = null;
                GridPreview.Hide();
                return;
            }

            Plant ghostPlant;
            if (!ghost.TryGetComponent(out ghostPlant)) { GridPreview.Hide(); return; }

            // The room ring first, and outside every gate below it. Whether an oak
            // will fit is worth answering at Farming 0 with the grid switched off -
            // it is about the seed being spent, not about the rows being tidy.
            GridPreview.Ring(ghost.transform.position, ghostPlant,
                             Room.Free(ghost.transform.position, ghostPlant));

            if (!FurrowConfig.GridEnabled.Value) { GridPreview.HideGrid(); return; }

            if (__instance.GetSkillFactor(Skills.SkillType.Farming) * 100f
                < FurrowConfig.GridLevel.Value) { GridPreview.HideGrid(); return; }

            // The crop's own grow radius is the tightest spacing that always takes, and
            // it differs per crop - right for a bed of one thing, wrong for lining rows
            // up with a floor, which is why an absolute override exists beside it.
            var cell = FurrowConfig.GridCell.Value;
            var step = cell > 0f
                ? Mathf.Max(0.1f, cell)
                : Mathf.Max(0.1f, ghostPlant.m_growRadius * 2f
                                  * Mathf.Max(0.1f, FurrowConfig.Spacing.Value));

            var crop = Utils.GetPrefabName(ghost.name);
            var at = ghost.transform.position;

            Pin(__instance, at);
            Turn(__instance);

            Vector3 anchor;
            if (_pin.HasValue)
            {
                anchor = _pin.Value;
            }
            else
            {
                if (_anchorCrop != crop
                    || (_anchor.HasValue
                        && FlatSqr(_anchor.Value, at) > HoldRadius * HoldRadius))
                    _anchor = null;

                if (!_anchor.HasValue)
                {
                    // No kin in reach, so the bed starts HERE - the lattice is born
                    // under the cursor rather than not at all.
                    //
                    // Until this, the origin could only ever be a plant that already
                    // existed, and the consequence was backwards: the first plant of
                    // every bed was placed blind, with no grid drawn and nothing to
                    // turn, and wherever it happened to land silently fixed the phase
                    // and angle for every plant after it. The one plant that most
                    // needed aiming was the only one with no help, and by the time the
                    // grid appeared it was already too late to move it.
                    //
                    // Seeding on the ghost costs nothing: on the frame it is set the
                    // ghost is exactly on a lattice point, so nothing jumps, and the
                    // first plant lands where the drawn grid says it will. The pin key
                    // re-seats it deliberately, and walking clear of the bed drops it.
                    var kin = NearestKin(ghostPlant, crop, at);
                    _anchor = kin.HasValue ? kin.Value : at;
                    _anchorCrop = crop;
                }
                anchor = _anchor.Value;
            }

            // Rounded in the lattice's own frame, so a turned grid stays square. At the
            // default angle the two rotations are identity and this is the plain
            // world-axis round it has always been.
            var angle = FurrowConfig.GridAngle.Value;
            var into = Quaternion.Euler(0f, -angle, 0f);
            var back = Quaternion.Euler(0f, angle, 0f);

            var local = into * (at - anchor);
            local.x = Mathf.Round(local.x / step) * step;
            local.z = Mathf.Round(local.z / step) * step;

            var snapped = anchor + back * new Vector3(local.x, 0f, local.z);
            snapped.y = at.y;

            // Follow the terrain at the snapped spot, or a snap across a dip leaves
            // the ghost floating and vanilla refuses the placement for it.
            float ground;
            if (ZoneSystem.instance != null
                && ZoneSystem.instance.GetGroundHeight(snapped, out ground))
                snapped.y = ground;

            ghost.transform.position = snapped;

            // Drawn from the same anchor, step and angle the snap just used, so the
            // lines cannot disagree with where the plant will land. Re-rung too, since
            // the ghost has moved since the ring above was drawn.
            GridPreview.Grid(anchor, step, angle, snapped, _pin.HasValue);
            GridPreview.Ring(snapped, ghostPlant, Room.Free(snapped, ghostPlant));

            Hint(__instance);
        }

        /// <summary>
        /// Name the two keys, once, the first time a grid is actually drawn.
        ///
        /// A key nobody knows about is a feature nobody has. Both of these are
        /// discoverable only by reading the config file, and the question they answer -
        /// "how am I supposed to turn this thing" - is asked while looking at the grid,
        /// which is exactly when this fires. Once per session: a hint repeated is a
        /// nag, and by the second bed it is already known.
        /// </summary>
        private static bool _hinted;

        private static void Hint(Player player)
        {
            if (_hinted) return;
            _hinted = true;

            player.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize(
                    "Grid on. $KEY_Use to plant, " + KeyName(FurrowConfig.GridTurnKey.Value)
                    + " to turn it, " + KeyName(FurrowConfig.GridPinKey.Value)
                    + " to pin it here."));
        }

        /// <summary>
        /// KeyCode.Mouse2 reads as "Mouse2", which names a button nobody calls that.
        /// The mouse buttons are spelled out; everything else is its own name, which
        /// for a keyboard key is already what is printed on it.
        /// </summary>
        private static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Mouse0: return "left click";
                case KeyCode.Mouse1: return "right click";
                case KeyCode.Mouse2: return "middle click";
                default: return key.ToString();
            }
        }

        /// <summary>
        /// Middle mouse is vanilla's Remove while a build tool is out, and it really
        /// does destroy the hovered piece - so for the one press we have taken over,
        /// removal is suppressed. Without this, turning the grid beside an existing
        /// bed would delete the very plant being lined up against.
        ///
        /// Scoped as tightly as it can be: only when the turn key IS middle mouse,
        /// only while a plant ghost is up, and only while the grid is actually
        /// running. Everywhere else - the hammer, the same cultivator on a piece that
        /// is not a plant, the grid switched off - removal is vanilla's again.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "RemovePiece")]
        private static bool BlockRemove(Player __instance)
        {
            if (FurrowConfig.GridTurnKey.Value != KeyCode.Mouse2) return true;
            if (!FurrowConfig.GridEnabled.Value) return true;
            if (__instance != Player.m_localPlayer) return true;

            var ghost = GhostRef(__instance);
            if (ghost == null || !ghost.activeSelf) return true;
            if (!ghost.GetComponent<Plant>()) return true;

            return __instance.GetSkillFactor(Skills.SkillType.Farming) * 100f
                   < FurrowConfig.GridLevel.Value;
        }

        /// <summary>
        /// Turn the lattice by a step.
        ///
        /// A key rather than the ghost's own rotation, which was the obvious binding
        /// and is wrong: crops carry m_randomInitBuildRotation, so the game re-rolls
        /// the ghost's yaw after every single placement. A grid tied to it would jump
        /// to a new angle each time a seed went in.
        ///
        /// Wrapped at 90 degrees because a square lattice repeats there - two presses
        /// of the default 22.5 covers every distinct grid, and the step matches the
        /// one vanilla turns buildings by, so a wall built square is reachable.
        /// </summary>
        private static void Turn(Player player)
        {
            if (!Pressed(FurrowConfig.GridTurnKey.Value)) return;

            var step = FurrowConfig.GridTurnStep.Value;
            if (step <= 0f) return;

            var angle = Mathf.Repeat(FurrowConfig.GridAngle.Value + step, 90f);
            FurrowConfig.GridAngle.Value = angle;

            player.Message(MessageHud.MessageType.Center,
                "Grid at " + angle.ToString("0.#") + "°");
        }

        /// <summary>
        /// Pin the lattice where the ghost stands, or lift the pin.
        ///
        /// The pin is what makes the grid line up with a BUILDING. Anchoring on the
        /// nearest kin is right for extending a bed that already exists and cannot be
        /// right for a bed that does not: the phase then comes from wherever the first
        /// plant happened to land, which is nowhere in particular. Pin a corner against
        /// your floor and every row runs from it.
        /// </summary>
        private static void Pin(Player player, Vector3 at)
        {
            if (!Pressed(FurrowConfig.GridPinKey.Value)) return;

            if (_pin.HasValue)
            {
                _pin = null;
                _anchor = null;
                player.Message(MessageHud.MessageType.Center, "Grid unpinned");
                return;
            }

            _pin = at;
            player.Message(MessageHud.MessageType.Center, "Grid pinned here");
        }

        /// <summary>
        /// The nearest standing plant of the same crop, because a carrot grid and a
        /// turnip grid interleaved is somebody's garden design, not a mistake to
        /// correct. "Same crop" spans the growth stages - the sapling prefab and its
        /// m_grownPrefabs - and the grown stage carries Pickable rather than Plant,
        /// so both components are consulted. Distances are XZ only: on a slope the
        /// height difference must not decide which plant seeds the lattice.
        /// </summary>
        private static Vector3? NearestKin(Plant ghostPlant, string crop, Vector3 at)
        {
            if (_kinCrop != crop)
            {
                _kin.Clear();
                _kin.Add(crop);
                if (ghostPlant.m_grownPrefabs != null)
                    foreach (var grown in ghostPlant.m_grownPrefabs)
                        if (grown != null) _kin.Add(grown.name);
                _kinCrop = crop;
            }

            Vector3? best = null;
            var bestSqr = SearchRadius * SearchRadius;

            foreach (var hit in Physics.OverlapSphere(at, SearchRadius))
            {
                if (hit == null) continue;

                Vector3 pos;
                var plant = hit.GetComponentInParent<Plant>();
                if (plant != null && plant.gameObject != ghostPlant.gameObject
                    && _kin.Contains(Utils.GetPrefabName(plant.gameObject.name)))
                {
                    pos = plant.transform.position;
                }
                else
                {
                    var pickable = hit.GetComponentInParent<Pickable>();
                    if (pickable == null
                        || !_kin.Contains(Utils.GetPrefabName(pickable.gameObject.name)))
                        continue;
                    pos = pickable.transform.position;
                }

                var d = FlatSqr(pos, at);
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = pos;
                }
            }

            return best;
        }

        /// <summary>
        /// A key press that is not a keystroke meant for a text field. Without this a
        /// keypad key typed into chat or the console also turns the grid, which reads
        /// as the grid moving on its own.
        /// </summary>
        private static bool Pressed(KeyCode key)
        {
            if (!Input.GetKeyDown(key)) return false;
            if (Chat.instance != null && Chat.instance.HasFocus()) return false;
            return !Console.IsVisible() && !TextInput.IsVisible();
        }

        private static float FlatSqr(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
