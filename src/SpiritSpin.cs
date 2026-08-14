using UnityEngine;

namespace Grove
{
    /// <summary>
    /// Turns the spirit's ring on all three axes at once.
    ///
    /// Three independent rates applied in local space, which is a genuine tumble
    /// rather than a spin: because each frame's rotation is applied to the result of
    /// the last one, the axes never resolve into a repeating cycle and the ring keeps
    /// presenting a new orientation. A single-axis spin reads as machinery, and the
    /// one thing this creature must not read as is machinery.
    ///
    /// Rates are irrational-ish multiples of each other on purpose. Whole-number
    /// ratios *do* close the loop - 10/20/30 degrees a second repeats every 36
    /// seconds and the eye catches it - so the defaults are deliberately awkward.
    /// </summary>
    internal class SpiritSpin : MonoBehaviour
    {
        /// <summary>Degrees per second, per axis. Slow: this is drifting, not whirling.</summary>
        public static float MinRate = 6f;
        public static float MaxRate = 19f;

        private Vector3 _rate;

        private void Start()
        {
            // Seeded off the ZDO rather than left to chance, so every client watching
            // the same spirit sees it at the same angle. Nothing depends on that, but
            // two players standing side by side describing different things is the
            // kind of small wrongness that makes a mod feel broken.
            var nview = GetComponentInParent<ZNetView>();
            var seed = nview != null && nview.IsValid()
                ? nview.GetZDO().m_uid.GetHashCode()
                : GetInstanceID();

            var random = new System.Random(seed);

            _rate = new Vector3(Rate(random), Rate(random), Rate(random));
        }

        private float Rate(System.Random random)
        {
            var speed = MinRate + (float)random.NextDouble() * (MaxRate - MinRate);
            return random.Next(2) == 0 ? -speed : speed;
        }

        private void Update()
        {
            transform.Rotate(_rate * Time.deltaTime, Space.Self);
        }
    }
}
