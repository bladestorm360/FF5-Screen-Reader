using System;
using Il2CppLast.Data;
using Il2CppLast.Management;
using Il2CppLast.UI.Touch;
using MelonLoader;
using UnityEngine;
using static FFV_ScreenReader.Utils.TextUtils;

namespace FFV_ScreenReader.Menus
{
    /// <summary>
    /// Handles reading save slot information for the load game menu.
    /// Extracts and announces: empty/occupied status, location, play time, character level.
    /// </summary>
    public static class SaveSlotReader
    {
        public static string TryReadSaveSlot(Transform cursorTransform, int cursorIndex)
        {
            try
            {
                MelonLogger.Msg($"=== SaveSlotReader: Checking cursor at index {cursorIndex} ===");

                Transform current = cursorTransform;
                int depth = 0;

                while (current != null && depth < 15)
                {
                    if (current.name.Contains("save") || current.name.Contains("load") ||
                        current.name.Contains("data_select"))
                    {
                        MelonLogger.Msg($"Found potential save menu structure: {current.name}");

                        Transform contentList = FindContentList(current);

                        if (contentList != null && cursorIndex >= 0 && cursorIndex < contentList.childCount)
                        {
                            Transform saveSlot = contentList.GetChild(cursorIndex);
                            MelonLogger.Msg($"Found save slot at index {cursorIndex}: {saveSlot.name}");

                            string slotInfo = ReadSlotInformation(saveSlot, cursorIndex);
                            if (slotInfo != null)
                            {
                                return slotInfo;
                            }
                        }
                    }

                    current = current.parent;
                    depth++;
                }

                MelonLogger.Msg("SaveSlotReader: Not a save slot menu");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"SaveSlotReader error: {ex.Message}");
            }

            return null;
        }

        private static Transform FindContentList(Transform root)
        {
            try
            {
                var content = FindTransformInChildren(root, "Content");
                if (content != null && content.parent != null &&
                    (content.parent.name == "Viewport" || content.parent.parent?.name == "Scroll View"))
                {
                    return content;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error finding content list: {ex.Message}");
            }
            return null;
        }

        private static string ReadSlotInformation(Transform slotTransform, int slotIndex)
        {
            try
            {
                var saveController = slotTransform.GetComponent<SaveContentController>();
                if (saveController == null)
                {
                    saveController = slotTransform.GetComponentInChildren<SaveContentController>();
                }

                var saveView = slotTransform.GetComponent<SaveContentView>();
                if (saveView == null)
                {
                    saveView = slotTransform.GetComponentInChildren<SaveContentView>();
                }

                if (saveView != null)
                {
                    MelonLogger.Msg("Found SaveContentView, extracting text fields");
                    return ReadFromSaveContentView(saveView, slotIndex);
                }

                MelonLogger.Msg("Trying text component fallback");
                return ReadFromTextComponents(slotTransform, slotIndex);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading slot information: {ex.Message}");
            }

            return null;
        }

        private static string ReadFromSaveContentView(SaveContentView view, int slotIndex)
        {
            try
            {
                MelonLogger.Msg("=== Inspecting SaveContentView fields ===");

                if (view.EmptyText != null && view.EmptyText.gameObject != null &&
                    view.EmptyText.gameObject.activeSelf)
                {
                    string emptyText = view.EmptyText.text;
                    if (!string.IsNullOrEmpty(emptyText))
                    {
                        MelonLogger.Msg($"Slot {slotIndex + 1} is empty");
                        return $"Slot {slotIndex + 1}: {emptyText}";
                    }
                }

                string slotNum = $"Slot {slotIndex + 1}";
                string location = "";
                string playTime = "";
                string level = "";

                if (view.areaNameText != null && !string.IsNullOrEmpty(view.areaNameText.text))
                {
                    location = view.areaNameText.text;

                    if (view.floorNameText != null && !string.IsNullOrEmpty(view.floorNameText.text))
                    {
                        location += " - " + view.floorNameText.text;
                    }
                }

                if (view.hourText != null && view.minuteText != null)
                {
                    string hours = view.hourText.text;
                    string minutes = view.minuteText.text;

                    if (!string.IsNullOrEmpty(hours) || !string.IsNullOrEmpty(minutes))
                    {
                        playTime = $"{hours}:{minutes}";
                    }
                }

                if (view.LevelText != null && !string.IsNullOrEmpty(view.LevelText.text))
                {
                    level = view.LevelText.text;
                }

                string announcement = slotNum;

                if (!string.IsNullOrEmpty(location))
                {
                    announcement += ": " + location;
                }

                if (!string.IsNullOrEmpty(level))
                {
                    announcement += ", Level " + level;
                }

                if (!string.IsNullOrEmpty(playTime))
                {
                    announcement += ", " + playTime;
                }

                MelonLogger.Msg($"Save slot info: {announcement}");
                return announcement;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading from SaveContentView: {ex.Message}");
            }

            return null;
        }

        private static string ReadFromTextComponents(Transform slotTransform, int slotIndex)
        {
            try
            {
                string location = null;
                string playTimeHour = null;
                string playTimeMinute = null;
                string level = null;
                string characterName = null;
                string empty = null;
                string slotType = null;
                string slotNumber = null;

                ForEachTextInChildren(slotTransform, text =>
                {
                    if (text == null || text.text == null) return;

                    string content = text.text.Trim();
                    if (string.IsNullOrEmpty(content)) return;

                    MelonLogger.Msg($"  Text component '{text.name}': '{content}'");

                    if (text.name.Contains("chara_name"))
                    {
                        characterName = content;
                    }
                    else if (text.name == "lv" || text.name == "level")
                    {
                        level = content;
                    }
                    else if (text.name == "current_area_name")
                    {
                        location = content;
                    }
                    else if (text.name == "time" && text.transform.parent?.name.Contains("hour") != true)
                    {
                        if (playTimeHour == null)
                            playTimeHour = content;
                        else if (playTimeMinute == null)
                            playTimeMinute = content;
                    }
                    else if (text.name == "time (2)")
                    {
                        playTimeMinute = content;
                    }
                    else if (text.name == "data")
                    {
                        slotType = content;
                    }
                    else if (text.name == "number")
                    {
                        slotNumber = content;
                    }
                    else if (text.name == "empty")
                    {
                        empty = content;
                    }
                });

                bool hasData = !string.IsNullOrEmpty(characterName) ||
                               !string.IsNullOrEmpty(level) ||
                               !string.IsNullOrEmpty(location);

                if (!hasData && !string.IsNullOrEmpty(empty))
                {
                    string slotIdentifier = "";
                    if (!string.IsNullOrEmpty(slotType))
                    {
                        slotIdentifier = slotType;
                        if (!string.IsNullOrEmpty(slotNumber))
                        {
                            slotIdentifier += " " + slotNumber;
                        }
                    }
                    else
                    {
                        slotIdentifier = $"Slot {slotIndex + 1}";
                    }

                    MelonLogger.Msg($"{slotIdentifier} is empty");
                    return $"{slotIdentifier}: {empty}";
                }

                string announcement = "";
                if (!string.IsNullOrEmpty(slotType))
                {
                    announcement = slotType;
                    if (!string.IsNullOrEmpty(slotNumber) && slotType != "Autosave" && slotType != "Quicksave")
                    {
                        announcement += " " + slotNumber;
                    }
                }
                else
                {
                    announcement = $"Slot {slotIndex + 1}";
                }

                if (!string.IsNullOrEmpty(location))
                {
                    announcement += ": " + location;
                }

                if (!string.IsNullOrEmpty(characterName))
                {
                    announcement += ", " + characterName;
                    if (!string.IsNullOrEmpty(level))
                    {
                        announcement += " Level " + level;
                    }
                }
                else if (!string.IsNullOrEmpty(level))
                {
                    announcement += ", Level " + level;
                }

                if (!string.IsNullOrEmpty(playTimeHour) && !string.IsNullOrEmpty(playTimeMinute))
                {
                    announcement += $", {playTimeHour}:{playTimeMinute}";
                }

                if (announcement != $"Slot {slotIndex + 1}")
                {
                    MelonLogger.Msg($"Fallback read successful: {announcement}");
                    return announcement;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in fallback text reading: {ex.Message}");
            }

            return null;
        }
    }
}