using System.Collections.Generic;
using UnityEngine;

namespace Stow
{
    /// <summary>
    /// The spirit that flies a stack from the post to a chest.
    ///
    /// Everything it does while it is in the air lives here: tumble, bob, drift, breathe,
    /// and cross the room. One component rather than five, for the reason Vaettir's
    /// ForestSpirit gives - they all read the same clock, and five components each
    /// reaching for the same transform is how they end up fighting. Nothing here writes
    /// the same field twice: flight writes the root's position, bob writes the body's,
    /// tumble writes the hoop's rotation, drift writes the motes', sway writes the
    /// sling's, and pulse writes a light.
    ///
    /// It has no body, no walk cycle and no ragdoll, which is not a limitation being
    /// worked around - it is why a bodiless spirit was the right answer. A mod with no
    /// Unity editor can author motion like this in code and cannot author an animation
    /// controller.
    ///
    /// It carries no ZNetView and is never registered with ZNetScene - and yet every
    /// player sees it. What is replicated is the trip, on the post's own ZDO, and every
    /// client builds its own local spirit to fly it. See SpiritTrips for why that is
    /// better than a networked prefab: a spirit needs no persistence, and a registered
    /// prefab would cost a name frozen forever.
    ///
    /// So this is a *view*. It holds no state that anyone else needs and makes no
    /// decisions; it is handed a Flight and draws it. Position comes from the shared
    /// clock rather than from accumulated deltaTime, which is what makes the owner's
    /// spirit and everyone else's occupy the same point in the air.
    /// </summary>
    internal class Carrier : MonoBehaviour
    {
        // --------------------------------------------------------------- tunables

        /// <summary>
        /// Radius the motes ride at. Matches ORBIT in vaettir/tools/spirit_core.py, and it
        /// has to: the hoop mesh is a torus built at that radius, so a mismatch puts the
        /// beads inside or outside the circle they are supposed to be riding.
        ///
        /// Was 0.21, against Vaettir's 0.34, because Stow generated its own smaller copy of
        /// the spirit. Now that both wear the same mesh this is the mesh's number, and the
        /// carrier is the size the spirit is. CarrierScale is where to make it smaller if it
        /// crowds a storage room - that is a preference, and this is a fact about the model.
        /// </summary>
        public const float Orbit = 0.34f;

        /// <summary>Degrees per second per axis.</summary>
        public static float Spin = 24f;

        public static float BobHeight = 0.045f;
        public static float BobPeriod = 3.1f;

        /// <summary>Degrees per second a mote slides along the hoop.</summary>
        public static float DriftMin = 4f;
        public static float DriftMax = 13f;

        /// <summary>
        /// Vanilla's amber, measured rather than picked. guard_stone's material carries
        /// _EmissionColor = (1.934, 1.017, 0.185), which normalises to this - deeper and
        /// far more orange than the (1, 0.74, 0.30) that was here, which was a guess and
        /// read as pale yellow next to anything the game lights with.
        /// </summary>
        public static Color LightColour = new Color(1f, 0.53f, 0.10f);
        public static float LightRange = 6f;
        public static float PulseDepth = 0.26f;
        public static float PulsePeriod = 4.4f;

        /// <summary>Seconds to shrink away when dismissed.</summary>
        public static float FadeTime = 0.45f;

        // --------------------------------------------------------------- wiring

        public Transform Body;
        public Transform Hoop;

        /// <summary>The pendulum pivot, at the bottom of the hoop. Sway turns this.</summary>
        public Transform Sling;

        /// <summary>Where the load actually hangs, at the end of the sling.</summary>
        public Transform Hook;

        /// <summary>
        /// The rings, not the beads. Turning a ring turns everything on it, so one Rotate
        /// per ring replaces one per bead and the beads keep their spacing for free.
        /// </summary>
        private readonly List<Transform> _rings = new List<Transform>();

        private readonly List<Transform> _motes = new List<Transform>();
        private readonly List<float> _moteAngle = new List<float>();
        private readonly List<float> _moteRate = new List<float>();

        private Light _light;

        // --------------------------------------------------------------- state

        private enum Phase { Flying, Fading }

        private Phase _phase = Phase.Flying;

        private SpiritTrips.Flight _flight;
        private bool _hasFlight;
        private float _fade;

        private Vector3 _spin;
        private Vector3 _rest;
        private float _bobPhase;
        private float _pulsePhase;
        private float _scale = 1f;

        private GameObject _cargo;


        // --------------------------------------------------------------- setup

        /// <summary>
        /// Start, not Awake, and that is load-bearing.
        ///
        /// AddComponent runs Awake synchronously on an active object, so a component
        /// added before its fields are assigned wakes up with all of them null - no
        /// motes, no bob, and a light parented to the root instead of the body. The
        /// builder assigns Body, Hoop and the rest on the lines after AddComponent, and
        /// Start is the first callback guaranteed to run after that. Vaettir's spirit
        /// uses Start for the same reason.
        /// </summary>
        private void Start()
        {
            var random = new System.Random(GetInstanceID());

            // Three independent rates in local space is a tumble rather than a spin:
            // each frame rotates the result of the last, so the axes never resolve into
            // a repeating cycle. Whole-number ratios do close the loop - 10/20/30 repeats
            // every 36 seconds and the eye catches it - so the range is awkward on purpose.
            _spin = new Vector3(Signed(random), Signed(random), Signed(random));

            _bobPhase = (float)random.NextDouble() * Mathf.PI * 2f;
            _pulsePhase = (float)random.NextDouble() * Mathf.PI * 2f;

            _scale = transform.localScale.x;
            _rest = Body != null ? Body.localPosition : Vector3.zero;

            CollectMotes(random);
            MakeLight();
        }

        private static float Signed(System.Random random)
        {
            var speed = 1f + (float)random.NextDouble();
            return random.Next(2) == 0 ? -speed : speed;
        }

        private void CollectMotes(System.Random random)
        {
            if (Hoop == null) return;

            // Hoop is the rings container, not a ring, so the beads are grandchildren. It
            // used to be the single drawn torus with the motes parented straight onto it;
            // walking only its direct children now finds two rings and no beads, and the
            // symptom is a spirit whose lights sit perfectly still.
            foreach (Transform ring in Hoop)
            {
                if (!ring.name.StartsWith("ring")) continue;

                _rings.Add(ring);

                foreach (Transform child in ring)
                {
                    if (!child.name.StartsWith("mote")) continue;

                    _motes.Add(child);

                    // Read the angle it was placed at rather than assuming an even
                    // spacing, so drift starts from wherever the builder put it.
                    _moteAngle.Add(
                        Mathf.Atan2(child.localPosition.y, child.localPosition.x)
                        * Mathf.Rad2Deg);

                    var rate = DriftMin
                               + (float)random.NextDouble() * (DriftMax - DriftMin);
                    _moteRate.Add(random.Next(2) == 0 ? -rate : rate);
                }
            }
        }

        private void MakeLight()
        {
            var holder = new GameObject("carrier_light");
            holder.transform.SetParent(Body != null ? Body : transform, false);

            _light = holder.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = LightRange;
            _light.color = LightColour;
            _light.intensity = 1.1f;

            // No shadows. A point light inside the thing that is itself the light source
            // would cast its own hoop across the floor in seven directions, and the cost
            // is real for something a storage room might have several of.
            _light.shadows = LightShadows.None;
        }

        // --------------------------------------------------------------- orders

        /// <summary>
        /// Fly the given trip. Everything about where it is comes from this and the clock.
        ///
        /// The arc is the entire reason this needs no pathfinding. A courier that walked
        /// would need to know about doorways, stairs and the chest being behind a pillar;
        /// one that flies over the top needs to know none of it, and "it is a spirit" is a
        /// better answer to why it can than any amount of navmesh would be.
        /// </summary>
        public void Follow(SpiritTrips.Flight flight)
        {
            if (_phase == Phase.Fading) return;

            _flight = flight;
            _hasFlight = true;
        }

        /// <summary>Hangs a model under the sling. Whatever was there is destroyed.</summary>
        public void Carry(GameObject model)
        {
            Release();
            if (model == null) return;

            _cargo = model;
            _cargo.transform.SetParent(Hook != null ? Hook : transform, false);
            _cargo.transform.localPosition = Vector3.zero;
            _cargo.transform.localRotation = Quaternion.identity;
        }

        public void Release()
        {
            if (_cargo == null) return;

            Destroy(_cargo);
            _cargo = null;
        }

        /// <summary>
        /// Goes out rather than pops.
        ///
        /// Shrinking is not decoration: a light that blinks off reads as a thing being
        /// deleted, and this one is meant to read as a thing leaving. The cargo goes
        /// first, so the last frame is never a crate hanging in the air on its own.
        /// </summary>
        public void Dismiss()
        {
            if (_phase == Phase.Fading) return;

            Release();
            _fade = FadeTime;
            _phase = Phase.Fading;
        }

        // --------------------------------------------------------------- per frame

        private void Update()
        {
            var delta = Time.deltaTime;

            switch (_phase)
            {
                case Phase.Flying:
                    if (_hasFlight) transform.position = _flight.At(SpiritTrips.Now);
                    break;

                case Phase.Fading:
                    _fade -= delta;
                    if (_fade <= 0f) { Destroy(gameObject); return; }

                    var shrink = Mathf.Clamp01(_fade / Mathf.Max(0.01f, FadeTime));
                    transform.localScale = Vector3.one * _scale * shrink;
                    if (_light != null) _light.intensity = 1.1f * shrink;
                    break;
            }

            Animate(Time.time, delta);
        }

        private void Animate(float time, float delta)
        {
            // The whole arrangement tumbles...
            if (Hoop != null) Hoop.Rotate(_spin * Spin * delta, Space.Self);

            // ...and each ring turns about its own axis inside that, which is what makes
            // the beads travel along their circles rather than merely be carried around by
            // the tumble. Local space, not world: a ring is built in its local XY plane and
            // then tilted, so its own Z is the axis its beads go round. Rotating in world
            // space would swing every ring about one shared axis and collapse the two back
            // into looking like one. Same rate on every ring, so they stay in step.
            var step = Spin * delta;
            for (var i = 0; i < _rings.Count; i++)
            {
                if (_rings[i] == null) continue;
                _rings[i].Rotate(0f, 0f, step, Space.Self);
            }

            if (Body != null)
            {
                var rise = Mathf.Sin(time * (Mathf.PI * 2f / BobPeriod) + _bobPhase);
                Body.localPosition = _rest + new Vector3(0f, rise * BobHeight, 0f);
            }

            Drift(delta);
            Sway(time);
            Pulse(time);
        }

        /// <summary>
        /// Slides each mote along the hoop at its own rate.
        ///
        /// A mote rides (cos, sin, 0) because the hoop mesh lies in its own local XY
        /// plane - the torus is built in Blender's XZ with its axis along +Y, and the
        /// exporter maps Blender Y to Unity Z. If that is ever wrong the symptom is
        /// unmistakable: the beads orbit through the hoop rather than along it.
        /// </summary>
        private void Drift(float delta)
        {
            for (var i = 0; i < _motes.Count; i++)
            {
                if (_motes[i] == null) continue;

                _moteAngle[i] += _moteRate[i] * delta;

                var radians = _moteAngle[i] * Mathf.Deg2Rad;
                _motes[i].localPosition = new Vector3(Mathf.Cos(radians) * Orbit,
                                                      Mathf.Sin(radians) * Orbit, 0f);
            }
        }

        /// <summary>
        /// The load swings, slightly, on two axes at rates that do not divide.
        ///
        /// This is the whole reason the sling hangs off the root and not off the hoop.
        /// Parented to the hoop it would inherit the tumble, and a crate rolling end over
        /// end under a spirit reads as something dropped rather than something carried.
        /// </summary>
        private void Sway(float time)
        {
            if (Sling == null) return;

            Sling.localRotation = Quaternion.Euler(
                Mathf.Sin(time * 1.7f) * 4.5f, 0f, Mathf.Cos(time * 1.3f) * 5.5f);
        }

        /// <summary>
        /// Breathing, on the light only.
        ///
        /// Never on the material. The glow is *borrowed* off a vanilla prefab and is
        /// shared with every other object using it, so writing to it would set half the
        /// world pulsing. Vaettir's spirit also drives an emission property block, which
        /// is skipped here: it depends on the borrowed shader happening to have
        /// _EmissionColor, and the light is the part that is certain to work.
        /// </summary>
        private void Pulse(float time)
        {
            if (_light == null || _phase == Phase.Fading) return;

            var breath = Mathf.Sin(time * (Mathf.PI * 2f / PulsePeriod) + _pulsePhase);
            _light.intensity = 1.1f * (1f + breath * PulseDepth);
        }
    }
}
