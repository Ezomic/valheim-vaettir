using UnityEngine;

namespace Furrow
{
    /// <summary>
    /// Will this plant have room to grow?
    ///
    /// The game already asks this and answers it too late to help. Plant.UpdateHealth
    /// skips the whole test for the first ten seconds after planting, so a sapling with
    /// no room looks exactly like a healthy one until it quietly turns unhealthy - and
    /// a plant carrying m_destroyIfCantGrow deletes itself outright. Placement does not
    /// check it at all: the ghost is green, the seed is spent, and the loss shows up
    /// later somewhere you are no longer standing. That is how an oak sapling gets
    /// wasted with nothing on screen having said no.
    ///
    /// So the test is re-implemented here to run on the GHOST, before the click. It is
    /// a deliberate mirror of Plant.HaveGrowSpace rather than an approximation of it -
    /// same layer mask, same radius field, same rule about which neighbours count -
    /// because a preview that disagrees with the game is worse than no preview. The
    /// method is private, or it would simply be called.
    ///
    /// The rule itself is worth stating plainly, because it is not "is anything here":
    /// ANY collider on those layers blocks, EXCEPT another plant - and another plant
    /// blocks only when it is currently Healthy. That is why two saplings can be placed
    /// touching and both survive when neither has grown yet, and why the same pair fails
    /// once one of them takes.
    /// </summary>
    internal static class Room
    {
        private static int _mask;

        /// <summary>
        /// Vanilla's own space mask. Built once and cached the way Plant does it -
        /// LayerMask.GetMask is a string lookup per layer and this runs per frame.
        /// </summary>
        private static int Mask
        {
            get
            {
                if (_mask == 0)
                    _mask = LayerMask.GetMask("Default", "static_solid", "Default_small",
                                              "piece", "piece_nonsolid");
                return _mask;
            }
        }

        /// <summary>
        /// True when a plant of this kind, planted here, would have space to grow.
        ///
        /// <paramref name="self"/> is the ghost's own Plant, excluded the way the
        /// original excludes the plant doing the asking. The ghost sits on the "ghost"
        /// layer, which is not in the mask, so its own colliders are already invisible
        /// to this - but a ghost that has been moved onto a real layer by some other
        /// mod would otherwise block itself, and that failure would look like the
        /// preview simply always saying no.
        /// </summary>
        public static bool Free(Vector3 at, Plant self)
        {
            if (self == null) return true;

            var radius = self.m_growRadius;
            if (radius <= 0f) return true;

            foreach (var hit in Physics.OverlapSphere(at, radius, Mask))
            {
                if (hit == null) continue;

                // GetComponent, not GetComponentInParent - vanilla checks the collider's
                // own object, and widening it here would let a plant excuse a wall it
                // happens to be parented under.
                var plant = hit.GetComponent<Plant>();
                if (plant == null) return false;
                if (plant != self && plant.GetStatus() == Plant.Status.Healthy) return false;
            }

            // Vines get a second, larger sweep in the original. Kept, because a plant
            // that carries the field is one this would otherwise clear wrongly.
            if (self.m_growRadiusVines > 0f)
            {
                foreach (var hit in Physics.OverlapSphere(at, self.m_growRadiusVines, Mask))
                {
                    if (hit == null) continue;
                    if (hit.GetComponentInParent<Vine>() != null) return false;
                }
            }

            return true;
        }
    }
}
