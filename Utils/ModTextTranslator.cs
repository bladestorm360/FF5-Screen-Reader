using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using MelonLoader;
using Il2CppLast.Management;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Translates mod-authored UI strings to the current game language.
    /// Keys are English strings; lookups fall back to English if a translation is missing.
    /// </summary>
    public static class ModTextTranslator
    {
        private static Dictionary<string, Dictionary<string, string>> translations;
        private static bool isInitialized = false;
        private static string cachedLanguageCode = "en";
        private static bool hasLoggedLanguage = false;

        private static readonly Dictionary<int, string> LanguageCodeMap = new()
        {
            {1,"ja"},{2,"en"},{3,"fr"},{4,"it"},{5,"de"},{6,"es"},
            {7,"ko"},{8,"zht"},{9,"zhc"},{10,"ru"},{11,"th"},{12,"pt"}
        };

        /// <summary>
        /// Detects the current game language via MessageManager and returns a language code.
        /// </summary>
        public static string DetectLanguage()
        {
            try
            {
                var mgr = MessageManager.Instance;
                if (mgr != null)
                {
                    int langId = (int)mgr.currentLanguage;
                    if (LanguageCodeMap.TryGetValue(langId, out string code))
                    {
                        cachedLanguageCode = code;
                        if (!hasLoggedLanguage)
                        {
                            MelonLogger.Msg($"[ModTextTranslator] Detected language: {cachedLanguageCode}");
                            hasLoggedLanguage = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!hasLoggedLanguage)
                    MelonLogger.Msg($"[ModTextTranslator] DetectLanguage exception: {ex.Message}");
            }
            return cachedLanguageCode;
        }

        /// <summary>
        /// Loads mod_text.json from embedded resources.
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;

            translations = new Dictionary<string, Dictionary<string, string>>();

            try
            {
                using var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("mod_text.json");

                if (stream != null)
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    string json = reader.ReadToEnd();

                    translations = ParseNestedJson(json);
                    MelonLogger.Msg($"[ModTextTranslator] Loaded {translations.Count} mod text entries");
                }
                else
                {
                    MelonLogger.Warning("[ModTextTranslator] Embedded mod_text.json not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ModTextTranslator] Error loading mod text: {ex.Message}");
            }

            isInitialized = true;
        }

        /// <summary>
        /// Returns the localized string for the given English key.
        /// Falls back to English, then to the key itself if no translation exists.
        /// </summary>
        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            if (!isInitialized)
                Initialize();

            if (translations == null || translations.Count == 0)
                return key;

            if (!translations.TryGetValue(key, out var langDict))
                return key;

            string lang = DetectLanguage();

            if (langDict.TryGetValue(lang, out string localized) && !string.IsNullOrEmpty(localized))
                return localized;

            // Fall back to English
            if (lang != "en" && langDict.TryGetValue("en", out string english) && !string.IsNullOrEmpty(english))
                return english;

            return key;
        }

        // ─────────────────────────────────────────────
        //  JSON parsing for nested { key: { lang: value } } format
        // ─────────────────────────────────────────────

        internal static Dictionary<string, Dictionary<string, string>> ParseNestedJson(string json)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            if (string.IsNullOrEmpty(json)) return result;

            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                return result;

            string inner = json.Substring(1, json.Length - 2);

            int pos = 0;
            while (pos < inner.Length)
            {
                int keyStart = inner.IndexOf('"', pos);
                if (keyStart < 0) break;
                int keyEnd = FindClosingQuote(inner, keyStart + 1);
                if (keyEnd < 0) break;

                string mapName = UnescapeJsonString(inner.Substring(keyStart + 1, keyEnd - keyStart - 1));

                int braceStart = inner.IndexOf('{', keyEnd);
                if (braceStart < 0) break;

                int braceEnd = FindMatchingBrace(inner, braceStart);
                if (braceEnd < 0) break;

                string mapJson = inner.Substring(braceStart + 1, braceEnd - braceStart - 1);
                result[mapName] = ParseStringDictionary(mapJson);

                pos = braceEnd + 1;
            }

            return result;
        }

        private static Dictionary<string, string> ParseStringDictionary(string json)
        {
            var dict = new Dictionary<string, string>();
            int pos = 0;
            while (pos < json.Length)
            {
                int keyStart = json.IndexOf('"', pos);
                if (keyStart < 0) break;
                int keyEnd = FindClosingQuote(json, keyStart + 1);
                if (keyEnd < 0) break;

                string key = UnescapeJsonString(json.Substring(keyStart + 1, keyEnd - keyStart - 1));

                int colonIdx = json.IndexOf(':', keyEnd);
                if (colonIdx < 0) break;

                int valStart = json.IndexOf('"', colonIdx);
                if (valStart < 0) break;
                int valEnd = FindClosingQuote(json, valStart + 1);
                if (valEnd < 0) break;

                string value = UnescapeJsonString(json.Substring(valStart + 1, valEnd - valStart - 1));
                dict[key] = value;

                pos = valEnd + 1;
            }
            return dict;
        }

        private static int FindClosingQuote(string s, int startAfterOpenQuote)
        {
            for (int i = startAfterOpenQuote; i < s.Length; i++)
            {
                if (s[i] == '\\') { i++; continue; }
                if (s[i] == '"') return i;
            }
            return -1;
        }

        private static int FindMatchingBrace(string s, int openBracePos)
        {
            int depth = 1;
            bool inString = false;
            for (int i = openBracePos + 1; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && inString) { i++; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;
                if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static string UnescapeJsonString(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case 'n': sb.Append('\n'); i++; break;
                        case 'r': sb.Append('\r'); i++; break;
                        case 't': sb.Append('\t'); i++; break;
                        case '/': sb.Append('/'); i++; break;
                        default: sb.Append(s[i]); break;
                    }
                }
                else
                {
                    sb.Append(s[i]);
                }
            }
            return sb.ToString();
        }
    }
}
