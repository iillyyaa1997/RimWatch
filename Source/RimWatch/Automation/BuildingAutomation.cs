using RimWatch.Core;
using RimWatch.Utils;
using RimWatch.Automation.BuildingPlacement;
using RimWatch.Automation.RoomBuilding;
using RimWatch.Settings;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation
{
    /// <summary>
    /// 🏗️ Building Automation - автоматическое планирование и управление строительством.
    /// Анализирует потребности колонии и рекомендует необходимые постройки.
    /// </summary>
    public static class BuildingAutomation
    {
        private static int _tickCounter = 0;
        private static bool _isEnabled = false;
        private const int UpdateInterval = 3600; // v0.8.4+: 60 секунд (было 30!) - слишком частые проверки создавали спам!
        
        // v0.8.4+: КРИТИЧЕСКИ увеличил cooldown - мод спамил задачи слишком быстро!
        // Cooldown system to prevent spam
        private static Dictionary<string, int> _lastPlacementTick = new Dictionary<string, int>();
        private const int PlacementCooldown = 3600; // v0.8.4+: 60 секунд (было 10!) между размещениями одного типа
        
        // Priority change cooldown
        private static int _lastPriorityChangeTick = 0;
        private const int PriorityChangeCooldown = 3600; // v0.8.4+: 60 секунд (было 30!) между изменениями приоритетов
        
        // v0.8.2: Rejected location cache to prevent repeated failed placement attempts
        private class RejectionInfo
        {
            public int LastAttemptTick;
            public int AttemptCount;
            public string Reason;
        }
        private static Dictionary<IntVec3, RejectionInfo> _rejectedLocations = new Dictionary<IntVec3, RejectionInfo>();
        private const int RejectionCooldown = 108000; // 30 minutes (108,000 ticks) before retrying
        private const int MaxRejectionAttempts = 3; // After 3 attempts, give up on this location

        /// <summary>
        /// Включена ли автоматизация строительства.
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                RimWatchLogger.Info($"BuildingAutomation: {(value ? "Enabled" : "Disabled")}");
            }
        }

        /// <summary>
        /// Вызывается каждый тик для обновления автоматизации.
        /// </summary>
        public static void Tick()
        {
            if (!IsEnabled) return;
            if (!RimWatchCore.AutopilotEnabled) return;

            _tickCounter++;
            if (_tickCounter >= UpdateInterval)
            {
                _tickCounter = 0;
                RimWatchLogger.Info("[BuildingAutomation] Tick! Running building analysis...");
                AnalyzeAndPlanBuildings();
            }
        }

        /// <summary>
        /// Analyzes colony needs and plans necessary buildings.
        /// </summary>
        private static void AnalyzeAndPlanBuildings()
        {
            // v0.8.3: Start performance tracking
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            Map map = Find.CurrentMap;
            if (map == null) return;

            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
            if (colonists.Count == 0) return;

            int colonistCount = colonists.Count;
            var buildings = map.listerBuildings.allBuildingsColonist;
            
            // v0.8.3: Log execution start with key parameters
            RimWatchLogger.LogExecutionStart("BuildingAutomation", "AnalyzeAndPlanBuildings", new Dictionary<string, object>
            {
                { "colonists", colonistCount },
                { "existingBuildings", buildings.Count },
                { "tick", Find.TickManager.TicksGame }
            });

            // ✅ NEW: Check bedroom to colonist ratio
            BedroomStats bedroomStats = GetBedroomStats(map);
            if (bedroomStats.BedroomDeficit > 0)
            {
                // v0.8.2: Use throttled warning to prevent spam (warn once per minute)
                RimWatchLogger.WarningThrottledByKey("bedroom_deficit", $"⚠️ Bedroom deficit detected! {bedroomStats.GetSummary()}");
            }
            else
            {
                RimWatchLogger.Debug($"Bedroom status: {bedroomStats.GetSummary()}");
            }

            // Check for critical building needs
            BuildingNeeds needs = AnalyzeBuildingNeeds(map, colonists);

            int totalNeeds = 0;
            
            // v0.8.3: Log decisions for each building need
            if (needs.NeedsBeds > 0)
            {
                RimWatchLogger.LogDecision("BuildingAutomation", "NeedsBeds", new Dictionary<string, object>
                {
                    { "colonists", colonistCount },
                    { "existingBeds", CountBuildings(map, ThingDefOf.Bed) },
                    { "needCount", needs.NeedsBeds }
                });
                RimWatchLogger.Info($"BuildingAutomation: ⚠️ Need {needs.NeedsBeds} more beds!");
                totalNeeds++;
            }

            if (needs.NeedsKitchen)
            {
                RimWatchLogger.LogDecision("BuildingAutomation", "NeedsKitchen", new Dictionary<string, object>
                {
                    { "colonists", colonistCount },
                    { "hasStove", false }
                });
                RimWatchLogger.Info("BuildingAutomation: ⚠️ Need a kitchen/stove!");
                totalNeeds++;
            }

            if (needs.NeedsPower)
            {
                RimWatchLogger.LogDecision("BuildingAutomation", "NeedsPower", new Dictionary<string, object>
                {
                    { "colonists", colonistCount },
                    { "generators", CountGenerators(map) }
                });
                RimWatchLogger.Info("BuildingAutomation: ⚠️ Need power generation!");
                totalNeeds++;
            }

            if (needs.NeedsResearch)
            {
                RimWatchLogger.LogDecision("BuildingAutomation", "NeedsResearch", new Dictionary<string, object>
                {
                    { "colonists", colonistCount },
                    { "researchBenches", CountBuildings(map, ThingDef.Named("SimpleResearchBench")) }
                });
                RimWatchLogger.Info("BuildingAutomation: ℹ️ Need research facilities");
                totalNeeds++;
            }

            if (needs.NeedsStorage)
            {
                RimWatchLogger.LogDecision("BuildingAutomation", "NeedsStorage", new Dictionary<string, object>
                {
                    { "colonists", colonistCount }
                });
                RimWatchLogger.Info("BuildingAutomation: ℹ️ Need more storage space");
                totalNeeds++;
            }

            if (needs.NeedsWorkshops)
            {
                RimWatchLogger.LogDecision("BuildingAutomation", "NeedsWorkshops", new Dictionary<string, object>
                {
                    { "colonists", colonistCount }
                });
                RimWatchLogger.Info("BuildingAutomation: ℹ️ Need production workshops");
                totalNeeds++;
            }

            if (needs.NeedsGatheringSpot)
            {
                RimWatchLogger.LogDecision("BuildingAutomation", "NeedsGatheringSpot", new Dictionary<string, object>
                {
                    { "colonists", colonistCount }
                });
                RimWatchLogger.Info("BuildingAutomation: ℹ️ Need gathering spot (recreation/parties)");
                totalNeeds++;
            }

            if (totalNeeds == 0)
            {
                RimWatchLogger.Debug("BuildingAutomation: All critical buildings present ✓");
            }
            else
            {
                RimWatchLogger.Info($"BuildingAutomation: Summary - {totalNeeds} building needs detected");
            }

            // **Execute building actions**
            AutoPlaceBuildings(map, needs);
            
            // **NEW: Room building system**
            // Build enclosed rooms with walls and doors
            // ✅ ALWAYS try to build rooms if we have 2+ colonists (they need shelter!)
            if (colonistCount >= 2)
            {
                RimWatchLogger.Debug($"BuildingAutomation: Attempting room building for {colonistCount} colonists");
                AutoBuildRooms(map);
            }

            // ✅ NEW: Auto-unforbid items so colonists can use them!
            AutoUnforbidItems(map);
            
            // **v0.7 ADVANCED: Turrets, repair, and decoration**
            AutoPlaceTurrets(map);
            AutoRepairBuildings(map);
            AutoPlaceDecorations(map);
            
            // v0.8.3: Log execution end with performance tracking
            stopwatch.Stop();
            RimWatchLogger.LogExecutionEnd("BuildingAutomation", "AnalyzeAndPlanBuildings", true, stopwatch.ElapsedMilliseconds, $"Processed {totalNeeds} needs");
            
            // v0.8.3: Log performance warning if operation took too long (>5ms threshold)
            if (stopwatch.ElapsedMilliseconds > 5)
            {
                RimWatchLogger.LogPerformance("BuildingAutomation", "AnalyzeAndPlanBuildings", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
                {
                    { "threshold", 5 },
                    { "colonists", colonistCount },
                    { "buildings", buildings.Count },
                    { "totalNeeds", totalNeeds }
                });
            }
        }

        /// <summary>
        /// Checks if we can place a building of this type (cooldown check).
        /// </summary>
        private static bool CanPlaceBuildingType(string buildingType)
        {
            if (!_lastPlacementTick.ContainsKey(buildingType))
            {
                return true;
            }

            int ticksSince = Find.TickManager.TicksGame - _lastPlacementTick[buildingType];
            return ticksSince >= PlacementCooldown;
        }

        /// <summary>
        /// Records that we placed a building of this type.
        /// </summary>
        private static void RecordPlacement(string buildingType)
        {
            _lastPlacementTick[buildingType] = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// Counts buildings including blueprints and frames under construction.
        /// </summary>
        private static int CountBuildingsAndPlanned(Map map, Func<ThingDef, bool> filter)
        {
            int count = 0;

            // Built buildings
            count += map.listerBuildings.allBuildingsColonist
                .Count(b => filter(b.def));

            // Blueprints
            var blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint);
            foreach (Thing thing in blueprints)
            {
                if (thing is Blueprint bp && bp.def.entityDefToBuild is ThingDef thingDef)
                {
                    if (filter(thingDef))
                    {
                        count++;
                    }
                }
            }

            // Frames (under construction)
            var frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame);
            foreach (Thing thing in frames)
            {
                if (thing is Frame frame && frame.def.entityDefToBuild is ThingDef thingDef)
                {
                    if (filter(thingDef))
                    {
                        count++;
                    }
                }
            }

            return count;
        }
        
        /// <summary>
        /// v0.8.3: Helper method to count specific buildings.
        /// </summary>
        private static int CountBuildings(Map map, ThingDef buildingDef)
        {
            if (buildingDef == null) return 0;
            return map.listerBuildings.allBuildingsColonist.Count(b => b.def == buildingDef);
        }
        
        /// <summary>
        /// v0.8.3: Helper method to count all generators.
        /// </summary>
        private static int CountGenerators(Map map)
        {
            return map.listerBuildings.allBuildingsColonist.Count(b => 
                b.TryGetComp<CompPowerPlant>() != null);
        }

        /// <summary>
        /// Analyzes what buildings the colony needs.
        /// </summary>
        private static BuildingNeeds AnalyzeBuildingNeeds(Map map, List<Pawn> colonists)
        {
            BuildingNeeds needs = new BuildingNeeds();
            int colonistCount = colonists.Count;

            // ✅ Check beds (including blueprints and frames)
            // Count bed SLOTS (double beds = 2 slots!)
            int bedSlots = 0;
            
            // Count existing beds
            foreach (Building bed in map.listerBuildings.allBuildingsColonist)
            {
                if (bed is Building_Bed buildingBed && buildingBed.def.building.bed_humanlike)
                {
                    bedSlots += buildingBed.SleepingSlotsCount; // 1 for single, 2 for double
                }
            }
            
            // Count planned beds (blueprints)
            var blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint);
            foreach (Thing thing in blueprints)
            {
                if (thing is Blueprint bp && bp.def.entityDefToBuild is ThingDef thingDef)
                {
                    if (thingDef.building != null && thingDef.building.bed_humanlike)
                    {
                        bedSlots += thingDef.building.bed_humanlike ? 1 : 0; // Assume 1 slot for planned beds
                    }
                }
            }
            
            // Count frames under construction
            var frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame);
            foreach (Thing thing in frames)
            {
                if (thing is Frame frame && frame.def.entityDefToBuild is ThingDef thingDef)
                {
                    if (thingDef.building != null && thingDef.building.bed_humanlike)
                    {
                        bedSlots += thingDef.building.bed_humanlike ? 1 : 0;
                    }
                }
            }
            
            needs.NeedsBeds = System.Math.Max(0, colonistCount - bedSlots);

            // Check kitchen (including blueprints and frames)
            int stoveCount = CountBuildingsAndPlanned(map, def =>
                def.defName.ToLower().Contains("stove") ||
                def.defName.ToLower().Contains("fueled"));
            needs.NeedsKitchen = stoveCount == 0;

            // Check power (including blueprints and frames)
            int powerCount = CountBuildingsAndPlanned(map, def =>
                def.defName.Contains("Generator") ||
                def.defName.Contains("Solar") ||
                def.defName.Contains("Geothermal"));
            needs.NeedsPower = powerCount == 0;

            // Check research (including blueprints and frames)
            int researchCount = CountBuildingsAndPlanned(map, def =>
                def.defName.ToLower().Contains("research"));
            needs.NeedsResearch = researchCount == 0;

            // Check storage (including blueprints and frames + stockpile zones)
            int shelvesCount = CountBuildingsAndPlanned(map, def =>
                def.defName.ToLower().Contains("shelf") ||
                def.defName.ToLower().Contains("equipment"));
            int stockpileZones = map.zoneManager.AllZones
                .OfType<Zone_Stockpile>().Count();
            
            // 1 shelf per 2 colonists, or stockpile zones count as 2 shelves worth
            int effectiveStorage = shelvesCount + (stockpileZones * 2);
            needs.NeedsStorage = effectiveStorage < (colonistCount / 2);

            // Check workshops (including blueprints and frames)
            int workshopCount = CountBuildingsAndPlanned(map, def =>
                def.defName.ToLower().Contains("craft") ||
                def.defName.ToLower().Contains("bench") ||
                def.defName.ToLower().Contains("table"));
            needs.NeedsWorkshops = workshopCount == 0;

            // Check gathering spots (including blueprints and frames)
            int gatheringCount = CountBuildingsAndPlanned(map, def =>
                def.defName.ToLower().Contains("campfire") ||
                def.defName.ToLower().Contains("horseshoe") ||
                def.defName.ToLower().Contains("gathering"));
            needs.NeedsGatheringSpot = gatheringCount == 0;

            return needs;
        }

        /// <summary>
        /// Structure for storing building needs analysis.
        /// </summary>
        private class BuildingNeeds
        {
            public int NeedsBeds { get; set; } = 0;
            public bool NeedsKitchen { get; set; } = false;
            public bool NeedsPower { get; set; } = false;
            public bool NeedsResearch { get; set; } = false;
            public bool NeedsStorage { get; set; } = false;
            public bool NeedsWorkshops { get; set; } = false;
            public bool NeedsGatheringSpot { get; set; } = false; // ✅ NEW
        }

        // ==================== ACTION METHODS ====================

        /// <summary>
        /// Checks if a bedroom room is currently being planned or built.
        /// ✅ NEW: Prevents placing standalone beds if room is in progress!
        /// Also checks frames (walls under construction).
        /// </summary>
        private static bool CheckIfBedroomRoomInProgress(Map map)
        {
            try
            {
                // Check for bedroom room walls under construction (blueprints)
                List<Blueprint> blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint)
                    .OfType<Blueprint>()
                    .Where(b => 
                    {
                        ThingDef? def = b.def.entityDefToBuild as ThingDef;
                        return def != null && def.IsEdifice();
                    })
                    .ToList();

                // Count walls being built (indicator of room construction)
                int wallBlueprints = blueprints.Count(b => 
                {
                    ThingDef? def = b.def.entityDefToBuild as ThingDef;
                    return def != null && (def.defName == "Wall" || def.building?.isNaturalRock == false);
                });

                // Also check frames (walls under construction)
                List<Frame> frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame)
                    .OfType<Frame>()
                    .Where(f =>
                    {
                        ThingDef? def = f.def.entityDefToBuild as ThingDef;
                        return def != null && (def.defName == "Wall" || def.building?.isNaturalRock == false);
                    })
                    .ToList();

                int wallFrames = frames.Count;

                // If 4+ walls (blueprints OR frames) are being built, assume bedroom room in progress
                if (wallBlueprints + wallFrames >= 4)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in CheckIfBedroomRoomInProgress", ex);
                return false; // Fallback: allow bed placement
            }
        }

        /// <summary>
        /// Automatically places blueprints for needed buildings.
        /// Progressive implementation - places essential structures for colony survival.
        /// </summary>
        private static void AutoPlaceBuildings(Map map, BuildingNeeds needs)
        {
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;

            // ✅ Check if bedroom rooms are being planned/built
            bool bedroomRoomInProgress = CheckIfBedroomRoomInProgress(map);

            // ✅ Ensure construction is top priority while critical housing is missing or in progress
            if (needs.NeedsBeds > 0 || bedroomRoomInProgress)
            {
                EnsureConstructionHighPriority(map);
            }

            // Priority 1: Beds (colonists need rest)
            // ✅ IMPORTANT: Don't place standalone beds if bedroom room is being built!
            if (needs.NeedsBeds > 0 && !bedroomRoomInProgress)
            {
                AutoPlaceBeds(map, needs.NeedsBeds);
            }
            else if (needs.NeedsBeds > 0 && bedroomRoomInProgress)
            {
                RimWatchLogger.Debug("BuildingAutomation: Skipping standalone beds - bedroom room in progress");
            }

            // Priority 2: Kitchen (colonists need food)
            if (needs.NeedsKitchen)
            {
                AutoPlaceKitchen(map);
            }

            // Priority 3: Power (enables most other buildings)
            if (needs.NeedsPower)
            {
                AutoPlacePower(map);
            }
            
            // Priority 4: Storage (keep resources organized)
            if (needs.NeedsStorage)
            {
                AutoCreateStorageZones(map);
            }
            
            // Priority 5: Gathering spot (prevent mental breaks, enable recreation)
            if (needs.NeedsGatheringSpot)
            {
                AutoPlaceGatheringSpot(map);
            }

            // Priority 6: Workshops (production buildings)
            if (needs.NeedsWorkshops)
            {
                AutoPlaceWorkshops(map);
            }

            // Priority 7: Base expansion for growing colonies
            AutoExpandBase(map, colonistCount);
        }

        /// <summary>
        /// Temporarily ensures Construction is set to highest priority for all colonists.
        /// Uses cooldown to prevent flickering.
        /// </summary>
        private static void EnsureConstructionHighPriority(Map map)
        {
            try
            {
                // Check cooldown to prevent priority flickering
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - _lastPriorityChangeTick < PriorityChangeCooldown)
                {
                    return; // Skip if changed recently
                }

                bool anyChanges = false;
                
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (pawn?.workSettings == null) continue;
                    if (!pawn.workSettings.EverWork) continue;
                    
                    // Enable work settings if needed
                    pawn.workSettings.EnableAndInitialize();
                    
                    // Set construction to highest priority (1) if not already
                    if (pawn.workSettings.GetPriority(WorkTypeDefOf.Construction) != 1)
                    {
                        pawn.workSettings.SetPriority(WorkTypeDefOf.Construction, 1);
                        anyChanges = true;
                    }
                }

                if (anyChanges)
                {
                    _lastPriorityChangeTick = currentTick;
                    RimWatchLogger.Debug("BuildingAutomation: Set Construction priority to 1 for all colonists");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"BuildingAutomation: Failed to boost construction priority: {ex.Message}");
            }
        }

        /// <summary>
        /// Automatically places bed blueprints using smart location finding.
        /// v0.7.9: Prioritizes installing existing minified beds from storage.
        /// </summary>
        private static void AutoPlaceBeds(Map map, int bedsNeeded)
        {
            try
            {
                // Check cooldown
                if (!CanPlaceBuildingType("Bed"))
                {
                    RimWatchLogger.Debug("BuildingAutomation: Bed placement on cooldown");
                    return;
                }

                // Get settings
                RimWatchMod modInstance = LoadedModManager.GetMod<RimWatchMod>();
                string logLevel = modInstance?.GetSettings<RimWatchSettings>()?.buildingLogLevel.ToString() ?? "Moderate";

                // v0.7.9: FIRST, check for minified beds in storage
                int installed = InstallMinifiedFurniture(map, "Bed", bedsNeeded, logLevel);
                if (installed > 0)
                {
                    RimWatchLogger.Info($"✅ BuildingAutomation: Installed {installed} existing beds from storage");
                    bedsNeeded -= installed;
                    RecordPlacement("Bed");
                }
                
                if (bedsNeeded <= 0) return; // All needs met by existing furniture

                // Select bed type
                ThingDef bedDef = BuildingSelector.SelectBed(map, map.mapPawns.FreeColonistsSpawnedCount);
                if (bedDef == null)
                {
                    RimWatchLogger.Warning("BuildingAutomation: Bed ThingDef not found!");
                    return;
                }

                ThingDef stuffDef = GenStuff.DefaultStuffFor(bedDef);
                
                int placed = 0;
                int maxToPlace = Math.Min(bedsNeeded, 2); // Max 2 beds per cycle

                for (int i = 0; i < maxToPlace; i++)
                {
                    // Find best location using LocationFinder
                    IntVec3 location = LocationFinder.FindBestLocation(
                        map,
                        bedDef,
                        LocationFinder.BuildingRole.Bedroom,
                        logLevel
                    );

                    if (location == IntVec3.Invalid)
                    {
                        if (logLevel != "Minimal")
                        {
                            RimWatchLogger.Warning($"BuildingAutomation: Could not find location for bed #{i + 1}");
                        }
                        break;
                    }

                    // Place blueprint via Designator with rotation probing
                    Rot4 usedRot;
                    bool success = BuildPlacer.TryPlaceWithBestRotation(map, bedDef, location, stuffDef, out usedRot, logLevel);
                    if (success)
                    {
                        placed++;
                        if (logLevel == "Minimal")
                        {
                            RimWatchLogger.Info($"✅ Placed bed at ({location.x}, {location.z})");
                        }
                        else
                        {
                            RimWatchLogger.Info($"🛏️ BuildingAutomation: Placed bed #{i + 1} at ({location.x}, {location.z})");
                        }
                        // Ensure roof over bed
                        RoofPlanner.BuildRoofOver(map, location, bedDef, usedRot, 0, logLevel);
                    }
                    else
                    {
                        RimWatchLogger.Warning($"BuildingAutomation: Failed to place bed at ({location.x}, {location.z})");
                    }
                }

                if (placed > 0)
                {
                    RecordPlacement("Bed");
                    
                    // Update zone cache after bed placements
                    BaseZoneCache.UpdateCache(map);
                    RimWatchLogger.Debug($"BuildingAutomation: Zone cache updated after placing {placed} bed(s)");
                    }
                else if (bedsNeeded > 0 && logLevel != "Minimal")
                {
                    RimWatchLogger.Warning("❌ BuildingAutomation: Failed to place beds - no suitable locations");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in AutoPlaceBeds", ex);
            }
        }

        /// <summary>
        /// Checks if a bed can be placed at the given location.
        /// </summary>
        private static bool CanPlaceBedAt(Map map, IntVec3 cell, ThingDef bedDef)
        {
            // Check if cell is valid
            if (!cell.InBounds(map)) return false;
            if (!cell.Standable(map)) return false;
            
            // Check if there's already something here
            if (cell.GetFirstBuilding(map) != null) return false;
            if (cell.GetFirstItem(map) != null) return false;
            
            // Check if there's already a blueprint here
            List<Thing> existingThings = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint);
            if (existingThings.Any(t => t.Position == cell && t is Blueprint)) return false;
            
            // Check size (beds are 1x2, need to check both cells)
            foreach (IntVec3 offset in GenAdj.OccupiedRect(cell, Rot4.North, bedDef.Size))
            {
                if (!offset.InBounds(map)) return false;
                if (!offset.Standable(map)) return false;
                if (offset.GetFirstBuilding(map) != null) return false;
            }
            
            return true;
        }

        /// <summary>
        /// Automatically places a cooking stove using smart selection and location finding.
        /// ✅ UPDATED: Now checks if kitchen room is being built to avoid placing stove outside.
        /// </summary>
        private static void AutoPlaceKitchen(Map map)
        {
            try
            {
                // Check cooldown
                if (!CanPlaceBuildingType("Stove"))
                {
                    RimWatchLogger.Debug("BuildingAutomation: Stove placement on cooldown");
                    return;
                }

                // ✅ NEW: Check if kitchen room is being built
                List<RoomBuilding.RoomConstructionManager.RoomConstructionState> activeRooms = 
                    RoomBuilding.RoomConstructionManager.GetActiveConstructions(map);
                
                bool kitchenRoomInProgress = activeRooms.Any(state => 
                    state.Plan.Role == RoomBuilding.RoomPlanner.RoomRole.Kitchen && 
                    state.Stage < RoomBuilding.RoomConstructionManager.ConstructionStage.COMPLETE);

                if (kitchenRoomInProgress)
                {
                    RimWatchLogger.Info("BuildingAutomation: Skipping standalone stove - kitchen room is being built");
                    return;
                }

                // Get settings
                RimWatchMod modInstance = LoadedModManager.GetMod<RimWatchMod>();
                string logLevel = modInstance?.GetSettings<RimWatchSettings>()?.buildingLogLevel.ToString() ?? "Moderate";

                // Smart stove selection (Fueled vs Electric)
                ThingDef stoveDef = BuildingSelector.SelectStove(map, IntVec3.Invalid);
                if (stoveDef == null)
                {
                    RimWatchLogger.Warning("BuildingAutomation: No stove ThingDef found!");
                    return;
                }

                // Find best location using global LocationFinder
                IntVec3 location = LocationFinder.FindBestLocation(
                    map,
                    stoveDef,
                    LocationFinder.BuildingRole.Kitchen,
                    logLevel
                );
                
                if (location == IntVec3.Invalid)
                {
                    // Fallback: use legacy kitchen location finder (more permissive)
                    IntVec3 fallback = FindKitchenLocation(map);
                    if (fallback == IntVec3.Invalid)
                    {
                        if (logLevel != "Minimal")
                        {
                            RimWatchLogger.Warning("❌ BuildingAutomation: Could not find suitable location for kitchen (LocationFinder + fallback both failed)");
                        }
                        
                        // Track failure pattern for diagnostics
                        RimWatchLogger.LogFailure(
                            "BuildingAutomation",
                            "AutoPlaceKitchen",
                            "No valid kitchen location found",
                            new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "colonists", map.mapPawns.FreeColonistsSpawnedCount },
                                { "buildings", map.listerBuildings.allBuildingsColonist.Count },
                                { "logLevel", logLevel }
                            });
                        
                        return;
                    }
                    
                    location = fallback;
                    if (logLevel == "Verbose" || logLevel == "Debug")
                    {
                        RimWatchLogger.Debug($"BuildingAutomation: Using fallback kitchen location at ({location.x}, {location.z})");
                    }
                }

                // Re-select stove with actual location (for power check)
                stoveDef = BuildingSelector.SelectStove(map, location);
                ThingDef? stuffDef = StuffSelector.DefaultNonSteelStuffFor(stoveDef, map);

                // Place blueprint via Designator with rotation probing
                Rot4 usedRot;
                bool success = BuildPlacer.TryPlaceWithBestRotation(map, stoveDef, location, stuffDef, out usedRot, logLevel);
                if (success)
                {
                    if (logLevel == "Minimal")
                    {
                        RimWatchLogger.Info($"✅ Placed {stoveDef.label} at ({location.x}, {location.z})");
                    }
                    else
                    {
                        RimWatchLogger.Info($"🍳 BuildingAutomation: Placed {stoveDef.label} at ({location.x}, {location.z})");
                    }
                    RecordPlacement("Stove");
                    
                    // Update zone cache after successful placement
                    BaseZoneCache.UpdateCache(map);
                    RimWatchLogger.Debug("BuildingAutomation: Zone cache updated after kitchen placement");

                    // Auto-connect to power grid if needed
                    bool needsPower = stoveDef.comps != null && stoveDef.comps.Any(c => c.compClass?.Name == "CompPowerTrader");
                    if (needsPower)
                    {
                        PowerPlanner.ConnectToNearestGrid(map, location, 30, logLevel);
                    }
                    // Ensure roof over kitchen
                    RoofPlanner.BuildRoofOver(map, location, stoveDef, usedRot, 0, logLevel);
                }
                else
                {
                    RimWatchLogger.Error($"BuildingAutomation: Failed to place kitchen (Designator rejected) at ({location.x}, {location.z})");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in AutoPlaceKitchen", ex);
            }
        }

        /// <summary>
        /// Finds a suitable location for a kitchen stove.
        /// Prefers roofed areas near existing structures.
        /// </summary>
        /// <summary>
        /// v0.8.5: Enhanced kitchen location finder with multiple fallback strategies and detailed logging.
        /// </summary>
        private static IntVec3 FindKitchenLocation(Map map)
        {
            try
            {
                // Find center of existing buildings
                List<Building> buildings = map.listerBuildings.allBuildingsColonist.ToList();
                IntVec3 baseCenter = map.Center;
                
                if (buildings.Count > 0)
                {
                    int avgX = (int)buildings.Average(b => b.Position.x);
                    int avgZ = (int)buildings.Average(b => b.Position.z);
                    baseCenter = new IntVec3(avgX, 0, avgZ);
                }

                // v0.8.5: STRATEGY 1 - Search for roofed locations near base (preferred)
                RimWatchLogger.LogDecision("BuildingAutomation", "FindKitchenLocation", new Dictionary<string, object>
                {
                    { "strategy", "roofed_near_base" },
                    { "baseCenter", baseCenter.ToString() },
                    { "buildingCount", buildings.Count }
                });
                
                List<IntVec3> candidates = new List<IntVec3>();
                int rejectedReasons = 0;
                
                for (int radius = 5; radius < 30; radius += 5)
                {
                    for (int angle = 0; angle < 360; angle += 45)
                    {
                        float radians = angle * (float)Math.PI / 180f;
                        int x = baseCenter.x + (int)(radius * Math.Cos(radians));
                        int z = baseCenter.z + (int)(radius * Math.Sin(radians));
                        IntVec3 candidate = new IntVec3(x, 0, z);

                        if (!candidate.InBounds(map))
                        {
                            rejectedReasons++;
                            continue;
                        }
                        
                        if (!candidate.Standable(map))
                        {
                            rejectedReasons++;
                            continue;
                        }
                        
                        if (!CanPlaceBuildingAt(map, candidate, new IntVec2(2, 2)))
                        {
                            rejectedReasons++;
                            continue;
                        }
                        
                        candidates.Add(candidate);
                    }
                    
                    if (candidates.Count > 0)
                    {
                        RimWatchLogger.LogDecision("BuildingAutomation", "KitchenLocationFound", new Dictionary<string, object>
                        {
                            { "strategy", "roofed_near_base" },
                            { "location", candidates.First().ToString() },
                            { "candidatesFound", candidates.Count },
                            { "rejected", rejectedReasons }
                        });
                        return candidates.First();
                    }
                }

                // v0.8.5: STRATEGY 2 - Wider search radius (up to 50 cells)
                RimWatchLogger.LogDecision("BuildingAutomation", "FindKitchenLocation", new Dictionary<string, object>
                {
                    { "strategy", "wider_radius" },
                    { "previousRejected", rejectedReasons }
                });
                
                candidates.Clear();
                for (int radius = 30; radius < 50; radius += 5)
                {
                    for (int angle = 0; angle < 360; angle += 30)
                    {
                        float radians = angle * (float)Math.PI / 180f;
                        int x = baseCenter.x + (int)(radius * Math.Cos(radians));
                        int z = baseCenter.z + (int)(radius * Math.Sin(radians));
                        IntVec3 candidate = new IntVec3(x, 0, z);

                        if (!candidate.InBounds(map)) continue;
                        if (!candidate.Standable(map)) continue;
                        if (!CanPlaceBuildingAt(map, candidate, new IntVec2(2, 2))) continue;
                        
                        candidates.Add(candidate);
                    }
                    
                    if (candidates.Count > 0)
                    {
                        RimWatchLogger.LogDecision("BuildingAutomation", "KitchenLocationFound", new Dictionary<string, object>
                        {
                            { "strategy", "wider_radius" },
                            { "location", candidates.First().ToString() },
                            { "radius", radius }
                        });
                        return candidates.First();
                    }
                }

                // v0.8.5: STRATEGY 3 - Desperate: Find ANY standable location with 1x1 space
                RimWatchLogger.LogDecision("BuildingAutomation", "FindKitchenLocation", new Dictionary<string, object>
                {
                    { "strategy", "desperate_1x1" }
                });
                
                for (int radius = 5; radius < 60; radius += 10)
                {
                    for (int angle = 0; angle < 360; angle += 45)
                    {
                        float radians = angle * (float)Math.PI / 180f;
                        int x = baseCenter.x + (int)(radius * Math.Cos(radians));
                        int z = baseCenter.z + (int)(radius * Math.Sin(radians));
                        IntVec3 candidate = new IntVec3(x, 0, z);

                        if (!candidate.InBounds(map)) continue;
                        if (!candidate.Standable(map)) continue;
                        if (!CanPlaceBuildingAt(map, candidate, new IntVec2(1, 1))) continue;
                        
                        RimWatchLogger.LogDecision("BuildingAutomation", "KitchenLocationFound", new Dictionary<string, object>
                        {
                            { "strategy", "desperate_1x1" },
                            { "location", candidate.ToString() },
                            { "warning", "Found 1x1 space, may be tight" }
                        });
                        return candidate;
                    }
                }

                // Complete failure - log detailed reason
                RimWatchLogger.LogFailure("BuildingAutomation", "FindKitchenLocation", "No valid location found after all strategies",
                    new Dictionary<string, object>
                    {
                        { "strategiesAttempted", 3 },
                        { "baseCenter", baseCenter.ToString() },
                        { "mapSize", $"{map.Size.x}x{map.Size.z}" },
                        { "buildingCount", buildings.Count }
                    });

                return IntVec3.Invalid;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in FindKitchenLocation", ex);
                return IntVec3.Invalid;
            }
        }

        /// <summary>
        /// Automatically places a power generator using smart selection.
        /// </summary>
        private static void AutoPlacePower(Map map)
        {
            try
            {
                // Check cooldown
                if (!CanPlaceBuildingType("Power"))
                {
                    RimWatchLogger.Debug("BuildingAutomation: Power placement on cooldown");
                    return;
                }

                // Get settings
                RimWatchMod modInstance = LoadedModManager.GetMod<RimWatchMod>();
                string logLevel = modInstance?.GetSettings<RimWatchSettings>()?.buildingLogLevel.ToString() ?? "Moderate";

                // Smart generator selection
                ThingDef powerDef = BuildingSelector.SelectPowerGenerator(map);
                if (powerDef == null)
                {
                    RimWatchLogger.Warning("BuildingAutomation: No power generator ThingDef found!");
                    return;
                }

                // ✅ NEW: Find location for generator - PREFER outdoor placement!
                IntVec3 location = IntVec3.Invalid;
                bool isWoodPowered = powerDef.defName.Contains("Wood") || 
                                     powerDef.defName.Contains("Fueled") ||
                                     powerDef.defName == "Generator";
                
                // v0.8.4++: КРИТИЧНО - генераторы ВСЕГДА снаружи, если нет технической комнаты!
                // Wood-fired generators CAN be outdoors in RimWorld - они не требуют roof!
                
                // Сначала пробуем найти локацию СНАРУЖИ (правильный способ)
                location = LocationFinder.FindBestLocation(
                    map,
                    powerDef,
                    LocationFinder.BuildingRole.Power,
                    logLevel
                );
                
                // Если снаружи не нашли, только ТОГДА пробуем около стены комнаты
                if (location == IntVec3.Invalid)
                {
                    if (isWoodPowered)
                    {
                        // Только если дровяной и нет места снаружи - ставим у стены
                        location = FindLocationInPowerRoom(map, powerDef, logLevel);
                        
                        if (location == IntVec3.Invalid)
                        {
                            RimWatchLogger.Warning("⚠️ BuildingAutomation: No suitable location for generator (tried outdoor and indoor)");
                            return;
                        }
                    }
                    else
                    {
                        // Солнечные панели ТОЛЬКО снаружи!
                        RimWatchLogger.Warning("❌ BuildingAutomation: Could not find outdoor location for solar generator");
                        return;
                    }
                }
                
                // v0.8.2: Check if this location was previously rejected
                if (IsLocationRejected(location))
                {
                    if (logLevel != "Minimal")
                    {
                        var rejection = _rejectedLocations[location];
                        RimWatchLogger.Debug($"BuildingAutomation: Skipping rejected location ({location.x}, {location.z}) - {rejection.Reason} (attempt {rejection.AttemptCount}/{MaxRejectionAttempts})");
                    }
                    return;
                }

                ThingDef stuffDef = GenStuff.DefaultStuffFor(powerDef);

            // Place blueprint via Designator with rotation probing
            Rot4 usedRot;
            bool success = BuildPlacer.TryPlaceWithBestRotation(map, powerDef, location, stuffDef, out usedRot, logLevel);
            if (success)
            {
                if (logLevel == "Minimal")
                {
                    RimWatchLogger.Info($"✅ Placed {powerDef.label} at ({location.x}, {location.z})");
                }
                else
                {
                    RimWatchLogger.Info($"⚡ BuildingAutomation: Placed {powerDef.label} at ({location.x}, {location.z})");
                }
                RecordPlacement("Power");
                
                // Update zone cache after power placement
                BaseZoneCache.UpdateCache(map);
                RimWatchLogger.Debug("BuildingAutomation: Zone cache updated after power placement");

                // For solar/wind, ensure no roof above
                bool isSolar = powerDef.defName.Contains("Solar");
                bool isWind = powerDef.defName.Contains("Wind");
                if (isSolar || isWind)
                {
                    RoofPlanner.RemoveRoofOver(map, location, powerDef, usedRot, 0, logLevel);
                }
                }
                else
                {
                    // v0.8.2: Record rejection to prevent repeated attempts
                    RecordRejection(location, "Designator rejected placement");
                    
                    // Only log warning if this is first or second attempt
                    if (!_rejectedLocations.ContainsKey(location) || _rejectedLocations[location].AttemptCount <= 2)
                    {
                        RimWatchLogger.Warning($"BuildingAutomation: Failed to place power at ({location.x}, {location.z}) - will retry after cooldown");
                    }
                    
                    // Structured failure tracking for repeated generator placement problems
                    RimWatchLogger.LogFailure(
                        "BuildingAutomation",
                        "AutoPlacePower",
                        "Designator rejected power placement",
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "location", $"({location.x}, {location.z})" },
                            { "generator", powerDef.defName },
                            { "isWoodPowered", isWoodPowered },
                            { "logLevel", logLevel }
                        });
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in AutoPlacePower", ex);
            }
        }

        /// <summary>
        /// Finds a suitable outdoor location for a power generator.
        /// </summary>
        private static IntVec3 FindPowerLocation(Map map)
        {
            try
            {
                // Find center of existing buildings
                List<Building> buildings = map.listerBuildings.allBuildingsColonist.ToList();
                IntVec3 baseCenter = map.Center;
                
                if (buildings.Count > 0)
                {
                    int avgX = (int)buildings.Average(b => b.Position.x);
                    int avgZ = (int)buildings.Average(b => b.Position.z);
                    baseCenter = new IntVec3(avgX, 0, avgZ);
                }

                // Search for outdoor locations near base
                List<IntVec3> candidates = new List<IntVec3>();
                
                for (int radius = 5; radius < 30; radius += 3)
                {
                    for (int angle = 0; angle < 360; angle += 30)
                    {
                        float radians = angle * (float)Math.PI / 180f;
                        int x = baseCenter.x + (int)(radius * Math.Cos(radians));
                        int z = baseCenter.z + (int)(radius * Math.Sin(radians));
                        IntVec3 candidate = new IntVec3(x, 0, z);

                        if (!candidate.InBounds(map)) continue;
                        
                        // Power should be outdoors (no roof) or in open area
                        if (!candidate.Roofed(map) && 
                            candidate.Standable(map) &&
                            CanPlaceBuildingAt(map, candidate, new IntVec2(2, 2))) // Generator is 2x2
                        {
                            candidates.Add(candidate);
                        }
                    }
                    
                    if (candidates.Count > 0)
                    {
                        return candidates.First(); // Return first suitable location
                    }
                }

                return IntVec3.Invalid;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in FindPowerLocation", ex);
                return IntVec3.Invalid;
            }
        }

        /// <summary>
        /// Finds a suitable location for generator INSIDE a PowerRoom.
        /// </summary>
        private static IntVec3 FindLocationInPowerRoom(Map map, ThingDef generatorDef, string logLevel)
        {
            try
            {
                // v0.8.4++: КРИТИЧНО - генераторы НЕ должны быть в центре жилых комнат!
                // Генераторы: СНАРУЖИ или у СТЕНЫ комнаты, НЕ в центре!
                
                // Find all roofed, enclosed rooms
                var allRooms = map.regionGrid.AllRooms
                    .Where(r => !r.PsychologicallyOutdoors &&
                               !r.IsHuge &&
                               r.ProperRoom)
                    .ToList();
                
                if (allRooms.Count == 0)
                {
                    RimWatchLogger.Debug("FindLocationInPowerRoom: No rooms found, generator will be placed outside");
                    return IntVec3.Invalid;
                }
                
                // ✅ CRITICAL: Check generator size (usually 2x2)
                IntVec2 generatorSize = generatorDef.size;
                
                // v0.8.4++: Ищем место У СТЕНЫ комнаты, НЕ в центре!
                foreach (var room in allRooms)
                {
                    // Находим клетки ОКОЛО стены (не в центре!)
                    var wallCandidates = room.Cells
                        .Where(c => IsNearWall(map, c, room) && CanFitGeneratorAt(map, c, generatorSize, room))
                        .ToList();
                    
                    if (wallCandidates.Count > 0)
                    {
                        // Берём случайную клетку около стены
                        IntVec3 bestCell = wallCandidates.RandomElement();
                        
                        RimWatchLogger.Info($"✅ FindLocationInPowerRoom: Found location NEAR WALL at ({bestCell.x}, {bestCell.z}) for {generatorSize.x}x{generatorSize.z} generator");
                        return bestCell;
                    }
                }
                
                RimWatchLogger.Warning("FindLocationInPowerRoom: No valid wall location found - generator should be placed OUTSIDE!");
                return IntVec3.Invalid;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FindLocationInPowerRoom: Error", ex);
                return IntVec3.Invalid;
            }
        }
        
        /// <summary>
        /// v0.8.4++: Проверяет что клетка находится ОКОЛО стены (не в центре комнаты).
        /// </summary>
        private static bool IsNearWall(Map map, IntVec3 cell, Room room)
        {
            // Проверяем соседние клетки - хотя бы одна должна быть стеной
            foreach (IntVec3 adjacentCell in GenAdj.CellsAdjacent8Way(new TargetInfo(cell, map)))
            {
                if (!adjacentCell.InBounds(map)) continue;
                
                // Если соседняя клетка - стена, то мы около стены
                Building building = adjacentCell.GetFirstBuilding(map);
                if (building != null && building.def.building != null && building.def.building.isNaturalRock)
                {
                    return true; // Около натуральной стены
                }
                
                // Или около построенной стены
                if (building != null && building.def.defName.Contains("Wall"))
                {
                    return true;
                }
                
                // Или соседняя клетка не в комнате (значит граница комнаты)
                if (!room.Cells.Contains(adjacentCell))
                {
                    return true;
                }
            }
            
            return false; // В центре комнаты - НЕ подходит!
        }

        /// <summary>
        /// Checks if generator of given size can fit at location (all cells must be free).
        /// </summary>
        private static bool CanFitGeneratorAt(Map map, IntVec3 origin, IntVec2 size, Room room)
        {
            try
            {
                // Check all cells in generator footprint
                for (int x = 0; x < size.x; x++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        IntVec3 cell = origin + new IntVec3(x, 0, z);
                        
                        // Cell must be in bounds
                        if (!cell.InBounds(map))
                            return false;
                        
                        // Cell must be in the room
                        if (!room.Cells.Contains(cell))
                            return false;
                        
                        // Cell must be standable
                        if (!cell.Standable(map))
                            return false;
                        
                        // Cell must not have buildings
                        if (cell.GetFirstBuilding(map) != null)
                            return false;
                        
                        // Cell must not have blueprints or frames
                        var things = cell.GetThingList(map);
                        if (things.Any(t => t is Blueprint || t is Frame || t.def.category == ThingCategory.Building))
                            return false;
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
        /// Creates storage zones for resources.
        /// </summary>
        private static void AutoCreateStorageZones(Map map)
        {
            try
            {
                // Find suitable indoor location for storage
                IntVec3 location = FindStorageLocation(map);
                
                if (location == IntVec3.Invalid)
                {
                    RimWatchLogger.Warning("BuildingAutomation: Could not find suitable location for storage zone");
                    return;
                }

                // Create 8x8 storage zone
                int zoneSize = 8;
                IntVec3 zoneMin = new IntVec3(
                    location.x - zoneSize / 2,
                    location.y,
                    location.z - zoneSize / 2
                );

                // ✅ CHECK: Will this zone overlap with existing zones or blueprints?
                bool hasOverlap = false;
                for (int x = zoneMin.x; x < zoneMin.x + zoneSize && !hasOverlap; x++)
                {
                    for (int z = zoneMin.z; z < zoneMin.z + zoneSize && !hasOverlap; z++)
                    {
                        IntVec3 checkCell = new IntVec3(x, 0, z);
                        if (!checkCell.InBounds(map)) continue;
                        
                        // Check for existing zones
                        Zone existingZone = map.zoneManager.ZoneAt(checkCell);
                        if (existingZone != null)
                        {
                            hasOverlap = true;
                            break;
                        }
                        
                        // Check for blueprints
                        if (checkCell.GetFirstBuilding(map) != null || checkCell.GetFirstItem(map) != null)
                        {
                            hasOverlap = true;
                            break;
                        }
                    }
                }
                
                if (hasOverlap)
                {
                    RimWatchLogger.Debug($"BuildingAutomation: Storage zone location ({location.x}, {location.z}) overlaps with existing zones/buildings - skipping");
                    return;
                }

                // Create stockpile zone
                Zone_Stockpile zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
                
                // Add cells to zone
                int cellsAdded = 0;
                for (int x = zoneMin.x; x < zoneMin.x + zoneSize; x++)
                {
                    for (int z = zoneMin.z; z < zoneMin.z + zoneSize; z++)
                    {
                        IntVec3 cell = new IntVec3(x, 0, z);
                        // ✅ RELAXED: Don't require roofed - outdoor storage is OK for early game
                        // ❌ BUT: Never place storage directly in water / marsh
                        if (IsGoodStorageCell(map, cell))
                        {
                            zone.AddCell(cell);
                            cellsAdded++;
                        }
                    }
                }

                // Register zone if we added enough cells
                if (cellsAdded >= 20) // Minimum viable storage size
                {
                    map.zoneManager.RegisterZone(zone);
                    RimWatchLogger.Info($"📦 BuildingAutomation: Created storage zone at ({location.x}, {location.z}) - {cellsAdded} cells");
                }
                else
                {
                    RimWatchLogger.Debug($"BuildingAutomation: Storage location had insufficient cells ({cellsAdded})");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in AutoCreateStorageZones", ex);
            }
        }

        /// <summary>
        /// Automatically places a gathering spot (horseshoes pin or campfire).
        /// Prevents mental breaks and enables recreation/parties.
        /// </summary>
        private static void AutoPlaceGatheringSpot(Map map)
        {
            try
            {
                // Horseshoes pin is cheap and requires no research
                ThingDef gatheringDef = DefDatabase<ThingDef>.GetNamedSilentFail("HorseshoesPin");
                
                RimWatchLogger.Debug($"BuildingAutomation: HorseshoesPin ThingDef = {(gatheringDef != null ? "FOUND" : "NULL")}");
                
                if (gatheringDef == null)
                {
                    // Fallback to campfire if horseshoes not available
                    gatheringDef = DefDatabase<ThingDef>.GetNamedSilentFail("Campfire");
                    RimWatchLogger.Debug($"BuildingAutomation: Campfire ThingDef = {(gatheringDef != null ? "FOUND" : "NULL")}");
                }
                
                if (gatheringDef == null)
                {
                    RimWatchLogger.Warning("BuildingAutomation: No gathering spot ThingDef found (HorseshoesPin or Campfire)!");
                    return;
                }

                ThingDef stuffDef = GenStuff.DefaultStuffFor(gatheringDef);
                RimWatchLogger.Debug($"BuildingAutomation: Using {gatheringDef.label} (stuff: {stuffDef?.label ?? "none"})");
                
                // Find suitable outdoor location
                IntVec3 location = FindGatheringSpotLocation(map);
                
                if (location == IntVec3.Invalid)
                {
                    RimWatchLogger.Warning("BuildingAutomation: Could not find suitable location for gathering spot");
                    return;
                }

                RimWatchLogger.Debug($"BuildingAutomation: Found location for gathering spot at ({location.x}, {location.z})");

                // Place blueprint via Designator with rotation probing
                bool success = BuildPlacer.TryPlaceWithBestRotation(map, gatheringDef, location, stuffDef, "Moderate");
                if (success)
                {
                    RimWatchLogger.Info($"🎉 BuildingAutomation: Placed {gatheringDef.label} blueprint at ({location.x}, {location.z})");
                }
                else
                {
                    RimWatchLogger.Warning($"BuildingAutomation: Failed to place {gatheringDef.label} blueprint at ({location.x}, {location.z})");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in AutoPlaceGatheringSpot", ex);
            }
        }

        /// <summary>
        /// Finds a suitable location for a gathering spot.
        /// Prefers open areas near base center.
        /// </summary>
        private static IntVec3 FindGatheringSpotLocation(Map map)
        {
            try
            {
                List<Building> buildings = map.listerBuildings.allBuildingsColonist.ToList();
                IntVec3 baseCenter = map.Center;
                
                if (buildings.Count > 0)
                {
                    int avgX = (int)buildings.Average(b => b.Position.x);
                    int avgZ = (int)buildings.Average(b => b.Position.z);
                    baseCenter = new IntVec3(avgX, 0, avgZ);
                }

                // Search for open areas (outdoor preferred for horseshoes/campfire)
                for (int radius = 10; radius < 40; radius += 5)
                {
                    for (int angle = 0; angle < 360; angle += 45)
                    {
                        float radians = angle * (float)Math.PI / 180f;
                        int x = baseCenter.x + (int)(radius * Math.Cos(radians));
                        int z = baseCenter.z + (int)(radius * Math.Sin(radians));
                        IntVec3 candidate = new IntVec3(x, 0, z);

                        if (!candidate.InBounds(map)) continue;
                        
                        // Gathering spot should be standable and open (no roof preferred)
                        if (candidate.Standable(map) &&
                            CanPlaceBuildingAt(map, candidate, new IntVec2(1, 1))) // 1x1 building
                        {
                            return candidate;
                        }
                    }
                }

                return IntVec3.Invalid;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in FindGatheringSpotLocation", ex);
                return IntVec3.Invalid;
            }
        }

        /// <summary>
        /// Finds suitable location for storage zone.
        /// Prefers roofed areas away from kitchen/beds.
        /// </summary>
        private static IntVec3 FindStorageLocation(Map map)
        {
            try
            {
                List<Building> buildings = map.listerBuildings.allBuildingsColonist.ToList();
                IntVec3 baseCenter = map.Center;
                
                if (buildings.Count > 0)
                {
                    int avgX = (int)buildings.Average(b => b.Position.x);
                    int avgZ = (int)buildings.Average(b => b.Position.z);
                    baseCenter = new IntVec3(avgX, 0, avgZ);
                }

                // ✅ Search for ROOFED locations first (protect items from weather!)
                for (int radius = 5; radius < 30; radius += 5)
                {
                    for (int angle = 0; angle < 360; angle += 45)
                    {
                        float radians = angle * (float)Math.PI / 180f;
                        int x = baseCenter.x + (int)(radius * Math.Cos(radians));
                        int z = baseCenter.z + (int)(radius * Math.Sin(radians));
                        IntVec3 candidate = new IntVec3(x, 0, z);
        
                        if (!candidate.InBounds(map)) continue;
                        
                        // ✅ PRIORITY: Storage should be UNDER ROOF (protects from deterioration)
                        // ❌ НЕ КЛАСТЬ ВОДОЙ: запрещаем террейн с водой/болотом
                        if (IsGoodStorageCell(map, candidate) && candidate.Roofed(map))
                        {
                            return candidate; // Found roofed & dry location!
                        }
                    }
                }

                // ✅ Fallback: If no roofed spot found, accept outdoor (emergency only)
                for (int radius = 5; radius < 30; radius += 5)
                {
                    for (int angle = 0; angle < 360; angle += 45)
                    {
                        float radians = angle * (float)Math.PI / 180f;
                        int x = baseCenter.x + (int)(radius * Math.Cos(radians));
                        int z = baseCenter.z + (int)(radius * Math.Sin(radians));
                        IntVec3 candidate = new IntVec3(x, 0, z);
        
                        if (!candidate.InBounds(map)) continue;
                        
                        if (IsGoodStorageCell(map, candidate))
                        {
                            RimWatchLogger.Warning($"BuildingAutomation: Using outdoor storage at ({candidate.x}, {candidate.z}) - no roofed space found!");
                            return candidate; // Outdoor storage (not ideal, but at least not in water)
                        }
                    }
                }

                // ✅ NO FALLBACK - Return invalid to signal storage room needed
                RimWatchLogger.Warning($"BuildingAutomation: No roofed storage location - waiting for Storage room to be built");
                return IntVec3.Invalid;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in FindStorageLocation", ex);
                return IntVec3.Invalid;
            }
        }

        /// <summary>
        /// Checks if a cell can have a building placed on it.
        /// </summary>
        private static bool CanPlaceBuildingAt(Map map, IntVec3 cell, IntVec2 size)
        {
            if (!cell.InBounds(map)) return false;
            
            // Check all cells the building would occupy
            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    IntVec3 checkCell = cell + new IntVec3(x, 0, z);
                    
                    if (!checkCell.InBounds(map)) return false;
                    if (!checkCell.Standable(map)) return false;
                    if (checkCell.GetFirstBuilding(map) != null) return false;
                    if (checkCell.GetFirstItem(map) != null) return false;
                    
                    // Check for existing blueprints
                    List<Thing> existingThings = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint);
                    if (existingThings.Any(t => t.Position == checkCell && t is Blueprint)) return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// v0.8.4: Checks if a cell is suitable for storage (no water/marsh, standable ground).
        /// Used for stockpile zones and storage location choosing.
        /// </summary>
        private static bool IsGoodStorageCell(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map)) return false;
            if (!cell.Standable(map)) return false;
            
            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain == null) return false;
            
            // Disallow any water / marsh / bridge-over-water terrains
            // Use IsWater if available, and also filter by common defName patterns.
            if (terrain.IsWater) return false;
            string defName = terrain.defName ?? string.Empty;
            if (defName.Contains("Water") || defName.Contains("Marsh") || defName.Contains("Bridge"))
            {
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// Checks if a cell has constructed flooring.
        /// </summary>
        private static bool IsConstructedFloor(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map)) return false;
            
            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain == null) return false;
            
            return terrain.defName.Contains("Floor") || 
                   terrain.defName.Contains("Smooth") ||
                   terrain.defName.Contains("Tile") ||
                   terrain.defName.Contains("Carpet");
        }

        /// <summary>
        /// Automatically places production workshops (smithing bench, tailoring bench, etc).
        /// Places near storage for resource access.
        /// </summary>
        private static void AutoPlaceWorkshops(Map map)
        {
            try
            {
                // Get settings
                RimWatchMod modInstance = LoadedModManager.GetMod<RimWatchMod>();
                string logLevel = modInstance?.GetSettings<RimWatchSettings>()?.buildingLogLevel.ToString() ?? "Moderate";

                // Check if we already have basic workshops
                List<Building> existingBuildings = map.listerBuildings.allBuildingsColonist.ToList();
                bool hasCraftingBench = existingBuildings.Any(b => b.def.defName.Contains("TableButcher") || b.def.defName.Contains("CraftingSpot"));
                bool hasSmithing = existingBuildings.Any(b => b.def.defName.Contains("TableSmithing") || b.def.defName.Contains("FueledSmithy"));
                bool hasTailoring = existingBuildings.Any(b => b.def.defName.Contains("TableTailor") || b.def.defName.Contains("ElectricTailoringBench"));

                int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;

                // Priority: Crafting spot (early) > Smithing > Tailoring
                List<ThingDef> workshopsToPlace = new List<ThingDef>();

                // Early game: crafting spot (no research needed)
                if (!hasCraftingBench && colonistCount >= 2)
                {
                    ThingDef craftingSpot = DefDatabase<ThingDef>.GetNamedSilentFail("CraftingSpot");
                    if (craftingSpot != null && IsResearchedOrNoResearch(craftingSpot))
                    {
                        workshopsToPlace.Add(craftingSpot);
                    }
                }

                // Mid game: smithing bench (for weapons/tools)
                if (!hasSmithing && colonistCount >= 3)
                {
                    ThingDef smithing = DefDatabase<ThingDef>.GetNamedSilentFail("FueledSmithy");
                    if (smithing != null && IsResearchedOrNoResearch(smithing))
                    {
                        workshopsToPlace.Add(smithing);
                    }
                }

                // Tailoring bench (for clothing)
                if (!hasTailoring && colonistCount >= 4)
                {
                    ThingDef tailoring = DefDatabase<ThingDef>.GetNamedSilentFail("HandTailoringBench");
                    if (tailoring != null && IsResearchedOrNoResearch(tailoring))
                    {
                        workshopsToPlace.Add(tailoring);
                    }
                }

                if (workshopsToPlace.Count == 0)
                {
                    return; // No workshops needed
                }

                // Place workshops near storage zones
                foreach (ThingDef workshopDef in workshopsToPlace)
                {
                    IntVec3 location = FindWorkshopLocation(map, workshopDef);
                    
                    if (location == IntVec3.Invalid)
                    {
                        RimWatchLogger.Debug($"BuildingAutomation: Could not find location for {workshopDef.label}");
                        continue;
                    }

                    ThingDef stuffDef = GenStuff.DefaultStuffFor(workshopDef);
                    
                    try
                    {
                        // ✅ CRITICAL FIX: Use Designator system for proper blueprint placement
                        // This ensures colonists can build it (not just a template)
                        Designator_Build designator = new Designator_Build(workshopDef);
                        designator.SetStuffDef(stuffDef);
                        
                        AcceptanceReport canPlace = designator.CanDesignateCell(location);
                        if (!canPlace.Accepted)
                        {
                            RimWatchLogger.Warning($"BuildingAutomation: Cannot place {workshopDef.label} at ({location.x}, {location.z}): {canPlace.Reason}");
                            continue;
                        }
                        
                        // Place the designation (creates buildable blueprint)
                        designator.DesignateSingleCell(location);
                        RimWatchLogger.Info($"🔨 BuildingAutomation: Placed {workshopDef.label} blueprint at ({location.x}, {location.z})");

                        // Auto-connect power if the bench needs it
                        bool needsPower = workshopDef.comps != null && workshopDef.comps.Any(c => c.compClass?.Name == "CompPowerTrader");
                        if (needsPower)
                        {
                            PowerPlanner.ConnectToNearestGrid(map, location, 25, logLevel);
                        }
                    }
                    catch (Exception placeEx)
                    {
                        RimWatchLogger.Warning($"BuildingAutomation: Failed to place {workshopDef.label}: {placeEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in AutoPlaceWorkshops", ex);
            }
        }

        /// <summary>
        /// Finds a suitable location for a workshop building.
        /// Prefers indoor areas near storage zones.
        /// </summary>
        private static IntVec3 FindWorkshopLocation(Map map, ThingDef workshopDef)
        {
            try
            {
                // Get storage zones for reference
                List<Zone_Stockpile> storageZones = map.zoneManager.AllZones
                    .OfType<Zone_Stockpile>()
                    .ToList();

                IntVec3 searchCenter = map.Center;

                // If we have storage zones, search near the first one
                if (storageZones.Count > 0)
                {
                    Zone_Stockpile firstStorage = storageZones.First();
                    if (firstStorage.Cells.Any())
                    {
                        searchCenter = firstStorage.Cells.First();
                    }
                }
                else
                {
                    // Search near existing buildings
                    List<Building> buildings = map.listerBuildings.allBuildingsColonist.ToList();
                    if (buildings.Count > 0)
                    {
                        searchCenter = buildings.First().Position;
                    }
                }

                // Workshop size (most are 3x1 or 3x2)
                IntVec2 size = workshopDef.size;

                // Search in expanding radius
                for (int radius = 5; radius < 30; radius += 3)
                {
                    for (int angle = 0; angle < 360; angle += 30)
                    {
                        float radians = angle * (float)Math.PI / 180f;
                        int x = searchCenter.x + (int)(radius * Math.Cos(radians));
                        int z = searchCenter.z + (int)(radius * Math.Sin(radians));
                        IntVec3 candidate = new IntVec3(x, 0, z);

                        if (!candidate.InBounds(map)) continue;

                        // Workshop can be indoor or outdoor
                        if (CanPlaceBuildingAt(map, candidate, size))
                        {
                            return candidate;
                        }
                    }
                }

                return IntVec3.Invalid;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"BuildingAutomation: Error finding workshop location: {ex.Message}");
                return IntVec3.Invalid;
            }
        }

        /// <summary>
        /// Checks if a building's research prerequisites are met or if it requires no research.
        /// </summary>
        private static bool IsResearchedOrNoResearch(ThingDef thingDef)
        {
            if (thingDef == null) return false;
            
            // If no research prerequisites, it's available
            if (thingDef.researchPrerequisites == null || thingDef.researchPrerequisites.Count == 0)
            {
                return true;
            }
            
            // Check if all research prerequisites are completed
            foreach (ResearchProjectDef research in thingDef.researchPrerequisites)
            {
                if (!research.IsFinished)
                {
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// Automatically expands the base when colony grows.
        /// Adds bedrooms, storage, and expands existing rooms.
        /// </summary>
        private static void AutoExpandBase(Map map, int colonistCount)
        {
            try
            {
                // Only expand if colony is growing (5+ colonists)
                if (colonistCount < 5)
                {
                    return;
                }

                // Check current infrastructure
                int bedCount = map.listerBuildings.allBuildingsColonist
                    .Count(b => b is Building_Bed bed && bed.def.building.bed_humanlike);
                
                int storageZones = map.zoneManager.AllZones
                    .OfType<Zone_Stockpile>()
                    .Count();

                // Expansion priorities:
                // 1. More beds if colonists > beds
                if (colonistCount > bedCount)
                {
                    int bedsNeeded = colonistCount - bedCount;
                    RimWatchLogger.Info($"BuildingAutomation: Colony expanding - need {bedsNeeded} more beds for {colonistCount} colonists");
                    // Beds will be handled by AutoPlaceBeds in the main flow
                }

                // 2. Additional storage if we have lots of colonists
                if (colonistCount > 6 && storageZones < 2)
                {
                    RimWatchLogger.Info($"BuildingAutomation: Large colony ({colonistCount} colonists) - need more storage zones");
                    // Storage zones will be handled by AutoCreateStorageZones
                }

                // 3. Recreation room (for colonies 8+)
                if (colonistCount >= 8)
                {
                    bool hasRecreation = map.listerBuildings.allBuildingsColonist
                        .Any(b => b.def.defName.Contains("ChessTable") || 
                                 b.def.defName.Contains("BilliardsTable") ||
                                 b.def.defName.Contains("HorseshoesPin"));

                    if (!hasRecreation)
                    {
                        RimWatchLogger.Info($"BuildingAutomation: Large colony ({colonistCount} colonists) - recommend recreation room");
                        // Could add recreation buildings in future version
                    }
                }

                RimWatchLogger.Debug($"BuildingAutomation: Base expansion check complete (colonists: {colonistCount}, beds: {bedCount}, storage: {storageZones})");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in AutoExpandBase", ex);
            }
        }

        /// <summary>
        /// Automatically builds complete rooms with walls, doors, and furniture.
        /// Integrated room building system.
        /// </summary>
        private static void AutoBuildRooms(Map map)
        {
            try
            {
                // Get settings
                RimWatchMod modInstance = LoadedModManager.GetMod<RimWatchMod>();
                string logLevel = modInstance?.GetSettings<RimWatchSettings>()?.buildingLogLevel.ToString() ?? "Moderate";

                // ✅ CRITICAL FIX: Check room needs BEFORE cooldown!
                List<RoomPlanner.RoomPlan> roomNeeds = RoomPlanner.GetRoomBuildingNeeds(map, logLevel);

                RimWatchLogger.Info($"🏠 BuildingAutomation: Found {roomNeeds.Count} room needs for {map.mapPawns.FreeColonistsSpawnedCount} colonists");
                
                if (logLevel == "Verbose" || logLevel == "Debug")
                {
                    foreach (var need in roomNeeds)
                    {
                        RimWatchLogger.Debug($"  - {need.Role} ({need.Size.x}x{need.Size.z}) Priority={need.Priority}");
                    }
                }

                if (roomNeeds.Count == 0)
                {
                    RimWatchLogger.Debug("BuildingAutomation: No room building needs detected - colony has enough rooms");
                    return;
                }

                // ✅ NEW: Check if there are CRITICAL bedroom needs (colonists sleeping outside)
                bool hasCriticalBedroomNeed = roomNeeds.Any(r => 
                    (r.Role == RoomPlanner.RoomRole.Bedroom || r.Role == RoomPlanner.RoomRole.Barracks) && 
                    r.Priority >= 95);

                // Check cooldown (but skip if CRITICAL bedroom need!)
                const int RoomBuildingCooldown = 7200; // 120 seconds (rooms are expensive)
                
                if (_lastPlacementTick.ContainsKey("Room"))
                {
                    int ticksSince = Find.TickManager.TicksGame - _lastPlacementTick["Room"];
                    if (ticksSince < RoomBuildingCooldown)
                    {
                        if (hasCriticalBedroomNeed)
                        {
                            RimWatchLogger.Warning($"⚠️ BuildingAutomation: BYPASSING cooldown - colonists sleeping outside! (cooldown: {ticksSince}/{RoomBuildingCooldown})");
                        }
                        else
                        {
                            RimWatchLogger.Debug($"BuildingAutomation: Room building on cooldown ({ticksSince}/{RoomBuildingCooldown} ticks)");
                            return;
                        }
                    }
                }
                
                // ✅ CHANGED: Do NOT record cooldown yet - only record after successful placement!

                // ✅ BUILD ALL NEEDED ROOMS (not just one!)
                // This is critical - if we need 3 bedrooms, build all 3 at once
                int roomsPlaced = 0;
                foreach (var roomNeed in roomNeeds.Take(3)) // Limit to 3 per tick for performance
                {
                    if (logLevel == "Verbose" || logLevel == "Debug")
                    {
                        RimWatchLogger.Info($"🏠 BuildingAutomation: Planning {roomNeed.Role} room ({roomNeed.Size.x}x{roomNeed.Size.z})");
                    }

                    // Plan room at best location
                    RoomPlanner.RoomPlan roomPlan = RoomPlanner.PlanRoomAtBestLocation(
                    map,
                    roomNeed.Role,
                    roomNeed.Size,
                    logLevel
                );

                if (!roomPlan.IsValid)
                {
                    // v0.8.2: Use throttled warning for planning failures to prevent spam
                    RimWatchLogger.WarningThrottledByKey($"room_plan_{roomNeed.Role}", 
                        $"BuildingAutomation: Cannot build {roomNeed.Role} room - {roomPlan.RejectionReason}");
                    continue; // Try next room
                }

                // Validate room plan
                if (!RoomValidator.ValidateRoomPlan(map, roomPlan, logLevel))
                {
                    // v0.8.2: Use throttled warning for validation failures to prevent spam
                    RimWatchLogger.WarningThrottledByKey($"room_validation_{roomNeed.Role}", 
                        $"BuildingAutomation: Room validation failed for {roomNeed.Role} - {roomPlan.RejectionReason}");
                    continue; // Try next room
                }

                // v0.8.2: Enhanced material checking with detailed diagnostics
                bool hasWallMaterials = WallBuilder.HasEnoughMaterials(map, roomPlan.WallCells.Count, null);
                bool hasDoorMaterials = DoorPlacer.HasEnoughMaterials(map, roomPlan.DoorCells.Count);

                if (!hasWallMaterials || !hasDoorMaterials)
                {
                    // v0.8.2: Detailed material diagnostics
                    string materialStatus = "";
                    if (!hasWallMaterials)
                    {
                        int wallsNeeded = roomPlan.WallCells.Count;
                        int stoneAvailable = map.resourceCounter.GetCount(ThingDefOf.BlocksGranite);
                        int woodAvailable = map.resourceCounter.GetCount(ThingDefOf.WoodLog);
                        materialStatus += $"Walls: need {wallsNeeded}, have stone={stoneAvailable}, wood={woodAvailable}. ";
                    }
                    if (!hasDoorMaterials)
                    {
                        int doorsNeeded = roomPlan.DoorCells.Count;
                        int woodAvailable = map.resourceCounter.GetCount(ThingDefOf.WoodLog);
                        materialStatus += $"Doors: need {doorsNeeded}, have wood={woodAvailable}. ";
                    }
                    
                    // Use throttled warning to prevent spam
                    RimWatchLogger.WarningThrottledByKey($"room_materials_{roomNeed.Role}", 
                        $"BuildingAutomation: Insufficient materials for {roomNeed.Role} room. {materialStatus}");
                    continue; // Try next room
                }

                // Place walls
                int wallsPlaced = WallBuilder.PlaceWalls(map, roomPlan.WallCells, roomPlan.DoorCells, logLevel);

                // Place doors
                int doorsPlaced = DoorPlacer.PlaceDoors(map, roomPlan.DoorCells, roomPlan.Role, logLevel);

                if (wallsPlaced > 0 || doorsPlaced > 0)
                {
                    RimWatchLogger.Info($"🏠 BuildingAutomation: Building {roomPlan.Role} room at ({roomPlan.Origin.x}, {roomPlan.Origin.z}) - {wallsPlaced} walls, {doorsPlaced} doors");
                    roomsPlaced++;
                    
                    // ✅ CRITICAL: Register room in RoomConstructionManager for tracking
                    RoomBuilding.RoomConstructionManager.RegisterRoomConstruction(map, roomPlan);
                    
                    // Update zone cache after room construction
                    BaseZoneCache.UpdateCache(map);
                    RimWatchLogger.Debug("BuildingAutomation: Zone cache updated after room placement");

                    // Place furniture inside room after a delay
                    PlaceFurnitureInRoom(map, roomPlan, logLevel);
                }
                }
                
                // Summary log
                if (roomsPlaced > 0)
                {
                    // ✅ CRITICAL: Record cooldown ONLY if rooms were successfully placed!
                    RecordPlacement("Room");
                    RimWatchLogger.Info($"✅ BuildingAutomation: Placed {roomsPlaced} room(s) successfully - next check in 120 seconds");
                }
                else
                {
                    // ✅ NO rooms placed - do NOT record cooldown, allow retry sooner
                    RimWatchLogger.Warning($"BuildingAutomation: Could not place any rooms (tried {roomNeeds.Count}) - will retry next cycle");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in AutoBuildRooms", ex);
            }
        }

        /// <summary>
        /// Places appropriate furniture inside a built room.
        /// </summary>
        private static void PlaceFurnitureInRoom(Map map, RoomPlanner.RoomPlan roomPlan, string logLevel)
        {
            try
            {
                if (roomPlan.FloorCells.Count == 0)
                {
                    if (logLevel == "Debug")
                        RimWatchLogger.Debug("BuildingAutomation: No floor cells for furniture placement");
                    return;
                }

                // Select furniture based on room role
                List<ThingDef> furnitureToPlace = new List<ThingDef>();

                switch (roomPlan.Role)
                {
                    case RoomPlanner.RoomRole.Bedroom:
                        // Bed + end table + dresser
                        ThingDef bed = DefDatabase<ThingDef>.GetNamedSilentFail("Bed");
                        if (bed != null) furnitureToPlace.Add(bed);
                        break;

                    case RoomPlanner.RoomRole.Barracks:
                        // Multiple beds
                        ThingDef bed2 = DefDatabase<ThingDef>.GetNamedSilentFail("Bed");
                        if (bed2 != null)
                        {
                            int bedCount = Math.Min(roomPlan.FloorCells.Count / 8, 6); // Max 6 beds
                            for (int i = 0; i < bedCount; i++)
                                furnitureToPlace.Add(bed2);
                        }
                        break;

                    case RoomPlanner.RoomRole.Kitchen:
                        // Stove (already placed by AutoPlaceKitchen, so skip)
                        break;

                    case RoomPlanner.RoomRole.DiningRoom:
                        // Table + chairs
                        ThingDef table = DefDatabase<ThingDef>.GetNamedSilentFail("TableShort");
                        if (table != null) furnitureToPlace.Add(table);
                        break;

                    case RoomPlanner.RoomRole.Workshop:
                        // Crafting benches (handled by other automation) + chairs!
                        PlaceChairsForWorkbenches(map, roomPlan, logLevel);
                        break;

                    case RoomPlanner.RoomRole.Research:
                        // Research bench (handled by other automation)
                        break;

                    case RoomPlanner.RoomRole.Hospital:
                        // Hospital beds
                        ThingDef hospitalBed = DefDatabase<ThingDef>.GetNamedSilentFail("HospitalBed");
                        if (hospitalBed != null)
                        {
                            int bedCount = Math.Min(roomPlan.FloorCells.Count / 10, 4);
                            for (int i = 0; i < bedCount; i++)
                                furnitureToPlace.Add(hospitalBed);
                        }
                        break;

                    case RoomPlanner.RoomRole.Recreation:
                        // Horseshoe pin or chess table
                        ThingDef horseshoes = DefDatabase<ThingDef>.GetNamedSilentFail("HorseshoesPin");
                        if (horseshoes != null) furnitureToPlace.Add(horseshoes);
                        break;
                }

                // Place furniture at appropriate locations
                foreach (ThingDef furnitureDef in furnitureToPlace)
                {
                    // Find a suitable floor cell
                    IntVec3 furnitureLocation = FindFurniturePlacement(map, roomPlan.FloorCells, furnitureDef);

                    if (furnitureLocation != IntVec3.Invalid)
                    {
                        ThingDef? stuffDef = StuffSelector.DefaultNonSteelStuffFor(furnitureDef, map);
                        bool success = BuildPlacer.TryPlaceWithBestRotation(map, furnitureDef, furnitureLocation, stuffDef, logLevel);
                        if (success)
                        {
                            if (logLevel == "Verbose" || logLevel == "Debug")
                                RimWatchLogger.Debug($"BuildingAutomation: Placed {furnitureDef.label} in {roomPlan.Role} at ({furnitureLocation.x}, {furnitureLocation.z})");
                        }
                        else
                        {
                            if (logLevel == "Debug")
                                RimWatchLogger.Debug($"BuildingAutomation: Failed to place {furnitureDef.label} at ({furnitureLocation.x}, {furnitureLocation.z})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in PlaceFurnitureInRoom", ex);
            }
        }

        /// <summary>
        /// Finds a suitable location for furniture within floor cells.
        /// </summary>
        private static IntVec3 FindFurniturePlacement(Map map, List<IntVec3> floorCells, ThingDef furnitureDef)
        {
            try
            {
                // Try each floor cell
                foreach (IntVec3 cell in floorCells)
                {
                    // Check if cell is clear
                    if (cell.GetFirstBuilding(map) != null) continue;
                    if (cell.GetFirstItem(map) != null) continue;

                    // Check if furniture fits (considering size)
                    bool fits = true;
                    IntVec2 size = furnitureDef.size;

                    for (int x = 0; x < size.x && fits; x++)
                    {
                        for (int z = 0; z < size.z && fits; z++)
                        {
                            IntVec3 checkCell = cell + new IntVec3(x, 0, z);
                            if (!floorCells.Contains(checkCell) ||
                                checkCell.GetFirstBuilding(map) != null)
                            {
                                fits = false;
                            }
                        }
                    }

                    if (fits) return cell;
                }

                return IntVec3.Invalid;
            }
            catch
            {
                return IntVec3.Invalid;
            }
        }

        /// <summary>
        /// Places chairs/stools adjacent to workbenches.
        /// ✅ NEW: Improves colonist comfort and work speed!
        /// </summary>
        private static void PlaceChairsForWorkbenches(Map map, RoomPlanner.RoomPlan roomPlan, string logLevel)
        {
            try
            {
                // Find all workbenches in the room
                List<Building> workbenches = map.listerBuildings.allBuildingsColonist
                    .Where(b => roomPlan.FloorCells.Contains(b.Position) &&
                               IsWorkbench(b.def))
                    .ToList();

                if (workbenches.Count == 0)
                {
                    if (logLevel == "Debug")
                        RimWatchLogger.Debug("BuildingAutomation: No workbenches in room for chair placement");
                    return;
                }

                // Get chair/stool def
                ThingDef chair = DefDatabase<ThingDef>.GetNamedSilentFail("Stool") ?? 
                                DefDatabase<ThingDef>.GetNamedSilentFail("Chair");

                if (chair == null)
                {
                    RimWatchLogger.Debug("BuildingAutomation: No chair/stool ThingDef found");
                    return;
                }

                ThingDef stuffDef = GenStuff.DefaultStuffFor(chair);
                int chairsPlaced = 0;

                // Place chair in front of each workbench
                foreach (Building workbench in workbenches)
                {
                    // Try all 4 cardinal directions
                    IntVec3[] adjacentCells = new IntVec3[]
                    {
                        workbench.Position + IntVec3.North,
                        workbench.Position + IntVec3.South,
                        workbench.Position + IntVec3.East,
                        workbench.Position + IntVec3.West
                    };

                    foreach (IntVec3 chairPos in adjacentCells)
                    {
                        if (!chairPos.InBounds(map)) continue;
                        if (!chairPos.Standable(map)) continue;
                        if (chairPos.GetFirstBuilding(map) != null) continue;

                        // Place chair
                        bool success = BuildPlacer.TryPlaceWithBestRotation(map, chair, chairPos, StuffSelector.DefaultNonSteelStuffFor(chair, map) ?? stuffDef, logLevel);
                        if (success)
                        {
                            chairsPlaced++;
                            if (logLevel == "Verbose" || logLevel == "Debug")
                                RimWatchLogger.Debug($"BuildingAutomation: Placed {chair.label} at ({chairPos.x}, {chairPos.z}) next to {workbench.def.label}");
                            break; // One chair per workbench
                        }
                        else if (logLevel == "Debug")
                        {
                            RimWatchLogger.Debug($"BuildingAutomation: Failed to place {chair.label} at ({chairPos.x}, {chairPos.z})");
                        }
                    }
                }

                if (chairsPlaced > 0)
                {
                    RimWatchLogger.Info($"🪑 BuildingAutomation: Placed {chairsPlaced} chairs/stools for workbenches");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in PlaceChairsForWorkbenches", ex);
            }
        }

        /// <summary>
        /// Checks if a building is a workbench/production table.
        /// </summary>
        private static bool IsWorkbench(ThingDef def)
        {
            try
            {
                return def.defName.Contains("Table") ||
                       def.defName.Contains("Bench") ||
                       def.defName.Contains("Smithy") ||
                       def.defName.Contains("Tailor") ||
                       def.defName.Contains("Craft");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Automatically unforbids useful items for colonists.
        /// ✅ NEW: Ensures colonists can pick up weapons, armor, resources!
        /// </summary>
        private static void AutoUnforbidItems(Map map)
        {
            try
            {
                // Only run occasionally (every ~5 seconds)
                if (Find.TickManager.TicksGame % 300 != 0)
                    return;

                List<Thing> forbiddenItems = map.listerThings.AllThings
                    .Where(t => t.IsForbidden(Faction.OfPlayer) &&
                               t.def.EverHaulable &&
                               t.Spawned &&
                               !t.IsInAnyStorage())
                    .ToList();

                int unforbidden = 0;

                foreach (Thing item in forbiddenItems)
                {
                    bool shouldUnforbid = false;

                    // ✅ ALWAYS unforbid weapons (colonists need them!)
                    if (item.def.IsWeapon)
                    {
                        shouldUnforbid = true;
                    }
                    // ✅ ALWAYS unforbid armor/apparel
                    else if (item.def.IsApparel)
                    {
                        shouldUnforbid = true;
                    }
                    // ✅ ALWAYS unforbid medicine
                    else if (item.def.IsMedicine)
                    {
                        shouldUnforbid = true;
                    }
                    // ✅ ALWAYS unforbid food
                    else if (item.def.IsNutritionGivingIngestible)
                    {
                        shouldUnforbid = true;
                    }
                    // ✅ ALWAYS unforbid building materials (wood, steel, stone, etc.)
                    else if (item.def.IsStuff)
                    {
                        shouldUnforbid = true;
                    }
                    // ✅ Unforbid valuable items (silver, components, etc.)
                    else if (item.MarketValue >= 50f)
                    {
                        shouldUnforbid = true;
                    }

                    if (shouldUnforbid)
                    {
                        item.SetForbidden(false, warnOnFail: false);
                        unforbidden++;
                    }
                }

                if (unforbidden > 0)
                {
                    RimWatchLogger.Info($"✅ BuildingAutomation: Unforbade {unforbidden} useful items for colonists");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in AutoUnforbidItems", ex);
            }
        }

        /// <summary>
        /// Checks bedroom to colonist ratio and returns detailed statistics.
        /// ✅ NEW: Explicit bedroom counting for better colony development tracking.
        /// </summary>
        public static BedroomStats GetBedroomStats(Map map)
        {
            BedroomStats stats = new BedroomStats();
            
            try
            {
                // Count total colonists
                stats.TotalColonists = map.mapPawns.FreeColonistsSpawnedCount;
                
                // Count colonists with assigned beds
                foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
                {
                    Building_Bed bed = colonist.ownership?.OwnedBed;
                    if (bed != null)
                    {
                        stats.ColonistsWithBeds++;
                        
                        // Check if bed is roofed
                        if (bed.Position.Roofed(map))
                        {
                            stats.ColonistsWithRoofedBeds++;
                            
                            // Check if bed is in proper bedroom
                            Room room = bed.GetRoom(RegionType.Set_All);
                            if (room != null && !room.PsychologicallyOutdoors && room.Role == RoomRoleDefOf.Bedroom)
                            {
                                stats.ColonistsInProperBedrooms++;
                            }
                        }
                    }
                }
                
                // Count available bedrooms
                HashSet<Room> processedBedrooms = new HashSet<Room>();
                foreach (Building building in map.listerBuildings.allBuildingsColonist)
                {
                    if (building is Building_Bed bed && bed.def.building.bed_humanlike)
                    {
                        Room room = bed.GetRoom(RegionType.Set_All);
                        if (room != null && !room.PsychologicallyOutdoors && 
                            room.Role == RoomRoleDefOf.Bedroom && !processedBedrooms.Contains(room))
                        {
                            processedBedrooms.Add(room);
                            stats.AvailableBedrooms++;
                            
                            // Count bed slots in this bedroom
                            int bedsInRoom = map.listerBuildings.allBuildingsColonist
                                .OfType<Building_Bed>()
                                .Count(b => b.GetRoom(RegionType.Set_All) == room);
                            
                            if (bedsInRoom > 1)
                                stats.Barracks++;
                        }
                    }
                }
                
                // Count bedrooms under construction (blueprints/frames)
                var blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint);
                var frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame);
                
                int wallBlueprints = blueprints.OfType<Blueprint>()
                    .Count(b => {
                        ThingDef def = b.def.entityDefToBuild as ThingDef;
                        return def != null && (def.defName == "Wall" || def.building?.isNaturalRock == false);
                    });
                    
                int wallFrames = frames.OfType<Frame>()
                    .Count(f => {
                        ThingDef def = f.def.entityDefToBuild as ThingDef;
                        return def != null && (def.defName == "Wall" || def.building?.isNaturalRock == false);
                    });
                
                // Estimate rooms under construction (4+ walls = likely a room)
                stats.BedroomsUnderConstruction = (wallBlueprints + wallFrames) / 12; // Rough estimate: 12 walls per room
                
                // Calculate deficit
                stats.BedroomDeficit = Math.Max(0, stats.TotalColonists - (stats.AvailableBedrooms + stats.BedroomsUnderConstruction));
                
                return stats;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in GetBedroomStats", ex);
                return stats;
            }
        }

        /// <summary>
        /// Structure for bedroom statistics.
        /// </summary>
        public class BedroomStats
        {
            public int TotalColonists { get; set; } = 0;
            public int ColonistsWithBeds { get; set; } = 0;
            public int ColonistsWithRoofedBeds { get; set; } = 0;
            public int ColonistsInProperBedrooms { get; set; } = 0;
            public int AvailableBedrooms { get; set; } = 0;
            public int Barracks { get; set; } = 0;
            public int BedroomsUnderConstruction { get; set; } = 0;
            public int BedroomDeficit { get; set; } = 0;
            
            /// <summary>
            /// Returns human-readable summary of bedroom situation.
            /// </summary>
            public string GetSummary()
            {
                return $"Colonists: {TotalColonists} | " +
                       $"With Beds: {ColonistsWithBeds} | " +
                       $"Roofed: {ColonistsWithRoofedBeds} | " +
                       $"Proper Bedrooms: {ColonistsInProperBedrooms} | " +
                       $"Available Bedrooms: {AvailableBedrooms} (+ {BedroomsUnderConstruction} building) | " +
                       $"Deficit: {BedroomDeficit}";
            }
        }

        // ==================== v0.7 ADVANCED FEATURES ====================

        private static int lastTurretPlacementTick = -9999;
        private const int TurretPlacementCooldown = 9000; // 150 seconds (turrets are expensive)
        
        private static int lastRepairCheckTick = -9999;
        private const int RepairCheckCooldown = 1800; // 30 seconds
        
        private static int lastDecorationTick = -9999;
        private const int DecorationCooldown = 18000; // 5 minutes

        /// <summary>
        /// Automatically places defensive turrets around the base perimeter.
        /// NEW in v0.7 - Turret defense system.
        /// </summary>
        private static void AutoPlaceTurrets(Map map)
        {
            try
            {
                // Cooldown check
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - lastTurretPlacementTick < TurretPlacementCooldown)
                {
                    return; // Too soon
                }

                // Check if we need turrets
                int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
                if (colonistCount < 3)
                {
                    return; // Too early for turrets
                }

                // Count existing turrets
                int existingTurrets = map.listerBuildings.allBuildingsColonist
                    .Count(b => b.def.building?.turretGunDef != null);

                int turretsNeeded = colonistCount / 2; // 1 turret per 2 colonists
                if (existingTurrets >= turretsNeeded)
                {
                    RimWatchLogger.Debug($"BuildingAutomation: Sufficient turrets ({existingTurrets}/{turretsNeeded}) ✓");
                    return;
                }

                // Check if turrets are researched
                ThingDef miniTurret = ThingDefOf.Turret_MiniTurret;
                if (miniTurret.researchPrerequisites != null && miniTurret.researchPrerequisites.Any(r => !r.IsFinished))
                {
                    RimWatchLogger.Info("BuildingAutomation: Mini-turrets not researched yet");
                    return;
                }

                // Check materials (steel)
                int steelCount = map.listerThings.ThingsOfDef(ThingDefOf.Steel)?.Sum(t => t.stackCount) ?? 0;
                int steelNeeded = 170; // Mini-turret costs ~170 steel

                if (steelCount < steelNeeded)
                {
                    RimWatchLogger.Info($"BuildingAutomation: Insufficient steel for turret ({steelCount}/{steelNeeded})");
                    return;
                }

                // Find base center
                IntVec3 baseCenter = BaseZoneCache.BaseCenter != IntVec3.Invalid 
                    ? BaseZoneCache.BaseCenter 
                    : map.Center;

                // Find good turret locations (around perimeter)
                List<IntVec3> turretLocations = FindTurretLocations(map, baseCenter);

                if (turretLocations.Count == 0)
                {
                    RimWatchLogger.Warning("BuildingAutomation: Could not find suitable turret locations");
                    return;
                }

                // Place turret at first available location
                IntVec3 turretPos = turretLocations[0];
                bool placed = BuildPlacer.TryPlaceWithBestRotation(map, miniTurret, turretPos, ThingDefOf.Steel, "Moderate");

                if (placed)
                {
                    RimWatchLogger.Info($"🔫 BuildingAutomation: Placed mini-turret at ({turretPos.x}, {turretPos.z}) for defense");
                    lastTurretPlacementTick = currentTick;
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in AutoPlaceTurrets", ex);
            }
        }

        /// <summary>
        /// Finds suitable locations for turret placement around base perimeter.
        /// </summary>
        private static List<IntVec3> FindTurretLocations(Map map, IntVec3 baseCenter)
        {
            List<IntVec3> locations = new List<IntVec3>();

            try
            {
                // Search in a ring around base (defensive perimeter)
                for (int radius = 25; radius <= 40 && locations.Count < 10; radius += 5)
                {
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(baseCenter, radius, true))
                    {
                        if (!cell.InBounds(map)) continue;
                        if (map.fogGrid.IsFogged(cell)) continue;

                        // Check if cell is suitable for turret
                        if (!cell.Standable(map)) continue;
                        if (cell.GetFirstBuilding(map) != null) continue;
                        if (!cell.Roofed(map)) // Turrets should be outdoors or under thin roof
                        {
                            // Good location
                            locations.Add(cell);

                            if (locations.Count >= 10) break;
                        }
                    }
                }

                return locations;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in FindTurretLocations", ex);
                return locations;
            }
        }

        /// <summary>
        /// Automatically repairs damaged buildings.
        /// NEW in v0.7 - Building maintenance system.
        /// </summary>
        private static void AutoRepairBuildings(Map map)
        {
            try
            {
                // Cooldown check
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - lastRepairCheckTick < RepairCheckCooldown)
                {
                    return; // Too soon
                }

                // Find damaged buildings
                List<Building> damagedBuildings = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.HitPoints < b.MaxHitPoints * 0.8f) // Less than 80% HP
                    .OrderBy(b => (float)b.HitPoints / b.MaxHitPoints) // Most damaged first
                    .ToList();

                if (damagedBuildings.Count == 0)
                {
                    lastRepairCheckTick = currentTick;
                    return; // Nothing to repair
                }

                int repairsScheduled = 0;

                // Get repair designation def
                DesignationDef repairDef = DefDatabase<DesignationDef>.GetNamedSilentFail("Repair");
                if (repairDef == null)
                {
                    RimWatchLogger.Debug("BuildingAutomation: Repair designation not found");
                    return;
                }
                
                foreach (Building building in damagedBuildings.Take(5)) // Limit to 5 per check
                {
                    // Check if already designated for repair
                    if (map.designationManager.DesignationOn(building, repairDef) != null)
                    {
                        continue; // Already marked
                    }

                    // Add repair designation
                    Designation repairDesignation = new Designation(building, repairDef);
                    map.designationManager.AddDesignation(repairDesignation);
                    
                    repairsScheduled++;
                    
                    float hpPercent = (float)building.HitPoints / building.MaxHitPoints;
                    RimWatchLogger.Debug($"🔧 BuildingAutomation: Scheduled repair for {building.def.label} ({hpPercent:P0} HP) at ({building.Position.x}, {building.Position.z})");
                }

                if (repairsScheduled > 0)
                {
                    RimWatchLogger.Info($"🔧 BuildingAutomation: Scheduled {repairsScheduled} building(s) for repair");
                }

                lastRepairCheckTick = currentTick;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in AutoRepairBuildings", ex);
            }
        }

        /// <summary>
        /// Automatically places decorations to improve mood.
        /// NEW in v0.7 - Beauty and mood enhancement system.
        /// </summary>
        private static void AutoPlaceDecorations(Map map)
        {
            try
            {
                // Cooldown check
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - lastDecorationTick < DecorationCooldown)
                {
                    return; // Too soon
                }

                int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
                if (colonistCount < 5)
                {
                    return; // Decorations are luxury, wait for more colonists
                }

                // Count existing decorations (sculptures, plants)
                int existingDecorations = map.listerThings.AllThings
                    .Count(t => t.def.category == ThingCategory.Building &&
                               (t.def.defName.Contains("Sculpture") || 
                                t.def.defName.Contains("Plant") && t.def.building != null));

                int decorationsNeeded = colonistCount / 3; // 1 decoration per 3 colonists
                if (existingDecorations >= decorationsNeeded)
                {
                    RimWatchLogger.Debug($"BuildingAutomation: Sufficient decorations ({existingDecorations}/{decorationsNeeded}) ✓");
                    return;
                }

                // Try to place a simple sculpture
                ThingDef sculpture = DefDatabase<ThingDef>.GetNamedSilentFail("SculptureSmall");
                if (sculpture == null)
                {
                    RimWatchLogger.Debug("BuildingAutomation: No sculpture def found");
                    return;
                }

                // Check if sculpture requires research
                if (sculpture.researchPrerequisites != null && sculpture.researchPrerequisites.Any(r => !r.IsFinished))
                {
                    RimWatchLogger.Debug("BuildingAutomation: Sculpture not researched yet");
                    return;
                }

                // Find a good location (in recreation room or common area)
                IntVec3 baseCenter = BaseZoneCache.BaseCenter != IntVec3.Invalid 
                    ? BaseZoneCache.BaseCenter 
                    : map.Center;

                IntVec3 decorationPos = IntVec3.Invalid;
                for (int radius = 5; radius < 30; radius += 5)
                {
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(baseCenter, radius, true))
                    {
                        if (!cell.InBounds(map)) continue;
                        if (!cell.Standable(map)) continue;
                        if (cell.GetFirstBuilding(map) != null) continue;
                        if (!cell.Roofed(map)) continue; // Prefer indoors

                        // Check if in a room
                        Room room = cell.GetRoom(map);
                        if (room != null && !room.PsychologicallyOutdoors)
                        {
                            decorationPos = cell;
                            break;
                        }
                    }

                    if (decorationPos != IntVec3.Invalid) break;
                }

                if (decorationPos == IntVec3.Invalid)
                {
                    RimWatchLogger.Debug("BuildingAutomation: Could not find suitable decoration location");
                    return;
                }

                // Place sculpture
                ThingDef stuffDef = StuffSelector.DefaultNonSteelStuffFor(sculpture, map);
                bool placed = BuildPlacer.TryPlaceWithBestRotation(map, sculpture, decorationPos, stuffDef, "Moderate");

                if (placed)
                {
                    RimWatchLogger.Info($"🎨 BuildingAutomation: Placed sculpture at ({decorationPos.x}, {decorationPos.z}) for beauty");
                    lastDecorationTick = currentTick;
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in AutoPlaceDecorations", ex);
            }
        }
        
        /// <summary>
        /// v0.7.9: Installs minified furniture from storage instead of building new ones.
        /// </summary>
        private static int InstallMinifiedFurniture(Map map, string furnitureType, int needed, string logLevel)
        {
            try
            {
                // Find all minified furniture matching type
                List<Thing> minifiedItems = map.listerThings.AllThings
                    .Where(t => t is MinifiedThing minified &&
                               !t.IsForbidden(Faction.OfPlayer) &&
                               t.Spawned &&
                               minified.InnerThing != null &&
                               IsFurnitureOfType(minified.InnerThing.def, furnitureType))
                    .Take(needed)
                    .ToList();

                if (minifiedItems.Count == 0) return 0;

                int installed = 0;
                foreach (Thing minifiedItem in minifiedItems)
                {
                    MinifiedThing minified = minifiedItem as MinifiedThing;
                    if (minified == null) continue;
                    
                    ThingDef innerDef = minified.InnerThing.def;
                    
                    // Find appropriate location
                    LocationFinder.BuildingRole role = GetRoleForFurniture(furnitureType);
                    IntVec3 location = LocationFinder.FindBestLocation(map, innerDef, role, logLevel);
                    
                    if (location == IntVec3.Invalid) continue;
                    
                    // Try all rotations
                    foreach (Rot4 rot in new[] { Rot4.North, Rot4.East, Rot4.South, Rot4.West })
                    {
                        AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(innerDef, location, rot, map);
                        if (report.Accepted)
                        {
                            // Designate for installation (using GenConstruct)
                            GenConstruct.PlaceBlueprintForInstall(minified, location, map, rot, Faction.OfPlayer);
                            installed++;
                            RimWatchLogger.Debug($"BuildingAutomation: Designated {innerDef.label} for installation at ({location.x}, {location.z})");
                            break;
                        }
                    }
                }
                
                return installed;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BuildingAutomation: Error in InstallMinifiedFurniture", ex);
                return 0;
            }
        }
        
        /// <summary>
        /// Checks if a ThingDef matches a furniture type.
        /// </summary>
        private static bool IsFurnitureOfType(ThingDef def, string furnitureType)
        {
            if (def == null) return false;
            
            switch (furnitureType)
            {
                case "Bed":
                    return def.defName.Contains("Bed") && def.building?.isSittable == false;
                case "Table":
                    return def.IsTable;
                case "Chair":
                    return def.building?.isSittable == true;
                case "Storage":
                    return def.defName.Contains("Shelf") || def.defName.Contains("Storage");
                default:
                    return def.defName.Contains(furnitureType);
            }
        }
        
        /// <summary>
        /// Gets BuildingRole for a furniture type.
        /// </summary>
        private static LocationFinder.BuildingRole GetRoleForFurniture(string furnitureType)
        {
            switch (furnitureType)
            {
                case "Bed": return LocationFinder.BuildingRole.Bedroom;
                case "Table": return LocationFinder.BuildingRole.Kitchen;
                case "Chair": return LocationFinder.BuildingRole.General;
                case "Storage": return LocationFinder.BuildingRole.Storage;
                default: return LocationFinder.BuildingRole.General;
            }
        }
        
        // ================== v0.8.2: Rejected Location Cache Helper Methods ==================
        
        /// <summary>
        /// Checks if a location is currently rejected and cooldown hasn't expired.
        /// Also cleans up expired rejections.
        /// </summary>
        private static bool IsLocationRejected(IntVec3 location)
        {
            if (!_rejectedLocations.ContainsKey(location))
                return false;
            
            var rejection = _rejectedLocations[location];
            int currentTick = Find.TickManager.TicksGame;
            int ticksSinceRejection = currentTick - rejection.LastAttemptTick;
            
            // If cooldown expired, remove from cache and allow retry
            if (ticksSinceRejection >= RejectionCooldown)
            {
                _rejectedLocations.Remove(location);
                return false;
            }
            
            // If exceeded max attempts, permanently reject (until cooldown expires)
            if (rejection.AttemptCount >= MaxRejectionAttempts)
            {
                return true;
            }
            
            return true;
        }
        
        /// <summary>
        /// Records a rejection for a location.
        /// Increments attempt count and updates last attempt tick.
        /// </summary>
        private static void RecordRejection(IntVec3 location, string reason)
        {
            int currentTick = Find.TickManager.TicksGame;
            
            if (_rejectedLocations.ContainsKey(location))
            {
                var rejection = _rejectedLocations[location];
                rejection.AttemptCount++;
                rejection.LastAttemptTick = currentTick;
                rejection.Reason = reason;
                
                if (rejection.AttemptCount >= MaxRejectionAttempts)
                {
                    RimWatchLogger.Info($"BuildingAutomation: Location ({location.x}, {location.z}) rejected {MaxRejectionAttempts} times - will not retry for {RejectionCooldown / 60} seconds");
                }
            }
            else
            {
                _rejectedLocations[location] = new RejectionInfo
                {
                    LastAttemptTick = currentTick,
                    AttemptCount = 1,
                    Reason = reason
                };
            }
        }
        
        /// <summary>
        /// Clears all rejected locations (for debugging or reset).
        /// </summary>
        private static void ClearRejectedLocations()
        {
            int count = _rejectedLocations.Count;
            _rejectedLocations.Clear();
            RimWatchLogger.Debug($"BuildingAutomation: Cleared {count} rejected locations");
        }
    }
}
