using System.Collections.Generic;
using UnityEngine;
using Grove;
using Stow;

namespace Hod
{
    /// <summary>
    /// Draws the reach circles of hod jibs (LHM-77).
    ///
    /// Shown on the placement ghost, with the ring tinted by whether the jib would join a network
    /// or stand alone, and on built jibs while the player holds a build tool within twice the
    /// reach, or looks at one. When a ghost would join a network, or a jib is looked at, the whole
    /// network's rings are drawn so the chain is visible.
    ///
    /// A pool of line renderers driven from HodRuntime.Tick, the same pattern as Furrow's
    /// GridPreview, whose shader list it borrows. A ring is re-sampled against the terrain only
    /// when its centre or radius changed.
    /// </summary>
    internal static class HodRing
    {
        private const int Segments = 72;

        private static readonly Color Built = new Color(0.85f, 0.64f, 0.25f, 0.8f);
        private static readonly Color Joins = new Color(0.45f, 0.85f, 0.45f, 0.9f);
        private static readonly Color Alone = new Color(0.55f, 0.75f, 1f, 0.9f);

        private sealed class Ring
        {
            public LineRenderer Line;
            public Vector3 At;
            public float Radius = -1f;
            public Color Colour;
        }

        private static readonly List<Ring> Pool = new List<Ring>();
        private static readonly HashSet<HodNet> Show = new HashSet<HodNet>();
        private static GameObject _root;
        private static Material _material;
        private static bool _materialTried;

        private static HodNet _hovered;
        private static float _hoverUntil;

        public static void Hovered(HodNet net)
        {
            _hovered = net;
            _hoverUntil = Time.time + 1f;
        }

        public static void Tick()
        {
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;

            var player = Player.m_localPlayer;
            if (player == null || !HodConfig.Enabled.Value || HodRuntime.Shut)
            {
                Finish(0);
                return;
            }

            var nets = HodNetwork.All;
            var ghosts = HodNetwork.GhostJibs;
            var radius = HodNetwork.Radius;

            Show.Clear();

            if (player.InPlaceMode())
            {
                var near = 4f * radius * radius;
                foreach (var net in nets)
                    foreach (var jib in net.Members)
                        if (jib != null
                            && (jib.transform.position - player.transform.position).sqrMagnitude
                               <= near)
                        {
                            Show.Add(net);
                            break;
                        }
            }

            if (_hovered != null && Time.time < _hoverUntil) Show.Add(_hovered);

            var drawn = 0;

            for (var g = 0; g < ghosts.Count; g++)
            {
                var ghost = ghosts[g];
                if (ghost == null || !ghost.gameObject.activeInHierarchy) continue;

                var at = ghost.transform.position;
                var joins = HodNetwork.Joins(at, ghost);

                if (joins)
                {
                    var served = HodNetwork.Serving(at);
                    if (served != null) Show.Add(served);
                }

                Draw(ref drawn, at, radius, joins ? Joins : Alone);
            }

            foreach (var net in nets)
            {
                if (!Show.Contains(net)) continue;

                foreach (var jib in net.Members)
                    if (jib != null) Draw(ref drawn, jib.transform.position, radius, Built);
            }

            Finish(drawn);
        }

        private static void Draw(ref int index, Vector3 at, float radius, Color colour)
        {
            if (radius <= 0.5f || !Ready()) return;

            while (Pool.Count <= index) Pool.Add(NewRing());

            var ring = Pool[index++];
            if (ring.Line == null) return;

            ring.Line.enabled = true;

            if ((ring.At - at).sqrMagnitude < 0.0004f && Mathf.Approximately(ring.Radius, radius))
            {
                if (ring.Colour != colour) Paint(ring, colour);
                return;
            }

            ring.At = at;
            ring.Radius = radius;
            Paint(ring, colour);

            ring.Line.positionCount = Segments;
            for (var i = 0; i < Segments; i++)
            {
                var a = i / (float)Segments * Mathf.PI * 2f;
                ring.Line.SetPosition(i, OnGround(new Vector3(
                    at.x + Mathf.Cos(a) * radius, at.y, at.z + Mathf.Sin(a) * radius)));
            }
        }

        private static void Paint(Ring ring, Color colour)
        {
            ring.Colour = colour;
            ring.Line.startColor = colour;
            ring.Line.endColor = colour;
        }

        private static void Finish(int drawn)
        {
            for (var i = drawn; i < Pool.Count; i++)
                if (Pool[i].Line != null) Pool[i].Line.enabled = false;
        }

        private static Vector3 OnGround(Vector3 world)
        {
            float ground;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(world, out ground))
                world.y = ground;

            world.y += 0.15f;
            return world;
        }

        private static Ring NewRing()
        {
            var go = new GameObject("hod_reach");
            go.transform.SetParent(_root.transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.sharedMaterial = _material;
            line.widthMultiplier = 0.12f;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            line.enabled = false;

            return new Ring { Line = line };
        }

        private static bool Ready()
        {
            if (_material == null && !Borrow()) return false;

            if (_root == null)
            {
                _root = new GameObject("hod_reach_rings");
                Pool.Clear();
            }

            return true;
        }

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
            };

            foreach (var name in names)
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;

                _material = new Material(shader);
                if (_material.HasProperty("_TintColor")) _material.SetColor("_TintColor", Color.white);

                GrovePlugin.Log.LogInfo("Hod reach rings drawn with " + name + ".");
                return true;
            }

            GrovePlugin.Log.LogWarning(
                "No shader available to draw the hod jib's reach rings - they stay off. "
                + "The jib's reach itself is unaffected.");
            return false;
        }

        /// <summary>
        /// What `hod ring` prints: the rings drawn as of the last Tick, counted by colour, and
        /// the facts they are drawn from. One line of name=value tokens, in a fixed order.
        ///
        /// A ring is "drawn" when its line renderer is enabled, which is exactly what Tick sets
        /// per frame and what the player sees, so this reads the end of the pipeline and not a
        /// copy of the decision. joins is the green ghost ring (the jib being placed would join a
        /// network), alone the blue one, built the gold ring on a standing jib. material=no means
        /// no shader could be borrowed and nothing is ever drawn, whatever the counts say.
        /// </summary>
        public static string Describe()
        {
            var built = 0;
            var joins = 0;
            var alone = 0;
            var other = 0;
            var points = 0;

            foreach (var ring in Pool)
            {
                if (ring.Line == null || !ring.Line.enabled) continue;

                if (ring.Colour == Built) built++;
                else if (ring.Colour == Joins) joins++;
                else if (ring.Colour == Alone) alone++;
                else other++;

                points = ring.Line.positionCount;
            }

            var nets = HodNetwork.All.Count;
            var ghosts = 0;
            foreach (var ghost in HodNetwork.GhostJibs)
                if (ghost != null && ghost.gameObject.activeInHierarchy) ghosts++;

            var player = Player.m_localPlayer;

            return "hod ring built=" + built + " joins=" + joins + " alone=" + alone + " other=" + other
                   + " points=" + points + " ghosts=" + ghosts + " nets=" + nets
                   + " radius=" + HodNetwork.Radius.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                   + " placing=" + (player != null && player.InPlaceMode() ? "yes" : "no")
                   + " material=" + (_material != null ? "yes" : "no");
        }

        public static void Forget()
        {
            _hovered = null;
            _root = null;
            Pool.Clear();
        }
    }
}
