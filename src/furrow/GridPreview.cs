using System.Collections.Generic;
using UnityEngine;

namespace Furrow
{
    /// <summary>
    /// Two drawings on the ground, sharing one pool of lines.
    ///
    /// THE LATTICE. Without it the grid is three numbers in a config file and a plant
    /// that lands somewhere unexpected - and turning it is guesswork, because the only
    /// way to see what an angle did was to plant something and look at where it went.
    /// Lines rather than dots at the intersections: the question a bed asks is which
    /// ROWS it will have and whether they run square to the wall beside them, and that
    /// is a question about long straight edges.
    ///
    /// THE ROOM RING. A circle at the plant's own grow radius, green when it would have
    /// space and red when it would not - see Room.cs for why the game cannot be relied
    /// on to say so in time. It is drawn whatever the grid is doing, because "will this
    /// oak fit" is worth answering at Farming 0 with the grid switched off.
    ///
    /// Both follow the ground, sampling height at every vertex, so they hug a slope
    /// instead of burying themselves in the first rise. That is also why each is
    /// rebuilt only when what it depicts actually moves rather than every frame: a
    /// rebuild is a hundred-odd ground samples, cheap once and wasteful at sixty a
    /// second.
    /// </summary>
    internal static class GridPreview
    {
        private static GameObject _root;
        private static readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private static LineRenderer _ring;

        private static Material _material;
        private static bool _materialTried;

        // What each drawing currently shows. A rebuild is skipped when none of it moved.
        private static float _step;
        private static float _angle;
        private static float _cellX;
        private static float _cellZ;
        private static bool _pinned;
        private static Vector3 _anchor;
        private static bool _gridShown;

        private static Vector3 _ringAt;
        private static float _ringRadius;
        private static bool _ringFree;
        private static bool _ringShown;

        /// <summary>Pale green while the lattice is found, amber while it is pinned.</summary>
        private static readonly Color Found = new Color(0.62f, 0.85f, 0.55f, 0.5f);
        private static readonly Color Pinned = new Color(1.00f, 0.78f, 0.35f, 0.62f);

        /// <summary>Room to grow, and none. Red is the one that has to read instantly.</summary>
        private static readonly Color Clear = new Color(0.55f, 0.90f, 0.55f, 0.55f);
        private static readonly Color Blocked = new Color(0.95f, 0.30f, 0.25f, 0.75f);

        private const int RingSegments = 24;

        /// <summary>Everything away - the tool is down or the ghost is not a plant.</summary>
        public static void Hide()
        {
            HideGrid();
            HideRing();

            // == null, never ?., because a destroyed GameObject compares equal to null
            // and the null-propagating operators walk straight past that.
            if (_root != null && _root.activeSelf) _root.SetActive(false);
        }

        public static void HideGrid()
        {
            if (!_gridShown) return;
            _gridShown = false;

            foreach (var line in _lines)
                if (line != null) line.enabled = false;
        }

        private static void HideRing()
        {
            if (!_ringShown) return;
            _ringShown = false;
            if (_ring != null) _ring.enabled = false;
        }

        // ------------------------------------------------------------------ the ring

        public static void Ring(Vector3 at, Plant plant, bool free)
        {
            if (!FurrowConfig.RoomPreview.Value || plant == null || plant.m_growRadius <= 0f)
            {
                HideRing();
                return;
            }

            // Redrawn on a real move rather than on every twitch of the cursor: the
            // ring is two dozen ground samples and the eye cannot see a tenth of a
            // metre of lag on a circle a metre wide.
            var moved = (at - _ringAt).sqrMagnitude > 0.01f;
            if (_ringShown && !moved
                && Mathf.Approximately(_ringRadius, plant.m_growRadius)
                && _ringFree == free) return;

            if (!Ready()) return;

            _ringAt = at;
            _ringRadius = plant.m_growRadius;
            _ringFree = free;

            if (_ring == null)
            {
                _ring = NewLine("room_ring");
                // Drawn a shade heavier than the lattice. It is the one of the two that
                // reports a refusal, and a refusal has to be the thing you notice.
                _ring.widthMultiplier = 0.06f;
            }

            _ring.positionCount = RingSegments + 1;
            var colour = free ? Clear : Blocked;
            _ring.startColor = colour;
            _ring.endColor = colour;
            _ring.enabled = true;

            for (var i = 0; i <= RingSegments; i++)
            {
                var a = i / (float)RingSegments * Mathf.PI * 2f;
                _ring.SetPosition(i, OnGround(new Vector3(
                    at.x + Mathf.Cos(a) * _ringRadius,
                    at.y,
                    at.z + Mathf.Sin(a) * _ringRadius)));
            }

            _ringShown = true;
            _root.SetActive(true);
        }

        // --------------------------------------------------------------- the lattice

        public static void Grid(Vector3 anchor, float step, float angle, Vector3 centre,
                                bool pinned)
        {
            if (!FurrowConfig.GridPreview.Value) { HideGrid(); return; }

            var rings = Mathf.Clamp(FurrowConfig.GridPreviewRings.Value, 1, 12);

            var into = Quaternion.Euler(0f, -angle, 0f);
            var back = Quaternion.Euler(0f, angle, 0f);

            var local = into * (centre - anchor);
            var cellX = Mathf.Round(local.x / step);
            var cellZ = Mathf.Round(local.z / step);

            // Everything the drawing is made of. Move any of it and it is redrawn;
            // move none of it and this costs a handful of float compares.
            var same = _gridShown
                       && Mathf.Approximately(_step, step)
                       && Mathf.Approximately(_angle, angle)
                       && Mathf.Approximately(_cellX, cellX)
                       && Mathf.Approximately(_cellZ, cellZ)
                       && _pinned == pinned
                       && (_anchor - anchor).sqrMagnitude < 0.0001f;
            if (same) return;

            if (!Ready()) return;

            _step = step;
            _angle = angle;
            _cellX = cellX;
            _cellZ = cellZ;
            _pinned = pinned;
            _anchor = anchor;

            var span = rings * 2 + 1;
            var colour = pinned ? Pinned : Found;
            var used = 0;

            // One line per lattice row, then one per column, each running the full
            // span - so the drawing is a grid rather than a cross under the cursor.
            for (var j = -rings; j <= rings; j++)
                Line(used++, anchor, back, step, cellX - rings, cellZ + j, span, true, colour);

            for (var i = -rings; i <= rings; i++)
                Line(used++, anchor, back, step, cellX + i, cellZ - rings, span, false, colour);

            for (var i = used; i < _lines.Count; i++)
                if (_lines[i] != null) _lines[i].enabled = false;

            _gridShown = true;
            _root.SetActive(true);
        }

        /// <summary>
        /// One line of the lattice, from cell (x, z) running <paramref name="span"/>
        /// cells along x when <paramref name="alongX"/>, otherwise along z.
        /// </summary>
        private static void Line(int index, Vector3 anchor, Quaternion back, float step,
                                 float x, float z, int span, bool alongX, Color colour)
        {
            var line = LineAt(index);
            if (line == null) return;

            line.positionCount = span;
            line.startColor = colour;
            line.endColor = colour;
            line.enabled = true;

            for (var i = 0; i < span; i++)
            {
                var lattice = alongX
                    ? new Vector3((x + i) * step, 0f, z * step)
                    : new Vector3(x * step, 0f, (z + i) * step);

                line.SetPosition(i, OnGround(anchor + back * lattice));
            }
        }

        // ------------------------------------------------------------------ plumbing

        /// <summary>
        /// The ground under a point, lifted clear of it. Without the lift a line lying
        /// exactly along the terrain z-fights with it and comes out as dashes.
        /// </summary>
        private static Vector3 OnGround(Vector3 world)
        {
            float ground;
            if (ZoneSystem.instance != null
                && ZoneSystem.instance.GetGroundHeight(world, out ground))
                world.y = ground;

            world.y += 0.05f;
            return world;
        }

        private static LineRenderer LineAt(int index)
        {
            while (_lines.Count <= index)
                _lines.Add(NewLine("line" + _lines.Count));

            return _lines[index];
        }

        private static LineRenderer NewLine(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = _material;
            line.widthMultiplier = 0.04f;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            return line;
        }

        /// <summary>
        /// The holder and the material, built on first use and rebuilt if the world
        /// took them - a world change destroys everything in the scene, and this pool
        /// is a scene object like any other.
        /// </summary>
        private static bool Ready()
        {
            if (_material == null && !Borrow()) return false;

            if (_root == null)
            {
                _root = new GameObject("furrow_preview");
                _lines.Clear();
                _ring = null;
                _gridShown = false;
                _ringShown = false;
            }

            return true;
        }

        /// <summary>
        /// A material to draw with, from a shader the game already ships.
        ///
        /// Shader.Find only answers for shaders that made it into the build, so this is
        /// a list rather than a name: the first is the one vanilla's own glow flares
        /// wear, and the rest are the usual unlit fallbacks. A miss turns the drawing
        /// off and says so once - a null material renders magenta, which is a louder
        /// wrong answer than no drawing at all.
        /// </summary>
        private static bool Borrow()
        {
            if (_materialTried) return false;
            _materialTried = true;

            var names = new[]
            {
                "Legacy Shaders/Particles/Alpha Blended",
                "Particles/Standard Unlit",
                "Sprites/Default",
                "Unlit/Color",
                "Unlit/Texture",
            };

            foreach (var name in names)
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;

                // Ours, made here - not a borrowed material being written to, which is
                // shared with every other object wearing it.
                _material = new Material(shader);

                // The legacy particle shaders multiply by _TintColor, which defaults to
                // half grey at half alpha - so a line drawn white arrives at a quarter
                // strength and reads as a rendering fault rather than as a choice.
                if (_material.HasProperty("_TintColor"))
                    _material.SetColor("_TintColor", Color.white);

                Grove.GrovePlugin.Log.LogInfo("Furrow preview drawn with " + name + ".");
                return true;
            }

            Grove.GrovePlugin.Log.LogWarning(
                "No shader available to draw the Furrow previews - they stay off. The "
                + "grid itself is unaffected.");
            return false;
        }
    }
}
