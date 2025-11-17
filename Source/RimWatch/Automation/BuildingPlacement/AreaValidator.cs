using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// Validates building placement area including footprint and surrounding buffer zone.
    /// Checks for existing buildings, blueprints, frames, terrain, and environmental context.
    /// </summary>
    public static class AreaValidator
    {
        /// <summary>
        /// Validates entire area for building placement: footprint + buffer zone.
        /// Returns detailed validation result with reasons for rejection.
        /// </summary>
        public static ValidationResult ValidateBuildingArea(
            Map map,
            IntVec3 location,
            ThingDef buildingDef,
            Rot4 rotation,
            int bufferSize = 1,
            string logLevel = "Moderate")
        {
            ValidationResult result = new ValidationResult { IsValid = true };

            try
            {
                // Get occupied cells for this building
                List<IntVec3> footprint = GetFootprintCells(location, buildingDef, rotation);
                List<IntVec3> bufferZone = GetBufferZone(footprint, bufferSize, map);

                // 1. Check footprint cells (CRITICAL - must be completely clear)
                foreach (IntVec3 cell in footprint)
                {
                    if (!cell.InBounds(map))
                    {
                        result.Reject($"Footprint extends out of bounds at ({cell.x}, {cell.z})");
                        return result;
                    }

                    // Check for existing buildings
                    Building building = cell.GetFirstBuilding(map);
                    if (building != null)
                    {
                        result.Reject($"Cell ({cell.x}, {cell.z}) blocked by {building.def.label}");
                        return result;
                    }

                    // Check for blueprints
                    var blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint);
                    if (blueprints.Any(bp => bp.Position == cell))
                    {
                        result.Reject($"Cell ({cell.x}, {cell.z}) has blueprint");
                        return result;
                    }

                    // Check for frames under construction
                    var frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame);
                    if (frames.Any(f => f.Position == cell))
                    {
                        result.Reject($"Cell ({cell.x}, {cell.z}) has frame under construction");
                        return result;
                    }

                    // Check terrain
                    TerrainDef terrain = cell.GetTerrain(map);
                    if (terrain == null)
                    {
                        result.Reject($"Cell ({cell.x}, {cell.z}) has null terrain");
                        return result;
                    }

                    if (terrain.IsWater)
                    {
                        result.Reject($"Cell ({cell.x}, {cell.z}) is water");
                        return result;
                    }

                    // ✅ CRITICAL FIX: Check if this is a wall BEFORE Standable check
                    bool isWall = buildingDef.building != null && 
                                  buildingDef.passability == Traversability.Impassable &&
                                  buildingDef.fillPercent >= 0.75f;

                    // ✅ For walls: allow if location WOULD be standable after plant removal
                    if (!cell.Standable(map))
                    {
                        if (isWall)
                        {
                            // Check if terrain itself is impassable (rock)
                            TerrainDef cellTerrain = cell.GetTerrain(map);
                            if (cellTerrain.passability == Traversability.Impassable)
                            {
                                result.Reject($"Cell ({cell.x}, {cell.z}) is impassable terrain (rock): {cellTerrain.label}");
                                return result;
                            }
                            
                            // Check if there's a plant blocking (will be pre-cleared)
                            Plant plant = cell.GetPlant(map);
                            if (plant == null)
                            {
                                // Something else is blocking (not a plant)
                                result.Reject($"Cell ({cell.x}, {cell.z}) not standable (no plant to clear)");
                                return result;
                            }
                            // Plant exists - will be pre-cleared, so it's OK for walls
                        }
                        else
                        {
                            // Non-walls: reject if not standable
                            result.Reject($"Cell ({cell.x}, {cell.z}) not standable");
                            return result;
                        }
                    }

                    // Check terrain affordance if needed
                    // ✅ CRITICAL FIX: SKIP terrain affordance check for walls!
                    // Walls can be built on ANY terrain (grass, dirt, sand, etc.)
                    
                    if (!isWall && buildingDef.terrainAffordanceNeeded != null)
                    {
                        if (terrain.affordances == null || 
                            !terrain.affordances.Contains(buildingDef.terrainAffordanceNeeded))
                        {
                            result.Reject($"Cell ({cell.x}, {cell.z}) lacks required terrain affordance");
                            return result;
                        }
                    }

                    // ✅ Check for plants/crops and impassable terrain
                    List<Thing> items = cell.GetThingList(map);
                    
                    // ✅ CRITICAL: Check for ROCK/MOUNTAINS (impassable terrain)
                    // Buildings CANNOT be built on solid rock!
                    if (terrain.passability == Traversability.Impassable)
                    {
                        result.Reject($"Cell ({cell.x}, {cell.z}) is impassable terrain (rock/mountain): {terrain.label}");
                        return result;
                    }
                    
                    foreach (Thing item in items)
                    {
                        // Check for CROPS (cultivated plants - valuable!)
                        if (item is Plant plant)
                        {
                            // ✅ REJECT ONLY cultivated crops!
                            // All wild plants (grass, bushes, trees) will be auto-cleared
                            if (plant.IsCrop)
                            {
                                result.Reject($"Cell ({cell.x}, {cell.z}) has cultivated crop: {plant.def.label}");
                                return result;
                            }
                            
                            // ✅ For ALL buildings: wild plants are OK, will be handled by pre-clearance
                            // Just add a debug note
                            if (plant.def.plant != null && !plant.IsCrop)
                            {
                                if (logLevel == "Debug")
                                    RimWatchLogger.Debug($"Wild plant {plant.def.label} at ({cell.x}, {cell.z}) will be cleared");
                            }
                        }
                        
                        // Check for blocking items
                        if (item.def.category == ThingCategory.Item && item.def.EverHaulable)
                        {
                            result.AddWarning($"Cell ({cell.x}, {cell.z}) has item: {item.def.label}");
                            // Don't reject, just warn - items can be hauled away
                        }
                    }
                }

                // 2. Check buffer zone (warnings, not rejections)
                int adjacentBuildings = 0;
                int adjacentBlueprints = 0;
                int adjacentWalls = 0;

                foreach (IntVec3 cell in bufferZone)
                {
                    if (!cell.InBounds(map)) continue;

                    Building building = cell.GetFirstBuilding(map);
                    if (building != null)
                    {
                        adjacentBuildings++;
                        if (building.def.defName.Contains("Wall"))
                        {
                            adjacentWalls++;
                        }
                    }

                    var blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint)
                        .Where(bp => bp.Position == cell);
                    if (blueprints.Any())
                    {
                        adjacentBlueprints++;
                    }
                }

                // Warn if surrounded by walls (might be building inside a room)
                if (adjacentWalls >= footprint.Count * 2)
                {
                    result.AddWarning($"Location surrounded by {adjacentWalls} walls - might be inside existing room");
                }

                // Warn if too many adjacent blueprints (construction congestion)
                if (adjacentBlueprints > 5)
                {
                    result.AddWarning($"Heavy construction activity nearby ({adjacentBlueprints} blueprints)");
                }

                // 3. Check access/reachability - v0.8.0: CRITICAL FIX - Make this HARD requirement
                IntVec3 nearestColonyCell = FindNearestColonyBuilding(map, location);
                if (nearestColonyCell != IntVec3.Invalid)
                {
                    bool reachable = map.reachability.CanReach(
                        nearestColonyCell,
                        location,
                        PathEndMode.Touch,
                        TraverseParms.For(TraverseMode.PassDoors));

                    if (!reachable)
                    {
                        result.Reject($"Location not reachable from colony");
                        return result;
                    }
                }
                else
                {
                    // v0.8.0: No colony buildings found - check if ANY colonist can reach
                    bool anyColonistCanReach = map.mapPawns.FreeColonistsSpawned
                        .Any(p => p != null && p.Spawned && !p.Dead && !p.Downed &&
                                 p.Map == map &&
                                 map.reachability.CanReach(p.Position, location, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors)));
                    
                    if (!anyColonistCanReach)
                    {
                        result.Reject($"Location not reachable by any colonist");
                        return result;
                    }
                }

                // 4. Environmental context
                bool isRoofed = location.Roofed(map);
                bool inHomeArea = map.areaManager.Home != null && map.areaManager.Home[location];

                result.AddInfo($"Roofed: {isRoofed}, HomeArea: {inHomeArea}");
                result.AddInfo($"Adjacent: {adjacentBuildings} buildings, {adjacentWalls} walls, {adjacentBlueprints} blueprints");

                if (logLevel == "Debug")
                {
                    RimWatchLogger.Debug($"AreaValidator: {buildingDef.label} at ({location.x}, {location.z}) - VALID");
                    RimWatchLogger.Debug($"  Footprint: {footprint.Count} cells, Buffer: {bufferZone.Count} cells");
                    RimWatchLogger.Debug($"  Context: roofed={isRoofed}, home={inHomeArea}, walls={adjacentWalls}");
                }
            }
            catch (System.Exception ex)
            {
                result.Reject($"Validation error: {ex.Message}");
                RimWatchLogger.Error($"AreaValidator: Exception validating {buildingDef?.label ?? "unknown"}", ex);
            }

            return result;
        }

        /// <summary>
        /// Gets all cells occupied by a building at given location and rotation.
        /// </summary>
        private static List<IntVec3> GetFootprintCells(IntVec3 location, ThingDef buildingDef, Rot4 rotation)
        {
            List<IntVec3> cells = new List<IntVec3>();

            IntVec2 size = buildingDef.size;
            
            // Adjust size based on rotation
            if (rotation == Rot4.East || rotation == Rot4.West)
            {
                size = new IntVec2(size.z, size.x); // Swap width/height
            }

            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    cells.Add(location + new IntVec3(x, 0, z));
                }
            }

            return cells;
        }

        /// <summary>
        /// Gets buffer zone around footprint (surrounding cells).
        /// </summary>
        private static List<IntVec3> GetBufferZone(List<IntVec3> footprint, int bufferSize, Map map)
        {
            HashSet<IntVec3> bufferCells = new HashSet<IntVec3>();

            foreach (IntVec3 cell in footprint)
            {
                // Get all cells within buffer distance
                for (int dx = -bufferSize; dx <= bufferSize; dx++)
                {
                    for (int dz = -bufferSize; dz <= bufferSize; dz++)
                    {
                        IntVec3 bufferCell = cell + new IntVec3(dx, 0, dz);
                        
                        // Only add if not in footprint and in bounds
                        if (!footprint.Contains(bufferCell) && bufferCell.InBounds(map))
                        {
                            bufferCells.Add(bufferCell);
                        }
                    }
                }
            }

            return bufferCells.ToList();
        }

        /// <summary>
        /// Finds nearest colony building to given location.
        /// </summary>
        private static IntVec3 FindNearestColonyBuilding(Map map, IntVec3 location)
        {
            var buildings = map.listerBuildings.allBuildingsColonist;
            
            if (buildings.Count == 0)
                return IntVec3.Invalid;

            Building nearest = buildings.OrderBy(b => location.DistanceTo(b.Position)).FirstOrDefault();
            return nearest?.Position ?? IntVec3.Invalid;
        }
    }

    /// <summary>
    /// Result of area validation with detailed feedback.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string RejectionReason { get; private set; } = string.Empty;
        public List<string> Warnings { get; private set; } = new List<string>();
        public List<string> Info { get; private set; } = new List<string>();

        public void Reject(string reason)
        {
            IsValid = false;
            RejectionReason = reason;
        }

        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        public void AddInfo(string info)
        {
            Info.Add(info);
        }

        public override string ToString()
        {
            if (!IsValid)
                return $"REJECTED: {RejectionReason}";

            string result = "VALID";
            if (Warnings.Count > 0)
                result += $" ({Warnings.Count} warnings)";
            return result;
        }
    }
}

