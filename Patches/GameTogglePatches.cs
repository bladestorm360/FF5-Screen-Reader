using System;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using Il2CppLast.Management;
using static FFV_ScreenReader.Utils.ModTextTranslator;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Narrates the game's own walk/run (F1) and encounter (F3) field toggles whatever the input
    /// source — keyboard, a stick click passed through to the game, anything that drives the
    /// field toggle — by hooking the game's setting setters instead of watching keys.
    ///
    /// Direct-call xrefs in GameAssembly.dll:
    ///   CheatSettingsClient.SetIsEnableEncount ← FieldMap.UpdatePlayerStatePlay (the field toggle),
    ///     ConfigActualDetailsControllerBase.SetEnableEncount (config menu),
    ///     SaveSlotManager.GotoLoadSaveData (loading a save).
    ///   ConfigClient.SetIsAutoDash ← FieldMap.UpdatePlayerStatePlay, plus the config menu's
    ///     SetIsAutoDash and SwitchArrowSelectTypeProcess.
    /// Only the field toggle should speak — the config menu announces its own row and a load is not
    /// a toggle — so these speak only on the field with no menu open, and only on a real change.
    ///
    /// Prefixes, so the old value is still readable: "real change" is exact with no seeding.
    /// Do NOT hook CheatSettingsData.set_IsEnableEncount instead: its body is folded with 22 other
    /// setters (RVA 0x346D70), so a detour there would fire for all of them.
    /// </summary>
    public static class GameTogglePatches
    {
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            Patch(harmony, typeof(CheatSettingsClient), "SetIsEnableEncount", nameof(SetIsEnableEncount_Prefix));
            Patch(harmony, typeof(ConfigClient), "SetIsAutoDash", nameof(SetIsAutoDash_Prefix));
        }

        private static void Patch(HarmonyLib.Harmony harmony, Type type, string method, string prefixName)
        {
            try
            {
                var target = AccessTools.Method(type, method);
                if (target == null)
                {
                    MelonLogger.Warning($"[GameToggle] {type.Name}.{method} not found — that toggle will be silent");
                    return;
                }
                harmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(GameTogglePatches), prefixName)));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameToggle] Failed to patch {type.Name}.{method}: {ex.Message}");
            }
        }

        /// <summary>The field toggle, as opposed to the config menu or a save load.</summary>
        private static bool IsFieldToggle() => InputManager.IsOnValidMap() && !MenuStateRegistry.AnyActive();

        // __0 = isEnable, the value about to be written.
        public static void SetIsEnableEncount_Prefix(bool __0)
        {
            try
            {
                if (!IsFieldToggle()) return;
                var cheat = UserDataManager.Instance()?.CheatSettingsData;
                if (cheat == null || cheat.IsEnableEncount == __0) return;
                FFV_ScreenReaderMod.SpeakText(__0 ? T("Encounters on") : T("Encounters off"), interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameToggle] Error announcing encounter toggle: {ex.Message}");
            }
        }

        // __0 = the new auto-dash value (0 = walk by default, non-zero = run by default).
        public static void SetIsAutoDash_Prefix(int __0)
        {
            try
            {
                if (!IsFieldToggle()) return;
                var config = UserDataManager.Instance()?.Config;
                if (config == null || (config.IsAutoDash != 0) == (__0 != 0)) return;
                FFV_ScreenReaderMod.SpeakText(__0 != 0 ? T("Run") : T("Walk"), interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameToggle] Error announcing walk/run toggle: {ex.Message}");
            }
        }
    }
}
