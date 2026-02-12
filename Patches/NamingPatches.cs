using System;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using MelonLoader;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;

// Type aliases for IL2CPP types
using GameCursor = Il2CppLast.UI.Cursor;
using ChangeNameController = Il2CppLast.UI.KeyInput.ChangeNameController;
using ChangeNameContentController = Il2CppLast.UI.KeyInput.ChangeNameContentController;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Patches for the character naming screen.
    /// Handles character selection (e.g., naming Boko) and keyboard input mode announcements.
    /// </summary>
    public static class NamingPatches
    {
        private const string DEDUP_CONTEXT = AnnouncementContexts.NAMING_SELECT;
        private static bool isPatched = false;

        // Memory offsets from dump.cs (KeyInput versions)
        // ChangeNameController: contentList at 0x30
        private const int CONTENT_LIST_OFFSET = 0x30;

        // ChangeNameContentController: targetData at 0x28
        private const int TARGET_DATA_OFFSET = 0x28;

        /// <summary>
        /// Tracks whether naming menu is currently active.
        /// Delegates to MenuStateRegistry for centralized state management.
        /// </summary>
        public static bool IsNamingMenuActive
        {
            get => MenuStateRegistry.IsActive(MenuStateRegistry.NAMING_MENU);
            private set => MenuStateRegistry.SetActive(MenuStateRegistry.NAMING_MENU, value);
        }

        static NamingPatches()
        {
            MenuStateRegistry.RegisterResetHandler(MenuStateRegistry.NAMING_MENU, () =>
            {
                AnnouncementDeduplicator.Reset(DEDUP_CONTEXT);
            });
        }

        /// <summary>
        /// Clears the naming menu state.
        /// </summary>
        public static void ClearState()
        {
            IsNamingMenuActive = false;
            AnnouncementDeduplicator.Reset(DEDUP_CONTEXT);
        }

        public static bool ShouldSuppress() => IsNamingMenuActive;

        /// <summary>
        /// Applies all naming screen patches using manual Harmony patching.
        /// </summary>
        public static void ApplyPatches(HarmonyLib.Harmony harmony)
        {
            if (isPatched)
                return;

            try
            {
                Type controllerType = typeof(ChangeNameController);

                // Patch SelectContent - called when cursor moves to select a character
                var selectContentMethod = AccessTools.Method(controllerType, "SelectContent");
                if (selectContentMethod != null)
                {
                    var postfix = typeof(NamingPatches).GetMethod(nameof(SelectContent_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(selectContentMethod, postfix: new HarmonyMethod(postfix));
                    MelonLogger.Msg("[Naming] Patched SelectContent");
                }
                else
                {
                    MelonLogger.Warning("[Naming] Could not find SelectContent method");
                }

                // Patch InitializeInput - called when entering keyboard input mode
                var initInputMethod = AccessTools.Method(controllerType, "InitializeInput");
                if (initInputMethod != null)
                {
                    var postfix = typeof(NamingPatches).GetMethod(nameof(InitializeInput_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(initInputMethod, postfix: new HarmonyMethod(postfix));
                    MelonLogger.Msg("[Naming] Patched InitializeInput");
                }
                else
                {
                    MelonLogger.Warning("[Naming] Could not find InitializeInput method");
                }

                // Patch InitializeSelect - called when entering character selection mode
                var initSelectMethod = AccessTools.Method(controllerType, "InitializeSelect");
                if (initSelectMethod != null)
                {
                    var postfix = typeof(NamingPatches).GetMethod(nameof(InitializeSelect_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(initSelectMethod, postfix: new HarmonyMethod(postfix));
                    MelonLogger.Msg("[Naming] Patched InitializeSelect");
                }
                else
                {
                    MelonLogger.Warning("[Naming] Could not find InitializeSelect method");
                }

                // Patch Open - to track when naming menu is opened
                var openMethod = AccessTools.Method(controllerType, "Open");
                if (openMethod != null)
                {
                    var postfix = typeof(NamingPatches).GetMethod(nameof(Open_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(openMethod, postfix: new HarmonyMethod(postfix));
                    MelonLogger.Msg("[Naming] Patched Open");
                }
                else
                {
                    MelonLogger.Warning("[Naming] Could not find Open method");
                }

                // Patch Close - to track when naming menu is closed
                var closeMethod = AccessTools.Method(controllerType, "Close");
                if (closeMethod != null)
                {
                    var postfix = typeof(NamingPatches).GetMethod(nameof(Close_Postfix),
                        BindingFlags.Public | BindingFlags.Static);
                    harmony.Patch(closeMethod, postfix: new HarmonyMethod(postfix));
                    MelonLogger.Msg("[Naming] Patched Close");
                }
                else
                {
                    MelonLogger.Warning("[Naming] Could not find Close method");
                }

                isPatched = true;
                MelonLogger.Msg("[Naming] Patches applied successfully");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Naming] Error applying patches: {ex.Message}");
                MelonLogger.Error($"[Naming] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Postfix for Open - marks naming menu as active.
        /// </summary>
        public static void Open_Postfix()
        {
            IsNamingMenuActive = true;
            AnnouncementDeduplicator.Reset(DEDUP_CONTEXT);
        }

        /// <summary>
        /// Postfix for Close - marks naming menu as inactive.
        /// </summary>
        public static void Close_Postfix()
        {
            IsNamingMenuActive = false;
            AnnouncementDeduplicator.Reset(DEDUP_CONTEXT);
        }

        /// <summary>
        /// Postfix for InitializeSelect - announces entering character selection.
        /// </summary>
        public static void InitializeSelect_Postfix(ChangeNameController __instance)
        {
            try
            {
                AnnouncementDeduplicator.Reset(DEDUP_CONTEXT);

                // Try to announce the currently selected character
                string characterName = GetSelectedCharacterName(__instance);
                if (!string.IsNullOrEmpty(characterName))
                {
                    FFV_ScreenReaderMod.SpeakText($"Name: {characterName}", interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Naming] Error in InitializeSelect_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for SelectContent - announces the selected character.
        /// </summary>
        public static void SelectContent_Postfix(ChangeNameController __instance, GameCursor targetCursor)
        {
            try
            {
                if (targetCursor == null)
                {
                    return;
                }

                int cursorIndex = targetCursor.Index;

                // Avoid duplicate announcements
                if (!AnnouncementDeduplicator.ShouldAnnounce(DEDUP_CONTEXT, cursorIndex))
                {
                    return;
                }

                // Get character name from contentList at cursor index
                string characterName = GetCharacterNameAtIndex(__instance, cursorIndex);

                if (!string.IsNullOrEmpty(characterName))
                {
                    FFV_ScreenReaderMod.SpeakText(characterName, interrupt: true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Naming] Error in SelectContent_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for InitializeInput - announces keyboard input mode.
        /// </summary>
        public static void InitializeInput_Postfix(ChangeNameController __instance)
        {
            FFV_ScreenReaderMod.SpeakText("Keyboard input active", interrupt: true);
        }

        /// <summary>
        /// Gets the character name at the specified index from contentList.
        /// </summary>
        private static string GetCharacterNameAtIndex(ChangeNameController controller, int index)
        {
            try
            {
                if (controller == null) return null;

                // Try using the contentList property/field
                var contentListProp = AccessTools.Property(typeof(ChangeNameController), "contentList");
                if (contentListProp == null)
                {
                    // Try field access
                    var contentListField = AccessTools.Field(typeof(ChangeNameController), "contentList");
                    if (contentListField != null)
                    {
                        var list = contentListField.GetValue(controller);
                        return GetNameFromList(list, index);
                    }

                    // Try memory offset as fallback
                    return GetNameFromMemoryOffset(controller.Pointer, index);
                }

                var contentList = contentListProp.GetValue(controller);
                return GetNameFromList(contentList, index);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Naming] Error getting character name at index: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets name from a List object using reflection.
        /// </summary>
        private static string GetNameFromList(object list, int index)
        {
            if (list == null) return null;

            try
            {
                // Get count
                var countProp = list.GetType().GetProperty("Count");
                if (countProp == null) return null;

                int count = (int)countProp.GetValue(list);
                if (index < 0 || index >= count) return null;

                // Get item at index
                var indexer = list.GetType().GetProperty("Item");
                if (indexer == null) return null;

                var contentController = indexer.GetValue(list, new object[] { index });
                if (contentController == null) return null;

                // Get TargetData from ChangeNameContentController
                var targetDataProp = AccessTools.Property(contentController.GetType(), "TargetData");
                if (targetDataProp != null)
                {
                    var targetData = targetDataProp.GetValue(contentController);
                    if (targetData != null)
                    {
                        // Get Name from OwnedCharacterData
                        var nameProp = AccessTools.Property(targetData.GetType(), "Name");
                        if (nameProp != null)
                        {
                            return nameProp.GetValue(targetData) as string;
                        }
                    }
                }

                // Fallback: Try NameText property
                var nameTextProp = AccessTools.Property(contentController.GetType(), "NameText");
                if (nameTextProp != null)
                {
                    var nameText = nameTextProp.GetValue(contentController);
                    if (nameText != null)
                    {
                        var textProp = nameText.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            return textProp.GetValue(nameText) as string;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Naming] Error reading from list: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets name using memory offsets as fallback.
        /// </summary>
        private static string GetNameFromMemoryOffset(IntPtr controllerPtr, int index)
        {
            if (controllerPtr == IntPtr.Zero) return null;

            try
            {
                // Read contentList pointer at offset 0x30
                IntPtr listPtr = Marshal.ReadIntPtr(controllerPtr + CONTENT_LIST_OFFSET);
                if (listPtr == IntPtr.Zero) return null;

                // IL2CPP List: _size at 0x18, _items at 0x10
                int size = Marshal.ReadInt32(listPtr + 0x18);
                if (index < 0 || index >= size) return null;

                IntPtr itemsPtr = Marshal.ReadIntPtr(listPtr + 0x10);
                if (itemsPtr == IntPtr.Zero) return null;

                // Array elements start at 0x20, 8 bytes per pointer
                IntPtr contentControllerPtr = Marshal.ReadIntPtr(itemsPtr + 0x20 + (index * 8));
                if (contentControllerPtr == IntPtr.Zero) return null;

                // Read targetData at offset 0x28
                IntPtr targetDataPtr = Marshal.ReadIntPtr(contentControllerPtr + TARGET_DATA_OFFSET);
                if (targetDataPtr == IntPtr.Zero) return null;

                // Try creating the managed wrapper
                var targetData = new Il2CppLast.Data.User.OwnedCharacterData(targetDataPtr);
                return targetData?.Name;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Naming] Error reading from memory: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the currently selected character name from selectedData.
        /// </summary>
        private static string GetSelectedCharacterName(ChangeNameController controller)
        {
            try
            {
                if (controller == null) return null;

                // Try selectedData field
                var selectedDataField = AccessTools.Field(typeof(ChangeNameController), "selectedData");
                if (selectedDataField != null)
                {
                    var selectedData = selectedDataField.GetValue(controller) as Il2CppLast.Data.User.OwnedCharacterData;
                    if (selectedData != null)
                    {
                        return selectedData.Name;
                    }
                }

                // Try reading the input field text as fallback
                var inputFieldProp = AccessTools.Property(typeof(ChangeNameController), "inputField");
                if (inputFieldProp != null)
                {
                    var inputField = inputFieldProp.GetValue(controller);
                    if (inputField != null)
                    {
                        var textProp = inputField.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            return textProp.GetValue(inputField) as string;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Naming] Error getting selected character: {ex.Message}");
            }

            return null;
        }
    }
}
