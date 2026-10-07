using System.Collections.Generic;
using UnityEngine;
using Stow;

namespace Hod
{
    /// <summary>One connected group of hod jibs: the jibs themselves and nothing else.</summary>
    internal sealed class HodNet
    {
        public readonly List<PostUpgrade> Members = new List<PostUpgrade>();
    }

    /// <summary>
    /// Where the bench service reaches, worked out from the jibs alone (LHM-77).
    ///
    /// A jib has a reach circle of HodRange. Two jibs belong to one network when one's centre lies
    /// inside the other's circle, and that is transitive, so a row of jibs each built on the edge of
    /// the last is one network however far it runs. A network covers the union of its circles.
    /// Every jib has the same radius, so "one's centre inside the other's" is symmetric and the
    /// grouping is a plain union-find over distances.
    ///
    /// <b>This replaces "the post carrying the jib".</b> The jib was always a free-standing piece
    /// that merely looked up the nearest stowing post, so nothing physical changes: a jib built
    /// beside a post is simply a jib, with its circle centred on itself. What changes is that no
    /// post is asked for, which is why a jib no longer needs one and why two of them extend each
    /// other. The prefab name stays hod_jib, so every jib already standing keeps its ZDO.
    ///
    /// Separate networks never mix. A point covered by two networks (two circles that overlap
    /// without either centre being inside the other) belongs to the network of the NEAREST jib
    /// centre, so a bench is served by exactly one set of chests and the count and the spend can
    /// never be asking different ones.
    ///
    /// Rebuilt only when the set of placed jibs or the range changes, compared member by member
    /// (a handful of jibs), once a frame at most. Not on a timer and not per call.
    /// </summary>
    internal static class HodNetwork
    {
        private static readonly List<PostUpgrade> Placed = new List<PostUpgrade>();
        private static readonly List<PostUpgrade> Ghosts = new List<PostUpgrade>();
        private static readonly List<PostUpgrade> Scratch = new List<PostUpgrade>();
        private static readonly List<HodNet> Nets = new List<HodNet>();

        private static float _builtRange = -1f;
        private static int _refreshedFrame = -1;

        public static float Radius
        {
            get { return Mathf.Max(0f, HodConfig.Range.Value); }
        }

        public static List<HodNet> All
        {
            get
            {
                Refresh();
                return Nets;
            }
        }

        public static List<PostUpgrade> GhostJibs
        {
            get
            {
                Refresh();
                return Ghosts;
            }
        }

        /// <summary>
        /// The network that serves a point, or null: the one whose nearest jib centre is within
        /// reach of it.
        /// </summary>
        public static HodNet Serving(Vector3 point)
        {
            Refresh();

            var radiusSq = Radius * Radius;
            HodNet best = null;
            var bestSq = float.MaxValue;

            for (var n = 0; n < Nets.Count; n++)
            {
                var members = Nets[n].Members;
                for (var m = 0; m < members.Count; m++)
                {
                    var jib = members[m];
                    if (jib == null) continue;

                    var distance = (jib.transform.position - point).sqrMagnitude;
                    if (distance > radiusSq || distance >= bestSq) continue;

                    bestSq = distance;
                    best = Nets[n];
                }
            }

            return best;
        }

        /// <summary>Whether a jib built at this point would join an existing network.</summary>
        public static bool Joins(Vector3 point, PostUpgrade except)
        {
            Refresh();

            var radiusSq = Radius * Radius;
            for (var i = 0; i < Placed.Count; i++)
            {
                var jib = Placed[i];
                if (jib == null || jib == except) continue;
                if ((jib.transform.position - point).sqrMagnitude <= radiusSq) return true;
            }

            return false;
        }

        public static HodNet NetOf(PostUpgrade jib)
        {
            Refresh();

            for (var n = 0; n < Nets.Count; n++)
                if (Nets[n].Members.Contains(jib)) return Nets[n];

            return null;
        }

        public static void Forget()
        {
            Placed.Clear();
            Ghosts.Clear();
            Nets.Clear();
            _builtRange = -1f;
            _refreshedFrame = -1;
        }

        private static void Refresh()
        {
            if (_refreshedFrame == Time.frameCount) return;
            _refreshedFrame = Time.frameCount;

            Scratch.Clear();
            Ghosts.Clear();
            PostUpgrade.CollectJibs(Scratch, Ghosts);

            var radius = Radius;
            if (Mathf.Approximately(radius, _builtRange) && SameAs(Scratch)) return;

            _builtRange = radius;
            Placed.Clear();
            Placed.AddRange(Scratch);
            Nets.Clear();

            var parent = new int[Placed.Count];
            for (var i = 0; i < parent.Length; i++) parent[i] = i;

            var radiusSq = radius * radius;
            for (var i = 0; i < Placed.Count; i++)
                for (var j = i + 1; j < Placed.Count; j++)
                {
                    var reach = (Placed[i].transform.position - Placed[j].transform.position)
                        .sqrMagnitude;
                    if (reach > radiusSq) continue;

                    parent[Root(parent, i)] = Root(parent, j);
                }

            var byRoot = new Dictionary<int, HodNet>();
            for (var i = 0; i < Placed.Count; i++)
            {
                var root = Root(parent, i);

                HodNet net;
                if (!byRoot.TryGetValue(root, out net))
                {
                    net = new HodNet();
                    byRoot[root] = net;
                    Nets.Add(net);
                }

                net.Members.Add(Placed[i]);
            }
        }

        private static int Root(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        private static bool SameAs(List<PostUpgrade> now)
        {
            if (now.Count != Placed.Count) return false;

            for (var i = 0; i < now.Count; i++)
                if (!ReferenceEquals(now[i], Placed[i])) return false;

            return true;
        }

        /// <summary>The hover text of a built jib, in place of the post-feeding line.</summary>
        public static string HoverText(PostUpgrade jib, string name, string what)
        {
            var net = NetOf(jib);
            HodRing.Hovered(net);

            string line;
            if (net == null || net.Members.Count < 2)
                line = "<color=grey>stands alone, reaching "
                       + Radius.ToString("0.#") + "m</color>";
            else
                line = "<color=#D9A441>linked with " + (net.Members.Count - 1)
                       + (net.Members.Count == 2 ? " other jib" : " other jibs")
                       + ", reaching " + Radius.ToString("0.#") + "m each</color>";

            return name + "\n<color=grey>" + what + "</color>\n" + line;
        }
    }
}
