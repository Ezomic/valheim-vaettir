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
            var wanted = container != null;

            if (_button.gameObject.activeSelf != wanted)
                _button.gameObject.SetActive(wanted);

            if (!wanted) return;

            if (StowPost.Is(container))
            {
                var fetch = PostRules.Fetch(container).Count;
                _label.text = fetch == 0 ? "Post…" : "Post · fetch " + fetch;
                return;
            }

            var entries = ChestFilter.Entries(container).Count;
            _label.text = entries == 0 ? "Holds…" : "Holds " + entries;
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

            // Directly under the button it was cloned from. Offsetting by the source's own
            // height keeps the gap right whatever the resolution, rather than picking a
            // number that happens to look right at 1080p.
            var rect = clone.GetComponent<RectTransform>();
            var from = source.GetComponent<RectTransform>();
            if (rect != null && from != null)
                rect.anchoredPosition = from.anchoredPosition
                                        - new Vector2(0f, from.rect.height + 4f);

            StowPlugin.Log.LogInfo("Rules button added to the container window.");
            return true;
        }

        private static void OnPressed()
        {
            var gui = InventoryGui.instance;
            if (gui == null) return;

            var container = StowPatches.CurrentContainer(gui);
            if (container == null) return;

            // The post's own settings, or a chest's rule. Toggled rather than opened, so
            // the same press that opened it closes it again - and the other panel is shut
            // either way, because two 520px windows over one chest window is a mess.
            if (StowPost.Is(container))
            {
                if (PostPanel.IsOpen) PostPanel.Close();
                else { FilterPanel.Close(); PostPanel.Open(container); }
                return;
            }

            if (FilterPanel.IsOpen) FilterPanel.Close();
            else { PostPanel.Close(); FilterPanel.Open(container); }
        }
    }
}
