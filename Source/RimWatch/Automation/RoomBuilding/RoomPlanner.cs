using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.RoomBuilding
{
    /// <summary>
    /// Plans room construction with intelligent size and role determination.
    /// Considers colony needs, available resources, and building priorities.
    /// </summary>
    public static class RoomPlanner
    {
        /// <summary>
        /// Room roles with specific requirements.
        /// </summary>
        public enum RoomRole
        {
            Bedroom,        // Individual bedrooms (4x4 minimum)
            Barracks,       // Multi-bed room (6x6 minimum)
            Kitchen,        // Cooking area (5x5 minimum)
            DiningRoom,     // Eating area (6x6 minimum)
            Workshop,       // Crafting/production (6x8 minimum)
            Storage,        // Warehouse (8x8 minimum)
            Hospital,       // Medical care (5x6 minimum)
            Research,       // Research benches (5x5 minimum)
            Recreation,     // Joy activities (6x6 minimum)
            Freezer,        // Cold storage (6x6 minimum)
            Prison,         // Prisoner cells (4x4 minimum)
            PowerRoom,      // Generator room with fuel storage (5x5 minimum)
            General         // Multi-purpose (5x5 minimum)
        }

        /// <summary>
        /// Room plan with all necessary details.
        /// </summary>
        public class RoomPlan
        {
            public RoomRole Role { get; set; }
            public IntVec3 Origin { get; set; }      // Bottom-left corner
            public IntVec2 Size { get; set; }         // Width x Height
            public int Priority { get; set; }         // Higher = more urgent
            public List<IntVec3> WallCells { get; set; } = new List<IntVec3>();
            public List<IntVec3> DoorCells { get; set; } = new List<IntVec3>();
            public List<IntVec3> FloorCells { get; set; } = new List<IntVec3>();
            public string RejectionReason { get; set; } = string.Empty;
            public bool IsValid => string.IsNullOrEmpty(RejectionReason);
        }

        /// <summary>
        /// Analyzes colony needs and returns prioritized list of rooms to build.
        /// </summary>
        public static List<RoomPlan> GetRoomBuildingNeeds(Map map, string logLevel = "Moderate")
        {
            List<RoomPlan> needs = new List<RoomPlan>();

            try
            {
                int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
                int prisonerCount = map.mapPawns.PrisonersOfColonyCount;

                RimWatchLogger.Info($"🏠 RoomPlanner: CHECKING room needs for {colonistCount} colonists");

                // ✅ NEW APPROACH: Check each colonist's bed situation individually
                List<Pawn> colonistsWithoutRoofedBeds = null;
                try
                {
                    colonistsWithoutRoofedBeds = GetColonistsWithoutRoofedBeds(map);
                    RimWatchLogger.Debug($"RoomPlanner: Got {colonistsWithoutRoofedBeds.Count} colonists without roofed beds");
                }
                catch (Exception ex)
                {
                    RimWatchLogger.Error("RoomPlanner: Error in GetColonistsWithoutRoofedBeds", ex);
                    colonistsWithoutRoofedBeds = new List<Pawn>();
                }
                
                int bedroomsNeeded = colonistsWithoutRoofedBeds.Count;
                
                RimWatchLogger.Info($"RoomPlanner: {bedroomsNeeded} colonists WITHOUT roofed beds: {string.Join(", ", colonistsWithoutRoofedBeds.Select(p => p.LabelShort))}");
                
                // Count existing enclosed rooms by role
                var existingRooms = AnalyzeExistingRooms(map);

                // Count rooms being built (blueprints/frames of walls = rooms under construction)
                int bedroomsUnderConstruction = CountBedroomRoomsUnderConstruction(map);
                
                // ✅ CRITICAL: Also count rooms registered in RoomConstructionManager
                List<RoomBuilding.RoomConstructionManager.RoomConstructionState> activeConstructions = 
                    RoomBuilding.RoomConstructionManager.GetActiveConstructions(map);
                int bedroomsInManager = activeConstructions.Count(state => 
                    state.Plan.Role == RoomRole.Bedroom && state.Stage < RoomBuilding.RoomConstructionManager.ConstructionStage.COMPLETE);
                int barracksInManager = activeConstructions.Count(state => 
                    state.Plan.Role == RoomRole.Barracks && state.Stage < RoomBuilding.RoomConstructionManager.ConstructionStage.COMPLETE);
                
                // Total bedrooms being built
                int totalBedroomsInProgress = bedroomsUnderConstruction + bedroomsInManager;
                
                RimWatchLogger.Info($"RoomPlanner: Total bedrooms in progress = {totalBedroomsInProgress} (construction={bedroomsUnderConstruction}, manager={bedroomsInManager}, barracks={barracksInManager})");
                
                if (logLevel == "Verbose" || logLevel == "Debug")
                {
                    RimWatchLogger.Info($"RoomPlanner: {bedroomsNeeded} colonists need roofed beds (total: {colonistCount})");
                    RimWatchLogger.Info($"RoomPlanner: Bedrooms under construction={bedroomsUnderConstruction}, in manager={bedroomsInManager}");
                    RimWatchLogger.Info($"RoomPlanner: Barracks in manager={barracksInManager}");
                    if (colonistsWithoutRoofedBeds.Count > 0)
                    {
                        RimWatchLogger.Info($"RoomPlanner: Colonists without roofed beds: {string.Join(", ", colonistsWithoutRoofedBeds.Select(p => p.LabelShort))}");
                    }
                }
                
                // ✅ CRITICAL: Build bedrooms if colonists are sleeping outside or in unroofed areas
                // BUT NOT if we're already building enough rooms!
                if (bedroomsNeeded > 0 && totalBedroomsInProgress < bedroomsNeeded)
                {
                    RimWatchLogger.Warning($"⚠️ RoomPlanner: NEED to build rooms! {bedroomsNeeded} colonists without beds, only {totalBedroomsInProgress} in progress");
                    
                    int roomsToAdd = Math.Min(bedroomsNeeded - totalBedroomsInProgress, 2); // Max 2 per cycle
                    
                    // Early game (2-4 colonists): Build barracks for all
                    int bedroomsExist = existingRooms.ContainsKey(RoomRole.Bedroom) ? existingRooms[RoomRole.Bedroom] : 0;
                    int barracksExist = existingRooms.ContainsKey(RoomRole.Barracks) ? existingRooms[RoomRole.Barracks] : 0;
                    
                    if (barracksExist == 0 && barracksInManager == 0 && colonistCount >= 2 && colonistCount <= 4 && bedroomsExist == 0 && totalBedroomsInProgress == 0)
                    {
                        // ✅ IMPROVED: Try multiple barracks sizes (6x8, 5x6, 4x6) with fallback
                        // Very early game: single barracks for all colonists
                        needs.Add(new RoomPlan
                        {
                            Role = RoomRole.Barracks,
                            Size = new IntVec2(6, 8), // 6x8 barracks
                            Priority = 100
                        });
                        RimWatchLogger.Info("RoomPlanner: Planning barracks 6x8 for early game");
                        
                        // ✅ FALLBACK: Also add smaller barracks option in case 6x8 doesn't fit
                        needs.Add(new RoomPlan
                        {
                            Role = RoomRole.Barracks,
                            Size = new IntVec2(5, 6), // 5x6 barracks (smaller)
                            Priority = 95
                        });
                        RimWatchLogger.Info("RoomPlanner: Planning fallback barracks 5x6");
                    }
                    else
                    {
                        // Mid/late game: individual bedrooms
                        for (int i = 0; i < roomsToAdd; i++)
                        {
                            needs.Add(new RoomPlan
                            {
                                Role = RoomRole.Bedroom,
                                Size = new IntVec2(4, 4), // 4x4 bedroom
                                Priority = 100 - i * 5 // High priority!
                            });
                        }
                        RimWatchLogger.Info($"RoomPlanner: Planning {roomsToAdd} bedroom(s) for colonists without roofs");
                    }
                }
                else if (bedroomsNeeded > 0)
                {
                    RimWatchLogger.Info($"RoomPlanner: {bedroomsNeeded} need beds but {totalBedroomsInProgress} already in progress - waiting");
                }
                else
                {
                    RimWatchLogger.Debug("RoomPlanner: All colonists have roofed beds ✓");
                }

                // Priority 2: Kitchen (if we have stoves but no enclosed kitchen)
                // Use BuildingClassifier for intelligent detection
                try
                {
                    bool hasStove = RimWatch.Automation.BuildingPlacement.BuildingClassifier.ColonyHasBuilding(
                        map, 
                        RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Stove);
                    int kitchensExist = existingRooms.ContainsKey(RoomRole.Kitchen) ? existingRooms[RoomRole.Kitchen] : 0;

                    if (hasStove && kitchensExist == 0 && colonistCount >= 2)
                    {
                        needs.Add(new RoomPlan
                        {
                            Role = RoomRole.Kitchen,
                            Size = new IntVec2(5, 5), // 5x5 kitchen
                            Priority = 90
                        });
                    }
                }
                catch (Exception ex)
                {
                    RimWatchLogger.Error("RoomPlanner: Error checking Kitchen needs", ex);
                }

                // Priority 3: Freezer (for food storage)
                try
                {
                    int freezersExist = existingRooms.ContainsKey(RoomRole.Freezer) ? existingRooms[RoomRole.Freezer] : 0;
                    int freezersInProgress = activeConstructions.Count(state => state.Plan.Role == RoomRole.Freezer);
                    int totalFreezers = freezersExist + freezersInProgress;
                    
                    bool hasCooler = map.listerBuildings.allBuildingsColonist.Any(b =>
                        b.def.defName.Contains("Cooler"));

                    if (totalFreezers == 0 && colonistCount >= 3)
                    {
                        needs.Add(new RoomPlan
                        {
                            Role = RoomRole.Freezer,
                            Size = new IntVec2(6, 6), // 6x6 freezer
                            Priority = 80
                        });
                        RimWatchLogger.Info($"RoomPlanner: Planning Freezer (exist: {freezersExist}, in progress: {freezersInProgress})");
                    }
                    else if (totalFreezers > 0)
                    {
                        RimWatchLogger.Debug($"RoomPlanner: Freezer NOT needed (exist: {freezersExist}, in progress: {freezersInProgress})");
                    }
                }
                catch (Exception ex)
                {
                    RimWatchLogger.Error("RoomPlanner: Error checking Freezer needs", ex);
                }

                // Priority 3: Storage warehouse (CRITICAL - protect items from deterioration!)
                try
                {
                    int storageRooms = existingRooms.ContainsKey(RoomRole.Storage) ? existingRooms[RoomRole.Storage] : 0;
                    int storageInProgress = activeConstructions.Count(state => state.Plan.Role == RoomRole.Storage);
                    int totalStorage = storageRooms + storageInProgress;
                    
                    if (totalStorage == 0 && colonistCount >= 2) // Even 2 colonists need storage!
                    {
                        needs.Add(new RoomPlan
                        {
                            Role = RoomRole.Storage,
                            Size = new IntVec2(8, 8), // 8x8 warehouse
                            Priority = 88 // HIGH PRIORITY - protect items from weather!
                        });
                        RimWatchLogger.Info($"RoomPlanner: Planning Storage (exist: {storageRooms}, in progress: {storageInProgress})");
                    }
                    else if (totalStorage > 0)
                    {
                        RimWatchLogger.Debug($"RoomPlanner: Storage NOT needed (exist: {storageRooms}, in progress: {storageInProgress})");
                    }
                }
                catch (Exception ex)
                {
                    RimWatchLogger.Error("RoomPlanner: Error checking Storage needs", ex);
                }

                // ✅ NEW: Priority 4: PowerRoom (for generators + fuel storage)
                try
                {
                    int powerRooms = existingRooms.ContainsKey(RoomRole.PowerRoom) ? existingRooms[RoomRole.PowerRoom] : 0;
                    int powerRoomsInProgress = activeConstructions.Count(state => state.Plan.Role == RoomRole.PowerRoom);
                    int totalPowerRooms = powerRooms + powerRoomsInProgress;
                    
                    // Check if we have any generators
                    bool hasGenerator = map.listerBuildings.allBuildingsColonist.Any(b =>
                        b.def.defName.Contains("Generator") || b.def.defName.Contains("Solar"));
                    
                    if (totalPowerRooms == 0 && hasGenerator && colonistCount >= 2)
                    {
                        needs.Add(new RoomPlan
                        {
                            Role = RoomRole.PowerRoom,
                            Size = new IntVec2(5, 5), // 5x5 power room
                            Priority = 75
                        });
                        RimWatchLogger.Info($"RoomPlanner: Planning PowerRoom (exist: {powerRooms}, in progress: {powerRoomsInProgress})");
                    }
                    else if (totalPowerRooms > 0)
                    {
                        RimWatchLogger.Debug($"RoomPlanner: PowerRoom NOT needed (exist: {powerRooms}, in progress: {powerRoomsInProgress})");
                    }
                }
                catch (Exception ex)
                {
                    RimWatchLogger.Error("RoomPlanner: Error checking PowerRoom needs", ex);
                }

                // Priority 5: Workshop
                int workshops = existingRooms.ContainsKey(RoomRole.Workshop) ? existingRooms[RoomRole.Workshop] : 0;
                int workshopsInProgress = activeConstructions.Count(state => state.Plan.Role == RoomRole.Workshop);
                int totalWorkshops = workshops + workshopsInProgress;
                
                bool hasCraftingBench = map.listerBuildings.allBuildingsColonist.Any(b =>
                    b.def.defName.Contains("Table") && !b.def.defName.Contains("Stove"));

                if (totalWorkshops == 0 && hasCraftingBench && colonistCount >= 3)
                {
                    needs.Add(new RoomPlan
                    {
                        Role = RoomRole.Workshop,
                        Size = new IntVec2(6, 8), // 6x8 workshop
                        Priority = 60
                    });
                    RimWatchLogger.Info($"RoomPlanner: Planning Workshop (exist: {workshops}, in progress: {workshopsInProgress})");
                }

                // Priority 6: Research lab
                int researchRooms = existingRooms.ContainsKey(RoomRole.Research) ? existingRooms[RoomRole.Research] : 0;
                int researchInProgress = activeConstructions.Count(state => state.Plan.Role == RoomRole.Research);
                int totalResearch = researchRooms + researchInProgress;
                
                bool hasResearchBench = map.listerBuildings.allBuildingsColonist.Any(b =>
                    b.def.defName.Contains("Research"));

                if (totalResearch == 0 && hasResearchBench && colonistCount >= 3)
                {
                    needs.Add(new RoomPlan
                    {
                        Role = RoomRole.Research,
                        Size = new IntVec2(5, 5), // 5x5 research lab
                        Priority = 50
                    });
                }

                // Priority 7: Hospital (if we have medical beds)
                int hospitals = existingRooms.ContainsKey(RoomRole.Hospital) ? existingRooms[RoomRole.Hospital] : 0;
                bool hasMedicalBed = map.listerBuildings.allBuildingsColonist.Any(b =>
                    b.def.defName.Contains("Hospital") || b.def.defName.Contains("Medical"));

                if (hospitals == 0 && colonistCount >= 4)
                {
                    needs.Add(new RoomPlan
                    {
                        Role = RoomRole.Hospital,
                        Size = new IntVec2(5, 6), // 5x6 hospital
                        Priority = 55
                    });
                }

                // Priority 8: Recreation room
                int recreationRooms = existingRooms.ContainsKey(RoomRole.Recreation) ? existingRooms[RoomRole.Recreation] : 0;
                if (recreationRooms == 0 && colonistCount >= 5)
                {
                    needs.Add(new RoomPlan
                    {
                        Role = RoomRole.Recreation,
                        Size = new IntVec2(6, 6), // 6x6 rec room
                        Priority = 40
                    });
                }

                // Priority 9: Prison cells (if we have prisoners)
                int prisons = existingRooms.ContainsKey(RoomRole.Prison) ? existingRooms[RoomRole.Prison] : 0;
                if (prisonerCount > prisons && prisonerCount > 0)
                {
                    needs.Add(new RoomPlan
                    {
                        Role = RoomRole.Prison,
                        Size = new IntVec2(4, 4), // 4x4 prison cell
                        Priority = 65
                    });
                }

                if (logLevel == "Verbose" || logLevel == "Debug")
                {
                    RimWatchLogger.Info($"RoomPlanner: Identified {needs.Count} room needs for {colonistCount} colonists");
                    foreach (var need in needs.OrderByDescending(n => n.Priority))
                    {
                        RimWatchLogger.Info($"  - {need.Role} ({need.Size.x}x{need.Size.z}), Priority: {need.Priority}");
                    }
                }

                return needs.OrderByDescending(n => n.Priority).ToList();
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("RoomPlanner: Error in GetRoomBuildingNeeds", ex);
                return needs;
            }
        }

        /// <summary>
        /// Finds colonists who don't have roofed beds assigned or are sleeping outside.
        /// This is the KEY method for determining bedroom needs.
        /// </summary>
        private static List<Pawn> GetColonistsWithoutRoofedBeds(Map map)
        {
            List<Pawn> colonistsWithoutRoofedBeds = new List<Pawn>();
            
            try
            {
                foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
                {
                    if (colonist.Dead || colonist.Destroyed)
                        continue;

                    // Check if colonist has an assigned bed
                    Building_Bed bed = colonist.ownership?.OwnedBed;
                    
                    if (bed == null)
                    {
                        // No bed assigned - needs one!
                        colonistsWithoutRoofedBeds.Add(colonist);
                        RimWatchLogger.Debug($"RoomPlanner: {colonist.LabelShort} has NO bed");
                        continue;
                    }

                    // Check if bed is roofed
                    bool isRoofed = bed.Position.Roofed(map);
                    
                    if (!isRoofed)
                    {
                        // Bed exists but NOT roofed - needs roofed room!
                        colonistsWithoutRoofedBeds.Add(colonist);
                        RimWatchLogger.Debug($"RoomPlanner: {colonist.LabelShort} has unroofed bed at ({bed.Position.x}, {bed.Position.z})");
                        continue;
                    }

                    // Check if bed is in an enclosed room
                    Room room = bed.GetRoom(RegionType.Set_Passable);
                    if (room == null || room.PsychologicallyOutdoors)
                    {
                        // Bed is roofed but room is "outdoors" psychologically
                        colonistsWithoutRoofedBeds.Add(colonist);
                        RimWatchLogger.Debug($"RoomPlanner: {colonist.LabelShort} has bed in outdoor/invalid room");
                        continue;
                    }
                    
                    // Colonist has proper roofed bed in enclosed room - OK!
                }

                return colonistsWithoutRoofedBeds;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("RoomPlanner: Error in GetColonistsWithoutRoofedBeds", ex);
                return colonistsWithoutRoofedBeds;
            }
        }

        /// <summary>
        /// Analyzes existing enclosed rooms on the map.
        /// </summary>
        private static Dictionary<RoomRole, int> AnalyzeExistingRooms(Map map)
        {
            Dictionary<RoomRole, int> roomCounts = new Dictionary<RoomRole, int>();

            try
            {
                // Get all distinct rooms
                HashSet<Room> processedRooms = new HashSet<Room>();

                foreach (Building building in map.listerBuildings.allBuildingsColonist)
                {
                    Room room = building.GetRoom(RegionType.Set_All);
                    if (room == null || room.PsychologicallyOutdoors || processedRooms.Contains(room))
                        continue;

                    processedRooms.Add(room);

                    // Classify room by contents
                    RoomRole role = ClassifyRoom(room);
                    if (!roomCounts.ContainsKey(role))
                        roomCounts[role] = 0;
                    roomCounts[role]++;
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"RoomPlanner: Error analyzing existing rooms: {ex.Message}");
            }

            return roomCounts;
        }

        /// <summary>
        /// Classifies a room based on its contents using BuildingClassifier.
        /// </summary>
        private static RoomRole ClassifyRoom(Room room)
        {
            try
            {
                var buildings = room.ContainedAndAdjacentThings.OfType<Building>().ToList();

                // Use BuildingClassifier for intelligent detection
                int beds = 0;
                int medicalBeds = 0;
                int stoves = 0;
                int tables = 0;
                int workbenches = 0;
                int researchBenches = 0;
                int coolers = 0;

                foreach (Building building in buildings)
                {
                    var category = RimWatch.Automation.BuildingPlacement.BuildingClassifier.ClassifyBuilding(building.def);
                    
                    switch (category)
                    {
                        case RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Bed:
                            beds++;
                            break;
                        case RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.MedicalBed:
                            medicalBeds++;
                            break;
                        case RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Stove:
                            stoves++;
                            break;
                        case RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Table:
                            tables++;
                            break;
                        case RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Workbench:
                            workbenches++;
                            break;
                        case RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.ResearchBench:
                            researchBenches++;
                            break;
                        case RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Temperature:
                            coolers++;
                            break;
                    }
                }

                // Classify based on contents (priority order)
                if (medicalBeds > 0) return RoomRole.Hospital;
                if (stoves > 0) return RoomRole.Kitchen;
                if (researchBenches > 0) return RoomRole.Research;
                if (workbenches > 0) return RoomRole.Workshop;
                if (coolers > 0) return RoomRole.Freezer;
                if (tables > 0) return RoomRole.DiningRoom;
                if (beds > 3) return RoomRole.Barracks;
                if (beds == 1) return RoomRole.Bedroom;

                return RoomRole.General;
            }
            catch
            {
                return RoomRole.General;
            }
        }

        /// <summary>
        /// Plans a room at the best available location.
        /// Populates wall cells, door cells, and validates placement.
        /// </summary>
        public static RoomPlan PlanRoomAtBestLocation(Map map, RoomRole role, IntVec2 size, string logLevel = "Moderate")
        {
            RoomPlan plan = new RoomPlan
            {
                Role = role,
                Size = size,
                Priority = 0
            };

            try
            {
                // Find center of existing base
                IntVec3 baseCenter = GetBaseCenter(map);

                // ✅ IMPROVED: Expanded search radius and smaller steps for better coverage
                // Search in expanding rings
                for (int radius = 3; radius <= 60; radius += 3)
                {
                    List<IntVec3> candidates = GetRingCandidates(map, baseCenter, radius);

                    foreach (IntVec3 candidate in candidates)
                    {
                        // Try to plan room at this location
                        RoomPlan testPlan = PlanRoomAtLocation(map, candidate, role, size, logLevel);

                        if (testPlan.IsValid)
                        {
                            if (logLevel == "Verbose" || logLevel == "Debug")
                            {
                                RimWatchLogger.Info($"RoomPlanner: ✓ Found location for {role} at ({candidate.x}, {candidate.z})");
                            }
                            return testPlan;
                        }
                        else if (logLevel == "Debug")
                        {
                            RimWatchLogger.Debug($"RoomPlanner: Rejected {role} at ({candidate.x}, {candidate.z}): {testPlan.RejectionReason}");
                        }
                    }
                }

                plan.RejectionReason = "No suitable location found within search radius";
                RimWatchLogger.Warning($"RoomPlanner: Could not find location for {role} room ({size.x}x{size.z})");
            }
            catch (Exception ex)
            {
                plan.RejectionReason = $"Error during planning: {ex.Message}";
                RimWatchLogger.Error($"RoomPlanner: Error planning {role} room", ex);
            }

            return plan;
        }

        /// <summary>
        /// Plans a room at a specific location, calculating walls, doors, and floors.
        /// </summary>
        private static RoomPlan PlanRoomAtLocation(Map map, IntVec3 origin, RoomRole role, IntVec2 size, string logLevel)
        {
            RoomPlan plan = new RoomPlan
            {
                Role = role,
                Origin = origin,
                Size = size
            };

            try
            {
                // Calculate all cells for the room (walls form perimeter)
                List<IntVec3> allCells = new List<IntVec3>();
                List<IntVec3> wallCells = new List<IntVec3>();
                List<IntVec3> floorCells = new List<IntVec3>();
                List<IntVec3> bufferCells = new List<IntVec3>(); // Buffer zone around room

                for (int x = 0; x < size.x; x++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        IntVec3 cell = origin + new IntVec3(x, 0, z);
                        allCells.Add(cell);

                        // Perimeter = walls, interior = floor
                        bool isPerimeter = (x == 0 || x == size.x - 1 || z == 0 || z == size.z - 1);

                        if (isPerimeter)
                            wallCells.Add(cell);
                        else
                            floorCells.Add(cell);
                    }
                }
                
                // Calculate buffer zone (1 cell around room)
                for (int x = -1; x <= size.x; x++)
                {
                    for (int z = -1; z <= size.z; z++)
                    {
                        IntVec3 cell = origin + new IntVec3(x, 0, z);
                        if (!allCells.Contains(cell) && cell.InBounds(map))
                        {
                            bufferCells.Add(cell);
                        }
                    }
                }

                // Validate all cells are clear
                foreach (IntVec3 cell in allCells)
                {
                    if (!cell.InBounds(map))
                    {
                        plan.RejectionReason = $"Room extends out of bounds at ({cell.x}, {cell.z})";
                        return plan;
                    }

                    // Check if cell is blocked
                    Building existingBuilding = cell.GetFirstBuilding(map);
                    if (existingBuilding != null && !IsRemovableBuilding(existingBuilding))
                    {
                        plan.RejectionReason = $"Cell ({cell.x}, {cell.z}) blocked by {existingBuilding.def.label}";
                        return plan;
                    }
                    
                    // ✅ CRITICAL: Check for blueprints and frames (planned construction)
                    var things = map.thingGrid.ThingsListAtFast(cell);
                    foreach (var thing in things)
                    {
                        if (thing is Blueprint || thing is Frame)
                        {
                            plan.RejectionReason = $"Cell ({cell.x}, {cell.z}) has blueprint/frame: {thing.def.label}";
                            return plan;
                        }
                    }

                    // Check terrain (no water/impassable)
                    TerrainDef terrain = cell.GetTerrain(map);
                    if (terrain != null && (terrain.IsWater || !cell.Standable(map)))
                    {
                        plan.RejectionReason = $"Cell ({cell.x}, {cell.z}) has unsuitable terrain: {terrain.label}";
                        return plan;
                    }
                }
                
                // ✅ Check buffer zone for existing structures (walls/doors only, not all buildings)
                foreach (IntVec3 cell in bufferCells)
                {
                    // Check for walls and doors in buffer zone
                    Building existingBuilding = cell.GetFirstBuilding(map);
                    if (existingBuilding != null && 
                        (existingBuilding.def.defName.Contains("Wall") || existingBuilding.def.defName.Contains("Door")))
                    {
                        plan.RejectionReason = $"Buffer zone conflict at ({cell.x}, {cell.z}) - too close to existing walls";
                        return plan;
                    }
                    
                    // Check for wall/door blueprints/frames in buffer zone
                    var things = map.thingGrid.ThingsListAtFast(cell);
                    foreach (var thing in things)
                    {
                        if (thing is Blueprint || thing is Frame)
                        {
                            if (thing.def.entityDefToBuild?.defName.Contains("Wall") == true ||
                                thing.def.entityDefToBuild?.defName.Contains("Door") == true)
                            {
                                plan.RejectionReason = $"Buffer zone has planned wall/door at ({cell.x}, {cell.z}) - too close to planned construction";
                                return plan;
                            }
                        }
                    }
                }

                plan.WallCells = wallCells;
                plan.FloorCells = floorCells;

                // Plan door locations (prefer on side closest to base center)
                plan.DoorCells = PlanDoorLocations(map, origin, size, wallCells);

                // ✅ CRITICAL: Rooms MUST have at least one door (avoid trapping colonists!)
                if (plan.DoorCells.Count == 0)
                {
                    plan.RejectionReason = "No door cells planned - colonists would be trapped!";
                    RimWatchLogger.Warning($"RoomPlanner: {role} room at ({origin.x}, {origin.z}) rejected - no doors!");
                    return plan;
                }

                if (logLevel == "Debug")
                {
                    RimWatchLogger.Debug($"RoomPlanner: Planned {role} at ({origin.x}, {origin.z}): {wallCells.Count} walls, {floorCells.Count} floor, {plan.DoorCells.Count} doors");
                }
            }
            catch (Exception ex)
            {
                plan.RejectionReason = $"Planning error: {ex.Message}";
                RimWatchLogger.Error($"RoomPlanner: Error planning room at ({origin.x}, {origin.z})", ex);
            }

            return plan;
        }

        /// <summary>
        /// Determines optimal door placements for a room.
        /// </summary>
        private static List<IntVec3> PlanDoorLocations(Map map, IntVec3 origin, IntVec2 size, List<IntVec3> wallCells)
        {
            List<IntVec3> doorCells = new List<IntVec3>();

            try
            {
                IntVec3 baseCenter = GetBaseCenter(map);
                IntVec3 roomCenter = origin + new IntVec3(size.x / 2, 0, size.z / 2);

                // Evaluate four sides in order of distance to base center,
                // but verify outside/inside viability to avoid trapping.
                var candidates = new List<(IntVec3 cell, IntVec3 inside, IntVec3 outside)>
                {
                    // South wall (outside = south)
                    (origin + new IntVec3(size.x / 2, 0, 0),
                     origin + new IntVec3(size.x / 2, 0, 1),
                     origin + new IntVec3(size.x / 2, 0, -1)),
                    // North wall (outside = north)
                    (origin + new IntVec3(size.x / 2, 0, size.z - 1),
                     origin + new IntVec3(size.x / 2, 0, size.z - 2),
                     origin + new IntVec3(size.x / 2, 0, size.z)),
                    // West wall (outside = west)
                    (origin + new IntVec3(0, 0, size.z / 2),
                     origin + new IntVec3(1, 0, size.z / 2),
                     origin + new IntVec3(-1, 0, size.z / 2)),
                    // East wall (outside = east)
                    (origin + new IntVec3(size.x - 1, 0, size.z / 2),
                     origin + new IntVec3(size.x - 2, 0, size.z / 2),
                     origin + new IntVec3(size.x, 0, size.z / 2)),
                };

                // Order by distance of door cell to base center
                candidates = candidates
                    .OrderBy(c => c.cell.DistanceTo(baseCenter))
                    .ToList();

                foreach (var c in candidates)
                {
                    if (!c.cell.InBounds(map) || !c.inside.InBounds(map) || !c.outside.InBounds(map))
                        continue;

                    // Inside must be interior of the room rectangle
                    bool insideIsInterior =
                        c.inside.x > origin.x &&
                        c.inside.x < origin.x + size.x - 1 &&
                        c.inside.z > origin.z &&
                        c.inside.z < origin.z + size.z - 1;
                    if (!insideIsInterior) continue;

                    // Outside must be standable AND reachable by colony
                    if (!c.outside.Standable(map)) continue;
                    if (!map.reachability.CanReachColony(c.outside)) continue;

                    // Extra: ensure one step further outside is not immediately blocked
                    IntVec3 further = new IntVec3(
                        c.outside.x + (c.outside.x - c.inside.x),
                        c.outside.y,
                        c.outside.z + (c.outside.z - c.inside.z));
                    if (further.InBounds(map) && !further.Standable(map))
                    {
                        // Not a hard reject, but prefer better candidates
                        // Only accept if everything else fails
                        // Mark with lower priority by pushing to end
                        continue;
                    }

                    doorCells.Add(c.cell);
                    break;
                }

                // Fallback: if none viable, default to south mid (will be validated later)
                if (doorCells.Count == 0)
                {
                    doorCells.Add(origin + new IntVec3(size.x / 2, 0, 0));
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"RoomPlanner: Error planning doors: {ex.Message}");
                // Default: place door in middle of south wall
                doorCells.Add(origin + new IntVec3(size.x / 2, 0, 0));
            }

            return doorCells;
        }

        /// <summary>
        /// Gets the center of the existing base.
        /// </summary>
        private static IntVec3 GetBaseCenter(Map map)
        {
            List<Building> buildings = map.listerBuildings.allBuildingsColonist.ToList();

            if (buildings.Count == 0)
                return map.Center;

            int avgX = (int)buildings.Average(b => b.Position.x);
            int avgZ = (int)buildings.Average(b => b.Position.z);

            return new IntVec3(avgX, 0, avgZ);
        }

        /// <summary>
        /// Gets candidate locations in a ring around a center point.
        /// Uses denser sampling (24 points per ring) for better distribution.
        /// </summary>
        private static List<IntVec3> GetRingCandidates(Map map, IntVec3 center, int radius)
        {
            List<IntVec3> candidates = new List<IntVec3>();

            // ✅ More points per ring (15° instead of 45°) for better spread
            for (int angle = 0; angle < 360; angle += 15)
            {
                float radians = angle * (float)Math.PI / 180f;
                int x = center.x + (int)(radius * Math.Cos(radians));
                int z = center.z + (int)(radius * Math.Sin(radians));

                IntVec3 candidate = new IntVec3(x, 0, z);
                if (candidate.InBounds(map))
                    candidates.Add(candidate);
            }

            // ✅ Add some offset variations to avoid perfect grid
            for (int angle = 7; angle < 360; angle += 30)
            {
                float radians = angle * (float)Math.PI / 180f;
                int x = center.x + (int)(radius * 0.7f * Math.Cos(radians));
                int z = center.z + (int)(radius * 0.7f * Math.Sin(radians));

                IntVec3 candidate = new IntVec3(x, 0, z);
                if (candidate.InBounds(map))
                    candidates.Add(candidate);
            }

            return candidates;
        }

        /// <summary>
        /// Checks if a building can be removed for room construction.
        /// </summary>
        private static bool IsRemovableBuilding(Building building)
        {
            // Don't remove important structures
            if (building.def.defName.Contains("Wall")) return false;
            if (building.def.defName.Contains("Door")) return false;
            if (building.def.building?.isNaturalRock == true) return false;

            return false; // For now, don't auto-remove anything
        }

        /// <summary>
        /// Counts rooms under construction (bedrooms/barracks) based on wall presence.
        /// UPDATED: Now checks ALL wall states (blueprints, frames, AND built).
        /// </summary>
        private static int CountBedroomRoomsUnderConstruction(Map map)
        {
            try
            {
                // Strategy: Look for groups of walls (any state) that form room-like clusters
                
                // 1. Collect ALL wall cells (blueprints, frames, built)
                HashSet<IntVec3> wallCells = new HashSet<IntVec3>();
                
                // Add blueprint walls
                List<Blueprint> wallBlueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint)
                    .OfType<Blueprint>()
                    .Where(b =>
                    {
                        ThingDef? def = b.def.entityDefToBuild as ThingDef;
                        return def != null && def.defName.Contains("Wall");
                    })
                    .ToList();
                
                foreach (var bp in wallBlueprints)
                    wallCells.Add(bp.Position);

                // Add frame walls
                List<Frame> wallFrames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame)
                    .OfType<Frame>()
                    .Where(f =>
                    {
                        ThingDef? def = f.def.entityDefToBuild as ThingDef;
                        return def != null && def.defName.Contains("Wall");
                    })
                    .ToList();
                
                foreach (var frame in wallFrames)
                    wallCells.Add(frame.Position);
                
                // ✅ NEW: Add built walls (small rooms being finished)
                List<Building> builtWalls = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.defName.Contains("Wall"))
                    .ToList();
                
                foreach (var wall in builtWalls)
                    wallCells.Add(wall.Position);

                int totalWalls = wallCells.Count;

                // Rough heuristic: 4x4 bedroom = ~12 walls, 6x8 barracks = ~28 walls
                // Average: ~15 walls per room
                int roomsUnderConstruction = totalWalls / 15;

                return roomsUnderConstruction;
            }
            catch
            {
                return 0;
            }
        }
    }
}

