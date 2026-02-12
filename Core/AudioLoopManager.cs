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

        // Audio feedback toggles
        private bool enableWallTones = false;
        private bool enableFootsteps = false;
        private bool enableAudioBeacons = false;
        private bool enableLandingPings = false;

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

        // Dialogue state storage (separate from battle)
        private NavigationStateSnapshot _preDialogueSnapshot;
        private bool _hasStoredDialogueState = false;

        public AudioLoopManager(EntityCache entityCache, EntityNavigator entityNavigator)
        {
            this.entityCache = entityCache;
            this.entityNavigator = entityNavigator;
            Instance = this;
        }

        /// <summary>
        /// Initializes toggles from saved preferences and starts loops if enabled.
        /// Call after PreferencesManager.Initialize().
        /// </summary>
        public void InitializeFromPreferences()
        {
            enableWallTones = PreferencesManager.WallTonesDefault;
            enableFootsteps = PreferencesManager.FootstepsDefault;
            enableAudioBeacons = PreferencesManager.AudioBeaconsDefault;
            enableLandingPings = PreferencesManager.LandingPingsDefault;

            if (enableWallTones) StartWallToneLoop();
            if (enableAudioBeacons) StartBeaconLoop();
            if (enableLandingPings) StartLandingPingLoop();
        }

        #region Public Toggle Accessors

        public bool IsWallTonesEnabled => enableWallTones;
        public bool IsFootstepsEnabled => enableFootsteps;
        public bool IsAudioBeaconsEnabled => enableAudioBeacons;
        public bool IsLandingPingsEnabled => enableLandingPings;

        #endregion

        #region Toggle Methods

        public void ToggleWallTones()
        {
            enableWallTones = !enableWallTones;

            if (enableWallTones)
                StartWallToneLoop();
            else
                StopWallToneLoop();

            PreferencesManager.SaveWallTones(enableWallTones);

            string status = enableWallTones ? "on" : "off";
            FFV_ScreenReaderMod.SpeakText($"Wall tones {status}");
        }

        public void ToggleFootsteps()
        {
            enableFootsteps = !enableFootsteps;

            PreferencesManager.SaveFootsteps(enableFootsteps);

            string status = enableFootsteps ? "on" : "off";
            FFV_ScreenReaderMod.SpeakText($"Footsteps {status}");
        }

        public void ToggleAudioBeacons()
        {
            enableAudioBeacons = !enableAudioBeacons;

            if (enableAudioBeacons)
                StartBeaconLoop();
            else
                StopBeaconLoop();

            PreferencesManager.SaveAudioBeacons(enableAudioBeacons);

            string status = enableAudioBeacons ? "on" : "off";
            FFV_ScreenReaderMod.SpeakText($"Audio beacons {status}");
        }

        public void ToggleLandingPings()
        {
            enableLandingPings = !enableLandingPings;

            if (enableLandingPings)
                StartLandingPingLoop();
            else
                StopLandingPingLoop();

            PreferencesManager.SaveLandingPings(enableLandingPings);

            string status = enableLandingPings ? "on" : "off";
            FFV_ScreenReaderMod.SpeakText($"Landing pings {status}");
        }

        #endregion

        #region Start/Stop Loop Methods

        private void StartWallToneLoop()
        {
            if (!enableWallTones) return;
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
            if (!enableAudioBeacons) return;
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
        }

        private void StartLandingPingLoop()
        {
            if (!enableLandingPings) return;
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

        #endregion

        #region Coroutines

        private IEnumerator BeaconLoop()
        {
            float nextBeaconTime = Time.time + GameConstants.INITIAL_LOOP_DELAY;

            while (enableAudioBeacons)
            {
                if (BattleState.IsInBattle)
                {
                    yield return null;
                    continue;
                }

                if (Time.time < nextBeaconTime)
                {
                    yield return null;
                    continue;
                }
                nextBeaconTime = Time.time + GameConstants.BEACON_INTERVAL;

                if (Time.time < beaconSuppressedUntil)
                    continue;

                try
                {
                    var entity = entityNavigator?.CurrentEntity;
                    if (entity == null) continue;

                    var playerController = GameObjectCache.Get<FieldPlayerController>();
                    if (playerController?.fieldPlayer == null) continue;

                    Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
                    Vector3 entityPos = entity.Position;

                    if (float.IsNaN(playerPos.x) || float.IsNaN(entityPos.x) ||
                        Mathf.Abs(playerPos.x) > 10000f || Mathf.Abs(entityPos.x) > 10000f)
                        continue;

                    float distance = Vector3.Distance(playerPos, entityPos);
                    float maxDist = 500f;
                    float volumeScale = Mathf.Clamp(1f - (distance / maxDist), 0.15f, 0.60f);

                    float deltaX = entityPos.x - playerPos.x;
                    float pan = Mathf.Clamp(deltaX / 100f, -1f, 1f) * 0.5f + 0.5f;

                    bool isSouth = entityPos.y < playerPos.y - 8f;

                    float timeSinceLast = Time.time - lastBeaconPlayedAt;
                    if (timeSinceLast < GameConstants.BEACON_INTERVAL * 0.8f)
                        continue;

                    SoundPlayer.PlayBeacon(isSouth, pan, volumeScale);
                    lastBeaconPlayedAt = Time.time;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Beacon] Error: {ex.Message}");
                }
            }

            beaconCoroutine = null;
        }

        private IEnumerator WallToneLoop()
        {
            float nextCheckTime = Time.time + GameConstants.INITIAL_LOOP_DELAY;

            while (enableWallTones)
            {
                if (BattleState.IsInBattle)
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

            while (enableLandingPings)
            {
                if (BattleState.IsInBattle)
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
        /// Suppresses all navigation features for battle. Called by BattleState.SetActive().
        /// </summary>
        public void SuppressNavigationForBattle()
        {
            StopWallToneLoop();
            StopBeaconLoop();
            StopLandingPingLoop();
            enableWallTones = false;
            enableFootsteps = false;
            enableAudioBeacons = false;
            enableLandingPings = false;
        }

        /// <summary>
        /// Restores navigation features after battle. Called by BattleState.Reset().
        /// </summary>
        public void RestoreNavigationAfterBattle(bool wallTones, bool footsteps, bool audioBeacons, bool pathfindingFilter, bool landingPings = false)
        {
            enableWallTones = wallTones;
            enableFootsteps = footsteps;
            enableAudioBeacons = audioBeacons;
            enableLandingPings = landingPings;
            if (entityNavigator != null) entityNavigator.FilterByPathfinding = pathfindingFilter;
            if (enableWallTones) StartWallToneLoop();
            if (enableAudioBeacons) StartBeaconLoop();
            if (enableLandingPings) StartLandingPingLoop();
        }

        /// <summary>
        /// Suppresses navigation features for dialogue. Stores current state first.
        /// </summary>
        public void SuppressNavigationForDialogue()
        {
            if (_hasStoredDialogueState) return;

            _preDialogueSnapshot = NavigationStateSnapshot.Capture(this);
            _hasStoredDialogueState = true;

            SuppressNavigationForBattle();
        }

        /// <summary>
        /// Restores navigation features after dialogue ends.
        /// </summary>
        public void RestoreNavigationAfterDialogue()
        {
            if (!_hasStoredDialogueState) return;

            _hasStoredDialogueState = false;
            _preDialogueSnapshot.RestoreTo(this);
        }

        #endregion

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
