using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Field;
using static FFV_ScreenReader.Utils.ModTextTranslator;
using Il2Cpp;
using Il2CppLast.Map;
using Il2CppLast.Management;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Translates Japanese entity names to the current game language using an embedded translation resource.
    /// Uses a 4-tier lookup: exact → strip prefix → strip suffix → strip both, with language fallback to English.
    /// Detects language via MessageManager.Instance.currentLanguage.
    /// </summary>
    public static class EntityTranslator
    {
        private static Dictionary<string, Dictionary<string, string>> translations;
        private static bool isInitialized = false;
        private static string cachedLanguageCode = "en";
        private static bool hasLoggedLanguage = false;
        private static HashSet<string> loggedMisses = new HashSet<string>();

        private static readonly Dictionary<int, string> LanguageCodeMap = new()
        {
            {1,"ja"},{2,"en"},{3,"fr"},{4,"it"},{5,"de"},{6,"es"},
            {7,"ko"},{8,"zht"},{9,"zhc"},{10,"ru"},{11,"th"},{12,"pt"}
        };

        // Matches numeric prefix (e.g., "6:") or SC prefix (e.g., "SC01:") at start of entity names
        private static readonly Regex EntityPrefixRegex = new Regex(
            @"^((?:SC)?\d+:)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches circled number suffix at END (① through ⑳)
        private static readonly Regex EntitySuffixRegex = new Regex(
            @"([①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳])$",
            RegexOptions.Compiled);

        private static readonly Dictionary<char, string> CircledNumberMap = new Dictionary<char, string>
        {
            {'①', "1"}, {'②', "2"}, {'③', "3"}, {'④', "4"}, {'⑤', "5"},
            {'⑥', "6"}, {'⑦', "7"}, {'⑧', "8"}, {'⑨', "9"}, {'⑩', "10"},
            {'⑪', "11"}, {'⑫', "12"}, {'⑬', "13"}, {'⑭', "14"}, {'⑮', "15"},
            {'⑯', "16"}, {'⑰', "17"}, {'⑱', "18"}, {'⑲', "19"}, {'⑳', "20"}
        };

        /// <summary>
        /// Detects the current game language via MessageManager and returns a language code.
        /// Caches the result; defaults to "en" if MessageManager is unavailable.
        /// </summary>
        public static string DetectLanguage()
        {
            try
            {
                var mgr = MessageManager.Instance;
                if (mgr != null)
                {
                    int langId = (int)mgr.currentLanguage;
                    if (!hasLoggedLanguage)
                        MelonLogger.Msg($"[EntityTranslator] Raw langId from MessageManager: {langId}");

                    if (LanguageCodeMap.TryGetValue(langId, out string code))
                    {
                        cachedLanguageCode = code;
                        if (!hasLoggedLanguage)
                        {
                            MelonLogger.Msg($"[EntityTranslator] Detected language: {cachedLanguageCode}");
                            hasLoggedLanguage = true;
                        }
                    }
                    else if (!hasLoggedLanguage)
                    {
                        MelonLogger.Msg($"[EntityTranslator] langId {langId} not in map, keeping default: {cachedLanguageCode}");
                    }
                }
                else if (!hasLoggedLanguage)
                {
                    MelonLogger.Msg("[EntityTranslator] MessageManager.Instance is null, using default: " + cachedLanguageCode);
                }
            }
            catch (Exception ex)
            {
                if (!hasLoggedLanguage)
                    MelonLogger.Msg($"[EntityTranslator] DetectLanguage exception: {ex.Message}, using default: {cachedLanguageCode}");
            }
            return cachedLanguageCode;
        }

        private static string GetEntityNamesFilePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string userDataDir = Path.Combine(baseDir, "UserData");
            if (!Directory.Exists(userDataDir))
                Directory.CreateDirectory(userDataDir);
            return Path.Combine(userDataDir, "EntityNames.json");
        }

        /// <summary>
        /// Loads the embedded translation resource into the multi-language lookup dictionary.
        /// Format: { "japaneseName": { "en": "English", "fr": "French", ... }, ... }
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;

            translations = new Dictionary<string, Dictionary<string, string>>();

            try
            {
                using var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("translation.json");

                if (stream != null)
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    string json = reader.ReadToEnd();
                    MelonLogger.Msg($"[EntityTranslator] Embedded resource length: {json.Length} chars");

                    var data = ParseNestedJson(json);
                    MelonLogger.Msg($"[EntityTranslator] Parsed {data.Count} raw entries from embedded resource");

                    foreach (var entry in data)
                    {
                        // Only include entries where at least one language value is non-empty
                        bool hasValue = false;
                        foreach (var langEntry in entry.Value)
                        {
                            if (!string.IsNullOrEmpty(langEntry.Value))
                            {
                                hasValue = true;
                                break;
                            }
                        }
                        if (hasValue)
                            translations[entry.Key] = entry.Value;
                    }

                    MelonLogger.Msg($"[EntityTranslator] Loaded {translations.Count} translations from embedded resource");
                }
                else
                {
                    MelonLogger.Warning("[EntityTranslator] Embedded translation resource not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[EntityTranslator] Error loading translations: {ex.Message}");
            }

            isInitialized = true;
        }

        /// <summary>
        /// Translates a Japanese entity name to the current game language.
        /// Returns original name if no translation found.
        /// 4-tier lookup: exact → strip prefix → strip suffix → strip both.
        /// </summary>
        public static string Translate(string japaneseName)
        {
            if (string.IsNullOrEmpty(japaneseName))
                return japaneseName;

            if (!isInitialized)
                Initialize();

            if (translations.Count == 0)
                return japaneseName;

            // When game is in Japanese, entity names are already Japanese — no translation needed
            if (DetectLanguage() == "ja")
                return japaneseName;

            // 1. Exact match
            if (TryLookup(japaneseName, out string exactMatch))
                return exactMatch;

            // 2. Strip numeric/SC prefix and try base name lookup
            StripPrefix(japaneseName, out string prefix, out string baseName);
            if (prefix != null && TryLookup(baseName, out string baseTranslation))
                return prefix + " " + baseTranslation;

            // 3. Strip circled number suffix and try base name lookup
            StripSuffix(japaneseName, out string suffix, out string baseNameNoSuffix);
            if (suffix != null && TryLookup(baseNameNoSuffix, out string baseSuffixTranslation))
                return baseSuffixTranslation + " " + ConvertCircledNumber(suffix);

            // 4. Handle both prefix AND suffix
            if (prefix != null)
            {
                StripSuffix(baseName, out string innerSuffix, out string innerBase);
                if (innerSuffix != null && TryLookup(innerBase, out string innerTranslation))
                    return prefix + " " + innerTranslation + " " + ConvertCircledNumber(innerSuffix);
            }

            // Log untranslated entities containing Japanese characters (once per unique name)
            if (ContainsJapaneseCharacters(japaneseName) && loggedMisses.Add(japaneseName))
                MelonLogger.Msg($"[EntityTranslator] MISS: \"{japaneseName}\"");

            return japaneseName;
        }

        /// <summary>
        /// Looks up a Japanese key in the translations dictionary for the current game language.
        /// Returns false if no translation found, so the caller can fall back to the original Japanese.
        /// </summary>
        private static bool TryLookup(string key, out string result)
        {
            result = null;
            if (!translations.TryGetValue(key, out var langDict))
                return false;
            string lang = DetectLanguage();
            if (langDict.TryGetValue(lang, out string localized) && !string.IsNullOrEmpty(localized))
            {
                result = localized;
                return true;
            }
            // Fallback to English if detected language wasn't found
            if (lang != "en" && langDict.TryGetValue("en", out string english) && !string.IsNullOrEmpty(english))
            {
                result = english;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if a string contains Japanese characters (Hiragana, Katakana, or CJK Unified Ideographs).
        /// </summary>
        public static bool ContainsJapaneseCharacters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if ((c >= '\u3040' && c <= '\u309F') ||  // Hiragana
                    (c >= '\u30A0' && c <= '\u30FF') ||  // Katakana
                    (c >= '\u4E00' && c <= '\u9FFF'))     // CJK Unified Ideographs
                    return true;
            }
            return false;
        }

        private static void StripPrefix(string name, out string prefix, out string baseName)
        {
            Match match = EntityPrefixRegex.Match(name);
            if (match.Success)
            {
                prefix = match.Groups[1].Value;
                baseName = name.Substring(prefix.Length);
            }
            else
            {
                prefix = null;
                baseName = name;
            }
        }

        private static void StripSuffix(string name, out string suffix, out string baseName)
        {
            Match match = EntitySuffixRegex.Match(name);
            if (match.Success)
            {
                suffix = match.Groups[1].Value;
                baseName = name.Substring(0, name.Length - suffix.Length);
            }
            else
            {
                suffix = null;
                baseName = name;
            }
        }

        private static string ConvertCircledNumber(string circled)
        {
            if (circled.Length == 1 && CircledNumberMap.TryGetValue(circled[0], out string num))
                return num;
            return circled;
        }

        /// <summary>
        /// Gets the count of loaded translations.
        /// </summary>
        public static int TranslationCount => translations?.Count ?? 0;

        // ─────────────────────────────────────────────
        //  JSON parsing — used for embedded translation data
        //  Format: { "outerKey": { "innerKey": "value", ... }, ... }
        // ─────────────────────────────────────────────

        /// <summary>
        /// Parses a two-level nested JSON dictionary: outerKey → (innerKey → value).
        /// Used for the embedded translation resource (jpName → {lang → translation}).
        /// </summary>
        internal static Dictionary<string, Dictionary<string, string>> ParseNestedJson(string json)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            if (string.IsNullOrEmpty(json)) return result;

            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                return result;

            // Strip outer braces
            string inner = json.Substring(1, json.Length - 2);

            int pos = 0;
            while (pos < inner.Length)
            {
                // Find next quoted key
                int keyStart = inner.IndexOf('"', pos);
                if (keyStart < 0) break;
                int keyEnd = FindClosingQuote(inner, keyStart + 1);
                if (keyEnd < 0) break;

                string outerKey = UnescapeJsonString(inner.Substring(keyStart + 1, keyEnd - keyStart - 1));

                // Find the opening brace for this key's entries
                int braceStart = inner.IndexOf('{', keyEnd);
                if (braceStart < 0) break;

                int braceEnd = FindMatchingBrace(inner, braceStart);
                if (braceEnd < 0) break;

                string innerJson = inner.Substring(braceStart + 1, braceEnd - braceStart - 1);
                result[outerKey] = ParseStringDictionary(innerJson);

                pos = braceEnd + 1;
            }

            return result;
        }

        /// <summary>
        /// Parses a flat JSON object of string→string pairs.
        /// </summary>
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

                // Find colon
                int colonIdx = json.IndexOf(':', keyEnd);
                if (colonIdx < 0) break;

                // Find value (quoted string)
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

        /// <summary>
        /// Finds the closing quote, handling escaped quotes.
        /// </summary>
        private static int FindClosingQuote(string s, int startAfterOpenQuote)
        {
            for (int i = startAfterOpenQuote; i < s.Length; i++)
            {
                if (s[i] == '\\') { i++; continue; } // skip escaped char
                if (s[i] == '"') return i;
            }
            return -1;
        }

        /// <summary>
        /// Finds the matching closing brace for an opening brace.
        /// </summary>
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

        // ─────────────────────────────────────────────
        //  EntityDump — dumps Japanese entity names for the current map
        // ─────────────────────────────────────────────

        /// <summary>
        /// Nested static class for dumping entity names to EntityNames.json.
        /// Triggered by pressing key 0 on the field.
        /// </summary>
        public static class EntityDump
        {
            /// <summary>
            /// Dumps Japanese entity names for the current map into EntityNames.json.
            /// Merges with existing data, only adding truly new entries.
            /// </summary>
            public static void DumpCurrentMap()
            {
                try
                {
                    string filePath = GetEntityNamesFilePath();

                    // 1. Load existing data
                    var existingData = new Dictionary<string, Dictionary<string, string>>();
                    if (File.Exists(filePath))
                    {
                        string json = File.ReadAllText(filePath, Encoding.UTF8);
                        existingData = ParseNestedJson(json);
                    }

                    // 2. Get current map name
                    string mapKey = MapNameResolver.GetCurrentMapName();
                    if (string.IsNullOrEmpty(mapKey))
                    {
                        FFV_ScreenReaderMod.SpeakText(T("Cannot determine current map"));
                        return;
                    }

                    // 3. Collect dumpable entities
                    var newEntities = CollectDumpableEntities();
                    if (newEntities.Count == 0)
                    {
                        FFV_ScreenReaderMod.SpeakText(string.Format(T("No Japanese entities found on {0}"), mapKey));
                        return;
                    }

                    // 4. Duplicate check
                    if (existingData.ContainsKey(mapKey))
                    {
                        var existingNames = new HashSet<string>(existingData[mapKey].Keys);
                        var trulyNew = new HashSet<string>();
                        foreach (string name in newEntities)
                        {
                            if (!existingNames.Contains(name))
                                trulyNew.Add(name);
                        }

                        if (trulyNew.Count == 0)
                        {
                            FFV_ScreenReaderMod.SpeakText(string.Format(T("No new entities for {0}"), mapKey));
                            return;
                        }

                        // Add new entries
                        foreach (string name in trulyNew)
                            existingData[mapKey][name] = "";

                        SaveDumpFile(filePath, existingData);
                        FFV_ScreenReaderMod.SpeakText(string.Format(T("{0} new entities for {1}"), trulyNew.Count, mapKey));
                    }
                    else
                    {
                        // New map entry
                        var mapEntries = new Dictionary<string, string>();
                        foreach (string name in newEntities)
                            mapEntries[name] = "";
                        existingData[mapKey] = mapEntries;

                        SaveDumpFile(filePath, existingData);
                        FFV_ScreenReaderMod.SpeakText(string.Format(T("Dumped {0} entities for {1}"), newEntities.Count, mapKey));
                    }

                    MelonLogger.Msg($"[EntityDump] Saved {newEntities.Count} entity names for {mapKey}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[EntityDump] Error: {ex.Message}");
                    FFV_ScreenReaderMod.SpeakText(T("Error dumping entities"));
                }
            }

            /// <summary>
            /// Collects Japanese entity names from the current map, excluding
            /// TreasureBox, SavePoint, GotoMap, and non-interactive types.
            /// </summary>
            private static HashSet<string> CollectDumpableEntities()
            {
                var names = new HashSet<string>();
                var fieldEntities = FieldNavigationHelper.GetAllFieldEntities();

                foreach (var fieldEntity in fieldEntities)
                {
                    if (fieldEntity?.Property == null) continue;

                    var objectType = (MapConstants.ObjectType)fieldEntity.Property.ObjectType;

                    // Exclude: TreasureBox, SavePoint, GotoMap
                    if (objectType == MapConstants.ObjectType.TreasureBox ||
                        objectType == MapConstants.ObjectType.SavePoint ||
                        objectType == MapConstants.ObjectType.GotoMap)
                        continue;

                    // Exclude non-interactive types
                    if (IsNonInteractiveType(objectType))
                        continue;

                    string name = fieldEntity.Property.Name;
                    if (string.IsNullOrEmpty(name)) continue;

                    // Only include entities with Japanese characters
                    if (!ContainsJapaneseCharacters(name))
                        continue;

                    names.Add(name);
                }

                return names;
            }

            /// <summary>
            /// Mirrors EntityFactory.IsNonInteractiveType for dump filtering.
            /// </summary>
            private static bool IsNonInteractiveType(MapConstants.ObjectType objectType)
            {
                return objectType == MapConstants.ObjectType.PointIn ||
                       objectType == MapConstants.ObjectType.OpenTrigger ||
                       objectType == MapConstants.ObjectType.CollisionEntity ||
                       objectType == MapConstants.ObjectType.EffectEntity ||
                       objectType == MapConstants.ObjectType.ScreenEffect ||
                       objectType == MapConstants.ObjectType.TileAnimation ||
                       objectType == MapConstants.ObjectType.MoveArea ||
                       objectType == MapConstants.ObjectType.Polyline ||
                       objectType == MapConstants.ObjectType.ChangeOffset ||
                       objectType == MapConstants.ObjectType.IgnoreRoute ||
                       objectType == MapConstants.ObjectType.NonEncountArea ||
                       objectType == MapConstants.ObjectType.MapRange ||
                       objectType == MapConstants.ObjectType.DamageFloorGimmickArea ||
                       objectType == MapConstants.ObjectType.SlidingFloorGimmickArea ||
                       objectType == MapConstants.ObjectType.TimeSwitchingGimmickArea;
            }

            /// <summary>
            /// Saves the entity names data to JSON file with UTF-8 encoding.
            /// </summary>
            private static void SaveDumpFile(string filePath, Dictionary<string, Dictionary<string, string>> data)
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");

                var mapKeys = new List<string>(data.Keys);
                for (int m = 0; m < mapKeys.Count; m++)
                {
                    string mapKey = mapKeys[m];
                    var entries = data[mapKey];

                    sb.AppendLine($"  \"{EscapeJsonString(mapKey)}\": {{");

                    var entityKeys = new List<string>(entries.Keys);
                    for (int e = 0; e < entityKeys.Count; e++)
                    {
                        string entityKey = entityKeys[e];
                        string value = entries[entityKey];
                        string comma = e < entityKeys.Count - 1 ? "," : "";
                        sb.AppendLine($"    \"{EscapeJsonString(entityKey)}\": \"{EscapeJsonString(value)}\"{comma}");
                    }

                    string mapComma = m < mapKeys.Count - 1 ? "," : "";
                    sb.AppendLine($"  }}{mapComma}");
                }

                sb.AppendLine("}");

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }

            private static string EscapeJsonString(string str)
            {
                if (string.IsNullOrEmpty(str)) return str;
                return str
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
            }
        }
    }
}
