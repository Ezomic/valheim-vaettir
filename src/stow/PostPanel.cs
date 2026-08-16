using System.Collections.Generic;
using UnityEngine;

namespace Stow
{
    /// <summary>
    /// The post's own settings, in two tabs.
    ///
    /// Layout A of three that were mocked up, and the mockup is the spec - the same 520px
    /// width, the same palette, the same monospace face and the same 118px cells as the
    /// chest panel, so the two read as one mod rather than as two windows that happen to
    /// be in the same plugin.
    ///
    /// Tabs rather than one column, which was the trade taken knowingly: a setting behind
    /// a tab is a setting people do not know exists, and the answer to that is the caption
    /// under the tab strip naming what is on the other one.
    ///
    /// Everything here writes straight through to the post's ZDO. There is no Save button
    /// because there is nothing one could protect you from - every control is a toggle,
    /// and the undo for a toggle is the same toggle.
    /// </summary>
    internal static class PostPanel
    {
        public static bool IsOpen { get; private set; }

        private const float WindowWidth = 520f;
        private const float CellWidth = 118f;
        private const float CellGap = 4f;
        private const int Columns = 4;

        private static Rect _window = new Rect(120f, 120f, WindowWidth, 0f);

        private static Container _post;
        private static List<string> _fetch = new List<string>();
        private static string _search = "";
        private static Vector2 _searchScroll;
        private static bool _settings;

        public static void Open(Container post)
        {
            if (post == null) return;

            _post = post;
            _fetch = PostRules.Fetch(post);
            _search = "";
            _searchScroll = Vector2.zero;
            _settings = false;
            IsOpen = true;
        }

        public static void Close()
        {
            IsOpen = false;
            _post = null;
            _search = "";
        }

        public static void Draw()
        {
            if (!IsOpen) return;

            // A post that was torn down, or that we walked away from, is not a post.
            if (_post == null || Player.m_localPlayer == null) { Close(); return; }

            FilterPanel.Styles.Build();

            _window = GUILayout.Window(0x57015, _window, DrawWindow, GUIContent.none,
                                       FilterPanel.Styles.Window, GUILayout.Width(WindowWidth));
        }

        private static void DrawWindow(int id)
        {
            DrawHeader();
            DrawTabs();

            if (_settings) DrawSettings();
            else DrawFetch();

            GUILayout.Space(6f);
            FilterPanel.Rule();
            DrawFooter();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private static void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.instance.Localize(_post.m_name),
                            FilterPanel.Styles.Title);
            GUILayout.FlexibleSpace();

            var inventory = _post.GetInventory();
            var waiting = inventory != null ? inventory.NrOfItems() : 0;
            GUILayout.Label(waiting == 0 ? "empty" : waiting + " waiting",
                            FilterPanel.Styles.Dim);

            GUILayout.EndHorizontal();
            FilterPanel.Rule();
        }

        private static void DrawTabs()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Fetch", _settings ? FilterPanel.Styles.CellOff
                                                    : FilterPanel.Styles.CellOn,
                                 GUILayout.Width(CellWidth), GUILayout.Height(24f)))
                _settings = false;

            GUILayout.Space(CellGap);

            if (GUILayout.Button("Settings", _settings ? FilterPanel.Styles.CellOn
                                                       : FilterPanel.Styles.CellOff,
                                 GUILayout.Width(CellWidth), GUILayout.Height(24f)))
                _settings = true;

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Says what is on the tab you are not looking at. A tab hides things, and the
            // cheapest fix for that is to name them.
            FilterPanel.Caption(_settings
                ? "FETCH  ·  WHAT SPIRITS BRING BACK TO THIS POST"
                : "SETTINGS  ·  TIDYING, AND WHERE THE SPIRIT LIVES");
        }

        // ------------------------------------------------------------------ fetch

        /// <summary>
        /// The same grid, chips and search the chest panel uses, pointed the other way.
        ///
        /// A post asking for ore and a chest offering to hold ore are the same sentence,
        /// so they get the same control and the same stored format - down to refusals,
        /// which on a fetch list mean "bring ore, but never tin".
        /// </summary>
        private static void DrawFetch()
        {
            FilterPanel.Caption("BRING THESE TO THE POST");
            FilterPanel.DrawGroupGrid(_fetch, Commit, CellWidth, CellGap, Columns);

            GUILayout.Space(6f);
            FilterPanel.Caption("SINGLE ITEMS");
            FilterPanel.DrawItemPicker(_fetch, Commit, ref _search, ref _searchScroll);
            FilterPanel.DrawItemChips(_fetch, Commit, WindowWidth);

            if (_fetch.Count == 0)
                GUILayout.Label("   nothing asked for - the post only sends things out",
                                FilterPanel.Styles.Dim);
        }

        // ------------------------------------------------------------------ settings

        private static void DrawSettings()
        {
            Row("Tidy", "pull misplaced items out of chests",
                PostRules.Tidy(_post), on => PostRules.SetTidy(_post, on));

            GUILayout.Space(CellGap);

            var lives = PostRules.Where(_post) == PostRules.Presence.LivesHere;
            Row("Spirit", lives ? "lives here, and sleeps" : "only while there is work",
                lives, on => PostRules.SetPresence(
                    _post, on ? PostRules.Presence.LivesHere : PostRules.Presence.OnlyWorking));

            GUILayout.Space(6f);
            GUILayout.Label(
                "   Tidy moves an item that is sitting in a chest whose rule refuses it.\n"
                + "   It only ever corrects a mistake, so it cannot churn a room that is\n"
                + "   already right.", FilterPanel.Styles.Dim);
        }

        /// <summary>One setting: a fixed-width name, a reason, and a switch.</summary>
        private static void Row(string name, string why, bool on, System.Action<bool> set)
        {
            GUILayout.BeginHorizontal();

            // Fixed width, as the mockup has it and as this repo's UI rule requires -
            // nothing may reflow when a label changes length.
            GUILayout.Label("  " + name, FilterPanel.Styles.Title, GUILayout.Width(104f));
            GUILayout.Label(why, FilterPanel.Styles.Dim);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(on ? "ON" : "OFF",
                                 on ? FilterPanel.Styles.CellOn : FilterPanel.Styles.CellOff,
                                 GUILayout.Width(62f), GUILayout.Height(24f)))
                set(!on);

            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ footer

        private static void DrawFooter()
        {
            GUILayout.BeginHorizontal();

            if (!_settings)
            {
                if (GUILayout.Button("Clear", FilterPanel.Styles.CellOff, GUILayout.Height(24f)))
                {
                    _fetch.Clear();
                    Commit();
                }

                GUILayout.Space(CellGap);
            }

            if (GUILayout.Button("Close", FilterPanel.Styles.CellOff, GUILayout.Height(24f)))
                Close();

            GUILayout.EndHorizontal();
        }

        private static void Commit()
        {
            PostRules.SetFetch(_post, _fetch);
        }
    }
}
