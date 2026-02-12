using System;
using FFV_ScreenReader.Field;
using FFV_ScreenReader.Utils;
using Il2CppLast.Management;
using MelonLoader;
using UnityEngine;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Coordinates all waypoint operations: CRUD, cycling, pathfinding.
    /// Extracted from FFV_ScreenReaderMod to reduce god class size.
    /// </summary>
    public class WaypointController
    {
        private readonly WaypointManager waypointManager;
        private readonly WaypointNavigator waypointNavigator;

        public WaypointController(WaypointManager waypointManager, WaypointNavigator waypointNavigator)
        {
            this.waypointManager = waypointManager;
            this.waypointNavigator = waypointNavigator;
        }

        /// <summary>
        /// Gets the current map ID as a string for waypoint storage.
        /// </summary>
        private string GetCurrentMapIdString()
        {
            try
            {
                var userDataManager = UserDataManager.Instance();
                if (userDataManager != null)
                    return userDataManager.CurrentMapId.ToString();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting map ID: {ex.Message}");
            }
            return "unknown";
        }

        public void CycleNextWaypoint()
        {
            string mapId = GetCurrentMapIdString();
            waypointNavigator.RefreshList(mapId);

            var waypoint = waypointNavigator.CycleNext();
            if (waypoint == null)
            {
                FFV_ScreenReaderMod.SpeakText("No waypoints");
                return;
            }

            FFV_ScreenReaderMod.SpeakText(waypointNavigator.FormatCurrentWaypoint());
        }

        public void CyclePreviousWaypoint()
        {
            string mapId = GetCurrentMapIdString();
            waypointNavigator.RefreshList(mapId);

            var waypoint = waypointNavigator.CyclePrevious();
            if (waypoint == null)
            {
                FFV_ScreenReaderMod.SpeakText("No waypoints");
                return;
            }

            FFV_ScreenReaderMod.SpeakText(waypointNavigator.FormatCurrentWaypoint());
        }

        public void CycleNextWaypointCategory()
        {
            string mapId = GetCurrentMapIdString();
            waypointNavigator.CycleNextCategory(mapId);
            FFV_ScreenReaderMod.SpeakText(waypointNavigator.GetCategoryAnnouncement());
        }

        public void CyclePreviousWaypointCategory()
        {
            string mapId = GetCurrentMapIdString();
            waypointNavigator.CyclePreviousCategory(mapId);
            FFV_ScreenReaderMod.SpeakText(waypointNavigator.GetCategoryAnnouncement());
        }

        public void PathfindToCurrentWaypoint()
        {
            var waypoint = waypointNavigator.SelectedWaypoint;
            if (waypoint == null)
            {
                FFV_ScreenReaderMod.SpeakText("No waypoint selected");
                return;
            }

            var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController == null || playerController.fieldPlayer == null || playerController.fieldPlayer.transform == null)
            {
                FFV_ScreenReaderMod.SpeakText("Not in field");
                return;
            }

            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;

            var pathInfo = FieldNavigationHelper.FindPathTo(
                playerPos,
                waypoint.Position,
                playerController.mapHandle,
                playerController.fieldPlayer
            );

            if (pathInfo.Success)
            {
                FFV_ScreenReaderMod.SpeakText($"Path to {waypoint.WaypointName}: {pathInfo.Description}");
            }
            else
            {
                string description = waypoint.FormatDescription(playerPos);
                FFV_ScreenReaderMod.SpeakText($"No path to {waypoint.WaypointName}. {description}");
            }
        }

        public void AddNewWaypointWithNaming()
        {
            var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController == null || playerController.fieldPlayer == null || playerController.fieldPlayer.transform == null)
            {
                FFV_ScreenReaderMod.SpeakText("Not in field");
                return;
            }

            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
            string mapId = GetCurrentMapIdString();

            var category = waypointNavigator.CurrentCategory;
            if (category == WaypointCategory.All)
                category = WaypointCategory.Miscellaneous;

            TextInputWindow.Open(
                "Enter waypoint name",
                "",
                (name) =>
                {
                    waypointManager.AddWaypoint(name, playerPos, mapId, category);
                    waypointNavigator.RefreshList(mapId);

                    string categoryName = WaypointEntity.GetCategoryDisplayName(category);
                    FFV_ScreenReaderMod.SpeakTextDelayed($"Added {name} as {categoryName}");
                },
                () =>
                {
                    FFV_ScreenReaderMod.SpeakTextDelayed("Waypoint creation cancelled");
                }
            );
        }

        public void AddNewWaypoint()
        {
            var playerController = GameObjectCache.Get<Il2CppLast.Map.FieldPlayerController>();
            if (playerController == null || playerController.fieldPlayer == null || playerController.fieldPlayer.transform == null)
            {
                FFV_ScreenReaderMod.SpeakText("Not in field");
                return;
            }

            Vector3 playerPos = playerController.fieldPlayer.transform.localPosition;
            string mapId = GetCurrentMapIdString();

            var category = waypointNavigator.CurrentCategory;
            if (category == WaypointCategory.All)
                category = WaypointCategory.Miscellaneous;

            string name = waypointManager.GetNextWaypointName(mapId);

            waypointManager.AddWaypoint(name, playerPos, mapId, category);
            waypointNavigator.RefreshList(mapId);

            string categoryName = WaypointEntity.GetCategoryDisplayName(category);
            FFV_ScreenReaderMod.SpeakText($"Added {name} as {categoryName}");
        }

        public void RenameCurrentWaypoint()
        {
            var waypoint = waypointNavigator.SelectedWaypoint;
            if (waypoint == null)
            {
                FFV_ScreenReaderMod.SpeakText("No waypoint selected");
                return;
            }

            string currentName = waypoint.WaypointName;
            string waypointId = waypoint.WaypointId;
            string mapId = GetCurrentMapIdString();

            TextInputWindow.Open(
                "Rename waypoint",
                currentName,
                (newName) =>
                {
                    if (waypointManager.RenameWaypoint(waypointId, newName))
                    {
                        waypointNavigator.RefreshList(mapId);
                        FFV_ScreenReaderMod.SpeakTextDelayed($"Renamed to {newName}");
                    }
                    else
                    {
                        FFV_ScreenReaderMod.SpeakTextDelayed("Rename failed");
                    }
                },
                () =>
                {
                    FFV_ScreenReaderMod.SpeakTextDelayed("Rename cancelled");
                }
            );
        }

        public void RemoveCurrentWaypoint()
        {
            var waypoint = waypointNavigator.SelectedWaypoint;
            if (waypoint == null)
            {
                FFV_ScreenReaderMod.SpeakText("No waypoint selected");
                return;
            }

            string name = waypoint.WaypointName;
            string waypointId = waypoint.WaypointId;

            ConfirmationDialog.Open(
                $"Delete {name}?",
                () =>
                {
                    waypointManager.RemoveWaypoint(waypointId);

                    string mapId = GetCurrentMapIdString();
                    waypointNavigator.RefreshList(mapId);
                    waypointNavigator.ClearSelection();

                    FFV_ScreenReaderMod.SpeakTextDelayed($"Removed {name}");
                },
                () =>
                {
                    FFV_ScreenReaderMod.SpeakTextDelayed("Cancelled");
                }
            );
        }

        public void ClearAllWaypointsForMap()
        {
            string mapId = GetCurrentMapIdString();
            int count = waypointManager.GetWaypointCountForMap(mapId);

            if (count == 0)
            {
                FFV_ScreenReaderMod.SpeakText("No waypoints to clear");
                return;
            }

            ConfirmationDialog.Open(
                $"Delete all {count} waypoints?",
                () =>
                {
                    ConfirmationDialog.Open(
                        "Are you absolutely sure?",
                        () =>
                        {
                            int cleared = waypointManager.ClearMapWaypoints(mapId);
                            waypointNavigator.RefreshList(mapId);
                            waypointNavigator.ClearSelection();
                            FFV_ScreenReaderMod.SpeakTextDelayed($"Cleared {cleared} waypoints");
                        },
                        () =>
                        {
                            FFV_ScreenReaderMod.SpeakTextDelayed("Cancelled");
                        }
                    );
                },
                () =>
                {
                    FFV_ScreenReaderMod.SpeakTextDelayed("Cancelled");
                }
            );
        }
    }
}
