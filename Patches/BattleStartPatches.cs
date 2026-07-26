using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Flips battle state the moment an encounter commits, rather than when the first command
    /// window appears.
    ///
    /// Before this, BattleState.SetActive() had exactly one caller —
    /// BattleCommandSelectController.SetCommandData — so "we are in battle" meant "the first
    /// character's command window was populated". That is after the encounter effect, after the
    /// battle scene loads and after the ATB fills, and every downstream gate turns on with it:
    /// AudioLoopManager.IsAudioSuppressed, ControllerRouter.IsFieldActive, KeyContext.Battle and
    /// the cursor suppression. The wall-tone loop was never the problem — it re-checks every
    /// 0.1s and silences on the first tick after the flag flips.
    ///
    /// Last.Map.EventProcedure.EventEncount is the single funnel where random encounters (via
    /// FieldController.ExcuteEncount) and scripted ones (the Encount script opcode) both
    /// converge, and it runs before the encounter SE and the screen effect. Bosses take the
    /// separate EventEncountBoss path, so both are hooked.
    ///
    /// Both are shared=1 in script.json, i.e. real bodies rather than folded empty stubs.
    /// A scene-load backstop lives in GameStatePatches.ChangeState_Postfix for Colosseum and AR
    /// battles, which never route through EventProcedure.
    /// </summary>
    public static class BattleStartPatches
    {
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            PatchEncounter(harmony,
                new[] { "EventEncount" },
                "random and scripted encounters");

            // Explicit interface implementation — IL2CPP metadata names it
            // "Last.Map.IEventAccessor.EventEncountBoss", but Il2CppInterop may expose it
            // unqualified, so try both rather than assuming.
            PatchEncounter(harmony,
                new[] { "Last.Map.IEventAccessor.EventEncountBoss", "EventEncountBoss" },
                "boss encounters");
        }

        private static void PatchEncounter(HarmonyLib.Harmony harmony, string[] candidates, string what)
        {
            try
            {
                var type = typeof(Il2CppLast.Map.EventProcedure);

                foreach (var name in candidates)
                {
                    var target = AccessTools.Method(type, name);
                    if (target == null) continue;

                    var postfix = typeof(BattleStartPatches).GetMethod(
                        nameof(Encounter_Postfix), BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                    MelonLogger.Msg($"[BattleStart] Hooked {name} for {what}");
                    return;
                }

                // Never fail silently — an unregistered patch that nobody notices is exactly how
                // the quicksave completion popup stayed broken for five months.
                MelonLogger.Warning($"[BattleStart] Could not resolve a hook for {what} "
                    + $"(tried: {string.Join(", ", candidates)}). Field audio will keep playing "
                    + "through the transition until the battle scene loads.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BattleStart] Failed to patch {what}: {ex.Message}");
            }
        }

        public static void Encounter_Postfix()
        {
            try
            {
                // Idempotent: SetActive() returns immediately when already in battle, so this
                // firing alongside the scene-load backstop costs nothing.
                BattleState.SetActive();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BattleStart] Error entering battle state: {ex.Message}");
            }
        }
    }
}
