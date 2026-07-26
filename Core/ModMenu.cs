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
            public string Name { get; protected set; }
            public abstract string GetValueString();
            public abstract void Adjust(int delta);
            public abstract void Toggle();
        }

        private class ToggleItem : MenuItem
        {
            private readonly Func<bool> getter;
            private readonly Action toggle;

            public ToggleItem(string name, Func<bool> getter, Action toggle)
            {
                Name = name;
                this.getter = getter;
                this.toggle = toggle;
            }

            public override string GetValueString() => getter() ? T("On") : T("Off");
            public override void Adjust(int delta) => toggle();
            public override void Toggle() => toggle();
        }

        private class VolumeItem : MenuItem
        {
            private readonly Func<int> getter;
            private readonly Action<int> setter;

            public VolumeItem(string name, Func<int> getter, Action<int> setter)
            {
                Name = name;
                this.getter = getter;
                this.setter = setter;
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

            public EnumItem(string name, string[] options, Func<int> getter, Action<int> setter)
            {
                Name = name;
                this.options = options;
                this.getter = getter;
                this.setter = setter;
            }

            public override string GetValueString()
            {
                int index = getter();
                if (index >= 0 && index < options.Length)
                    return options[index];
                return "Unknown";
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
                Name = name;
            }

            public override string GetValueString() => "";
            public override void Adjust(int delta) { }
            public override void Toggle() { }
        }

        private class ActionItem : MenuItem
        {
            private readonly Action action;

            public ActionItem(string name, Action action)
            {
                Name = name;
                this.action = action;
            }

            public override string GetValueString() => "";
            public override void Adjust(int delta) => action();
            public override void Toggle() => action();
        }

        #endregion

        /// <summary>
        /// Initializes the mod menu with all menu items.
        /// Call this once during mod initialization.
        /// </summary>
        public static void Initialize()
        {
            items = new List<MenuItem>
            {
                // Audio Feedback section
                new SectionHeader(T("Audio Feedback")),
                new ToggleItem(T("Wall Tones"),
                    () => PreferencesManager.WallTonesEnabled,
                    () => FFV_ScreenReaderMod.Instance?.ToggleWallTones()),
                new ToggleItem(T("Footsteps"),
                    () => PreferencesManager.FootstepsEnabled,
                    () => FFV_ScreenReaderMod.Instance?.ToggleFootsteps()),
                new ToggleItem(T("Audio Beacons"),
                    () => PreferencesManager.AudioBeaconsEnabled,
                    () => FFV_ScreenReaderMod.Instance?.ToggleAudioBeacons()),
                new ToggleItem(T("Landing Pings"),
                    () => PreferencesManager.LandingPingsEnabled,
                    () => FFV_ScreenReaderMod.Instance?.ToggleLandingPings()),
                new ToggleItem(T("Beacon Destination Announcement"),
                    () => PreferencesManager.AnnounceOnBeaconRestartEnabled,
                    FFV_ScreenReaderMod.ToggleAnnounceOnBeaconRestart),
                new ToggleItem(T("Menu Position Announcements"),
                    () => PreferencesManager.MenuPositionAnnouncementsEnabled,
                    FFV_ScreenReaderMod.ToggleMenuPositionAnnouncements),
                new ToggleItem(T("Auto Detail"),
                    () => PreferencesManager.AutoDetailEnabled,
                    FFV_ScreenReaderMod.ToggleAutoDetail),

                // Volume Controls section
                new SectionHeader(T("Volume Controls")),
                new VolumeItem(T("Wall Bump Volume"),
                    () => PreferencesManager.WallBumpVolume,
                    PreferencesManager.SetWallBumpVolume),
                new VolumeItem(T("Footstep Volume"),
                    () => PreferencesManager.FootstepVolume,
                    PreferencesManager.SetFootstepVolume),
                new VolumeItem(T("Wall Tone Volume"),
                    () => PreferencesManager.WallToneVolume,
                    PreferencesManager.SetWallToneVolume),
                new VolumeItem(T("Beacon Volume"),
                    () => PreferencesManager.BeaconVolume,
                    PreferencesManager.SetBeaconVolume),
                new VolumeItem(T("Landing Ping Volume"),
                    () => PreferencesManager.LandingPingVolume,
                    PreferencesManager.SetLandingPingVolume),

                // Navigation Filters section
                new SectionHeader(T("Navigation Filters")),
                new ToggleItem(T("Pathfinding Filter"),
                    () => FFV_ScreenReaderMod.PathfindingFilterEnabled,
                    () => FFV_ScreenReaderMod.Instance?.TogglePathfindingFilter()),
                new ToggleItem(T("Map Exit Filter"),
                    () => FFV_ScreenReaderMod.MapExitFilterEnabled,
                    () => FFV_ScreenReaderMod.Instance?.ToggleMapExitFilter()),
                new ToggleItem(T("Layer Transition Filter"),
                    () => FFV_ScreenReaderMod.ToLayerFilterEnabled,
                    () => FFV_ScreenReaderMod.Instance?.ToggleToLayerFilter()),

                // Controller Settings section
                new SectionHeader(T("Controller Settings")),
                new ToggleItem(T("Stick Click Normalization"),
                    () => PreferencesManager.StickClickNormalizationEnabled,
                    FFV_ScreenReaderMod.ToggleStickClickNormalization),

                // Battle Results section
                new SectionHeader(T("Battle Results")),
                new ToggleItem(T("EXP Counter Sound"),
                    () => PreferencesManager.ExpCounterEnabled,
                    FFV_ScreenReaderMod.ToggleExpCounter),
                new VolumeItem(T("EXP Counter Volume"),
                    () => PreferencesManager.ExpCounterVolume,
                    PreferencesManager.SetExpCounterVolume),

                // Battle Settings section
                new SectionHeader(T("Battle Settings")),
                new EnumItem(T("Enemy HP Display"),
                    new[] { T("Numbers"), T("Percentage"), T("Hidden") },
                    () => PreferencesManager.EnemyHPDisplay,
                    PreferencesManager.SetEnemyHPDisplay),
                new ToggleItem(T("Enemy Letters"),
                    () => PreferencesManager.EnemyLettersEnabled,
                    FFV_ScreenReaderMod.ToggleEnemyLetters),
                new EnumItem(T("Multi-hit Damage"),
                    new[] { T("Total only"), T("With hit count") },
                    () => PreferencesManager.DamageDisplay,
                    PreferencesManager.SetDamageDisplay),

                // Close Menu action
                new ActionItem(T("Close Menu"), Close)
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

            return true; // Consume all input while menu is open
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

            FFV_ScreenReaderMod.SpeakText(announcement, interrupt: interrupt);
        }
    }
}
