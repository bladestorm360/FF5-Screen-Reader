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
                ["before"] = "\u524D",                 // 前
                ["after"] = "\u5F8C",                  // 後
                ["change"] = "\u5909\u5316",           // 変化
                ["next_level"] = "\u6B21",             // 次
                ["battle_results"] = "\u6226\u95D8\u7D50\u679C", // 戦闘結果
                ["no_data"] = "\u30C7\u30FC\u30BF\u306A\u3057", // データなし
            },
            ["en"] = new() {
                ["earned"] = "earned",
                ["to"] = "to",
                ["received"] = "Received",
                ["learned"] = "Learned",
                ["level"] = "Lv",
                ["default_name_hint"] = "Press Enter for default name",
                ["before"] = "Before",
                ["after"] = "After",
                ["change"] = "Change",
                ["next_level"] = "Next",
                ["battle_results"] = "Battle Results",
                ["no_data"] = "No data available",
            },
            ["fr"] = new() {
                ["earned"] = "obtenu",
                ["to"] = "\u2192",
                ["received"] = "Re\u00E7u",           // Reçu
                ["learned"] = "Appris",
                ["level"] = "Nv",
                ["default_name_hint"] = "Appuyez sur Entr\u00E9e pour le nom par d\u00E9faut", // Appuyez sur Entrée pour le nom par défaut
                ["before"] = "Avant",
                ["after"] = "Apr\u00E8s",              // Après
                ["change"] = "Diff",
                ["next_level"] = "Suivant",
                ["battle_results"] = "R\u00E9sultats de combat", // Résultats de combat
                ["no_data"] = "Aucune donn\u00E9e",   // Aucune donnée
            },
            ["it"] = new() {
                ["earned"] = "ottenuto",
                ["to"] = "\u2192",
                ["received"] = "Ricevuto",
                ["learned"] = "Appreso",
                ["level"] = "Lv",
                ["default_name_hint"] = "Premi Invio per il nome predefinito",
                ["before"] = "Prima",
                ["after"] = "Dopo",
                ["change"] = "Diff",
                ["next_level"] = "Prossimo",
                ["battle_results"] = "Risultati battaglia",
                ["no_data"] = "Nessun dato",
            },
            ["de"] = new() {
                ["earned"] = "erhalten",
                ["to"] = "\u2192",
                ["received"] = "Erhalten",
                ["learned"] = "Erlernt",
                ["level"] = "Lv",
                ["default_name_hint"] = "Eingabetaste f\u00FCr Standardnamen dr\u00FCcken", // Eingabetaste für Standardnamen drücken
                ["before"] = "Vorher",
                ["after"] = "Nachher",
                ["change"] = "Diff",
                ["next_level"] = "N\u00E4chstes",     // Nächstes
                ["battle_results"] = "Kampfergebnisse",
                ["no_data"] = "Keine Daten",
            },
            ["es"] = new() {
                ["earned"] = "obtenido",
                ["to"] = "\u2192",
                ["received"] = "Recibido",
                ["learned"] = "Aprendido",
                ["level"] = "Nv",
                ["default_name_hint"] = "Pulsa Enter para el nombre predeterminado",
                ["before"] = "Antes",
                ["after"] = "Despu\u00E9s",           // Después
                ["change"] = "Cambio",
                ["next_level"] = "Siguiente",
                ["battle_results"] = "Resultados de batalla",
                ["no_data"] = "Sin datos",
            },
            ["ko"] = new() {
                ["earned"] = "\uD68D\uB4DD",          // 획득
                ["to"] = "\u2192",
                ["received"] = "\uC785\uC218",         // 입수
                ["learned"] = "\uC2B5\uB4DD",          // 습득
                ["level"] = "Lv",
                ["default_name_hint"] = "Enter \uD0A4\uB85C \uAE30\uBCF8 \uC774\uB984 \uC0AC\uC6A9", // Enter 키로 기본 이름 사용
                ["before"] = "\uC774\uC804",           // 이전
                ["after"] = "\uC774\uD6C4",            // 이후
                ["change"] = "\uBCC0\uD654",           // 변화
                ["next_level"] = "\uB2E4\uC74C",       // 다음
                ["battle_results"] = "\uC804\uD22C \uACB0\uACFC", // 전투 결과
                ["no_data"] = "\uB370\uC774\uD130 \uC5C6\uC74C", // 데이터 없음
            },
            ["zht"] = new() {
                ["earned"] = "\u7372\u5F97",           // 獲得
                ["to"] = "\u2192",
                ["received"] = "\u5165\u624B",         // 入手
                ["learned"] = "\u7FD2\u5F97",          // 習得
                ["level"] = "Lv",
                ["default_name_hint"] = "\u6309Enter\u4F7F\u7528\u9810\u8A2D\u540D\u7A31", // 按Enter使用預設名稱
                ["before"] = "\u4E4B\u524D",           // 之前
                ["after"] = "\u4E4B\u5F8C",            // 之後
                ["change"] = "\u8B8A\u5316",           // 變化
                ["next_level"] = "\u4E0B\u4E00",       // 下一
                ["battle_results"] = "\u6230\u9B25\u7D50\u679C", // 戰鬥結果
                ["no_data"] = "\u7121\u8CC7\u6599",    // 無資料
            },
            ["zhc"] = new() {
                ["earned"] = "\u83B7\u5F97",           // 获得
                ["to"] = "\u2192",
                ["received"] = "\u5165\u624B",         // 入手
                ["learned"] = "\u4E60\u5F97",          // 习得
                ["level"] = "Lv",
                ["default_name_hint"] = "\u6309Enter\u4F7F\u7528\u9ED8\u8BA4\u540D\u79F0", // 按Enter使用默认名称
                ["before"] = "\u4E4B\u524D",           // 之前
                ["after"] = "\u4E4B\u540E",            // 之后
                ["change"] = "\u53D8\u5316",           // 变化
                ["next_level"] = "\u4E0B\u4E00",       // 下一
                ["battle_results"] = "\u6218\u6597\u7ED3\u679C", // 战斗结果
                ["no_data"] = "\u65E0\u6570\u636E",    // 无数据
            },
            ["ru"] = new() {
                ["earned"] = "\u043F\u043E\u043B\u0443\u0447\u0435\u043D\u043E", // получено
                ["to"] = "\u2192",
                ["received"] = "\u041F\u043E\u043B\u0443\u0447\u0435\u043D\u043E", // Получено
                ["learned"] = "\u0418\u0437\u0443\u0447\u0435\u043D\u043E",       // Изучено
                ["level"] = "Lv",
                ["default_name_hint"] = "\u041D\u0430\u0436\u043C\u0438\u0442\u0435 Enter \u0434\u043B\u044F \u0438\u043C\u0435\u043D\u0438 \u043F\u043E \u0443\u043C\u043E\u043B\u0447\u0430\u043D\u0438\u044E", // Нажмите Enter для имени по умолчанию
                ["before"] = "\u0414\u043E",           // До
                ["after"] = "\u041F\u043E\u0441\u043B\u0435", // После
                ["change"] = "\u0418\u0437\u043C.",    // Изм.
                ["next_level"] = "\u0421\u043B\u0435\u0434.", // След.
                ["battle_results"] = "\u0420\u0435\u0437\u0443\u043B\u044C\u0442\u0430\u0442\u044B \u0431\u043E\u044F", // Результаты боя
                ["no_data"] = "\u041D\u0435\u0442 \u0434\u0430\u043D\u043D\u044B\u0445", // Нет данных
            },
            ["th"] = new() {
                ["earned"] = "\u0E44\u0E14\u0E49\u0E23\u0E31\u0E1A", // ได้รับ
                ["to"] = "\u2192",
                ["received"] = "\u0E44\u0E14\u0E49\u0E23\u0E31\u0E1A", // ได้รับ
                ["learned"] = "\u0E40\u0E23\u0E35\u0E22\u0E19\u0E23\u0E39\u0E49", // เรียนรู้
                ["level"] = "Lv",
                ["default_name_hint"] = "\u0E01\u0E14 Enter \u0E40\u0E1E\u0E37\u0E48\u0E2D\u0E43\u0E0A\u0E49\u0E0A\u0E37\u0E48\u0E2D\u0E40\u0E23\u0E34\u0E48\u0E21\u0E15\u0E49\u0E19", // กด Enter เพื่อใช้ชื่อเริ่มต้น
                ["before"] = "\u0E01\u0E48\u0E2D\u0E19", // ก่อน
                ["after"] = "\u0E2B\u0E25\u0E31\u0E07",   // หลัง
                ["change"] = "\u0E40\u0E1B\u0E25\u0E35\u0E48\u0E22\u0E19", // เปลี่ยน
                ["next_level"] = "\u0E16\u0E31\u0E14\u0E44\u0E1B", // ถัดไป
                ["battle_results"] = "\u0E1C\u0E25\u0E01\u0E32\u0E23\u0E15\u0E48\u0E2D\u0E2A\u0E39\u0E49", // ผลการต่อสู้
                ["no_data"] = "\u0E44\u0E21\u0E48\u0E21\u0E35\u0E02\u0E49\u0E2D\u0E21\u0E39\u0E25", // ไม่มีข้อมูล
            },
            ["pt"] = new() {
                ["earned"] = "obtido",
                ["to"] = "\u2192",
                ["received"] = "Recebido",
                ["learned"] = "Aprendido",
                ["level"] = "Nv",
                ["default_name_hint"] = "Pressione Enter para o nome padr\u00E3o", // Pressione Enter para o nome padrão
                ["before"] = "Antes",
                ["after"] = "Depois",
                ["change"] = "Mudan\u00E7a",          // Mudança
                ["next_level"] = "Pr\u00F3ximo",       // Próximo
                ["battle_results"] = "Resultados da batalha",
                ["no_data"] = "Sem dados",
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
