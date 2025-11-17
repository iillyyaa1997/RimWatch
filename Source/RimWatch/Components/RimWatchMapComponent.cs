using RimWatch.Automation;
using RimWatch.Automation.BuildingPlacement;
using RimWatch.Automation.RoomBuilding;
using RimWatch.Core;
using RimWatch.Utils;
using Verse;

namespace RimWatch.Components
{
    /// <summary>
    /// MapComponent для RimWatch - привязывается к каждой карте и управляет автоматизацией.
    /// Вызывается каждый тик для обновления систем автоматизации.
    /// </summary>
    public class RimWatchMapComponent : MapComponent
    {
        private int _tickCounter = 0;
        private const int CoreTickInterval = 60; // Каждые 60 тиков (~1 секунда) обновляем ядро
        private const int ActivityMonitorInterval = 250; // Каждые 250 тиков (~5 секунд) проверяем активность колонистов
        private const int RoomConstructionUpdateInterval = 180; // Каждые 180 тиков (~3 секунды) обновляем состояния комнат
        private const int ConstructionDiagnosticsInterval = 3600; // Каждые 3600 тиков (~60 секунд) диагностируем строительство
        // ConstructionMonitor сам контролирует свой интервал (каждые 10 секунд)

        private static bool _firstTickLogged = false;

        public RimWatchMapComponent(Map map) : base(map)
        {
            RimWatchLogger.Debug($"RimWatchMapComponent created for map: {map.uniqueID}");
        }

        /// <summary>
        /// Вызывается каждый игровой тик.
        /// </summary>
        public override void MapComponentTick()
        {
            base.MapComponentTick();

            // Логируем первый тик для подтверждения работы
            if (!_firstTickLogged)
            {
                _firstTickLogged = true;
                RimWatchLogger.Info($"[MapComponent] FIRST TICK! AutopilotEnabled={RimWatchCore.AutopilotEnabled}");
                RimWatchLogger.Info($"[MapComponent] Categories: Work={RimWatchCore.WorkEnabled}, Building={RimWatchCore.BuildingEnabled}, Farming={RimWatchCore.FarmingEnabled}");
                RimWatchLogger.Info($"[MapComponent] Defense={RimWatchCore.DefenseEnabled}, Trade={RimWatchCore.TradeEnabled}, Medical={RimWatchCore.MedicalEnabled}");
                RimWatchLogger.Info($"[MapComponent] Social={RimWatchCore.SocialEnabled}, Research={RimWatchCore.ResearchEnabled}");
            }

            // Проверяем, что автопилот включен
            if (!RimWatchCore.AutopilotEnabled) return;

            // Тик всех активных категорий автоматизации
            if (RimWatchCore.WorkEnabled)
                WorkAutomation.Tick();

            if (RimWatchCore.BuildingEnabled)
            {
                BuildingAutomation.Tick();
                // ✅ NEW: Resource gathering for building materials
                ResourceAutomation.AutoManageResources(map);
            }

            if (RimWatchCore.FarmingEnabled)
                FarmingAutomation.Tick();

            if (RimWatchCore.DefenseEnabled)
            {
                DefenseAutomation.Tick();
                // v0.8.0: Auto weapon upgrades
                DefenseAutomation.AutoUpgradeWeapons(map);
            }

            // v0.8.0: Apparel automation
            ApparelAutomation.Tick(map);

            if (RimWatchCore.TradeEnabled)
                TradeAutomation.Tick();

            if (RimWatchCore.MedicalEnabled)
                MedicalAutomation.Tick();

            if (RimWatchCore.SocialEnabled)
                SocialAutomation.Tick();

            if (RimWatchCore.ResearchEnabled)
                ResearchAutomation.Tick();

            // v0.8.5: NEW - Production Automation (bills management)
            ProductionAutomation.Tick();

            // v0.8.0: NEW SYSTEMS - Command System, Game Speed Controller
            ColonistCommandSystem.Tick(map);
            GameSpeedController.Tick(map);

            // Периодический тик ядра
            _tickCounter++;
            if (_tickCounter >= CoreTickInterval)
            {
                _tickCounter = 0;
                RimWatchCore.Tick();
            }
            
            // ✅ NEW: Monitor colonist activity every 5 seconds
            if (Find.TickManager.TicksGame % ActivityMonitorInterval == 0)
            {
                ColonistActivityMonitor.MonitorColonistActivity(map);
            }
            
            // ✅ NEW: Update room construction states every 3 seconds
            if (Find.TickManager.TicksGame % RoomConstructionUpdateInterval == 0)
            {
                RoomConstructionManager.UpdateConstructionStates(map);
                
                // ✅ NEW: Auto-build floors in rooms
                RimWatch.Automation.RoomBuilding.FloorBuilder.AutoBuildFloors(map);
            }
            
            // ✅ NEW: Постоянный мониторинг строительства (сам контролирует интервал)
            try
            {
                RimWatch.Automation.RoomBuilding.ConstructionMonitor.MonitorConstruction(map);
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("RimWatchMapComponent: Error in ConstructionMonitor", ex);
            }
            
            // ✅ NEW: Diagnose unfinished construction every 60 seconds
            if (Find.TickManager.TicksGame % ConstructionDiagnosticsInterval == 0)
            {
                try
                {
                    RimWatch.Automation.RoomBuilding.ConstructionDiagnostics.DiagnoseUnfinishedConstruction(map);
                }
                catch (System.Exception ex)
                {
                    RimWatchLogger.Error("RimWatchMapComponent: Error in ConstructionDiagnostics", ex);
                }
            }
            
            // ✅ NEW: Furniture relocation (beds from outdoor to indoor) every 10 seconds
            // Runs its own cooldown check internally
            try
            {
                RimWatch.Automation.FurnitureRelocator.AutoRelocateFurniture(map);
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("RimWatchMapComponent: Error in FurnitureRelocator", ex);
            }
            
            // ✅ NEW: Fire fighting automation every 2 seconds
            // Runs its own cooldown check internally
            try
            {
                RimWatch.Automation.FireAutomation.AutoManageFires(map);
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("RimWatchMapComponent: Error in FireAutomation", ex);
            }
            
            // ✅ NEW: Colony development system every 5 seconds (300 ticks)
            if (Find.TickManager.TicksGame % 300 == 0)
            {
                try
                {
                    RimWatch.Automation.ColonyDevelopment.DevelopmentStage stage = 
                        RimWatch.Automation.ColonyDevelopment.DevelopmentStageManager.GetCurrentStage(map);
                    
                    var priorities = RimWatch.Automation.ColonyDevelopment.StagePriorities.GetPrioritiesForStage(stage, map);
                    
                    RimWatch.Automation.ColonyDevelopment.ColonyTaskExecutor.ExecutePriorityTasks(map, priorities);
                }
                catch (System.Exception ex)
                {
                    RimWatchLogger.Error("RimWatchMapComponent: Error in ColonyDevelopment", ex);
                }
            }
            
            // ✅ NEW: Outfit automation (smart apparel management)
            try
            {
                RimWatch.Automation.OutfitAutomation.Tick(map);
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("RimWatchMapComponent: Error in OutfitAutomation", ex);
            }

            // ✅ NEW: Context-aware work schedule management
            try
            {
                // Analyze colony state for context-aware scheduling
                var state = RimWatch.Automation.OutfitAutomation.AnalyzeColonyState(map);
                RimWatch.Automation.WorkScheduleAutomation.Tick(map, state);
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("RimWatchMapComponent: Error in WorkScheduleAutomation", ex);
            }
        }
        
        /// <summary>
        /// Вызывается для отрисовки на карте (правильный метод для GUI).
        /// </summary>
        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();
            
            // Draw debug overlay if enabled and this is the current map
            if (map == Find.CurrentMap && RimWatchMod.Settings != null && RimWatchMod.Settings.enableDebugOverlay)
            {
                RimWatch.Debug.DebugOverlay.Draw(map);
            }
        }

        /// <summary>
        /// Вызывается при сохранении игры.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            // TODO: Сохранение/загрузка состояния автоматизации
        }
    }
}

