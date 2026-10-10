using UnityEngine;

namespace DshMobile
{
    /// <summary>
    /// A CJK-capable <see cref="Font"/> for uGUI, resolved from the operating system at
    /// runtime so the project still ships no font asset — the same rule the IMGUI UI lived
    /// by. IMGUI's built-in font quietly fell back to OS fonts for Chinese; uGUI's Text
    /// does not, so that fallback has to be requested explicitly here.
    ///
    /// Order matters: Android ships Noto Sans CJK, Windows ships Microsoft YaHei/SimHei,
    /// macOS ships PingFang SC. "sans-serif" is the Android family alias that covers CJK,
    /// and "Arial" is the last-ditch desktop name.
    /// </summary>
    public static class UguiFont
    {
        private static readonly string[] Candidates =
        {
            "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC", "PingFang SC",
            "Heiti SC", "Microsoft YaHei", "SimHei", "Droid Sans Fallback",
            "sans-serif", "Arial"
        };

        private static Font _font;
        private static int _size = -1;

        /// <summary>Resolves (and caches) a dynamic CJK font at the requested size.</summary>
        public static Font Resolve(int size = 28)
        {
            if (_font != null && size == _size) return _font;

            Font resolved = null;
            for (int i = 0; i < Candidates.Length; i++)
            {
                try
                {
                    Font candidate = Font.CreateDynamicFontFromOSFont(Candidates[i], size);
                    if (candidate != null)
                    {
                        resolved = candidate;
                        break;
                    }
                }
                catch
                {
                    // The name is not a font on this platform; try the next candidate.
                }
            }

            if (resolved == null)
            {
                // Last resort: Unity's legacy runtime font. It may not cover CJK, but it
                // guarantees a non-null font so text still appears.
                resolved = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            _font = resolved;
            _size = size;
            return _font;
        }

        /// <summary>
        /// Whether the font covers representative CJK glyphs. '宠' and '房' are the two
        /// characters of the project's title words; a font that renders them renders the UI.
        /// </summary>
        public static bool CoversCjk(Font font)
        {
            if (font == null) return false;
            font.RequestCharactersInTexture("宠物房间", 32);
            return font.HasCharacter('宠') && font.HasCharacter('房');
        }

        /// <summary>Drops the cached font so the next Resolve re-queries the OS.</summary>
        public static void ClearCache()
        {
            _font = null;
            _size = -1;
        }
    }
}
