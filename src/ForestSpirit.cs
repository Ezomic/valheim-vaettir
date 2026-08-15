using System.Collections.Generic;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// Everything the spirit does while it is standing there: tumble, hover, breathe,
    /// and notice you.
    ///
    /// One component rather than four, because all four read the same number - how
    /// awake it is - and four components each measuring the player's distance and
    /// each reaching for the same transform is how they end up fighting. Bob writes
    /// the root's position, spin writes the hoop's rotation, drift writes the motes',
    /// and pulse writes a light and a property block. Nothing writes the same field
    /// twice.
    ///
    /// The creature has no body, so this *is* the creature: there is no walk cycle to
    /// blend with and no ragdoll to fall into. Which is most of why a bodiless spirit
    /// was the right answer - a mod with no Unity editor can author motion like this
    /// and cannot author an animation controller.
    /// </summary>
    internal class ForestSpirit : MonoBehaviour, Hoverable, Interactable
    {
        // --------------------------------------------------------------- tunables

        /// <summary>Degrees per second per axis, asleep and fully awake.</summary>
        public static float SpinIdle = 6f;
        public static float SpinAwake = 19f;

        /// <summary>Metres, and seconds for a full rise and fall.</summary>
        public static float BobHeight = 0.07f;
        public static float BobPeriod = 4.3f;

        /// <summary>How far the light swings either side of its resting brightness.</summary>
        public static float PulseDepth = 0.35f;
        public static float PulsePeriod = 5.7f;
        public static float LightRange = 7f;
        public static Color LightColour = new Color(1f, 0.74f, 0.30f);

        /// <summary>Degrees per second a mote slides along the hoop.</summary>
        public static float DriftMin = 3f;
        public static float DriftMax = 11f;

        /// <summary>It is fully awake at Near and fully asleep beyond Far.</summary>
        public static float NearRange = 4f;
        public static float FarRange = 16f;

        /// <summary>Radius the motes ride at. Matches ORBIT in tools/spirit_core.py.</summary>
        public static float Orbit = 0.34f;

        // --------------------------------------------------------------- state

        public Transform Hoop;
        public Transform Heart;

        private readonly List<Transform> _motes = new List<Transform>();
        private readonly List<float> _moteAngle = new List<float>();
        private readonly List<float> _moteRate = new List<float>();

        private Light _light;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;

        private Vector3 _spin;
        private Vector3 _rest;
        private float _bobPhase;
        private float _pulsePhase;
        private float _wake;

        private static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");

        // --------------------------------------------------------------- setup

        private void Start()
        {
            // Seeded off the ZDO rather than left to chance, so every client watching
            // the same spirit sees it at the same angle. Nothing depends on that, but
            // two players describing different things is the kind of small wrongness
            // that makes a mod feel broken.
            var nview = GetComponentInParent<ZNetView>();
            var seed = nview != null && nview.IsValid()
                ? nview.GetZDO().m_uid.GetHashCode()
                : GetInstanceID();

            var random = new System.Random(seed);

            // Three independent rates applied in local space is a genuine tumble
            // rather than a spin: each frame rotates the result of the last, so the
            // axes never resolve into a repeating cycle. Whole-number ratios do close
            // the loop - 10/20/30 repeats every 36 seconds and the eye catches it -
            // so the range is deliberately awkward.
            _spin = new Vector3(Signed(random), Signed(random), Signed(random));

            _bobPhase = (float)random.NextDouble() * Mathf.PI * 2f;
            _pulsePhase = (float)random.NextDouble() * Mathf.PI * 2f;

            _rest = transform.localPosition;

            CollectMotes(random);
            MakeLight();

            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
        }

        private float Signed(System.Random random)
        {
            var speed = 1f + (float)random.NextDouble();
            return random.Next(2) == 0 ? -speed : speed;
        }

        private void CollectMotes(System.Random random)
        {
            if (Hoop == null) return;

            foreach (Transform child in Hoop)
            {
                if (!child.name.StartsWith("mote")) continue;

                _motes.Add(child);

                // Read the angle it was placed at rather than assuming an even
                // spacing, so drift starts from wherever the spawner put it.
                _moteAngle.Add(Mathf.Atan2(child.localPosition.y, child.localPosition.x)
                               * Mathf.Rad2Deg);

                var rate = DriftMin + (float)random.NextDouble() * (DriftMax - DriftMin);
                _moteRate.Add(random.Next(2) == 0 ? -rate : rate);
            }
        }

        private void MakeLight()
        {
            _light = GetComponentInChildren<Light>();
            if (_light != null) return;

            var holder = new GameObject("spirit_light");
            holder.transform.SetParent(transform, false);

            _light = holder.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = LightRange;
            _light.color = LightColour;
            _light.intensity = 1f;

            // No shadows. A point light inside a creature that is itself the light
            // source would cast its own geometry across the ground in seven directions,
            // and the cost is real for something a base might hold several of.
            _light.shadows = LightShadows.None;
        }

        // --------------------------------------------------------------- per frame

        private void Update()
        {
            var delta = Time.deltaTime;
            var time = Time.time;

            Wake(delta);

            var liveliness = Mathf.Lerp(SpinIdle, SpinAwake, _wake);

            if (Hoop != null)
                Hoop.Rotate(_spin * liveliness * delta, Space.Self);

            Bob(time);
            Drift(delta, liveliness / SpinAwake);
            Pulse(time);
        }

        /// <summary>
        /// How awake it is, eased rather than snapped.
        ///
        /// Lerped towards the target instead of set to it, so walking briskly past one
        /// does not make it flick on and off. The rate is slow enough to read as the
        /// thing noticing you rather than as a trigger firing.
        /// </summary>
        private void Wake(float delta)
        {
            var player = Player.m_localPlayer;
            var target = 0f;

            if (player != null)
            {
                var distance = Vector3.Distance(player.transform.position, transform.position);
                target = 1f - Mathf.InverseLerp(NearRange, FarRange, distance);
            }

            _wake = Mathf.MoveTowards(_wake, target, delta * 0.6f);
        }

        private void Bob(float time)
        {
            var rise = Mathf.Sin(time * (Mathf.PI * 2f / BobPeriod) + _bobPhase);

            // Bobs even when asleep, at a third of the height. A light that goes
            // completely still reads as switched off rather than as resting.
            var height = BobHeight * Mathf.Lerp(0.33f, 1f, _wake);

            transform.localPosition = _rest + new Vector3(0f, rise * height, 0f);
        }

        /// <summary>
        /// Slides each mote along the hoop at its own rate.
        ///
        /// The hoop mesh comes out of Blender lying in its local XY plane - the torus
        /// is built in Blender's XZ with its axis along +Y, and the exporter maps
        /// Blender Y to Unity Z. So a mote rides (cos, sin, 0), and if that turns out
        /// to be the wrong plane in game the symptom is unmistakable: the beads will
        /// orbit through the hoop rather than along it.
        /// </summary>
        private void Drift(float delta, float liveliness)
        {
            for (var i = 0; i < _motes.Count; i++)
            {
                if (_motes[i] == null) continue;

                _moteAngle[i] += _moteRate[i] * liveliness * delta;

                var radians = _moteAngle[i] * Mathf.Deg2Rad;
                _motes[i].localPosition = new Vector3(Mathf.Cos(radians) * Orbit,
                                                      Mathf.Sin(radians) * Orbit, 0f);
            }
        }

        /// <summary>
        /// Breathing.
        ///
        /// Driven on a light and on a property block, never on the material itself:
        /// the spirit's material is *borrowed* off a vanilla prefab and shared with
        /// every other object using it, so writing to it would set half the world
        /// glowing. A property block writes per renderer and touches nothing else.
        ///
        /// The light is the part that is certain to work. Whether the emission write
        /// lands depends on the borrowed shader actually having _EmissionColor, which
        /// Valheim's custom shaders may not - hence the guard, and hence not relying
        /// on it for the effect to read.
        /// </summary>
        private void Pulse(float time)
        {
            var breath = Mathf.Sin(time * (Mathf.PI * 2f / PulsePeriod) + _pulsePhase);
            var level = 1f + breath * PulseDepth * Mathf.Lerp(0.4f, 1f, _wake);

            if (_light != null)
                _light.intensity = Mathf.Lerp(0.5f, 1.6f, _wake) * level;

            if (_renderers == null || _block == null) return;

            var colour = LightColour * level;

            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;

                var material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(EmissionColour)) continue;

                renderer.GetPropertyBlock(_block);
                _block.SetColor(EmissionColour, colour);
                renderer.SetPropertyBlock(_block);
            }
        }

        // --------------------------------------------------------------- communing

        public string GetHoverName()
        {
            return GroveConfig.SpiritName.Value;
        }

        public string GetHoverText()
        {
            return Localization.instance.Localize(
                GetHoverName() + "\n[<color=yellow><b>$KEY_Use</b></color>] commune");
        }

        /// <summary>
        /// You speak to it, and it folds itself into the heartwood for you to carry.
        ///
        /// One press, not a fight. The whole chain up to here was violent - an hour of
        /// killing greydwarfs to feed a seed - and having it end in one more killing
        /// would make the spirit just another thing in the forest with loot in it.
        ///
        /// So the heartwood is its home rather than its heart. Mechanically identical -
        /// an item appears and the spirit stops standing there - but it is the whole
        /// difference between harvesting a thing and being trusted with one, and the
        /// rest of the chain reads differently for it: the stowing post is not built
        /// out of a spirit, it is built *for* one, and taking the post down gives the
        /// heartwood back because you have not destroyed anything, only moved out.
        ///
        /// Nothing is destroyed until the item is actually in your pack. A full
        /// inventory has to be a refusal rather than a loss, because there is no way
        /// to get another one without growing another seed.
        /// </summary>
        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;

            var player = user as Player;
            if (player == null) return false;

            var nview = GetComponentInParent<ZNetView>();
            if (nview == null || !nview.IsValid()) return false;

            var amount = Mathf.Max(1, GroveConfig.HeartwoodGiven.Value);

            var inventory = player.GetInventory();
            var prefab = ObjectDB.instance != null
                ? ObjectDB.instance.GetItemPrefab(HeartwoodPrefab.Name)
                : null;

            if (prefab == null)
            {
                GrovePlugin.LogOnce(HeartwoodPrefab.Name + " is not registered - the "
                                    + "spirit has nothing to give.");
                return true;
            }

            var drop = prefab.GetComponent<ItemDrop>();
            if (drop == null) return true;

            if (!inventory.CanAddItem(drop.m_itemData, amount))
            {
                user.Message(MessageHud.MessageType.Center, "$inventory_full");
                return true;
            }

            inventory.AddItem(HeartwoodPrefab.Name, amount, 1, 0, 0L, "");

            user.Message(MessageHud.MessageType.Center, Localization.instance.Localize(
                "The " + GetHoverName().ToLowerInvariant()
                + " folds itself into the heartwood."));

            Fade(nview);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        /// <summary>
        /// Goes in rather than out, and certainly rather than pops.
        ///
        /// The effect is played where it stood because something did happen there, but
        /// what happened is that it left with you - so this is a departure, not a
        /// death, and nothing about it should read as one.
        ///
        /// Ownership is claimed first because whoever communed is not necessarily who
        /// owns the zone, and Destroy on a ZDO you do not own is ignored - the spirit
        /// would fold itself into one heartwood and then still be standing there,
        /// ready to do it again.
        /// </summary>
        private void Fade(ZNetView nview)
        {
            var effect = GroveConfig.FadeEffect.Value;
            if (!string.IsNullOrEmpty(effect) && ZNetScene.instance != null)
            {
                var prefab = ZNetScene.instance.GetPrefab(effect);
                if (prefab != null)
                    Instantiate(prefab, transform.position, Quaternion.identity);
                else
                    GrovePlugin.LogOnce("Fade effect '" + effect + "' does not exist.");
            }

            nview.ClaimOwnership();
            nview.Destroy();
        }
    }
}
