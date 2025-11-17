using RimWatch.Utils;
using RimWorld;
using System.Linq;
using Verse;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// Centralized helper for safe blueprint placement using Designator_Build
    /// with rotation probing, interaction cell reachability checks, and comprehensive area validation.
    /// </summary>
    public static class BuildPlacer
    {
        /// <summary>
        /// Try to place a building blueprint and return the rotation used.
        /// v0.7.9: Returns detailed rejection reason in out parameter.
        /// </summary>
        public static bool TryPlaceWithBestRotation(Map map, ThingDef buildingDef, IntVec3 cell, ThingDef? stuffDef, out Rot4 usedRot, string logLevel = "Moderate")
        {
            usedRot = Rot4.North;
            Rot4[] tryRots = new[] { Rot4.North, Rot4.East, Rot4.South, Rot4.West };
            
            string lastRejection = "";
            
            foreach (Rot4 rot in tryRots)
            {
                if (TryPlaceSingleRotation(map, buildingDef, cell, rot, stuffDef, out lastRejection, logLevel))
                {
                    usedRot = rot;
                    return true;
                }
            }
            
            // v0.7.9: Log WHY placement failed for debugging
            if (logLevel != "Minimal" && !string.IsNullOrEmpty(lastRejection))
            {
                RimWatchLogger.Warning($"BuildPlacer: Failed to place {buildingDef.label} at ({cell.x}, {cell.z}) - {lastRejection}");
            }
            
            return false;
        }

        /// <summary>
        /// Try to place a building blueprint at cell using the best rotation.
        /// Returns true if successfully designated.
        /// </summary>
        public static bool TryPlaceWithBestRotation(Map map, ThingDef buildingDef, IntVec3 cell, ThingDef? stuffDef, string logLevel = "Moderate")
        {
            // Try four rotations in an order that often works visually
            Rot4[] tryRots = new[] { Rot4.North, Rot4.East, Rot4.South, Rot4.West };
            foreach (Rot4 rot in tryRots)
            {
                if (TryPlaceSingleRotation(map, buildingDef, cell, rot, stuffDef, out string _, logLevel))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Attempt placement at a specific rotation with comprehensive area validation.
        /// Uses AreaValidator to check footprint + buffer zone for buildings, blueprints, terrain, accessibility.
        /// v0.7.9: Added rejectionReason out parameter for better diagnostics.
        /// </summary>
        public static bool TryPlaceSingleRotation(Map map, ThingDef buildingDef, IntVec3 cell, Rot4 rot, ThingDef? stuffDef, out string rejectionReason, string logLevel = "Moderate")
        {
            rejectionReason = "";
            
            if (!cell.InBounds(map))
            {
                rejectionReason = "Out of bounds";
                return false;
            }

            // STEP 0: PRE-CLEARANCE - Удаляем растения/кусты в footprint
            ClearFootprint(map, buildingDef, cell, rot, logLevel);

            // STEP 1: Comprehensive area validation (footprint + buffer zone)
            ValidationResult areaCheck = AreaValidator.ValidateBuildingArea(
                map, 
                cell, 
                buildingDef, 
                rot, 
                bufferSize: 1, 
                logLevel: logLevel);

            if (!areaCheck.IsValid)
            {
                rejectionReason = $"Area validation: {areaCheck.RejectionReason}";
                if (logLevel == "Debug")
                {
                    RimWatchLogger.Debug($"BuildPlacer: Area validation failed for {buildingDef.label} at ({cell.x}, {cell.z}) rot={rot}: {areaCheck.RejectionReason}");
                }
                return false;
            }

            // Log warnings if any
            if (areaCheck.Warnings.Count > 0 && logLevel != "Minimal")
            {
                foreach (string warning in areaCheck.Warnings)
                {
                    RimWatchLogger.Warning($"BuildPlacer: {buildingDef.label} at ({cell.x}, {cell.z}): {warning}");
                }
            }

            // ✅ Check if this is a wall (same logic as in AreaValidator)
            bool isWall = buildingDef.building != null && 
                          buildingDef.passability == Traversability.Impassable &&
                          buildingDef.fillPercent >= 0.75f;

            // STEP 2: Validate with GenConstruct (respects PlaceWorker & rules)
            // ✅ CRITICAL: SKIP GenConstruct for walls! It rejects based on terrain/plants which we want to ignore
            if (!isWall)
            {
                AcceptanceReport canPlace = GenConstruct.CanPlaceBlueprintAt(buildingDef, cell, rot, map);
                if (!canPlace.Accepted)
                {
                    // Attempt pre-clear of blocking plants, then retry once
                    PreClearPlanner.DesignateBlockingPlants(map, cell, buildingDef, rot, logLevel);
                    canPlace = GenConstruct.CanPlaceBlueprintAt(buildingDef, cell, rot, map);
                    if (!canPlace.Accepted)
                    {
                        rejectionReason = $"GenConstruct: {canPlace.Reason}";
                        if (logLevel == "Debug")
                        {
                            RimWatchLogger.Debug($"BuildPlacer: GenConstruct rejected {buildingDef.label} at ({cell.x}, {cell.z}): {canPlace.Reason}");
                        }
                        return false;
                    }
                }
            }
            else
            {
                // For walls: only do pre-clearance, don't check GenConstruct
                PreClearPlanner.DesignateBlockingPlants(map, cell, buildingDef, rot, logLevel);
                
                if (logLevel == "Debug")
                    RimWatchLogger.Debug($"BuildPlacer: Skipped GenConstruct validation for wall (will build on any terrain)");
            }

            // STEP 3: Double-check for overlapping (redundant but safe)
            foreach (IntVec3 occ in GenAdj.OccupiedRect(cell, rot, buildingDef.Size))
            {
                if (!occ.InBounds(map)) return false;
                if (occ.GetFirstBuilding(map) != null) return false;
                var things = map.thingGrid.ThingsListAtFast(occ);
                foreach (var t in things)
                {
                    if (t is Blueprint || t is Frame)
                    {
                        if (logLevel == "Debug")
                        {
                            RimWatchLogger.Debug($"BuildPlacer: Overlap detected at ({occ.x}, {occ.z}) with {t.def.label}");
                        }
                        return false;
                    }
                }
            }

            // STEP 4: Place the blueprint
            GenConstruct.PlaceBlueprintForBuild(buildingDef, cell, map, rot, Faction.OfPlayer, stuffDef);
            
            if (logLevel == "Debug" || logLevel == "Moderate")
            {
                RimWatchLogger.Info($"BuildPlacer: Successfully placed {buildingDef.label} at ({cell.x}, {cell.z}) rot={rot}");
            }
            
            return true;
        }

        /// <summary>
        /// Очищает footprint от растений/кустов перед размещением blueprint.
        /// Автоматически ставит designations на CutPlant.
        /// </summary>
        private static void ClearFootprint(Map map, ThingDef buildingDef, IntVec3 cell, Rot4 rot, string logLevel)
        {
            try
            {
                // Получаем все клетки в footprint
                CellRect footprint = GenAdj.OccupiedRect(cell, rot, buildingDef.size);
                int cleared = 0;

                foreach (IntVec3 c in footprint)
                {
                    if (!c.InBounds(map)) continue;

                    // Ищем растения на клетке
                    var plants = c.GetThingList(map).Where(t => t is Plant).ToList();
                    foreach (Thing plant in plants)
                    {
                        // Пропускаем если это дерево (слишком долго рубить)
                        Plant p = plant as Plant;
                        if (p != null && p.def.plant != null && p.def.plant.IsTree)
                        {
                            if (logLevel == "Debug")
                                RimWatchLogger.Debug($"BuildPlacer: Skipping tree {p.def.label} at {c} - too big");
                            continue;
                        }

                        // Проверяем нет ли уже designation
                        if (map.designationManager.DesignationOn(plant, DesignationDefOf.CutPlant) != null)
                            continue;

                        // Добавляем designation на срезку
                        map.designationManager.AddDesignation(new Designation(plant, DesignationDefOf.CutPlant));
                        cleared++;
                        
                        if (logLevel == "Debug")
                            RimWatchLogger.Debug($"BuildPlacer: Designated {plant.def.label} for cutting at {c}");
                    }
                }

                if (cleared > 0 && logLevel != "Minimal")
                {
                    RimWatchLogger.Info($"BuildPlacer: Cleared {cleared} plants in footprint for {buildingDef.label}");
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error($"BuildPlacer: Error clearing footprint", ex);
            }
        }
    }
}


