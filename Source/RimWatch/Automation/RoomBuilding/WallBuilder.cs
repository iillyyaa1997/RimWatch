using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.RoomBuilding
{
    /// <summary>
    /// Handles wall construction with intelligent material selection.
    /// </summary>
    public static class WallBuilder
    {
        /// <summary>
        /// Wall material tiers based on available resources and game stage.
        /// </summary>
        private enum WallTier
        {
            Wood,           // Early game - fast but flammable
            Stone,          // Mid game - durable and fire-proof
            SteelReinforced // Late game - strongest
        }

        /// <summary>
        /// Places wall blueprints for a list of cells.
        /// </summary>
        public static int PlaceWalls(Map map, List<IntVec3> wallCells, List<IntVec3> doorCells, string logLevel = "Moderate")
        {
            int wallsPlaced = 0;

            try
            {
                if (wallCells == null || wallCells.Count == 0)
                {
                    RimWatchLogger.Warning("WallBuilder: No wall cells provided");
                    return 0;
                }

                // Select appropriate wall material
                ThingDef wallDef = SelectWallMaterial(map, logLevel);
                if (wallDef == null)
                {
                    RimWatchLogger.Error("WallBuilder: Could not find wall ThingDef!");
                    return 0;
                }

                ThingDef stuffDef = RimWatch.Automation.BuildingPlacement.StuffSelector.DefaultNonSteelStuffFor(wallDef, map);
                if (stuffDef == null)
                {
                    RimWatchLogger.Warning($"WallBuilder: No suitable material for {wallDef.defName}");
                    return 0;
                }

                // Place wall blueprints (skip door locations)
                foreach (IntVec3 cell in wallCells)
                {
                    // Skip if this cell will have a door
                    if (doorCells.Contains(cell))
                        continue;

                    // Check if already has wall or blueprint
                    if (HasWallOrBlueprint(map, cell))
                    {
                        if (logLevel == "Debug")
                            RimWatchLogger.Debug($"WallBuilder: Skipping ({cell.x}, {cell.z}) - already has wall/blueprint");
                        continue;
                    }

                    // Place blueprint via Designator with rotation probing
                    bool success = RimWatch.Automation.BuildingPlacement.BuildPlacer.TryPlaceWithBestRotation(map, wallDef, cell, stuffDef, logLevel);
                    if (success)
                    {
                        wallsPlaced++;
                        if (logLevel == "Verbose" || logLevel == "Debug")
                            RimWatchLogger.Debug($"WallBuilder: Placed {stuffDef.label} wall at ({cell.x}, {cell.z})");
                    }
                    else if (logLevel == "Debug")
                    {
                        RimWatchLogger.Debug($"WallBuilder: Failed to place wall at ({cell.x}, {cell.z})");
                    }
                }

                if (wallsPlaced > 0)
                {
                    RimWatchLogger.Info($"🧱 WallBuilder: Placed {wallsPlaced} {stuffDef.label} wall blueprints");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("WallBuilder: Error in PlaceWalls", ex);
            }

            return wallsPlaced;
        }

        /// <summary>
        /// Selects the appropriate wall type based on game stage.
        /// </summary>
        private static ThingDef? SelectWallMaterial(Map map, string logLevel)
        {
            try
            {
                // Always use standard wall for now
                ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
                
                if (wall == null)
                {
                    RimWatchLogger.Error("WallBuilder: Wall ThingDef not found in database!");
                }

                return wall;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("WallBuilder: Error selecting wall material", ex);
                return null;
            }
        }

        /// <summary>
        /// Selects the best available stuff (material) for walls.
        /// Priority: Wood (cheap, renewable) > Stone (fireproof) > Steel (expensive)
        /// NEVER use: Silver, Gold, Jade, Uranium (too valuable!)
        /// </summary>
        private static ThingDef? SelectWallStuff(Map map, ThingDef wallDef, string logLevel)
        {
            try
            {
                if (!wallDef.MadeFromStuff)
                {
                    if (logLevel == "Debug")
                        RimWatchLogger.Debug("WallBuilder: Wall doesn't require stuff");
                    return null;
                }

                // Get colonist-owned resources
                List<Thing> availableResources = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                    .Where(t => t.def.IsStuff && t.def.stuffProps != null)
                    .Where(t => map.areaManager.Home[t.Position]) // Only resources in home area
                    .ToList();

                // ✅ Priority 1: Wood (cheap, renewable, easy to get more)
                ThingDef? woodStuff = TryGetStuff(availableResources, new[] {
                    "WoodLog"
                }, wallDef, minCount: 50);

                if (woodStuff != null)
                {
                    if (logLevel == "Verbose" || logLevel == "Debug")
                        RimWatchLogger.Debug($"WallBuilder: Selected wood material (cheap & renewable)");
                    return woodStuff;
                }

                // Priority 2: Stone blocks (fireproof, durable, renewable via quarry)
                ThingDef? stoneStuff = TryGetStuff(availableResources, new[] {
                    "BlocksGranite", "BlocksLimestone", "BlocksSlate",
                    "BlocksMarble", "BlocksSandstone"
                }, wallDef, minCount: 50);

                if (stoneStuff != null)
                {
                    if (logLevel == "Verbose" || logLevel == "Debug")
                        RimWatchLogger.Debug($"WallBuilder: Selected stone material: {stoneStuff.label}");
                    return stoneStuff;
                }

                // ❌ NEVER use valuable materials automatically
                // Filter out: Silver, Gold, Jade, Uranium, Plasteel
                List<string> forbiddenMaterials = new List<string> {
                    "Steel", "Silver", "Gold", "Jade", "Uranium", "Plasteel"
                };

                // Fallback: use any valid stuff (but not forbidden materials)
                ThingDef? fallback = wallDef.stuffCategories?
                    .SelectMany(cat => DefDatabase<ThingDef>.AllDefs.Where(def =>
                        def.IsStuff &&
                        def.stuffProps != null &&
                        def.stuffProps.categories != null &&
                        def.stuffProps.categories.Contains(cat) &&
                        !forbiddenMaterials.Contains(def.defName))) // ✅ Exclude valuable materials
                    .FirstOrDefault();

                if (fallback != null && logLevel == "Debug")
                    RimWatchLogger.Debug($"WallBuilder: Using fallback material: {fallback.label}");

                return fallback;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("WallBuilder: Error selecting stuff", ex);
                return GenStuff.DefaultStuffFor(wallDef);
            }
        }

        /// <summary>
        /// Tries to get a stuff ThingDef from available resources.
        /// </summary>
        private static ThingDef? TryGetStuff(List<Thing> availableResources, string[] defNames, ThingDef forDef, int minCount)
        {
            foreach (string defName in defNames)
            {
                ThingDef? stuffDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (stuffDef == null) continue;

                // Check if we have enough of this material
                int totalCount = availableResources
                    .Where(t => t.def == stuffDef)
                    .Sum(t => t.stackCount);

                if (totalCount >= minCount)
                {
                    // Verify it's valid for this wall
                    if (forDef.stuffCategories != null &&
                        stuffDef.stuffProps?.categories != null &&
                        forDef.stuffCategories.Any(cat => stuffDef.stuffProps.categories.Contains(cat)))
                    {
                        return stuffDef;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a cell already has a wall or wall blueprint.
        /// </summary>
        private static bool HasWallOrBlueprint(Map map, IntVec3 cell)
        {
            try
            {
                // Check for existing wall
                Building? existingBuilding = cell.GetFirstBuilding(map);
                if (existingBuilding != null && existingBuilding.def.defName.Contains("Wall"))
                    return true;

                // Check for wall blueprint
                List<Thing> blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint);
                foreach (Thing thing in blueprints)
                {
                    if (thing.Position == cell && thing is Blueprint bp)
                    {
                        if (bp.def.entityDefToBuild is ThingDef thingDef && thingDef.defName.Contains("Wall"))
                            return true;
                    }
                }

                // Check for wall frame under construction
                List<Thing> frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame);
                foreach (Thing thing in frames)
                {
                    if (thing.Position == cell && thing is Frame frame)
                    {
                        if (frame.def.entityDefToBuild is ThingDef thingDef && thingDef.defName.Contains("Wall"))
                            return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"WallBuilder: Error checking wall at ({cell.x}, {cell.z}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Estimates material cost for wall construction.
        /// </summary>
        public static int EstimateMaterialCost(int wallCount, ThingDef? stuffDef)
        {
            // Walls typically cost 5 material each
            int costPerWall = 5;
            return wallCount * costPerWall;
        }

        /// <summary>
        /// Checks if colony has enough materials to build walls.
        /// </summary>
        public static bool HasEnoughMaterials(Map map, int wallCount, ThingDef? stuffDef)
        {
            try
            {
                // If no specific stuff provided, check if we have ANY wall material available
                if (stuffDef == null)
                {
                    // Check for stone, steel, or wood
                    int stoneCount = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                        .Where(t => t.def.defName.Contains("Blocks"))
                        .Sum(t => t.stackCount);

                    int steelCount = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                        .Where(t => t.def.defName == "Steel")
                        .Sum(t => t.stackCount);

                    int woodCount = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                        .Where(t => t.def.defName == "WoodLog")
                        .Sum(t => t.stackCount);

                    int estimatedCost = EstimateMaterialCost(wallCount, null);

                    bool hasEnough = (stoneCount >= estimatedCost) ||
                                   (steelCount >= estimatedCost) ||
                                   (woodCount >= estimatedCost);

                    if (!hasEnough)
                    {
                        RimWatchLogger.Debug($"WallBuilder: Not enough materials - need {estimatedCost}, have: Stone={stoneCount}, Steel={steelCount}, Wood={woodCount}");
                    }

                    return hasEnough;
                }

                int requiredAmount = EstimateMaterialCost(wallCount, stuffDef);

                int availableAmount = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                    .Where(t => t.def == stuffDef)
                    .Sum(t => t.stackCount);

                if (availableAmount < requiredAmount)
                {
                    RimWatchLogger.Debug($"WallBuilder: Not enough {stuffDef.label} - need {requiredAmount}, have {availableAmount}");
                }

                return availableAmount >= requiredAmount;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"WallBuilder: Error checking materials: {ex.Message}");
                return false;
            }
        }
    }
}

