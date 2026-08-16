using System.Collections.Generic;
using UnityEngine;

namespace Stow
{
    /// <summary>
    /// Saying what a chest holds.
    ///
    /// Groups are the control and single items are the escape hatch, in that order and at
    /// that weight. Ticking "ore" is one press and stays right when a mod adds a twelfth
    /// ore; naming eleven ores by hand is eleven presses and goes stale. So the groups are
    /// a grid you can hit without reading, and the item picker is a search box below them
    /// for the handful of cases a group cannot express - a chest for surtling cores, say,
    /// which is not a category of anything.
    ///
    /// Every change writes through to the chest immediately. There is no Save button
    /// because there is nothing a Save button could protect you from: every control here
    /// is a toggle, and the undo for a toggle is the same toggle.
    /// </summary>
    internal static class FilterPanel
    {
        public static bool IsOpen { get; private set; }

        private const float WindowWidth = 520f;
        private const float CellWidth = 118f;
        private const float CellGap = 4f;
        private const int Columns = 4;

        private static Rect _window = new Rect(90f, 90f, WindowWidth, 0f);

        private static Container _container;
        private static List<string> _entries = new List<string>();

        /// <summary>
        /// Which list the shared controls are editing, and how to save it.
        ///
        /// The grid, the picker and the chips are drawn for the chest panel and for the
        /// post's fetch tab, which are the same sentence pointed in opposite directions -
        /// so they take a target rather than reaching for this panel's own list. Set for
        /// the duration of one draw call, which is safe because IMGUI draws one window at
        /// a time on one thread.
        /// </summary>
        private static List<string> _edit;
        private static System.Action _save;

        private static void Editing(List<string> entries, System.Action commit)
        {
            _edit = entries;
            _save = commit;
        }

        private static string _search = "";
        private static Vector2 _searchScroll;

        public static void Open(Container container)
        {
            if (container == null) return;

            _container = container;
            _entries = ChestFilter.Entries(container);
            _search = "";
            _searchScroll = Vector2.zero;
            IsOpen = true;
        }

        public static void Close()
        {
            IsOpen = false;
            _container = null;
            _search = "";
        }

        public static void Draw()
        {
            if (!IsOpen) return;

            // A chest that was torn down, or that we walked away from, is not a chest.
            if (_container == null || Player.m_localPlayer == null) { Close(); return; }

            Styles.Build();

            _window = GUILayout.Window(0x57014, _window, DrawWindow, GUIContent.none,
                                       Styles.Window, GUILayout.Width(WindowWidth));
        }

        private static void DrawWindow(int id)
        {
            DrawHeader();

            Caption("GROUPS");
            DrawGroupGrid(_entries, CommitChest, CellWidth, CellGap, Columns);

            GUILayout.Space(6f);
            Caption("SINGLE ITEMS");
            DrawItemPicker(_entries, CommitChest, ref _search, ref _searchScroll);
            DrawItemChips(_entries, CommitChest, WindowWidth);

            GUILayout.Space(6f);
            Rule();
            DrawFooter();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        // ------------------------------------------------------------------ header

        private static void DrawHeader()
        {
            var inventory = _container.GetInventory();

            var kinds = 0;
            var used = 0;
            var slots = 0;

            if (inventory != null)
            {
                var seen = new HashSet<string>();
                foreach (var item in inventory.GetAllItems())
                    if (item != null && item.m_shared != null) seen.Add(item.m_shared.m_name);

                kinds = seen.Count;
                used = inventory.NrOfItems();
                slots = inventory.GetWidth() * inventory.GetHeight();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.instance.Localize(_container.m_name), Styles.Title);
            GUILayout.FlexibleSpace();
            GUILayout.Label("holds " + kinds + " kind" + (kinds == 1 ? "" : "s")
                            + "  ·  " + used + "/" + slots + " slots", Styles.Dim);
            GUILayout.EndHorizontal();

            Rule();
        }

        // ------------------------------------------------------------------ groups

        public static void DrawGroupGrid(List<string> entries, System.Action commit,
                                         float cellWidth, float cellGap, int columns)
        {
            Editing(entries, commit);
            DrawGroups(cellWidth, cellGap, columns);
        }

        private static void DrawGroups(float CellWidth, float CellGap, int Columns)
        {
            var groups = ItemGroups.Groups;

            // The catch-all rides at the end of the same grid rather than sitting in the
            // footer as its own control. It answers the same question every other cell
            // answers - "does this belong here" - and pulling it out would imply it is a
            // different kind of setting.
            var cells = groups.Count + 1;
            var rows = (cells + Columns - 1) / Columns;

            for (var row = 0; row < rows; row++)
            {
                GUILayout.BeginHorizontal();

                for (var column = 0; column < Columns; column++)
                {
                    var index = row * Columns + column;
                    if (index >= cells) { GUILayout.Space(CellWidth + CellGap); continue; }

                    if (index < groups.Count)
                    {
                        var group = groups[index];
                        Cell(group.Display, "@" + group.Id, CellWidth);
                    }
                    else
                    {
                        Cell("Anything else", ChestFilter.CatchAll, CellWidth);
                    }

                    if (column < Columns - 1) GUILayout.Space(CellGap);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(CellGap);
            }
        }

        /// <summary>
        /// Three states, not two: ignored, held, refused.
        ///
        /// A cycle rather than a second control beside each cell, because the three are
        /// answers to one question - does this belong here - and splitting them into a
        /// switch and a checkbox would imply they can both be true. Refused is the last
        /// stop so the common two are one press apart and the rare one costs an extra.
        ///
        /// Refused is gold text on the dark fill against held's gold fill: both read as
        /// "you set this", which is the thing that separates them from a cell you have
        /// not touched, and the wording carries the rest.
        /// </summary>
        private static void Cell(string label, string entry, float CellWidth)
        {
            var refused = _edit.Contains("-" + entry);
            var on = _edit.Contains(entry);

            var style = refused ? Styles.CellNot : on ? Styles.CellOn : Styles.CellOff;

            if (GUILayout.Button(refused ? "not " + label : label, style,
                                 GUILayout.Width(CellWidth), GUILayout.Height(24f)))
                Cycle(entry);
        }

        // ------------------------------------------------------------------ items

        public static void DrawItemPicker(List<string> entries, System.Action commit,
                                          ref string search, ref Vector2 scroll)
        {
            Editing(entries, commit);

            _search = search;
            _searchScroll = scroll;
            DrawItemPicker();
            search = _search;
            scroll = _searchScroll;
        }

        private static void DrawItemPicker()
        {
            GUILayout.BeginHorizontal();
            _search = GUILayout.TextField(_search, Styles.Field, GUILayout.Height(22f));
            if (GUILayout.Button("add", Styles.CellOff, GUILayout.Width(52f), GUILayout.Height(22f)))
                AddFirstMatch();
            GUILayout.EndHorizontal();

            if (_search.Trim().Length == 0) return;

            var matches = Matches(_search.Trim());
            if (matches.Count == 0)
            {
                GUILayout.Label("   nothing matches", Styles.Dim);
                return;
            }

            // Vertical only. A horizontal scrollbar here would clip the right-hand end of
            // every name, which on a picker is the half you are reading.
            _searchScroll = GUILayout.BeginScrollView(_searchScroll, false, true,
                                                      GUIStyle.none, GUI.skin.verticalScrollbar,
                                                      Styles.List, GUILayout.Height(120f));

            foreach (var prefab in matches)
            {
                if (GUILayout.Button(" " + ItemGroups.DisplayNameOf(prefab), Styles.Row,
                                     GUILayout.Height(20f)))
                {
                    // Shift refuses instead of holds. The grid can cycle because a cell
                    // stays put and can be pressed again; a search result vanishes the
                    // moment it is picked, so there is nothing left to cycle - the
                    // modifier has to be part of the same press.
                    if (Event.current != null && Event.current.shift) Refuse(prefab);
                    else Toggle(prefab);

                    _search = "";
                    _searchScroll = Vector2.zero;
                    break;
                }
            }

            GUILayout.EndScrollView();
            Caption("shift-click to refuse an item instead of holding it");
        }

        /// <summary>
        /// The chips are laid out by measuring rather than by wrapping, because IMGUI has
        /// no flow layout and a horizontal group would run off the edge of the window.
        /// </summary>
        public static void DrawItemChips(List<string> entries, System.Action commit,
                                         float windowWidth)
        {
            Editing(entries, commit);
            DrawItemChips(windowWidth);
        }

        private static void DrawItemChips(float WindowWidth)
        {
            // Refusals are chips too. They are entries on the same rule and have to be
            // removable the same way - an exclusion you can create and not delete is a
            // chest you have to Clear entirely to fix.
            var chips = new List<string>();
            foreach (var entry in _edit)
            {
                var bare = ChestFilter.Bare(entry);
                if (ChestFilter.IsGroup(bare) || bare == ChestFilter.CatchAll) continue;

                chips.Add(entry);
            }

            if (chips.Count == 0) return;

            GUILayout.Space(4f);

            var available = WindowWidth - 40f;
            var line = 0f;
            var open = false;
            string remove = null;

            foreach (var prefab in chips)
            {
                var refused = ChestFilter.IsExclusion(prefab);
                var name = ItemGroups.DisplayNameOf(ChestFilter.Bare(prefab));
                var label = (refused ? "not " + name : name) + "  ×";
                var width = Styles.Chip.CalcSize(new GUIContent(label)).x + 4f;

                if (!open || line + width > available)
                {
                    if (open) { GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); }
                    GUILayout.BeginHorizontal();
                    open = true;
                    line = 0f;
                }

                if (GUILayout.Button(label, Styles.Chip, GUILayout.Width(width),
                                     GUILayout.Height(20f)))
                    remove = prefab;

                line += width + 4f;
            }

            if (open) { GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); }

            if (remove != null) Toggle(remove);
        }

        private static List<string> Matches(string text)
        {
            var found = new List<string>();
            if (ObjectDB.instance == null || ObjectDB.instance.m_items == null) return found;

            foreach (var prefab in ObjectDB.instance.m_items)
            {
                if (prefab == null || found.Count >= 60) continue;

                var display = ItemGroups.DisplayNameOf(prefab.name);
                if (display.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) < 0
                    && prefab.name.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (!found.Contains(prefab.name)) found.Add(prefab.name);
            }

            found.Sort(System.StringComparer.OrdinalIgnoreCase);
            return found;
        }

        private static void AddFirstMatch()
        {
            var text = _search.Trim();
            if (text.Length == 0) return;

            var matches = Matches(text);
            if (matches.Count == 0) return;

            Toggle(matches[0]);
            _search = "";
            _searchScroll = Vector2.zero;
        }

        // ------------------------------------------------------------------ footer

        private static void DrawFooter()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Learn from contents", Styles.CellOff, GUILayout.Height(24f)))
                LearnFromContents();

            GUILayout.Space(CellGap);
            if (GUILayout.Button("Clear", Styles.CellOff, GUILayout.Height(24f)))
            {
                _entries.Clear();
                CommitChest();
            }

            GUILayout.Space(CellGap);
            if (GUILayout.Button("Close", Styles.CellOff, GUILayout.Height(24f))) Close();

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Turns what is in the chest into what the chest asks for.
        ///
        /// Named items rather than guessed groups: a chest holding copper and tin might
        /// mean "ore" or might mean those two, and picking the wider reading for you would
        /// quietly claim every ore in the world for it. Widening is one press away in the
        /// grid above; unwinding a wrong guess is not.
        /// </summary>
        private static void LearnFromContents()
        {
            Editing(_entries, CommitChest);

            var inventory = _container.GetInventory();
            if (inventory == null) return;

            foreach (var item in inventory.GetAllItems())
            {
                var prefab = ItemGroups.PrefabNameOf(item);
                if (prefab == null || _edit.Contains(prefab)) continue;

                _edit.Add(prefab);
            }

            Commit();
        }

        private static void Toggle(string entry)
        {
            if (_edit.Contains(entry)) _edit.Remove(entry);
            else _edit.Add(entry);

            Commit();
        }

        /// <summary>Adds an entry as a refusal, replacing any held form of it.</summary>
        private static void Refuse(string entry)
        {
            var not = "-" + entry;

            _edit.Remove(entry);
            if (!_edit.Contains(not)) _edit.Add(not);

            Commit();
        }

        /// <summary>Ignored -> held -> refused -> ignored.</summary>
        private static void Cycle(string entry)
        {
            var not = "-" + entry;

            // Both forms are cleared on the way through, never left co-existing. A rule
            // holding "@ore" and "-@ore" at once is readable in the ZDO and meaningless
            // to the matcher, which resolves exclusions first - so it would silently
            // behave as refused while the grid showed it held.
            if (_edit.Contains(entry))
            {
                _edit.Remove(entry);
                _edit.Add(not);
            }
            else if (_edit.Contains(not))
            {
                _edit.Remove(not);
            }
            else
            {
                _edit.Add(entry);
            }

            Commit();
        }

        /// <summary>Saves whatever list is being edited, by whatever means it saves.</summary>
        private static void Commit()
        {
            if (_save != null) _save();
        }

        /// <summary>This panel's own save: the rule goes back on the chest.</summary>
        private static void CommitChest()
        {
            ChestFilter.Write(_container, _entries);
            RulesButton.Refresh();
        }

        // ------------------------------------------------------------------ chrome

        public static void Caption(string text)
        {
            GUILayout.Label(text, Styles.Caption);
        }

        public static void Rule()
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), Styles.RuleLine);
            GUILayout.Space(5f);
        }

        /// <summary>
        /// Built on first paint, not at load: GUI.skin only exists inside OnGUI, and the
        /// styles derive from it so the panel keeps whatever skin the game is using.
        /// </summary>
        public static class Styles
        {
            public static GUIStyle Window, Title, Dim, Caption, CellOn, CellOff, CellNot, Chip, Row,
                                   Field, List;

            public static Texture2D RuleLine;

            private static bool _built;

            public static void Build()
            {
                if (_built) return;
                _built = true;

                RuleLine = Theme.Solid(Theme.Rule);

                Window = new GUIStyle(GUI.skin.window)
                {
                    padding = new RectOffset(14, 14, 12, 12),
                    border = new RectOffset(1, 1, 1, 1)
                };
                Paint(Window, Theme.Bordered(Theme.Bg, Theme.Rule));

                Title = Label(14, Theme.Gold);
                Dim = Label(12, Theme.Dim);

                Caption = Label(12, Theme.Dim);
                Caption.margin = new RectOffset(0, 0, 2, 4);

                CellOff = Button(13, Theme.Ink, Theme.Row);
                CellOn = Button(13, Theme.Bg, Theme.Gold);

                // Gold lettering on the dark fill. Both it and CellOn read as "you set
                // this", which is what separates them from a cell nobody has touched;
                // the "not " in front carries which of the two it is. No new colour -
                // the palette is the mockup's and a red would be the only one in it.
                CellNot = Button(13, Theme.Gold, Theme.Bg);

                Chip = Button(12, Theme.Gold, Theme.Panel);
                Chip.padding = new RectOffset(8, 8, 2, 2);

                Row = Button(13, Theme.Ink, Color.clear);
                Row.alignment = TextAnchor.MiddleLeft;

                Field = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = 13,
                    padding = new RectOffset(6, 6, 3, 3)
                };
                if (Theme.Mono != null) Field.font = Theme.Mono;
                Field.normal.textColor = Theme.Ink;
                Field.focused.textColor = Theme.Ink;

                List = new GUIStyle(GUI.skin.box) { padding = new RectOffset(2, 2, 2, 2) };
                Paint(List, Theme.Bordered(Theme.Panel, Theme.Rule));
                List.border = new RectOffset(1, 1, 1, 1);
            }

            private static GUIStyle Label(int size, Color colour)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size,
                    padding = new RectOffset(0, 0, 1, 1),
                    margin = new RectOffset(0, 0, 0, 0),
                    wordWrap = false,
                    clipping = TextClipping.Clip
                };

                if (Theme.Mono != null) style.font = Theme.Mono;
                style.normal.textColor = colour;

                return style;
            }

            private static GUIStyle Button(int size, Color ink, Color fill)
            {
                var style = new GUIStyle(GUI.skin.button)
                {
                    fontSize = size,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(4, 4, 2, 2),
                    margin = new RectOffset(0, 0, 0, 0),
                    wordWrap = false,
                    clipping = TextClipping.Clip
                };

                if (Theme.Mono != null) style.font = Theme.Mono;

                Paint(style, fill == Color.clear ? Theme.Clear() : Theme.Solid(fill));

                style.normal.textColor = ink;
                style.hover.textColor = ink;
                style.active.textColor = ink;
                style.focused.textColor = ink;

                return style;
            }

            private static void Paint(GUIStyle style, Texture2D texture)
            {
                style.normal.background = texture;
                style.hover.background = texture;
                style.active.background = texture;
                style.focused.background = texture;
                style.onNormal.background = texture;
                style.onHover.background = texture;
                style.onActive.background = texture;
                style.onFocused.background = texture;
            }
        }
    }
}
