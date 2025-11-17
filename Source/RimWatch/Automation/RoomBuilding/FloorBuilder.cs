using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.RoomBuilding
{
    /// <summary>
    /// Автоматически укладывает пол внутри комнат и под дверями.
    /// </summary>
    public static class FloorBuilder
    {
        /// <summary>
        /// Проверяет и укладывает пол в завершённых/строящихся комнатах.
        /// Вызывается периодически из RoomConstructionManager.
        /// </summary>
        public static void AutoBuildFloors(Map map)
        {
            try
            {
                // v0.8.3: Log execution start
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // ✅ CRITICAL: Add cooldown to prevent spam (only run once every 10 seconds)
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - _lastFloorCheckTick < 600) // 600 ticks = 10 seconds
                    return;
                
                _lastFloorCheckTick = currentTick;
                
                // Получаем все комнаты в процессе строительства
                var activeRooms = RoomConstructionManager.GetActiveConstructions(map);
                
                // v0.8.3: Log execution start with context
                RimWatchLogger.LogExecutionStart("FloorBuilder", "AutoBuildFloors", new Dictionary<string, object>
                {
                    { "activeRooms", activeRooms.Count }
                });
                
                int roomsProcessed = 0;
                int roomsSkipped = 0;
                
                foreach (var roomData in activeRooms)
                {
                    // Укладываем пол только если стены хотя бы начали строиться
                    if (roomData.Stage >= RoomConstructionManager.ConstructionStage.WALLS_BUILDING)
                    {
                        BuildFloorInRoom(map, roomData);
                        roomsProcessed++;
                    }
                    else
                    {
                        roomsSkipped++;
                    }
                }

                // Также проверяем существующие enclosed комнаты без пола
                BuildFloorsInExistingRooms(map);
                
                // v0.8.3: Log execution end
                stopwatch.Stop();
                RimWatchLogger.LogExecutionEnd("FloorBuilder", "AutoBuildFloors", true, stopwatch.ElapsedMilliseconds,
                    $"Processed {roomsProcessed} rooms, Skipped {roomsSkipped}");
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("FloorBuilder: Error in AutoBuildFloors", ex);
            }
        }
        
        private static int _lastFloorCheckTick = 0;

        /// <summary>
        /// Укладывает пол внутри конкретной комнаты (по данным из RoomConstructionManager).
        /// </summary>
        private static void BuildFloorInRoom(Map map, RoomConstructionManager.RoomConstructionState roomData)
        {
            try
            {
                // Определяем все клетки внутри стен
                List<IntVec3> floorCells = GetFloorCellsInRoom(map, roomData);
                
                if (floorCells.Count == 0)
                    return;

                // Выбираем тип пола (простой деревянный)
                TerrainDef floorDef = GetBestFloorType(map);
                if (floorDef == null)
                    return;

                // ✅ SMART APPROACH: Check room material to decide floor type
                // If walls are STONE → SmoothFloor is perfect (creates smooth stone floor)
                // If walls are WOOD → Skip automatic flooring (or place wood floor AFTER walls complete)
                bool roomHasStoneWalls = CheckIfRoomHasStoneWalls(map, roomData);
                
                // ⚠️ LIMITATION: RimWorld doesn't support terrain blueprints!
                // We can only use SmoothFloor designation (for stone) or direct placement (instant, like cheats)
                // Decision: ONLY auto-floor stone rooms with SmoothFloor
                if (!roomHasStoneWalls)
                {
                    // Skip wooden rooms - SmoothFloor doesn't make sense for wood
                    RimWatchLogger.Debug($"FloorBuilder: Skipping {roomData.Plan.Role} room - wooden walls (SmoothFloor not appropriate)");
                    return;
                }
                
                int floorsPlaced = 0;
                foreach (IntVec3 cell in floorCells)
                {
                    if (ShouldPlaceFloorAt(map, cell))
                    {
                        // Check for ANY designation on this cell
                        bool hasAnyDesignation = map.designationManager.AllDesignationsAt(cell).Any();
                        if (hasAnyDesignation)
                            continue; // Already has a designation, skip
                        
                        // ✅ For STONE rooms: SmoothFloor creates nice smooth stone floor
                        try
                        {
                            Designation designation = new Designation(cell, DesignationDefOf.SmoothFloor);
                            map.designationManager.AddDesignation(designation);
                            floorsPlaced++;
                        }
                        catch (System.Exception ex)
                        {
                            RimWatchLogger.Debug($"FloorBuilder: Failed to designate floor at ({cell.x}, {cell.z}): {ex.Message}");
                        }
                    }
                }

                if (floorsPlaced > 0)
                {
                    RimWatchLogger.Info($"🏗️ FloorBuilder: Placed {floorsPlaced} floor tiles in {roomData.Plan.Role} room");
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error($"FloorBuilder: Error building floor in {roomData.Plan.Role} room", ex);
            }
        }

        /// <summary>
        /// Укладывает пол в уже существующих enclosed комнатах, где нет пола.
        /// </summary>
        private static void BuildFloorsInExistingRooms(Map map)
        {
            try
            {
                // Получаем все enclosed комнаты колонии
                var rooms = map.regionGrid.AllRooms
                    .Where(r => !r.PsychologicallyOutdoors && !r.IsHuge && r.TouchesMapEdge == false)
                    .ToList();

                foreach (Room room in rooms)
                {
                    // Проверяем есть ли хотя бы одна дверь (признак жилой комнаты)
                    var doors = room.ContainedAndAdjacentThings.OfType<Building_Door>();
                    if (doors.Any())
                    {
                        BuildFloorInExistingRoom(map, room);
                    }
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("FloorBuilder: Error building floors in existing rooms", ex);
            }
        }

        /// <summary>
        /// Укладывает пол в существующей enclosed комнате.
        /// </summary>
        private static void BuildFloorInExistingRoom(Map map, Room room)
        {
            try
            {
                // ⚠️ DISABLED: Direct terrain placement is instant (like cheats)
                // RimWorld doesn't support terrain blueprints for wood floors
                // Players should manually build floors if desired
                // 
                // TODO: Consider enabling ONLY for stone rooms with SmoothFloor designation
                return;
                
                /* ORIGINAL CODE - DISABLED
                TerrainDef floorDef = GetBestFloorType(map);
                if (floorDef == null)
                    return;

                int floorsPlaced = 0;
                foreach (IntVec3 cell in room.Cells)
                {
                    if (ShouldPlaceFloorAt(map, cell))
                    {
                        map.terrainGrid.SetTerrain(cell, floorDef);
                        floorsPlaced++;
                    }
                }

                if (floorsPlaced > 5) // Логируем только если уложили много клеток
                {
                    RimWatchLogger.Debug($"FloorBuilder: Placed {floorsPlaced} floor tiles in existing room");
                }
                */
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("FloorBuilder: Error in BuildFloorInExistingRoom", ex);
            }
        }

        /// <summary>
        /// Определяет все клетки где нужно уложить пол внутри комнаты.
        /// </summary>
        private static List<IntVec3> GetFloorCellsInRoom(Map map, RoomConstructionManager.RoomConstructionState roomData)
        {
            List<IntVec3> floorCells = new List<IntVec3>();

            // ✅ CRITICAL FIX: Calculate correct bounding box (min inclusive, max exclusive)
            IntVec3 min = roomData.Plan.Origin;
            IntVec3 max = new IntVec3(
                min.x + roomData.Plan.Size.x - 1,  // ✅ -1 to stay within bounds
                0,
                min.z + roomData.Plan.Size.z - 1   // ✅ -1 to stay within bounds
            );

            for (int x = min.x; x <= max.x; x++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map))
                        continue;

                    // Пропускаем клетки со стенами
                    Building building = cell.GetFirstBuilding(map);
                    if (building != null)
                    {
                        var category = RimWatch.Automation.BuildingPlacement.BuildingClassifier.ClassifyBuilding(building.def);
                        if (category == RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Wall)
                            continue;
                        
                        // ✅ Под дверью ОБЯЗАТЕЛЬНО кладём пол!
                        if (category == RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Door)
                        {
                            floorCells.Add(cell);
                            continue;
                        }
                    }

                    // Добавляем клетку для пола
                    floorCells.Add(cell);
                }
            }

            return floorCells;
        }

        /// <summary>
        /// Проверяет являются ли стены комнаты каменными (не деревянными).
        /// Возвращает true если хотя бы одна стена из камня.
        /// </summary>
        private static bool CheckIfRoomHasStoneWalls(Map map, RoomConstructionManager.RoomConstructionState roomData)
        {
            // Check a few wall cells to determine material
            var wallCellsToCheck = roomData.Plan.WallCells.Take(5).ToList();
            
            foreach (IntVec3 wallCell in wallCellsToCheck)
            {
                if (!wallCell.InBounds(map))
                    continue;
                    
                // Check for built walls
                Building wall = wallCell.GetFirstBuilding(map);
                if (wall != null && wall.Stuff != null)
                {
                    // Check if stuff is stone (blocks)
                    if (wall.Stuff.defName.Contains("Blocks"))
                    {
                        return true; // Stone wall found
                    }
                }
                
                // Check for blueprints/frames
                var things = map.thingGrid.ThingsListAtFast(wallCell);
                foreach (Thing thing in things)
                {
                    if (thing is Blueprint_Build blueprint && blueprint.stuffToUse != null)
                    {
                        if (blueprint.stuffToUse.defName.Contains("Blocks"))
                        {
                            return true; // Stone blueprint found
                        }
                    }
                    else if (thing is Frame frame && frame.Stuff != null)
                    {
                        if (frame.Stuff.defName.Contains("Blocks"))
                        {
                            return true; // Stone frame found
                        }
                    }
                }
            }
            
            // Default: assume wooden walls (don't use SmoothFloor)
            return false;
        }
        
        /// <summary>
        /// Проверяет нужно ли укладывать пол на этой клетке.
        /// </summary>
        private static bool ShouldPlaceFloorAt(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map))
                return false;

            // Проверяем текущий terrain
            TerrainDef currentTerrain = cell.GetTerrain(map);
            if (currentTerrain == null)
                return false;

            // Пропускаем если уже есть constructed пол
            if (currentTerrain.layerable || currentTerrain.designatorDropdown != null)
            {
                // Уже есть пол
                return false;
            }

            // Пропускаем скалу/горы
            if (currentTerrain.passability == Traversability.Impassable)
                return false;

            // Пропускаем воду
            if (currentTerrain.IsWater)
                return false;
            
            // v0.8.3: ⚠️ CRITICAL FIX - Check for ore/mineable resources!
            // Don't place floor on ore - colonists need to mine it first!
            Thing mineable = cell.GetFirstMineable(map);
            if (mineable != null)
            {
                // v0.8.3: Log decision to skip ore
                RimWatchLogger.LogDecision("FloorBuilder", "SkipOre", new Dictionary<string, object>
                {
                    { "cell", cell.ToString() },
                    { "ore", mineable.def.defName },
                    { "label", mineable.LabelShort }
                });
                
                RimWatchLogger.Debug($"FloorBuilder: Skipping floor at {cell} - found ore: {mineable.LabelShort}");
                return false;
            }

            // Пропускаем клетки со стенами (не дверями!)
            Building building = cell.GetFirstBuilding(map);
            if (building != null)
            {
                var category = RimWatch.Automation.BuildingPlacement.BuildingClassifier.ClassifyBuilding(building.def);
                if (category == RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Wall)
                    return false;
                
                // ✅ Под дверью - ОБЯЗАТЕЛЬНО нужен пол!
                if (category == RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Door)
                    return true;
            }

            return true;
        }

        /// <summary>
        /// Выбирает лучший доступный тип пола для укладки.
        /// Приоритет: деревянный пол > каменный пол > грунт
        /// </summary>
        private static TerrainDef GetBestFloorType(Map map)
        {
            // Ищем доступные типы полов
            // Приоритет: WoodPlankFloor > PavedTile > FlagstoneSandstone
            
            var floorOptions = new[]
            {
                "WoodPlankFloor",     // Деревянный пол (дешево, красиво)
                "FlagstoneSandstone", // Каменный пол (песчаник)
                "FlagstoneSandstone", // Slate
                "PavedTile"           // Простая плитка
            };

            foreach (string floorDefName in floorOptions)
            {
                TerrainDef floorDef = DefDatabase<TerrainDef>.GetNamedSilentFail(floorDefName);
                if (floorDef != null)
                {
                    // TODO: Проверить доступность материалов
                    // Пока что просто возвращаем первый найденный
                    return floorDef;
                }
            }

            // Fallback: любой constructible пол
            return DefDatabase<TerrainDef>.AllDefs
                .FirstOrDefault(t => t.designationCategory != null && t.fertility == 0);
        }
    }
}

