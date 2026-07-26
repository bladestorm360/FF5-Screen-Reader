using System;
using System.Collections;
using System.Collections.Generic;
using FFV_ScreenReader.Field;
using FFV_ScreenReader.Patches;
using FFV_ScreenReader.Utils;
using Il2CppLast.Entity.Field;
using Il2CppLast.Map;
using Il2CppLast.Management;
using MelonLoader;
using UnityEngine;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Manages all audio loop coroutines (wall tones, beacons, landing pings),
    /// their enable/disable toggles, and battle/dialogue suppression state.
    /// Extracted from FFV_ScreenReaderMod to reduce god class size.
    /// </summary>
    public class AudioLoopManager
    {
        /// <summary>
        /// Singleton instance. Set during initialization.
        /// </summary>
        public static AudioLoopManager Instance { get; private set; }

        private readonly EntityCache entityCache;
        private readonly EntityNavigator entityNavigator;
        private WaypointNavigator waypointNavigator;

        // Transient battle/dialogue suppression gate. NON-persisted — flipped by the
        // suppress/restore machinery, checked inside each loop's stop-gate. The ENABLED
        // state is the single source of truth in PreferencesManager (WallTonesEnabled, etc.);
        // this gate only silences the loops temporarily, it never changes the saved toggle.
        private static bool suppressed = false;

        /// <summary>
        /// Single source of truth for "all mod audio feedback must be silent right now".
        /// Covers battle, cutscene/event state, NPC dialogue, transient suppression,
        /// in-game menus and off-field contexts (via IsFieldActive), mod overlays
        /// (mod mode/menu, text input, confirmation, battle results), and screen fades.
        ///
        /// Used by the three audio loops plus the two one-shot movement sounds, so every
        /// feature silences under exactly the same conditions. Loop-specific gates
        /// (map-change and vehicle-transition suppression windows) stay in their loops.
        /// </summary>
        internal static bool IsAudioSuppressed =>
            BattleState.IsInBattle
            || GameStatePatches.IsInEventState
            || DialogueTracker.IsInDialogue
            || suppressed
            || !ControllerRouter.IsFieldActive
            || ControllerRouter.SuppressGameInput
            || GameStatePatches.IsScreenFading;

        // Beacon navigation constants — proximity-based interval modulation.
        // Mode A (valid path): 1.0s at 31.5 tiles (pathfinding limit) → 0.2s at 2 tiles; silent at ≤1 tile.
        // Mode B (no valid path / out of range): 1.0s at ≥100 tiles → 0.5s at 32 tiles; halved pitch.
        private const float MODE_A_INTERVAL_FAR  = 1.0f;
        private const float MODE_A_INTERVAL_NEAR = 0.2f;
        private const float MODE_A_FAR_TILES     = 31.5f;
        private const float MODE_A_NEAR_TILES    = 2.0f;
        private const float BEACON_STOP_TILES    = 1.0f;
        private const float MODE_B_INTERVAL_FAR  = 1.0f;
        private const float MODE_B_INTERVAL_NEAR = 0.5f;
        private const float MODE_B_FAR_TILES     = 100f;
        private const float MODE_B_NEAR_TILES    = 32f;
        private const float TILE_SIZE            = 16f;

        // Beacon state (proximity-modulated, mode-aware)
        private bool beaconSilenced = false;
        private object lastBeaconTarget = null;
        private float nextBeaconTime = 0f;

        // Coroutine-based audio loops
        private IEnumerator wallToneCoroutine = null;
        private IEnumerator beaconCoroutine = null;
        private IEnumerator landingPingCoroutine = null;

        // Map transition suppression for wall tones
        private int wallToneMapId = -1;
        private float wallToneSuppressedUntil = 0f;

        // Vehicle transition suppression for wall tones and landing pings
        private static float vehicleTransitionSuppressedUntil = 0f;

        // Beacon suppression after scene load
        private float beaconSuppressedUntil = 0f;

        // Reusable direction list buffer to avoid per-cycle allocations
        private static readonly List<SoundPlayer.Direction> wallDirectionsBuffer = new List<SoundPlayer.Direction>(4);
        private static readonly List<SoundPlayer.Direction> landingDirectionsBuffer = new List<SoundPlayer.Direction>(4);

        // Map transition suppression for landing pings
        private int landingPingMapId = -1;
        private float landingPingSuppressedUntil = 0f;

        // Beacon debouncing tracker
        private float lastBeaconPlayedAt = 0f;

        public AudioLoopManager(EntityCache entityCache, EntityNavigator entityNavigator)
        {
            this.entityCache = entityCache;
            this.entityNavigator = entityNavigator;
            Instance = this;
        }

        /// <summary>
        /// Allows the WaypointNavigator to be wired up after construction — the navigator
        /// is created after the AudioLoopManager during mod initialization.
        /// </summary>
        public void SetWaypointNavigator(WaypointNavigator navigator)
        {
            this.waypointNavigator = navigator;
        }

        /// <summary>
        /// Forces the beacon to ping on the next loop iteration and clears any silence latch.
        /// Called by the pathfinding commands when beacon navigation mode is on.
        /// </summary>
        public void RestartBeacon()
        {
            beaconSilenced = false;
            nextBeaconTime = 0f;
        }

        /// <summary>
        /// Initializes toggles from saved preferences and starts loops if enabled.
        /// Call after PreferencesManager.Initialize().
        /// </summary>
        public void InitializeFromPreferences()
        {
            if (PreferencesManager.WallTonesEnabled) StartWallToneLoop();
            if (PreferencesManager.AudioBeaconsEnabled) StartBeaconLoop();
            if (PreferencesManager.LandingPingsEnabled) StartLandingPingLoop();
        }

        #region Public Toggle Accessors (read the single source of truth)

        public bool IsWallTonesEnabled => PreferencesManager.WallTonesEnabled;
        public bool IsFootstepsEnabled => PreferencesManager.FootstepsEnabled;
        public bool IsAudioBeaconsEnabled => PreferencesManager.AudioBeaconsEnabled;
        public bool IsLandingPingsEnabled => PreferencesManager.LandingPingsEnabled;

        #endregion

        #region Toggle Methods

        // Ordering: persist the preference FIRST (it is the single source of truth the loop's
        // while-condition reads), THEN start/stop the coroutine.

        public void ToggleWallTones()
        {
            bool newValue = !PreferencesManager.WallTonesEnabled;
            PreferencesManager.SaveWallTones(newValue);

            if (newValue)
                StartWallToneLoop();
            else
                StopWallToneLoop();

            FFV_ScreenReaderMod.SpeakText($"Wall tones {(newValue ? "on" : "off")}");
        }

        public void ToggleFootsteps()
        {
            bool newValue = !PreferencesManager.FootstepsEnabled;
            PreferencesManager.SaveFootsteps(newValue);

            FFV_ScreenReaderMod.SpeakText($"Footsteps {(newValue ? "on" : "off")}");
        }

        public void ToggleAudioBeacons()
        {
            bool newValue = !PreferencesManager.AudioBeaconsEnabled;
            PreferencesManager.SaveAudioBeacons(newValue);

            if (newValue)
                StartBeaconLoop();
            else
                StopBeaconLoop();

            FFV_ScreenReaderMod.SpeakText($"Audio beacons {(newValue ? "on" : "off")}");
        }

        public void ToggleLandingPings()
        {
            bool newValue = !PreferencesManager.LandingPingsEnabled;
            PreferencesManager.SaveLandingPings(newValue);

            if (newValue)
                StartLandingPingLoop();
            else
                StopLandingPingLoop();

            FFV_ScreenReaderMod.SpeakText($"Landing pings {(newValue ? "on" : "off")}");
        }

        #endregion

        #region Start/Stop Loop Methods

        private void StartWallToneLoop()
        {
            if (!PreferencesManager.WallTonesEnabled) return;
            if (wallToneCoroutine != null) return;
            wallToneCoroutine = WallToneLoop();
            CoroutineManager.StartManaged(wallToneCoroutine);
        }

        private void StopWallToneLoop()
        {
            if (wallToneCoroutine != null)
            {
                CoroutineManager.StopManaged(wallToneCoroutine);
                wallToneCoroutine = null;
            }
            if (SoundPlayer.IsWallTonePlaying())
                SoundPlayer.StopWallTone();
        }

        private void StartBeaconLoop()
        {
            if (!PreferencesManager.AudioBeaconsEnabled) return;
            if (beaconCoroutine != null) return;
            beaconCoroutine = BeaconLoop();
            CoroutineManager.StartManaged(beaconCoroutine);
        }

        private void StopBeaconLoop()
        {
            if (beaconCoroutine != null)
            {
                CoroutineManager.StopManaged(beaconCoroutine);
                beaconCoroutine = null;
            }
            beaconSilenced = false;
            lastBeaconTarget = null;
        }

        private void StartLandingPingLoop()
        {
            if (!PreferencesManager.LandingPingsEnabled) return;
            if (landingPingCoroutine != null) return;
            landingPingCoroutine = LandingPingLoop();
            CoroutineManager.StartManaged(landingPingCoroutine);
        }

        private void StopLandingPingLoop()
        {
            if (landingPingCoroutine != null)
            {
                CoroutineManager.StopManaged(landingPingCoroutine);
                landingPingCoroutine = null;
            }
            if (SoundPlayer.IsLandingPingPlaying())
                SoundPlayer.StopLandingPing();
        }

        /// <summary>
        /// Stops all audio loops. Called during shutdown.
        /// </summary>
        public void StopAllLoops()
        {
            StopWallToneLoop();
            StopBeaconLoop();
            StopLandingPingLoop();
        }

        /// <summary>
        /// Restarts loops that are currently enabled. Called when re-enabling accessibility
        /// to resume loops that were stopped by the toggle. Enable flags persist through
        /// the disable/enable cycle since we only stop coroutines, not clear flags.
        /// </summary>
        public void RestartEnabledLoops()
        {
            if (PreferencesManager.WallTonesEnabled) StartWallToneLoop();
            if (PreferencesManager.AudioBeaconsEnabled) StartBeaconLoop();
            if (PreferencesManager.LandingPingsEnabled) StartLandingPingLoop();
        }

        #endregion

        #region Coroutines

        /// <summary>
        /// Coroutine loop that plays proximity-based audio beacon pings.
        /// Interval shortens as the player nears the selected target.
        /// Mode A (valid path): normal pitch, 1.0s→0.2s over 31.5→2 tiles, silent at ≤1 tile.
        /// Mode B (no valid path): halved pitch, 1.0s→0.5s over 100→32 tiles, no silence latch.
        /// Target is whichever the player last selected (entity OR waypoint), via NavigationTargetTracker.
        /// </summary>
        private IEnumerator BeaconLoop()
        {
            nextBeaconTime = Time.time + GameConstants.INITIAL_LOOP_DELAY;

            while (PreferencesManager.AudioBeaconsEnabled)
            {
                // Stop-gate: battle, event/cutscene, dialogue, menus, mod overlays, fades.
                // Beacon is one-shot pings — no channel to stop, just skip the tick.
                if (IsAudioSuppressed)
                {
                    yield return null;
                    continue;
                }

                if (Time.time < nextBeaconTime)
                {
                    yield return null;
                    continue;
                }

                if (Time.time < beaconSuppressedUntil)
                {
                    nextBeaconTime = Time.time + 0.1f;
                    continue;
                }

                try
                {
                    object targetRef = null;
                    Vector3 targetPos = Vector3.zero;
                    switch (NavigationTargetTracker.LastKind)
                    {
                        case NavigationTargetTracker.Kind.Entity:
                            var e = entityNavigator?.CurrentEntity;
                            if (e != null) { targetRef = e; targetPos = e.Position; }
                            break;
                        case NavigationTargetTracker.Kind.Waypoint:
                            var w = waypointNavigator?.SelectedWaypoint;
                            if (w != null) { targetRef = w; targetPos = w.Position; }
                            break;
                    }

                    if (targetRef == null)
                    {
                        nextBeaconTime = Time.time + 0.2f;
                        continue;
                    }

                    // Selection change clears the silence latch so new targets always ping.
                    if (!ReferenceEquals(targetRef, lastBeaconTarget))
                    {
                        beaconSilenced = false;
                        lastBeaconTarget = targetRef;
                    }

                    var playerController = GameObjectCache.Get<FieldPlayerController>();
                    if (playerController?.fieldPlayer == null)
                    {
                        nextBeaconTime = Time.time + 0.2f;
                        continue;
                    }

                    Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;

                    // Sanity check: skip if positions look invalid (garbage data during load)
                    if (float.IsNaN(playerPos.x) || float.IsNaN(targetPos.x) ||
                        Mathf.Abs(playerPos.x) > 10000f || Mathf.Abs(targetPos.x) > 10000f)
                    {
                        nextBeaconTime = Time.time + 0.2f;
                        continue;
                    }

                    float distTiles = Vector3.Distance(playerPos, targetPos) / TILE_SIZE;

                    // Mode selection — expensive (A* per beacon tick) but only 1–5 Hz.
                    bool pathValid;
                    try
                    {
                        var pathInfo = FieldNavigationHelper.FindPathTo(
                            playerPos, targetPos,
                            playerController.mapHandle,
                            playerController.fieldPlayer);
                        pathValid = pathInfo.Success;
                    }
                    catch
                    {
                        pathValid = false;
                    }

                    float interval;
                    bool lowPitch;
                    if (pathValid)
                    {
                        // Mode A: valid path
                        if (distTiles <= BEACON_STOP_TILES)
                        {
                            beaconSilenced = true;
                            nextBeaconTime = Time.time + 0.2f;
                            continue;
                        }
                        // Player moved back out of the stop radius — release the arrival-silence
                        // latch so the beacon resumes pinging when they walk away from a reached,
                        // still-selected target (previously it stayed silent permanently).
                        beaconSilenced = false;
                        float t = Mathf.Clamp01((distTiles - MODE_A_NEAR_TILES) /
                                                (MODE_A_FAR_TILES - MODE_A_NEAR_TILES));
                        interval = Mathf.Lerp(MODE_A_INTERVAL_NEAR, MODE_A_INTERVAL_FAR, t);
                        lowPitch = false;
                    }
                    else
                    {
                        // Mode B: out of range or blocked — halved pitch, no silence latch
                        float t = Mathf.Clamp01((distTiles - MODE_B_NEAR_TILES) /
                                                (MODE_B_FAR_TILES - MODE_B_NEAR_TILES));
                        interval = Mathf.Lerp(MODE_B_INTERVAL_NEAR, MODE_B_INTERVAL_FAR, t);
                        lowPitch = true;
                    }

                    // Silence latch only holds while the path is valid (Mode A).
                    if (beaconSilenced && pathValid)
                    {
                        nextBeaconTime = Time.time + 0.2f;
                        continue;
                    }

                    nextBeaconTime = Time.time + interval;

                    float maxDist = 500f;
                    float volumeScale = Mathf.Clamp(1f - (distTiles * TILE_SIZE / maxDist), 0.15f, 0.60f);

                    float deltaX = targetPos.x - playerPos.x;
                    float pan = Mathf.Clamp(deltaX / 100f, -1f, 1f) * 0.5f + 0.5f;

                    bool isSouth = targetPos.y < playerPos.y - 8f;

                    // Debounce: ensure at least 80% of the current interval has elapsed
                    float timeSinceLast = Time.time - lastBeaconPlayedAt;
                    if (timeSinceLast < interval * 0.8f)
                        continue;

                    SoundPlayer.PlayBeacon(isSouth, pan, volumeScale, lowPitch);
                    lastBeaconPlayedAt = Time.time;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Beacon] Error: {ex.Message}");
                    nextBeaconTime = Time.time + 0.5f;
                }
            }

            beaconCoroutine = null;
            beaconSilenced = false;
            lastBeaconTarget = null;
        }

        private IEnumerator WallToneLoop()
        {
            float nextCheckTime = Time.time + GameConstants.INITIAL_LOOP_DELAY;

            while (PreferencesManager.WallTonesEnabled)
            {
                // Stop-gate: battle, event/cutscene, dialogue, menus, mod overlays, fades.
                if (IsAudioSuppressed)
                {
                    if (SoundPlayer.IsWallTonePlaying())
                        SoundPlayer.StopWallTone();
                    yield return null;
                    continue;
                }

                if (Time.time < nextCheckTime)
                {
                    yield return null;
                    continue;
                }
                nextCheckTime = Time.time + GameConstants.WALL_TONE_INTERVAL;

                try
                {
                    float currentTime = Time.time;

                    int currentMapId = GetCurrentMapId();
                    if (currentMapId > 0 && wallToneMapId > 0 && currentMapId != wallToneMapId)
                    {
                        wallToneSuppressedUntil = currentTime + GameConstants.SCENE_LOAD_SUPPRESSION;
                        if (SoundPlayer.IsWallTonePlaying())
                            SoundPlayer.StopWallTone();
                    }
                    if (currentMapId > 0)
                        wallToneMapId = currentMapId;

                    if (currentTime < wallToneSuppressedUntil)
                    {
                        if (SoundPlayer.IsWallTonePlaying())
                            SoundPlayer.StopWallTone();
                        continue;
                    }

                    if (currentTime < vehicleTransitionSuppressedUntil)
                    {
                        if (SoundPlayer.IsWallTonePlaying())
                            SoundPlayer.StopWallTone();
                        continue;
                    }

                    if (GameStatePatches.IsScreenFading)
                    {
                        if (SoundPlayer.IsWallTonePlaying())
                            SoundPlayer.StopWallTone();
                        continue;
                    }

                    var player = GetFieldPlayer();
                    if (player == null)
                    {
                        if (SoundPlayer.IsWallTonePlaying())
                            SoundPlayer.StopWallTone();
                        continue;
                    }

                    var walls = FieldNavigationHelper.GetNearbyWallsWithDistance(player);

                    var mapExitPositions = entityCache?.GetMapExitPositions();
                    Vector3 playerPos = player.transform.localPosition;

                    wallDirectionsBuffer.Clear();

                    if (walls.NorthDist == 0 &&
                        !FieldNavigationHelper.IsDirectionNearMapExit(playerPos, GameConstants.DirNorth, mapExitPositions))
                        wallDirectionsBuffer.Add(SoundPlayer.Direction.North);

                    if (walls.SouthDist == 0 &&
                        !FieldNavigationHelper.IsDirectionNearMapExit(playerPos, GameConstants.DirSouth, mapExitPositions))
                        wallDirectionsBuffer.Add(SoundPlayer.Direction.South);

                    if (walls.EastDist == 0 &&
                        !FieldNavigationHelper.IsDirectionNearMapExit(playerPos, GameConstants.DirEast, mapExitPositions))
                        wallDirectionsBuffer.Add(SoundPlayer.Direction.East);

                    if (walls.WestDist == 0 &&
                        !FieldNavigationHelper.IsDirectionNearMapExit(playerPos, GameConstants.DirWest, mapExitPositions))
                        wallDirectionsBuffer.Add(SoundPlayer.Direction.West);

                    SoundPlayer.PlayWallTonesLooped(wallDirectionsBuffer);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[WallTones] Error: {ex.Message}");
                }
            }

            wallToneCoroutine = null;
            if (SoundPlayer.IsWallTonePlaying())
                SoundPlayer.StopWallTone();
        }

        private IEnumerator LandingPingLoop()
        {
            float nextCheckTime = Time.time + GameConstants.INITIAL_LOOP_DELAY;

            while (PreferencesManager.LandingPingsEnabled)
            {
                // Stop-gate: battle, event/cutscene, dialogue, menus, mod overlays, fades.
                if (IsAudioSuppressed)
                {
                    if (SoundPlayer.IsLandingPingPlaying())
                        SoundPlayer.StopLandingPing();
                    yield return null;
                    continue;
                }

                if (Time.time < nextCheckTime)
                {
                    yield return null;
                    continue;
                }
                nextCheckTime = Time.time + GameConstants.LANDING_PING_INTERVAL;

                try
                {
                    float currentTime = Time.time;

                    int currentMapId = GetCurrentMapId();
                    if (currentMapId > 0 && landingPingMapId > 0 && currentMapId != landingPingMapId)
                    {
                        landingPingSuppressedUntil = currentTime + GameConstants.SCENE_LOAD_SUPPRESSION;
                        if (SoundPlayer.IsLandingPingPlaying())
                            SoundPlayer.StopLandingPing();
                    }
                    if (currentMapId > 0)
                        landingPingMapId = currentMapId;

                    if (currentTime < landingPingSuppressedUntil)
                    {
                        if (SoundPlayer.IsLandingPingPlaying())
                            SoundPlayer.StopLandingPing();
                        continue;
                    }

                    if (currentTime < vehicleTransitionSuppressedUntil)
                    {
                        if (SoundPlayer.IsLandingPingPlaying())
                            SoundPlayer.StopLandingPing();
                        continue;
                    }

                    if (GameStatePatches.IsScreenFading)
                    {
                        if (SoundPlayer.IsLandingPingPlaying())
                            SoundPlayer.StopLandingPing();
                        continue;
                    }

                    var player = GetFieldPlayer();
                    if (player == null)
                    {
                        if (SoundPlayer.IsLandingPingPlaying())
                            SoundPlayer.StopLandingPing();
                        continue;
                    }

                    bool isShip = MoveStateHelper.IsControllingShip();
                    if (!isShip)
                    {
                        if (SoundPlayer.IsLandingPingPlaying())
                            SoundPlayer.StopLandingPing();
                        continue;
                    }

                    FieldNavigationHelper.GetNearbyLandingSpots(player, landingDirectionsBuffer);

                    SoundPlayer.PlayLandingPingsLooped(landingDirectionsBuffer);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[LandingPing] Error: {ex.Message}");
                }
            }

            landingPingCoroutine = null;
            if (SoundPlayer.IsLandingPingPlaying())
                SoundPlayer.StopLandingPing();
        }

        #endregion

        #region Scene Transition

        /// <summary>
        /// Called during scene load. Stops all loops and sets suppression timestamps.
        /// </summary>
        public void OnSceneTransition()
        {
            StopWallToneLoop();
            StopBeaconLoop();
            StopLandingPingLoop();
            wallToneSuppressedUntil = Time.time + GameConstants.SCENE_LOAD_SUPPRESSION;
            beaconSuppressedUntil = Time.time + GameConstants.SCENE_LOAD_SUPPRESSION;
            landingPingSuppressedUntil = Time.time + GameConstants.SCENE_LOAD_SUPPRESSION;
        }

        /// <summary>
        /// Suppresses wall tones and landing pings briefly during vehicle boarding/disembarking transitions.
        /// Called from GetOn_Postfix and GetOff_Postfix to prevent spurious tones during animation.
        /// </summary>
        public static void SuppressWallTonesForTransition()
        {
            vehicleTransitionSuppressedUntil = Time.time + GameConstants.VEHICLE_TRANSITION_SUPPRESSION;
        }

        #endregion

        #region Battle/Dialogue Navigation Suppression

        /// <summary>
        /// Silences all navigation loops for battle. Called by BattleState.SetActive().
        /// Flips the transient suppression gate — the persisted enabled toggles are untouched,
        /// so the loops resume automatically once suppression clears. The coroutines keep
        /// running (they silence themselves via the stop-gate); we only stop any sound already
        /// playing so the silence is immediate.
        /// </summary>
        public void SuppressNavigationForBattle()
        {
            suppressed = true;
            if (SoundPlayer.IsWallTonePlaying())
                SoundPlayer.StopWallTone();
            if (SoundPlayer.IsLandingPingPlaying())
                SoundPlayer.StopLandingPing();
        }

        /// <summary>
        /// Restores navigation loops after battle. Called by BattleState.Reset().
        /// Clears the suppression gate and re-arms any loops the enabled preference wants
        /// (StartX is a no-op when already running or when the pref is off — e.g. after a
        /// scene reload stopped the coroutines). The enabled-toggle bool parameters are legacy
        /// (enabled state now lives in PreferencesManager); only the pathfinding filter is restored.
        /// </summary>
        public void RestoreNavigationAfterBattle(bool wallTones, bool footsteps, bool audioBeacons, bool pathfindingFilter, bool landingPings = false)
        {
            suppressed = false;
            if (entityNavigator != null) entityNavigator.FilterByPathfinding = pathfindingFilter;
            StartWallToneLoop();
            StartBeaconLoop();
            StartLandingPingLoop();
        }

        /// <summary>
        /// Silences navigation loops for dialogue. Flips the transient suppression gate.
        /// </summary>
        public void SuppressNavigationForDialogue()
        {
            SuppressNavigationForBattle();
        }

        /// <summary>
        /// Restores navigation loops after dialogue ends. Clears the suppression gate.
        /// The loops stay silenced by their own BattleState/DialogueTracker stop-gate checks
        /// if battle or dialogue is somehow still active.
        /// </summary>
        public void RestoreNavigationAfterDialogue()
        {
            suppressed = false;
            StartWallToneLoop();
            StartBeaconLoop();
            StartLandingPingLoop();
        }

        #endregion

        /// <summary>
        /// Clears stale internal state (suppression gate, coroutine references) so
        /// re-enable via Ctrl+F8 starts clean. Called by the kill switch.
        /// </summary>
        public void ForceResetInternalState()
        {
            suppressed = false;
            // Clear coroutine references — CoroutineManager.CleanupAll() already stopped them
            wallToneCoroutine = null;
            beaconCoroutine = null;
            landingPingCoroutine = null;
        }

        #region Helpers

        private static FieldPlayer GetFieldPlayer()
        {
            try
            {
                var playerController = GameObjectCache.Get<FieldPlayerController>();
                if (playerController?.fieldPlayer != null)
                    return playerController.fieldPlayer;

                playerController = GameObjectCache.Refresh<FieldPlayerController>();
                return playerController?.fieldPlayer;
            }
            catch
            {
                return null;
            }
        }

        private static int GetCurrentMapId()
        {
            try
            {
                var userDataManager = UserDataManager.Instance();
                if (userDataManager != null)
                    return userDataManager.CurrentMapId;
            }
            catch { }
            return -1;
        }

        #endregion
    }
}
