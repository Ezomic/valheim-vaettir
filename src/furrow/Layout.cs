using System.Collections.Generic;
using UnityEngine;

namespace Furrow
{
    /// <summary>
    /// Where the extra seeds go, relative to the one the game already planted.
    ///
    /// The centre is never in this list. Vanilla places the seed under the cursor through
    /// its own TryPlacePiece, and everything here is what gets added around it, so index 0
    /// of a count of five is the game's and only four positions come back.
    /// </summary>
    internal static class Layout
    {
        public static List<Vector3> Offsets(SowShape shape, int count, float step, Quaternion rot)
        {
            var offsets = new List<Vector3>();
            if (count <= 1 || step <= 0f) return offsets;

            switch (shape)
            {
                case SowShape.Circle:
                    Ring(offsets, count - 1, step);
                    break;

                default:
                    Line(offsets, count - 1, step);
                    break;
            }

            // Built flat in local space and turned as a whole, so a row follows the way the
            // ghost is facing rather than the world axes. Doing it per point would let the
            // row bend as the player turned between one seed and the next.
            for (var i = 0; i < offsets.Count; i++)
                offsets[i] = rot * offsets[i];

            return offsets;
        }

        /// <summary>
        /// Alternating out from the centre, so the row grows evenly both ways rather than
        /// trailing off to one side. Four extras land at -1, +1, -2, +2 steps.
        /// </summary>
        private static void Line(List<Vector3> offsets, int extras, float step)
        {
            var placed = 0;
            var distance = 1;

            while (placed < extras)
            {
                foreach (var sign in new[] { -1, 1 })
                {
                    if (placed >= extras) break;

                    var d = sign * distance * step;

                    // Across the facing by default: x is the ghost's right. Along the facing
                    // puts the far end on ground the player cannot judge, which is why it is
                    // not the default, but it suits a narrow strip between two paths.
                    offsets.Add(FurrowConfig.RowAcrossFacing.Value
                        ? new Vector3(d, 0f, 0f)
                        : new Vector3(0f, 0f, d));

                    placed++;
                }

                distance++;
            }
        }

        /// <summary>
        /// One ring around the centre seed, sized so neighbours sit a step apart along the
        /// arc: circumference = extras * step, so radius = extras * step / 2pi.
        ///
        /// Deriving the radius from the count rather than fixing it is what keeps the shape
        /// honest at both ends - three seeds make a tight triangle around your cursor and
        /// twenty make a wide ring, and in neither case do two of them land on top of each
        /// other and get refused for want of space.
        /// </summary>
        private static void Ring(List<Vector3> offsets, int extras, float step)
        {
            var radius = Mathf.Max(step, extras * step / (2f * Mathf.PI));

            for (var i = 0; i < extras; i++)
            {
                var angle = i * 2f * Mathf.PI / extras;
                offsets.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }
    }
}
