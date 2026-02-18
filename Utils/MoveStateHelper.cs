using System;
using Il2CppLast.Map;
using Il2CppLast.Entity.Field;
using MelonLoader;

namespace FFV_ScreenReader.Utils
{
    /// <summary>
    /// Helper class for tracking player movement state.
    /// Handles vehicle boarding/disembarking state for FF5's vehicles:
    /// Ship, Airship, Submarine, Wind Drake, Chocobo
    ///
    /// State is updated directly by GetOn/GetOff patches - no polling or timeouts.
    /// </summary>
    public static class MoveStateHelper
    {
        // MoveState enum values (from FieldPlayerConstants.MoveState)
        public const int MOVE_STATE_WALK = 0;
        public const int MOVE_STATE_DUSH = 1;    // Dash
        public const int MOVE_STATE_AIRSHIP = 2;
        public const int MOVE_STATE_SHIP = 3;     // Ship
        public const int MOVE_STATE_LOWFLYING = 4;
        public const int MOVE_STATE_CHOCOBO = 5;
        public const int MOVE_STATE_GIMMICK = 6;
        public const int MOVE_STATE_UNIQUE = 7;

        // TransportationType enum values (more specific vehicle types)
        public const int TRANSPORT_NONE = 0;
        public const int TRANSPORT_PLAYER = 1;
        public const int TRANSPORT_SHIP = 2;
        public const int TRANSPORT_PLANE = 3;
        public const int TRANSPORT_SYMBOL = 4;
        public const int TRANSPORT_CONTENT = 5;
        public const int TRANSPORT_SUBMARINE = 6;
        public const int TRANSPORT_LOWFLYING = 7;
        public const int TRANSPORT_SPECIAL_PLANE = 8;
        public const int TRANSPORT_YELLOW_CHOCOBO = 9;
        public const int TRANSPORT_BLACK_CHOCOBO = 10;
        public const int TRANSPORT_BOKO = 11;
        public const int TRANSPORT_MAGICAL_ARMOR = 12;

        // Cached state tracking (set by GetOn/GetOff patches)
        private static int cachedMoveState = MOVE_STATE_WALK;
        private static int cachedTransportType = TRANSPORT_NONE;
        private static int lastAnnouncedState = -1;

        // Dash flag: read directly from player.moveState (no cached tracking needed)

        /// <summary>
        /// Set vehicle state when boarding (called from GetOn patch).
        /// </summary>
        public static void SetVehicleState(int transportationType)
        {
            cachedTransportType = transportationType;
            cachedMoveState = TransportTypeToMoveState(transportationType);
            lastAnnouncedState = cachedMoveState;
        }

        /// <summary>
        /// Set on foot state when disembarking (called from GetOff patch).
        /// </summary>
        public static void SetOnFoot()
        {
            cachedTransportType = TRANSPORT_NONE;
            cachedMoveState = MOVE_STATE_WALK;
            lastAnnouncedState = MOVE_STATE_WALK;
        }

        /// <summary>
        /// Reset state tracking (call on map transitions).
        /// </summary>
        public static void ResetState()
        {
            cachedMoveState = MOVE_STATE_WALK;
            cachedTransportType = TRANSPORT_NONE;
            lastAnnouncedState = -1;
            // cachedDashFlag removed — dash read directly from player
        }

        /// <summary>
        /// Get the effective walk/run state by reading moveState directly from the player.
        /// In FF5, AutoDash setting inverts the running behavior:
        /// - AutoDash ON: Player runs by default, F1/hold makes them walk
        /// - AutoDash OFF: Player walks by default, F1/hold makes them run
        /// </summary>
        public static bool GetDashFlag()
        {
            try
            {
                var playerController = GameObjectCache.Get<FieldPlayerController>();
                var player = playerController?.fieldPlayer;
                if (player == null) return false;

                bool isDashing = (int)player.moveState == MOVE_STATE_DUSH;

                var userDataManager = Il2CppLast.Management.UserDataManager.Instance();
                bool autoDash = (userDataManager?.Config?.IsAutoDash ?? 0) != 0;

                return autoDash != isDashing;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Called when transitioning to a new map.
        /// Interior maps (non-world maps) should always be on-foot state.
        /// </summary>
        public static bool OnMapTransition(bool isWorldMap)
        {
            if (!isWorldMap && IsVehicleState(cachedMoveState))
            {
                // Check actual game state - player may still be on a vehicle
                // (e.g., riding Boko into an interior map before scripted dismount)
                try
                {
                    var playerController = GameObjectCache.Get<FieldPlayerController>();
                    var player = playerController?.fieldPlayer;
                    if (player != null)
                    {
                        int actualMoveState = (int)player.moveState;
                        if (IsVehicleState(actualMoveState))
                        {
                            // Game still has player on vehicle - don't force on-foot.
                            // ChangeMoveState_Patch will handle the dismount when it happens.
                            return false;
                        }
                    }
                }
                catch { }

                // Player is actually on foot (or can't read state) - update cache
                cachedMoveState = MOVE_STATE_WALK;
                cachedTransportType = TRANSPORT_NONE;
                lastAnnouncedState = MOVE_STATE_WALK;

                Patches.MovementSpeechPatches.SyncToOnFoot();

                Core.FFV_ScreenReaderMod.SpeakText("On foot", interrupt: false);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if a state is a vehicle state (ship, chocobo, airship, low flying).
        /// </summary>
        public static bool IsVehicleState(int state)
        {
            return state == MOVE_STATE_SHIP || state == MOVE_STATE_CHOCOBO ||
                   state == MOVE_STATE_AIRSHIP || state == MOVE_STATE_LOWFLYING;
        }

        /// <summary>
        /// Announce movement state changes (called from ChangeMoveState patch).
        /// </summary>
        public static void AnnounceStateChange(int previousState, int newState)
        {
            if (newState == lastAnnouncedState)
                return;

            string announcement = null;

            if (newState == MOVE_STATE_SHIP)
            {
                announcement = "On ship";
                cachedMoveState = MOVE_STATE_SHIP;
            }
            else if (newState == MOVE_STATE_CHOCOBO)
            {
                announcement = "On chocobo";
                cachedMoveState = MOVE_STATE_CHOCOBO;
            }
            else if (newState == MOVE_STATE_AIRSHIP || newState == MOVE_STATE_LOWFLYING)
            {
                announcement = "On airship";
                cachedMoveState = newState;
            }
            else if (IsVehicleState(previousState) &&
                     (newState == MOVE_STATE_WALK || newState == MOVE_STATE_DUSH))
            {
                announcement = "On foot";
                cachedMoveState = newState;
                cachedTransportType = TRANSPORT_NONE;
            }
            else
            {
                cachedMoveState = newState;
            }

            if (announcement != null)
            {
                lastAnnouncedState = newState;
                Core.FFV_ScreenReaderMod.SpeakText(announcement, interrupt: false);
            }
        }

        /// <summary>
        /// Convert TransportationType to MoveState.
        /// </summary>
        private static int TransportTypeToMoveState(int transportationType)
        {
            switch (transportationType)
            {
                case TRANSPORT_SHIP:
                    return MOVE_STATE_SHIP;
                case TRANSPORT_PLANE:
                case TRANSPORT_SPECIAL_PLANE:
                    return MOVE_STATE_AIRSHIP;
                case TRANSPORT_SUBMARINE:
                    return MOVE_STATE_SHIP; // Submarine uses ship movement
                case TRANSPORT_LOWFLYING:
                    return MOVE_STATE_LOWFLYING;
                case TRANSPORT_YELLOW_CHOCOBO:
                case TRANSPORT_BLACK_CHOCOBO:
                case TRANSPORT_BOKO:
                    return MOVE_STATE_CHOCOBO;
                default:
                    return MOVE_STATE_WALK;
            }
        }

        /// <summary>
        /// Get current MoveState (returns cached state set by GetOn/GetOff).
        /// </summary>
        public static int GetCurrentMoveState()
        {
            return cachedMoveState;
        }

        /// <summary>
        /// Get current TransportationType.
        /// </summary>
        public static int GetCurrentTransportType()
        {
            return cachedTransportType;
        }

        /// <summary>
        /// Check if currently controlling ship.
        /// </summary>
        public static bool IsControllingShip()
        {
            return cachedMoveState == MOVE_STATE_SHIP;
        }

        /// <summary>
        /// Check if currently on foot (walking or dashing).
        /// </summary>
        public static bool IsOnFoot()
        {
            return cachedMoveState == MOVE_STATE_WALK || cachedMoveState == MOVE_STATE_DUSH;
        }

        /// <summary>
        /// Check if currently riding chocobo.
        /// </summary>
        public static bool IsRidingChocobo()
        {
            return cachedMoveState == MOVE_STATE_CHOCOBO;
        }

        /// <summary>
        /// Check if currently controlling airship.
        /// </summary>
        public static bool IsControllingAirship()
        {
            return cachedMoveState == MOVE_STATE_AIRSHIP;
        }

        /// <summary>
        /// Get pathfinding scope multiplier based on current MoveState.
        /// </summary>
        public static float GetPathfindingMultiplier()
        {
            switch (cachedMoveState)
            {
                case MOVE_STATE_WALK:
                case MOVE_STATE_DUSH:
                    return 1.0f;

                case MOVE_STATE_SHIP:
                    return 2.5f;

                case MOVE_STATE_CHOCOBO:
                    return 1.5f;

                case MOVE_STATE_AIRSHIP:
                case MOVE_STATE_LOWFLYING:
                    return 1.0f;

                default:
                    return 1.0f;
            }
        }

        /// <summary>
        /// Get human-readable name for MoveState.
        /// </summary>
        public static string GetMoveStateName(int moveState)
        {
            switch (moveState)
            {
                case MOVE_STATE_WALK: return "Walking";
                case MOVE_STATE_DUSH: return "Dashing";
                case MOVE_STATE_SHIP: return "Ship";
                case MOVE_STATE_AIRSHIP: return "Airship";
                case MOVE_STATE_LOWFLYING: return "Low Flying";
                case MOVE_STATE_CHOCOBO: return "Chocobo";
                case MOVE_STATE_GIMMICK: return "Gimmick";
                case MOVE_STATE_UNIQUE: return "Unique";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Set vehicle state from MoveState value (used for state sync).
        /// This is a failsafe when hooks don't fire properly.
        /// </summary>
        public static void SetVehicleStateFromMoveState(int moveState)
        {
            cachedMoveState = moveState;
            cachedTransportType = MoveStateToTransportType(moveState);
            lastAnnouncedState = moveState;
        }

        /// <summary>
        /// Convert MoveState to approximate TransportationType.
        /// </summary>
        private static int MoveStateToTransportType(int moveState)
        {
            switch (moveState)
            {
                case MOVE_STATE_SHIP: return TRANSPORT_SHIP;
                case MOVE_STATE_AIRSHIP: return TRANSPORT_PLANE;
                case MOVE_STATE_LOWFLYING: return TRANSPORT_LOWFLYING;
                case MOVE_STATE_CHOCOBO: return TRANSPORT_YELLOW_CHOCOBO;
                default: return TRANSPORT_NONE;
            }
        }

        /// <summary>
        /// Sync cached state with actual game state.
        /// Called by V key handler as a failsafe when hooks don't fire.
        /// </summary>
        public static void SyncWithActualGameState()
        {
            try
            {
                var playerController = GameObjectCache.Get<FieldPlayerController>();
                var player = playerController?.fieldPlayer;
                if (player == null) return;

                int actualMoveState = (int)player.moveState;

                // If actual state differs from cached, update cache
                if (actualMoveState != cachedMoveState)
                {
                    if (actualMoveState == MOVE_STATE_WALK || actualMoveState == MOVE_STATE_DUSH)
                    {
                        SetOnFoot();
                    }
                    else
                    {
                        // Map moveState to rough transport type
                        SetVehicleStateFromMoveState(actualMoveState);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MoveState] Error syncing state: {ex.Message}");
            }
        }
    }
}
