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
    /// onto a lattice spaced by the plant's own grow radius.
    ///
    /// World-aligned axes rather than anchor-rotated: rows running north-south are
    /// predictable from any approach angle, which is what a grid is for.
    ///
    /// WHICH lattice - its origin, the phase every row runs from, and beside a bed its
    /// angle too - is Lattice.cs's business, and the reasons it is decided the way it is
    /// are written there. In short: a plant beside plants already in the ground continues
    /// their rows, at their angle, voted on by the plants within a few cells so one stray
    /// cannot steer a bed; and on open ground it lands on one grid shared by the whole
    /// world. That replaced a rule that fell back on the cursor, and the cursor is what
    /// put two patches of one field on two grids and walked an oak row off the grid of
    /// its first tree (LHM-29).
    ///
    /// This file is the part that runs every frame: the gates, the gestures, putting the
    /// ghost where the lattice says, and asking vanilla's placement questions again at
    /// the spot the ghost was moved to.
    ///
    /// All three properties of a lattice are the player's, because "it does not line
    /// up with my build" is not answerable by any default. Its SPACING is GridCell,
    /// absolute metres, overriding the crop's own grow radius - which is per-crop and
    /// so can never match a floor. Its ANGLE is GridAngle, for a building that does not
    /// sit square to the world; beside a bed laid at another angle the bed's rows win,
    /// and the turn message says so. And its ORIGIN is GridPinKey: the shared grid and
    /// the beds already planted are right for everything except lining a new bed up
    /// with a floor, which is what the pin is for. A pin outranks everything else -
    /// angle included - and survives a change of crop, so one pinned bed takes carrots
    /// and turnips in the same rows.
    /// </summary>
    [HarmonyPatch]
    internal static class GridPlacement
    {
        private static readonly AccessTools.FieldRef<Player, GameObject> GhostRef =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        // A pinned phase outranks the found one and survives a change of crop, because
        // it is about the ground rather than about the plant: you pin a corner of the
        // bed you are laying out, then plant carrots and turnips into the same rows.
        // Not held across a session - a pin is for the bed being worked on.
        private static Vector3? _pin;

        /// <summary>
        /// The grid in use, as a point on it. A position, deliberately not a plant - it
        /// stays valid after the plant it came from is harvested, and a Vector3 cannot
        /// become a dead UnityEngine.Object mid-frame.
        ///
        /// It is the vote's tie-break, which is what keeps the rows still while the cursor
        /// sits between two beds of equal size. With GridShared off it is also this
        /// session's own grid, carried from bed to bed across open ground.
        ///
        /// It is NOT dropped when the ghost disappears, and that is half of the LHM-29 fix.
        /// It used to be, on the argument that the next pick would land on the same lattice
        /// anyway - which was true only when the next pick found a plant. Vanilla hides the
        /// ghost whenever the placement ray misses or runs past reach, so looking up at the
        /// next spot for an oak dropped the grid, and the next pick - four metres round a
        /// spot a whole oak's spacing from the last oak - found nothing and started again
        /// under the cursor.
        /// </summary>
        private static Vector3? _current;

        /// <summary>
        /// The angle of the grid in use, which is not always yours: beside a bed laid at
        /// another angle the rows follow the bed (see Lattice's header). A turn writes the new
        /// angle here too, so the grid in use turns with it wherever no bed says otherwise.
        /// </summary>
        private static float _currentAngle;

        internal enum Source
        {
            /// <summary>The pin key put it here.</summary>
            Pin,
            /// <summary>Plants already in the ground agree on it.</summary>
            Bed,
            /// <summary>Nothing stands near: the grid shared by the whole world.</summary>
            World,
            /// <summary>GridShared off: the grid this session has been planting on.</summary>
            Held,
            /// <summary>GridShared off and nothing to go on: a new grid, starting here.</summary>
            Here
        }

        // The last vote, reused while nothing it depended on has moved. The vote walks
        // every loaded plant and sweeps the ground for grown crops, which is cheap once
        // and wasteful sixty times a second. Neither limit costs anything that shows:
        // your own plant placed since the vote went ON the voted grid, so between reruns
        // the vote can only have gained agreement.
        private static bool _voted;
        private static Vector3 _votedAt;
        private static float _votedTime;
        private static float _votedStep;
        private static float _votedAngle;
        private static bool _votedShared;
        private static Vector3 _anchor;
        private static float _anchorAngle;
        private static Source _source;
        private static int _support;

        /// <summary>
        /// Set by a turn, and read once the snap has been worked out, so the message can say
        /// what the turn actually did to the rows under the cursor rather than only what it
        /// wrote to the config.
        /// </summary>
        private static bool _turned;

        private const float RevoteDistance = 0.25f;
        private const float RevoteSeconds = 0.5f;

        private static readonly List<Vector3> _kin = new List<Vector3>();

        /// <summary>Where the last placement's grid came from, for the console and the log.</summary>
        internal static Source LastSource { get { return _pin.HasValue ? Source.Pin : _source; } }

        /// <summary>How many standing plants agreed on it, when it came from a bed.</summary>
        internal static int LastSupport { get { return _pin.HasValue ? 0 : _support; } }

        /// <summary>
        /// Whether the player is holding the grid away for this placement.
        ///
        /// Left at None the key is vanilla's own AltPlace - Shift by default, and
        /// rebindable in the game's controls, so a rebind moves this along with it.
        /// GetButton rather than a raw KeyCode read is what makes that true, and it
        /// brings the gamepad's JoyAltPlace along unasked. A configured key replaces
        /// the button instead of joining it, so someone who moved the gesture off
        /// Shift is not still triggering it by sprinting.
        /// </summary>
        private static bool Suspended()
        {
            var key = FurrowConfig.GridFreeKey.Value;
            if (key != KeyCode.None) return Keys.Held(key);

            return ZInput.instance != null
                && (ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyAltPlace"));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static void Snap(Player __instance, bool flashGuardStone)
        {
            if (__instance != Player.m_localPlayer) return;

            var ghost = GhostRef(__instance);
            if (ghost == null || !ghost.activeSelf)
            {
                // The drawing goes and the next frame votes afresh, but the grid in use
                // stays - see _current for why dropping it here was the bug.
                _voted = false;
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

            // Held, not toggled: a player asked to plant one thing off-grid, and a
            // toggle is state you forget while a held key ends when the hand opens.
            // Returning here, before Pin/Turn/Wheel and the snap, is the whole
            // feature - the ghost is vanilla's again, and the scroll wheel goes back
            // to being the camera's.
            if (Suspended()) { GridPreview.HideGrid(); return; }

            if (__instance.GetSkillFactor(Skills.SkillType.Farming) * 100f
                < FurrowConfig.GridLevel.Value) { GridPreview.HideGrid(); return; }

            // The crop's own grow radius is the tightest spacing that always takes, and
            // it differs per crop - right for a bed of one thing, wrong for lining rows
            // up with a floor, which is why an absolute override exists beside it.
            var step = Lattice.StepFor(ghostPlant);
            Lattice.Describe(Utils.GetPrefabName(ghost.name), ghostPlant, step);

            var at = ghost.transform.position;

            Pin(__instance, at);
            Turn(__instance);
            Wheel(__instance);

            Vector3 anchor;
            float angle;
            var snapped = Resolve(at, step, out anchor, out angle);
            ghost.transform.position = snapped;

            Recheck(__instance, ghost, at, snapped, flashGuardStone);

            if (_turned)
            {
                _turned = false;
                SayTurn(__instance, angle);
            }

            // Drawn from the same anchor, step and angle the snap just used, so the
            // lines cannot disagree with where the plant will land. Re-rung too, since
            // the ghost has moved since the ring above was drawn.
            GridPreview.Grid(anchor, step, angle, snapped, _pin.HasValue);
            GridPreview.Ring(snapped, ghostPlant, Room.Free(snapped, ghostPlant));

            Hint(__instance);
        }

        // ------------------------------------------------------------------ the checks

        private static AccessTools.FieldRef<Player, Player.PlacementStatus> _status;
        private static System.Func<Player, bool> _blockedByPlayer;
        private static bool _checksBound;

        /// <summary>
        /// Ask vanilla's questions again at the spot the plant will actually land.
        ///
        /// Player.UpdatePlacementGhost checks tilled ground at the placement ray's hit point,
        /// and wards, no-build locations, biome and standing players at the ghost's position -
        /// all before this postfix moves the ghost onto the grid. TryPlacePiece then acts on
        /// that verdict and plants wherever the ghost stands. So a carrot aimed at the edge
        /// of a hoed patch passed on the hoed side, snapped half a row onto grass, was planted
        /// there, wilted at its first health check ten seconds later, and was gone when it
        /// should have ripened, seed and all: carrots rip with m_destroyIfCantGrow on, and
        /// Plant.Grow destroys a plant that is not healthy instead of growing it. That is the
        /// only place it is destroyed, so it stands there wilted for the whole grow time
        /// first, where it cannot be removed either. An oak aimed just outside a
        /// neighbour's ward could snap inside it the same way. That was true of every plant
        /// after the first from the day the grid shipped; the shared grid made it true of the
        /// first one too, which is when review caught it.
        ///
        /// Only ever turns a yes into a no, never the other way: a spot vanilla refused keeps
        /// vanilla's refusal and vanilla's reason. The reasons are vanilla's own statuses, so
        /// the press earns vanilla's own message ("$msg_needcultivated" and the rest) and the
        /// ghost goes red the way it always does. Heightmap.FindHeightmap at the snapped spot
        /// is what Plant.UpdateHealth itself uses for its tilled-ground test, so the answer is
        /// the one the plant would have given ten seconds too late.
        ///
        /// Water and clipping are not rechecked. Water needs the ray's own water hit, and no
        /// plant ripped so far sets m_noClipping; a half-row move changes neither in practice.
        /// </summary>
        private static void Recheck(Player player, GameObject ghost, Vector3 aimed, Vector3 snapped,
                                    bool flash)
        {
            if (Lattice.FlatSqr(aimed, snapped) < 1e-6f) return;

            BindChecks();
            if (_status == null) return;
            if (_status(player) != Player.PlacementStatus.Valid) return;

            Piece piece;
            if (!ghost.TryGetComponent(out piece)) return;

            var refused = Refusal(player, piece, snapped, flash);
            if (refused == Player.PlacementStatus.Valid) return;

            _status(player) = refused;
            piece.SetInvalidPlacementHeightlight(true);
        }

        /// <summary>
        /// Vanilla's order and vanilla's tests, so where two apply the later one names the
        /// reason, as it would have.
        /// </summary>
        private static Player.PlacementStatus Refusal(Player player, Piece piece, Vector3 at, bool flash)
        {
            var status = Player.PlacementStatus.Valid;
            var heightmap = Heightmap.FindHeightmap(at);

            if (piece.m_cultivatedGroundOnly && (heightmap == null || !heightmap.IsCultivated(at)))
                status = Player.PlacementStatus.NeedCultivated;

            if (piece.m_vegetationGroundOnly)
            {
                var bare = heightmap == null;
                if (!bare)
                {
                    var mask = heightmap.GetVegetationMask(at);
                    bare = heightmap.GetBiome(at) == Heightmap.Biome.AshLands ? mask > 0.1f : mask < 0.25f;
                }
                if (bare) status = Player.PlacementStatus.NeedDirt;
            }

            if (Location.IsInsideNoBuildLocation(at))
                status = Player.PlacementStatus.NoBuildZone;

            PrivateArea ward;
            var hasWard = piece.TryGetComponent(out ward);
            if (!PrivateArea.CheckAccess(at, hasWard ? ward.m_radius : 0f, flash, hasWard))
                status = Player.PlacementStatus.PrivateZone;

            // Reads the ghost where it now stands, which is why this runs after the move.
            if (_blockedByPlayer != null && _blockedByPlayer(player))
                status = Player.PlacementStatus.BlockedbyPlayer;

            if (piece.m_onlyInBiome != 0 && (Heightmap.FindBiome(at) & piece.m_onlyInBiome) == 0)
                status = Player.PlacementStatus.WrongBiome;

            return status;
        }

        /// <summary>
        /// Bound on first use inside a try/catch, never in a field initialiser: a throw from a
        /// static initialiser would poison every patch this class carries, and the grid would
        /// stop working rather than stop rechecking. A failed binding costs the recheck - the
        /// grid then places the way it did before this existed - and says so once.
        /// </summary>
        private static void BindChecks()
        {
            if (_checksBound) return;
            _checksBound = true;

            try
            {
                _status = AccessTools.FieldRefAccess<Player, Player.PlacementStatus>("m_placementStatus");
            }
            catch (System.Exception e)
            {
                Grove.GrovePlugin.Log.LogWarning("Furrow grid: Player.m_placementStatus could not be "
                    + "reached (" + e.Message + "), so a plant snapped onto grass or into a ward is "
                    + "not refused before it is planted.");
            }

            try
            {
                _blockedByPlayer = AccessTools.MethodDelegate<System.Func<Player, bool>>(
                    AccessTools.Method(typeof(Player), "CheckPlacementGhostVSPlayers"));
            }
            catch (System.Exception e)
            {
                Grove.GrovePlugin.Log.LogWarning("Furrow grid: Player.CheckPlacementGhostVSPlayers "
                    + "could not be reached (" + e.Message + "), so a snap onto a player is not "
                    + "refused. Everything else is still rechecked.");
            }
        }

        /// <summary>
        /// Where a plant with rows <paramref name="step"/> apart, aimed at
        /// <paramref name="at"/>, lands - and the point its grid runs through, and the angle
        /// its rows run at.
        ///
        /// The one path from an aim to a planted spot. The ghost goes through it every
        /// frame, and so does `furrowtest plant`, which is what lets a Devkit scenario test
        /// the grid a player gets rather than a copy of it.
        ///
        /// The pin keeps your own angle; so does open ground. Beside a bed the angle is the
        /// bed's - see Lattice's header for why.
        /// </summary>
        internal static Vector3 Resolve(Vector3 at, float step, out Vector3 anchor, out float angle)
        {
            var preferred = FurrowConfig.GridAngle.Value;

            if (_pin.HasValue)
            {
                anchor = _pin.Value;
                angle = preferred;
            }
            else
            {
                Choose(at, step, preferred, out anchor, out angle);
            }

            return Lattice.Snap(anchor, step, angle, at);
        }

        /// <summary>
        /// Throw away everything the grid is holding - the pin, the grid in use and the last
        /// vote - so the next plant has only the ground to go on. That is the state a fresh
        /// game starts in, and the state the ghost used to reset to every time it vanished,
        /// which is why the console exposes it: a scenario that forgets between plants is
        /// testing the worst case rather than the lucky one.
        /// </summary>
        internal static void Forget()
        {
            _pin = null;
            _current = null;
            _voted = false;
        }

        /// <summary>
        /// Which grid, when there is no pin. The reasons for each branch are in Lattice.cs;
        /// this is the order they are asked in.
        /// </summary>
        private static void Choose(Vector3 at, float step, float preferred,
                                   out Vector3 anchor, out float angle)
        {
            var shared = FurrowConfig.GridShared.Value;

            // A quarter of a cell, and never less than RevoteDistance. Less than that barely
            // changes which plants are in reach, and a tree's cell is metres wide: re-running
            // a sweep three cells across for every centimetre the mouse moves would be the
            // cost of aiming an oak. The half-second limit catches whatever this misses.
            var revote = Mathf.Max(RevoteDistance, step * 0.25f);

            if (_voted
                && Time.time - _votedTime < RevoteSeconds
                && Lattice.FlatSqr(at, _votedAt) < revote * revote
                && Lattice.SameStep(step, _votedStep)
                && Mathf.Approximately(preferred, _votedAngle)
                && shared == _votedShared)
            {
                anchor = _anchor;
                angle = _anchorAngle;
                return;
            }

            Lattice.Gather(at, step, _kin);

            Vector3 best;
            float bestAngle;
            int support;
            if (Lattice.Strongest(_kin, at, step, preferred, _current, _currentAngle, shared,
                                  out best, out bestAngle, out support))
            {
                _anchor = best;
                _anchorAngle = bestAngle;
                _source = Source.Bed;
                _support = support;
            }
            else if (shared)
            {
                _anchor = Vector3.zero;
                _anchorAngle = preferred;
                _source = Source.World;
                _support = 0;
            }
            else if (_current.HasValue)
            {
                _anchor = _current.Value;
                _anchorAngle = _currentAngle;
                _source = Source.Held;
                _support = 0;
            }
            else
            {
                // GridShared off, and nothing planted this session or standing near: the
                // grid is born under the cursor, the way it was before there was a shared
                // one. On this frame the ghost is exactly on a lattice point, so nothing
                // jumps and the first plant lands where the drawn grid says it will.
                _anchor = at;
                _anchorAngle = preferred;
                _source = Source.Here;
                _support = 0;
            }

            _current = _anchor;
            _currentAngle = _anchorAngle;
            anchor = _anchor;
            angle = _anchorAngle;

            _voted = true;
            _votedAt = at;
            _votedTime = Time.time;
            _votedStep = step;
            _votedAngle = angle;
            _votedShared = shared;

            // Only on a change, so the log says when the rows moved and not that they
            // did not. Verbose, because a player walking along a field changes this often
            // and every one of those is the grid working.
            if (FurrowConfig.Verbose.Value
                && (!_logged || _loggedSource != _source
                    || (_loggedAnchor - _anchor).sqrMagnitude > 1e-6f
                    || Lattice.AngleGap(_loggedAngle, _anchorAngle) >= Lattice.SameAngle))
            {
                _logged = true;
                _loggedSource = _source;
                _loggedAnchor = _anchor;
                _loggedAngle = _anchorAngle;

                Grove.GrovePlugin.Log.LogInfo("Furrow grid: " + Describe(_source, _support)
                                              + " (rows " + step.ToString("0.##") + "m apart, "
                                              + _anchorAngle.ToString("0.#") + " degrees"
                                              + (Lattice.AngleGap(_anchorAngle, preferred) >= Lattice.SameAngle
                                                  ? ", yours is " + preferred.ToString("0.#")
                                                  : "")
                                              + ").");
            }
        }

        private static bool _logged;
        private static Source _loggedSource;
        private static Vector3 _loggedAnchor;
        private static float _loggedAngle;

        /// <summary>Where a grid came from, in words a player would use.</summary>
        internal static string Describe(Source source, int support)
        {
            switch (source)
            {
                case Source.Pin: return "on the pinned grid";
                case Source.Bed:
                    return "on the grid of the plants already there (" + support
                           + (support == 1 ? " plant" : " plants") + " in line)";
                case Source.World: return "on the shared grid, nothing planted near";
                case Source.Held: return "on the grid this session has been planting on";
                default: return "on a new grid starting here";
            }
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
                    "Grid on. $KEY_Use to plant, " + TurnGesture()
                    + " to turn it, " + KeyName(FurrowConfig.GridPinKey.Value)
                    + " to pin it here."));
        }

        /// <summary>
        /// KeyCode.Mouse2 reads as "Mouse2", which names a button nobody calls that.
        /// The mouse buttons are spelled out; everything else is its own name, which
        /// for a keyboard key is already what is printed on it.
        /// </summary>
        /// <summary>What to call the turn gesture, given what is actually bound.</summary>
        private static string TurnGesture()
        {
            var key = FurrowConfig.GridTurnKey.Value;
            var scroll = FurrowConfig.GridTurnScroll.Value;

            if (scroll && key != KeyCode.None) return "scroll or " + KeyName(key);
            if (scroll) return "scroll";
            if (key != KeyCode.None) return KeyName(key);
            return "nothing (no turn gesture is bound)";
        }

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
            if (Suspended()) return true;
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
        /// <summary>
        /// The wheel turns the rows.
        ///
        /// This is the gesture, and a key is the option, because the wheel is what a
        /// player already reaches for to rotate something - and while planting it is
        /// very nearly free. A crop carries m_randomInitBuildRotation, so the game
        /// re-rolls the ghost's facing after every single placement: whatever yaw you
        /// scrolled to is discarded the instant the seed goes in, and no amount of
        /// scrolling can give a bed a consistent facing. Spending the same wheel on
        /// the lattice spends it on the one thing that does survive the click.
        ///
        /// The plant still turns underneath, because vanilla's own rotation runs
        /// before this postfix and is left alone. Holding it still would mean fighting
        /// UpdatePlacement for a facing the game is about to randomise anyway.
        ///
        /// Accumulated against vanilla's own threshold so a notch here is a notch
        /// there: the rows and the ghost step together rather than at different rates,
        /// which would read as the grid lagging the mouse.
        /// </summary>
        private static float _wheel;

        private static void Wheel(Player player)
        {
            if (!FurrowConfig.GridTurnScroll.Value) return;

            _wheel += ZInput.GetMouseScrollWheel();

            const float threshold = 0.1f;   // Player.m_scrollAmountThreshold
            if (_wheel > threshold) { _wheel = 0f; Step(player, 1); }
            else if (_wheel < -threshold) { _wheel = 0f; Step(player, -1); }
        }

        private static void Turn(Player player)
        {
            if (!Keys.Pressed(FurrowConfig.GridTurnKey.Value)) return;
            Step(player, 1);
        }

        /// <summary>
        /// One notch of the lattice, either way.
        ///
        /// Wrapped at 90 degrees because a square lattice repeats there - four presses
        /// of the default 22.5 covers every distinct grid, so there is no long way
        /// round to find the one you want.
        /// </summary>
        private static void Step(Player player, int direction)
        {
            if (TurnBy(direction)) _turned = true;
        }

        /// <summary>
        /// Turn your angle by whole notches of GridTurnStep, and the grid in use with it.
        /// False when GridTurnStep is 0 and nothing turned. Shared by the wheel, the key and
        /// `furrowtest turn`, so a scenario turns the grid the way a hand does.
        ///
        /// The grid in use takes the new angle too, which is what makes a turn do what it
        /// says wherever the plants do not overrule it: on open ground, about a lone plant,
        /// and on the grid this session was carrying with GridShared off. Beside two or more
        /// plants in line the vote still finds their angle and keeps it - SayTurn tells you so.
        /// </summary>
        internal static bool TurnBy(int notches)
        {
            var step = FurrowConfig.GridTurnStep.Value;
            if (step <= 0f || notches == 0) return false;

            var angle = Mathf.Repeat(FurrowConfig.GridAngle.Value + step * notches, 90f);
            FurrowConfig.GridAngle.Value = angle;

            _currentAngle = angle;
            _voted = false;
            return true;
        }

        /// <summary>
        /// Say what a turn did to the rows under the cursor, after the vote has run on the new
        /// angle.
        ///
        /// Usually that is "Grid at 22.5°" and the drawn lines turn. Beside a bed laid at
        /// another angle they do not, because the bed keeps its rows (Lattice's header has
        /// why), and a message claiming the grid turned while the lines on the ground sat
        /// still would read as the turn being broken. So it names both angles, and the pin,
        /// which is how you turn the rows beside a bed on purpose.
        /// </summary>
        private static void SayTurn(Player player, float used)
        {
            var yours = FurrowConfig.GridAngle.Value;

            if (_pin.HasValue || Lattice.AngleGap(yours, used) < Lattice.SameAngle)
            {
                player.Message(MessageHud.MessageType.Center, "Grid at " + yours.ToString("0.#") + "°");
                return;
            }

            player.Message(MessageHud.MessageType.Center,
                "Grid at " + yours.ToString("0.#") + "°, but this bed keeps its rows at "
                + used.ToString("0.#") + "°. " + KeyName(FurrowConfig.GridPinKey.Value)
                + " pins yours here.");
        }

        /// <summary>
        /// Pin the lattice where the ghost stands, or lift the pin.
        ///
        /// The pin is what makes the grid line up with a BUILDING. Following the plants
        /// already standing is right for extending a bed, and the shared grid is right for
        /// keeping every bed in line with every other - and neither has any reason to meet
        /// your walls. Pin a corner against your floor and every row runs from it. Once a
        /// few plants are in, the pinned bed is its own evidence, so the pin can be lifted
        /// and the bed still extends on its rows.
        /// </summary>
        private static void Pin(Player player, Vector3 at)
        {
            if (!Keys.Pressed(FurrowConfig.GridPinKey.Value)) return;

            // Either way the next frame votes afresh. Lifting a pin also lets go of the
            // grid in use, so what takes over is read off the ground - which, once a few
            // plants are in, is the pinned bed itself.
            _voted = false;

            if (_pin.HasValue)
            {
                _pin = null;
                _current = null;
                player.Message(MessageHud.MessageType.Center, "Grid unpinned");
                return;
            }

            _pin = at;
            player.Message(MessageHud.MessageType.Center, "Grid pinned here");
        }
    }
}
