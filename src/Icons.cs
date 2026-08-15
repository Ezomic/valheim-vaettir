using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// PNGs off disk, turned into Sprites.
    ///
    /// Valheim builds both its item icons and its piece icons from a camera rig in
    /// the editor, and there is no editor at runtime - so a rendered PNG beside the
    /// dll and Sprite.Create over it is the whole of the answer. This started life
    /// private inside HeartwoodPrefab; the sapling needed exactly the same thing for
    /// Piece.m_icon, and two copies of a reflection lookup is one too many.
    /// </summary>
    internal static class Icons
    {
        /// <summary>
        /// Reads a PNG beside the dll. Null if it is missing or unreadable, and the
        /// caller is expected to carry on without it rather than fail - an icon is
        /// the most cosmetic thing in the mod, and the donor's own picture is a poor
        /// but survivable fallback.
        /// </summary>
        public static Sprite Load(string fileName, string label)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            var directory = Path.GetDirectoryName(typeof(Icons).Assembly.Location);
            var path = Path.Combine(directory, fileName);

            if (!File.Exists(path))
            {
                GrovePlugin.LogOnce(
                    "No " + fileName + " beside the dll - " + label + " will use the "
                    + "donor's icon, which is someone else's picture.");
                return null;
            }

            try
            {
                // Bilinear, not point, even though everything else in these mods wants
                // point. The source is 128px and the inventory draws it smaller, so it
                // is always being minified, and point-sampling a minified image is how
                // you get a shimmering mess as the slot moves.
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                if (!LoadPng(texture, File.ReadAllBytes(path))) return null;

                texture.name = label + "_icon";
                texture.hideFlags = HideFlags.HideAndDontSave;

                return Sprite.Create(texture,
                                     new Rect(0f, 0f, texture.width, texture.height),
                                     new Vector2(0.5f, 0.5f));
            }
            catch (System.Exception e)
            {
                GrovePlugin.Log.LogError("Could not read " + fileName + ": " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Texture2D.LoadImage, by reflection.
        ///
        /// It lives in UnityEngine.ImageConversionModule, which targets netstandard 2.1
        /// while this builds against net462 - referencing it outright fails the build
        /// with CS1705. The method is present at runtime regardless, so reaching it
        /// this way costs one lookup and removes the whole problem.
        /// </summary>
        private static bool LoadPng(Texture2D texture, byte[] data)
        {
            var type = AccessTools.TypeByName("UnityEngine.ImageConversion");
            if (type == null)
            {
                GrovePlugin.LogOnce("UnityEngine.ImageConversion is missing - "
                                    + "cannot read icons.");
                return false;
            }

            var method = AccessTools.Method(type, "LoadImage",
                                            new[] { typeof(Texture2D), typeof(byte[]) })
                         ?? AccessTools.Method(type, "LoadImage",
                                               new[] { typeof(Texture2D), typeof(byte[]),
                                                       typeof(bool) });

            if (method == null)
            {
                GrovePlugin.LogOnce("No LoadImage overload found on "
                                    + "UnityEngine.ImageConversion.");
                return false;
            }

            var args = method.GetParameters().Length == 3
                ? new object[] { texture, data, false }
                : new object[] { texture, data };

            return (bool)method.Invoke(null, args);
        }
    }
}
