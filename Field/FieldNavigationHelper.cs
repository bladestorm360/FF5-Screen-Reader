using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Il2CppLast.Entity.Field;
using Il2CppLast.Map;
using UnityEngine;
using Il2Cpp;
using MelonLoader;
using FFV_ScreenReader.Utils;
using FFV_ScreenReader.Field.Routing;
using static FFV_ScreenReader.Utils.ModTextTranslator;
using MapRouteSearcher = Il2Cpp.MapRouteSearcher;
using FieldPlayerController = Il2CppLast.Map.FieldPlayerController;

namespace FFV_ScreenReader.Field
{
    public static class FieldNavigationHelper
    {
        /// <summary>
        /// Maps FieldEntity instances to their vehicle type information.
        /// Populated during GetAllFieldEntities() from Transportation.ModelList.
        /// </summary>
        public static Dictionary<FieldEntity, (int Type, string MessageId)> VehicleTypeMap { get; }
            = new Dictionary<FieldEntity, (int, string)>();


        // Diagnostic one-shot flags — reset on map change
        private static bool _shouldLogEntities = true;
        private static bool _shouldLogSlowPathfinding = true;

        /// <summary>
        /// Resets the vehicle type map. Called on map transitions.
        /// </summary>
        public static void ResetVehicleTypeMap()
        {
            VehicleTypeMap.Clear();
            _shouldLogEntities = true;
            _shouldLogSlowPathfinding = true;
        }

        public static List<FieldEntity> GetAllFieldEntities()
        {
            var results = new List<FieldEntity>();

            var fieldMap = GameObjectCache.Get<FieldMap>();
            if (fieldMap?.fieldController == null)
                return results;

            var entityList = fieldMap.fieldController.entityList;
            int entityListCount = 0;
            if (entityList != null)
            {
                foreach (var fieldEntity in entityList)
                {
                    if (fieldEntity != null)
                    {
                        results.Add(fieldEntity);
                        entityListCount++;
                    }
                }
            }
            MelonLogger.Msg($"[EntityScan] entityList count: {entityListCount}");

            // Log individual entity details (one-shot per map)
            if (_shouldLogEntities)
            {
                _shouldLogEntities = false;
                foreach (var fe in results)
                {
                    try
                    {
                        string name = fe.gameObject?.name ?? "null";
                        string objType = "unknown";
                        try
                        {
                            if (fe.Property != null)
                                objType = ((MapConstants.ObjectType)fe.Property.ObjectType).ToString();
                        }
                        catch { }
                        var pos = fe.transform?.localPosition ?? UnityEngine.Vector3.zero;
                        MelonLogger.Msg($"[EntityScan] Entity: name={name}, type={objType}, pos=({pos.x:F1},{pos.y:F1})");
                    }
                    catch { }
                }
            }

            if (fieldMap.fieldController.transportation != null)
            {
                var transportationEntities = fieldMap.fieldController.transportation.NeedInteractiveList();
                int transportCount = 0;
                if (transportationEntities != null)
                {
                    foreach (var interactiveEntity in transportationEntities)
                    {
                        if (interactiveEntity == null) continue;

                        var fieldEntity = interactiveEntity.TryCast<Il2CppLast.Entity.Field.FieldEntity>();
                        if (fieldEntity != null)
                        {
                            results.Add(fieldEntity);
                            transportCount++;
                        }
                    }
                }
                MelonLogger.Msg($"[EntityScan] transportationEntities count: {transportCount}");

                // Populate VehicleTypeMap from TransportationController.ModelList
                PopulateVehicleTypeMap(fieldMap.fieldController.transportation, results);
            }

            return results;
        }

        /// <summary>
        /// Populates VehicleTypeMap from Transportation.ModelList using pointer offsets.
        /// This mirrors FF4's approach to iterate all vehicles on the map.
        /// </summary>
        private static unsafe void PopulateVehicleTypeMap(Il2CppLast.Map.TransportationController transportController, List<FieldEntity> results)
        {
            if (transportController == null) return;

            var sw = Stopwatch.StartNew();
            int entryCount = 0;

            try
            {
                // Access Transportation.ModelList via pointer offsets
                IntPtr transportControllerPtr = transportController.Pointer;
                if (transportControllerPtr == IntPtr.Zero)
                    return;

                // TransportationController.infoData (Transportation) at offset 0x18
                IntPtr infoDataPtr = *(IntPtr*)((byte*)transportControllerPtr + 0x18);
                if (infoDataPtr == IntPtr.Zero)
                    return;

                // Transportation.modelList (Dictionary<int, TransportationInfo>) at offset 0x18
                IntPtr modelListPtr = *(IntPtr*)((byte*)infoDataPtr + 0x18);
                if (modelListPtr == IntPtr.Zero)
                    return;

                // Cast to IL2CPP Dictionary
                var modelListObj = new Il2CppSystem.Object(modelListPtr);
                var modelDict = modelListObj.TryCast<Il2CppSystem.Collections.Generic.Dictionary<int, TransportationInfo>>();
                if (modelDict == null)
                    return;

                // Iterate all vehicles in ModelList
                foreach (var kvp in modelDict)
                {
                    try
                    {
                        var transportInfo = kvp.Value;
                        if (transportInfo == null) continue;

                        bool enabled = transportInfo.Enable;
                        int vehicleType = transportInfo.Type;
                        string messageId = transportInfo.MessageId ?? "";

                        entryCount++;

                        // Get the FieldEntity (MapObject) for this vehicle
                        var mapObject = transportInfo.MapObject;
                        if (mapObject == null)
                            continue;

                        // Filter out non-vehicle types (NONE=0, PLAYER=1, SYMBOL=4, CONTENT=5)
                        // Also skip disabled vehicles (not yet unlocked in story)
                        if (vehicleType > 1 && vehicleType != 4 && vehicleType != 5 && enabled)
                        {
                            if (!VehicleTypeMap.ContainsKey(mapObject))
                            {
                                VehicleTypeMap[mapObject] = (vehicleType, messageId);
                            }

                            // Add vehicle MapObject to results so EntityCache can track it
                            if (!results.Contains(mapObject))
                            {
                                results.Add(mapObject);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[Vehicle] Error processing vehicle: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Vehicle] Error in PopulateVehicleTypeMap: {ex.Message}");
            }
            finally
            {
                sw.Stop();
                MelonLogger.Msg($"[VehicleMap] PopulateVehicleTypeMap completed: {entryCount} entries in {sw.ElapsedMilliseconds}ms");
            }
        }

        /// <summary>
        /// Checks adjacent tiles for landing spots when controlling a ship.
        /// Uses terrain attributes + TransportationController.CheckLandingList to detect
        /// tiles where the ship can dock. This works for both ship-style (pirate ship)
        /// and airship-style landings.
        /// </summary>
        public static void GetNearbyLandingSpots(FieldPlayer player, List<SoundPlayer.Direction> resultBuffer)
        {
            resultBuffer.Clear();
            if (player == null || player.transform == null) return;

            try
            {
                var fieldMap = GameObjectCache.Get<FieldMap>();
                var fieldController = fieldMap?.fieldController;
                if (fieldController == null) return;

                var transportController = fieldController.transportation;
                if (transportController == null)
                    return;

                // Get the current transport ID for CheckLandingList
                int transportId = MoveStateHelper.GetCurrentTransportType();

                // Use the transport controller's ID if available, fall back to MoveStateHelper
                var currentTransportInfo = transportController.CurrentTransportation;
                int checkTransportId = currentTransportInfo?.Id ?? transportId;

                Vector3 pos = player.transform.position;
                float tile = GameConstants.TILE_SIZE;

                // Check each adjacent tile's terrain attribute
                CheckAdjacentLanding(fieldController, transportController, checkTransportId,
                    pos, new Vector3(0, tile, 0), SoundPlayer.Direction.North, resultBuffer);
                CheckAdjacentLanding(fieldController, transportController, checkTransportId,
                    pos, new Vector3(0, -tile, 0), SoundPlayer.Direction.South, resultBuffer);
                CheckAdjacentLanding(fieldController, transportController, checkTransportId,
                    pos, new Vector3(tile, 0, 0), SoundPlayer.Direction.East, resultBuffer);
                CheckAdjacentLanding(fieldController, transportController, checkTransportId,
                    pos, new Vector3(-tile, 0, 0), SoundPlayer.Direction.West, resultBuffer);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[LandingPing] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks a single adjacent tile for landing eligibility using terrain attributes.
        /// A tile is a landing spot if:
        /// 1. CheckLandingList returns true for the tile's attribute (airship-style), OR
        /// 2. The tile's attribute is NOT in the vehicle's OkList (ship-style: can't sail there = land)
        /// </summary>
        private static void CheckAdjacentLanding(
            FieldController fieldController,
            Il2CppLast.Map.TransportationController transportController,
            int transportId,
            Vector3 playerWorldPos,
            Vector3 offset,
            SoundPlayer.Direction direction,
            List<SoundPlayer.Direction> resultBuffer)
        {
            try
            {
                Vector3 adjacentWorldPos = playerWorldPos + offset;
                Vector3 adjacentCellPos = fieldController.ConvertWorldPositionToCellPosition(adjacentWorldPos);
                int attribute = fieldController.GetCellAttribute(new Vector2(adjacentCellPos.x, adjacentCellPos.y));

                bool isInLandingList = transportController.CheckLandingList(transportId, attribute);
                bool isInOkList = transportController.CheckOkList(transportId, attribute);

                // Landing spot detection:
                // - CheckLandingList: explicit landing tiles (e.g., airship landing spots)
                // - NOT in OkList: tile the vehicle can't traverse = land for ships
                // Use either signal as a landing indicator
                if (isInLandingList || !isInOkList)
                {
                    resultBuffer.Add(direction);
                }
            }
            catch
            {
                // Silently skip this direction on error
            }
        }

        public static string GetWalkableDirections(FieldPlayer player, IMapAccessor mapHandle)
        {
            if (player == null || mapHandle == null)
                return "Cannot check directions";

            Vector3 currentPos = player.transform.position;
            float stepSize = GameConstants.TILE_SIZE;

            var directions = new List<string>();

            Vector3 northPos = currentPos + new Vector3(0, stepSize, 0);
            if (CheckPositionWalkable(player, northPos, mapHandle))
                directions.Add("North");

            Vector3 southPos = currentPos + new Vector3(0, -stepSize, 0);
            if (CheckPositionWalkable(player, southPos, mapHandle))
                directions.Add("South");

            Vector3 eastPos = currentPos + new Vector3(stepSize, 0, 0);
            if (CheckPositionWalkable(player, eastPos, mapHandle))
                directions.Add("East");

            Vector3 westPos = currentPos + new Vector3(-stepSize, 0, 0);
            if (CheckPositionWalkable(player, westPos, mapHandle))
                directions.Add("West");

            if (directions.Count == 0)
                return "STUCK - No walkable directions!";

            return string.Join(", ", directions);
        }

        private static bool CheckPositionWalkable(FieldPlayer player, Vector3 position, IMapAccessor mapHandle)
        {
            try
            {
                var fieldMap = GameObjectCache.Get<FieldMap>();
                var fieldController = fieldMap?.fieldController;
                if (fieldController != null)
                {
                    return fieldController.IsCanMoveToDestPosition(player, ref position);
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Result structure for wall proximity detection.
        /// Distance values: -1 = no wall within range, 0 = adjacent/blocked
        /// </summary>
        public struct WallDistances
        {
            public int NorthDist;
            public int SouthDist;
            public int EastDist;
            public int WestDist;

            public WallDistances(int north, int south, int east, int west)
            {
                NorthDist = north;
                SouthDist = south;
                EastDist = east;
                WestDist = west;
            }
        }

        /// <summary>
        /// Gets distance to nearest wall in each cardinal direction (in tiles).
        /// Returns -1 for a direction if no wall adjacent (1 tile away).
        /// </summary>
        public static WallDistances GetNearbyWallsWithDistance(FieldPlayer player)
        {
            if (player == null || player.transform == null)
                return new WallDistances(-1, -1, -1, -1);

            Vector3 pos = player.transform.localPosition;

            return new WallDistances(
                GetWallDistance(player, pos, new Vector3(0, GameConstants.TILE_SIZE, 0)),
                GetWallDistance(player, pos, new Vector3(0, -GameConstants.TILE_SIZE, 0)),
                GetWallDistance(player, pos, new Vector3(GameConstants.TILE_SIZE, 0, 0)),
                GetWallDistance(player, pos, new Vector3(-GameConstants.TILE_SIZE, 0, 0))
            );
        }

        /// <summary>
        /// Gets the distance to a wall in a given direction using pathfinding.
        /// Returns: 0 = adjacent/blocked, -1 = no wall adjacent
        /// Only checks the immediately adjacent tile to reduce confusion from distant walls.
        /// </summary>
        private static int GetWallDistance(FieldPlayer player, Vector3 pos, Vector3 step)
        {
            if (IsAdjacentTileBlocked(player, pos, step))
                return 0;

            return -1;
        }

        /// <summary>
        /// Checks if an adjacent tile is blocked using pathfinding.
        /// More reliable than IsCanMoveToDestPosition for predictive checks.
        /// </summary>
        private static bool IsAdjacentTileBlocked(FieldPlayer player, Vector3 playerPos, Vector3 direction)
        {
            try
            {
                var playerController = GameObjectCache.Get<FieldPlayerController>();
                if (playerController == null)
                {
                    playerController = GameObjectCache.Refresh<FieldPlayerController>();
                    if (playerController == null)
                        return false;
                }

                var mapHandle = playerController.mapHandle;
                if (mapHandle == null)
                    return false;

                int mapWidth = mapHandle.GetCollisionLayerWidth();
                int mapHeight = mapHandle.GetCollisionLayerHeight();

                if (mapWidth <= 0 || mapHeight <= 0 || mapWidth > 10000 || mapHeight > 10000)
                    return false;

                Vector3 startCell = new Vector3(
                    Mathf.FloorToInt(mapWidth * 0.5f + playerPos.x * GameConstants.TILE_SIZE_INVERSE),
                    Mathf.FloorToInt(mapHeight * 0.5f - playerPos.y * GameConstants.TILE_SIZE_INVERSE),
                    player.gameObject.layer - 9
                );

                Vector3 targetPos = playerPos + direction;
                Vector3 destCell = new Vector3(
                    Mathf.FloorToInt(mapWidth * 0.5f + targetPos.x * GameConstants.TILE_SIZE_INVERSE),
                    Mathf.FloorToInt(mapHeight * 0.5f - targetPos.y * GameConstants.TILE_SIZE_INVERSE),
                    startCell.z
                );

                bool playerCollisionState = player._IsOnCollision_k__BackingField;

                // Pre-call log: if Search() hangs, last log line identifies the call
                if (_shouldLogSlowPathfinding)
                {
                    _shouldLogSlowPathfinding = false;
                    MelonLogger.Msg($"[WallTones] Search: ({startCell.x},{startCell.y},{startCell.z}) → ({destCell.x},{destCell.y},{destCell.z})");
                }

                var pathPoints = MapRouteSearcher.Search(mapHandle, startCell, destCell, playerCollisionState);

                bool blocked = pathPoints == null || pathPoints.Count == 0;

                return blocked;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[WallTones] IsAdjacentTileBlocked error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a direction from the player position leads to a map exit entity.
        /// Used to suppress wall tones at map exits, doors, and stairs where
        /// MapRouteSearcher.Search() reports blocked but the tile is actually accessible.
        /// </summary>
        public static bool IsDirectionNearMapExit(Vector3 playerPos, Vector3 direction,
            List<Vector3> mapExitPositions, float tolerance = GameConstants.MAP_EXIT_TOLERANCE)
        {
            if (mapExitPositions == null || mapExitPositions.Count == 0)
                return false;

            Vector3 adjacentTilePos = playerPos + direction;

            foreach (var exitPos in mapExitPositions)
            {
                float dx = adjacentTilePos.x - exitPos.x;
                float dy = adjacentTilePos.y - exitPos.y;
                float dist2D = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist2D <= tolerance)
                    return true;
            }

            return false;
        }

        public static PathInfo FindPathTo(Vector3 playerWorldPos, Vector3 targetWorldPos, IMapAccessor mapHandle, FieldPlayer player = null)
            => FindPathTo(playerWorldPos, targetWorldPos, mapHandle, player, PathSearchMode.Quick);

        public static PathInfo FindPathTo(Vector3 playerWorldPos, Vector3 targetWorldPos, IMapAccessor mapHandle,
            FieldPlayer player, PathSearchMode mode)
        {
            var pathInfo = new PathInfo { Success = false };

            if (mapHandle == null)
            {
                pathInfo.ErrorMessage = "Map handle not available";
                return pathInfo;
            }

            // Vehicles get their own searcher: MapRouteSearcher models walking collision, so
            // it cannot see that a ship crosses ocean or an airship crosses mountains, and
            // its 64x64 window puts the far side of a world map out of reach regardless.
            // Sync first so the decision is never made on a cached state the game has moved
            // past (same failsafe the V key uses).
            MoveStateHelper.SyncWithActualGameState();
            if (!RoutingAdapter.IsOnFoot())
            {
                var vehiclePath = TryVehicleRoute(playerWorldPos, targetWorldPos, mapHandle, player);
                if (vehiclePath != null)
                    return Finish(vehiclePath);
                // Fall through: no grid, no transport id, or the searcher declined. The
                // game's searcher is still better than nothing.
            }

            try
            {
                int mapWidth = mapHandle.GetCollisionLayerWidth();
                int mapHeight = mapHandle.GetCollisionLayerHeight();
                
                Vector3 startCell = new Vector3(
                    Mathf.FloorToInt(mapWidth * 0.5f + playerWorldPos.x * GameConstants.TILE_SIZE_INVERSE),
                    Mathf.FloorToInt(mapHeight * 0.5f - playerWorldPos.y * GameConstants.TILE_SIZE_INVERSE),
                    0
                );

                Vector3 destCell = new Vector3(
                    Mathf.FloorToInt(mapWidth * 0.5f + targetWorldPos.x * GameConstants.TILE_SIZE_INVERSE),
                    Mathf.FloorToInt(mapHeight * 0.5f - targetWorldPos.y * GameConstants.TILE_SIZE_INVERSE),
                    0
                );
                
                if (player != null)
                {
                    float layerZ = player.gameObject.layer - 9;
                    startCell.z = layerZ;
                }
                
                Il2CppSystem.Collections.Generic.List<Vector3> pathPoints = null;

                if (player != null)
                {
                    bool playerCollisionState = player._IsOnCollision_k__BackingField;
                    
                    // Try pathfinding with different destination layers until one succeeds
                    for (int tryDestZ = 2; tryDestZ >= 0; tryDestZ--)
                    {
                        destCell.z = tryDestZ;
                        pathPoints = SafeSearch(mapHandle, startCell, destCell, playerCollisionState);

                        if (pathPoints != null && pathPoints.Count > 0)
                        {
                            break;
                        }
                    }

                    // If direct path failed, try adjacent tiles
                    if (pathPoints == null || pathPoints.Count == 0)
                    {
                        // Try adjacent tiles (one cell = TILE_SIZE units in world space)
                        // Try all 8 directions: cardinals first, then diagonals
                        float t = GameConstants.TILE_SIZE;
                        Vector3[] adjacentOffsets = new Vector3[] {
                            new Vector3(0, t, 0),    // north
                            new Vector3(t, 0, 0),    // east
                            new Vector3(0, -t, 0),   // south
                            new Vector3(-t, 0, 0),   // west
                            new Vector3(t, t, 0),    // northeast
                            new Vector3(t, -t, 0),   // southeast
                            new Vector3(-t, -t, 0),  // southwest
                            new Vector3(-t, t, 0)    // northwest
                        };

                        foreach (var offset in adjacentOffsets)
                        {
                            Vector3 adjacentTargetWorld = targetWorldPos + offset;

                            // Convert to cell coordinates
                            Vector3 adjacentDestCell = new Vector3(
                                Mathf.FloorToInt(mapWidth * 0.5f + adjacentTargetWorld.x * GameConstants.TILE_SIZE_INVERSE),
                                Mathf.FloorToInt(mapHeight * 0.5f - adjacentTargetWorld.y * GameConstants.TILE_SIZE_INVERSE),
                                0
                            );

                            // Try pathfinding with different layers
                            for (int tryDestZ = 2; tryDestZ >= 0; tryDestZ--)
                            {
                                adjacentDestCell.z = tryDestZ;
                                pathPoints = SafeSearch(mapHandle, startCell, adjacentDestCell, playerCollisionState);

                                if (pathPoints != null && pathPoints.Count > 0)
                                {
                                    break;
                                }
                            }

                            // If we found a path, stop trying other adjacent tiles
                            if (pathPoints != null && pathPoints.Count > 0)
                                break;
                        }
                    }

                    // Don't fall back to collision=false - if we can't find a valid path, report failure
                    // (collision=false would route through walls, which is misleading)
                }
                else
                {
                    try
                    {
                        pathPoints = MapRouteSearcher.SearchSimple(mapHandle, startCell, destCell);
                    }
                    catch (Exception ex)
                    {
                        LogSearchThrewOnce(ex);
                        pathPoints = null;
                    }
                }

                if (pathPoints == null || pathPoints.Count == 0)
                {
                    // The plain search came up empty. Only now — and only for an explicit
                    // player action — is it worth chaining several searches together to
                    // reach past the game's ~31.5-tile window. Quick mode (the entity filter,
                    // the beacon loop) never pays for this.
                    if (mode == PathSearchMode.Full)
                    {
                        var chained = TryChainedRoute(playerWorldPos, targetWorldPos, mapHandle, player);
                        if (chained != null)
                            return Finish(chained);
                    }

                    return pathInfo;
                }
                
                var rawPath = new List<Vector3>();

                for (int i = 0; i < pathPoints.Count; i++)
                {
                    rawPath.Add(pathPoints[i]);
                }

                // Settle which coordinate space the game's searcher works in, then hand the
                // caller world space regardless — DirectionHelper reads +y as north, which
                // is only true in world space.
                PathSpaceNormalizer.Detect(startCell, rawPath, mapWidth, mapHeight);
                pathInfo.WorldPath = PathSpaceNormalizer.ToWorld(rawPath, mapWidth, mapHeight);

                pathInfo.Success = true;
                pathInfo.StepCount = pathPoints.Count > 0 ? pathPoints.Count - 1 : 0;

                return Finish(pathInfo);
            }
            catch (System.Exception ex)
            {
                // A silent catch here is what hid the breadcrumb subsystem for an entire
                // test session: it swallowed the search exception and returned before the
                // long-range fallback could run, leaving nothing in the log at all. Keep the
                // backstop, but never let it be quiet again.
                pathInfo.ErrorMessage = $"Pathfinding error: {ex.Message}";

                if (!loggedOuterCatch)
                {
                    loggedOuterCatch = true;
                    MelonLogger.Warning($"[Routing] Route search aborted ({ex.GetType().Name}: {ex.Message}); " +
                                        "this target will report no path and no fallback will run");
                }

                return pathInfo;
            }
        }
        
        private static bool loggedSearchThrew;
        private static bool loggedOuterCatch;

        /// <summary>
        /// `MapRouteSearcher.Search`, with a throw turned into "no path found".
        ///
        /// The game's searcher does not return an empty list for a destination outside its
        /// 64x64 window — it raises `MapRouteSearchException` (dump.cs:262688). Before this
        /// wrapper existed that exception escaped all the way to the method's outer catch,
        /// which returned silently, so the long-range fallback below it was unreachable and
        /// the whole breadcrumb subsystem never ran once. A far target simply said "no path"
        /// with nothing in the log to say why.
        /// </summary>
        private static Il2CppSystem.Collections.Generic.List<Vector3> SafeSearch(
            IMapAccessor mapHandle, Vector3 startCell, Vector3 destCell, bool collisionEnabled)
        {
            try
            {
                return MapRouteSearcher.Search(mapHandle, startCell, destCell, collisionEnabled);
            }
            catch (Exception ex)
            {
                LogSearchThrewOnce(ex);
                return null;
            }
        }

        /// <summary>
        /// One-shot: this fires from the beacon loop and once per entity in the pathfinding
        /// filter, so an ungated line would flood the log. Names the consequence rather than
        /// the error, per the TryPatch convention in docs/debug.md.
        /// </summary>
        private static void LogSearchThrewOnce(Exception ex)
        {
            if (loggedSearchThrew) return;
            loggedSearchThrew = true;
            MelonLogger.Msg($"[Routing] Out-of-window search threw ({ex.GetType().Name}: {ex.Message}); " +
                            "treating as no-path so long-range fallback can run");
        }

        /// <summary>
        /// Renders Legs and Description from WorldPath. Every successful route — vehicle,
        /// breadcrumb, or the game's own searcher — leaves through here, so all three sound
        /// identical and none of the searchers has to build a string.
        /// </summary>
        private static PathInfo Finish(PathInfo pathInfo)
        {
            if (pathInfo == null) return null;

            if (pathInfo.Success)
            {
                pathInfo.Legs = BuildLegs(pathInfo.WorldPath);
                pathInfo.Description = DescribeRoute(pathInfo);
            }

            return pathInfo;
        }

        /// <summary>
        /// Runs the breadcrumb chainer for an on-foot target the plain search could not
        /// reach. Returns null when chaining does not apply, leaving the caller's own
        /// failure result intact.
        /// </summary>
        private static PathInfo TryChainedRoute(Vector3 playerWorldPos, Vector3 targetWorldPos,
            IMapAccessor mapHandle, FieldPlayer player)
        {
            try
            {
                return BreadcrumbRouteChainer.TryChain(playerWorldPos, targetWorldPos, mapHandle, player);
            }
            catch (Exception ex)
            {
                RoutingAdapter.WarnOnce("chain-entry", $"Chained route failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Routes with the vehicle searcher. Returns null when it cannot run at all (no
        /// field controller, no transport id, grid not built) so the caller can fall back
        /// to the game's searcher rather than reporting a spurious failure.
        /// </summary>
        private static PathInfo TryVehicleRoute(Vector3 playerWorldPos, Vector3 targetWorldPos,
            IMapAccessor mapHandle, FieldPlayer player)
        {
            try
            {
                var fieldMap = GameObjectCache.Get<FieldMap>();
                var fieldController = fieldMap?.fieldController;
                var transportController = fieldController?.transportation;
                if (fieldController == null || transportController == null)
                    return null;

                var userData = Il2CppLast.Management.UserDataManager.Instance();
                if (userData == null) return null;
                int mapId = userData.CurrentMapId;

                // Terrain attributes govern movement on world maps. Inside towns and
                // dungeons it is layers and collision entities that decide, and the
                // attribute grid models neither — so a route built from it there would be
                // confidently wrong. The player can still be mounted in an interior (riding
                // Boko in before the scripted dismount), so this needs an explicit guard
                // rather than relying on vehicle state alone.
                if (!RoutingAdapter.IsWorldMap(mapId))
                    return null;

                if (!RoutingAdapter.TryGetCurrentTransportId(transportController, out int transportId))
                    return null;

                if (!VehicleRouteSearcher.EnsureGrid(fieldController, mapHandle, mapId))
                    return null;

                return VehicleRouteSearcher.FindPath(fieldController, mapHandle, transportController,
                    mapId, transportId, playerWorldPos, targetWorldPos);
            }
            catch (Exception ex)
            {
                RoutingAdapter.WarnOnce("vehicle-route", $"Vehicle route failed, falling back: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Maximum legs spoken before the route is summarised. A real world-map route hugs
        /// terrain and runs to dozens of short legs, which is unusable as speech. The player
        /// walks what they heard and presses the key again — the search re-runs from their
        /// new position, so there is no stored route and nothing to invalidate.
        /// </summary>
        private const int MAX_SPOKEN_LEGS = 6;

        /// <summary>
        /// Collapses a cell-by-cell path into straight runs. Consecutive steps in the same
        /// direction merge, which is what makes a route stitched from several breadcrumb
        /// hops read as "north 89" rather than "north 24, north 30, north 35".
        /// </summary>
        internal static List<RouteLeg> BuildLegs(List<Vector3> worldPath)
        {
            var legs = new List<RouteLeg>();
            if (worldPath == null || worldPath.Count < 2)
                return legs;

            Vector3 currentDir = Vector3.zero;
            int stepCount = 0;

            for (int i = 1; i < worldPath.Count; i++)
            {
                Vector3 dir = worldPath[i] - worldPath[i - 1];
                dir.Normalize();

                if (Vector3.Distance(dir, currentDir) < 0.1f)
                {
                    stepCount++;
                }
                else
                {
                    if (stepCount > 0)
                        legs.Add(new RouteLeg(GetCardinalDirectionName(currentDir), stepCount));

                    currentDir = dir;
                    stepCount = 1;
                }
            }

            if (stepCount > 0)
                legs.Add(new RouteLeg(GetCardinalDirectionName(currentDir), stepCount));

            return legs;
        }

        /// <summary>
        /// The single place route text is produced. Both searchers feed it, so a vehicle
        /// route and a breadcrumb route sound the same, and neither searcher has to know
        /// anything about language.
        /// </summary>
        internal static string DescribeRoute(PathInfo pathInfo)
        {
            if (pathInfo?.Legs == null || pathInfo.Legs.Count == 0)
                return T("No movement needed");

            var parts = new List<string>();
            int spoken = Mathf.Min(MAX_SPOKEN_LEGS, pathInfo.Legs.Count);

            for (int i = 0; i < spoken; i++)
                parts.Add($"{pathInfo.Legs[i].Direction} {pathInfo.Legs[i].Steps}");

            int remaining = pathInfo.Legs.Count - spoken;
            if (remaining > 0)
                parts.Add(string.Format(T("and {0} more"), remaining));

            string text = string.Join(", ", parts);

            // Approach clause: the route stops short because the target's own tile is not
            // one this vehicle can occupy. Saying how far short turns a confusing "you have
            // arrived somewhere near it" into something actionable.
            if (pathInfo.IsApproximate)
            {
                var offset = pathInfo.ApproachOffset;
                int tiles = Mathf.RoundToInt(
                    (Mathf.Abs(offset.x) + Mathf.Abs(offset.y)) * GameConstants.TILE_SIZE_INVERSE);

                if (tiles > 0)
                {
                    string dir = DirectionHelper.GetCompassDirectionFromVector(
                        new Vector3(offset.x, offset.y, 0f));
                    text += ", " + string.Format(T("target {0} {1}"), tiles, dir);
                }

                if (pathInfo.IsLandingSpot)
                    text += ", " + T("landing spot");
            }

            return text;
        }

        private static string DescribePath(List<Vector3> worldPath)
        {
            var legs = BuildLegs(worldPath);
            if (legs.Count == 0)
                return T("No movement needed");

            var segments = new List<string>();
            foreach (var leg in legs)
                segments.Add($"{leg.Direction} {leg.Steps}");

            return string.Join(", ", segments);
        }
        
        private static string GetCardinalDirectionName(Vector3 dir)
        {
            return FFV_ScreenReader.Utils.DirectionHelper.GetCompassDirectionFromVector(dir);
        }
    }
    
    /// <summary>
    /// How hard a caller is willing to work for a route.
    ///
    /// Quick is the default so that any call site added later is cheap by construction.
    /// Only the two explicit player actions — pathfind-to-waypoint and announce-entity —
    /// opt into Full; the per-entity filter and the 2 s beacon loop must never pay for
    /// breadcrumb chaining.
    /// </summary>
    public enum PathSearchMode
    {
        Quick,
        Full
    }

    /// <summary>
    /// Why a route was not produced. Nothing speaks these — every failure says exactly what
    /// the mod said before long-range routing existed — but the chainer sets and logs them,
    /// and the distinction is what separates a real finding from a tuning problem when
    /// reading a log.
    /// </summary>
    public enum RouteFailure
    {
        None = 0,
        /// <summary>Search space exhausted. Within the model, there is genuinely no route.</summary>
        NoPathProved,
        /// <summary>A budget or bound fired first. Reachability is unknown.</summary>
        StoppedEarly
    }

    /// <summary>One straight run of the route: a direction and how many steps to take.</summary>
    public struct RouteLeg
    {
        public string Direction;
        public int Steps;

        public RouteLeg(string direction, int steps)
        {
            Direction = direction;
            Steps = steps;
        }
    }

    public class PathInfo
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int StepCount { get; set; }
        public string Description { get; set; }
        public System.Collections.Generic.List<Vector3> WorldPath { get; set; }

        /// <summary>Structured, untruncated. Description is rendered from this.</summary>
        public System.Collections.Generic.List<RouteLeg> Legs { get; set; }

        /// <summary>Route reaches a stand-in tile near the target, not the target itself.</summary>
        public bool IsApproximate { get; set; }

        /// <summary>Target minus the tile actually reached, in world units. Only set when approximate.</summary>
        public Vector2 ApproachOffset { get; set; }

        /// <summary>The stand-in tile is a valid landing/dock spot for the current vehicle.</summary>
        public bool IsLandingSpot { get; set; }

        /// <summary>Diagnostic only — never spoken. See RouteFailure.</summary>
        public RouteFailure Failure { get; set; }
    }
}
