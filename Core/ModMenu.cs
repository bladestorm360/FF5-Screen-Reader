using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Audio-only virtual menu for adjusting screen reader settings.
    /// Accessible via F8 key. No Unity UI overlay - purely navigational state + announcements.
    /// </summary>
    public static class ModMenu
    {
        /// <summary>
        /// Whether the mod menu is currently open.
        /// </summary>
        public static bool IsOpen { get; private set; }

        private static int currentIndex = 0;
        private static List<MenuItem> items;

        #region Menu Item Types

        private abstract class MenuItem
        {
            // mod_text keys, translated when spoken: Initialize runs at mod load, before the game's
            // MessageManager exists, so translating there would pin every label to English.
            protected string nameKey;
            protected string descriptionKey;

            public string Name => T(nameKey);
            // What the setting does, read on demand with I. Blank for section headers.
            public string Description => string.IsNullOrEmpty(descriptionKey) ? "" : T(descriptionKey);
            public abstract string GetValueString();
            public abstract void Adjust(int delta);
            public abstract void Toggle();
        }

        private class ToggleItem : MenuItem
        {
            private readonly Func<bool> getter;
            private readonly Action toggle;

            public ToggleItem(string name, Func<bool> getter, Action toggle, string description)
            {
                nameKey = name;
                this.getter = getter;
                this.toggle = toggle;
                descriptionKey = description;
            }

            public override string GetValueString() => getter() ? T("On") : T("Off");
            public override void Adjust(int delta) => toggle();
            public override void Toggle() => toggle();
        }

        private class VolumeItem : MenuItem
        {
            private readonly Func<int> getter;
            private readonly Action<int> setter;

            public VolumeItem(string name, Func<int> getter, Action<int> setter, string description)
            {
                nameKey = name;
                this.getter = getter;
                this.setter = setter;
                descriptionKey = description;
            }

            public override string GetValueString() => $"{getter()}%";

            public override void Adjust(int delta)
            {
                int current = getter();
                int newValue = Math.Clamp(current + (delta * 5), 0, 100);
                setter(newValue);
            }

            public override void Toggle()
            {
                // Toggle between 0 and 50 for quick mute/unmute
                int current = getter();
                setter(current == 0 ? 50 : 0);
            }
        }

        private class EnumItem : MenuItem
        {
            private readonly string[] options;
            private readonly Func<int> getter;
            private readonly Action<int> setter;

            public EnumItem(string name, string[] options, Func<int> getter, Action<int> setter, string description)
            {
                nameKey = name;
                this.options = options;
                this.getter = getter;
                this.setter = setter;
                descriptionKey = description;
            }

            public override string GetValueString()
            {
                int index = getter();
                if (index >= 0 && index < options.Length)
                    return T(options[index]);
                return T("Unknown");
            }

            public override void Adjust(int delta)
            {
                int current = getter();
                int newValue = current + delta;
                if (newValue < 0) newValue = options.Length - 1;
                if (newValue >= options.Length) newValue = 0;
                setter(newValue);
            }

            public override void Toggle() => Adjust(1);
        }

        private class SectionHeader : MenuItem
        {
            public SectionHeader(string name)
            {
                nameKey = name;
            }

            public override string GetValueString() => "";
            public override void Adjust(int delta) { }
            public override void Toggle() { }
        }

        private class ActionItem : MenuItem
        {
            private readonly Action action;

            public ActionItem(string name, Action action, string description)
            {
                nameKey = name;
                this.action = action;
                descriptionKey = description;
            }

            public override string GetValueString() => "";
            public override void Adjust(int delta) => action();
            public override void Toggle() => action();
        }

        #endregion

        /// <summary>
        /// Initializes the mod menu with all menu items.
        /// Call this once during mod initialization. Every string here is a mod_text.json key,
        /// translated when spoken (see MenuItem) — so no T() at these call sites.
        /// </summary>
        public static void Initialize()
        {
            items = new List<MenuItem>
            {
                // Audio Feedback section
                new SectionHeader("Audio Feedback"),
                new ToggleItem("Wall Tones",
                    () => PreferencesManager.WallTonesEnabled,
                    () => FFV_ScreenReaderMod.Instance?.ToggleWallTones(),
                    "Plays a tone toward any wall right next to you, so you can feel out corridors and doorways."),
                new ToggleItem("Footsteps",
                    () => PreferencesManager.FootstepsEnabled,
                    () => FFV_ScreenReaderMod.Instance?.ToggleFootsteps(),
                    "Plays a click for each tile you move on foot."),
                new ToggleItem("Audio Beacons",
                    () => PreferencesManager.AudioBeaconsEnabled,
                    () => FFV_ScreenReaderMod.Instance?.ToggleAudioBeacons(),
                    "Pings the selected entity or waypoint, panned toward it and faster as you get closer. A lower pitch means there is no path. While on, backslash and P restart the beacon instead of reading directions."),
                new ToggleItem("Landing Pings",
                    () => PreferencesManager.LandingPingsEnabled,
                    () => FFV_ScreenReaderMod.Instance?.ToggleLandingPings(),
                    "While riding a vehicle, pings the directions in which you can land or get off."),
                new ToggleItem("Beacon Destination Announcement",
                    () => PreferencesManager.AnnounceOnBeaconRestartEnabled,
                    FFV_ScreenReaderMod.ToggleAnnounceOnBeaconRestart,
                    "When you restart the audio beacon, also reads the directions to its destination."),
                new ToggleItem("Menu Position Announcements",
                    () => PreferencesManager.MenuPositionAnnouncementsEnabled,
                    FFV_ScreenReaderMod.ToggleMenuPositionAnnouncements,
                    "Adds the position in the list to menu entries, for example 3 of 12."),
                new ToggleItem("Auto Detail",
                    () => PreferencesManager.AutoDetailEnabled,
                    FFV_ScreenReaderMod.ToggleAutoDetail,
                    "Reads descriptions automatically as you move through items, abilities, equipment and shops. When off, press I for details."),

                // Volume Controls section
                new SectionHeader("Volume Controls"),
                new VolumeItem("Wall Bump Volume",
                    () => PreferencesManager.WallBumpVolume,
                    PreferencesManager.SetWallBumpVolume,
                    "Volume of the sound played when you walk into a wall."),
                new VolumeItem("Footstep Volume",
                    () => PreferencesManager.FootstepVolume,
                    PreferencesManager.SetFootstepVolume,
                    "Volume of the footstep clicks."),
                new VolumeItem("Wall Tone Volume",
                    () => PreferencesManager.WallToneVolume,
                    PreferencesManager.SetWallToneVolume,
                    "Volume of the wall tones."),
                new VolumeItem("Beacon Volume",
                    () => PreferencesManager.BeaconVolume,
                    PreferencesManager.SetBeaconVolume,
                    "Volume of the audio beacon pings."),
                new VolumeItem("Landing Ping Volume",
                    () => PreferencesManager.LandingPingVolume,
                    PreferencesManager.SetLandingPingVolume,
                    "Volume of the landing pings."),

                // Navigation Filters section
                new SectionHeader("Navigation Filters"),
                new ToggleItem("Pathfinding Filter",
                    () => FFV_ScreenReaderMod.PathfindingFilterEnabled,
                    () => FFV_ScreenReaderMod.Instance?.TogglePathfindingFilter(),
                    "Entity cycling skips anything you cannot currently walk to."),
                new ToggleItem("Map Exit Filter",
                    () => FFV_ScreenReaderMod.MapExitFilterEnabled,
                    () => FFV_ScreenReaderMod.Instance?.ToggleMapExitFilter(),
                    "Exits that lead to the same place are merged into the closest one."),
                new ToggleItem("Layer Transition Filter",
                    () => FFV_ScreenReaderMod.ToLayerFilterEnabled,
                    () => FFV_ScreenReaderMod.Instance?.ToggleToLayerFilter(),
                    "Hides layer transitions, the spots that move you between levels of the same map, from entity cycling."),

                // Controller Settings section
                new SectionHeader("Controller Settings"),
                new ToggleItem("Stick Click Normalization",
                    () => PreferencesManager.StickClickNormalizationEnabled,
                    FFV_ScreenReaderMod.ToggleStickClickNormalization,
                    "When on, stick clicks go to the game for walk or run and encounters, and their mod functions move to mod mode. When off, the right stick click toggles the pathfinding filter and the left stick click toggles audio beacons."),

                // Battle Results section
                new SectionHeader("Battle Results"),
                new ToggleItem("EXP Counter Sound",
                    () => PreferencesManager.ExpCounterEnabled,
                    FFV_ScreenReaderMod.ToggleExpCounter,
                    "Plays a ticking sound while experience counts up on the battle results screen."),
                new VolumeItem("EXP Counter Volume",
                    () => PreferencesManager.ExpCounterVolume,
                    PreferencesManager.SetExpCounterVolume,
                    "Volume of the experience counter ticking."),

                // Battle Settings section
                new SectionHeader("Battle Settings"),
                new EnumItem("Enemy HP Display",
                    new[] { "Numbers", "Percentage", "Hidden" },
                    () => PreferencesManager.EnemyHPDisplay,
                    PreferencesManager.SetEnemyHPDisplay,
                    "How enemy HP is read when you target an enemy: as numbers, as a percentage, or not at all."),
                new ToggleItem("Enemy Letters",
                    () => PreferencesManager.EnemyLettersEnabled,
                    FFV_ScreenReaderMod.ToggleEnemyLetters,
                    "Adds a letter such as A or B to enemies that share a name, so you can tell them apart."),
                new EnumItem("Multi-hit Damage",
                    new[] { "Total only", "With hit count" },
                    () => PreferencesManager.DamageDisplay,
                    PreferencesManager.SetDamageDisplay,
                    "For attacks that hit several times, reads the total damage alone or together with the number of hits."),

                // Close Menu action
                new ActionItem("Close Menu", Close,
                    "Closes the mod menu and returns to the game.")
            };
        }

        /// <summary>
        /// Opens the mod menu.
        /// </summary>
        public static void Open()
        {
            if (IsOpen) return;

            IsOpen = true;
            currentIndex = 0;

            // Skip section header at index 0
            if (items != null && items.Count > 1 && items[0] is SectionHeader)
                currentIndex = 1;

            // Announce that the menu opened (both F8 and the controller Start button reach here),
            // then the first item after a short delay. The menu is virtual — game input is
            // suppressed via ControllerRouter.SuppressGameInput + InputPassthroughPatches (no
            // window stealing), so we speak the title ourselves instead of relying on NVDA.
            FFV_ScreenReaderMod.SpeakText(T("Mod menu"), interrupt: true);
            CoroutineManager.StartManaged(AnnounceFirstItemDelayed());
        }

        private static IEnumerator AnnounceFirstItemDelayed()
        {
            // Wait 2 frames for TTS to queue "Mod menu" before adding first item
            yield return null;
            yield return null;

            if (IsOpen) // Still open after delay
            {
                AnnounceCurrentItem(interrupt: false);
            }
        }

        /// <summary>
        /// Closes the mod menu.
        /// </summary>
        public static void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            // Announce on every close path (keyboard Escape/F8, "Close Menu" item, controller B/Start).
            // Game input is restored automatically — ControllerRouter.SuppressGameInput becomes false.
            FFV_ScreenReaderMod.SpeakText(T("Mod menu closed"), interrupt: true);
        }

        /// <summary>
        /// Handles input when the mod menu is open. Reads keys via GamepadManager
        /// (SDL3 + GetAsyncKeyState — hardware state); game input is suppressed by
        /// InputPassthroughPatches + Input.ResetInputAxes while open. No window focus stealing.
        /// Returns true if input was consumed (menu is open).
        /// </summary>
        public static bool HandleInput()
        {
            if (!IsOpen) return false;
            if (items == null || items.Count == 0) return false;

            // Escape or F8 to close
            if (GamepadManager.IsKeyCodePressed(KeyCode.Escape) || GamepadManager.IsKeyCodePressed(KeyCode.F8))
            {
                Close();
                return true;
            }

            // Up arrow - navigate to previous item
            if (GamepadManager.IsKeyCodePressed(KeyCode.UpArrow))
            {
                NavigatePrevious();
                return true;
            }

            // Down arrow - navigate to next item
            if (GamepadManager.IsKeyCodePressed(KeyCode.DownArrow))
            {
                NavigateNext();
                return true;
            }

            // Left arrow - decrease value
            if (GamepadManager.IsKeyCodePressed(KeyCode.LeftArrow))
            {
                AdjustCurrentItem(-1);
                return true;
            }

            // Right arrow - increase value
            if (GamepadManager.IsKeyCodePressed(KeyCode.RightArrow))
            {
                AdjustCurrentItem(1);
                return true;
            }

            // Enter or Space - toggle/activate
            if (GamepadManager.IsKeyCodePressed(KeyCode.Return) || GamepadManager.IsKeyCodePressed(KeyCode.Space))
            {
                ToggleCurrentItem();
                return true;
            }

            // I - what the focused setting does
            if (GamepadManager.IsKeyCodePressed(KeyCode.I))
            {
                AnnounceCurrentItemDescription();
                return true;
            }

            return true; // Consume all input while menu is open
        }

        /// <summary>Reads the focused setting's description. Bound to I and right stick up.</summary>
        public static void AnnounceCurrentItemDescription()
        {
            if (items == null || currentIndex < 0 || currentIndex >= items.Count) return;

            string desc = items[currentIndex].Description;
            FFV_ScreenReaderMod.SpeakText(
                string.IsNullOrWhiteSpace(desc) ? T("No description available") : desc,
                interrupt: true);
        }

        public static void NavigateNext()
        {
            int startIndex = currentIndex;
            do
            {
                currentIndex++;
                if (currentIndex >= items.Count)
                    currentIndex = 0;

                // Skip section headers
                if (!(items[currentIndex] is SectionHeader))
                    break;

            } while (currentIndex != startIndex);

            AnnounceCurrentItem();
        }

        public static void NavigatePrevious()
        {
            int startIndex = currentIndex;
            do
            {
                currentIndex--;
                if (currentIndex < 0)
                    currentIndex = items.Count - 1;

                // Skip section headers
                if (!(items[currentIndex] is SectionHeader))
                    break;

            } while (currentIndex != startIndex);

            AnnounceCurrentItem();
        }

        public static void AdjustCurrentItem(int delta)
        {
            if (currentIndex < 0 || currentIndex >= items.Count) return;

            var item = items[currentIndex];
            if (item is SectionHeader) return;

            item.Adjust(delta);
            AnnounceCurrentItem();
        }

        public static void ToggleCurrentItem()
        {
            if (currentIndex < 0 || currentIndex >= items.Count) return;

            var item = items[currentIndex];
            if (item is SectionHeader) return;

            item.Toggle();

            // For action items (like Close Menu), don't re-announce
            if (item is ActionItem) return;

            AnnounceCurrentItem();
        }

        private static void AnnounceCurrentItem(bool interrupt = true)
        {
            if (currentIndex < 0 || currentIndex >= items.Count) return;

            var item = items[currentIndex];
            string value = item.GetValueString();

            string announcement;
            if (string.IsNullOrEmpty(value))
            {
                announcement = item.Name;
            }
            else
            {
                announcement = $"{item.Name}: {value}";
            }

            var (index, count) = NavigablePosition();
            announcement = MenuPosition.Format(announcement, index, count);

            FFV_ScreenReaderMod.SpeakText(announcement, interrupt: interrupt);
        }

        /// <summary>
        /// Position of the current item among the navigable (non-header) items. Headers are skipped
        /// during navigation, so the player hears "(N of total settings)" without counting them.
        /// </summary>
        private static (int index, int count) NavigablePosition()
        {
            int count = 0, index = -1;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is SectionHeader) continue;
                if (i == currentIndex) index = count;
                count++;
            }
            return (index, count);
        }
    }
}
