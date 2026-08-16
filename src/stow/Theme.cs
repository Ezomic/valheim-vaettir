using UnityEngine;

namespace Stow
{
    /// <summary>
    /// The palette and the textures the menu is drawn from.
    ///
    /// Everything here exists because IMGUI has no stylesheet: a GUIStyle paints itself
    /// from a Texture2D, so a colour scheme means generating one image per surface. They
    /// are three pixels square and nine-sliced - one pixel of border, one of fill, scaled
    /// to whatever the control turns out to be - which is how a one-pixel outline survives
    /// being stretched across a 700px window.
    ///
    /// Colours are the mockup's, unchanged.
    /// </summary>
    internal static class Theme
    {
        public static readonly Color Bg    = Hex(0x22282E);
        public static readonly Color Panel = Hex(0x2C343C);
        public static readonly Color Row   = Hex(0x333C45);
        public static readonly Color Ink   = Hex(0xD8DEE4);
        public static readonly Color Dim   = Hex(0x8A97A3);
        public static readonly Color Gold  = Hex(0xD9A441);
        public static readonly Color Rule  = Hex(0x414C56);

        /// <summary>The hairline under each settings row - white at 4%, as in the mockup.</summary>
        public static readonly Color Hair = new Color(1f, 1f, 1f, 0.04f);

        private static Font _font;

        /// <summary>
        /// A monospace face, because the mockup is set in one and because a column of
        /// numbers only lines up in a font whose digits are the same width.
        ///
        /// Null if none of these are installed, which GUIStyle reads as "use the skin's
        /// font" - so the menu degrades to Valheim's own face rather than to nothing.
        /// </summary>
        public static Font Mono
        {
            get
            {
                if (_font == null)
                {
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Consolas", "Cascadia Mono", "Courier New", "Lucida Console" }, 12);

                    if (_font != null) _font.hideFlags = HideFlags.HideAndDontSave;
                }

                return _font;
            }
        }

        public static Texture2D Solid(Color colour)
        {
            var texture = Make(1, 1);
            texture.SetPixel(0, 0, colour);
            texture.Apply();

            return texture;
        }

        /// <summary>A one-pixel outline around a fill. Use with border = 1 on every side.</summary>
        public static Texture2D Bordered(Color fill, Color border)
        {
            var texture = Make(3, 3);

            for (var x = 0; x < 3; x++)
                for (var y = 0; y < 3; y++)
                    texture.SetPixel(x, y, x == 1 && y == 1 ? fill : border);

            texture.Apply();
            return texture;
        }

        /// <summary>A hairline along the bottom edge and nothing else.</summary>
        public static Texture2D Underline(Color line)
        {
            var texture = Make(3, 3);

            for (var x = 0; x < 3; x++)
                for (var y = 0; y < 3; y++)
                    texture.SetPixel(x, y, y == 0 ? line : Color.clear);

            texture.Apply();
            return texture;
        }

        public static Texture2D Clear()
        {
            var texture = Make(1, 1);
            texture.SetPixel(0, 0, Color.clear);
            texture.Apply();

            return texture;
        }

        /// <summary>
        /// Point filtering so the one-pixel borders stay one pixel rather than blurring
        /// into a gradient, and HideAndDontSave so a scene change does not collect them
        /// out from under the styles still pointing at them.
        /// </summary>
        private static Texture2D Make(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.ARGB32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f,
                             ((rgb >> 8) & 0xFF) / 255f,
                             (rgb & 0xFF) / 255f);
        }
    }
}
