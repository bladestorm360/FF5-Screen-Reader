using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using Il2CppLast.UI.KeyInput;
using Il2CppLast.Data.Master;

namespace FFV_ScreenReader.Menus
{
    /// <summary>
    /// Reads music player (Extra Sound) data and formats speech output.
    /// Uses unsafe pointer access because IL2CppInterop property accessors
    /// on ExtraSoundListContentController/Info throw OutOfMemoryException.
    /// </summary>
    public static class MusicPlayerReader
    {
        // ExtraSoundListContentController field offsets
        private const int OFFSET_CONTENT_INFO = 0x30;  // <ContentInfo>k__BackingField
        private const int OFFSET_INDEX = 0x38;          // <Index>k__BackingField

        // ExtraSoundListContentInfo field offsets
        private const int OFFSET_MUSIC_NAME = 0x10;     // musicName (Il2CppString*)
        private const int OFFSET_BGM_ID = 0x18;         // bgmId (int)

        // ExtraSoundController field offset (KeyInput namespace, TypeDefIndex 9349)
        private const int OFFSET_PLAYER_LIST = 0x50;    // <PlayerList>k__BackingField

        /// <summary>
        /// Read song data from an ExtraSoundListContentController pointer using unsafe field access.
        /// Returns false if any pointer is zero/null.
        /// </summary>
        public static unsafe bool ReadContentFromPointer(IntPtr contentControllerPtr, out string musicName, out int bgmId, out int index)
        {
            musicName = null;
            bgmId = 0;
            index = 0;

            if (contentControllerPtr == IntPtr.Zero)
                return false;

            // Read Index backing field at +0x38
            index = *(int*)((byte*)contentControllerPtr.ToPointer() + OFFSET_INDEX);

            // Read ContentInfo backing field at +0x30
            IntPtr contentInfoPtr = *(IntPtr*)((byte*)contentControllerPtr.ToPointer() + OFFSET_CONTENT_INFO);
            if (contentInfoPtr == IntPtr.Zero)
                return false;

            // Read bgmId (int) at +0x18 from ContentInfo
            bgmId = *(int*)((byte*)contentInfoPtr.ToPointer() + OFFSET_BGM_ID);

            // Read musicName (Il2CppString*) at +0x10 from ContentInfo
            IntPtr musicNamePtr = *(IntPtr*)((byte*)contentInfoPtr.ToPointer() + OFFSET_MUSIC_NAME);
            if (musicNamePtr == IntPtr.Zero)
                return false;

            musicName = IL2CPP.Il2CppStringToManaged(musicNamePtr);
            return !string.IsNullOrEmpty(musicName);
        }

        /// <summary>
        /// Format a song entry announcement: "01: Main Theme of Final Fantasy V, 1:30"
        /// Takes pre-extracted C# values (not IL2CPP references).
        /// </summary>
        public static string ReadSongEntry(string musicName, int bgmId, int index)
        {
            if (string.IsNullOrEmpty(musicName)) return null;

            string number = (index + 1).ToString("D2");
            int durationSec = LookupDuration(bgmId);
            string duration = FormatPlayTime(durationSec);
            return $"{number}: {musicName}, {duration}";
        }

        /// <summary>
        /// Look up song duration from SoundPlayerList master data via ExtraSoundController.PlayerList.
        /// </summary>
        public static int LookupDuration(int bgmId)
        {
            try
            {
                var controller = UnityEngine.Object.FindObjectOfType<ExtraSoundController>();
                if (controller == null) return 0;

                unsafe
                {
                    IntPtr controllerPtr = controller.Pointer;
                    if (controllerPtr == IntPtr.Zero) return 0;

                    IntPtr playerListPtr = *(IntPtr*)((byte*)controllerPtr.ToPointer() + OFFSET_PLAYER_LIST);
                    if (playerListPtr == IntPtr.Zero) return 0;

                    var playerListObj = new Il2CppSystem.Object(playerListPtr);
                    var playerDict = playerListObj.TryCast<Il2CppSystem.Collections.Generic.Dictionary<int, SoundPlayerList>>();
                    if (playerDict == null) return 0;

                    if (!playerDict.ContainsKey(bgmId))
                        return 0;

                    var entry = playerDict[bgmId];
                    if (entry == null) return 0;

                    return entry.Time;
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Format seconds as "M:SS" duration string.
        /// </summary>
        public static string FormatPlayTime(int totalSeconds)
        {
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes}:{seconds:D2}";
        }
    }
}
