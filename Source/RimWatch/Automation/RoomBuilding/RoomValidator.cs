using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.RoomBuilding
{
    /// <summary>
    /// Validates room placement locations for safety, terrain, and suitability.
    /// </summary>
    public static class RoomValidator
    {
        /// <summary>
        /// Validates a complete room plan.
        /// Checks all cells for safety, terrain, and obstacles.
        /// </summary>
        public static bool ValidateRoomPlan(Map map, RoomPlanner.RoomPlan plan, string logLevel = "Moderate")
        {
            try
            {
                if (plan == null)
                {
                    RimWatchLogger.Warning("RoomValidator: Null room plan provided");
                    return false;
                }

                // Validate all wall cells
                foreach (IntVec3 cell in plan.WallCells)
                {
                    if (!ValidateCell(map, cell, CellType.Wall, logLevel))
                    {
                        plan.RejectionReason = $"Invalid wall cell at ({cell.x}, {cell.z}) - {plan.RejectionReason}";
                        RimWatchLogger.Warning($"RoomValidator: Wall validation failed at ({cell.x}, {cell.z}): {plan.RejectionReason}");
                        return false;
                    }
                }

                // Validate all floor cells
                foreach (IntVec3 cell in plan.FloorCells)
                {
                    if (!ValidateCell(map, cell, CellType.Floor, logLevel))
                    {
                        plan.RejectionReason = $"Invalid floor cell at ({cell.x}, {cell.z})";
                        return false;
                    }
                }

                // Validate door cells
                foreach (IntVec3 cell in plan.DoorCells)
                {
                    if (!ValidateCell(map, cell, CellType.Door, logLevel))
                    {
                    plan.RejectionReason = $"Invalid door cell at ({cell.x}, {cell.z})";
                        return false;
                    }
                }

                // Check overall safety
                if (!IsSafeArea(map, plan.Origin, plan.Size, logLevel))
                {
                    plan.RejectionReason = "Area not safe (enemies nearby or hostile territory)";
                    return false;
                }

                // Check terrain suitability
                if (!HasSuitableTerrain(map, plan.Origin, plan.Size, logLevel))
                {
                    plan.RejectionReason = "Terrain not suitable (water, steep slopes, or impassable)";
                    return false;
                }

                if (logLevel == "Verbose" || logLevel == "Debug")
                {
                    RimWatchLogger.Debug($"RoomValidator: ✓ Room plan validated at ({plan.Origin.x}, {plan.Origin.z})");
                }

                return true;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("RoomValidator: Error validating room plan", ex);
                plan.RejectionReason = $"Validation error: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Cell types for validation.
        /// </summary>
        private enum CellType
        {
            Wall,
            Floor,
            Door
        }

        /// <summary>
        /// Validates a single cell for a specific purpose.
        /// </summary>
        private static bool ValidateCell(Map map, IntVec3 cell, CellType type, string logLevel)
        {
            try
            {
                // Basic checks
                if (!cell.InBounds(map))
                {
                    if (logLevel == "Debug")
                        RimWatchLogger.Debug($"RoomValidator: Cell ({cell.x}, {cell.z}) out of bounds");
                    return false;
                }

                // Check fog of war
                if (map.fogGrid.IsFogged(cell))
                {
                    RimWatchLogger.Debug($"RoomValidator: Cell ({cell.x}, {cell.z}) is fogged");
                    return false;
                }

                // Check standability (for floors, not for walls)
                if (type == CellType.Floor || type == CellType.Door)
                {
                    if (!cell.Standable(map))
                    {
                        RimWatchLogger.Debug($"RoomValidator: Cell ({cell.x}, {cell.z}) not standable (type={type})");
                        return false;
                    }
                }

                // Check terrain
                TerrainDef terrain = cell.GetTerrain(map);
                if (terrain == null)
                {
                    RimWatchLogger.Debug($"RoomValidator: Cell ({cell.x}, {cell.z}) has null terrain");
                    return false;
                }

                // Water/lava is not suitable
                if (terrain.IsWater || terrain.defName.Contains("Lava"))
                {
                    RimWatchLogger.Debug($"RoomValidator: Cell ({cell.x}, {cell.z}) has water/lava terrain: {terrain.label}");
                    return false;
                }

                // ✅ NEW: Check for growing zones - don't build on farms!
                Zone zone = map.zoneManager.ZoneAt(cell);
                if (zone is Zone_Growing)
                {
                    RimWatchLogger.Debug($"RoomValidator: Cell ({cell.x}, {cell.z}) is in growing zone");
                    return false;
                }

                // Check for existing impassable structures
                Building? existingBuilding = cell.GetFirstBuilding(map);
                if (existingBuilding != null)
                {
                    // Natural rock is OK for walls (we can mine it)
                    bool isNaturalRock = existingBuilding.def.building?.isNaturalRock == true;
                    
                    if (!isNaturalRock)
                    {
                        RimWatchLogger.Debug($"RoomValidator: Cell ({cell.x}, {cell.z}) has existing building: {existingBuilding.def.label}");
                        return false;
                    }
                }

                // Check for blocking items
                List<Thing> things = cell.GetThingList(map);
                foreach (Thing thing in things)
                {
                    // ✅ CRITICAL: Don't build walls on items!
                    if (thing.def.category == ThingCategory.Item && thing.def.EverHaulable)
                    {
                        RimWatchLogger.Debug($"RoomValidator: Cell ({cell.x}, {cell.z}) has item: {thing.def.label}");
                        return false; // Don't build on items!
                    }
                    
                    if (thing.def.passability == Traversability.Impassable && !(thing is Building))
                    {
                        RimWatchLogger.Debug($"RoomValidator: Cell ({cell.x}, {cell.z}) blocked by {thing.def.label}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"RoomValidator: Error validating cell ({cell.x}, {cell.z}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if an area is safe for building.
        /// </summary>
        private static bool IsSafeArea(Map map, IntVec3 origin, IntVec2 size, string logLevel)
        {
            try
            {
                IntVec3 center = origin + new IntVec3(size.x / 2, 0, size.z / 2);

                // Check if in home area (preferred but not required)
                Area? homeArea = map.areaManager.Home;
                bool inHomeArea = homeArea != null && homeArea[center];

                // Check for nearby enemies
                List<Pawn> hostilePawns = map.mapPawns.AllPawns
                    .Where(p => p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer))
                    .ToList();

                foreach (Pawn enemy in hostilePawns)
                {
                    float distance = enemy.Position.DistanceTo(center);
                    if (distance < 20f) // Enemy within 20 cells
                    {
                        if (logLevel == "Verbose" || logLevel == "Debug")
                            RimWatchLogger.Debug($"RoomValidator: Enemy {enemy.LabelShort} too close ({distance:F1} cells)");
                        return false;
                    }
                }

                // Check for dangerous structures (e.g., ancient dangers, mechanoid clusters)
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (thing.def.defName.Contains("AncientDanger") ||
                        thing.def.defName.Contains("Mechanoid") ||
                        thing.def.defName.Contains("Turret"))
                    {
                        float distance = thing.Position.DistanceTo(center);
                        if (distance < 15f)
                        {
                            if (logLevel == "Verbose" || logLevel == "Debug")
                                RimWatchLogger.Debug($"RoomValidator: Dangerous structure {thing.def.label} too close");
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"RoomValidator: Error checking safety: {ex.Message}");
                return true; // Default to safe if check fails
            }
        }

        /// <summary>
        /// Checks if terrain is suitable for room construction.
        /// </summary>
        private static bool HasSuitableTerrain(Map map, IntVec3 origin, IntVec2 size, string logLevel)
        {
            try
            {
                int waterCells = 0;
                int totalCells = size.x * size.z;

                for (int x = 0; x < size.x; x++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        IntVec3 cell = origin + new IntVec3(x, 0, z);

                        if (!cell.InBounds(map))
                            return false;

                        TerrainDef terrain = cell.GetTerrain(map);
                        if (terrain == null)
                            return false;

                        // Count water cells
                        if (terrain.IsWater || terrain.defName.Contains("Lava"))
                        {
                            waterCells++;
                        }
                    }
                }

                // Allow up to 10% water (can be bridged)
                float waterPercentage = (float)waterCells / totalCells;
                if (waterPercentage > 0.1f)
                {
                    if (logLevel == "Verbose" || logLevel == "Debug")
                        RimWatchLogger.Debug($"RoomValidator: Too much water in area ({waterPercentage * 100:F1}%)");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"RoomValidator: Error checking terrain: {ex.Message}");
                return true; // Default to suitable if check fails
            }
        }

        /// <summary>
        /// Checks if room location has good access to existing base.
        /// </summary>
        public static int GetAccessibilityScore(Map map, IntVec3 roomOrigin, IntVec2 roomSize)
        {
            try
            {
                IntVec3 roomCenter = roomOrigin + new IntVec3(roomSize.x / 2, 0, roomSize.z / 2);

                // Get base center
                List<Building> buildings = map.listerBuildings.allBuildingsColonist.ToList();
                if (buildings.Count == 0)
                    return 50; // Neutral score if no base exists

                int avgX = (int)buildings.Average(b => b.Position.x);
                int avgZ = (int)buildings.Average(b => b.Position.z);
                IntVec3 baseCenter = new IntVec3(avgX, 0, avgZ);

                float distance = roomCenter.DistanceTo(baseCenter);

                // Score: closer is better
                // 0-10 cells = 100 points
                // 10-20 cells = 80 points
                // 20-30 cells = 60 points
                // 30+ cells = 40 points

                if (distance <= 10f) return 100;
                if (distance <= 20f) return 80;
                if (distance <= 30f) return 60;
                return 40;
            }
            catch
            {
                return 50; // Neutral score on error
            }
        }

        /// <summary>
        /// Checks if room is too close to existing rooms.
        /// </summary>
        public static bool IsTooCloseToExistingRooms(Map map, IntVec3 origin, IntVec2 size, int minDistance = 2)
        {
            try
            {
                // Get all existing walls
                List<Building> walls = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.defName.Contains("Wall"))
                    .ToList();

                foreach (Building wall in walls)
                {
                    // Check if any corner of the new room is too close to existing walls
                    IntVec3[] corners = new IntVec3[]
                    {
                        origin,
                        origin + new IntVec3(size.x - 1, 0, 0),
                        origin + new IntVec3(0, 0, size.z - 1),
                        origin + new IntVec3(size.x - 1, 0, size.z - 1)
                    };

                    foreach (IntVec3 corner in corners)
                    {
                        if (corner.DistanceTo(wall.Position) < minDistance)
                        {
                            return true; // Too close
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false; // Assume not too close on error
            }
        }

        /// <summary>
        /// Gets overall suitability score for room placement.
        /// Higher is better.
        /// </summary>
        public static int GetOverallSuitabilityScore(Map map, RoomPlanner.RoomPlan plan, string logLevel = "Moderate")
        {
            try
            {
                int score = 50; // Base score

                // Accessibility (0-100)
                int accessScore = GetAccessibilityScore(map, plan.Origin, plan.Size);
                score += (accessScore / 5); // Max +20

                // Home area bonus
                IntVec3 center = plan.Origin + new IntVec3(plan.Size.x / 2, 0, plan.Size.z / 2);
                Area? homeArea = map.areaManager.Home;
                if (homeArea != null && homeArea[center])
                {
                    score += 15; // +15 for home area
                }

                // Flat terrain bonus (easier to build)
                bool isFlat = IsTerrainFlat(map, plan.Origin, plan.Size);
                if (isFlat)
                {
                    score += 10; // +10 for flat terrain
                }

                // Penalty for being too close to other rooms
                if (IsTooCloseToExistingRooms(map, plan.Origin, plan.Size))
                {
                    score -= 20; // -20 for cramped placement
                }

                if (logLevel == "Debug")
                {
                    RimWatchLogger.Debug($"RoomValidator: Suitability score for ({plan.Origin.x}, {plan.Origin.z}): {score}");
                }

                return score;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"RoomValidator: Error calculating suitability: {ex.Message}");
                return 50; // Neutral score
            }
        }

        /// <summary>
        /// Checks if terrain is relatively flat (no steep slopes).
        /// </summary>
        private static bool IsTerrainFlat(Map map, IntVec3 origin, IntVec2 size)
        {
            try
            {
                // RimWorld doesn't have elevation in base game
                // Just check that all cells are standable
                for (int x = 0; x < size.x; x++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        IntVec3 cell = origin + new IntVec3(x, 0, z);
                        if (!cell.InBounds(map) || !cell.Standable(map))
                            return false;
                    }
                }
                return true;
            }
            catch
            {
                return true; // Assume flat on error
            }
        }
    }
}

