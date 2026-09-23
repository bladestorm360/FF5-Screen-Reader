using System;
using System.Collections.Generic;
using Il2CppLast.UI.KeyInput;
using UnityEngine;
using UnityEngine.UI;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;
using static FFV_ScreenReader.Utils.ModTextTranslator;

namespace FFV_ScreenReader.Menus
{
    /// <summary>
    /// Reads the controls display. Two independent features:
    ///   • Shift+I — reads the on-screen control-hint bar (the persistent KeyHelpController) at once
    ///     via GameObjectCache + transform navigation (AnnounceKeyHelp). Works on any screen.
    ///   • Arrows/WASD — step the config menu's "Gamepad/Keyboard Controls" list one entry at a time,
    ///     gated to KeyContext.KeyHelp.
    ///
    /// The list is armed ONLY from ConfigKeysSettingController's GamePad/Keyboard Help state
    /// (ConfigMenuPatches hands over the pre-rendered entries via <see cref="OpenControlsHelp"/>),
    /// never from the hint bar: the title-screen Options menu hosts a hint bar too, and arming off it
    /// would flip that menu's arrows into KeyContext.KeyHelp.
    /// </summary>
    public static class KeyHelpReader
    {
        // KeyHelpController.view (KeyHelpView) — private field, no public accessor
        private const int OFFSET_VIEW = 0x18;

        // Controls-list state. helpOwner validates the screen is still up (cheap, safe per frame —
        // no scene scan) so a missed close can never leave KeyContext.KeyHelp stuck.
        private static List<string> entries;
        private static int index;
        private static ConfigKeysSettingController helpOwner;

        /// <summary>
        /// Called by ConfigMenuPatches when the Gamepad/Keyboard Controls list opens, with each row
        /// already rendered as "action (binding)". Announces the first entry as the initial focus.
        /// </summary>
        public static void OpenControlsHelp(ConfigKeysSettingController owner, List<string> rendered)
        {
            if (owner == null || rendered == null || rendered.Count == 0)
            {
                CloseControlsHelp();
                return;
            }

            helpOwner = owner;
            entries = rendered;
            index = 0;
            SpeakCurrent();
        }

        /// <summary>Called when the list closes or returns to the controls select state.</summary>
        public static void CloseControlsHelp()
        {
            helpOwner = null;
            entries = null;
            index = 0;
        }

        /// <summary>True while the controls list is on screen — drives KeyContext.KeyHelp.</summary>
        public static bool IsScreenActive
        {
            get
            {
                if (entries == null) return false;
                try
                {
                    if (helpOwner != null && helpOwner.gameObject != null && helpOwner.gameObject.activeInHierarchy)
                        return true;
                }
                catch { }
                CloseControlsHelp();
                return false;
            }
        }

        // ── Navigation (arrows + WASD, gated to KeyContext.KeyHelp) ──
        public static void NavigateNext()
        {
            if (entries == null) return;
            index = (index + 1) % entries.Count;
            SpeakCurrent();
        }

        public static void NavigatePrevious()
        {
            if (entries == null) return;
            index = (index - 1 + entries.Count) % entries.Count;
            SpeakCurrent();
        }

        public static void JumpToTop()
        {
            if (entries == null) return;
            index = 0;
            SpeakCurrent();
        }

        public static void JumpToBottom()
        {
            if (entries == null) return;
            index = entries.Count - 1;
            SpeakCurrent();
        }

        private static void SpeakCurrent()
        {
            FFV_ScreenReaderMod.SpeakText(MenuPosition.Format(entries[index], index, entries.Count), interrupt: true);
        }

        /// <summary>
        /// Shift+I — reads all visible key help controls and speaks them.
        /// </summary>
        public static void AnnounceKeyHelp()
        {
            try
            {
                string result = ReadVisibleKeyHelp();
                FFV_ScreenReaderMod.SpeakText(result, interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Error in AnnounceKeyHelp: {ex.Message}");
                FFV_ScreenReaderMod.SpeakText(T("Error reading controls"), interrupt: true);
            }
        }

        /// <summary>
        /// Gets the active KeyHelpController via GameObjectCache (single instance, no Cast-based
        /// array indexer), navigates to its ContentsParent via the private view field, then reads
        /// all visible control entries using GetComponentsInChildren&lt;Text&gt;().
        /// </summary>
        private static unsafe string ReadVisibleKeyHelp()
        {
            // Get KeyHelpController via GameObjectCache (same pattern as AnnounceConfigTooltip)
            var controller = GameObjectCache.Get<KeyHelpController>();
            if (controller == null)
                controller = GameObjectCache.Refresh<KeyHelpController>();

            if (controller == null || controller.gameObject == null || !controller.gameObject.activeInHierarchy)
                return T("No controls displayed");

            // Read private 'view' field (KeyHelpView) at offset 0x18 via unsafe pointer
            IntPtr controllerPtr = controller.Pointer;
            IntPtr viewPtr = *(IntPtr*)((byte*)controllerPtr.ToPointer() + OFFSET_VIEW);
            if (viewPtr == IntPtr.Zero)
                return T("No controls displayed");

            var view = new KeyHelpView(viewPtr);

            // Get ContentsParent via public property
            var contentsParent = view.ContentsParent;
            if (contentsParent == null)
                return T("No controls displayed");

            var contentsTransform = contentsParent.transform;
            if (contentsTransform == null || contentsTransform.childCount == 0)
                return T("No controls displayed");

            var visibleEntries = new List<string>();

            // Iterate children of ContentsParent — each is a control entry (KeyIconController).
            // The game deactivates entries on other pages, so activeInHierarchy filters to
            // the visible page only.
            for (int i = 0; i < contentsTransform.childCount; i++)
            {
                var child = contentsTransform.GetChild(i);
                if (child == null || child.gameObject == null || !child.gameObject.activeInHierarchy)
                    continue;

                // Get all Text components within this entry (same pattern as KeyboardGamepadReader)
                var texts = child.GetComponentsInChildren<Text>(false);
                if (texts == null)
                    continue;

                var parts = new List<string>();
                foreach (var txt in texts)
                {
                    if (txt != null && !string.IsNullOrWhiteSpace(txt.text))
                        parts.Add(txt.text.Trim());
                }

                if (parts.Count > 0)
                    visibleEntries.Add(string.Join(": ", parts));
            }

            if (visibleEntries.Count == 0)
                return T("No controls displayed");

            return string.Join(", ", visibleEntries);
        }
    }
}
