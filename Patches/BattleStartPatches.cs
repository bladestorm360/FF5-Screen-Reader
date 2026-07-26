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

            // Explicit interface implementation. IL2CPP metadata spells it
            // "Last.Map.IEventAccessor.EventEncountBoss", but Il2CppInterop rewrites the dots as
            // underscores — the real member is Last_Map_IEventAccessor_EventEncountBoss.
            // Confirmed by scanning the generated Assembly-CSharp.dll; the dotted spelling does
            // not resolve, which the startup warning caught in testing.
            PatchEncounter(harmony,
                new[]
                {
                    "Last_Map_IEventAccessor_EventEncountBoss",
                    "Last.Map.IEventAccessor.EventEncountBoss",
                    "EventEncountBoss",
                },
                "boss encounters");
        }

        private static void PatchEncounter(HarmonyLib.Harmony harmony, string[] candidates, string what)
        {
            try
            {
                var type = typeof(Il2CppLast.Map.EventProcedure);

                var postfix = typeof(BattleStartPatches).GetMethod(
                    nameof(Encounter_Postfix), BindingFlags.Public | BindingFlags.Static);

                foreach (var name in candidates)
                {
                    var target = AccessTools.Method(type, name);
                    if (target == null) continue;

                    harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                    MelonLogger.Msg($"[BattleStart] Hooked {name} for {what}");
                    return;
                }

                // Last resort: match on the bare method name regardless of how the interface
                // qualifier was mangled, so a future Il2CppInterop naming change degrades to a
                // scan rather than to silence.
                string bare = candidates[candidates.Length - 1];
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                                                  | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (!m.Name.EndsWith(bare, System.StringComparison.Ordinal)) continue;

                    harmony.Patch(m, postfix: new HarmonyMethod(postfix));
                    MelonLogger.Msg($"[BattleStart] Hooked {m.Name} for {what} (matched by scan)");
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
