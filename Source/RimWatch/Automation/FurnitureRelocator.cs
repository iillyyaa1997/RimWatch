using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace RimWatch.Automation
{
    /// <summary>
    /// Автоматически перемещает мебель (кровати) из неподходящих мест в правильные комнаты.
    /// </summary>
    public static class FurnitureRelocator
    {
        private static int _lastCheckTick = 0;
        
        /// <summary>
        /// Главная функция - вызывается периодически для проверки и перемещения мебели.
        /// </summary>
        public static void AutoRelocateFurniture(Map map)
        {
            try
            {
                // Run every 10 seconds
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - _lastCheckTick < 600) return;
                _lastCheckTick = currentTick;
                
                RelocateOutdoorBeds(map);
                InstallStoredBeds(map); // ✅ NEW: Install minified beds from storage
                OptimizeBedPositions(map);
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FurnitureRelocator: Error in AutoRelocateFurniture", ex);
            }
        }
        
        /// <summary>
        /// Находит кровати на улице и перемещает их в подходящие комнаты.
        /// </summary>
        private static void RelocateOutdoorBeds(Map map)
        {
            try
            {
                // Find all outdoor beds
                var outdoorBeds = map.listerBuildings.allBuildingsColonist
                    .OfType<Building_Bed>()
                    .Where(bed => bed.def.building.bed_humanlike && 
                                 (!bed.Position.Roofed(map) || 
                                  bed.GetRoom()?.PsychologicallyOutdoors == true))
                    .ToList();
                
                if (outdoorBeds.Count == 0) return;
                
                RimWatchLogger.Info($"FurnitureRelocator: Found {outdoorBeds.Count} outdoor beds to relocate");
                
                foreach (var bed in outdoorBeds)
                {
                    // Find suitable indoor room
                    IntVec3 newLocation = FindBestBedroomLocation(map, bed);
                    if (newLocation.IsValid)
                    {
                        // Create reinstall designation
                        Designation uninstallDesig = map.designationManager.DesignationOn(bed, DesignationDefOf.Uninstall);
                        if (uninstallDesig == null)
                        {
                            map.designationManager.AddDesignation(
                                new Designation(bed, DesignationDefOf.Uninstall));
                                
                            RimWatchLogger.Info($"🛏️ FurnitureRelocator: Marked bed at ({bed.Position.x}, {bed.Position.z}) for relocation to ({newLocation.x}, {newLocation.z})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FurnitureRelocator: Error in RelocateOutdoorBeds", ex);
            }
        }
        
        /// <summary>
        /// ✅ NEW: Finds and installs minified (stored/uninstalled) beds from storage.
        /// </summary>
        private static void InstallStoredBeds(Map map)
        {
            try
            {
                // Find colonists without beds
                var colonistsWithoutBeds = map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.Dead && 
                               (p.ownership?.OwnedBed == null || 
                                !p.ownership.OwnedBed.Position.Roofed(map)))
                    .ToList();
                
                if (colonistsWithoutBeds.Count == 0) return;
                
                // Find all minified beds (stored/uninstalled beds)
                var minifiedBeds = map.listerThings.AllThings
                    .OfType<MinifiedThing>()
                    .Where(m => m.InnerThing is Building_Bed && 
                               ((Building_Bed)m.InnerThing).def.building.bed_humanlike &&
                               !m.IsForbidden(Faction.OfPlayer))
                    .ToList();
                
                if (minifiedBeds.Count == 0) return;
                
                RimWatchLogger.Info($"🛏️ FurnitureRelocator: Found {minifiedBeds.Count} stored beds for {colonistsWithoutBeds.Count} colonists without beds");
                
                // Find suitable bedrooms to install beds
                var suitableRooms = map.regionGrid.AllRooms
                    .Where(r => !r.PsychologicallyOutdoors && 
                               !r.IsHuge &&
                               r.ProperRoom &&
                               r.Role == RoomRoleDefOf.Bedroom)
                    .ToList();
                
                int bedsInstalled = 0;
                
                foreach (var minifiedBed in minifiedBeds)
                {
                    if (bedsInstalled >= colonistsWithoutBeds.Count) break;
                    
                    // Find best room for this bed
                    IntVec3 installLocation = FindBestBedInstallLocation(map, suitableRooms, minifiedBed);
                    
                    if (installLocation.IsValid)
                    {
                        // Create install designation
                        if (!map.reservationManager.IsReservedByAnyoneOf(installLocation, Faction.OfPlayer))
                        {
                            // Create blueprint for reinstallation
                            Building_Bed bed = (Building_Bed)minifiedBed.InnerThing;
                            ThingDef bedDef = bed.def;
                            
                            // Check if location is clear
                            if (CanPlaceBedAt(map, installLocation, bedDef))
                            {
                                // Create install job by placing blueprint
                                GenConstruct.PlaceBlueprintForInstall(minifiedBed, installLocation, map, Rot4.South, Faction.OfPlayer);
                                
                                bedsInstalled++;
                                RimWatchLogger.Info($"🛏️ FurnitureRelocator: Created install blueprint for stored bed at ({installLocation.x}, {installLocation.z})");
                            }
                        }
                    }
                }
                
                if (bedsInstalled > 0)
                {
                    RimWatchLogger.Info($"✅ FurnitureRelocator: Scheduled {bedsInstalled} stored bed(s) for installation");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FurnitureRelocator: Error in InstallStoredBeds", ex);
            }
        }
        
        /// <summary>
        /// Finds the best location to install a bed in available bedrooms.
        /// </summary>
        private static IntVec3 FindBestBedInstallLocation(Map map, List<Room> suitableRooms, MinifiedThing minifiedBed)
        {
            try
            {
                Building_Bed bed = (Building_Bed)minifiedBed.InnerThing;
                
                foreach (var room in suitableRooms)
                {
                    // Check if room already has a bed
                    bool hasExistingBed = room.ContainedAndAdjacentThings
                        .OfType<Building_Bed>()
                        .Any(b => b.def.building.bed_humanlike);
                    
                    if (hasExistingBed) continue; // Skip rooms with beds
                    
                    // Find available space in room
                    IntVec3 roomCenter = room.Cells.OrderBy(c => c.DistanceToSquared(room.Cells.First())).First();
                    var availableCells = room.Cells
                        .Where(c => c.Standable(map) && 
                                   c.GetFirstBuilding(map) == null &&
                                   c.GetFirstItem(map) == null)
                        .OrderBy(c => c.DistanceToSquared(roomCenter))
                        .ToList();
                    
                    foreach (var cell in availableCells)
                    {
                        if (CanPlaceBedAt(map, cell, bed.def))
                        {
                            return cell;
                        }
                    }
                }
                
                return IntVec3.Invalid;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FurnitureRelocator: Error in FindBestBedInstallLocation", ex);
                return IntVec3.Invalid;
            }
        }
        
        /// <summary>
        /// Checks if a bed can be placed at the specified location.
        /// v0.8.4+: Добавлена проверка на blueprints и frames - НЕ СТАВИТЬ если уже есть!
        /// </summary>
        private static bool CanPlaceBedAt(Map map, IntVec3 location, ThingDef bedDef)
        {
            try
            {
                if (!location.InBounds(map)) return false;
                if (!location.Standable(map)) return false;
                
                // Check all cells the bed would occupy (beds are typically 1x2)
                IntVec2 size = bedDef.size;
                for (int x = 0; x < size.x; x++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        IntVec3 cell = location + new IntVec3(x, 0, z);
                        
                        if (!cell.InBounds(map)) return false;
                        if (!cell.Standable(map)) return false;
                        
                        // v0.8.4+: CRITICAL - проверка на blueprints и frames!
                        if (cell.GetFirstBuilding(map) != null) return false;
                        if (cell.GetThingList(map).Any(t => t is Blueprint || t is Frame)) return false;
                    }
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Оптимизирует позиции кроватей внутри комнат.
        /// </summary>
        private static void OptimizeBedPositions(Map map)
        {
            try
            {
                // Check all beds in rooms - move if needed for better layout
                var bedroomRooms = GetBedroomRooms(map);
                
                foreach (var room in bedroomRooms)
                {
                    var bedsInRoom = room.ContainedAndAdjacentThings
                        .OfType<Building_Bed>()
                        .Where(b => b.def.building.bed_humanlike)
                        .ToList();
                    
                    if (bedsInRoom.Count == 0) continue;
                    
                    foreach (var bed in bedsInRoom)
                    {
                        // Check if bed is blocking path or poorly positioned
                        if (IsBlockingPath(map, bed, room))
                        {
                            IntVec3 betterPos = FindBetterPositionInRoom(map, room, bed);
                            if (betterPos.IsValid && betterPos != bed.Position)
                            {
                                // Mark for relocation within same room
                                Designation uninstallDesig = map.designationManager.DesignationOn(bed, DesignationDefOf.Uninstall);
                                if (uninstallDesig == null)
                                {
                                    map.designationManager.AddDesignation(
                                        new Designation(bed, DesignationDefOf.Uninstall));
                                    
                                    RimWatchLogger.Debug($"FurnitureRelocator: Marked bed for optimization in room (blocking path)");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FurnitureRelocator: Error in OptimizeBedPositions", ex);
            }
        }
        
        /// <summary>
        /// Находит лучшую позицию для кровати в комнате.
        /// </summary>
        private static IntVec3 FindBestBedroomLocation(Map map, Building_Bed bed)
        {
            try
            {
                // Find all completed, roofed bedrooms
                var suitableRooms = map.regionGrid.AllRooms
                    .Where(r => !r.PsychologicallyOutdoors && 
                               !r.IsHuge &&
                               r.ProperRoom &&
                               r.Role == RoomRoleDefOf.Bedroom)
                    .OrderBy(r => r.CellCount) // Prefer smaller rooms
                    .ToList();
                
                foreach (var room in suitableRooms)
                {
                    // Check if room has space for bed
                    var availableCells = room.Cells
                        .Where(c => c.Standable(map) && 
                                   c.GetFirstBuilding(map) == null &&
                                   c.GetThingList(map).All(t => t.def.category != ThingCategory.Building))
                        .ToList();
                    
                    if (availableCells.Count >= 4) // Need space for 2x1 bed
                    {
                        // Find best cell in room (away from door, against wall)
                        var bestCell = availableCells
                            .OrderByDescending(c => ScoreBedPosition(map, c, room))
                            .FirstOrDefault();
                        
                        if (bestCell != IntVec3.Invalid)
                        {
                            return bestCell;
                        }
                    }
                }
                
                return IntVec3.Invalid;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FurnitureRelocator: Error in FindBestBedroomLocation", ex);
                return IntVec3.Invalid;
            }
        }
        
        /// <summary>
        /// Оценивает насколько хороша позиция для кровати.
        /// </summary>
        private static float ScoreBedPosition(Map map, IntVec3 cell, Room room)
        {
            float score = 0f;
            
            // Prefer cells against walls
            int adjacentWalls = 0;
            foreach (IntVec3 adj in GenAdj.CardinalDirections.Select(d => cell + d))
            {
                if (!adj.InBounds(map)) continue;
                
                Building building = adj.GetFirstBuilding(map);
                if (building != null && building.def.building != null && building.def.building.isNaturalRock)
                {
                    adjacentWalls++;
                }
            }
            score += adjacentWalls * 10f;
            
            // Avoid cells near doors
            var doors = room.ContainedAndAdjacentThings.OfType<Building_Door>();
            foreach (var door in doors)
            {
                float dist = cell.DistanceTo(door.Position);
                if (dist < 3f)
                {
                    score -= 20f / (dist + 1f);
                }
            }
            
            return score;
        }
        
        /// <summary>
        /// Получает список комнат-спален.
        /// </summary>
        private static List<Room> GetBedroomRooms(Map map)
        {
            try
            {
                return map.regionGrid.AllRooms
                    .Where(r => !r.PsychologicallyOutdoors &&
                               !r.IsHuge &&
                               r.ProperRoom &&
                               r.Role == RoomRoleDefOf.Bedroom)
                    .ToList();
            }
            catch
            {
                return new List<Room>();
            }
        }
        
        /// <summary>
        /// Проверяет блокирует ли кровать проход.
        /// </summary>
        private static bool IsBlockingPath(Map map, Building_Bed bed, Room room)
        {
            try
            {
                // Check if bed is on a main path by testing reachability
                var doors = room.ContainedAndAdjacentThings.OfType<Building_Door>().ToList();
                
                if (doors.Count < 2) return false; // Only one door - can't block path
                
                // Check if all doors are reachable from each other without going through bed
                for (int i = 0; i < doors.Count - 1; i++)
                {
                    for (int j = i + 1; j < doors.Count; j++)
                    {
                        // Simplified check: if bed is within 2 cells of both doors, might be blocking
                        float dist1 = bed.Position.DistanceTo(doors[i].Position);
                        float dist2 = bed.Position.DistanceTo(doors[j].Position);
                        
                        if (dist1 < 2f && dist2 < 2f)
                        {
                            return true; // Likely blocking path between doors
                        }
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Находит лучшую позицию для кровати в той же комнате.
        /// </summary>
        private static IntVec3 FindBetterPositionInRoom(Map map, Room room, Building_Bed bed)
        {
            try
            {
                // Find cells in room that are not blocking and better positioned
                var availableCells = room.Cells
                    .Where(c => c != bed.Position &&
                               c.Standable(map) &&
                               c.GetFirstBuilding(map) == null)
                    .OrderByDescending(c => ScoreBedPosition(map, c, room))
                    .ToList();
                
                if (availableCells.Any())
                {
                    return availableCells.First();
                }
                
                return IntVec3.Invalid;
            }
            catch
            {
                return IntVec3.Invalid;
            }
        }
    }
}

