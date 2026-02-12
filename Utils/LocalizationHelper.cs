using System.Collections.Generic;
using Il2CppLast.Management;
using MelonLoader;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Localization helper: wraps MessageManager for game strings,
    /// and provides a small embedded dictionary for mod-specific framing text.
    /// </summary>
    public static class LocalizationHelper
    {
        // Mod-specific strings keyed by language code → string key → translation
        private static readonly Dictionary<string, Dictionary<string, string>> ModStrings = new()
        {
            ["ja"] = new() {
                ["earned"] = "\u7372\u5F97",          // 獲得
                ["to"] = "\u2192",                     // →
                ["received"] = "\u5165\u624B",         // 入手
                ["learned"] = "\u4E60\u5F97",          // 习得
                ["level"] = "Lv",
                ["default_name_hint"] = "Enter\u30AD\u30FC\u3067\u30C7\u30D5\u30A9\u30EB\u30C8\u540D\u3092\u4F7F\u7528", // Enterキーでデフォルト名を使用
            },
            ["en"] = new() {
                ["earned"] = "earned",
                ["to"] = "to",
                ["received"] = "Received",
                ["learned"] = "Learned",
                ["level"] = "Lv",
                ["default_name_hint"] = "Press Enter for default name",
            },
            ["fr"] = new() {
                ["earned"] = "obtenu",
                ["to"] = "\u2192",
                ["received"] = "Re\u00E7u",           // Reçu
                ["learned"] = "Appris",
                ["level"] = "Nv",
                ["default_name_hint"] = "Appuyez sur Entr\u00E9e pour le nom par d\u00E9faut", // Appuyez sur Entrée pour le nom par défaut
            },
            ["it"] = new() {
                ["earned"] = "ottenuto",
                ["to"] = "\u2192",
                ["received"] = "Ricevuto",
                ["learned"] = "Appreso",
                ["level"] = "Lv",
                ["default_name_hint"] = "Premi Invio per il nome predefinito",
            },
            ["de"] = new() {
                ["earned"] = "erhalten",
                ["to"] = "\u2192",
                ["received"] = "Erhalten",
                ["learned"] = "Erlernt",
                ["level"] = "Lv",
                ["default_name_hint"] = "Eingabetaste f\u00FCr Standardnamen dr\u00FCcken", // Eingabetaste für Standardnamen drücken
            },
            ["es"] = new() {
                ["earned"] = "obtenido",
                ["to"] = "\u2192",
                ["received"] = "Recibido",
                ["learned"] = "Aprendido",
                ["level"] = "Nv",
                ["default_name_hint"] = "Pulsa Enter para el nombre predeterminado",
            },
            ["ko"] = new() {
                ["earned"] = "\uD68D\uB4DD",          // 획득
                ["to"] = "\u2192",
                ["received"] = "\uC785\uC218",         // 입수
                ["learned"] = "\uC2B5\uB4DD",          // 습득
                ["level"] = "Lv",
                ["default_name_hint"] = "Enter \uD0A4\uB85C \uAE30\uBCF8 \uC774\uB984 \uC0AC\uC6A9", // Enter 키로 기본 이름 사용
            },
            ["zht"] = new() {
                ["earned"] = "\u7372\u5F97",           // 獲得
                ["to"] = "\u2192",
                ["received"] = "\u5165\u624B",         // 入手
                ["learned"] = "\u7FD2\u5F97",          // 習得
                ["level"] = "Lv",
                ["default_name_hint"] = "\u6309Enter\u4F7F\u7528\u9810\u8A2D\u540D\u7A31", // 按Enter使用預設名稱
            },
            ["zhc"] = new() {
                ["earned"] = "\u83B7\u5F97",           // 获得
                ["to"] = "\u2192",
                ["received"] = "\u5165\u624B",         // 入手
                ["learned"] = "\u4E60\u5F97",          // 习得
                ["level"] = "Lv",
                ["default_name_hint"] = "\u6309Enter\u4F7F\u7528\u9ED8\u8BA4\u540D\u79F0", // 按Enter使用默认名称
            },
            ["ru"] = new() {
                ["earned"] = "\u043F\u043E\u043B\u0443\u0447\u0435\u043D\u043E", // получено
                ["to"] = "\u2192",
                ["received"] = "\u041F\u043E\u043B\u0443\u0447\u0435\u043D\u043E", // Получено
                ["learned"] = "\u0418\u0437\u0443\u0447\u0435\u043D\u043E",       // Изучено
                ["level"] = "Lv",
                ["default_name_hint"] = "\u041D\u0430\u0436\u043C\u0438\u0442\u0435 Enter \u0434\u043B\u044F \u0438\u043C\u0435\u043D\u0438 \u043F\u043E \u0443\u043C\u043E\u043B\u0447\u0430\u043D\u0438\u044E", // Нажмите Enter для имени по умолчанию
            },
            ["th"] = new() {
                ["earned"] = "\u0E44\u0E14\u0E49\u0E23\u0E31\u0E1A", // ได้รับ
                ["to"] = "\u2192",
                ["received"] = "\u0E44\u0E14\u0E49\u0E23\u0E31\u0E1A", // ได้รับ
                ["learned"] = "\u0E40\u0E23\u0E35\u0E22\u0E19\u0E23\u0E39\u0E49", // เรียนรู้
                ["level"] = "Lv",
                ["default_name_hint"] = "\u0E01\u0E14 Enter \u0E40\u0E1E\u0E37\u0E48\u0E2D\u0E43\u0E0A\u0E49\u0E0A\u0E37\u0E48\u0E2D\u0E40\u0E23\u0E34\u0E48\u0E21\u0E15\u0E49\u0E19", // กด Enter เพื่อใช้ชื่อเริ่มต้น
            },
            ["pt"] = new() {
                ["earned"] = "obtido",
                ["to"] = "\u2192",
                ["received"] = "Recebido",
                ["learned"] = "Aprendido",
                ["level"] = "Nv",
                ["default_name_hint"] = "Pressione Enter para o nome padr\u00E3o", // Pressione Enter para o nome padrão
            },
        };

        /// <summary>
        /// Wraps MessageManager.GetMessage() with null safety.
        /// </summary>
        public static string GetGameMessage(string mesId)
        {
            if (string.IsNullOrEmpty(mesId)) return null;
            try
            {
                var mm = MessageManager.Instance;
                if (mm == null) return null;
                string msg = mm.GetMessage(mesId);
                return string.IsNullOrEmpty(msg) ? null : msg;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a mod-specific localized string. Falls back to English if not found.
        /// </summary>
        public static string GetModString(string key)
        {
            string lang = GetCurrentLanguageCode();
            if (ModStrings.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var value))
                return value;
            if (ModStrings.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enValue))
                return enValue;
            return key;
        }

        /// <summary>
        /// Detects the current game language and returns a lowercase code.
        /// </summary>
        private static string GetCurrentLanguageCode()
        {
            try
            {
                var mm = MessageManager.Instance;
                if (mm == null) return "en";

                // MessageManager.currentLanguage is a Language enum
                // Language enum is 1-indexed: Ja=1, En=2, Fr=3, It=4, De=5, Es=6, Ko=7, Zht=8, Zhc=9, Ru=10, Th=11, Pt=12
                int langId = (int)mm.currentLanguage;
                string code = langId switch
                {
                    1 => "ja",
                    2 => "en",
                    3 => "fr",
                    4 => "it",
                    5 => "de",
                    6 => "es",
                    7 => "ko",
                    8 => "zht",
                    9 => "zhc",
                    10 => "ru",
                    11 => "th",
                    12 => "pt",
                    _ => "en",
                };
                return code;
            }
            catch
            {
                return "en";
            }
        }
    }
}
