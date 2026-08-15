using System.Collections.Generic;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// Makes the stowing post cost heartwood - which is to say, gives the spirit
    /// somewhere to live.
    ///
    /// The post is not built out of a spirit, it is built for one, and that is the
    /// whole reason a post that sorts your chests is worth an hour of greydwarfs when
    /// a chest is worth ten wood. Something is doing the sorting.
    ///
    /// The whole coupling lives on this side, and Stow is never told about Grove. That
    /// direction matters: Stow stays a mod that works on its own and still builds its
    /// post out of wood and fine wood, and installing Grove is what raises the price.
    /// A two-way dependency would mean neither could ship without the other, for a
    /// feature that is really just "one recipe gains one ingredient".
    ///
    /// There is no assembly reference either - the post is found by its prefab name.
    /// So this compiles, and runs, whether or not Stow exists on the machine.
    /// </summary>
    internal static class StowCoupling
    {
        /// <summary>
        /// Stow's piece, by the name it registers with ZNetScene.
        ///
        /// A string rather than a reference to StowPost.Name, because referencing it
        /// would mean referencing the assembly, which would mean Grove failing to load
        /// when Stow is absent - exactly the coupling this is written to avoid. The
        /// cost is that a rename in Stow breaks this silently, so it says so in the log
        /// when it cannot find the piece and Stow is clearly loaded.
        /// </summary>
        private const string PostPrefab = "stow_post";

        private static bool _done;

        public static void Invalidate()
        {
            _done = false;
        }

        /// <summary>Idempotent, and safe to call every frame until it takes.</summary>
        public static void Apply()
        {
            if (_done || !GroveConfig.CoupleToStow.Value) return;
            if (ZNetScene.instance == null || ObjectDB.instance == null) return;

            // Heartwood has to exist first, or the requirement would name an item that
            // ObjectDB cannot resolve and the post would become unbuildable rather than
            // more expensive.
            if (!HeartwoodPrefab.Ready) return;

            var prefab = ZNetScene.instance.GetPrefab(PostPrefab);
            if (prefab == null) return;

            var piece = prefab.GetComponent<Piece>();
            if (piece == null)
            {
                GrovePlugin.LogOnce("Found " + PostPrefab + " but it has no Piece - not "
                                    + "touching its recipe.");
                _done = true;
                return;
            }

            var extra = Requirements(GroveConfig.StowPostCost.Value);
            if (extra.Count == 0) { _done = true; return; }

            piece.m_resources = Merge(piece.m_resources, extra);
            _done = true;

            GrovePlugin.Log.LogInfo("The stowing post now costs heartwood.");
        }

        /// <summary>
        /// Adds to the post's cost rather than replacing it.
        ///
        /// Replacing would throw away whatever Stow's own config says the post costs,
        /// which is a setting that belongs to Stow and that someone may have deliberately
        /// changed. An ingredient already present is raised to whichever amount is
        /// higher rather than added twice, so running this after a config reload cannot
        /// quietly double the price.
        /// </summary>
        private static Piece.Requirement[] Merge(Piece.Requirement[] existing,
                                                 List<Piece.Requirement> extra)
        {
            var merged = new List<Piece.Requirement>(existing ?? new Piece.Requirement[0]);

            foreach (var addition in extra)
            {
                var found = false;

                foreach (var current in merged)
                {
                    if (current.m_resItem == null || addition.m_resItem == null) continue;
                    if (current.m_resItem.name != addition.m_resItem.name) continue;

                    current.m_amount = Mathf.Max(current.m_amount, addition.m_amount);
                    found = true;
                    break;
                }

                if (!found) merged.Add(addition);
            }

            return merged.ToArray();
        }

        private static List<Piece.Requirement> Requirements(string spec)
        {
            var list = new List<Piece.Requirement>();

            foreach (var entry in (spec ?? "").Split(','))
            {
                var parts = entry.Split(':');
                if (parts.Length != 2) continue;

                var itemName = parts[0].Trim();
                if (itemName.Length == 0) continue;

                int amount;
                if (!int.TryParse(parts[1].Trim(), out amount) || amount <= 0) continue;

                var prefab = ObjectDB.instance.GetItemPrefab(itemName);
                var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    GrovePlugin.LogOnce("Stow post cost mentions unknown item '"
                                        + itemName + "'.");
                    continue;
                }

                list.Add(new Piece.Requirement
                {
                    m_resItem = drop,
                    m_amount = amount,

                    // Recoverable, unlike the seed, and now for a reason rather than
                    // only a convention. Taking the post down does not destroy
                    // anything: the spirit's home is the heartwood, not the post, so
                    // dismantling one hands the heartwood back and the spirit moves
                    // with you. The seed is spent because it was planted; this never
                    // is, because nothing was consumed to begin with.
                    m_recover = true
                });
            }

            return list;
        }
    }
}
