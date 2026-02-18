using System;
using HarmonyLib;
using Il2CppLast.Entity.Field;
using Il2CppLast.Map;
using UnityEngine;
using FFV_ScreenReader.Core;
using FFV_ScreenReader.Utils;

namespace FFV_ScreenReader.Patches
{
    /// <summary>
    /// Patches for playing sound effects when player hits a wall/obstacle.
    /// Uses FieldController.OnPlayerHitCollider which fires on collision events.
    /// Ported from FF4 mod pattern.
    /// </summary>
    [HarmonyPatch]
    public static class MovementSoundPatches
    {
        // Cooldown to prevent sound spam when holding direction against a wall
        private static float lastBumpTime = 0f;
        private const float BUMP_COOLDOWN = 0.3f; // 300ms

        // Footstep tracking
        private static Vector2Int lastTilePosition = Vector2Int.zero;
        private static bool tileTrackingInitialized = false;
        private static float lastFootstepTime = 0f;
        private const float FOOTSTEP_COOLDOWN = 0.15f;

        /// <summary>
        /// Fires when the player collides with an obstacle.
        /// </summary>
        [HarmonyPatch(typeof(FieldController), nameof(FieldController.OnPlayerHitCollider))]
        [HarmonyPostfix]
        private static void OnPlayerHitCollider_Postfix(FieldPlayer playerEntity)
        {
            try
            {
                // Suppress wall bumps during battle, dialogue, or events
                if (BattleState.IsInBattle || DialogueTracker.IsInDialogue || GameStatePatches.IsInEventState)
                    return;

                float currentTime = Time.time;
                if (currentTime - lastBumpTime < BUMP_COOLDOWN)
                    return;

                lastBumpTime = currentTime;
                SoundPlayer.PlayWallBump();
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Error in OnPlayerHitCollider_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Check and play footstep when player position changes.
        /// Called from InputManager or other movement tracking.
        /// </summary>
        public static void CheckFootstep(Vector3 worldPos)
        {
            try
            {
                if (!FFV_ScreenReaderMod.FootstepsEnabled)
                    return;

                Vector2Int currentTile = new Vector2Int(
                    Mathf.FloorToInt(worldPos.x / GameConstants.TILE_SIZE),
                    Mathf.FloorToInt(worldPos.y / GameConstants.TILE_SIZE)
                );

                if (!tileTrackingInitialized)
                {
                    lastTilePosition = currentTile;
                    tileTrackingInitialized = true;
                    return;
                }

                if (currentTile != lastTilePosition)
                {
                    lastTilePosition = currentTile;
                    float currentTime = Time.time;
                    if (currentTime - lastFootstepTime >= FOOTSTEP_COOLDOWN)
                    {
                        SoundPlayer.PlayFootstep();
                        lastFootstepTime = currentTime;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"Error in CheckFootstep: {ex.Message}");
            }
        }

        /// <summary>
        /// Resets all static state. Called on map transitions.
        /// </summary>
        public static void ResetState()
        {
            lastBumpTime = 0f;
            lastTilePosition = Vector2Int.zero;
            tileTrackingInitialized = false;
            lastFootstepTime = 0f;
        }
    }
}
