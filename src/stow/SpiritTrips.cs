using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Stow
{
    /// <summary>
    /// What every client needs to draw the spirits of one post, published on that post's
    /// own ZDO.
    ///
    /// **No new networked prefab, deliberately.** The obvious way to let everyone see a
    /// spirit is to register one with ZNetScene and give it a ZDO - and that would mean a
    /// prefab name frozen forever, because renaming it later discards every ZDO that
    /// carries it. Stow already lost a post to exactly that failure. A spirit is a
    /// transient visual that nothing needs to persist, so paying a permanent name for it
    /// would be buying a liability with no asset.
    ///
    /// What is replicated is the **trip, not the position**. A flight is a start point, an
    /// end point, a moment and a duration; every client can compute the arc from those, so
    /// the ZDO is written once per trip rather than once per frame, and the motion is
    /// smooth everywhere because it is local. The moment comes from ZNet's shared clock,
    /// which is the only reason two clients agree about where the spirit is.
    ///
    /// The format is deliberately legible - a ZDO is not a place you can put a breakpoint:
    ///
    ///     fx,fy,fz:tx,ty,tz:prefab:start:duration;  ...one per spirit
    /// </summary>
    internal static class SpiritTrips
    {
        public const string ZTrips = "stowTrips";

        /// <summary>
        /// Everything needed to place a spirit at any moment, on any client.
        ///
        /// The control point is stored rather than recomputed, because it is derived from
        /// the configured cruise height - and two clients with different config would
        /// otherwise draw two different arcs between the same two points.
        /// </summary>
        public struct Flight
        {
            public Vector3 From;
            public Vector3 Control;
            public Vector3 To;
            public double Start;
            public float Duration;
            public string Cargo;

            public double EndsAt { get { return Start + Duration; } }

            /// <summary>
            /// Eased at both ends, and clamped past the end so a spirit that has arrived
            /// hovers at the chest rather than shooting past it. Holding there is what
            /// draws the unloading pause without the pause needing to be replicated.
            /// </summary>
            public Vector3 At(double now)
            {
                var t = Duration <= 0f ? 1f : Mathf.Clamp01((float)((now - Start) / Duration));
                t = t * t * (3f - 2f * t);

                var inverse = 1f - t;
                return inverse * inverse * From
                       + 2f * inverse * t * Control
                       + t * t * To;
            }
        }

        /// <summary>
        /// The shared clock. Every client computing the same arc from the same moment is
        /// the whole mechanism, so this must not fall back to something local while a
        /// network exists.
        /// </summary>
        public static double Now
        {
            get
            {
                return ZNet.instance != null
                    ? ZNet.instance.GetTimeSeconds()
                    : Time.time;
            }
        }

        public static string Read(ZNetView nview)
        {
            return nview == null || !nview.IsValid() ? "" : nview.GetZDO().GetString(ZTrips, "");
        }

        /// <summary>
        /// Publishes, but only when the text actually changed.
        ///
        /// A ZDO set marks the object dirty and queues it for every peer, so writing an
        /// identical string every frame would be a network message per frame per post for
        /// a value nobody needs told again.
        /// </summary>
        public static void Publish(ZNetView nview, string trips)
        {
            if (nview == null || !nview.IsValid()) return;
            if (Read(nview) == trips) return;

            nview.GetZDO().Set(ZTrips, trips ?? "");
        }

        // ------------------------------------------------------------------ the wire

        public static string Encode(IEnumerable<Flight> flights)
        {
            var text = new StringBuilder();

            foreach (var flight in flights)
            {
                if (text.Length > 0) text.Append(';');

                Vector(text, flight.From); text.Append(':');
                Vector(text, flight.Control); text.Append(':');
                Vector(text, flight.To); text.Append(':');

                text.Append(flight.Cargo ?? "").Append(':');
                text.Append(flight.Start.ToString("0.###", Culture)).Append(':');
                text.Append(flight.Duration.ToString("0.###", Culture));
            }

            return text.ToString();
        }

        public static List<Flight> Decode(string text)
        {
            var flights = new List<Flight>();
            if (string.IsNullOrEmpty(text)) return flights;

            foreach (var entry in text.Split(';'))
            {
                var parts = entry.Split(':');
                if (parts.Length != 6) continue;

                Vector3 from, control, to;
                double start;
                float duration;

                if (!Vector(parts[0], out from)) continue;
                if (!Vector(parts[1], out control)) continue;
                if (!Vector(parts[2], out to)) continue;

                if (!double.TryParse(parts[4], NumberStyles.Float, Culture, out start)) continue;
                if (!float.TryParse(parts[5], NumberStyles.Float, Culture, out duration)) continue;

                flights.Add(new Flight
                {
                    From = from,
                    Control = control,
                    To = to,
                    Cargo = parts[3],
                    Start = start,
                    Duration = duration
                });
            }

            return flights;
        }

        /// <summary>
        /// Invariant, always, on both ends.
        ///
        /// This is not pedantry: on a Dutch machine - which is where this was written -
        /// the default float format writes "1,23", and the separator here is a comma. The
        /// string would round-trip perfectly on one locale and fall apart between two
        /// players with different ones, which is the worst kind of bug to find.
        /// </summary>
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        private static void Vector(StringBuilder text, Vector3 value)
        {
            text.Append(value.x.ToString("0.##", Culture)).Append(',')
                .Append(value.y.ToString("0.##", Culture)).Append(',')
                .Append(value.z.ToString("0.##", Culture));
        }

        private static bool Vector(string text, out Vector3 value)
        {
            value = Vector3.zero;

            var parts = (text ?? "").Split(',');
            if (parts.Length != 3) return false;

            float x, y, z;
            if (!float.TryParse(parts[0], NumberStyles.Float, Culture, out x)) return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, Culture, out y)) return false;
            if (!float.TryParse(parts[2], NumberStyles.Float, Culture, out z)) return false;

            value = new Vector3(x, y, z);
            return true;
        }
    }
}
