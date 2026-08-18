using System;
using System.Collections.Generic;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// The planted ancient seed, and everything it does while it waits.
    ///
    /// It does not grow on a timer. Vanilla `Plant` does - `SUpdate` compares
    /// `TimeSincePlanted()` against a seeded random grow time and swaps between two
    /// visual states at the halfway mark - and both halves of that are wrong here, so
    /// the donor's Plant is torn out and this keeps its own count instead.
    ///
    /// The count lives on the ZDO, so it saves with the world, survives a reload, and
    /// is the same number for everyone on a server without any syncing of our own.
    /// </summary>
    internal class Sapling : MonoBehaviour, Hoverable
    {
        private const string ZBlood = "groveBlood";

        // --------------------------------------------------------------- stirring

        /// <summary>Degrees of lean at full. Small: this is a seed in the ground breathing,
        /// not a plant in a gale, and anything past a few degrees reads as a physics bug.</summary>
        public static float SwayDegrees = 3.2f;

        /// <summary>Seconds for one sway. Slow enough that it is noticed rather than
        /// watched.</summary>
        public static float SwayPeriod = 5.6f;

        /// <summary>Seconds for one breath, deliberately not a multiple of the sway.</summary>
        public static float BreathPeriod = 3.7f;

        /// <summary>How much it swells at full, as a fraction.</summary>
        public static float BreathDepth = 0.035f;

        private float _stirPhase;

        /// <summary>Every sapling currently loaded, so a death can find the nearest.</summary>
        public static readonly List<Sapling> All = new List<Sapling>();

        private static readonly string[] StageNames =
        {
            "newly planted", "rooting", "swelling", "ready to open"
        };

        public Transform[] Stages = new Transform[0];

        private ZNetView _nview;
        private Piece _piece;
        private int _shown = -1;

        private WearNTear _wear;
        private Destructible _destructible;
        private float _lastCry = -99f;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _piece = GetComponent<Piece>();
            All.Add(this);

            WatchForHarm();
        }

        /// <summary>
        /// Shout when something is chewing on it.
        ///
        /// Both WearNTear and Destructible expose m_onDamaged and m_onDestroyed as plain
        /// Actions, so this needs no patch at all and does not care which one the donor
        /// turned out to carry. Subscribing rather than patching also keeps it per
        /// instance: the prefab itself never runs Awake, because it lives in an inactive
        /// holder, so nothing accumulates on the shared copy.
        ///
        /// Without this the mod is unplayable rather than merely hard. You are asked to
        /// spend an hour killing greydwarfs beside a thing greydwarfs attack, and losing
        /// it silently - to come back to bare ground with no idea when or why - is not a
        /// difficulty, it is a mystery.
        /// </summary>
        private void WatchForHarm()
        {
            _wear = GetComponentInChildren<WearNTear>(true);
            if (_wear != null)
            {
                _wear.m_onDamaged = (Action)Delegate.Combine(_wear.m_onDamaged,
                                                             new Action(Hurt));
                _wear.m_onDestroyed = (Action)Delegate.Combine(_wear.m_onDestroyed,
                                                               new Action(Lost));
            }

            _destructible = GetComponentInChildren<Destructible>(true);
            if (_destructible == null) return;

            _destructible.m_onDamaged = (Action)Delegate.Combine(_destructible.m_onDamaged,
                                                                 new Action(Hurt));
            _destructible.m_onDestroyed = (Action)Delegate.Combine(_destructible.m_onDestroyed,
                                                                   new Action(Lost));
        }

        /// <summary>
        /// How intact it is, 1 down to 0.
        ///
        /// Two readings because there are two possible components. WearNTear keeps a
        /// percentage of its own; Destructible keeps the live number on the ZDO under
        /// the same key the game uses, defaulting to full when nothing has hit it yet.
        /// </summary>
        private float Condition
        {
            get
            {
                if (_wear != null) return Mathf.Clamp01(_wear.GetHealthPercentage());

                if (_destructible == null) return 1f;

                var nview = _destructible.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) return 1f;

                var full = Mathf.Max(1f, _destructible.m_health);
                return Mathf.Clamp01(nview.GetZDO().GetFloat(ZDOVars.s_health, full) / full);
            }
        }

        /// <summary>
        /// Rate-limited hard. A mob lands a blow roughly every second and a line per
        /// blow would bury the feeding counter, which is the message that matters.
        /// </summary>
        private void Hurt()
        {
            if (!GroveConfig.Messages.Value || Player.m_localPlayer == null) return;
            if (Time.time - _lastCry < 5f) return;

            _lastCry = Time.time;

            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                Localization.instance.Localize(GetHoverName() + " is being torn at."));
        }

        /// <summary>
        /// Centre screen, and not gated on the Messages setting.
        ///
        /// Losing this costs an ancient seed that m_recover is false for and however
        /// long you spent feeding it. That is not a corner notification, and someone who
        /// turned the counter off did not thereby ask to lose an hour in silence.
        /// </summary>
        private void Lost()
        {
            GrovePlugin.Log.LogInfo(GetHoverName() + " was destroyed at "
                                    + Mathf.FloorToInt(Blood) + "/"
                                    + Mathf.FloorToInt(Needed) + ".");

            if (Player.m_localPlayer == null) return;

            Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize(GetHoverName() + " was destroyed."));
        }

        /// <summary>
        /// Fires on a destroyed sapling and on an unloaded one alike, and the map pin must
        /// only come off for the first.
        ///
        /// Unity gives no way to tell those apart, so the ZDO is asked instead: a zone
        /// going quiet leaves the ZDO in ZDOMan, while a sapling that opened or was torn
        /// down has had it destroyed. Getting this wrong the other way would unpin every
        /// sapling the moment you walked away from it, which is precisely when the pin is
        /// the only thing you have.
        /// </summary>
        private void OnDestroy()
        {
            All.Remove(this);

            if (!Gone()) return;

            SaplingPin.Clear(transform.position, GetHoverName());
        }

        private bool Gone()
        {
            if (_nview == null || ZDOMan.instance == null) return false;

            var zdo = _nview.GetZDO();
            if (zdo == null) return true;

            return ZDOMan.instance.GetZDO(zdo.m_uid) == null;
        }

        private void Start()
        {
            Show();

            // In Start rather than Awake: the Minimap does not exist during the first
            // frames of a world load, and this simply does nothing then - the sapling gets
            // its pin the next time its zone loads, which is the frame you could see it
            // anyway.
            SaplingPin.Mark(transform.position, GetHoverName());
        }

        // ------------------------------------------------------------------ progress

        public float Blood
        {
            get
            {
                return _nview != null && _nview.IsValid()
                    ? _nview.GetZDO().GetFloat(ZBlood, 0f)
                    : 0f;
            }
        }

        public float Needed
        {
            get { return GroveConfig.BloodNeededNow(); }
        }

        public float Progress
        {
            get { return Mathf.Clamp01(Blood / Needed); }
        }

        /// <summary>
        /// Adds to the count and, if that finishes it, opens.
        ///
        /// Ownership is claimed first for the same reason the game claims it before
        /// taking from a chest: without it the write lands on a copy that the real
        /// owner will overwrite, and kills would silently stop counting for whoever
        /// was not holding the zone.
        /// </summary>
        public void Feed(float amount)
        {
            if (_nview == null || !_nview.IsValid() || amount <= 0f) return;

            _nview.ClaimOwnership();

            var before = Blood;
            var after = Mathf.Min(Needed, before + amount);
            _nview.GetZDO().Set(ZBlood, after);

            if (GroveConfig.Verbose.Value)
                GrovePlugin.Log.LogInfo("sapling fed " + amount + " -> " + after + "/" + Needed);

            Show();

            if (after >= Needed) Open();
        }

        // ------------------------------------------------------------------ stages

        /// <summary>
        /// Shows the stage the count has reached.
        ///
        /// Four stages at a quarter each, and the last one is only reached *at* the
        /// threshold rather than approaching it - Mathf.FloorToInt(1.0 * 4) is 4, which
        /// would index past the end of a four-element array on the frame it completes.
        /// </summary>
        private void Show()
        {
            if (Stages == null || Stages.Length == 0) return;

            var stage = Mathf.Clamp(Mathf.FloorToInt(Progress * Stages.Length),
                                    0, Stages.Length - 1);
            if (stage == _shown) return;

            _shown = stage;

            for (var i = 0; i < Stages.Length; i++)
                if (Stages[i] != null) Stages[i].gameObject.SetActive(i == stage);
        }

        private void Update()
        {
            // Cheap, and it catches the case that matters: another player fed it, so
            // the count changed under us without Feed ever running on this client.
            Show();
            Stir();
        }

        // ------------------------------------------------------------------ stirring

        /// <summary>Base pose of each stage, so the animation is applied to it rather than
        /// accumulating on top of itself.</summary>
        private Quaternion[] _restRotation;
        private Vector3[] _restScale;

        /// <summary>
        /// The planted seed moves, and how much it moves is how close it is to opening.
        ///
        /// Same idea as the spirit, which drives everything it does off one number. Here the
        /// number is Progress, so a seed nobody has fed is almost still and a full one is
        /// visibly restless. That makes the thing readable across a clearing without a hover:
        /// you can tell at a glance whether the sapling you planted this evening has had a
        /// good night, and it gives the last few kills something to look at.
        ///
        /// Driven from Time.time rather than accumulated deltaTime, so every client sees the
        /// same phase and a sapling does not lurch when somebody walks into the zone.
        ///
        /// Two motions at periods that do not divide into each other, because a sway and a
        /// breath on the same clock read as one stiff pulse rather than as something alive.
        /// </summary>
        private void Stir()
        {
            if (Stages == null || Stages.Length == 0) return;
            if (_shown < 0 || _shown >= Stages.Length) return;

            var stage = Stages[_shown];
            if (stage == null) return;

            Capture();

            // Never fully still, even at zero. A seed that only starts moving once fed reads
            // as broken until the first kill lands, and the floor is small enough that the
            // difference between empty and full is still the thing you notice.
            var life = Mathf.Lerp(0.25f, 1f, Progress);
            var time = Time.time + _stirPhase;

            var lean = Mathf.Sin(time * (Mathf.PI * 2f / SwayPeriod)) * SwayDegrees * life;
            var roll = Mathf.Cos(time * (Mathf.PI * 2f / (SwayPeriod * 1.37f)))
                       * SwayDegrees * 0.6f * life;

            stage.localRotation = _restRotation[_shown] * Quaternion.Euler(lean, 0f, roll);

            var breath = 1f + Mathf.Sin(time * (Mathf.PI * 2f / BreathPeriod))
                              * BreathDepth * life;
            stage.localScale = _restScale[_shown] * breath;
        }

        /// <summary>
        /// Remembers the pose each stage was built with, once.
        ///
        /// Read lazily rather than in Awake because the stages are parented and posed by
        /// SaplingPrefab after the component exists, so anything captured in Awake is the
        /// pose before the model was put on.
        /// </summary>
        private void Capture()
        {
            if (_restRotation != null && _restRotation.Length == Stages.Length) return;

            _restRotation = new Quaternion[Stages.Length];
            _restScale = new Vector3[Stages.Length];

            for (var i = 0; i < Stages.Length; i++)
            {
                _restRotation[i] = Stages[i] != null
                    ? Stages[i].localRotation : Quaternion.identity;
                _restScale[i] = Stages[i] != null ? Stages[i].localScale : Vector3.one;
            }

            // Seeded off the position so two saplings side by side are not in lockstep, and
            // so the same one is in the same phase for everybody watching it.
            var p = transform.position;
            _stirPhase = Mathf.Abs(p.x * 12.9898f + p.z * 78.233f) % (Mathf.PI * 2f);
        }

        // ------------------------------------------------------------------ opening

        /// <summary>
        /// The pod opens and the spirit is what was inside.
        ///
        /// Only the owner spawns, or every client watching would create one.
        /// </summary>
        private void Open()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;

            var prefab = ZNetScene.instance != null
                ? ZNetScene.instance.GetPrefab(SpiritPrefab.Name)
                : null;

            if (prefab == null)
            {
                GrovePlugin.LogOnce("Sapling is ready but " + SpiritPrefab.Name
                                    + " is not registered - it will stay ready.");
                return;
            }

            var at = transform.position + Vector3.up * GroveConfig.SpiritRise.Value;
            // Fully qualified: this file uses System for the damage-hook delegates, which
            // makes both Object and Random ambiguous against their UnityEngine namesakes.
            UnityEngine.Object.Instantiate(
                prefab, at, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));

            GrovePlugin.Log.LogInfo("A forest spirit answered at " + at + ".");

            _nview.Destroy();
        }

        // ------------------------------------------------------------------ hover

        public string GetHoverName()
        {
            return _piece != null ? _piece.m_name : GroveConfig.SaplingName.Value;
        }

        public string GetHoverText()
        {
            var stage = Mathf.Clamp(Mathf.FloorToInt(Progress * StageNames.Length),
                                    0, StageNames.Length - 1);

            // Only when something has actually hit it. A condition line on an untouched
            // sapling would read as a warning and there is nothing to warn about.
            var condition = Condition;
            var hurt = condition < 0.999f
                ? string.Format("\n<color=#dd6666>damaged</color>  ( {0}% )",
                                Mathf.CeilToInt(condition * 100f))
                : "";

            // The count is shown, not a percentage. "41 of 60" tells you to go and kill
            // nineteen more things; "68%" tells you nothing you can act on.
            return Localization.instance.Localize(string.Format(
                "{0}\n{1}  ( {2} / {3} ){4}", GetHoverName(), StageNames[stage],
                Mathf.FloorToInt(Blood), Mathf.FloorToInt(Needed), hurt));
        }
    }
}
