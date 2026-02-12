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
        /// <summary>
        /// Try to read save slot information from the current cursor position.
        /// Returns a formatted string with all relevant save information, or null if not a save slot.
        /// </summary>
        public static string TryReadSaveSlot(Transform cursorTransform, int cursorIndex)
        {
            try
            {
                // Walk up the hierarchy to find the save list structure
                Transform current = cursorTransform;
                int depth = 0;

                while (current != null && depth < 15)
                {
                    // Look for save/load menu structures
                    if (current.name.Contains("save") || current.name.Contains("load") ||
                        current.name.Contains("data_select"))
                    {
                        // Try to find Content list (common pattern: Scroll View -> Viewport -> Content)
                        Transform contentList = FindContentList(current);

                        if (contentList != null && cursorIndex >= 0 && cursorIndex < contentList.childCount)
                        {
                            Transform saveSlot = contentList.GetChild(cursorIndex);

                            // Try to read the slot information
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
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"SaveSlotReader error: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Read information from a save slot transform.
        /// Returns formatted announcement string or null if unable to read.
        /// </summary>
        private static string ReadSlotInformation(Transform slotTransform, int slotIndex)
        {
            try
            {
                // Try to get SaveContentController component
                var saveController = slotTransform.GetComponent<SaveContentController>();
                if (saveController == null)
                {
                    saveController = slotTransform.GetComponentInChildren<SaveContentController>();
                }

                // Try to get SaveContentView component
                var saveView = slotTransform.GetComponent<SaveContentView>();
                if (saveView == null)
                {
                    saveView = slotTransform.GetComponentInChildren<SaveContentView>();
                }

                // Try direct text extraction as fallback
                if (saveView != null)
                {
                    return ReadFromSaveContentView(saveView, slotIndex);
                }

                // Fallback: Try to find text components
                return ReadFromTextComponents(slotTransform, slotIndex);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading slot information: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Read save information from SaveContentView component.
        /// Uses public properties where available for reliable access.
        /// Format: "[SlotName] [SlotNum], [Date] [Time], [Location], [CharName] Level [Level], Time [Hours]:[Minutes]"
        /// </summary>
        private static string ReadFromSaveContentView(SaveContentView view, int slotIndex)
        {
            try
            {
                // Read slot name and number using public properties
                string slotName = "";
                string slotNum = "";

                try
                {
                    if (view.SlotNameText != null)
                        slotName = view.SlotNameText.text?.Trim() ?? "";
                }
                catch { }

                try
                {
                    if (view.SlotNumText != null)
                        slotNum = view.SlotNumText.text?.Trim() ?? "";
                }
                catch { }

                // Build slot identifier
                string slotId = slotName;
                if (!string.IsNullOrEmpty(slotNum) &&
                    !slotName.Contains("Auto") && !slotName.Contains("Quick"))
                {
                    slotId = $"{slotName} {slotNum}".Trim();
                }
                if (string.IsNullOrEmpty(slotId))
                    slotId = $"Slot {slotIndex + 1}";

                // Check if slot is empty
                if (view.EmptyText != null && view.EmptyText.gameObject != null &&
                    view.EmptyText.gameObject.activeSelf)
                {
                    string emptyText = view.EmptyText.text;
                    if (!string.IsNullOrEmpty(emptyText))
                    {
                        return $"{slotId}, {emptyText}";
                    }
                }

                // Read timestamp using public properties
                string date = "";
                string time = "";
                try
                {
                    if (view.TimeStampDate != null)
                        date = view.TimeStampDate.text?.Trim() ?? "";
                }
                catch { }

                try
                {
                    if (view.TimeStampTime != null)
                        time = view.TimeStampTime.text?.Trim() ?? "";
                }
                catch { }

                // Get location (area + floor)
                string location = "";
                if (view.areaNameText != null && !string.IsNullOrEmpty(view.areaNameText.text))
                {
                    location = view.areaNameText.text.Trim();

                    if (view.floorNameText != null && !string.IsNullOrEmpty(view.floorNameText.text))
                    {
                        location += " " + view.floorNameText.text.Trim();
                    }
                }

                // Get level
                string level = "";
                if (view.LevelText != null && !string.IsNullOrEmpty(view.LevelText.text))
                {
                    level = view.LevelText.text.Trim();
                }

                // Get play time
                string playTime = "";
                if (view.hourText != null && view.minuteText != null)
                {
                    string hours = view.hourText.text?.Trim() ?? "";
                    string minutes = view.minuteText.text?.Trim() ?? "";

                    if (!string.IsNullOrEmpty(hours) || !string.IsNullOrEmpty(minutes))
                    {
                        playTime = $"{hours}:{minutes}";
                    }
                }

                // Build the announcement string in visual display order
                var parts = new System.Collections.Generic.List<string>();

                // Slot identifier
                if (!string.IsNullOrEmpty(slotId))
                    parts.Add(slotId);

                // Timestamp (date time)
                if (!string.IsNullOrEmpty(date) && !string.IsNullOrEmpty(time))
                    parts.Add($"{date} {time}");
                else if (!string.IsNullOrEmpty(date))
                    parts.Add(date);

                // Location
                if (!string.IsNullOrEmpty(location))
                    parts.Add(location);

                // Level (with character name if available - though charaNameText is private)
                if (!string.IsNullOrEmpty(level))
                    parts.Add($"Level {level}");

                // Play time
                if (!string.IsNullOrEmpty(playTime))
                    parts.Add($"Time {playTime}");

                string announcement = string.Join(", ", parts);
                return announcement;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading from SaveContentView: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Fallback: Try to find and read text components directly.
        /// </summary>
        private static string ReadFromTextComponents(Transform slotTransform, int slotIndex)
        {
            try
            {
                // Look for specific text patterns
                string location = null;
                string playTimeHour = null;
                string playTimeMinute = null;
                string level = null;
                string characterName = null;
                string empty = null;
                string slotType = null;  // Autosave, Quicksave, File, etc.
                string slotNumber = null;
                string timestampDate = null;
                string timestampTime = null;

                // Use non-allocating traversal instead of GetComponentsInChildren
                ForEachTextInChildren(slotTransform, text =>
                {
                    if (text == null || text.text == null) return;

                    string content = text.text.Trim();
                    if (string.IsNullOrEmpty(content)) return;

                    string textName = text.name?.ToLowerInvariant() ?? "";

                    // Check for character name
                    if (textName.Contains("chara") && textName.Contains("name"))
                    {
                        characterName = content;
                    }
                    // Check for level value (not the "Lv." label)
                    else if (textName == "lv" || textName == "level" || textName == "leveltext")
                    {
                        level = content;
                    }
                    // Check for location - specifically current_area_name or areaname
                    else if (textName.Contains("area") && textName.Contains("name"))
                    {
                        location = content;
                    }
                    // Check for timestamp date
                    else if (textName.Contains("timestamp") && textName.Contains("date"))
                    {
                        timestampDate = content;
                    }
                    // Check for timestamp time
                    else if (textName.Contains("timestamp") && textName.Contains("time"))
                    {
                        timestampTime = content;
                    }
                    // Check for hour component
                    else if (textName == "hour" || textName == "hourtext")
                    {
                        playTimeHour = content;
                    }
                    // Check for minute component
                    else if (textName == "minute" || textName == "minutetext")
                    {
                        playTimeMinute = content;
                    }
                    // Check for time components (fallback for old pattern)
                    else if (textName == "time" && text.transform.parent?.name.Contains("hour") != true)
                    {
                        if (playTimeHour == null)
                            playTimeHour = content;
                        else if (playTimeMinute == null)
                            playTimeMinute = content;
                    }
                    else if (textName == "time (2)")
                    {
                        playTimeMinute = content;
                    }
                    // Check for slot type (Autosave, File, etc.)
                    else if (textName == "data" || textName.Contains("slotname"))
                    {
                        slotType = content;
                    }
                    // Check for slot number
                    else if (textName == "number" || textName.Contains("slotnum"))
                    {
                        slotNumber = content;
                    }
                    // Check for empty slot text
                    else if (textName == "empty" || textName == "emptytext")
                    {
                        empty = content;
                    }
                });

                // Build slot identifier
                string slotIdentifier = "";
                if (!string.IsNullOrEmpty(slotType))
                {
                    slotIdentifier = slotType;
                    if (!string.IsNullOrEmpty(slotNumber) &&
                        !slotType.Contains("Auto") && !slotType.Contains("Quick"))
                    {
                        slotIdentifier += " " + slotNumber;
                    }
                }
                else
                {
                    slotIdentifier = $"Slot {slotIndex + 1}";
                }

                // IMPORTANT: Only treat as empty if we have NO character data
                bool hasData = !string.IsNullOrEmpty(characterName) ||
                               !string.IsNullOrEmpty(level) ||
                               !string.IsNullOrEmpty(location);

                if (!hasData && !string.IsNullOrEmpty(empty))
                {
                    return $"{slotIdentifier}, {empty}";
                }

                // Build announcement for occupied slot in visual display order
                var parts = new System.Collections.Generic.List<string>();

                // Slot identifier
                parts.Add(slotIdentifier);

                // Timestamp (date time)
                if (!string.IsNullOrEmpty(timestampDate) && !string.IsNullOrEmpty(timestampTime))
                    parts.Add($"{timestampDate} {timestampTime}");
                else if (!string.IsNullOrEmpty(timestampDate))
                    parts.Add(timestampDate);

                // Location
                if (!string.IsNullOrEmpty(location))
                    parts.Add(location);

                // Character name and level together
                if (!string.IsNullOrEmpty(characterName))
                {
                    if (!string.IsNullOrEmpty(level))
                        parts.Add($"{characterName} Level {level}");
                    else
                        parts.Add(characterName);
                }
                else if (!string.IsNullOrEmpty(level))
                {
                    parts.Add($"Level {level}");
                }

                // Play time
                if (!string.IsNullOrEmpty(playTimeHour) && !string.IsNullOrEmpty(playTimeMinute))
                    parts.Add($"Time {playTimeHour}:{playTimeMinute}");

                if (parts.Count > 1)
                {
                    string announcement = string.Join(", ", parts);
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
