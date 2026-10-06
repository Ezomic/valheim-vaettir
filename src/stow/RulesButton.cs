using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Stow
{
    /// <summary>
    /// A "Holds…" button in the chest window, beside the game's own Stack all.
    ///
    /// It is cloned from `m_stackAllButton` rather than built, which is the whole trick:
    /// the clone inherits the skin, the font, the hover and press states, the click sound
    /// and the layout metrics, so it looks like it shipped with the game instead of like a
    /// mod drew a rectangle. Anything hand-built here would be a near-miss forever.
    ///
    /// It sits in the chest window because that is where you are standing when the
    /// question occurs to you. A keybind would work and did work, but it has to be
    /// remembered, and the whole point of the post was to stop asking you to remember
    /// things.
    /// </summary>
    internal static class RulesButton
    {
        private static Button _button;
        private static TMP_Text _label;

        /// <summary>
        /// Built the first time a container window is drawn, because the buttons it is
        /// cloned from are only wired up by then.
        /// </summary>
        public static void Sync(InventoryGui gui, Container container)
        {
            if (gui == null) return;

            if (_button == null && !Build(gui)) return;

            // One button, two jobs, because there is only ever one container window and
            // the two panels are never both relevant. A chest is asked what it holds; a
            // post is asked what it fetches and how it behaves. Building a second button
            // beside this one would put a dead control on screen for every container in
            // the game.
            // Hidden on a stowing post, not merely relabelled. A post has no settings of
            // its own in this release and it does not sort by its own rule either - it
            // distributes to the chests around it - so a rule editor opened on one would
            // edit something nothing reads. A button that does nothing is worse than no
            // button, which is the whole reason there is only ever one of these.
            var wanted = container != null && !StowPost.Is(container);

            if (_button.gameObject.activeSelf != wanted)
                _button.gameObject.SetActive(wanted);

            if (!wanted) return;

            var entries = ChestFilter.Entries(container).Count;
            _label.text = entries == 0 ? "Holds…" : "Holds " + entries;
        }

        /// <summary>
        /// Finds the button a spot that cannot cover an inventory cell, every frame the
        /// container window updates.
        ///
        /// It used to sit one button-height under Place stacks. That is clear of the grid in
        /// a narrow chest and on top of the top right cell in a wide one (a Reinforced chest
        /// is six columns), half hiding a stack of 100 behind it. LHM-58.
        ///
        /// Not placed from Show, because the grid is only sized and filled by the first
        /// UpdateContainer after it, so at Show there are no cells to measure. And not a
        /// fixed offset, because no offset under the buttons is right for every chest: the
        /// grid grows to the right edge. The row the buttons are in is the one place the
        /// grid never reaches, since the game itself puts Place stacks there. So the button
        /// goes in that row, to the left of the pair, and is checked against the cells, both
        /// buttons, the window's edge and the title's drawn text. If the row cannot take it
        /// (a long title, a narrow window) it goes under the lowest row of cells, which is
        /// clear of them by construction.
        /// </summary>
        public static void Place(InventoryGui gui)
        {
            if (_button == null || !_button.gameObject.activeSelf || gui == null) return;

            var layout = Measure(gui);
            if (layout == null) return;

            var rect = (RectTransform)_button.transform;
            const float gap = 4f;
            var size = layout.Button.size;

            var row = layout.Stack;
            if (layout.Take.width > 0f && Mathf.Abs(layout.Take.center.y - layout.Stack.center.y) < size.y)
                row = Union(layout.Take, layout.Stack);

            var beside = new Rect(row.xMin - gap - size.x, layout.Stack.y, size.x, size.y);
            var below = new Rect(layout.Stack.xMax - size.x, layout.LowestCell - gap - size.y, size.x, size.y);

            // Not clamped back up into the frame. That is what put the button on the bottom right
            // cell of a four row chest (LHM-75): the room under the last row was too small, the
            // button was pushed up to fit, and up is where the cells are. Outside the frame is
            // always free, so it hangs under the bottom edge, or over the top edge when that
            // would leave the screen.
            if (layout.Panel.width > 0f && below.yMin < layout.Panel.yMin)
            {
                below.y = layout.Panel.yMin - gap - size.y;
                if (LeavesScreen(gui, below))
                    below.y = layout.Panel.yMax + gap;
            }

            var chosen = below;
            if ((layout.Panel.width <= 0f || Inside(layout.Panel, beside)) && layout.Overlaps(beside) == 0)
                chosen = beside;

            var delta = chosen.position - layout.Button.position;
            if (delta.sqrMagnitude < 0.0001f) return;

            rect.anchoredPosition += delta;
        }

        private static bool LeavesScreen(InventoryGui gui, Rect spot)
        {
            var parent = _button.transform.parent as RectTransform;
            if (parent == null) return false;

            var low = parent.TransformPoint(new Vector3(spot.xMin, spot.yMin, 0f));
            return low.y < 0f;
        }

        /// <summary>
        /// Where everything in the container window is, in the window's own space.
        ///
        /// One measurement shared by the placement and by `stow holds`, so the scenario asks
        /// the same geometry the placement used. A cell scrolled out of a tall chest's view is
        /// clipped to the grid's viewport first: it is not on screen, and counting it would
        /// keep the button off a spot that is clear.
        /// </summary>
        internal sealed class Layout
        {
            public Rect Button;
            public Rect Stack;
            public Rect Take;
            public Rect Title;
            public Rect Panel;
            public readonly System.Collections.Generic.List<Rect> Cells = new System.Collections.Generic.List<Rect>();
            public float LowestCell;

            public int Overlaps(Rect candidate)
            {
                return OverlapCells(candidate) + OverlapOthers(candidate);
            }

            public int OverlapCells(Rect candidate)
            {
                var n = 0;
                foreach (var cell in Cells)
                    if (Touches(candidate, cell)) n++;
                return n;
            }

            public int OverlapOthers(Rect candidate)
            {
                var n = 0;
                if (Take.width > 0f && Touches(candidate, Take)) n++;
                if (Stack.width > 0f && Touches(candidate, Stack)) n++;
                if (Title.width > 0f && Touches(candidate, Title)) n++;
                return n;
            }
        }

        internal static Layout Measure(InventoryGui gui)
        {
            var source = gui.m_stackAllButton;
            var grid = gui.ContainerGrid;
            if (_button == null || source == null || grid == null || grid.m_gridRoot == null) return null;

            var parent = _button.transform.parent;
            if (parent == null) return null;

            var layout = new Layout
            {
                Button = In(parent, (RectTransform)_button.transform),
                Stack = In(parent, (RectTransform)source.transform),
                Take = gui.m_takeAllButton == null ? new Rect() : In(parent, (RectTransform)gui.m_takeAllButton.transform),
                Panel = parent is RectTransform ? ((RectTransform)parent).rect : new Rect(),
                LowestCell = float.MaxValue
            };

            var viewport = In(parent, (RectTransform)grid.transform);
            for (var i = 0; i < grid.m_gridRoot.childCount; i++)
            {
                var child = grid.m_gridRoot.GetChild(i) as RectTransform;
                // InventoryGrid destroys its old cells deferred, so for a frame they are still
                // there and still active; a destroyed one compares equal to null.
                if (child == null || child.gameObject == null || !child.gameObject.activeInHierarchy) continue;

                var cell = Clip(In(parent, child), viewport);
                if (cell.width <= 0f || cell.height <= 0f) continue;

                layout.Cells.Add(cell);
                if (cell.yMin < layout.LowestCell) layout.LowestCell = cell.yMin;
            }

            if (layout.Cells.Count == 0) layout.LowestCell = viewport.yMin;

            var title = gui.m_containerName;
            if (title != null && title.gameObject.activeInHierarchy && !string.IsNullOrEmpty(title.text))
            {
                var bounds = title.textBounds;
                var a = parent.InverseTransformPoint(title.transform.TransformPoint(bounds.min));
                var b = parent.InverseTransformPoint(title.transform.TransformPoint(bounds.max));
                layout.Title = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                                               Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }

            return layout;
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        private static Rect In(Transform parent, RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            var a = parent.InverseTransformPoint(Corners[0]);
            var b = parent.InverseTransformPoint(Corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                                   Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static Rect Clip(Rect a, Rect to)
        {
            var xMin = Mathf.Max(a.xMin, to.xMin);
            var yMin = Mathf.Max(a.yMin, to.yMin);
            var xMax = Mathf.Min(a.xMax, to.xMax);
            var yMax = Mathf.Min(a.yMax, to.yMax);
            return xMax <= xMin || yMax <= yMin ? new Rect() : Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static Rect Union(Rect a, Rect b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                                   Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }

        private static bool Inside(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin && inner.xMax <= outer.xMax
                   && inner.yMin >= outer.yMin && inner.yMax <= outer.yMax;
        }

        /// <summary>Overlap by more than a pixel either way; two rects that only share an edge do not touch.</summary>
        private static bool Touches(Rect a, Rect b)
        {
            return a.xMin < b.xMax - 1f && b.xMin < a.xMax - 1f
                   && a.yMin < b.yMax - 1f && b.yMin < a.yMax - 1f;
        }

        public static bool IsShown
        {
            get { return _button != null && _button.gameObject.activeInHierarchy; }
        }

        public static void Hide()
        {
            if (_button != null) _button.gameObject.SetActive(false);
        }

        /// <summary>Re-reads the count after the panel has changed a rule.</summary>
        public static void Refresh()
        {
            var gui = InventoryGui.instance;
            if (gui == null) return;

            Sync(gui, StowPatches.CurrentContainer(gui));
        }

        private static bool Build(InventoryGui gui)
        {
            var source = gui.m_stackAllButton;
            if (source == null) return false;

            var clone = Object.Instantiate(source.gameObject, source.transform.parent);
            clone.name = "StowRulesButton";

            _button = clone.GetComponent<Button>();
            if (_button == null) { Object.Destroy(clone); return false; }

            // The clone arrives carrying the original's listeners - Take all's, in this
            // case - which would empty the chest every time you asked what it holds.
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnPressed);

            _label = clone.GetComponentInChildren<TMP_Text>();
            if (_label != null)
            {
                _label.text = "Holds…";

                // Localization would otherwise re-resolve the donor's token on the next
                // language change and put "Stack all" back on our button.
                var localize = _label.GetComponent<Localize>();
                if (localize != null) Object.Destroy(localize);
            }

            // Not positioned here: where it can go depends on the grid, which does not exist
            // until the first UpdateContainer, and Place moves it from there.
            StowRuntime.Log.LogInfo("Rules button added to the container window.");
            return true;
        }

        private static void OnPressed()
        {
            var gui = InventoryGui.instance;
            if (gui == null) return;

            var container = StowPatches.CurrentContainer(gui);
            if (container == null) return;

            // Toggled rather than opened, so the same press that opened it closes it.
            //
            // A post has no settings of its own in this release. Fetch, tidy and presence
            // are held for 1.2, and with all three gone the post's panel had nothing left
            // in it - so the button is a chest rule button and nothing else.
            if (FilterPanel.IsOpen) FilterPanel.Close();
            else FilterPanel.Open(container);
        }
    }
}
