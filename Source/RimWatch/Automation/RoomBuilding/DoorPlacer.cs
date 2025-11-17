using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.RoomBuilding
{
    /// <summary>
    /// Handles intelligent door placement for rooms.
    /// Considers pathfinding, accessibility, and colony layout.
    /// </summary>
    public static class DoorPlacer
    {
        /// <summary>
        /// Places door blueprints at specified cells.
        /// </summary>
        public static int PlaceDoors(Map map, List<IntVec3> doorCells, RoomPlanner.RoomRole roomRole, string logLevel = "Moderate")
        {
            int doorsPlaced = 0;

            try
            {
                if (doorCells == null || doorCells.Count == 0)
                {
                    RimWatchLogger.Warning("DoorPlacer: No door cells provided");
                    return 0;
                }

                // Select appropriate door type
                ThingDef doorDef = SelectDoorType(map, roomRole, logLevel);
                if (doorDef == null)
                {
                    RimWatchLogger.Error("DoorPlacer: Could not find door ThingDef!");
                    return 0;
                }

                ThingDef? stuffDef = RimWatch.Automation.BuildingPlacement.StuffSelector.DefaultNonSteelStuffFor(doorDef, map);

                // Place door blueprints
                foreach (IntVec3 cell in doorCells)
                {
                    // Check if already has door or blueprint
                    if (HasDoorOrBlueprint(map, cell))
                    {
                        if (logLevel == "Debug")
                            RimWatchLogger.Debug($"DoorPlacer: Skipping ({cell.x}, {cell.z}) - already has door/blueprint");
                        continue;
                    }

                    // Validate door placement
                    if (!IsValidDoorLocation(map, cell, logLevel))
                    {
                        if (logLevel == "Verbose" || logLevel == "Debug")
                            RimWatchLogger.Debug($"DoorPlacer: Invalid door location at ({cell.x}, {cell.z})");
                        continue;
                    }

                    // Place blueprint via Designator with rotation probing
                    bool success = RimWatch.Automation.BuildingPlacement.BuildPlacer.TryPlaceWithBestRotation(map, doorDef, cell, stuffDef, logLevel);
                    if (success)
                    {
                        doorsPlaced++;
                        if (logLevel == "Verbose" || logLevel == "Debug")
                            RimWatchLogger.Debug($"DoorPlacer: Placed {doorDef.label} at ({cell.x}, {cell.z})");
                    }
                    else if (logLevel == "Debug")
                    {
                        RimWatchLogger.Debug($"DoorPlacer: Failed to place door at ({cell.x}, {cell.z})");
                    }
                }

                if (doorsPlaced > 0)
                {
                    RimWatchLogger.Info($"🚪 DoorPlacer: Placed {doorsPlaced} {doorDef.label} blueprint(s)");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DoorPlacer: Error in PlaceDoors", ex);
            }

            return doorsPlaced;
        }

        /// <summary>
        /// Selects appropriate door type based on room role and game stage.
        /// </summary>
        private static ThingDef? SelectDoorType(Map map, RoomPlanner.RoomRole roomRole, string logLevel)
        {
            try
            {
                int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;

                // Special doors for specific rooms
                switch (roomRole)
                {
                    case RoomPlanner.RoomRole.Freezer:
                        // Autodoor for freezer (prevents temperature loss)
                        ThingDef? autodoor = DefDatabase<ThingDef>.GetNamedSilentFail("Autodoor");
                        if (autodoor != null && IsResearchedOrNoResearch(autodoor))
                        {
                            if (logLevel == "Debug")
                                RimWatchLogger.Debug("DoorPlacer: Selected autodoor for freezer");
                            return autodoor;
                        }
                        break;

                    case RoomPlanner.RoomRole.Prison:
                        // Prison door for security
                        // Note: RimWorld doesn't have special prison doors by default
                        // Just use regular door
                        break;
                }

                // Tech progression: Autodoor > Door
                if (colonistCount >= 5)
                {
                    ThingDef? autodoor = DefDatabase<ThingDef>.GetNamedSilentFail("Autodoor");
                    if (autodoor != null && IsResearchedOrNoResearch(autodoor))
                    {
                        if (logLevel == "Debug")
                            RimWatchLogger.Debug("DoorPlacer: Selected autodoor (mid-game)");
                        return autodoor;
                    }
                }

                // Default: regular door
                ThingDef? door = DefDatabase<ThingDef>.GetNamedSilentFail("Door");
                if (door != null)
                {
                    if (logLevel == "Debug")
                        RimWatchLogger.Debug("DoorPlacer: Selected regular door");
                    return door;
                }

                RimWatchLogger.Error("DoorPlacer: No door ThingDef found in database!");
                return null;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DoorPlacer: Error selecting door type", ex);
                return null;
            }
        }

        /// <summary>
        /// Validates if a cell is suitable for door placement.
        /// </summary>
        private static bool IsValidDoorLocation(Map map, IntVec3 cell, string logLevel)
        {
            try
            {
                // Basic checks
                if (!cell.InBounds(map))
                    return false;

                if (!cell.Standable(map))
                    return false;

                // Check if cell is fogged
                if (map.fogGrid.IsFogged(cell))
                    return false;

                // ✅ CRITICAL: Check for items blocking door location
                List<Thing> things = cell.GetThingList(map);
                foreach (Thing item in things)
                {
                    if (item.def.category == ThingCategory.Item && item.def.EverHaulable)
                    {
                        if (logLevel == "Debug")
                            RimWatchLogger.Debug($"DoorPlacer: Cell ({cell.x}, {cell.z}) blocked by item: {item.def.label}");
                        return false; // Don't place door on items!
                    }
                }

                // Check if in home area (prefer building in home area)
                Area? homeArea = map.areaManager.Home;
                bool inHomeArea = homeArea != null && homeArea[cell];

                if (!inHomeArea && logLevel == "Debug")
                {
                    RimWatchLogger.Debug($"DoorPlacer: Cell ({cell.x}, {cell.z}) not in home area");
                }

                // Check adjacent cells - doors should connect passable areas
                int passableNeighbors = 0;
                foreach (IntVec3 neighbor in GenAdj.CardinalDirections.Select(d => cell + d))
                {
                    if (neighbor.InBounds(map) && neighbor.Standable(map))
                    {
                        passableNeighbors++;
                    }
                }

                // Door should have at least 2 passable neighbors
                if (passableNeighbors < 2)
                {
                    if (logLevel == "Debug")
                        RimWatchLogger.Debug($"DoorPlacer: Cell ({cell.x}, {cell.z}) has insufficient passable neighbors: {passableNeighbors}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"DoorPlacer: Error validating location ({cell.x}, {cell.z}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a cell already has a door or door blueprint.
        /// </summary>
        private static bool HasDoorOrBlueprint(Map map, IntVec3 cell)
        {
            try
            {
                // Check for existing door
                Building? existingBuilding = cell.GetFirstBuilding(map);
                if (existingBuilding != null && existingBuilding.def.defName.Contains("Door"))
                    return true;

                // Check for door blueprint
                List<Thing> blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint);
                foreach (Thing thing in blueprints)
                {
                    if (thing.Position == cell && thing is Blueprint bp)
                    {
                        if (bp.def.entityDefToBuild is ThingDef thingDef && thingDef.defName.Contains("Door"))
                            return true;
                    }
                }

                // Check for door frame under construction
                List<Thing> frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame);
                foreach (Thing thing in frames)
                {
                    if (thing.Position == cell && thing is Frame frame)
                    {
                        if (frame.def.entityDefToBuild is ThingDef thingDef && thingDef.defName.Contains("Door"))
                            return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"DoorPlacer: Error checking door at ({cell.x}, {cell.z}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a building's required research is completed (or has no research requirement).
        /// </summary>
        private static bool IsResearchedOrNoResearch(ThingDef def)
        {
            try
            {
                if (def.researchPrerequisites == null || def.researchPrerequisites.Count == 0)
                    return true;

                foreach (ResearchProjectDef research in def.researchPrerequisites)
                {
                    if (!research.IsFinished)
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Finds optimal door location for connecting a room to existing base.
        /// Returns the wall cell closest to the base center.
        /// </summary>
        public static IntVec3 FindOptimalDoorLocation(Map map, List<IntVec3> wallCells, string logLevel = "Moderate")
        {
            try
            {
                if (wallCells == null || wallCells.Count == 0)
                    return IntVec3.Invalid;

                // Get base center
                List<Building> buildings = map.listerBuildings.allBuildingsColonist.ToList();
                IntVec3 baseCenter = map.Center;

                if (buildings.Count > 0)
                {
                    int avgX = (int)buildings.Average(b => b.Position.x);
                    int avgZ = (int)buildings.Average(b => b.Position.z);
                    baseCenter = new IntVec3(avgX, 0, avgZ);
                }

                // Find wall cell closest to base center
                IntVec3 bestCell = wallCells
                    .OrderBy(cell => cell.DistanceTo(baseCenter))
                    .FirstOrDefault();

                if (logLevel == "Verbose" || logLevel == "Debug")
                {
                    RimWatchLogger.Debug($"DoorPlacer: Optimal door location: ({bestCell.x}, {bestCell.z}), distance to base: {bestCell.DistanceTo(baseCenter):F1}");
                }

                return bestCell;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("DoorPlacer: Error finding optimal door location", ex);
                return IntVec3.Invalid;
            }
        }

        /// <summary>
        /// Checks if colony has enough resources to build doors.
        /// </summary>
        public static bool HasEnoughMaterials(Map map, int doorCount)
        {
            try
            {
                // Doors cost ~25 wood/steel each
                int requiredAmount = doorCount * 25;

                // Check for wood
                int woodAvailable = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                    .Where(t => t.def.defName == "WoodLog")
                    .Sum(t => t.stackCount);

                if (woodAvailable >= requiredAmount)
                    return true;

                // Check for steel
                int steelAvailable = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                    .Where(t => t.def.defName == "Steel")
                    .Sum(t => t.stackCount);

                return steelAvailable >= requiredAmount;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"DoorPlacer: Error checking materials: {ex.Message}");
                return false;
            }
        }
    }
}

