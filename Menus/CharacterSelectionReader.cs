using System;
using MelonLoader;
using UnityEngine;
using static FFV_ScreenReader.Utils.TextUtils;

namespace FFV_ScreenReader.Menus
{
    /// <summary>
    /// Handles reading character information from character selection screens.
    /// Used in menus like Status, Equipment, Skills, etc.
    /// Extracts and announces: character name, job, level, HP, MP.
    /// </summary>
    public static class CharacterSelectionReader
    {
        /// <summary>
        /// Try to read character information from the current cursor position.
        /// Returns a formatted string with character information, or null if not a character selection.
        /// </summary>
        public static string TryReadCharacterSelection(Transform cursorTransform, int cursorIndex)
        {
            try
            {
                MelonLogger.Msg($"=== CharacterSelectionReader: Checking cursor at index {cursorIndex} ===");

                // Walk up the hierarchy to find character selection structures
                Transform current = cursorTransform;
                int depth = 0;

                while (current != null && depth < 15)
                {
                    string name = current.name.ToLower();

                    // Look for character selection menu structures
                    if (name.Contains("chara") || name.Contains("status") ||
                        name.Contains("party") || name.Contains("member"))
                    {
                        MelonLogger.Msg($"Found potential character menu structure: {current.name}");

                        // Strategy 1: Try to find Content list with indexed children
                        Transform contentList = FindTransformInChildren(current, "Content");
                        if (contentList != null && cursorIndex >= 0 && cursorIndex < contentList.childCount)
                        {
                            Transform characterSlot = contentList.GetChild(cursorIndex);
                            MelonLogger.Msg($"Found character slot via Content at index {cursorIndex}: {characterSlot.name}");
                            string info = ReadFromTextComponents(characterSlot);
                            if (info != null) return info;
                        }

                        // Strategy 2: Read directly from this panel (for status_info_content style)
                        MelonLogger.Msg($"Trying to read directly from: {current.name}");
                        string characterInfo = ReadFromTextComponents(current);
                        if (characterInfo != null)
                        {
                            return characterInfo;
                        }
                    }

                    current = current.parent;
                    depth++;
                }

                MelonLogger.Msg("CharacterSelectionReader: Not a character selection menu");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"CharacterSelectionReader error: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Read character information from text components.
        /// </summary>
        private static string ReadFromTextComponents(Transform slotTransform)
        {
            try
            {
                string characterName = null;
                string jobName = null;
                string level = null;
                string currentHP = null;
                string maxHP = null;
                string currentMP = null;
                string maxMP = null;
                string firstValidText = null; // Fallback for character name
                int textCount = 0;
                bool foundLvLabel = false;

                // Use non-allocating traversal
                ForEachTextInChildren(slotTransform, text =>
                {
                    textCount++;
                    if (text == null || text.text == null) return;

                    string content = text.text.Trim();
                    if (string.IsNullOrEmpty(content)) return;

                    string textName = text.name.ToLower();

                    MelonLogger.Msg($"  Text '{text.name}': '{content}'");

                    // Track labels
                    string upperContent = content.ToUpper();
                    if (upperContent == "LV" || upperContent == "LEVEL")
                    {
                        foundLvLabel = true;
                        return;
                    }
                    if (upperContent == "HP" || upperContent == "MP")
                    {
                        return; // Skip labels
                    }
                    if (content == "/" || content == ":")
                    {
                        return;
                    }

                    // Character name - look for name fields but not job_name
                    if (textName.Contains("name") && !textName.Contains("job"))
                    {
                        if (content.Length > 1 && content.Length < 20 && !IsNumeric(content))
                        {
                            characterName = content;
                            MelonLogger.Msg($"  -> Found character name: '{content}'");
                        }
                    }
                    // Job name
                    else if (textName.Contains("job") || textName.Contains("class"))
                    {
                        if (content.Length > 0 && !IsNumeric(content))
                        {
                            jobName = content;
                            MelonLogger.Msg($"  -> Found job: '{content}'");
                        }
                    }
                    // Level - look for numeric value in level-related fields OR small number after LV label
                    else if (textName.Contains("lv") || textName.Contains("level"))
                    {
                        if (IsNumeric(content))
                        {
                            level = content;
                            MelonLogger.Msg($"  -> Found level: '{content}'");
                        }
                    }
                    // HP values - match by field name containing "hp"
                    else if (textName.Contains("hp"))
                    {
                        if (IsNumeric(content))
                        {
                            // Check for "current" or "max" in the name
                            if (textName.Contains("current") || textName.Contains("now"))
                            {
                                // Only update if we don't have a value yet, or if current value is "0"
                                if (currentHP == null || currentHP == "0")
                                {
                                    currentHP = content;
                                    MelonLogger.Msg($"  -> Found current HP: '{content}'");
                                }
                            }
                            else if (textName.Contains("max"))
                            {
                                // Only update if we don't have a value yet, or if current value is "0"
                                if (maxHP == null || maxHP == "0")
                                {
                                    maxHP = content;
                                    MelonLogger.Msg($"  -> Found max HP: '{content}'");
                                }
                            }
                        }
                    }
                    // MP values - match by field name containing "mp"
                    else if (textName.Contains("mp"))
                    {
                        if (IsNumeric(content))
                        {
                            // Check for "current" or "max" in the name
                            if (textName.Contains("current") || textName.Contains("now"))
                            {
                                if (currentMP == null || currentMP == "0")
                                {
                                    currentMP = content;
                                    MelonLogger.Msg($"  -> Found current MP: '{content}'");
                                }
                            }
                            else if (textName.Contains("max"))
                            {
                                if (maxMP == null || maxMP == "0")
                                {
                                    maxMP = content;
                                    MelonLogger.Msg($"  -> Found max MP: '{content}'");
                                }
                            }
                        }
                    }
                    // Track first valid text as potential character name fallback
                    // Must be non-numeric, not a known label, and reasonable length
                    else if (firstValidText == null && !IsNumeric(content) &&
                             content.Length >= 2 && content.Length <= 15 &&
                             upperContent != "LV" && upperContent != "HP" && upperContent != "MP" &&
                             upperContent != "LEVEL")
                    {
                        firstValidText = content;
                        MelonLogger.Msg($"  -> Tracking as potential name: '{content}'");
                    }
                    // If we found an LV label and this is a small number, it's probably the level
                    else if (foundLvLabel && level == null && IsNumeric(content) && content.Length <= 3)
                    {
                        int val;
                        if (int.TryParse(content, out val) && val >= 1 && val <= 99)
                        {
                            level = content;
                            MelonLogger.Msg($"  -> Found level after label: '{content}'");
                        }
                    }
                });

                MelonLogger.Msg($"Found {textCount} text components in character slot");

                // If we didn't find a character name via "name" field, use first valid text as fallback
                if (string.IsNullOrEmpty(characterName) && !string.IsNullOrEmpty(firstValidText))
                {
                    if (firstValidText != jobName)
                    {
                        characterName = firstValidText;
                        MelonLogger.Msg($"  Using fallback for character name: '{characterName}'");
                    }
                }

                // Build announcement string
                string announcement = "";

                // Start with character name
                if (!string.IsNullOrEmpty(characterName))
                {
                    announcement = characterName;
                }

                // Add level
                if (!string.IsNullOrEmpty(level))
                {
                    if (!string.IsNullOrEmpty(announcement))
                    {
                        announcement += ", Level " + level;
                    }
                    else
                    {
                        announcement = "Level " + level;
                    }
                }

                // Add job name
                if (!string.IsNullOrEmpty(jobName))
                {
                    if (!string.IsNullOrEmpty(announcement))
                    {
                        announcement += ", " + jobName;
                    }
                    else
                    {
                        announcement = jobName;
                    }
                }

                // Add HP
                if (!string.IsNullOrEmpty(currentHP) && !string.IsNullOrEmpty(maxHP))
                {
                    announcement += $", HP {currentHP}/{maxHP}";
                }
                else if (!string.IsNullOrEmpty(currentHP))
                {
                    announcement += $", HP {currentHP}";
                }

                // Add MP
                if (!string.IsNullOrEmpty(currentMP) && !string.IsNullOrEmpty(maxMP))
                {
                    announcement += $", MP {currentMP}/{maxMP}";
                }
                else if (!string.IsNullOrEmpty(currentMP))
                {
                    announcement += $", MP {currentMP}";
                }

                if (!string.IsNullOrEmpty(announcement))
                {
                    MelonLogger.Msg($"Character info read: {announcement}");
                    return announcement;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading character text components: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Check if a string is numeric
        /// </summary>
        private static bool IsNumeric(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (!char.IsDigit(c)) return false;
            }
            return true;
        }
    }
}
