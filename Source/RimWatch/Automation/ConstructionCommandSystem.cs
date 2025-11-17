using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using RimWatch.Utils;

namespace RimWatch.Automation
{
    /// <summary>
    /// Система принудительного назначения строителей на критичные объекты строительства.
    /// Решает проблему простоев пешек при наличии незавершённого строительства.
    /// </summary>
    public static class ConstructionCommandSystem
    {
        private static int _lastCheckTick = -9999;
        private const int CheckInterval = 600; // 10 секунд

        private static readonly Dictionary<string, int> _assignmentPriorities = new Dictionary<string, int>
        {
            // Стены комнат - высший приоритет (защита от погоды)
            { "Wall", 100 },
            
            // Двери - высокий приоритет (проход в комнаты)
            { "Door", 90 },
            { "Autodoor", 90 },
            
            // Кровати - высокий приоритет (сон колонистов)
            { "Bed", 85 },
            { "DoubleBed", 85 },
            { "RoyalBed", 85 },
            
            // Крыша - важно для защиты
            { "Roof", 80 },
            
            // Генераторы - энергия
            { "Generator", 75 },
            { "SolarGenerator", 75 },
            { "WoodFiredGenerator", 75 },
            
            // Кухня - еда
            { "Stove", 70 },
            { "ElectricStove", 70 },
            { "FueledStove", 70 },
            
            // Всё остальное
            { "Default", 50 }
        };

        public static void ManageConstructionPriorities(Map map, RimWatch.Settings.RimWatchSettings settings)
        {
            if (!settings.constructionCommandsEnabled)
                return;

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - _lastCheckTick < CheckInterval)
                return;

            _lastCheckTick = currentTick;

            RimWatchLogger.LogExecutionStart("ConstructionCommandSystem", "ManageConstructionPriorities", new Dictionary<string, object>
            {
                { "tick", currentTick }
            });

            try
            {
                // Найти все незавершённые объекты строительства
                List<Thing> unfinished = map.listerThings.AllThings
                    .Where(t => t is Frame || t is Blueprint_Build)
                    .Where(t => t.Spawned && t.Map == map)
                    .ToList();

                if (unfinished.Count == 0)
                {
                    RimWatchLogger.LogDecision("ConstructionCommandSystem", "NoUnfinished", new Dictionary<string, object>
                    {
                        { "map", map.ToString() }
                    });
                    return;
                }

                // Найти всех способных строителей
                List<Pawn> builders = map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.Dead && !p.Downed && p.workSettings != null)
                    .Where(p => p.workSettings.GetPriority(WorkTypeDefOf.Construction) > 0)
                    .Where(p => p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                    .ToList();

                if (builders.Count == 0)
                {
                    RimWatchLogger.LogDecision("ConstructionCommandSystem", "NoBuilders", new Dictionary<string, object>
                    {
                        { "unfinished", unfinished.Count }
                    });
                    return;
                }

                RimWatchLogger.LogDecision("ConstructionCommandSystem", "AnalyzingConstruction", new Dictionary<string, object>
                {
                    { "unfinished", unfinished.Count },
                    { "builders", builders.Count }
                });

                // Отсортировать объекты по приоритету
                var prioritized = unfinished
                    .Select(obj => new { Thing = obj, Priority = GetConstructionPriority(obj) })
                    .OrderByDescending(x => x.Priority)
                    .ToList();

                // Назначить строителей на топ-приоритетные объекты
                int assignmentsCount = 0;
                foreach (var item in prioritized.Take(3)) // Топ-3 приоритетных объекта
                {
                    Pawn nearestBuilder = FindNearestAvailableBuilder(item.Thing, builders);
                    if (nearestBuilder == null)
                        continue;

                    // Если строитель уже что-то строит, не прерываем
                    if (nearestBuilder.CurJob != null && 
                        (nearestBuilder.CurJob.def == JobDefOf.Build || 
                         nearestBuilder.CurJob.def == JobDefOf.FinishFrame))
                    {
                        continue;
                    }

                    // Назначить строителю работу
                    bool assigned = TryAssignConstructionJob(nearestBuilder, item.Thing);
                    if (assigned)
                    {
                        assignmentsCount++;
                        RimWatchLogger.LogDecision("ConstructionCommandSystem", "AssignedBuilder", new Dictionary<string, object>
                        {
                            { "builder", nearestBuilder.LabelShort },
                            { "target", item.Thing.def.defName },
                            { "priority", item.Priority },
                            { "position", item.Thing.Position.ToString() }
                        });
                    }
                }

                RimWatchLogger.LogExecutionEnd("ConstructionCommandSystem", "ManageConstructionPriorities", true, new Dictionary<string, object>
                {
                    { "unfinished", unfinished.Count },
                    { "builders", builders.Count },
                    { "assignments", assignmentsCount }
                });
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.LogExecutionEnd("ConstructionCommandSystem", "ManageConstructionPriorities", false, new Dictionary<string, object>
                {
                    { "error", ex.Message }
                });
            }
        }

        private static int GetConstructionPriority(Thing thing)
        {
            string defName = thing.def.defName;

            // Проверка exact match
            if (_assignmentPriorities.TryGetValue(defName, out int priority))
                return priority;

            // Проверка по частичному совпадению
            foreach (var kvp in _assignmentPriorities)
            {
                if (kvp.Key != "Default" && defName.Contains(kvp.Key))
                    return kvp.Value;
            }

            return _assignmentPriorities["Default"];
        }

        private static Pawn FindNearestAvailableBuilder(Thing target, List<Pawn> builders)
        {
            Pawn nearest = null;
            float minDist = float.MaxValue;

            foreach (Pawn builder in builders)
            {
                // Проверка что строитель может достичь объекта
                if (!builder.CanReach(target, PathEndMode.Touch, Danger.Deadly))
                    continue;

                float dist = (builder.Position - target.Position).LengthHorizontalSquared;
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = builder;
                }
            }

            return nearest;
        }

        private static bool TryAssignConstructionJob(Pawn builder, Thing target)
        {
            try
            {
                Job job = null;

                if (target is Frame frame)
                {
                    // Если фрейм уже частично построен
                    if (frame.workDone > 0)
                    {
                        job = JobMaker.MakeJob(JobDefOf.FinishFrame, frame);
                    }
                    else
                    {
                        job = JobMaker.MakeJob(JobDefOf.Build, frame);
                    }
                }
                else if (target is Blueprint_Build blueprint)
                {
                    // Blueprint ещё не начат
                    job = JobMaker.MakeJob(JobDefOf.Build, blueprint);
                }

                if (job != null)
                {
                    builder.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.LogFailure("ConstructionCommandSystem", "TryAssignConstructionJob", 
                    $"Failed to assign job: {ex.Message}", new Dictionary<string, object>
                    {
                        { "builder", builder.LabelShort },
                        { "target", target.def.defName }
                    });
            }

            return false;
        }
    }
}

