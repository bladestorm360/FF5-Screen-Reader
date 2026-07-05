using FFV_ScreenReader.Core;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Appends the cursor's position in a list to a menu announcement, e.g. "Potion, 5" → "Potion, 5, (3 of 12)".
    /// Self-gates on the "Menu Position Announcements" toggle so call sites never repeat the gating logic.
    ///
    /// The leading comma is deliberate and uniform: many entries end in a number (stat values, spell
    /// levels/MP, item quantities), and a bare "(3 of 12)" would let that trailing number blur into the
    /// position ("...59 3 of 12"). The comma forces a prosodic break, and reads fine on plain text too
    /// ("New game, (1 of 5)"). Strips/trims nothing — icon markup is already stripped upstream.
    /// </summary>
    public static class MenuPosition
    {
        /// <param name="text">The already-assembled announcement string.</param>
        /// <param name="index">ZERO-BASED cursor index within the list.</param>
        /// <param name="count">Logical total count of the list.</param>
        public static string Format(string text, int index, int count)
        {
            if (!PreferencesManager.MenuPositionAnnouncementsEnabled) return text;
            if (string.IsNullOrEmpty(text)) return text;
            if (count <= 1) return text;                  // skip the pointless "1 of 1" / empty / unknown
            if (index < 0 || index >= count) return text; // never emit "0 of 5" / "6 of 5"
            try
            {
                // Reuse the mod's existing, fully-translated "{0} of {1}" key so the position is
                // localized (word order differs per language). Comma + parens stay literal.
                return text + ", (" + string.Format(ModTextTranslator.T("{0} of {1}"), index + 1, count) + ")";
            }
            catch
            {
                return $"{text}, ({index + 1} of {count})"; // English fallback if a translation is malformed
            }
        }
    }
}
