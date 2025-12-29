using Il2CppLast.Map;
using Il2CppLast.Entity.Field;
using MelonLoader;

namespace FFV_ScreenReader.Utils
{
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

        // Cached state tracking (workaround for unreliable moveState field)
        private static int cachedMoveState = MOVE_STATE_WALK;
        private static bool useCachedState = false;
        private static int lastAnnouncedState = -1;
        private static float lastVehicleStateSeenTime = 0f;
        private const float VEHICLE_STATE_TIMEOUT_SECONDS = 1.0f; // If we don't see vehicle state for 1s, assume disembarked

        /// <summary>
        /// Update the cached move state (called from MovementSpeechPatches when state changes)
        /// This is the "reliable" update path from ChangeMoveState event
        /// </summary>
        public static void UpdateCachedMoveState(int newState)
        {
            int previousState = cachedMoveState;
            cachedMoveState = newState;
            useCachedState = true;

            // If this is a vehicle state, update the timestamp
            if (IsVehicleState(newState))
            {
                lastVehicleStateSeenTime = UnityEngine.Time.time;
            }

            // Announce state changes that weren't already announced
            if (newState != lastAnnouncedState)
            {
                AnnounceStateChange(previousState, newState);
            }
        }

        /// <summary>
        /// Check if a state is a vehicle state (ship, chocobo, airship)
        /// </summary>
        private static bool IsVehicleState(int state)
        {
            return state == MOVE_STATE_SHIP || state == MOVE_STATE_CHOCOBO ||
                   state == MOVE_STATE_AIRSHIP || state == MOVE_STATE_LOWFLYING;
        }

        /// <summary>
        /// Announce movement state changes
        /// Public so coroutine can call it from MovementSpeechPatches
        /// </summary>
        public static void AnnounceStateChange(int previousState, int newState)
        {
            string announcement = null;

            if (newState == MOVE_STATE_SHIP)
            {
                announcement = "On ship";
            }
            else if (newState == MOVE_STATE_CHOCOBO)
            {
                announcement = "On chocobo";
            }
            else if (newState == MOVE_STATE_AIRSHIP || newState == MOVE_STATE_LOWFLYING)
            {
                announcement = "On airship";
            }
            else if ((previousState == MOVE_STATE_SHIP || previousState == MOVE_STATE_CHOCOBO ||
                      previousState == MOVE_STATE_AIRSHIP || previousState == MOVE_STATE_LOWFLYING) &&
                     (newState == MOVE_STATE_WALK || newState == MOVE_STATE_DUSH))
            {
                announcement = "On foot";
            }

            if (announcement != null)
            {
                lastAnnouncedState = newState;
                Core.FFV_ScreenReaderMod.SpeakText(announcement, interrupt: true);
            }
        }

        /// <summary>
        /// Get current MoveState from FieldPlayer
        /// </summary>
        public static int GetCurrentMoveState()
        {
            var controller = GameObjectCache.Get<FieldPlayerController>();
            if (controller?.fieldPlayer == null)
                return useCachedState ? cachedMoveState : MOVE_STATE_WALK;

            // Read actual state from game
            int actualState = (int)controller.fieldPlayer.moveState;
            float currentTime = UnityEngine.Time.time;

            // BUG WORKAROUND: moveState field unreliably reverts to Walking even when on vehicles
            // Vehicle states (ship, chocobo, airship) are "sticky" - once detected, we don't revert
            // to Walking unless ChangeMoveState explicitly fires OR we timeout

            // If actual state shows a vehicle, update timestamp
            if (IsVehicleState(actualState))
            {
                lastVehicleStateSeenTime = currentTime;

                // If this is a new vehicle state, cache it (coroutine will announce)
                if (actualState != cachedMoveState)
                {
                    cachedMoveState = actualState;
                    useCachedState = true;
                }
            }

            // If we have a cached vehicle state but actual shows Walking
            if (useCachedState && IsVehicleState(cachedMoveState) && actualState == MOVE_STATE_WALK)
            {
                // Check if we've timed out (haven't seen vehicle state in actual for too long)
                float timeSinceLastSeen = currentTime - lastVehicleStateSeenTime;
                if (timeSinceLastSeen > VEHICLE_STATE_TIMEOUT_SECONDS)
                {
                    // Timeout: assume player disembarked without ChangeMoveState firing
                    cachedMoveState = actualState;
                    return actualState;
                }

                // Still within timeout: trust cached vehicle state
                return cachedMoveState;
            }

            // For non-vehicle states when not cached as vehicle, update cache normally
            if (!IsVehicleState(actualState) && actualState != cachedMoveState && !IsVehicleState(cachedMoveState))
            {
                cachedMoveState = actualState;
            }

            return useCachedState && IsVehicleState(cachedMoveState) ? cachedMoveState : actualState;
        }

        /// <summary>
        /// Check if currently controlling pirate ship
        /// </summary>
        public static bool IsControllingShip()
        {
            return GetCurrentMoveState() == MOVE_STATE_SHIP;
        }

        /// <summary>
        /// Check if currently on foot (walking or dashing)
        /// </summary>
        public static bool IsOnFoot()
        {
            int state = GetCurrentMoveState();
            return state == MOVE_STATE_WALK || state == MOVE_STATE_DUSH;
        }

        /// <summary>
        /// Check if currently riding chocobo
        /// </summary>
        public static bool IsRidingChocobo()
        {
            return GetCurrentMoveState() == MOVE_STATE_CHOCOBO;
        }

        /// <summary>
        /// Check if currently controlling airship
        /// </summary>
        public static bool IsControllingAirship()
        {
            return GetCurrentMoveState() == MOVE_STATE_AIRSHIP;
        }

        /// <summary>
        /// Get pathfinding scope multiplier based on current MoveState
        /// </summary>
        public static float GetPathfindingMultiplier()
        {
            int moveState = GetCurrentMoveState();
            float multiplier;

            switch (moveState)
            {
                case MOVE_STATE_WALK:
                case MOVE_STATE_DUSH:
                    multiplier = 1.0f;  // Baseline (on foot)
                    break;

                case MOVE_STATE_SHIP:
                    multiplier = 2.5f;  // 2.5x scope for ship (1250 units)
                    break;

                case MOVE_STATE_CHOCOBO:
                    multiplier = 1.5f;  // Moderate increase for chocobo
                    break;

                case MOVE_STATE_AIRSHIP:
                case MOVE_STATE_LOWFLYING:
                    multiplier = 1.0f;  // Airship uses different navigation system
                    break;

                default:
                    multiplier = 1.0f;  // Default to baseline
                    break;
            }

            return multiplier;
        }

        /// <summary>
        /// Get human-readable name for MoveState
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
    }
}
