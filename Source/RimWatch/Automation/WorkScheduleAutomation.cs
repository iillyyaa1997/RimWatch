using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation
{
    /// <summary>
    /// Schedule type based on colony state.
    /// </summary>
    public enum ScheduleType
    {
        Normal,
        NightOwl,
        Emergency,
        FoodCrisis,
        Medical,
        Recreation,
        Seasonal
    }

    /// <summary>
    /// 🌙 Work Schedule Automation - автоматическое управление расписанием работы колонистов.
    /// Учитывает Night Owl trait и оптимизирует расписание сна и работы.
    /// </summary>
    public static class WorkScheduleAutomation
    {
        private static int _tickCounter = 0;
        private const int UpdateInterval = 18000; // Обновление каждые 5 минут (18000 тиков / 300 секунд)
        private static Dictionary<Pawn, bool> _scheduleApplied = new Dictionary<Pawn, bool>();
        private static Dictionary<Pawn, ScheduleType> _currentSchedules = new Dictionary<Pawn, ScheduleType>();

        /// <summary>
        /// Вызывается периодически для обновления расписаний.
        /// </summary>
        public static void Tick(Map map)
        {
            try
            {
                _tickCounter++;
                if (_tickCounter >= UpdateInterval)
                {
                    _tickCounter = 0;
                    ManageWorkSchedules(map);
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("WorkScheduleAutomation: Error in Tick", ex);
            }
        }

        /// <summary>
        /// Tick with colony state (for context-aware scheduling).
        /// </summary>
        public static void Tick(Map map, RimWatch.Automation.ColonyState state)
        {
            try
            {
                _tickCounter++;
                if (_tickCounter >= UpdateInterval)
                {
                    _tickCounter = 0;
                    ManageWorkSchedulesWithContext(map, state);
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("WorkScheduleAutomation: Error in Tick with context", ex);
            }
        }

        /// <summary>
        /// Manages work schedules for all colonists.
        /// </summary>
        private static void ManageWorkSchedules(Map map)
        {
            try
            {
                List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
                if (colonists.Count == 0) return;

                int nightOwlsScheduled = 0;
                int normalScheduled = 0;

                foreach (Pawn colonist in colonists)
                {
                    if (colonist.Dead || colonist.timetable == null) continue;

                    // Check if colonist has Night Owl trait
                    bool isNightOwl = HasNightOwlTrait(colonist);

                    // Check if we've already applied schedule to this colonist
                    bool alreadyApplied = _scheduleApplied.ContainsKey(colonist) && _scheduleApplied[colonist];

                    if (!alreadyApplied || ShouldReapplySchedule(colonist, isNightOwl))
                    {
                        ApplySchedule(colonist, isNightOwl);
                        _scheduleApplied[colonist] = true;

                        if (isNightOwl)
                            nightOwlsScheduled++;
                        else
                            normalScheduled++;
                    }
                }

                if (nightOwlsScheduled > 0 || normalScheduled > 0)
                {
                    RimWatchLogger.Info($"🌙 WorkScheduleAutomation: Updated schedules - {normalScheduled} normal colonists, {nightOwlsScheduled} night owls");
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("WorkScheduleAutomation: Error in ManageWorkSchedules", ex);
            }
        }

        /// <summary>
        /// Checks if a colonist has the Night Owl trait.
        /// </summary>
        private static bool HasNightOwlTrait(Pawn colonist)
        {
            if (colonist.story?.traits == null) return false;

            // Night Owl trait defName is "NightOwl" - check by string since TraitDefOf doesn't have it
            TraitDef nightOwlDef = DefDatabase<TraitDef>.GetNamedSilentFail("NightOwl");
            if (nightOwlDef == null) return false;
            
            return colonist.story.traits.HasTrait(nightOwlDef);
        }

        /// <summary>
        /// Checks if schedule should be reapplied (e.g., colonist manually changed it).
        /// </summary>
        private static bool ShouldReapplySchedule(Pawn colonist, bool isNightOwl)
        {
            try
            {
                if (colonist.timetable == null) return false;

                // Sample a few hours to check if schedule matches expected pattern
                // Night Owl: should be sleeping during day (6:00-14:00)
                // Normal: should be sleeping during night (22:00-6:00)

                if (isNightOwl)
                {
                    // Check hour 10 (10:00 AM) - Night Owls should be sleeping
                    TimeAssignmentDef assignment = colonist.timetable.GetAssignment(10);
                    return assignment != TimeAssignmentDefOf.Sleep;
                }
                else
                {
                    // Check hour 2 (2:00 AM) - Normal colonists should be sleeping
                    TimeAssignmentDef assignment = colonist.timetable.GetAssignment(2);
                    return assignment != TimeAssignmentDefOf.Sleep;
                }
            }
            catch
            {
                return true; // If error, reapply to be safe
            }
        }

        /// <summary>
        /// Applies appropriate schedule based on colonist type.
        /// </summary>
        private static void ApplySchedule(Pawn colonist, bool isNightOwl)
        {
            try
            {
                if (colonist.timetable == null) return;

                if (isNightOwl)
                {
                    ApplyNightOwlSchedule(colonist);
                }
                else
                {
                    ApplyNormalSchedule(colonist);
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error($"WorkScheduleAutomation: Error applying schedule to {colonist.LabelShort}", ex);
            }
        }

        /// <summary>
        /// Applies Night Owl schedule: work at night, sleep during day.
        /// Schedule: 
        ///   22:00-6:00 (night): Work (8 hours)
        ///   6:00-14:00 (day): Sleep (8 hours)
        ///   14:00-18:00 (afternoon): Anything (4 hours)
        ///   18:00-22:00 (evening): Work (4 hours)
        /// </summary>
        private static void ApplyNightOwlSchedule(Pawn colonist)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                TimeAssignmentDef assignment;

                if (hour >= 6 && hour < 14)
                {
                    // 6:00-14:00: Sleep (day sleep for night owls)
                    assignment = TimeAssignmentDefOf.Sleep;
                }
                else if (hour >= 14 && hour < 18)
                {
                    // 14:00-18:00: Anything (recreation, meals)
                    assignment = TimeAssignmentDefOf.Anything;
                }
                else
                {
                    // 22:00-6:00 (wraps around midnight) and 18:00-22:00: Work
                    assignment = TimeAssignmentDefOf.Work;
                }

                colonist.timetable.SetAssignment(hour, assignment);
            }

            RimWatchLogger.Debug($"WorkScheduleAutomation: Applied Night Owl schedule to {colonist.LabelShort}");
        }

        /// <summary>
        /// Applies normal schedule: work during day, sleep at night.
        /// Schedule:
        ///   22:00-6:00 (night): Sleep (8 hours)
        ///   6:00-12:00 (morning): Work (6 hours)
        ///   12:00-13:00 (noon): Anything (1 hour - lunch)
        ///   13:00-18:00 (afternoon): Work (5 hours)
        ///   18:00-22:00 (evening): Anything (4 hours - dinner, recreation)
        /// </summary>
        private static void ApplyNormalSchedule(Pawn colonist)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                TimeAssignmentDef assignment;

                if ((hour >= 22 && hour < 24) || (hour >= 0 && hour < 6))
                {
                    // 22:00-6:00: Sleep (night sleep)
                    assignment = TimeAssignmentDefOf.Sleep;
                }
                else if (hour >= 6 && hour < 12)
                {
                    // 6:00-12:00: Work (morning)
                    assignment = TimeAssignmentDefOf.Work;
                }
                else if (hour >= 12 && hour < 13)
                {
                    // 12:00-13:00: Anything (lunch break)
                    assignment = TimeAssignmentDefOf.Anything;
                }
                else if (hour >= 13 && hour < 18)
                {
                    // 13:00-18:00: Work (afternoon)
                    assignment = TimeAssignmentDefOf.Work;
                }
                else
                {
                    // 18:00-22:00: Anything (evening - dinner, recreation)
                    assignment = TimeAssignmentDefOf.Anything;
                }

                colonist.timetable.SetAssignment(hour, assignment);
            }

            RimWatchLogger.Debug($"WorkScheduleAutomation: Applied Normal schedule to {colonist.LabelShort}");
        }

        /// <summary>
        /// Manages schedules with colony state context.
        /// </summary>
        private static void ManageWorkSchedulesWithContext(Map map, RimWatch.Automation.ColonyState state)
        {
            try
            {
                List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
                if (colonists.Count == 0) return;

                int schedulesChanged = 0;

                foreach (Pawn colonist in colonists)
                {
                    if (colonist.Dead || colonist.timetable == null) continue;

                    ScheduleType newSchedule = DetermineScheduleType(colonist, state);
                    ScheduleType oldSchedule = _currentSchedules.ContainsKey(colonist) ? 
                        _currentSchedules[colonist] : ScheduleType.Normal;

                    if (newSchedule != oldSchedule)
                    {
                        ApplyScheduleByType(colonist, newSchedule);
                        _currentSchedules[colonist] = newSchedule;
                        schedulesChanged++;

                        RimWatchLogger.Info($"WorkScheduleAutomation: {colonist.LabelShort} schedule: {oldSchedule} → {newSchedule}");
                    }
                }

                if (schedulesChanged > 0)
                {
                    RimWatchLogger.Info($"WorkScheduleAutomation: Updated {schedulesChanged} schedules based on colony state");
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("WorkScheduleAutomation: Error in ManageWorkSchedulesWithContext", ex);
            }
        }

        /// <summary>
        /// Determines appropriate schedule type based on colony state and colonist traits.
        /// </summary>
        private static ScheduleType DetermineScheduleType(Pawn colonist, RimWatch.Automation.ColonyState state)
        {
            // Emergency takes precedence
            if (state.IsEmergency || state.IsCombatActive)
            {
                return ScheduleType.Emergency;
            }

            // Food crisis
            if (state.IsLowFood)
            {
                return ScheduleType.FoodCrisis;
            }

            // Medical priority if many injured
            if (state.HasInjuredColonists && colonist.workSettings != null)
            {
                if (colonist.workSettings.GetPriority(WorkTypeDefOf.Doctor) <= 2)
                {
                    return ScheduleType.Medical;
                }
            }

            // Recreation if mood is low
            float mood = colonist.needs?.mood?.CurLevel ?? 0.5f;
            if (mood < 0.3f)
            {
                return ScheduleType.Recreation;
            }

            // Night Owl trait
            if (HasNightOwlTrait(colonist))
            {
                return ScheduleType.NightOwl;
            }

            // Seasonal adjustments for winter
            if (state.IsWinterApproaching)
            {
                return ScheduleType.Seasonal;
            }

            return ScheduleType.Normal;
        }

        /// <summary>
        /// Applies schedule by type.
        /// </summary>
        private static void ApplyScheduleByType(Pawn colonist, ScheduleType type)
        {
            switch (type)
            {
                case ScheduleType.Emergency:
                    ApplyEmergencySchedule(colonist);
                    break;
                case ScheduleType.FoodCrisis:
                    ApplyFoodCrisisSchedule(colonist);
                    break;
                case ScheduleType.Medical:
                    ApplyMedicalSchedule(colonist);
                    break;
                case ScheduleType.Recreation:
                    ApplyRecreationSchedule(colonist);
                    break;
                case ScheduleType.NightOwl:
                    ApplyNightOwlSchedule(colonist);
                    break;
                case ScheduleType.Seasonal:
                    ApplySeasonalSchedule(colonist);
                    break;
                default:
                    ApplyNormalSchedule(colonist);
                    break;
            }
        }

        /// <summary>
        /// Emergency schedule: 20h work, 4h sleep.
        /// </summary>
        private static void ApplyEmergencySchedule(Pawn colonist)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                TimeAssignmentDef assignment;
                
                // Only 4 hours sleep (2-6 AM)
                if (hour >= 2 && hour < 6)
                {
                    assignment = TimeAssignmentDefOf.Sleep;
                }
                else
                {
                    assignment = TimeAssignmentDefOf.Work;
                }

                colonist.timetable.SetAssignment(hour, assignment);
            }
        }

        /// <summary>
        /// Food crisis schedule: Focus on food production.
        /// </summary>
        private static void ApplyFoodCrisisSchedule(Pawn colonist)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                TimeAssignmentDef assignment;

                if ((hour >= 23 && hour < 24) || (hour >= 0 && hour < 5))
                {
                    assignment = TimeAssignmentDefOf.Sleep; // 6h sleep
                }
                else
                {
                    assignment = TimeAssignmentDefOf.Work; // 18h work
                }

                colonist.timetable.SetAssignment(hour, assignment);
            }
        }

        /// <summary>
        /// Medical schedule: Staggered for 24/7 coverage.
        /// </summary>
        private static void ApplyMedicalSchedule(Pawn colonist)
        {
            // Doctors work in shifts for 24/7 coverage
            // This is simplified - could be improved with shift rotation
            ApplyNormalSchedule(colonist); // For now, use normal schedule
        }

        /// <summary>
        /// Recreation schedule: More "Anything" time for mood recovery.
        /// </summary>
        private static void ApplyRecreationSchedule(Pawn colonist)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                TimeAssignmentDef assignment;

                if ((hour >= 23 && hour < 24) || (hour >= 0 && hour < 7))
                {
                    assignment = TimeAssignmentDefOf.Sleep; // 8h sleep
                }
                else if (hour >= 7 && hour < 12)
                {
                    assignment = TimeAssignmentDefOf.Work; // 5h work
                }
                else if (hour >= 12 && hour < 18)
                {
                    assignment = TimeAssignmentDefOf.Anything; // 6h recreation
                }
                else
                {
                    assignment = TimeAssignmentDefOf.Work; // 5h work
                }

                colonist.timetable.SetAssignment(hour, assignment);
            }
        }

        /// <summary>
        /// Seasonal schedule: Extended work hours for winter prep.
        /// </summary>
        private static void ApplySeasonalSchedule(Pawn colonist)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                TimeAssignmentDef assignment;

                if ((hour >= 22 && hour < 24) || (hour >= 0 && hour < 5))
                {
                    assignment = TimeAssignmentDefOf.Sleep; // 7h sleep
                }
                else if (hour >= 12 && hour < 13)
                {
                    assignment = TimeAssignmentDefOf.Anything; // 1h lunch
                }
                else
                {
                    assignment = TimeAssignmentDefOf.Work; // 16h work
                }

                colonist.timetable.SetAssignment(hour, assignment);
            }
        }

        /// <summary>
        /// Resets schedule tracking (call when colonists join/leave).
        /// </summary>
        public static void ResetScheduleTracking()
        {
            _scheduleApplied.Clear();
            _currentSchedules.Clear();
            RimWatchLogger.Debug("WorkScheduleAutomation: Reset schedule tracking");
        }
    }
}

