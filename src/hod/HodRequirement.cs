using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Grove;

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
    /// What vanilla does here is worth knowing before reading the code, because the field
    /// names suggest otherwise. It does not draw a have/need pair. It writes only the NEEDED
    /// amount into res_amount, and uses the have count for one thing only: deciding whether
    /// that text flashes red. The have is read through player.GetInventory().CountItems(name),
    /// and a crafting-panel line is drawn inside the scope bracket, so the flash and the
    /// greying of the recipe list are already correct before this file does anything. (A build
    /// HUD line is not inside it, which is why its flash is vanilla's.) All that is left is
    /// telling the player WHY, which is the difference between a number that looks right and a
    /// number that looks like a bug.
    ///
    /// The default format does that by drawing the pair vanilla leaves out, what you carry
    /// over what it costs, and then what the chests add, in blue: "24/40 +169". Robbin picked
    /// it from three mockups (LHM-28). The one before it, "40 (+169)", gave the cost and the
    /// chest total and left the player to find the third number, the one in the pack, by
    /// opening the inventory. The flash still colours the whole line and the colour tag
    /// still keeps the chest part blue, so a line short even with the chests flashes its
    /// "24/40" red the way vanilla flashes a bare "40".
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
    /// <b>The chest number does not depend on the craft multiplier, and a report once read as
    /// if it did.</b> Holding the multi-craft key showed "40 (+16" where a moment earlier the
    /// same recipe showed "8 (+169". Nothing between the key and the chest count reads either:
    /// the multiplier scales <c>need</c> below and nothing else, <see cref="Available"/> takes
    /// no multiplier at all, and vanilla's own gate does the same - HaveRequirementItems
    /// multiplies the requirement and counts the pack exactly as it always does. Both
    /// screenshots show the line cut after the same seven characters, four digits, a space, a
    /// bracket and a plus, and that is exactly what "8 (+169)" and "40 (+169)" look like
    /// through one fixed width: the first loses its bracket, the second is a digit longer and
    /// loses the 9 as well. That is why the line is now fitted to its slot - see
    /// <see cref="Fit"/> - and it is also why a clipped number is not a cosmetic bug. It reads
    /// as the mod counting wrong.
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
        ///
        /// No craft multiplier, and none is wanted. The multiplier is a fact about how much
        /// the recipe costs; this is a fact about how much there is, and the two only meet in
        /// the comparison the gate makes.
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
            // that goes wrong the day the line is shown again - and so is putting a label
            // back, which is why a hidden row is not unfitted here either. Every path that
            // shows the row again comes back through this method with __result true.
            if (!__result) return;

            TMP_Text amount;
            string text;

            if (!Compose(elementRoot, req, player, quality, craftMultiplier,
                         out amount, out text))
            {
                // Vanilla's own number is on this line now. If an earlier frame fitted the
                // label for a longer one, it goes back to exactly what vanilla built, so a
                // recipe with nothing in the chests reads the way it would without the mod.
                Unfit(elementRoot);
                return;
            }

            amount.text = text;
            Fit(elementRoot, amount, text);
        }

        /// <summary>
        /// Whether this line gets the chest total, and the label and text if it does.
        ///
        /// Every refusal in here is a line vanilla draws alone, which is why they are gathered
        /// into one question: the caller has exactly two things to do, write or put the label
        /// back, and a return in the middle of the postfix would have been a third.
        /// </summary>
        private static bool Compose(Transform elementRoot, Piece.Requirement req,
            Player player, int quality, int craftMultiplier,
            out TMP_Text amount, out string text)
        {
            amount = null;
            text = null;

            // The crafting panel and nothing else. The bracket is opened by
            // SetupRequirementList, so an open scope here means this line belongs to the panel
            // rather than to the build HUD - which draws through the same method and must be
            // left alone. See the class docstring for why the `craft` argument is not the test.
            if (!HodCrafting.InScope) return false;

            if (!HodConfig.ShowChestTotals.Value) return false;

            // An optimisation rather than a rule - with the scope shut the chest total below
            // comes out zero and the line is left alone anyway - but it is the answer for
            // every requirement line drawn anywhere except beside a post with a jib, and this
            // method is called once per requirement of the selected recipe every frame the
            // panel is open. Nothing is promised or refused here, so short-circuiting a
            // display cannot cost anything.
            if (!HodScope.IsOpen) return false;

            if (req == null || req.m_resItem == null) return false;
            if (player == null || player != Player.m_localPlayer) return false;

            // The multiplier is the multi-craft amount - five while the key is held, one
            // otherwise - and it belongs here and only here. It is what the recipe COSTS, and
            // the chest count below is asked without it.
            var need = req.GetAmount(quality) * craftMultiplier;
            if (need <= 0) return false;

            // Both numbers at once, asked the way the crafting gate asks - see Available.
            //
            // Read with the scope suspended, which CarriedOnly does: this runs inside the
            // feature's own bracket, so an ordinary CountItems here would answer with the
            // chests already added and the line would report the chest total twice.
            int have;
            int chest;
            Available(player, req, out have, out chest);

            if (chest <= 0) return false;

            if (elementRoot == null) return false;

            var amountRoot = elementRoot.Find("res_amount");
            if (amountRoot == null) return false;

            // TryGetComponent and a plain null check throughout. Unity overloads == so a
            // destroyed object compares equal to null, and ?. bypasses that overload
            // entirely - the destroyed component sails through and throws somewhere
            // unrelated later, in Player.log where nobody is looking.
            if (!amountRoot.TryGetComponent(out amount)) return false;

            text = Format(need, have, chest);
            return true;
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

        // ------------------------------------------------------------------ fitting

        /// <summary>
        /// The smallest the line may be drawn, in the panel's own text units.
        ///
        /// Twelve because CLAUDE.md says twelve: Robbin reads small text badly on this setup,
        /// and a number shrunk until it fits is no use if it cannot then be read. Never used
        /// to ENLARGE a label - a vanilla label drawn smaller than this stays its own size,
        /// because the floor is about how far this mod may shrink the game's text, not about
        /// correcting the game.
        /// </summary>
        private const float Floor = 12f;

        /// <summary>What vanilla built a label with, so it can be put back exactly.</summary>
        private sealed class Label
        {
            public bool AutoSizing;
            public float Min;
            public float Max;
            public TextWrappingModes Wrapping;
            public TextOverflowModes Overflow;

            /// <summary>The size the label was drawing at when it was first fitted.</summary>
            public float Size;

            /// <summary>
            /// The size TMP starts an auto-sized line from, which is not the same thing as
            /// <see cref="Size"/> - that one is wherever the last line settled. Only read for a
            /// label vanilla auto-sizes; for any other it is Size.
            /// </summary>
            public float Base;

            /// <summary>The size vanilla draws at, and so the largest this ever uses.</summary>
            public float Ceiling;

            /// <summary>
            /// The rect vanilla built, as its sizeDelta, which is the one field widening the label
            /// writes, and as a width, so the label is never made narrower than it was.
            /// </summary>
            public Vector2 SizeDelta;
            public float Width;

            /// <summary>The floor, or the ceiling when vanilla is already smaller than it.</summary>
            public float Lowest;

            /// <summary>
            /// The text and the room it was last sized for, so an unchanged line in an
            /// unchanged label is not measured again.
            /// </summary>
            public string Measured;
            public float Room = -1f;
        }

        /// <summary>
        /// Every label currently drawn at a fitted size, and what it was before.
        ///
        /// Keyed on the TMP_Text itself. The crafting panel has four requirement slots and
        /// reuses them for every recipe, so this never holds more than four entries - and
        /// Unfit returns on its first line while it holds none, which is every frame of every
        /// session that is not standing at a jib-served bench.
        /// </summary>
        private static readonly Dictionary<TMP_Text, Label> Fitted =
            new Dictionary<TMP_Text, Label>();

        private static bool _described;
        private static bool _warnedTooWide;

        /// <summary>
        /// Makes the whole line fit the slot vanilla drew a bare number in.
        ///
        /// Vanilla's res_amount was laid out for "8" and "40", and the chest total makes the
        /// line several times that - "24/40 +169" in the default format is ten characters in a
        /// label built for two. Robbin's report, on the older and shorter format, had it cut at
        /// seven characters in both states of the panel - "8 (+169" and "40 (+16" - so the
        /// bracket was gone in one and a digit with it in the other, and a digit gone is a
        /// number that is simply wrong. Nothing below knows which format is in use: the line is
        /// measured as it will be drawn, colour tags and all, so a longer format is just a
        /// wider line. The fix is three settings on the label and one width:
        ///
        ///   one width      the slot's, not the label's, as below
        ///   one line       wrapping off, so a number and its bracket are never split across
        ///                  two lines of a slot that was laid out for one
        ///   one size       chosen here from the WIDTH of the line and nothing else, between
        ///                  vanilla's own size and <see cref="Floor"/>. A line that fits across
        ///                  is drawn at exactly vanilla's size - the ordinary case changes
        ///                  nothing at all - and one that does not is shrunk by exactly the
        ///                  ratio it is too wide by
        ///   overflow       for the last case only: a line still too wide at the floor is not
        ///                  cut by the label. A number that runs a few pixels long is ugly and
        ///                  true; a number with its last digit hidden is neat and false, and
        ///                  false is what this was reported for
        ///
        /// <b>TextMeshPro's own auto size was the first version of this, and it answers a
        /// different question.</b> It shrinks for height as well as width -
        /// TextMeshProUGUI.GenerateTextMesh compares the line's height against the label's
        /// before it ever looks at the width, and steps the size down if the line is taller -
        /// and before shrinking for width it squeezes the glyphs by whatever character-width
        /// adjustment the label was built with. Neither is visible for a bare number drawn in
        /// overflow mode, so a label a little shorter than its own font is an ordinary thing
        /// for vanilla to have built. On a label like that, auto size would have put every blue
        /// line at the floor, however short, while the plain numbers beside it stayed full
        /// size - and the log, which compares widths, would have said everything fitted. The
        /// only thing this needs to know is whether the line fits across, so that is the only
        /// thing measured, and the size is set rather than negotiated.
        ///
        /// <b>The room is the slot, because the label the game built is narrower than the
        /// slot it sits in.</b> The first version fitted the line to res_amount's own rect and set
        /// no widths, since the layout lives in an asset bundle and could not be read offline.
        /// The first run in game read it (the once-a-session log line below): res_amount is
        /// 47.5 wide, centred in a slot 64 wide, with no mask above either. The default format
        /// at size 12 needs 55 for "24/10 +169" and 63 for "124/50 +769", so against the label
        /// every line with a three-digit chest count went to the floor, still ran past the
        /// label's edges, and warned that it was too wide for its slot when the slot had room
        /// for it. Both requirement-line scenarios failed `fits` on exactly that. So the label
        /// is widened to the slot (<see cref="Widen"/>), the width read off the slot every time
        /// and never written down here, and the size is chosen against that: "24/10 +169" is
        /// drawn at about 14 instead of 12, whole and inside its slot.
        ///
        /// The three settings are written once, when a label is first fitted, the width whenever
        /// the slot's changes, and all four are put back by <see cref="Unfit"/> the moment its
        /// line goes back to vanilla's number. Leaving them on would have been nearly invisible,
        /// but "a recipe with nothing in the chests reads exactly like vanilla" is a promise in
        /// the config file, and it is cheaper to keep it literally than to argue that it is kept
        /// in effect.
        /// </summary>
        private static void Fit(Transform elementRoot, TMP_Text amount, string text)
        {
            Label label;
            if (!Fitted.TryGetValue(amount, out label))
            {
                label = Capture(amount);
                Fitted[amount] = label;

                amount.textWrappingMode = TextWrappingModes.NoWrap;
                amount.overflowMode = TextOverflowModes.Overflow;
                amount.enableAutoSizing = false;
                amount.fontSize = label.Ceiling;
            }

            // Every time rather than once, because the slot can be laid out a frame after the
            // label is first fitted, and a width read off a slot that has none yet would leave
            // the label at vanilla's for as long as it stays fitted. It writes only on a change.
            Widen(elementRoot, amount, label);

            // Measured when the text or the room changes and not otherwise. The line is
            // rewritten every frame, but "24/40 +169" on this frame is "24/40 +169" on the
            // next, and a counted stack - in the pack or in a chest - changes a few times a
            // minute at most. The room is part of the key because a size chosen for one
            // width is wrong for another: if the panel is laid out a frame late, the first
            // measurement is taken against a rect that is about to change, and with the text
            // alone as the key it would never be retaken.
            var room = Room(amount);
            if (label.Measured == text && Mathf.Abs(label.Room - room) < 0.01f) return;

            label.Measured = text;
            label.Room = room;

            Measure(elementRoot, amount, text, label, room);
        }

        /// <summary>
        /// Makes the label as wide as the slot it sits in, about its own centre.
        ///
        /// Only for a label that is a direct child of its slot, unscaled and centred on its
        /// pivot, which is how the game builds res_amount today. Then the label's own position
        /// and the slot's rect are in the same space, and growing it with its anchors keeps its
        /// middle where it was, so the text, which the game centres, does not move sideways.
        /// Any other layout keeps the rect the game gave it: a line that does not fit that is
        /// shrunk and then runs over, as it did before this, rather than being moved somewhere
        /// the game did not put it.
        ///
        /// Never narrower than the game built it, and the room is the smaller of the two sides
        /// from the label's centre, so a label a little off the middle of its slot still ends
        /// inside it.
        /// </summary>
        private static void Widen(Transform elementRoot, TMP_Text amount, Label label)
        {
            var slot = elementRoot as RectTransform;
            var own = amount.rectTransform;

            if (slot == null || own.parent != slot) return;
            if (Mathf.Abs(own.pivot.x - 0.5f) > 0.001f) return;
            if (Mathf.Abs(own.localScale.x - 1f) > 0.001f) return;

            var centre = own.localPosition.x;
            var half = Mathf.Min(centre - slot.rect.xMin, slot.rect.xMax - centre);
            var width = Mathf.Max(label.Width, 2f * half);

            if (Mathf.Abs(own.rect.width - width) < 0.01f) return;

            own.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        /// <summary>The width the label gives its text, the way TMP itself works it out.</summary>
        private static float Room(TMP_Text amount)
        {
            // TextMeshProUGUI computes its line width as exactly this - the rect less both
            // side margins, taken as they are, so a negative margin widens the line.
            var margin = amount.margin;
            return amount.rectTransform.rect.width - margin.x - margin.z;
        }

        private static Label Capture(TMP_Text amount)
        {
            var label = new Label
            {
                AutoSizing = amount.enableAutoSizing,
                Size = amount.fontSize,
                Min = amount.fontSizeMin,
                Max = amount.fontSizeMax,
                Wrapping = amount.textWrappingMode,
                Overflow = amount.overflowMode,
                SizeDelta = amount.rectTransform.sizeDelta,
                Width = amount.rectTransform.rect.width
            };

            // With auto size off, the size it draws at is the size it asks for. With it on,
            // TMP starts every line from the base size clamped between the min and the max
            // (TextMeshProUGUI.OnPreRenderCanvas), so that clamp is the largest vanilla would
            // draw this label at, and the base is what has to go back when it is restored.
            label.Base = label.AutoSizing ? BaseSize(amount) : label.Size;
            label.Ceiling = label.AutoSizing
                ? Mathf.Clamp(label.Base, label.Min, label.Max)
                : label.Size;
            label.Lowest = Mathf.Min(label.Ceiling, Floor);

            return label;
        }

        private static AccessTools.FieldRef<TMP_Text, float> _baseSize;
        private static bool _baseSizeBound;

        /// <summary>
        /// TMP_Text.m_fontSizeBase, which has no public getter: fontSize reads the size the
        /// last line settled on, and the base is only ever written.
        ///
        /// Bound lazily inside a try, and only for a label vanilla auto-sizes. A field ref in a
        /// static initialiser that fails to bind throws at type-init, and every Harmony patch
        /// this class carries would then throw with it - so a renamed field costs the exact
        /// restore of an auto-sized label, and nothing else. The fallback is the settled size,
        /// which puts the label back drawing what it drew a moment ago.
        /// </summary>
        private static float BaseSize(TMP_Text amount)
        {
            if (!_baseSizeBound)
            {
                _baseSizeBound = true;

                try
                {
                    _baseSize = AccessTools.FieldRefAccess<TMP_Text, float>("m_fontSizeBase");
                }
                catch (Exception e)
                {
                    GrovePlugin.LogOnce("Could not read TextMeshPro's base font size ("
                                        + e.Message + "). A requirement label the game "
                                        + "auto-sizes is put back at the size it last drew.");
                }
            }

            if (_baseSize == null) return amount.fontSize;

            try
            {
                return _baseSize(amount);
            }
            catch (Exception)
            {
                return amount.fontSize;
            }
        }

        /// <summary>
        /// Puts a label back exactly as vanilla built it, when its line is vanilla's again.
        /// </summary>
        private static void Unfit(Transform elementRoot)
        {
            // The common case by a long way, and it has to cost nothing: every requirement
            // line of every panel and of the build HUD comes through here while it is empty.
            if (Fitted.Count == 0) return;
            if (elementRoot == null) return;

            var amountRoot = elementRoot.Find("res_amount");
            if (amountRoot == null) return;

            TMP_Text amount;
            if (!amountRoot.TryGetComponent(out amount)) return;

            Label label;
            if (!Fitted.TryGetValue(amount, out label)) return;

            Fitted.Remove(amount);
            Restore(amount, label);
        }

        private static void Restore(TMP_Text amount, Label label)
        {
            // The size is written back while auto size is still OFF, and for an auto-sized
            // label that order is load-bearing. TMP's fontSize setter writes the size it draws
            // at every time, and the base size auto size starts from only while auto size is
            // off (TMP_Text.fontSize). Fitting wrote every size with it off, so the fitted
            // size is sitting in both; written now, in the same state, vanilla's replaces
            // both. Switch auto size back on first and the base would keep the fitted size,
            // and vanilla's label would start every later line from it.
            //
            // For a label vanilla draws at a fixed size the order does not matter, but the
            // write does: with auto size off nothing in TMP ever resets the drawn size, so
            // without this line the label would stay at whatever the last chest total shrank
            // it to.
            amount.fontSize = label.AutoSizing ? label.Base : label.Size;

            amount.fontSizeMin = label.Min;
            amount.fontSizeMax = label.Max;
            amount.enableAutoSizing = label.AutoSizing;

            amount.textWrappingMode = label.Wrapping;
            amount.overflowMode = label.Overflow;

            amount.rectTransform.sizeDelta = label.SizeDelta;
        }

        /// <summary>
        /// For a world change. The crafting panel is torn down with the scene, so its labels
        /// are about to be destroyed; any that are still alive are put back first, so the
        /// next fit reads vanilla's settings rather than this file's.
        /// </summary>
        public static void Forget()
        {
            foreach (var pair in Fitted)
            {
                if (pair.Key == null) continue;
                Restore(pair.Key, pair.Value);
            }

            Fitted.Clear();
        }

        /// <summary>
        /// Picks the size a line is drawn at, and - once a session - logs everything about
        /// the label it has to fit in.
        ///
        /// The line is measured at the ceiling and shrunk by the ratio it is too wide by.
        /// TMP scales every advance and every spacing linearly with the size, so the ratio
        /// is as good as measuring again at the new size; it is rounded DOWN to TMP's own
        /// twentieth of a point, so a line sized to fit does fit rather than landing a hair
        /// over.
        ///
        /// <b>The width GetPreferredValues returns is not the text's width.</b> It adds the
        /// label's positive side margins on at the end (TMP_Text.CalculatePreferredValues),
        /// and the room they are compared with has already had the margins taken off. Left
        /// in, the margins are counted twice, and scaled with the font besides, which they are
        /// not - so they come off here, before anything is compared or scaled.
        ///
        /// Caught whole. If measuring throws, the label stays at vanilla's own size with
        /// wrapping off and overflow on - one line, never cut by the label - and only the
        /// shrinking and the diagnostics are lost.
        /// </summary>
        private static void Measure(Transform elementRoot, TMP_Text amount, string text,
                                    Label label, float room)
        {
            try
            {
                // GetPreferredValues measures at the current size when auto size is off, and
                // the current size is whatever the previous line was shrunk to. Setting the
                // ceiling first makes the number it returns mean one thing every time.
                amount.fontSize = label.Ceiling;

                var margin = amount.margin;
                var preferred = amount.GetPreferredValues(text);
                var wide = preferred.x - Mathf.Max(margin.x, 0f) - Mathf.Max(margin.z, 0f);
                var tall = preferred.y - Mathf.Max(margin.y, 0f) - Mathf.Max(margin.w, 0f);

                var size = label.Ceiling;
                if (room > 0f && wide > room)
                {
                    size = Mathf.Floor(label.Ceiling * room / wide * 20f) / 20f;
                    size = Mathf.Max(size, label.Lowest);
                }

                amount.fontSize = size;

                // A label with no width yet is one the panel has not laid out, and it is
                // measured again the frame it gets one - see Fit. Describing it now would log
                // a zero as the slot's size and warn that every line is too wide for it.
                if (room <= 0f) return;

                var atFloor = label.Ceiling > 0f ? wide * label.Lowest / label.Ceiling : wide;

                if (!_described)
                {
                    _described = true;

                    if (GrovePlugin.Log != null)
                        GrovePlugin.Log.LogInfo(Describe(elementRoot, amount, text, label,
                            margin, room, wide, tall, atFloor, size));
                }

                // Half a unit of slack. TMP rounds the width it measures up to the next
                // hundredth, and a line within half a unit of its slot is one nobody can see
                // run over.
                if (atFloor > room + 0.5f && !_warnedTooWide)
                {
                    _warnedTooWide = true;

                    GrovePlugin.LogOnce(
                        "A requirement line on the crafting panel is too wide for its slot even "
                        + "at size " + N(label.Lowest) + ": \"" + text + "\" needs "
                        + N(atFloor) + " and the slot has " + N(room) + ". It is left at "
                        + N(label.Lowest) + " and runs long. A shorter Hod/ChestTotalFormat "
                        + "keeps it inside.");
                }
            }
            catch (Exception e)
            {
                GrovePlugin.LogOnce("Could not measure a crafting requirement line ("
                                    + e.Message + "). It is drawn at the game's own size on "
                                    + "one line and may run past its slot.");
            }
        }

        /// <summary>
        /// The one line that settles everything the offline fix had to assume.
        ///
        /// Positions are given in the LABEL's own space, left edge to right edge, for the
        /// label itself, the slot around it and every mask above it. A width alone cannot say
        /// whether a mask cuts the label - a mask wider than the label can still start inside
        /// it - and a mask that does cut it clips what the label draws however well the label
        /// itself fits. A line that still loses a character after this change is that case,
        /// and the edges here are what would show it. The height is there for the same kind of
        /// reason: a line taller than its label is drawn anyway in overflow mode, and it is the
        /// fact that decided against TMP's own auto size.
        /// </summary>
        private static string Describe(Transform elementRoot, TMP_Text amount, string text,
            Label label, Vector4 margin, float room, float wide, float tall, float atFloor,
            float size)
        {
            var rect = amount.rectTransform.rect;
            var line = new StringBuilder();

            line.Append("Hod requirement line, measured once: res_amount is ")
                .Append(N(rect.width)).Append(" x ").Append(N(rect.height))
                .Append(", x ").Append(N(rect.xMin)).Append(" to ").Append(N(rect.xMax));

            if (Mathf.Abs(rect.width - label.Width) >= 0.01f)
                line.Append(", widened to its slot from the game's ").Append(N(label.Width));

            line.Append(" (margins left ").Append(N(margin.x))
                .Append(", right ").Append(N(margin.z))
                .Append(", top ").Append(N(margin.y))
                .Append(", bottom ").Append(N(margin.w)).Append(")");

            var slot = elementRoot as RectTransform;
            if (slot != null)
                line.Append(" in a slot ").Append(N(slot.rect.width)).Append(" wide at x ")
                    .Append(Span(slot, amount.rectTransform));

            line.Append(". Vanilla draws it at ").Append(N(label.Size))
                .Append(label.AutoSizing
                    ? " with auto size on, " + N(label.Min) + " to " + N(label.Max)
                      + " from a base of " + N(label.Base)
                    : " with auto size off")
                .Append(", wrapping ").Append(label.Wrapping)
                .Append(", overflow ").Append(label.Overflow)
                .Append(", aligned ").Append(amount.alignment)
                .Append(". Masks above it: ")
                .Append(Masks(amount.transform.parent, amount.rectTransform))
                .Append(". Now sized from ").Append(N(label.Lowest))
                .Append(" to ").Append(N(label.Ceiling))
                .Append("; \"").Append(text).Append("\" is ").Append(N(wide))
                .Append(" wide and ").Append(N(tall)).Append(" tall at ")
                .Append(N(label.Ceiling)).Append(", ").Append(N(atFloor))
                .Append(" wide at ").Append(N(label.Lowest))
                .Append(", against ").Append(N(room)).Append(" of room and ")
                .Append(N(rect.height - margin.y - margin.w)).Append(" of height")
                .Append(", so it is drawn at ").Append(N(size)).Append(".");

            return line.ToString();
        }

        /// <summary>
        /// Every enabled mask between the label and the top of the panel, nearest first.
        ///
        /// Twelve levels, which is further than the requirement slot sits from the inventory
        /// screen's root; the bound is only there so a strange hierarchy cannot make a
        /// diagnostic walk for long.
        /// </summary>
        private static string Masks(Transform from, RectTransform label)
        {
            var found = new StringBuilder();
            var t = from;

            for (var depth = 0; depth < 12 && t != null; depth++, t = t.parent)
            {
                RectMask2D soft;
                if (t.TryGetComponent(out soft) && soft.enabled)
                    Name(found, "RectMask2D", t, label);

                Mask hard;
                if (t.TryGetComponent(out hard) && hard.enabled)
                    Name(found, "Mask", t, label);
            }

            return found.Length == 0 ? "none" : found.ToString();
        }

        private static void Name(StringBuilder found, string kind, Transform t,
                                 RectTransform label)
        {
            if (found.Length > 0) found.Append(", ");
            found.Append(kind).Append(" on ").Append(t.name);

            var rect = t as RectTransform;
            if (rect != null)
                found.Append(" (").Append(N(rect.rect.width)).Append(" wide, x ")
                     .Append(Span(rect, label)).Append(")");
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>
        /// Where a rect's left and right edges fall in another rect's local space.
        ///
        /// Through world space rather than by adding anchored positions, because the two sit
        /// at different depths of the hierarchy and any parent between them may be scaled or
        /// offset. Local x reads left to right on screen as long as the label is not rotated,
        /// and nothing about a requirement slot suggests it is; if it were, the edges logged
        /// would say so by coming out in an order that makes no sense.
        /// </summary>
        private static string Span(RectTransform of, RectTransform frame)
        {
            of.GetWorldCorners(Corners);

            var left = float.PositiveInfinity;
            var right = float.NegativeInfinity;

            for (var i = 0; i < 4; i++)
            {
                var x = frame.InverseTransformPoint(Corners[i]).x;
                left = Mathf.Min(left, x);
                right = Mathf.Max(right, x);
            }

            return N(left) + " to " + N(right);
        }

        /// <summary>
        /// A number for the log, the same on every machine. This one is Dutch, and "12,5"
        /// in a line about widths reads as two numbers.
        /// </summary>
        private static string N(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
