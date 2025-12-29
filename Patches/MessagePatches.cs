using System;
using HarmonyLib;
using MelonLoader;
using Il2CppLast.Message;
using Il2CppLast.Management;
using Il2CppLast.UI;
using Il2CppLast.UI.Touch;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.UI.Message;
using Il2CppLast.Battle;
using Il2CppLast.Data.Master;
using FFV_ScreenReader.Core;
using UnityEngine;
using Il2CppLast.Map;

namespace FFV_ScreenReader.Patches
{
    // MessageWindowView.Awake patch removed as it was causing patching errors (method not found)

    [HarmonyPatch(typeof(Il2CppLast.Message.MessageWindowView), "SetSpeker")]
    public static class MessageWindowView_SetSpeker_Patch
    {
        private static string lastSpeaker = "";

        [HarmonyPostfix]
        public static void Postfix(string value)
        {
            try
            {
                // MelonLogger.Msg("MessageWindowView.SetSpeker patch executed.");
                if (string.IsNullOrWhiteSpace(value))
                {
                    lastSpeaker = "";
                    return;
                }

                string cleanSpeaker = value.Trim();

                if (cleanSpeaker == lastSpeaker)
                {
                    return;
                }

                lastSpeaker = cleanSpeaker;
                MelonLogger.Msg($"[Speaker] {cleanSpeaker}");
                FFV_ScreenReaderMod.SpeakText(cleanSpeaker, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MessageWindowView.SetSpeker patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Il2CppLast.Message.MessageWindowView), "SetMessage")]
    public static class MessageWindowView_SetMessage_Patch
    {
        private static string lastMessage = "";

        [HarmonyPostfix]
        public static void Postfix(Il2CppLast.Message.MessageWindowView __instance, string message)
        {
            try
            {
                MelonLogger.Msg($"[DEBUG] MessageWindowView.SetMessage called with: '{message}'");
                
                if (string.IsNullOrWhiteSpace(message))
                {
                    if (!string.IsNullOrWhiteSpace(lastMessage))
                    {
                        lastMessage = "";
                    }
                    return;
                }

                string cleanMessage = message.Trim();

                if (cleanMessage == lastMessage)
                {
                    MelonLogger.Msg($"[DEBUG] Message unchanged, skipping");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(lastMessage) && cleanMessage.StartsWith(lastMessage))
                {
                    string newText = cleanMessage.Substring(lastMessage.Length).Trim();
                    if (!string.IsNullOrWhiteSpace(newText))
                    {
                        MelonLogger.Msg($"[MessageWindowView.SetMessage - New] {newText}");
                        FFV_ScreenReaderMod.SpeakText(newText, interrupt: false);
                    }
                }
                else
                {
                    MelonLogger.Msg($"[MessageWindowView.SetMessage - Full] {cleanMessage}");
                    FFV_ScreenReaderMod.SpeakText(cleanMessage, interrupt: false);
                }

                lastMessage = cleanMessage;
                
                // Also try to read from the MessageText component directly
                try
                {
                    if (__instance != null && __instance.MessageText != null)
                    {
                        string textComponentText = __instance.MessageText.text;
                        MelonLogger.Msg($"[DEBUG] MessageText.text property contains: '{textComponentText}'");
                    }
                }
                catch (Exception innerEx)
                {
                    MelonLogger.Warning($"Could not read MessageText component: {innerEx.Message}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MessageWindowView.SetMessage patch: {ex.Message}");
            }
        }
    }

    // FF5 specific: Patch Unity Text component's text property setter
    // This should capture dialogue text being set to UI Text components
    [HarmonyPatch(typeof(UnityEngine.UI.Text), "set_text")]
    public static class UnityText_SetText_Patch
    {
        private static string lastTextValue = "";
        private static float lastTextTime = 0f;

        [HarmonyPostfix]
        public static void Postfix(UnityEngine.UI.Text __instance, string value)
        {
            try
            {
                // Only process non-empty text
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                string cleanText = value.Trim();
                
                // Avoid duplicate announcements (debounce within 0.1 seconds)
                float currentTime = UnityEngine.Time.time;
                if (cleanText == lastTextValue && (currentTime - lastTextTime) < 0.1f)
                {
                    return;
                }

                // Try to identify if this is a message window text component
                bool isMessageText = false;
                string gameObjectName = "";
                try
                {
                    if (__instance != null && __instance.gameObject != null)
                    {
                        gameObjectName = __instance.gameObject.name;
                        
                        // INCLUDE: Message/Dialogue text
                        if (gameObjectName.Contains("Message") && !gameObjectName.Contains("System"))
                        {
                            isMessageText = true;
                        }
                        
                        // EXCLUDE: Menu, UI, Status, and other non-dialogue text
                        if (gameObjectName.Contains("Menu") || 
                            gameObjectName.Contains("Item") || 
                            gameObjectName.Contains("Ability") ||
                            gameObjectName.Contains("Status") ||
                            gameObjectName.Contains("HP") ||
                            gameObjectName.Contains("MP") ||
                            gameObjectName.Contains("Level") ||
                            gameObjectName.Contains("Button") ||
                            gameObjectName.Contains("Icon") ||
                            gameObjectName.Contains("Gauge") ||
                            gameObjectName.Contains("Number") ||
                            gameObjectName.Contains("Command"))
                        {
                            isMessageText = false;
                        }

                        // Robust Battle Exclusion to prevent "Attack" overwriting "Target"
                        // If the text is part of a Battle Menu that we have specialized patches for, ignore it here.
                        if (__instance.GetComponentInParent<Il2CppLast.UI.KeyInput.BattleCommandSelectController>() != null ||
                            __instance.GetComponentInParent<Il2CppLast.UI.KeyInput.BattleItemInfomationController>() != null ||
                            __instance.GetComponentInParent<Il2CppSerial.FF5.UI.KeyInput.BattleQuantityAbilityInfomationController>() != null ||
                            __instance.GetComponentInParent<Il2CppLast.UI.KeyInput.ItemUseController>() != null)
                        {
                            // MelonLogger.Msg($"[DEBUG] Ignoring UnityText update from Battle Controller: {cleanText}");
                            return;
                        }
                    }
                }
                catch { }

                if (isMessageText)
                {
                    MelonLogger.Msg($"[UnityText:{gameObjectName}] {cleanText}");
                    FFV_ScreenReaderMod.SpeakText(cleanText, interrupt: false);
                    lastTextValue = cleanText;
                    lastTextTime = currentTime;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in UnityText.set_text patch: {ex.Message}");
            }
        }
    }



    // FF5 specific: Patching MessageWindowController as a fallback/primary source for dialogue updates
    // DISABLED AGAIN: Still causing crashes even with IntPtr. Will try property setters instead.
    /*
    [HarmonyPatch(typeof(Il2CppLast.Message.MessageWindowController), "SetMessage")]
    public static class MessageWindowController_SetMessage_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(IntPtr message)
        {
            try
            {
                if (message == IntPtr.Zero)
                {
                    return;
                }

                // Manually marshal the string to avoid ArgumentOutOfRangeException
                string managedMessage = Il2CppInterop.Runtime.IL2CPP.Il2CppStringToManaged(message);
                
                if (string.IsNullOrWhiteSpace(managedMessage))
                {
                    return;
                }

                string cleanMessage = managedMessage.Trim();
                // We use interrupt: true here because a new message command usually implies the previous one is done/skipped
                MelonLogger.Msg($"[MessageWindowController] {cleanMessage}");
                FFV_ScreenReaderMod.SpeakText(cleanMessage, interrupt: true); 
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MessageWindowController.SetMessage patch: {ex.Message}");
            }
        }
    }
    */

    // FF5 specific: Patching FadeMessageManager for location names, chapter titles, etc.
    [HarmonyPatch(typeof(Il2CppLast.Message.FadeMessageManager), "Play")]
    public static class FadeMessageManager_Play_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                string cleanMessage = message.Trim();
                MelonLogger.Msg($"[FadeMessageManager] {cleanMessage}");
                FFV_ScreenReaderMod.SpeakText(cleanMessage, interrupt: false); // Usually background text, don't interrupt
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in FadeMessageManager.Play patch: {ex.Message}");
            }
        }
    }

    // FF5 specific: Patching LineFadeMessageManager for scrolling credits, intro text, etc.
    [HarmonyPatch(typeof(Il2CppLast.Message.LineFadeMessageManager), "Play")]
    public static class LineFadeMessageManager_Play_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppSystem.Collections.Generic.List<string> messages)
        {
            try
            {
                if (messages == null || messages.Count == 0)
                {
                    return;
                }

                var sb = new System.Text.StringBuilder();
                // Iterate using for loop because Il2Cpp List enumeration can be tricky
                for (int i = 0; i < messages.Count; i++)
                {
                    string msg = messages[i];
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        sb.AppendLine(msg.Trim());
                    }
                }

                string fullText = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(fullText))
                {
                    MelonLogger.Msg($"[LineFadeMessageManager] {fullText}");
                    FFV_ScreenReaderMod.SpeakText(fullText, interrupt: false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in LineFadeMessageManager.Play patch: {ex.Message}");
            }
        }
    }

    // FF5 specific: Patching MessageWindowManager.SetSpeker for speaker names
    [HarmonyPatch(typeof(Il2CppLast.Message.MessageWindowManager), "SetSpeker")]
    public static class MessageWindowManager_SetSpeker_Patch
    {
        private static string lastSpeaker = "";

        [HarmonyPostfix]
        public static void Postfix(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    lastSpeaker = "";
                    return;
                }

                string cleanSpeaker = name.Trim();
                if (cleanSpeaker == lastSpeaker)
                {
                    return;
                }

                lastSpeaker = cleanSpeaker;
                MelonLogger.Msg($"[MessageWindowManager.Speaker] {cleanSpeaker}");
                FFV_ScreenReaderMod.SpeakText(cleanSpeaker, interrupt: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MessageWindowManager.SetSpeker patch: {ex.Message}");
            }
        }
    }

    // FF5 specific: Patching MessageWindowManager.SetContent for dialogue content
    // BaseContent is in Last.Systems.Message namespace and has ContentText property
    [HarmonyPatch(typeof(Il2CppLast.Message.MessageWindowManager), "SetContent")]
    public static class MessageWindowManager_SetContent_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Il2CppSystem.Collections.Generic.List<Il2CppLast.Systems.Message.BaseContent> contentList)
        {
            try
            {
                if (contentList == null || contentList.Count == 0)
                {
                    MelonLogger.Msg("[MessageWindowManager.Content] Content list is null or empty");
                    return;
                }

                MelonLogger.Msg($"[MessageWindowManager.Content] Content list with {contentList.Count} items set");

                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < contentList.Count; i++)
                {
                    var content = contentList[i];
                    if (content != null && !string.IsNullOrWhiteSpace(content.ContentText))
                    {
                        sb.AppendLine(content.ContentText.Trim());
                    }
                }

                string fullText = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(fullText))
                {
                    MelonLogger.Msg($"[MessageWindowManager.Content - Text] {fullText}");
                    FFV_ScreenReaderMod.SpeakText(fullText, interrupt: false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MessageWindowManager.SetContent patch: {ex.Message}");
            }
        }
    }

    // FF5 specific: Patching SystemMessageWindowManager for system messages
    [HarmonyPatch(typeof(Il2CppLast.Management.SystemMessageWindowManager), "SetMessage")]
    public static class SystemMessageWindowManager_SetMessage_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string messageId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(messageId))
                {
                    return;
                }

                // Try to resolve the message ID to actual text
                var messageManager = Il2CppLast.Management.MessageManager.Instance;
                if (messageManager != null)
                {
                    string message = messageManager.GetMessage(messageId);
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        MelonLogger.Msg($"[SystemMessage] {message}");
                        FFV_ScreenReaderMod.SpeakText(message, interrupt: true);
                    }
                }
                else
                {
                    // Fallback: speak the ID itself
                    MelonLogger.Msg($"[SystemMessage ID] {messageId}");
                    FFV_ScreenReaderMod.SpeakText(messageId, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in SystemMessageWindowManager.SetMessage patch: {ex.Message}");
            }
        }
    }

    // FF5 specific: Patching MessageChoiceWindowManager for choice menus
    [HarmonyPatch(typeof(Il2CppLast.Management.MessageChoiceWindowManager), "Play", new Type[] { typeof(string[]) })]
    public static class MessageChoiceWindowManager_Play_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string[] values)
        {
            try
            {
                if (values == null || values.Length == 0)
                {
                    return;
                }

                var sb = new System.Text.StringBuilder("Choices: ");
                for (int i = 0; i < values.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(values[i]))
                    {
                        sb.Append(values[i].Trim());
                        if (i < values.Length - 1)
                        {
                            sb.Append(", ");
                        }
                    }
                }

                string choicesText = sb.ToString();
                MelonLogger.Msg($"[MessageChoice] {choicesText}");
                FFV_ScreenReaderMod.SpeakText(choicesText, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in MessageChoiceWindowManager.Play patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(EventProcedure), "EventTalk")]
    public static class EventProcedure_EventTalk_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string messageId, Vector3 worldPos, int changeCharacterStatusId)
        {
            try
            {
                MelonLogger.Msg("EventProcedure.EventTalk patch executed.");
                if (string.IsNullOrWhiteSpace(messageId))
                {
                    return;
                }

                var messageManager = Il2CppLast.Management.MessageManager.Instance;
                if (messageManager != null)
                {
                    string message = messageManager.GetMessage(messageId);
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        MelonLogger.Msg($"[EventTalk] {message}");
                        FFV_ScreenReaderMod.SpeakText(message, interrupt: false);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error in EventProcedure.EventTalk patch: {ex.Message}");
            }
        }
    }
}