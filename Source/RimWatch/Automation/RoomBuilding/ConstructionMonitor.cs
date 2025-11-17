using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace RimWatch.Automation.RoomBuilding
{
    /// <summary>
    /// Постоянный мониторинг строительства на карте.
    /// Сканирует карту каждые 10 секунд, подсчитывает незавершённые комнаты,
    /// диагностирует проблемы и принимает меры.
    /// </summary>
    public static class ConstructionMonitor
    {
        private static Dictionary<IntVec3, int> _stuckRooms = new Dictionary<IntVec3, int>(); // location -> ticks stuck
        private static int _lastScanTick = 0;
        private const int ScanInterval = 600; // 10 секунд

        // v0.8.0: Track unreachable blueprints for auto-cancel
        private static Dictionary<Thing, int> _unreachableBlueprints = new Dictionary<Thing, int>(); // blueprint -> tick discovered
        private const int UnreachableTimeout = 18000; // 5 minutes (300 seconds * 60 ticks)

        /// <summary>
        /// Основной метод мониторинга. Вызывать каждый тик из MapComponent.
        /// Сам решит когда запускаться по интервалу.
        /// </summary>
        public static void MonitorConstruction(Map map)
        {
            try
            {
                int currentTick = Find.TickManager.TicksGame;
                
                // Проверяем только каждые 10 секунд
                if (currentTick - _lastScanTick < ScanInterval)
                    return;
            
                _lastScanTick = currentTick;
                
                // v0.8.4: Early exit if no colonists to avoid spam
                if (map.mapPawns.FreeColonistsSpawned.Count() == 0)
                {
                    RimWatchLogger.WarningThrottledByKey(
                        "construction_monitor_no_colonists",
                        "ConstructionMonitor: No colonists found on map - skipping monitoring");
                    return;
                }

                var logLevel = RimWatch.Settings.SystemLogLevel.Moderate;
                if (RimWatchMod.Settings != null)
                {
                    logLevel = RimWatchMod.Settings.constructionLogLevel;
                }
            
                if (logLevel != RimWatch.Settings.SystemLogLevel.Off)
                {
                    RimWatchLogger.Info("🔍 ConstructionMonitor: Scanning map for construction state...");
                }
                if (logLevel >= RimWatch.Settings.SystemLogLevel.Verbose)
                {
                    RimWatchLogger.Debug($"ConstructionMonitor: Current tick={currentTick}, last scan was at {currentTick - ScanInterval}");
                }

                // 1. Подсчитываем все строительные объекты
                var constructionState = AnalyzeConstructionState(map);

                // 2. Логируем текущее состояние
                LogConstructionState(constructionState);

                // 3. Проверяем застрявшие комнаты
                CheckStuckRooms(map, constructionState);

                // 4. v0.8.0: Check and auto-cancel unreachable blueprints
                CheckUnreachableBlueprints(map, currentTick);

                // 5. Диагностируем если есть проблемы
                if (constructionState.TotalUnfinished > 0)
                {
                    DiagnoseConstructionIssues(map, constructionState);
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ConstructionMonitor: Error in MonitorConstruction", ex);
            }
        }

        /// <summary>
        /// Анализирует состояние всего строительства на карте.
        /// </summary>
        private static ConstructionState AnalyzeConstructionState(Map map)
        {
            var state = new ConstructionState();

            // Подсчитываем blueprints
            var blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).ToList();
            foreach (var blueprint in blueprints)
            {
                var def = blueprint.def.entityDefToBuild as ThingDef;
                if (def == null) continue;

                var category = RimWatch.Automation.BuildingPlacement.BuildingClassifier.ClassifyBuilding(def);
                
                if (category == RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Wall)
                    state.WallBlueprints++;
                else if (category == RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Door)
                    state.DoorBlueprints++;
                else if (category == RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Bed)
                    state.BedBlueprints++;
                else
                    state.OtherBlueprints++;
            }

            // Подсчитываем frames (строятся но не завершены)
            var frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).OfType<Frame>().ToList();
            foreach (var frame in frames)
            {
                var def = frame.def.entityDefToBuild as ThingDef;
                if (def == null) continue;

                var category = RimWatch.Automation.BuildingPlacement.BuildingClassifier.ClassifyBuilding(def);
                
                if (category == RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Wall)
                    state.WallFrames++;
                else if (category == RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Door)
                    state.DoorFrames++;
                else if (category == RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Bed)
                    state.BedFrames++;
                else
                    state.OtherFrames++;
            }

            // Подсчитываем построенные здания (для сравнения)
            state.TotalBuiltWalls = map.listerBuildings.allBuildingsColonist.Count(b =>
                RimWatch.Automation.BuildingPlacement.BuildingClassifier.ClassifyBuilding(b.def) == 
                RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Wall);

            state.TotalBuiltDoors = map.listerBuildings.allBuildingsColonist.Count(b =>
                RimWatch.Automation.BuildingPlacement.BuildingClassifier.ClassifyBuilding(b.def) == 
                RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.Door);

            return state;
        }

        /// <summary>
        /// Логирует текущее состояние строительства.
        /// </summary>
        private static void LogConstructionState(ConstructionState state)
        {
            var logLevel = RimWatchMod.Settings?.constructionLogLevel ?? RimWatch.Settings.SystemLogLevel.Moderate;

            if (logLevel == RimWatch.Settings.SystemLogLevel.Off)
            {
                return;
            }

            if (state.TotalUnfinished == 0)
            {
                if (logLevel >= RimWatch.Settings.SystemLogLevel.Minimal)
                {
                    RimWatchLogger.Info("✅ ConstructionMonitor: No unfinished construction");
                }
                return;
            }
        
            if (logLevel == RimWatch.Settings.SystemLogLevel.Minimal)
            {
                RimWatchLogger.Info($"📊 ConstructionMonitor: TOTAL UNFINISHED: {state.TotalUnfinished}");
                return;
            }

            RimWatchLogger.Info($"📊 ConstructionMonitor: Walls: {state.WallFrames}F + {state.WallBlueprints}B ({state.TotalBuiltWalls} built)");
            RimWatchLogger.Info($"📊 ConstructionMonitor: Doors: {state.DoorFrames}F + {state.DoorBlueprints}B ({state.TotalBuiltDoors} built)");
            RimWatchLogger.Info($"📊 ConstructionMonitor: Beds: {state.BedFrames}F + {state.BedBlueprints}B");
            RimWatchLogger.Info($"📊 ConstructionMonitor: Other: {state.OtherFrames}F + {state.OtherBlueprints}B");
            RimWatchLogger.Info($"📊 ConstructionMonitor: TOTAL UNFINISHED: {state.TotalUnfinished}");
        }

        /// <summary>
        /// Проверяет комнаты которые застряли в строительстве.
        /// </summary>
        private static void CheckStuckRooms(Map map, ConstructionState state)
        {
            // ✅ DISABLED: Blueprint.creationTick doesn't exist in current RimWorld version
            // Would check for old blueprints (older than 1 minute) here
            
            /* DISABLED - API changed
            var blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint)
                .OfType<Blueprint>()
                .ToList();
            */
        }

        /// <summary>
        /// Диагностирует проблемы со строительством.
        /// </summary>
        private static void DiagnoseConstructionIssues(Map map, ConstructionState state)
        {
            List<Pawn> canConstruct;
            
            try
            {
                // 1. Проверяем есть ли colonists способные строить
                if (map == null || map.mapPawns == null)
                {
                    RimWatchLogger.Warning("ConstructionMonitor: Map or mapPawns is null, skipping diagnostics");
                    return;
                }

                var colonists = map.mapPawns.FreeColonistsSpawned.ToList();
                if (colonists == null || colonists.Count == 0)
                {
                    // Throttled to avoid spam when colony is dead
                    RimWatchLogger.WarningThrottledByKey("construction_no_colonists", "ConstructionMonitor: No colonists found on map");
                    return;
                }

                canConstruct = colonists.Where(p => 
                    p != null &&
                    p.Spawned &&
                    !p.Dead && 
                    !p.Downed && 
                    !p.InMentalState &&
                    p.workSettings != null &&
                    !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                ).ToList();

                if (canConstruct.Count == 0)
                {
                    RimWatchLogger.Warning("⚠️ ConstructionMonitor: NO colonists can do Construction!");
                    return;
                }

                // 2. Проверяем приоритеты работы
                var constructionPriorities = canConstruct
                    .Select(p => p.workSettings?.GetPriority(WorkTypeDefOf.Construction) ?? 0)
                    .Where(p => p > 0)
                    .ToList();

                if (constructionPriorities.Count == 0)
                {
                    RimWatchLogger.Warning("⚠️ ConstructionMonitor: Construction work is DISABLED for all colonists!");
                    return;
                }

                int avgPriority = (int)constructionPriorities.Average();
                var logLevel = RimWatchMod.Settings?.constructionLogLevel ?? RimWatch.Settings.SystemLogLevel.Moderate;
                if (logLevel != RimWatch.Settings.SystemLogLevel.Off)
                {
                    RimWatchLogger.Info($"📊 ConstructionMonitor: {canConstruct.Count} colonists can build, avg priority: {avgPriority}");
                }

                // 3. Проверяем что они делают сейчас
                var currentJobs = canConstruct
                    .Select(p => new { Name = p.LabelShort, Job = p.CurJobDef?.defName ?? "idle" })
                    .ToList();

                if (logLevel >= RimWatch.Settings.SystemLogLevel.Moderate)
                {
                    RimWatchLogger.Info($"📊 ConstructionMonitor: Colonist activities:");
                    foreach (var cj in currentJobs)
                    {
                        RimWatchLogger.Info($"  - {cj.Name}: {cj.Job}");
                    }
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ConstructionMonitor: Error in diagnostics", ex);
                return;
            }

            // 4. Проверяем первый blueprint/frame на доступность
            try
            {
                var firstUnfinished = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).FirstOrDefault()
                    ?? map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).FirstOrDefault();

                if (firstUnfinished != null && firstUnfinished.Spawned && firstUnfinished.def != null)
                {
                    // Safely check reachability with proper null/spawn checks
                    var reachableColonists = canConstruct
                        .Where(p => p != null && p.Spawned && p.Map == map && !p.Dead && !p.Downed)
                        .Where(p =>
                        {
                            try
                            {
                                return p.CanReach(firstUnfinished, PathEndMode.Touch, Danger.Deadly);
                            }
                            catch (Exception ex)
                            {
                                RimWatchLogger.Warning($"ConstructionMonitor: Error checking reachability for {p.LabelShort}: {ex.Message}");
                                return false;
                            }
                        })
                        .ToList();

                    if (!reachableColonists.Any())
                    {
                        RimWatchLogger.Warning($"⚠️ ConstructionMonitor: NO colonist can reach {firstUnfinished.def.defName} at {firstUnfinished.Position}");
                    }
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Warning($"ConstructionMonitor: Error checking construction reachability: {ex.Message}");
            }
        }

        /// <summary>
        /// v0.8.0: Check blueprints for reachability and auto-cancel if stuck too long.
        /// CRITICAL FIX for unreachable blueprint issue.
        /// </summary>
        private static void CheckUnreachableBlueprints(Map map, int currentTick)
        {
            try
            {
                // Get all colonists who can construct
                var canConstruct = map.mapPawns.FreeColonistsSpawned
                    .Where(p => p != null && !p.Downed && !p.Dead &&
                               p.workSettings != null &&
                               p.workSettings.WorkIsActive(WorkTypeDefOf.Construction))
                    .ToList();

                if (canConstruct.Count == 0) return; // No builders available

                // Check all blueprints
                var blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint)
                    .Where(b => b != null && b.Spawned)
                    .ToList();

                List<Thing> toRemove = new List<Thing>();

                foreach (var blueprint in blueprints)
                {
                    // Check if ANY colonist can reach this blueprint
                    bool anyCanReach = canConstruct.Any(p =>
                    {
                        try
                        {
                            return p.Spawned && p.Map == map &&
                                   map.reachability.CanReach(p.Position, blueprint.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors));
                        }
                        catch
                        {
                            return false;
                        }
                    });

                    if (!anyCanReach)
                    {
                        // Blueprint is unreachable - track it
                        if (!_unreachableBlueprints.ContainsKey(blueprint))
                        {
                            _unreachableBlueprints[blueprint] = currentTick;
                            RimWatchLogger.Warning($"⚠️ ConstructionMonitor: Blueprint {blueprint.def.defName} at {blueprint.Position} is UNREACHABLE by all colonists!");
                        }
                        else
                        {
                            // Check how long it's been unreachable
                            int ticksUnreachable = currentTick - _unreachableBlueprints[blueprint];
                            if (ticksUnreachable >= UnreachableTimeout)
                            {
                                // Auto-cancel after 5 minutes
                                RimWatchLogger.Warning($"❌ ConstructionMonitor: Auto-canceling {blueprint.def.defName} at {blueprint.Position} - unreachable for {ticksUnreachable / 60} seconds");
                                toRemove.Add(blueprint);
                            }
                        }
                    }
                    else
                    {
                        // Blueprint is reachable - remove from tracking
                        if (_unreachableBlueprints.ContainsKey(blueprint))
                        {
                            _unreachableBlueprints.Remove(blueprint);
                            RimWatchLogger.Info($"✅ ConstructionMonitor: Blueprint {blueprint.def.defName} at {blueprint.Position} is now reachable");
                        }
                    }
                }

                // Remove unreachable blueprints
                foreach (var blueprint in toRemove)
                {
                    blueprint.Destroy(DestroyMode.Cancel);
                    _unreachableBlueprints.Remove(blueprint);
                }

                // Clean up destroyed blueprints from tracking
                var destroyed = _unreachableBlueprints.Keys.Where(b => b == null || !b.Spawned || b.Destroyed).ToList();
                foreach (var b in destroyed)
                {
                    _unreachableBlueprints.Remove(b);
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Warning($"ConstructionMonitor: Error checking unreachable blueprints: {ex.Message}");
            }
        }

        /// <summary>
        /// Структура для хранения состояния строительства.
        /// </summary>
        private class ConstructionState
        {
            public int WallBlueprints = 0;
            public int WallFrames = 0;
            public int DoorBlueprints = 0;
            public int DoorFrames = 0;
            public int BedBlueprints = 0;
            public int BedFrames = 0;
            public int OtherBlueprints = 0;
            public int OtherFrames = 0;
            
            public int TotalBuiltWalls = 0;
            public int TotalBuiltDoors = 0;

            public int TotalUnfinished => WallBlueprints + WallFrames + DoorBlueprints + DoorFrames + 
                                          BedBlueprints + BedFrames + OtherBlueprints + OtherFrames;
        }
    }
}

