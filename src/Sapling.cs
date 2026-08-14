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

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _piece = GetComponent<Piece>();
            All.Add(this);
        }

        private void OnDestroy()
        {
            All.Remove(this);
        }

        private void Start()
        {
            Show();
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
            get { return Mathf.Max(1f, GroveConfig.BloodNeeded.Value); }
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
            Object.Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

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

            // The count is shown, not a percentage. "41 of 60" tells you to go and kill
            // nineteen more things; "68%" tells you nothing you can act on.
            return Localization.instance.Localize(string.Format(
                "{0}\n{1}  ( {2} / {3} )", GetHoverName(), StageNames[stage],
                Mathf.FloorToInt(Blood), Mathf.FloorToInt(Needed)));
        }
    }
}
