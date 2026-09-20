using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Hod
{
    /// <summary>
    /// Says on the crafting panel's requirement line that the material is in a chest around
    /// the post behind you.
    ///
    /// InventoryGui.SetupRequirement is the single draw choke point for both places a
    /// requirement is ever shown - the crafting panel and the build HUD - and it is public
    /// static with exactly two callers, so there is nothing else to patch and nothing to keep
    /// in step. <b>Only one of those two callers is this feature's business.</b> The hammer
    /// does not draw on chests, so a build line that added the chest total would be promising
    /// material the placement will not spend - the two-parts-of-the-interface-disagreeing
    /// failure, arrived at from the other side. The build HUD's numbers are exactly vanilla's.
    ///
    /// Which caller this is comes from HodCrafting.InScope rather than from the `craft`
    /// argument sitting right there, and that is not squeamishness. Hud.UpdateBuild passes
    /// <c>piece.FreeBuildKey() == GlobalKeys.NoCraftCost</c> for it, which is TRUE for any
    /// piece carrying an ItemDrop or a Feast - so the build HUD really does call this with
    /// craft:true for item stands and feasts, and following the argument would have put chest
    /// totals on exactly those lines. The scope bracket is opened by
    /// InventoryGui.SetupRequirementList, which is the crafting panel's own private method and
    /// nothing else, so "the bracket is open" is the fact that actually means "this is the
    /// crafting panel".
    ///
    /// What it does NOT do is worth knowing before reading the code, because the field names
    /// suggest otherwise. It does not draw a have/need pair. It writes only the NEEDED amount
    /// into res_amount, and uses the have count for one thing only: deciding whether that text
    /// flashes red. The have is read through player.GetInventory().CountItems(name), and a
    /// crafting-panel line is drawn inside the scope bracket, so the flash and the greying of
    /// the recipe list are already correct before this file does anything. (A build HUD line is
    /// not inside it, which is why its flash is vanilla's.) All that is left is telling the
    /// player WHY, which is the difference between a number that looks right and a number that
    /// looks like a bug.
    ///
    /// Recomputing the have count here re-enters the feature's own CountItems patch - the
    /// bracket is open, that is the whole point - so it goes through CarriedOnly, which
    /// suspends the scope for the length of the question.
    ///
    /// The chest number is the OPTIMISTIC one - HodChests.CountAllowed, every reachable chest,
    /// including the ones another machine owns - and that is on purpose. This line is drawn
    /// for every requirement of every recipe as the panel scrolls, so it cannot ask anybody
    /// anything; what it reports is what this client's copy of each chest says, and that copy
    /// is refreshed once a second. Payment is a different question with a different answer -
    /// see HodCrafting. The consequence a player can see is that a recipe may show blue and
    /// then decline; the alternative is showing nothing at all until a round trip completes,
    /// for a number that is redrawn every frame.
    ///
    /// One thing deliberately not attempted. The 0.221 radial hover menu builds its list from
    /// real ItemData objects, through GetAllItemsInGridOrder and GetAllItems, rather than from
    /// counts - so chest items can never appear there off the back of a count patch, and
    /// nothing in this file tries. Turret.TryGetItems in particular dereferences
    /// FindAmmoItem's result unguarded, so faking availability into that menu would throw
    /// rather than lie. The bench service simply never goes there.
    /// </summary>
    internal static class HodRequirement
    {
        /// <summary>
        /// The number this line is about, asked the way the gate behind it asks.
        ///
        /// Player.HaveRequirementItems loops quality 1..maxQuality and takes the MAXIMUM of
        /// CountItems at each level. Three at quality 1 and three at quality 2 satisfies
        /// nothing that wants five, so a sum across levels would be a promise the craft button
        /// then refuses.
        ///
        /// Vanilla's own red flash reads a -1 sum instead - every level added together - so on
        /// a multi-quality material the flash is already wrong about the craft gate. Following
        /// the flash was the first version of this and it was the wrong half to match: the
        /// number on the line is what a player reads as a promise, and a line that says the
        /// material is there beside a button that refuses is the two-parts-of-the-panel-
        /// disagreeing failure the scope bracket exists to prevent. Rare in vanilla, where
        /// materials sit at one quality; ordinary the day a content mod ships one that does not.
        ///
        /// Carried and chest are read at the SAME quality level rather than maximised
        /// separately, because the gate maximises their sum - the CountItems postfix has
        /// already added the two together by the time HaveRequirementItems compares them.
        /// </summary>
        private static void Available(Player player, Piece.Requirement req,
                                      out int have, out int chest)
        {
            var pack = player.GetInventory();
            var name = req.m_resItem.m_itemData.m_shared.m_name;

            have = 0;
            chest = 0;
            var best = -1;

            var maxQuality = req.m_resItem.m_itemData.m_shared.m_maxQuality;

            for (var q = 1; q <= maxQuality; q++)
            {
                var carried = HodCrafting.CarriedOnly(pack, name, q, true);

                // No position. The sphere is the post's, resolved once a frame by HodScope -
                // which is what makes it impossible for this line to describe a different set
                // of chests from the one the craft will spend out of. It was a parameter in
                // Hirsla and it is the single thing the fold changed about this file.
                var stored = HodChests.CountAllowed(name, q, true);

                if (carried + stored <= best) continue;

                best = carried + stored;
                have = carried;
                chest = stored;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
        private static void ShowChestTotal(Transform elementRoot, Piece.Requirement req,
            Player player, int quality, int craftMultiplier, bool __result)
        {
            // False means vanilla hid the whole row, which it does for a requirement whose
            // amount at this quality is zero. Writing into a hidden line is invisible work
            // that goes wrong the day the line is shown again.
            if (!__result) return;

            // The crafting panel and nothing else. The bracket is opened by
            // SetupRequirementList, so an open scope here means this line belongs to the panel
            // rather than to the build HUD - which draws through the same method and must be
            // left alone. See the class docstring for why the `craft` argument is not the test.
            if (!HodCrafting.InScope) return;

            if (!HodConfig.ShowChestTotals.Value) return;

            // An optimisation rather than a rule - with the scope shut the chest total below
            // comes out zero and the line is left alone anyway - but it is the answer for
            // every requirement line drawn anywhere except beside a post with a jib, and this
            // method is called once per requirement of the selected recipe every frame the
            // panel is open. Nothing is promised or refused here, so short-circuiting a
            // display cannot cost anything.
            if (!HodScope.IsOpen) return;

            if (req == null || req.m_resItem == null) return;
            if (player == null || player != Player.m_localPlayer) return;

            var need = req.GetAmount(quality) * craftMultiplier;
            if (need <= 0) return;

            // Both numbers at once, asked the way the crafting gate asks - see Available.
            //
            // Read with the scope suspended, which CarriedOnly does: this runs inside the
            // feature's own bracket, so an ordinary CountItems here would answer with the
            // chests already added and the line would report the chest total twice.
            int have;
            int chest;
            Available(player, req, out have, out chest);

            if (chest <= 0) return;

            var amountRoot = elementRoot.Find("res_amount");
            if (amountRoot == null) return;

            // TryGetComponent and a plain null check throughout. Unity overloads == so a
            // destroyed object compares equal to null, and ?. bypasses that overload
            // entirely - the destroyed component sails through and throws somewhere
            // unrelated later, in Player.log where nobody is looking.
            TMP_Text amount;
            if (!amountRoot.TryGetComponent(out amount)) return;

            amount.text = Format(need, have, chest);
        }

        /// <summary>
        /// The configured layout, with the four numbers substituted in.
        ///
        /// Token replacement rather than string.Format, because {0} and {1} in a .cfg is a
        /// setting nobody can edit without first finding the code that reads it. Named tokens
        /// are self-documenting in the file, which is where this setting is read.
        /// </summary>
        private static string Format(int need, int have, int chest)
        {
            var text = HodConfig.ChestTotalFormat.Value;
            if (string.IsNullOrEmpty(text)) return need.ToString();

            return text
                .Replace("{need}", need.ToString())
                .Replace("{have}", have.ToString())
                .Replace("{chest}", chest.ToString())
                .Replace("{total}", (have + chest).ToString());
        }
    }
}
