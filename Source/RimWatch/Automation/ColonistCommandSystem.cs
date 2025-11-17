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
    /// v0.8.0: Colonist Command System - Emergency task prioritization.
    /// Auto-detects and queues critical tasks (rescue, firefighting, medical).
    /// </summary>
    public static class ColonistCommandSystem
    {
        private static Queue<EmergencyTask> _taskQueue = new Queue<EmergencyTask>();
        private static int _lastCheckTick = 0;
        private const int CheckInterval = 60; // Check every second
        
        // v0.8.4: Track repeated Rescue failures per pawn to avoid infinite requeue loops
        private static readonly Dictionary<string, int> _rescueFailureCount = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> _rescueLastFailureTick = new Dictionary<string, int>();
        private const int RescueFailureCooldownTicks = 600; // 10 секунд между попытками после серии фэйлов
        
        /// <summary>
        /// Helper: current log level for ColonistCommandSystem.
        /// </summary>
        private static Settings.SystemLogLevel CommandLogLevel
        {
            get
            {
                return RimWatchMod.Settings?.colonistCommandsLogLevel ?? Settings.SystemLogLevel.Moderate;
            }
        }
        
        /// <summary>
        /// Main tick method - call from MapComponent.
        /// </summary>
        public static void Tick(Map map)
        {
            try
            {
                // v0.8.1: Check if enabled in settings
                if (!RimWatchMod.Settings.colonistCommandsEnabled) return;
                
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - _lastCheckTick < CheckInterval) return;
                _lastCheckTick = currentTick;
                
                // Process queued tasks first
                ProcessTaskQueue(map);
                
                // Detect new emergencies
                DetectAndQueueEmergencies(map);
            }
            catch (Exception ex)
            {
                // Include full stack trace for debugging
                RimWatchLogger.Error("ColonistCommandSystem: Error in Tick", ex);
            }
        }
        
        /// <summary>
        /// Manually queue an emergency task (for external systems).
        /// </summary>
        public static void QueueEmergencyTask(EmergencyTask task)
        {
            if (task == null) return;

            // Avoid enqueuing duplicate tasks for the same target and type
            if (task.Target != null && _taskQueue.Any(t => t != null && t.Type == task.Type && t.Target == task.Target))
            {
                if (CommandLogLevel >= Settings.SystemLogLevel.Verbose)
                {
                    RimWatchLogger.Debug($"ColonistCommandSystem: Skipping duplicate {task.Type} task for {task.Target.LabelShort}");
                }
                return;
            }

            _taskQueue.Enqueue(task);
            if (CommandLogLevel != Settings.SystemLogLevel.Off)
            {
                // Compact vs detailed logging based on level
                if (CommandLogLevel == Settings.SystemLogLevel.Minimal)
                {
                    RimWatchLogger.Info($"🚨 ColonistCommandSystem: Queued {task.Type} task");
                }
                else
                {
                    RimWatchLogger.Info($"🚨 ColonistCommandSystem: Queued {task.Type} task (priority {task.Priority})");
                }
            }
        }
        
        /// <summary>
        /// Detects emergencies and auto-queues them.
        /// </summary>
        private static void DetectAndQueueEmergencies(Map map)
        {
            try
            {
                // 1. Downed colonists (HIGHEST PRIORITY)
                var downed = map.mapPawns.FreeColonistsSpawned
                    .Where(p => p.Downed && !p.Dead)
                    .ToList();
                    
                foreach (var pawn in downed)
                {
                    // Check if already being rescued
                    if (IsBeingRescued(pawn, map)) continue;
                    
                    // v0.8.4: Skip pawns с многократными Rescue-фейлами в короткий период
                    string pid = pawn.ThingID;
                    int currentTick = Find.TickManager.TicksGame;
                    if (_rescueFailureCount.TryGetValue(pid, out int failCount) &&
                        _rescueLastFailureTick.TryGetValue(pid, out int lastFailTick))
                    {
                        if (failCount >= 3 && currentTick - lastFailTick < RescueFailureCooldownTicks)
                        {
                            // Мы уже трижды подряд падали на Rescue этого pawn — не спамим задачи каждую секунду
                            continue;
                        }
                    }
                    
                    QueueEmergencyTask(new EmergencyTask
                    {
                        Type = TaskType.Rescue,
                        Target = pawn,
                        Priority = 100, // Max priority
                        Description = $"Rescue {pawn.LabelShort}"
                    });
                }
                
                // 2. Major fires (>5 fire things)
                var fires = map.listerThings.ThingsInGroup(ThingRequestGroup.Fire).ToList();
                if (fires.Count > 5)
                {
                    // Check if already fighting
                    bool anyFighting = map.mapPawns.FreeColonistsSpawned
                        .Any(p => p.CurJob != null && p.CurJob.def == JobDefOf.BeatFire);
                    
                    if (!anyFighting)
                    {
                        QueueEmergencyTask(new EmergencyTask
                        {
                            Type = TaskType.Firefight,
                            Target = fires.FirstOrDefault(),
                            Priority = 90,
                            Description = $"Fight {fires.Count} fires"
                        });
                    }
                }
                
                // 3. Bleeding colonists
                var bleeding = map.mapPawns.FreeColonistsSpawned
                    .Where(p => p.health.hediffSet.BleedRateTotal > 0.1f && !p.Dead && !p.Downed)
                    .ToList();
                    
                foreach (var pawn in bleeding)
                {
                    // Check if already being treated
                    if (IsBeingDoctored(pawn, map)) continue;
                    
                    QueueEmergencyTask(new EmergencyTask
                    {
                        Type = TaskType.Medical,
                        Target = pawn,
                        Priority = 85,
                        Description = $"Stop bleeding for {pawn.LabelShort}"
                    });
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ColonistCommandSystem: Error detecting emergencies", ex);
            }
        }
        
        /// <summary>
        /// Processes queued tasks.
        /// </summary>
        private static void ProcessTaskQueue(Map map)
        {
            if (_taskQueue.Count == 0) return;
            
            try
            {
                // Process up to 3 tasks per tick
                int processed = 0;
                while (_taskQueue.Count > 0 && processed < 3)
                {
                    EmergencyTask task = _taskQueue.Dequeue();
                    ExecuteEmergencyTask(task, map);
                    processed++;
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ColonistCommandSystem: Error processing queue", ex);
            }
        }
        
        /// <summary>
        /// Executes an emergency task.
        /// </summary>
        private static void ExecuteEmergencyTask(EmergencyTask task, Map map)
        {
            try
            {
                if (task == null || map == null)
                {
                    return;
                }
                
                switch (task.Type)
                {
                    case TaskType.Rescue:
                        ExecuteRescue(task, map);
                        break;
                        
                    case TaskType.Firefight:
                        ExecuteFirefight(task, map);
                        break;
                        
                    case TaskType.Medical:
                        ExecuteMedical(task, map);
                        break;
                }
            }
            catch (Exception ex)
            {
                // Log as structured failure with context
                var context = new Dictionary<string, object>
                {
                    { "taskType", task?.Type.ToString() ?? "null" },
                    { "target", task?.Target?.LabelShort ?? task?.Target?.def?.defName ?? "null" }
                };
                // Full stack trace to main log
                RimWatchLogger.Error($"ColonistCommandSystem: Error executing {task?.Type}", ex);
                RimWatchLogger.LogFailure("ColonistCommandSystem", $"Execute{task?.Type}", ex.Message, context);
                
                // v0.8.4: Track Rescue failures per pawn to prevent infinite queue spam
                if (task != null && task.Type == TaskType.Rescue && task.Target is Pawn p)
                {
                    string pid = p.ThingID;
                    int currentTick = Find.TickManager.TicksGame;
                    
                    if (!_rescueFailureCount.ContainsKey(pid))
                    {
                        _rescueFailureCount[pid] = 0;
                    }
                    _rescueFailureCount[pid]++;
                    _rescueLastFailureTick[pid] = currentTick;
                }
            }
        }
        
        /// <summary>
        /// Execute rescue task.
        /// v0.8.5: Enhanced with detailed step-by-step logging and comprehensive null-checks.
        /// </summary>
        private static void ExecuteRescue(EmergencyTask task, Map map)
        {
            // v0.8.5: STEP 1 - Validate task and target
            try
            {
                if (task == null)
                {
                    RimWatchLogger.LogFailure("ColonistCommandSystem", "ExecuteRescue", "Task is null", null);
                    return;
                }
                
                if (!(task.Target is Pawn downedPawn))
                {
                    RimWatchLogger.LogFailure("ColonistCommandSystem", "ExecuteRescue", "Target is not a Pawn",
                        new Dictionary<string, object>
                        {
                            { "targetType", task.Target?.GetType().Name ?? "null" }
                        });
                    return;
                }
                
                // v0.8.5: STEP 2 - Validate pawn state
                if (downedPawn == null)
                {
                    RimWatchLogger.LogFailure("ColonistCommandSystem", "ExecuteRescue", "Downed pawn is null", null);
                    return;
                }
                
                if (downedPawn.Dead)
                {
                    RimWatchLogger.LogDecision("ColonistCommandSystem", "RescueSkipped", new Dictionary<string, object>
                    {
                        { "patient", downedPawn.LabelShort },
                        { "reason", "Patient is dead" }
                    });
                    return;
                }
                
                if (!downedPawn.Spawned)
                {
                    RimWatchLogger.LogDecision("ColonistCommandSystem", "RescueSkipped", new Dictionary<string, object>
                    {
                        { "patient", downedPawn.LabelShort },
                        { "reason", "Patient not spawned" }
                    });
                    return;
                }
                
                if (downedPawn.Map != map)
                {
                    RimWatchLogger.LogDecision("ColonistCommandSystem", "RescueSkipped", new Dictionary<string, object>
                    {
                        { "patient", downedPawn.LabelShort },
                        { "reason", "Patient on different map" }
                    });
                    return;
                }
                
                // v0.8.5: STEP 3 - Check if already being rescued
                if (IsBeingRescued(downedPawn, map))
                {
                    RimWatchLogger.LogDecision("ColonistCommandSystem", "RescueSkipped", new Dictionary<string, object>
                    {
                        { "patient", downedPawn.LabelShort },
                        { "reason", "Already being rescued" }
                    });
                    return;
                }
                
                // v0.8.5: STEP 4 - Find rescuer
                RimWatchLogger.LogExecutionStart("ColonistCommandSystem", "FindRescuer", new Dictionary<string, object>
                {
                    { "patient", downedPawn.LabelShort },
                    { "patientPos", downedPawn.Position.ToString() }
                });
                
                Pawn rescuer = null;
                try
                {
                    rescuer = FindNearestAbleColonist(downedPawn.Position, map);
                }
                catch (Exception ex)
                {
                    RimWatchLogger.Error("ColonistCommandSystem: Error finding rescuer", ex);
                    RimWatchLogger.LogFailure("ColonistCommandSystem", "FindRescuer", ex.Message,
                        new Dictionary<string, object>
                        {
                            { "patient", downedPawn.LabelShort }
                        });
                    return;
                }
                
                if (rescuer == null)
                {
                    string key = $"rescue_no_rescuer_{downedPawn.ThingID}";
                    RimWatchLogger.WarningThrottledByKey(
                        key,
                        $"ColonistCommandSystem: No rescuer available for {downedPawn.LabelShort}");
                    RimWatchLogger.LogFailure("ColonistCommandSystem", "FindRescuer", "No able colonist found",
                        new Dictionary<string, object>
                        {
                            { "patient", downedPawn.LabelShort },
                            { "colonistsOnMap", map?.mapPawns?.FreeColonistsSpawnedCount ?? 0 }
                        });
                    return;
                }
                
                // v0.8.5: STEP 5 - Validate rescuer state
                if (!rescuer.Spawned)
                {
                    RimWatchLogger.LogFailure("ColonistCommandSystem", "ExecuteRescue", "Rescuer not spawned",
                        new Dictionary<string, object>
                        {
                            { "rescuer", rescuer.LabelShort },
                            { "patient", downedPawn.LabelShort }
                        });
                    return;
                }
                
                if (rescuer.Map != map)
                {
                    RimWatchLogger.LogFailure("ColonistCommandSystem", "ExecuteRescue", "Rescuer on different map",
                        new Dictionary<string, object>
                        {
                            { "rescuer", rescuer.LabelShort },
                            { "rescuerMap", rescuer.Map?.Index.ToString() ?? "null" },
                            { "targetMap", map?.Index.ToString() ?? "null" },
                            { "patient", downedPawn.LabelShort }
                        });
                    return;
                }

                if (rescuer.jobs == null)
                {
                    RimWatchLogger.LogFailure("ColonistCommandSystem", "ExecuteRescue", "Rescuer has no jobs tracker",
                        new Dictionary<string, object>
                        {
                            { "rescuer", rescuer.LabelShort },
                            { "rescuerMap", rescuer.Map?.Index.ToString() ?? "null" },
                            { "patient", downedPawn.LabelShort }
                        });
                    return;
                }
                
                // v0.8.5: STEP 6 - Check if rescuer already busy with rescue
                if (rescuer.CurJob != null && rescuer.CurJob.def == JobDefOf.Rescue)
                {
                    RimWatchLogger.LogDecision("ColonistCommandSystem", "RescueSkipped", new Dictionary<string, object>
                    {
                        { "rescuer", rescuer.LabelShort },
                        { "patient", downedPawn.LabelShort },
                        { "reason", "Rescuer already performing rescue" }
                    });
                    return;
                }
                
                // v0.8.5: STEP 7 - Create rescue job
                RimWatchLogger.LogExecutionStart("ColonistCommandSystem", "CreateRescueJob", new Dictionary<string, object>
                {
                    { "rescuer", rescuer.LabelShort },
                    { "patient", downedPawn.LabelShort }
                });
                
                Job rescueJob = null;
                try
                {
                    rescueJob = JobMaker.MakeJob(JobDefOf.Rescue, downedPawn);
                }
                catch (Exception ex)
                {
                    RimWatchLogger.Error("ColonistCommandSystem: Error creating rescue job", ex);
                    RimWatchLogger.LogFailure("ColonistCommandSystem", "CreateRescueJob", ex.Message,
                        new Dictionary<string, object>
                        {
                            { "rescuer", rescuer.LabelShort },
                            { "patient", downedPawn.LabelShort }
                        });
                    return;
                }
                
                if (rescueJob == null)
                {
                    RimWatchLogger.LogFailure("ColonistCommandSystem", "CreateRescueJob", "JobMaker returned null",
                        new Dictionary<string, object>
                        {
                            { "rescuer", rescuer.LabelShort },
                            { "patient", downedPawn.LabelShort },
                            { "jobDef", JobDefOf.Rescue?.defName ?? "null" }
                        });
                    return;
                }

                rescueJob.count = 1;
                
                // v0.8.5: STEP 8 - Assign job to rescuer
                RimWatchLogger.LogExecutionStart("ColonistCommandSystem", "AssignRescueJob", new Dictionary<string, object>
                {
                    { "rescuer", rescuer.LabelShort },
                    { "patient", downedPawn.LabelShort },
                    { "jobDef", rescueJob.def?.defName ?? "null" }
                });
                
                bool jobAccepted = false;
                try
                {
                    jobAccepted = rescuer.jobs.TryTakeOrderedJob(rescueJob, JobTag.Misc);
                }
                catch (Exception ex)
                {
                    RimWatchLogger.Error("ColonistCommandSystem: Error assigning rescue job", ex);
                    RimWatchLogger.LogFailure("ColonistCommandSystem", "AssignRescueJob", ex.Message,
                        new Dictionary<string, object>
                        {
                            { "rescuer", rescuer.LabelShort },
                            { "patient", downedPawn.LabelShort }
                        });
                    return;
                }
                
                if (jobAccepted)
                {
                    RimWatchLogger.LogExecutionEnd("ColonistCommandSystem", "ExecuteRescue", true, 0,
                        $"{rescuer.LabelShort} → {downedPawn.LabelShort}");
                    RimWatchLogger.Info($"🚑 ColonistCommandSystem: {rescuer.LabelShort} rescuing {downedPawn.LabelShort}");
                    
                    // Clear failure count on success
                    string pid = downedPawn.ThingID;
                    if (_rescueFailureCount.ContainsKey(pid))
                    {
                        _rescueFailureCount[pid] = 0;
                    }
                }
                else
                {
                    RimWatchLogger.LogFailure("ColonistCommandSystem", "AssignRescueJob", "Rescuer refused Rescue job",
                        new Dictionary<string, object>
                        {
                            { "rescuer", rescuer.LabelShort },
                            { "patient", downedPawn.LabelShort },
                            { "rescuerCurrentJob", rescuer.CurJobDef?.defName ?? "null" },
                            { "rescuerDowned", rescuer.Downed },
                            { "rescuerDead", rescuer.Dead },
                            { "rescuerMentalState", rescuer.InMentalState }
                        });
                }
            }
            catch (Exception ex)
            {
                // Log full exception with complete stack trace
                RimWatchLogger.Error("ColonistCommandSystem: Unhandled exception in ExecuteRescue", ex);
                RimWatchLogger.LogFailure("ColonistCommandSystem", "ExecuteRescue", ex.Message,
                    new Dictionary<string, object>
                    {
                        { "targetType", task?.Target?.GetType().Name ?? "null" },
                        { "stackTrace", ex.StackTrace ?? "no stack trace" }
                    });
            }
        }
        
        /// <summary>
        /// Execute firefighting task.
        /// </summary>
        private static void ExecuteFirefight(EmergencyTask task, Map map)
        {
            var fires = map.listerThings.ThingsInGroup(ThingRequestGroup.Fire).Take(10).ToList();
            if (fires.Count == 0) return;
            
            // Get top 3 nearest colonists
            IntVec3 fireCenter = fires.First().Position;
            var firefighters = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && !p.Dead && !p.InMentalState)
                .OrderBy(p => p.Position.DistanceTo(fireCenter))
                .Take(3)
                .ToList();
            
            int assigned = 0;
            foreach (var fighter in firefighters)
            {
                if (assigned >= fires.Count) break;
                
                // Assign to nearest fire
                Thing fire = fires[assigned];
                Job beatFire = JobMaker.MakeJob(JobDefOf.BeatFire, fire);
                
                if (fighter.jobs.TryTakeOrderedJob(beatFire, JobTag.Misc))
                {
                    assigned++;
                }
            }
            
            if (assigned > 0)
            {
                RimWatchLogger.Info($"🔥 ColonistCommandSystem: {assigned} colonists fighting fires");
            }
        }
        
        /// <summary>
        /// Execute medical task.
        /// v0.8.4+: Добавлена проверка, что пациент уже лечится - НЕ прерывать!
        /// </summary>
        private static void ExecuteMedical(EmergencyTask task, Map map)
        {
            if (!(task.Target is Pawn patient)) return;
            if (patient.Dead || !patient.Spawned) return;
            
            // v0.8.4+: КРИТИЧНО - проверить что пациент УЖЕ лечится!
            if (IsBeingDoctored(patient, map))
            {
                // Уже кто-то лечит - НЕ вмешиваться!
                return;
            }
            
            // Find doctor
            Pawn doctor = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && !p.Dead && 
                           p.workSettings != null &&
                           p.workSettings.WorkIsActive(WorkTypeDefOf.Doctor))
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Medicine).Level)
                .FirstOrDefault();
            
            if (doctor == null) return;
            
            // v0.8.4+: НЕ прерывать доктора, если он уже лечит кого-то!
            if (doctor.CurJob != null && doctor.CurJob.def == JobDefOf.TendPatient)
            {
                // Доктор уже лечит кого-то - пусть закончит!
                return;
            }
            
            // Create tend job
            Job tendJob = JobMaker.MakeJob(JobDefOf.TendPatient, patient);
            
            if (doctor.jobs.TryTakeOrderedJob(tendJob, JobTag.Misc))
            {
                RimWatchLogger.Info($"🏥 ColonistCommandSystem: {doctor.LabelShort} treating {patient.LabelShort}");
            }
        }
        
        /// <summary>
        /// Helper: Check if pawn is being rescued.
        /// </summary>
        private static bool IsBeingRescued(Pawn pawn, Map map)
        {
            return map.mapPawns.FreeColonistsSpawned
                .Any(p => p.CurJob != null && 
                         p.CurJob.def == JobDefOf.Rescue && 
                         p.CurJob.targetA.Thing == pawn);
        }
        
        /// <summary>
        /// Helper: Check if pawn is being doctored.
        /// </summary>
        private static bool IsBeingDoctored(Pawn pawn, Map map)
        {
            return map.mapPawns.FreeColonistsSpawned
                .Any(p => p.CurJob != null && 
                         p.CurJob.def == JobDefOf.TendPatient && 
                         p.CurJob.targetA.Thing == pawn);
        }
        
        /// <summary>
        /// Helper: Find nearest able colonist.
        /// v0.8.4: Enhanced null checks for Map, jobs, health, capacities.
        /// </summary>
        private static Pawn FindNearestAbleColonist(IntVec3 position, Map map)
        {
            if (map == null || map.mapPawns == null) return null;
            
            return map.mapPawns.FreeColonistsSpawned
                .Where(p => p != null && 
                           p.Spawned && 
                           p.Map == map &&
                           !p.Downed && 
                           !p.Dead && 
                           !p.InMentalState &&
                           p.jobs != null &&
                           p.health != null &&
                           p.health.capacities != null &&
                           p.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
                .OrderBy(p => p.Position.DistanceTo(position))
                .FirstOrDefault();
        }
        
        /// <summary>
        /// Emergency task types.
        /// </summary>
        public enum TaskType
        {
            Rescue,
            Firefight,
            Medical
        }
        
        /// <summary>
        /// Emergency task structure.
        /// </summary>
        public class EmergencyTask
        {
            public TaskType Type;
            public Thing Target;
            public int Priority;
            public string Description;
        }
    }
}

