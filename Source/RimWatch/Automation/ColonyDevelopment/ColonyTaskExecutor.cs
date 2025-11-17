using RimWatch.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.ColonyDevelopment
{
    /// <summary>
    /// Выполняет приоритетные задачи развития колонии.
    /// </summary>
    public static class ColonyTaskExecutor
    {
        private static DevelopmentStage _lastStage = DevelopmentStage.Emergency;
        private static int _lastLogTick = 0;
            private static int _lastStageDecisionTick = 0;
        
        /// <summary>
        /// Выполняет приоритетные задачи для текущего этапа развития.
        /// </summary>
        public static void ExecutePriorityTasks(Map map, List<ColonyTask> tasks)
        {
            try
            {
                if (tasks == null || tasks.Count == 0) return;
                
                // Log stage changes
                DevelopmentStage currentStage = DevelopmentStageManager.GetCurrentStage(map);
                if (currentStage != _lastStage)
                {
                    string stageDesc = DevelopmentStageManager.GetStageDescription(currentStage);
                    
                    // Structured state change log
                    RimWatchLogger.LogStateChange(
                        "ColonyDevelopment",
                        _lastStage.ToString(),
                        currentStage.ToString(),
                        stageDesc);
                    
                    RimWatchLogger.Info($"🎯 Colony Development: Stage changed to {stageDesc}");
                    _lastStage = currentStage;
                }
                
                // Periodically log current stage snapshot for decision analysis (every 10 in-game minutes)
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - _lastStageDecisionTick > 36000)
                {
                    _lastStageDecisionTick = currentTick;
                    
                    int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
                    float wealth = map.wealthWatcher.WealthTotal;
                    
                    RimWatchLogger.LogDecision("ColonyDevelopment", "StageSnapshot", new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "stage", currentStage.ToString() },
                        { "daysPassed", Find.TickManager.TicksGame / 60000 }, // Convert ticks to days
                        { "colonists", colonistCount },
                        { "wealth", wealth }
                    });
                }
                
                // Log current priorities (every 10 minutes)
                if (currentTick - _lastLogTick > 36000)
                {
                    _lastLogTick = currentTick;
                    LogCurrentPriorities(currentStage, tasks);
                }
                
                // Execute top priority tasks
                foreach (var task in tasks.OrderByDescending(t => t.Priority).Take(3))
                {
                    // Track execution for each top-priority task
                    var ctx = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "stage", currentStage.ToString() },
                        { "description", task.Description },
                        { "priority", task.Priority }
                    };
                    
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    RimWatchLogger.LogExecutionStart("ColonyDevelopment", "ExecuteTask", ctx);
                    
                    bool completed = ExecuteTask(map, task);
                    
                    stopwatch.Stop();
                    RimWatchLogger.LogExecutionEnd(
                        "ColonyDevelopment",
                        "ExecuteTask",
                        completed,
                        stopwatch.ElapsedMilliseconds,
                        $"{task.Description} (priority {task.Priority})");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ColonyTaskExecutor: Error in ExecutePriorityTasks", ex);
            }
        }
        
        /// <summary>
        /// Логирует текущие приоритеты.
        /// </summary>
        private static void LogCurrentPriorities(DevelopmentStage stage, List<ColonyTask> tasks)
        {
            try
            {
                string stageDesc = DevelopmentStageManager.GetStageDescription(stage);
                RimWatchLogger.Info($"🎯 Colony Development: {stageDesc}");
                
                var topTasks = tasks.OrderByDescending(t => t.Priority).Take(3).ToList();
                if (topTasks.Any())
                {
                    RimWatchLogger.Info("   Top priorities:");
                    foreach (var task in topTasks)
                    {
                        RimWatchLogger.Info($"   - [{task.Priority}] {task.Description}");
                    }
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ColonyTaskExecutor: Error in LogCurrentPriorities", ex);
            }
        }
        
        /// <summary>
        /// Выполняет конкретную задачу, делегируя соответствующей системе автоматизации.
        /// </summary>
        private static bool ExecuteTask(Map map, ColonyTask task)
        {
            try
            {
                string desc = task.Description.ToLower();
                
                // Задачи обеспечения кроватей/спален
                if (desc.Contains("roofed beds") || desc.Contains("bedrooms"))
                {
                    // Эти задачи уже выполняются через BuildingAutomation.AutoBuildRooms
                    return true; // Система уже работает
                }
                
                // Задачи еды и фермерства
                if (desc.Contains("food source") || desc.Contains("farming") || desc.Contains("berries"))
                {
                    // Выполняется через FarmingAutomation
                    return true;
                }
                
                // Задачи хранения
                if (desc.Contains("storage"))
                {
                    // Выполняется через BuildingAutomation.AutoCreateStorageZones
                    return true;
                }
                
                // Задачи кухни
                if (desc.Contains("kitchen") || desc.Contains("cooking"))
                {
                    // Выполняется через BuildingAutomation.AutoPlaceKitchen
                    return true;
                }
                
                // Задачи энергии
                if (desc.Contains("power"))
                {
                    // Выполняется через BuildingAutomation.AutoPlacePower
                    return true;
                }
                
                // Задачи мастерских
                if (desc.Contains("workshop") || desc.Contains("crafting"))
                {
                    // Будет выполняться через BuildingAutomation
                    return true;
                }
                
                // Задачи обороны
                if (desc.Contains("defenses") || desc.Contains("turrets") || desc.Contains("wall"))
                {
                    // Выполняется через DefenseAutomation
                    return true;
                }
                
                // Задачи госпиталя
                if (desc.Contains("hospital"))
                {
                    // Будет добавлено позже
                    return false;
                }
                
                // Задачи исследований
                if (desc.Contains("research"))
                {
                    // Выполняется через ResearchAutomation
                    return true;
                }
                
                // Задачи отдыха
                if (desc.Contains("rec room"))
                {
                    // Будет выполняться через BuildingAutomation
                    return true;
                }
                
                // Неизвестная задача
                return false;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"ColonyTaskExecutor: Error executing task '{task.Description}'", ex);
                return false;
            }
        }
    }
}

