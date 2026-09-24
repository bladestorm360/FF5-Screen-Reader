using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Message;
using Il2CppLast.Management;
using Il2CppLast.UI;
using Il2CppLast.UI.Touch;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.UI.Message;
using Il2CppLast.Battle;
using Il2CppLast.Battle.Function;
using Il2CppLast.Data.Master;
using Il2CppLast.Systems;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;
using UnityEngine;
using BattleCommandMessageController_KeyInput = Il2CppLast.UI.KeyInput.BattleCommandMessageController;
using BattleCommandMessageController_Touch = Il2CppLast.UI.Touch.BattleCommandMessageController;
using BattlePlayerData = Il2Cpp.BattlePlayerData;
using Il2CppLast.Data.User;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Patches for message display methods - View layer and scrolling messages in Battle
    /// </summary>

    [HarmonyPatch(typeof(ScrollMessageManager), nameof(ScrollMessageManager.Play))]
    public static class ScrollMessageManager_Play_Patch
    {
        // ScrollMessageManager.Play re-fires with the same text while a scroll message is on
        // screen, so hold the last one spoken. Cleared each turn by SetCommandSelectTarget so a
        // repeated system message ("Preemptive Strike" two battles running) still announces.
        private static string _lastScrollMessage;

        /// <summary>Clears the last scroll message so it can be announced again.</summary>
        public static void ResetLastMessage() => _lastScrollMessage = null;

        [HarmonyPostfix]
        public static void Postfix(ScrollMessageClient.ScrollType type, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message)) return;

                string cleanMessage = message.Trim();
                if (cleanMessage == _lastScrollMessage) return;
                _lastScrollMessage = cleanMessage;

                FFV_ScreenReaderMod.SpeakText(cleanMessage, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in ScrollMessageManager.Play patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch ParameterActFunctionManagment.CreateActFunction to announce actor names with their actions
    /// This is called when a battle action function is being created (before execution)
    /// </summary>
    [HarmonyPatch(typeof(ParameterActFunctionManagment), nameof(ParameterActFunctionManagment.CreateActFunction))]
    public static class ParameterActFunctionManagment_CreateActFunction_Patch
    {
        // Two guards, different jobs:
        //  _lastActDataPtr  — CreateActFunction is invoked more than once for the SAME BattleActData
        //                     (per-target / per-hit function creation). Keyed on the INSTANCE, not the
        //                     text, so two goblins both attacking produce distinct pointers and both
        //                     announce (a string compare would swallow the second).
        //  _lastSpokenActorName — ally dual-wield: a second, different BattleActData for the same ally
        //                     swinging again. Only suppressed for direct attacks.
        private static IntPtr _lastActDataPtr = IntPtr.Zero;
        private static string _lastSpokenActorName;

        // Actions are classified by the command's identity, never its localized name (matching
        // "attack"/"defend"/"item" text only ever worked in English). Ids from the game:
        // CommandSortData.CommandId Fight = 1, Item = 3; Command.CommandType Defence = 8 is the
        // Defend/Flee menu, and BattleConstants.EscapeCommandId = 22 is Flee.
        private const int FIGHT_COMMAND_ID = 1;
        private const int ITEM_COMMAND_ID = 3;
        private const int ESCAPE_COMMAND_ID = 22;
        private const int DEFENCE_COMMAND_TYPE = 8;
        // The ability the Fight command executes (command master row 1, ability_id = 1).
        private const int PLAIN_ATTACK_ABILITY_ID = 1;

        private static int FirstAbilityId(BattleActData battleActData)
        {
            try
            {
                var abilityList = battleActData.abilityList;
                if (abilityList != null && abilityList.Count > 0 && abilityList[0] != null)
                    return abilityList[0].Id;
            }
            catch { }
            return 0;
        }

        /// <summary>Clears both guards so a repeated action announces fresh next turn.</summary>
        public static void ResetLastAction()
        {
            _lastActDataPtr = IntPtr.Zero;
            _lastSpokenActorName = null;
        }

        [HarmonyPostfix]
        public static void Postfix(BattleActData battleActData)
        {
            try
            {
                if (battleActData == null) return;

                // Get the attacker's name
                string actorName = GetActorName(battleActData);

                // Get the action/ability name
                string actionName = GetActionName(battleActData, out bool isCommandName);

                if (!string.IsNullOrEmpty(actorName))
                {
                    if (battleActData.Pointer == _lastActDataPtr) return;
                    _lastActDataPtr = battleActData.Pointer;

                    var command = battleActData.Command;
                    int commandId = command != null ? command.Id : 0;
                    // A plain attack is the Fight command's own ability, or the Fight command with no
                    // ability name at all. Keyed on the ability id rather than the command alone, so an
                    // ability that runs under the Fight command still keeps its name.
                    bool isDirectAttack = string.IsNullOrEmpty(actionName)
                        || FirstAbilityId(battleActData) == PLAIN_ATTACK_ABILITY_ID
                        || (isCommandName && commandId == FIGHT_COMMAND_ID);
                    bool isDefend = command != null && command.CommandType == DEFENCE_COMMAND_TYPE
                        && commandId != ESCAPE_COMMAND_ID;

                    string announcement;
                    if (isDirectAttack)
                        announcement = string.Format(T("{0} attacks"), actorName);
                    else if (isDefend)
                        announcement = string.Format(T("{0} defends"), actorName);
                    else if (isCommandName && commandId == ITEM_COMMAND_ID)
                        announcement = string.Format(T("{0} uses item"), actorName);
                    else
                        announcement = $"{actorName}, {actionName}";

                    // Ally dual-wield suppression: same ally name + direct attack = redundant second swing
                    bool isAlly = battleActData.AttackUnitData?.TryCast<Il2Cpp.BattlePlayerData>() != null;

                    if (isAlly && isDirectAttack && actorName == _lastSpokenActorName)
                        return;

                    _lastSpokenActorName = actorName;

                    FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in CreateActFunction patch: {ex.Message}");
            }
        }

        private static string GetActorName(BattleActData battleActData)
        {
            try
            {
                var attackUnit = battleActData.AttackUnitData;
                if (attackUnit == null) return null;

                return BattleUnitHelper.GetUnitName(attackUnit);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting actor name: {ex.Message}");
            }

            return null;
        }

        /// <param name="isCommandName">True when the name is the command's own (no named ability),
        /// so "uses item" never replaces the name of the item actually used.</param>
        private static string GetActionName(BattleActData battleActData, out bool isCommandName)
        {
            isCommandName = false;
            try
            {
                // Try to get the ability name first (spells, skills)
                var abilityList = battleActData.abilityList;
                if (abilityList != null && abilityList.Count > 0)
                {
                    var ability = abilityList[0];
                    if (ability != null)
                    {
                        // Use ContentUtitlity to get the localized ability name directly
                        string abilityName = ContentUtitlity.GetAbilityName(ability);
                        if (!string.IsNullOrEmpty(abilityName))
                        {
                            return abilityName;
                        }
                    }
                }

                // Fall back to command name (Attack, Defend, etc.)
                var command = battleActData.Command;
                if (command != null)
                {
                    var messageManager = MessageManager.Instance;
                    if (messageManager != null)
                    {
                        string commandMesId = command.MesIdName;
                        if (!string.IsNullOrEmpty(commandMesId))
                        {
                            string localizedName = messageManager.GetMessage(commandMesId);
                            if (!string.IsNullOrEmpty(localizedName))
                            {
                                isCommandName = true;
                                return localizedName;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting action name: {ex.Message}");
            }

            return null;
        }
    }

    // Damage is always read as the total. FF5 has no multi-hit "×N" reading: the game draws its ×N
    // (DamageViewUIManager.CreateHitCount) only in Command battles, and FF5 is ATB, and its calc
    // results never carry a hit count either (CalcControllerProvider.GetFightStatus 0x3C9D40 is a
    // stub; the real path, GetUniqueStatus 0x3CA940, passes 0). See docs/debug.md.

    [HarmonyPatch(typeof(Il2CppLast.Battle.Function.BattleBasicFunction), nameof(Il2CppLast.Battle.Function.BattleBasicFunction.CreateDamageView))]
    public static class BattleBasicFunction_CreateDamageView_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.Battle.BattleUnitData data, int value, Il2CppLast.Systems.HitType hitType, bool isRecovery, Il2CppLast.Systems.CalcResult.MissType missType)
        {
            try
            {
                string targetName = BattleUnitHelper.GetUnitName(data) ?? T("Unknown");

                // Speak only the views the game actually draws. A postfix runs even when the original
                // returned early, and BattleBasicFunction.CreateDamageView (0x889870) creates NO view for:
                //  - value 0 with HitType Hit or Non (nothing appears on screen, yet the mod used to
                //    say "X: 0 damage" — a 2026-07-26 log has "Faris: 0 damage" right before
                //    "Faris: Paralyze" from Entangle);
                //  - RecoveryCondition unless SystemConfigData.GetSerialType() == 5 and value > 0 —
                //    FF5's GetSerialType (0x2B2720) returns 5, so it is drawn only with a value;
                //  - MissType.NonView, whatever the HitType (Steal, Focus and similar).
                // HitType.Zero with value 0 IS drawn (a "0" over the target), so it still reads
                // "X: 0 damage" below — the same thing a sighted player sees.
                if (value == 0 && (hitType == Il2CppLast.Systems.HitType.Hit || hitType == Il2CppLast.Systems.HitType.Non))
                    return;
                if (hitType == Il2CppLast.Systems.HitType.RecoveryCondition && value <= 0)
                    return;
                if (missType == Il2CppLast.Systems.CalcResult.MissType.NonView)
                    return;

                string message;
                if (hitType == Il2CppLast.Systems.HitType.Miss)
                {
                    message = string.Format(T("{0}: Miss"), targetName);
                }
                else if (hitType == Il2CppLast.Systems.HitType.MPRecovery
                    || (hitType == Il2CppLast.Systems.HitType.MpAbs && isRecovery))
                {
                    message = string.Format(T("{0}: Recovered {1} MP"), targetName, value);
                }
                else if (hitType == Il2CppLast.Systems.HitType.MPHit || hitType == Il2CppLast.Systems.HitType.MpAbs)
                {
                    // MP damage (Osmose, Rasp) and the losing side of an MP drain
                    message = string.Format(T("{0}: {1} MP damage"), targetName, value);
                }
                else if (hitType == Il2CppLast.Systems.HitType.Recovery
                    || hitType == Il2CppLast.Systems.HitType.RecoveryCondition
                    || isRecovery)
                {
                    // HP recovery, including the gaining side of an HP drain (HpAbs) — the game's
                    // own isRecovery flag says which side of the drain this view is. The game forces
                    // RecoveryCondition views (drawn only with a value > 0, see above) to draw as
                    // recovery whatever isRecovery says, so they read as recovery too.
                    message = string.Format(T("{0}: Recovered {1} HP"), targetName, value);
                }
                else
                {
                    // HP damage, always the total (see the note above this patch).
                    message = string.Format(T("{0}: {1} damage"), targetName, value);
                }

                // Announce damage/recovery
                FFV_ScreenReaderMod.SpeakText(message, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleBasicFunction.CreateDamageView patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch SetCommandSelectTarget to reset target tracking when a new character's turn begins.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.UI.KeyInput.BattleMenuController), nameof(Il2CppLast.UI.KeyInput.BattleMenuController.SetCommandSelectTarget))]
    public static class BattleMenuController_SetCommandSelectTarget_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(BattlePlayerData targetData)
        {
            try
            {
                // New turn: clear every "already said this" guard so the same message,
                // action or status can be announced again this turn.
                BattleTargetPatches.ResetState();
                ScrollMessageManager_Play_Patch.ResetLastMessage();
                ParameterActFunctionManagment_CreateActFunction_Patch.ResetLastAction();
                BattleConditionController_Add_Patch.ResetLastCondition();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SetCommandSelectTarget patch: {ex.Message}");
            }
        }
    }

    // Actor + action announcements come from ParameterActFunctionManagment.CreateActFunction,
    // system messages ("Preemptive Strike") from ScrollMessageManager.Play. Each patch owns its
    // own re-fire guard, cleared per turn by SetCommandSelectTarget above and per battle by
    // BattleState.SetActive.

    /// <summary>
    /// Patch BattleStealItemPlug.StealItem to announce when items are stolen
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.Battle.BattleStealItemPlug), nameof(Il2CppLast.Battle.BattleStealItemPlug.StealItem))]
    public static class BattleStealItemPlug_StealItem_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int contentId, int cnt)
        {
            try
            {
                if (contentId <= 0) return;

                // Get item name from contentId using MessageManager directly
                var messageManager = MessageManager.Instance;
                if (messageManager == null)
                {
                    MelonLogger.Warning("[Steal] MessageManager not available");
                    return;
                }

                // Get the item name message ID using ContentUtitlity
                string itemMesId = ContentUtitlity.GetMesIdItemName(contentId);
                if (!string.IsNullOrEmpty(itemMesId))
                {
                    string itemName = messageManager.GetMessage(itemMesId);
                    if (!string.IsNullOrEmpty(itemName))
                    {
                        // Remove icon markup from name
                        itemName = Utils.TextUtils.StripIconMarkup(itemName);

                        string announcement;
                        if (cnt > 1)
                        {
                            announcement = string.Format(T("Stole {0} x{1}"), itemName, cnt);
                        }
                        else
                        {
                            announcement = string.Format(T("Stole {0}"), itemName);
                        }

                        FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
                        return;
                    }
                }

                // Fallback if we couldn't get the item name
                FFV_ScreenReaderMod.SpeakText(T("Stole item"), interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleStealItemPlug.StealItem patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Patch BattleConditionController.Add to announce status effects when applied: KO (UnableFight),
    /// Silence, Sleep, Haste, Protect and every other condition the game names. Add creates the
    /// condition's BattleConditionFunction; RemoveFunction (patched below) is its mirror. FF5's
    /// condition table gives Poison, Blind, Stone, Toad, Mini and Float no name (mes_id_name
    /// "None"); GetConditionName gives them the localized status name by type instead.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.Battle.BattleConditionController), nameof(Il2CppLast.Battle.BattleConditionController.Add))]
    public static class BattleConditionController_Add_Patch
    {
        // Add() is invoked once per target of a multi-target status spell AND re-invoked while a
        // persistent condition (poison, sleep) stays applied, so remember what has already been
        // spoken this turn. Cleared each turn by SetCommandSelectTarget so re-applying the same
        // status to the same unit on a later turn still announces.
        //
        // Keyed on (unit INSTANCE, condition id), never on the rendered text: two Devil Crabs
        // both produce "Devil Crab: KO", so a string compare swallowed the second enemy's death
        // entirely. Distinct pointers means both now announce. Same reasoning as _lastActDataPtr
        // above. A set rather than a single slot so interleaved units can't evict each other and
        // let a persistent condition re-announce.
        private static readonly HashSet<(IntPtr, int)> _announcedConditions = new HashSet<(IntPtr, int)>();

        /// <summary>Clears announced conditions so they can be announced again next turn.</summary>
        public static void ResetLastCondition() => _announcedConditions.Clear();

        /// <summary>
        /// Forgets one (unit, condition) pair once its removal has been announced, so the status
        /// being applied again in the same turn is announced again.
        /// </summary>
        internal static void Forget(IntPtr unit, int id) => _announcedConditions.Remove((unit, id));

        // ConditionType values never given a fallback name here, even though the target reader
        // lists them: Dying (4, "Critical") is an HP threshold rather than a status, and the
        // nameless KO row (id 75, only in condition group 900 beside the named KO row 5) would
        // otherwise read "X: KO" a second time.
        private const int CONDITION_TYPE_DYING = 4;
        private const int CONDITION_TYPE_UNABLE_FIGHT = 5;

        /// <summary>
        /// The localized name of a condition master row, shared by the add and removal
        /// announcements so both use the same wording. The game's own name (MesIdName →
        /// MessageManager) when it has one. FF5's condition table gives Poison, Blind, Stone, Toad,
        /// Mini, Float and one Doom row none ("None"), so those fall back to the localized status
        /// name by ConditionType (CharacterStatusHelper, the game's own words through T()). Null
        /// when there is no name at all (internal states such as Defend or Jump); an empty string
        /// when the message lookup fails.
        /// </summary>
        internal static string GetConditionName(Il2CppLast.Data.Master.Condition condition)
        {
            if (condition == null) return null;

            string mesId = condition.MesIdName;
            if (string.IsNullOrEmpty(mesId) || mesId == "None")
            {
                int type = condition.ConditionType;
                if (type == CONDITION_TYPE_DYING || type == CONDITION_TYPE_UNABLE_FIGHT)
                    return null;
                return CharacterStatusHelper.GetConditionTypeName(type);
            }
            return TextUtils.StripIconMarkup(MessageManager.Instance?.GetMessage(mesId) ?? "");
        }

        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.Battle.BattleUnitData battleUnitData, int id)
        {
            try
            {
                if (battleUnitData == null)
                {
                    return;
                }

                // Get target name using BattleUnitHelper
                string targetName = FFV_ScreenReader.Utils.BattleUnitHelper.GetUnitName(battleUnitData) ?? T("Unknown");

                // Get condition name from ID - look up from ConfirmedConditionList
                string conditionName = null;
                try
                {
                    var unitDataInfo = battleUnitData.BattleUnitDataInfo;
                    if (unitDataInfo?.Parameter != null)
                    {
                        var confirmedList = unitDataInfo.Parameter.ConfirmedConditionList();
                        if (confirmedList != null && confirmedList.Count > 0)
                        {
                            // Look for condition matching our ID
                            foreach (var condition in confirmedList)
                            {
                                if (condition != null && condition.Id == id)
                                {
                                    string localizedConditionName = GetConditionName(condition);

                                    // Skip conditions with no message ID (internal/hidden statuses)
                                    if (localizedConditionName == null)
                                        return;

                                    if (localizedConditionName.Length > 0)
                                        conditionName = localizedConditionName;
                                    break;
                                }
                            }
                        }
                    }

                    // Fallback: Announce raw ID if we couldn't resolve the name
                    if (conditionName == null)
                    {
                        conditionName = string.Format(T("Status {0}"), id);
                        MelonLogger.Warning($"[Status] Could not resolve condition ID {id}, announcing as raw ID");
                    }
                }
                catch (Exception condEx)
                {
                    MelonLogger.Warning($"Error resolving condition ID {id}: {condEx.Message}");
                    conditionName = string.Format(T("Status {0}"), id);
                }

                // Add() returns false when the pair is already present — a genuine re-fire for
                // this same unit and condition, so stay silent.
                if (!_announcedConditions.Add((battleUnitData.Pointer, id))) return;

                FFV_ScreenReaderMod.SpeakText($"{targetName}: {conditionName}", interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleConditionController.Add patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announces a status leaving a unit, "{0}: {1} removed": cures, natural wear-off, revive (KO).
    ///
    /// Hooked on BattleConditionController.RemoveFunction (0x333260), not Remove (0x333410). In FF5,
    /// Remove has three callers only (the two InterruptRemoveCondition overloads and
    /// BattleEndRecoveryCondition), so cures and wear-off never reach it. Every other path takes the
    /// condition out of Parameter.CurrentConditionList directly: the action's result, NaturalRemove
    /// (timed wear-off), Recovery(unit, untilType), Cancellation (a conflicting status). The sync
    /// CheckConditionFunction → RemoveConditionFunction then drops the condition's
    /// BattleConditionFunction through RemoveFunction, and Remove itself tail-calls RemoveFunction.
    /// RemoveFunction is therefore the one place every removal passes, and it is the mirror of Add,
    /// which creates that function and which the add announcement hooks.
    ///
    /// Silent for:
    ///  - no function for this id: nothing is removed, and Add never ran, so nothing was announced;
    ///  - conditions without a name, own or fallback (GetConditionName, shared with the add line);
    ///  - a unit that is KO or Stone: death clearing its other statuses (Cancellation on KO);
    ///  - the battle-end cleanup and anything after victory, defeat or escape starts;
    ///  - a stack that is still present (a second Image, for example) — only the last one speaks;
    ///  - the same (unit, condition) twice in one frame.
    /// FF5's Remove never passes isNegate = true (all three call sites pass false), so there is no
    /// negation path to filter.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.Battle.BattleConditionController), nameof(Il2CppLast.Battle.BattleConditionController.RemoveFunction))]
    public static class BattleConditionController_RemoveFunction_Patch
    {
        // ConditionType values (Last.Defaine.ConditionType): UnableFight = KO, Mineralization = Stone.
        private const int CONDITION_TYPE_KO = 5;
        private const int CONDITION_TYPE_STONE = 11;

        // Set when the battle ends (BattleController.StateChange into a win/lose/escape state, or
        // BattleEndRecoveryCondition); cleared when the next battle starts.
        private static bool _battleOver;

        private static int _frame = -1;
        private static readonly HashSet<(IntPtr, int)> _spokenThisFrame = new HashSet<(IntPtr, int)>();

        internal static void SetBattleOver(bool over)
        {
            _battleOver = over;
            if (!over) _spokenThisFrame.Clear();
        }

        /// <summary>The unit's condition object for this id, taken from its live function list.</summary>
        private static Il2CppLast.Data.Master.Condition FindFunctionCondition(Il2CppLast.Battle.BattleUnitDataInfo info, int id)
        {
            var functions = info?.BattleConditionFunction;
            if (functions == null) return null;
            for (int i = 0; i < functions.Count; i++)
            {
                var condition = functions[i]?.condition;
                if (condition != null && condition.Id == id)
                    return condition;
            }
            return null;
        }

        /// <summary>True when the unit's current condition list holds this id.</summary>
        private static bool HasCondition(Il2CppLast.Battle.BattleUnitDataInfo info, int id)
        {
            var list = info?.Parameter?.CurrentConditionList;
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                var condition = list[i];
                if (condition != null && condition.Id == id)
                    return true;
            }
            return false;
        }

        /// <summary>True when the unit is KO or Stone for a reason other than the condition being removed.</summary>
        private static bool IsDown(Il2CppLast.Battle.BattleUnitDataInfo info, int removedId)
        {
            var list = info?.Parameter?.CurrentConditionList;
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                var condition = list[i];
                if (condition == null || condition.Id == removedId) continue;
                int type = condition.ConditionType;
                if (type == CONDITION_TYPE_KO || type == CONDITION_TYPE_STONE)
                    return true;
            }
            return false;
        }

        [HarmonyPrefix]
        public static void Prefix(Il2CppLast.Battle.BattleUnitData battleUnitData, int id, out string __state)
        {
            __state = null;
            try
            {
                if (_battleOver || battleUnitData == null) return;

                var info = battleUnitData.BattleUnitDataInfo;
                var condition = FindFunctionCondition(info, id);
                if (condition == null) return;

                string name = BattleConditionController_Add_Patch.GetConditionName(condition);
                if (string.IsNullOrEmpty(name)) return;

                if (IsDown(info, id)) return;

                __state = name;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleConditionController.RemoveFunction prefix: {ex.Message}");
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.Battle.BattleUnitData battleUnitData, int id, string __state)
        {
            if (__state == null) return;
            try
            {
                var info = battleUnitData.BattleUnitDataInfo;
                if (HasCondition(info, id) || FindFunctionCondition(info, id) != null) return;

                int frame = Time.frameCount;
                if (frame != _frame)
                {
                    _frame = frame;
                    _spokenThisFrame.Clear();
                }
                if (!_spokenThisFrame.Add((battleUnitData.Pointer, id))) return;

                BattleConditionController_Add_Patch.Forget(battleUnitData.Pointer, id);

                string unitName = BattleUnitHelper.GetUnitName(battleUnitData) ?? T("Unknown");
                FFV_ScreenReaderMod.SpeakText(string.Format(T("{0}: {1} removed"), unitName, __state), interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in BattleConditionController.RemoveFunction postfix: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Battle-end cleanup: BattleController.SaveRecoveryCondition → BattleEndRecoveryCondition
    /// (0x32F890, its only caller) strips the party's battle-only statuses through Remove →
    /// RemoveFunction. SaveRecoveryCondition runs from StartWinResult, EndEscapeFadeOut,
    /// StartForcedOnSave and the scripted-end lambda &lt;StartBattle&gt;b__29_0; the last two call it
    /// before any StateChange, so the latch is set here too.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.Battle.BattleConditionController), nameof(Il2CppLast.Battle.BattleConditionController.BattleEndRecoveryCondition))]
    public static class BattleConditionController_BattleEndRecoveryCondition_Patch
    {
        [HarmonyPrefix]
        public static void Prefix() => BattleConditionController_RemoveFunction_Patch.SetBattleOver(true);
    }

    /// <summary>
    /// BattleController.StateChange (0x341570), the battle's own state transition: Init (1) through
    /// Event (6) are a live battle, WinWait (7) through End (20) are victory, defeat, escape and
    /// their fades. Every battle starts with StateChange(Init) from StartBattle, which clears the
    /// latch. A prefix, so the latch is set before the new state's own start-up code runs.
    /// Transitions only — never per frame.
    /// </summary>
    [HarmonyPatch(typeof(Il2CppLast.Battle.BattleController), nameof(Il2CppLast.Battle.BattleController.StateChange))]
    public static class BattleController_StateChange_Patch
    {
        private const int STATE_INIT = 1;
        private const int STATE_EVENT = 6;
        private const int STATE_WIN_WAIT = 7;
        private const int STATE_END = 20;

        [HarmonyPrefix]
        public static void Prefix(Il2CppLast.Battle.BattleController.BattleState __0)
        {
            int state = (int)__0;
            if (state >= STATE_INIT && state <= STATE_EVENT)
                BattleConditionController_RemoveFunction_Patch.SetBattleOver(false);
            else if (state >= STATE_WIN_WAIT && state <= STATE_END)
                BattleConditionController_RemoveFunction_Patch.SetBattleOver(true);
        }
    }

    /// <summary>
    /// Announces defeat messages via BattleCommandMessageController.
    /// Uses manual Harmony patching since the types are in non-standard IL2CPP namespaces.
    /// </summary>
    public static class BattleCommandMessagePatches
    {
        private static string lastBattleCommandMessage = "";

        /// <summary>
        /// Applies manual Harmony patches for BattleCommandMessageController methods.
        /// </summary>
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            try
            {
                PatchBattleCommandMessage(harmony);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Battle Command Message] Error applying patches: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds a type by name across all loaded assemblies.
        /// </summary>
        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.FullName == fullName)
                        {
                            return type;
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// Patch BattleCommandMessageController.SetMessage for system messages like "The party was defeated".
        /// </summary>
        private static void PatchBattleCommandMessage(HarmonyLib.Harmony harmony)
        {
            try
            {
                // KeyInput version - uses SetMessage
                var keyInputType = FindType("Il2CppLast.UI.KeyInput.BattleCommandMessageController");
                if (keyInputType != null)
                {
                    var setMessageMethod = AccessTools.Method(keyInputType, "SetMessage");
                    if (setMessageMethod != null)
                    {
                        var postfix = typeof(BattleCommandMessagePatches).GetMethod(
                            nameof(SetMessage_Postfix), BindingFlags.Public | BindingFlags.Static);
                        harmony.Patch(setMessageMethod, postfix: new HarmonyMethod(postfix));
                    }
                    else
                    {
                        MelonLogger.Warning("[Battle Command Message] KeyInput.BattleCommandMessageController.SetMessage method not found");
                    }
                }
                else
                {
                    MelonLogger.Warning("[Battle Command Message] KeyInput.BattleCommandMessageController type not found");
                }

                // Touch version - uses SetCommandMessage and SetSystemMessage
                var touchType = FindType("Il2CppLast.UI.Touch.BattleCommandMessageController");
                if (touchType != null)
                {
                    // Patch SetCommandMessage
                    var setCommandMsgMethod = AccessTools.Method(touchType, "SetCommandMessage");
                    if (setCommandMsgMethod != null)
                    {
                        var postfix = typeof(BattleCommandMessagePatches).GetMethod(
                            nameof(SetMessage_Postfix), BindingFlags.Public | BindingFlags.Static);
                        harmony.Patch(setCommandMsgMethod, postfix: new HarmonyMethod(postfix));
                    }

                    // Patch SetSystemMessage
                    var setSystemMsgMethod = AccessTools.Method(touchType, "SetSystemMessage");
                    if (setSystemMsgMethod != null)
                    {
                        var postfix = typeof(BattleCommandMessagePatches).GetMethod(
                            nameof(SetMessage_Postfix), BindingFlags.Public | BindingFlags.Static);
                        harmony.Patch(setSystemMsgMethod, postfix: new HarmonyMethod(postfix));
                    }
                }
                else
                {
                    MelonLogger.Warning("[Battle Command Message] Touch.BattleCommandMessageController type not found");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Battle Command Message] Error patching BattleCommandMessageController: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for BattleCommandMessageController.SetMessage/SetCommandMessage/SetSystemMessage.
        /// Announces battle messages including "The party was defeated".
        /// </summary>
        public static void SetMessage_Postfix(object __0)
        {
            try
            {
                // __0 is the message string (using __0 to avoid IL2CPP string param issues)
                string message = __0?.ToString();
                if (string.IsNullOrEmpty(message)) return;

                // Deduplicate
                if (message == lastBattleCommandMessage) return;
                lastBattleCommandMessage = message;

                // Clean up the message
                string cleanMessage = TextUtils.StripIconMarkup(message);
                cleanMessage = cleanMessage.Replace("\n", " ").Replace("\r", " ").Trim();
                while (cleanMessage.Contains("  "))
                    cleanMessage = cleanMessage.Replace("  ", " ");

                if (string.IsNullOrEmpty(cleanMessage)) return;

                // Use interrupt for defeat message so it's heard immediately
                bool isDefeatMessage = cleanMessage.Contains("defeated", StringComparison.OrdinalIgnoreCase);

                FFV_ScreenReaderMod.SpeakText(cleanMessage, interrupt: isDefeatMessage);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Battle Command Message] Error in SetMessage_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset state tracking (call at battle end).
        /// </summary>
        public static void ResetState()
        {
            lastBattleCommandMessage = "";
        }
    }

}
