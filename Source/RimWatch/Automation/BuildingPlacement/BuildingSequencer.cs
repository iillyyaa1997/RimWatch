using RimWatch.Automation.ColonyDevelopment;
using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// v0.8.4: Manages building construction priorities based on development stage.
    /// Determines what to build, when, and in what order for optimal colony development.
    /// </summary>
    public static class BuildingSequencer
    {
        /// <summary>
        /// Building priority information.
        /// </summary>
        public class BuildingPriority
        {
            public string BuildingType;     // "Bedroom", "Kitchen", etc.
            public int Priority;            // 100 = critical, 1 = optional
            public string Reason;           // Why this is needed
            public ThingDef RequiredTech;   // null if no research needed
            public ThingDef BuildingDef;    // Specific building to construct
        }
        
        /// <summary>
        /// Gets prioritized list of buildings that should be constructed for current stage.
        /// </summary>
        public static List<BuildingPriority> GetBuildingPriorities(Map map, DevelopmentStage stage)
        {
            var priorities = new List<BuildingPriority>();
            
            switch (stage)
            {
                case DevelopmentStage.Emergency:
                    AddEmergencyPriorities(map, priorities);
                    break;
                    
                case DevelopmentStage.EarlyGame:
                    AddEarlyGamePriorities(map, priorities);
                    break;
                    
                case DevelopmentStage.MidGame:
                    AddMidGamePriorities(map, priorities);
                    break;
                    
                case DevelopmentStage.LateGame:
                    AddLateGamePriorities(map, priorities);
                    break;
                    
                case DevelopmentStage.EndGame:
                    AddEndGamePriorities(map, priorities);
                    break;
            }
            
            return priorities.OrderByDescending(p => p.Priority).ToList();
        }
        
        /// <summary>
        /// Emergency (Day 1-3): Survive the first night.
        /// </summary>
        private static void AddEmergencyPriorities(Map map, List<BuildingPriority> priorities)
        {
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            
            // 1. Roofed beds - HIGHEST PRIORITY
            int bedsWithRoof = CountRoofedBeds(map);
            if (bedsWithRoof < colonistCount)
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "RoofedBeds",
                    Priority = 100,
                    Reason = $"{colonistCount - bedsWithRoof} colonists sleeping outside",
                    RequiredTech = null,
                    BuildingDef = ThingDefOf.Bed
                });
            }
            
            // 2. Campfire for cooking
            if (!HasCookingStation(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "Campfire",
                    Priority = 95,
                    Reason = "No cooking station available",
                    RequiredTech = null,
                    BuildingDef = ThingDef.Named("Campfire")
                });
            }
            
            // 3. Basic storage zone (not building, just designation)
            priorities.Add(new BuildingPriority
            {
                BuildingType = "StorageZone",
                Priority = 90,
                Reason = "Need organized storage",
                RequiredTech = null,
                BuildingDef = null // Zone, not building
            });
            
            // 4. Minimal defense (sandbags)
            int sandbags = CountDefenseBuildings(map);
            if (sandbags < 10)
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "Sandbags",
                    Priority = 85,
                    Reason = "Minimal defense needed",
                    RequiredTech = null,
                    BuildingDef = ThingDef.Named("Sandbags")
                });
            }
        }
        
        /// <summary>
        /// Early Game (Day 4-30): Build basic infrastructure.
        /// </summary>
        private static void AddEarlyGamePriorities(Map map, List<BuildingPriority> priorities)
        {
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            
            // 1. Proper bedrooms (priority depends on if still in barracks)
            int properBedrooms = CountProperBedrooms(map);
            if (properBedrooms < colonistCount)
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "Bedrooms",
                    Priority = 100,
                    Reason = $"Need {colonistCount - properBedrooms} more bedrooms",
                    RequiredTech = null,
                    BuildingDef = ThingDefOf.Bed
                });
            }
            
            // 2. Kitchen with Fueled Stove
            if (!HasKitchen(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "Kitchen",
                    Priority = 95,
                    Reason = "Need proper kitchen with stove",
                    RequiredTech = null,
                    BuildingDef = ThingDef.Named("FueledStove")
                });
            }
            
            // 3. Freezer next to kitchen
            if (!HasFreezer(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "Freezer",
                    Priority = 90,
                    Reason = "Food spoiling without refrigeration",
                    RequiredTech = ResearchProjectDefOf.Electricity, // Needs cooler
                    BuildingDef = ThingDefOf.Cooler
                });
            }
            
            // 4. Enclosed storage room
            if (!HasStorageRoom(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "Storage",
                    Priority = 85,
                    Reason = "Need protected storage",
                    RequiredTech = null,
                    BuildingDef = null // Room, not specific building
                });
            }
            
            // 5. Workshop
            if (!HasWorkshop(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "Workshop",
                    Priority = 80,
                    Reason = "Need crafting station",
                    RequiredTech = null,
                    BuildingDef = ThingDefOf.CraftingSpot
                });
            }
            
            // 6. Dining room
            if (!HasDiningRoom(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "DiningRoom",
                    Priority = 75,
                    Reason = "Colonists eating without table (-3 mood)",
                    RequiredTech = null,
                    BuildingDef = null // Room with tables
                });
            }
            
            // 7. Power generation
            if (!HasPowerGenerator(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "PowerGenerator",
                    Priority = 85,
                    Reason = "Need electricity",
                    RequiredTech = ResearchProjectDefOf.Electricity,
                    BuildingDef = ThingDef.Named("WoodFiredGenerator")
                });
            }
            
            // 8. Base perimeter walls
            priorities.Add(new BuildingPriority
            {
                BuildingType = "PerimeterWalls",
                Priority = 65,
                Reason = "Base needs defensive perimeter",
                RequiredTech = null,
                BuildingDef = null // Walls around base
            });
            
            // 9. Rec room
            if (!HasRecRoom(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "RecRoom",
                    Priority = 60,
                    Reason = "Colonists need recreation",
                    RequiredTech = null,
                    BuildingDef = ThingDef.Named("HorseshoesPin")
                });
            }
        }
        
        /// <summary>
        /// Mid Game (Day 31-120): Expand and specialize.
        /// </summary>
        private static void AddMidGamePriorities(Map map, List<BuildingPriority> priorities)
        {
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            
            // 1. Upgrade bedrooms (4x4 → 5x5)
            priorities.Add(new BuildingPriority
            {
                BuildingType = "UpgradeBedrooms",
                Priority = 70,
                Reason = "Upgrade to more impressive bedrooms",
                RequiredTech = null,
                BuildingDef = null
            });
            
            // 2. Hospital
            if (!HasHospital(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "Hospital",
                    Priority = 90,
                    Reason = "Need dedicated medical facility",
                    RequiredTech = ResearchProjectDefOf.Electricity,
                    BuildingDef = ThingDef.Named("HospitalBed")
                });
            }
            
            // 3. Expand kitchen (electric stove)
            priorities.Add(new BuildingPriority
            {
                BuildingType = "ExpandKitchen",
                Priority = 75,
                Reason = "Upgrade to electric stove",
                RequiredTech = ResearchProjectDefOf.Electricity,
                BuildingDef = ThingDef.Named("ElectricStove")
            });
            
            // 4. Specialized workshops
            if (!HasSpecializedWorkshops(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "SpecializedWorkshops",
                    Priority = 80,
                    Reason = "Need smithy, tailoring, drug lab",
                    RequiredTech = ResearchProjectDefOf.Smithing,
                    BuildingDef = ThingDef.Named("ElectricSmithy")
                });
            }
            
            // 5. Research lab
            if (!HasResearchLab(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "ResearchLab",
                    Priority = 85,
                    Reason = "Need research facility",
                    RequiredTech = null,
                    BuildingDef = ThingDef.Named("SimpleResearchBench")
                });
            }
            
            // 6. Solar panels + batteries
            if (!HasRenewablePower(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "SolarPower",
                    Priority = 80,
                    Reason = "Need reliable power source",
                    RequiredTech = ResearchProjectDefOf.SolarPanels,
                    BuildingDef = ThingDef.Named("SolarGenerator")
                });
            }
            
            // 7. Defensive turrets
            int turrets = CountTurrets(map);
            if (turrets < colonistCount / 2)
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "Turrets",
                    Priority = 85,
                    Reason = "Need automated defense",
                    RequiredTech = ResearchProjectDefOf.GunTurrets,
                    BuildingDef = ThingDef.Named("Turret_MiniTurret")
                });
            }
            
            // 8. Prison
            if (!HasPrison(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "Prison",
                    Priority = 60,
                    Reason = "Need prison for captives",
                    RequiredTech = null,
                    BuildingDef = null // Prison cells
                });
            }
            
            // 9. Upgrade rec room
            priorities.Add(new BuildingPriority
            {
                BuildingType = "UpgradeRecRoom",
                Priority = 65,
                Reason = "Add TV and better furniture",
                RequiredTech = ResearchProjectDefOf.Electricity,
                BuildingDef = ThingDef.Named("TubeTelevision")
            });
        }
        
        /// <summary>
        /// Late Game (Day 121-365): Advanced technology.
        /// </summary>
        private static void AddLateGamePriorities(Map map, List<BuildingPriority> priorities)
        {
            // 1. Luxury bedrooms for leaders
            priorities.Add(new BuildingPriority
            {
                BuildingType = "LuxuryBedrooms",
                Priority = 70,
                Reason = "Leaders deserve luxury (6x6+)",
                RequiredTech = null,
                BuildingDef = null
            });
            
            // 2. Advanced production
            priorities.Add(new BuildingPriority
            {
                BuildingType = "AdvancedProduction",
                Priority = 85,
                Reason = "Need fabrication bench",
                RequiredTech = ResearchProjectDefOf.Fabrication,
                BuildingDef = ThingDef.Named("FabricationBench")
            });
            
            // 3. Massive storage (15x15+)
            priorities.Add(new BuildingPriority
            {
                BuildingType = "MassiveStorage",
                Priority = 75,
                Reason = "Expand storage capacity",
                RequiredTech = null,
                BuildingDef = null
            });
            
            // 4. Advanced defense (autocannon turrets)
            priorities.Add(new BuildingPriority
            {
                BuildingType = "AdvancedDefense",
                Priority = 90,
                Reason = "Upgrade to autocannon turrets",
                RequiredTech = ResearchProjectDefOf.AutocannonTurret,
                BuildingDef = ThingDef.Named("Turret_Autocannon")
            });
            
            // 5. Geothermal power (if geyser available)
            if (HasNearbyGeyser(map))
            {
                priorities.Add(new BuildingPriority
                {
                    BuildingType = "GeothermalPower",
                    Priority = 85,
                    Reason = "Unlimited power from geyser",
                    RequiredTech = ResearchProjectDefOf.GeothermalPower,
                    BuildingDef = ThingDef.Named("GeothermalGenerator")
                });
            }
        }
        
        /// <summary>
        /// End Game (Year 2+): Victory conditions.
        /// </summary>
        private static void AddEndGamePriorities(Map map, List<BuildingPriority> priorities)
        {
            // 1. Ship components (if aiming for ship ending)
            priorities.Add(new BuildingPriority
            {
                BuildingType = "ShipComponents",
                Priority = 100,
                Reason = "Build ship to escape",
                RequiredTech = ResearchProjectDefOf.ShipBasics,
                BuildingDef = null
            });
            
            // 2. Maximum comfort and beauty
            priorities.Add(new BuildingPriority
            {
                BuildingType = "MaximumComfort",
                Priority = 80,
                Reason = "Maximize impressiveness and comfort",
                RequiredTech = null,
                BuildingDef = null
            });
            
            // 3. Complete defenses (killbox, layered walls)
            priorities.Add(new BuildingPriority
            {
                BuildingType = "CompleteDefenses",
                Priority = 95,
                Reason = "Perfect defensive setup",
                RequiredTech = null,
                BuildingDef = null
            });
        }
        
        // ===== HELPER METHODS =====
        
        /// <summary>
        /// Checks if a building can be built now (tech researched, resources available).
        /// </summary>
        public static bool CanBuildNow(ThingDef building, Map map)
        {
            if (building == null) return false;
            
            // Check if research completed
            if (building.researchPrerequisites != null && building.researchPrerequisites.Count > 0)
            {
                foreach (var research in building.researchPrerequisites)
                {
                    if (!research.IsFinished)
                    {
                        RimWatchLogger.Debug($"BuildingSequencer: {building.defName} requires {research.defName} research");
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        private static int CountRoofedBeds(Map map)
        {
            return map.listerBuildings.allBuildingsColonist
                .OfType<Building_Bed>()
                .Count(b => b.Position.Roofed(map) && b.GetRoom()?.PsychologicallyOutdoors == false);
        }
        
        private static bool HasCookingStation(Map map)
        {
            return map.listerBuildings.allBuildingsColonist
                .Any(b => b.def.building?.isMealSource == true);
        }
        
        private static bool HasKitchen(Map map)
        {
            return map.listerBuildings.allBuildingsColonist
                .Any(b => b.def.defName.Contains("Stove"));
        }
        
        private static bool HasFreezer(Map map)
        {
            return map.listerBuildings.allBuildingsColonist
                .Any(b => b.def == ThingDefOf.Cooler);
        }
        
        private static bool HasStorageRoom(Map map)
        {
            // Check for storage zones inside roofed rooms
            return map.zoneManager.AllZones
                .OfType<Zone_Stockpile>()
                .Any(z => z.Cells.Any(c => c.Roofed(map)));
        }
        
        private static bool HasWorkshop(Map map)
        {
            return map.listerBuildings.allBuildingsColonist
                .Any(b => b is Building_WorkTable);
        }
        
        private static bool HasDiningRoom(Map map)
        {
            return map.listerBuildings.allBuildingsColonist
                .Any(b => b.def == ThingDefOf.Table);
        }
        
        private static bool HasPowerGenerator(Map map)
        {
            return map.listerBuildings.allBuildingsColonist
                .Any(b => b is Building_PowerPlant);
        }
        
        private static bool HasRecRoom(Map map)
        {
            // Check for recreation buildings (horseshoes, chess, etc)
            return map.listerBuildings.allBuildingsColonist
                .Any(b => b.def.building?.isJoySource == true);
        }
        
        private static int CountProperBedrooms(Map map)
        {
            // Count beds in individual rooms (not barracks)
            return map.listerBuildings.allBuildingsColonist
                .OfType<Building_Bed>()
                .Count(b =>
                {
                    Room room = b.GetRoom();
                    if (room == null || room.PsychologicallyOutdoors) return false;
                    
                    // Check if room is proper bedroom (not barracks)
                    int bedsInRoom = room.ContainedBeds.Count();
                    return bedsInRoom == 1; // Individual bedroom
                });
        }
        
        private static bool HasHospital(Map map)
        {
            return map.listerBuildings.allBuildingsColonist
                .Any(b => b.def.defName.Contains("HospitalBed"));
        }
        
        private static bool HasSpecializedWorkshops(Map map)
        {
            bool hasSmithy = map.listerBuildings.allBuildingsColonist
                .Any(b => b.def.defName.Contains("Smithy"));
            bool hasTailoring = map.listerBuildings.allBuildingsColonist
                .Any(b => b.def.defName.Contains("TailoringBench"));
            
            return hasSmithy && hasTailoring;
        }
        
        private static bool HasResearchLab(Map map)
        {
            return map.listerBuildings.allBuildingsColonist
                .Any(b => b.def.defName.Contains("ResearchBench"));
        }
        
        private static bool HasRenewablePower(Map map)
        {
            return map.listerBuildings.allBuildingsColonist
                .Any(b => b.def.defName.Contains("Solar") || b.def.defName.Contains("Wind"));
        }
        
        private static int CountTurrets(Map map)
        {
            return map.listerBuildings.allBuildingsColonist
                .Count(b => b.def.building?.IsTurret == true);
        }
        
        private static int CountDefenseBuildings(Map map)
        {
            return map.listerBuildings.allBuildingsColonist
                .Count(b => b.def.defName == "Sandbags" || b.def.building?.IsTurret == true);
        }
        
        private static bool HasPrison(Map map)
        {
            return map.listerBuildings.allBuildingsColonist
                .OfType<Building_Bed>()
                .Any(b => b.ForPrisoners);
        }
        
        private static bool HasNearbyGeyser(Map map)
        {
            return map.listerThings.ThingsOfDef(ThingDefOf.SteamGeyser).Count > 0;
        }
    }
}

