using RimWatch.AI;
using RimWatch.Core;
using RimWatch.Settings;
using RimWatch.Utils;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWatch.Automation
{
    /// <summary>
    /// Автоматизация управления работой колонистов.
    /// Автоматически назначает приоритеты работы на основе анализа колонии и решений AI.
    /// </summary>
    public static class WorkAutomation
    {
        private static bool _isEnabled = false;
        private static int _tickCounter = 0;
        private const int UpdateInterval = 250; // Обновление каждые ~4 секунды (250 тиков)

        /// <summary>
        /// Helper: current log level for WorkAutomation.
        /// </summary>
        private static SystemLogLevel WorkLogLevel
        {
            get
            {
                return RimWatchMod.Settings?.workLogLevel ?? SystemLogLevel.Moderate;
            }
        }

        /// <summary>
        /// Включает или выключает автоматизацию работы.
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                RimWatchLogger.Info($"WorkAutomation: {(value ? "Enabled" : "Disabled")}");
            }
        }

        /// <summary>
        /// Вызывается каждый тик игры. Выполняет автоматизацию с заданным интервалом.
        /// </summary>
        public static void Tick()
        {
            if (!IsEnabled) return;
            if (!RimWatchCore.AutopilotEnabled) return;

            _tickCounter++;
            if (_tickCounter >= UpdateInterval)
            {
                _tickCounter = 0;

                if (WorkLogLevel >= SystemLogLevel.Verbose)
                {
                    RimWatchLogger.Debug($"[WorkAutomation] Interval reached ({UpdateInterval} ticks), running work priority update...");
                }

                UpdateWorkPriorities();
                
                // v0.7.9: Update colonist schedules (work/sleep/rest/food/recreation)
                UpdateColonistSchedules();
            }
        }

        /// <summary>
        /// Обновляет приоритеты работы для всех колонистов.
        /// </summary>
        private static void UpdateWorkPriorities()
        {
            // Получаем текущую колонию
            Map map = Find.CurrentMap;
            if (map == null) return;

            // Получаем всех колонистов
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
            if (colonists.Count == 0) return;

            // v0.8.3: Log execution start
            RimWatchLogger.LogExecutionStart("WorkAutomation", "UpdateWorkPriorities", new Dictionary<string, object>
            {
                { "colonists", colonists.Count }
            });

            if (WorkLogLevel >= SystemLogLevel.Verbose)
            {
                RimWatchLogger.Debug($"WorkAutomation: Updating priorities for {colonists.Count} colonists");
            }

            // Переключаем режим приоритетов в зависимости от настройки
            bool useManualPriorities = RimWatchMod.Settings?.useManualPriorities ?? true;
            bool currentUseWorkPriorities = Current.Game.playSettings.useWorkPriorities;
            
            if (currentUseWorkPriorities != useManualPriorities)
            {
                Current.Game.playSettings.useWorkPriorities = useManualPriorities;
                string modeName = useManualPriorities ? "Manual Priorities (1-4)" : "Simple Checkboxes";
                RimWatchLogger.Info($"🔄 WorkAutomation: Switched to {modeName}");
            }

            // Анализируем потребности колонии
            ColonyNeeds needs = AnalyzeColonyNeeds(map);
            
            // v0.8.3: Log colony needs analysis
            RimWatchLogger.LogDecision("WorkAutomation", "ColonyNeeds", new Dictionary<string, object>
            {
                { "foodUrgency", needs.FoodUrgency },
                { "constructionUrgency", needs.ConstructionUrgency },
                { "researchUrgency", needs.ResearchUrgency },
                { "defenseUrgency", needs.DefenseUrgency }
            });

            // Получаем текущего рассказчика (пока Balanced)
            AIStoryteller storyteller = RimWatchCore.CurrentStoryteller;
            
            int prioritiesChanged = 0;

            foreach (Pawn colonist in colonists)
            {
                if (colonist.workSettings == null) continue;
                if (colonist.Dead || colonist.Downed) continue;

                // AI рассказчик принимает решения о приоритетах
                bool changed = AssignWorkPriorities(colonist, needs, storyteller);
                if (changed) prioritiesChanged++;
            }
            
            // v0.8.3: Log execution end with summary
            RimWatchLogger.LogExecutionEnd("WorkAutomation", "UpdateWorkPriorities", true, 0, $"Updated {prioritiesChanged}/{colonists.Count} colonists");
        }

        /// <summary>
        /// Анализирует текущие потребности колонии.
        /// </summary>
        private static ColonyNeeds AnalyzeColonyNeeds(Map map)
        {
            ColonyNeeds needs = new ColonyNeeds();

            // ✅ EMERGENCY: Check if colonists are sleeping outside (HIGHEST PRIORITY!)
            bool colonistsSleepingOutside = false;
            foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
            {
                Building_Bed bed = colonist.ownership?.OwnedBed;
                if (bed == null || !bed.Position.Roofed(map))
                {
                    colonistsSleepingOutside = true;
                    break;
                }
            }
            
            if (colonistsSleepingOutside)
            {
                // v0.8.2: Use throttled warning to prevent spam (warn once per minute)
                RimWatchLogger.WarningThrottledByKey("emergency_sleeping_outside", "WorkAutomation: EMERGENCY - Colonists sleeping outside! Construction priority = MAXIMUM");
                needs.ConstructionUrgency = 4; // EMERGENCY LEVEL
            }

            // Анализ еды
            int mealCount = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Count;
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            needs.FoodUrgency = mealCount < colonistCount * 3 ? 3 : (mealCount < colonistCount * 5 ? 2 : 1);

            // Анализ незавершенного строительства (if not already emergency)
            if (needs.ConstructionUrgency < 4)
            {
                int unfinishedCount = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).Count;
                needs.ConstructionUrgency = unfinishedCount > 5 ? 3 : (unfinishedCount > 0 ? 2 : 1);
            }

            // Анализ исследований
            needs.ResearchUrgency = DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Any(r => r.CanStartNow && !r.IsFinished) ? 2 : 1;

            // Анализ растений
            int plantCount = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant)
                .Count(t => t is Plant p && p.HarvestableNow);
            needs.PlantUrgency = plantCount > 50 ? 3 : (plantCount > 20 ? 2 : 1);

            // Анализ медицины - проверяем раненых/больных/кровотечения
            int injuredCount = map.mapPawns.FreeColonistsSpawned
                .Count(p => p.health.hediffSet.HasTendedAndHealingInjury() || 
                           p.health.hediffSet.HasNaturallyHealingInjury() ||
                           p.health.hediffSet.BleedRateTotal > 0.01f); // ✅ КРИТИЧНО: включаем кровотечения!
            needs.MedicalUrgency = injuredCount > 2 ? 3 : (injuredCount > 0 ? 2 : 1);

            // Анализ обороны - проверяем врагов на карте
            int enemyCount = map.mapPawns.AllPawns.Count(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed);
            needs.DefenseUrgency = enemyCount > 5 ? 3 : (enemyCount > 0 ? 2 : 1);

            if (WorkLogLevel >= SystemLogLevel.Verbose)
            {
                RimWatchLogger.Debug($"ColonyNeeds: Food={needs.FoodUrgency}, Construction={needs.ConstructionUrgency}, " +
                                   $"Research={needs.ResearchUrgency}, Plants={needs.PlantUrgency}, " +
                                   $"Medical={needs.MedicalUrgency}, Defense={needs.DefenseUrgency}");
            }

            return needs;
        }

        /// <summary>
        /// Назначает приоритеты работы для конкретного колониста.
        /// </summary>
        /// <returns>True if any priorities were changed</returns>
        private static bool AssignWorkPriorities(Pawn colonist, ColonyNeeds needs, AIStoryteller storyteller)
        {
            if (colonist.workSettings == null) return false;

            // Получаем все типы работ (including mods)
            List<WorkTypeDef> allWorkTypes = WorkPriorityMapper.GetAllModdedWorkTypes();

            int changedPriorities = 0;
            List<string> changes = new List<string>();
            
            foreach (WorkTypeDef workType in allWorkTypes)
            {
                // Проверяем, может ли колонист выполнять эту работу
                if (colonist.WorkTypeIsDisabled(workType)) continue;

                // Получаем текущий приоритет
                int oldPriority = colonist.workSettings.GetPriority(workType);

                // AI определяет приоритет на основе потребностей и личности рассказчика
                int priority = DeterminePriority(workType, colonist, needs, storyteller);
                
                // ✅ EMERGENCY MODE: Force construction priority = 1 if colonists sleeping outside
                if (needs.ConstructionUrgency >= 4 && workType == WorkTypeDefOf.Construction)
                {
                    priority = 1; // MAXIMUM PRIORITY
                    RimWatchLogger.WarningThrottledByKey(
                        key: $"emergency_force_construction_{colonist.ThingID}",
                        message: $"WorkAutomation: EMERGENCY - Forcing {colonist.LabelShort} Construction priority to 1",
                        cooldownTicks: 600);
                }
                
                // ✅ КРИТИЧЕСКОЕ ПРАВИЛО: Если есть раненые/кровотечения, ВСЕГДА нужен доктор!
                string workDefName = workType.defName.ToLower();
                if (workDefName.Contains("doctor") && needs.MedicalUrgency >= 2)
                {
                    int oldPriorityBeforeForce = priority;
                    // Принудительно повышаем приоритет Doctor при наличии раненых
                    priority = System.Math.Min(priority, 2); // Минимум priority=2 (высокий)
                    RimWatchLogger.Debug($"WorkAutomation: FORCE Doctor priority for {colonist.LabelShort}: {oldPriorityBeforeForce} → {priority} (Medical={needs.MedicalUrgency})");
                }

                // Manual Priorities: 1-4 (1=высший, 4=низший)
                // Simple Checkboxes: 0=disabled, 1=enabled
                // priority==0 означает "выключить работу"
                if (priority > 0 && priority <= 4)
                {
                    colonist.workSettings.SetPriority(workType, priority);
                    if (oldPriority != priority)
                    {
                        changedPriorities++;
                        string priorityChange = $"{workType.labelShort}: {oldPriority} → {priority}";
                        changes.Add(priorityChange);
                    }
                }
                else if (priority == 0)
                {
                    colonist.workSettings.Disable(workType);
                    if (oldPriority != 0)
                    {
                        changedPriorities++;
                        string priorityChange = $"{workType.labelShort}: {oldPriority} → DISABLED";
                        changes.Add(priorityChange);
                    }
                }
            }

            if (changedPriorities > 0 && WorkLogLevel != SystemLogLevel.Off)
            {
                if (WorkLogLevel == SystemLogLevel.Minimal)
                {
                    // Compact summary only
                    RimWatchLogger.Info($"👷 WorkAutomation: {colonist.LabelShort} - Changed {changedPriorities} priorities");
                }
                else
                {
                    RimWatchLogger.Info($"👷 WorkAutomation: {colonist.LabelShort} - Changed {changedPriorities} priorities:");
                    foreach (string change in changes)
                    {
                        RimWatchLogger.Info($"   • {change}");
                    }
                }
            }
            
            return changedPriorities > 0;
        }

        /// <summary>
        /// Определяет приоритет работы на основе потребностей колонии и AI рассказчика.
        /// </summary>
        private static int DeterminePriority(WorkTypeDef workType, Pawn colonist, ColonyNeeds needs, AIStoryteller storyteller)
        {
            // Базовый приоритет - 3 (средний)
            int basePriority = 3;

            // Если у рассказчика есть своя логика, используем её
            if (storyteller != null)
            {
                basePriority = storyteller.DetermineWorkPriority(workType, colonist, needs);
            }
            else
            {
                // Fallback: средний приоритет если нет рассказчика
                basePriority = 3;
            }

            // Корректируем на основе навыков и passion колониста
            int passion = GetPassionLevel(colonist, workType);
            int skillLevel = GetAverageSkillLevel(colonist, workType);
            
            // Повышаем приоритет если есть passion
            if (passion == 2) basePriority = System.Math.Max(1, basePriority - 2); // Major passion: -2 priority (higher)
            else if (passion == 1) basePriority = System.Math.Max(1, basePriority - 1); // Minor passion: -1 priority
            
            // Повышаем приоритет если высокий навык (10+)
            if (skillLevel >= 10) basePriority = System.Math.Max(1, basePriority - 1);
            
            // Понижаем приоритет если низкий навык (<3) и нет passion
            if (skillLevel < 3 && passion == 0) basePriority = System.Math.Min(4, basePriority + 1);

            return basePriority;
        }

        /// <summary>
        /// Определяет приоритет по умолчанию (если нет рассказчика).
        /// Uses WorkPriorityMapper for universal mod support.
        /// </summary>
        private static int DeterminDefaultPriority(WorkTypeDef workType, ColonyNeeds needs)
        {
            // Use WorkPriorityMapper for intelligent priority assignment
            return WorkPriorityMapper.GetBasePriorityForWork(workType, needs);
        }

        /// <summary>
        /// Определяет уровень passion колониста к типу работы.
        /// </summary>
        private static int GetPassionLevel(Pawn colonist, WorkTypeDef workType)
        {
            if (colonist.skills == null) return 0;

            // Находим связанные навыки
            int maxPassion = 0;
            foreach (SkillDef skill in workType.relevantSkills)
            {
                SkillRecord skillRecord = colonist.skills.GetSkill(skill);
                if (skillRecord == null) continue;

                // Проверяем passion (берем максимальный)
                if (skillRecord.passion == Passion.Major) maxPassion = System.Math.Max(maxPassion, 2);
                else if (skillRecord.passion == Passion.Minor) maxPassion = System.Math.Max(maxPassion, 1);
            }

            return maxPassion;
        }
        
        /// <summary>
        /// Получает средний уровень навыков для типа работы.
        /// </summary>
        private static int GetAverageSkillLevel(Pawn colonist, WorkTypeDef workType)
        {
            if (colonist.skills == null) return 0;
            if (workType.relevantSkills == null || workType.relevantSkills.Count == 0) return 0;

            int totalLevel = 0;
            int count = 0;
            
            foreach (SkillDef skill in workType.relevantSkills)
            {
                SkillRecord skillRecord = colonist.skills.GetSkill(skill);
                if (skillRecord != null)
                {
                    totalLevel += skillRecord.Level;
                    count++;
                }
            }

            return count > 0 ? totalLevel / count : 0;
        }
        
        /// <summary>
        /// v0.7.9: Updates colonist schedules with comprehensive daily routines.
        /// Includes: Work, Sleep, Rest, Food, Recreation, and Anything (flexible time).
        /// </summary>
        private static void UpdateColonistSchedules()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
            if (colonists.Count == 0) return;

            foreach (Pawn colonist in colonists)
            {
                if (colonist.Dead || colonist.Downed) continue;
                if (colonist.timetable == null) continue;

                // Get colonist traits and needs for smart scheduling
                TraitDef nightOwlTrait = DefDatabase<TraitDef>.GetNamedSilentFail("NightOwl");
                bool isNightOwl = nightOwlTrait != null && (colonist.story?.traits?.HasTrait(nightOwlTrait) ?? false);
                
                float ageYears = colonist.ageTracker?.AgeBiologicalYearsFloat ?? 20f;
                
                // Age-specific sleep needs
                bool isChild = ageYears < 13f;
                bool isElderly = ageYears > 60f;
                
                // Assign schedule based on colonist type
                if (isNightOwl)
                {
                    // Night shift: Sleep 7am-3pm, Work 4pm-2am, Anything else
                    SetSchedulePattern(colonist, 
                        sleepStart: 7, sleepEnd: 15,  // 8 hours sleep during day
                        workStart: 16, workEnd: 2,     // 10 hours work at night
                        mealTimes: new[] { 3, 15 });   // Wake-up meal + before work
                }
                else if (isChild)
                {
                    // Children: More sleep, less work, more play
                    SetSchedulePattern(colonist,
                        sleepStart: 21, sleepEnd: 7,   // 10 hours sleep
                        workStart: 9, workEnd: 16,     // 7 hours work
                        mealTimes: new[] { 8, 12, 18 }, // Breakfast, lunch, dinner
                        recreationHours: new[] { 17, 18, 19, 20 }); // 4 hours recreation
                }
                else if (isElderly)
                {
                    // Elderly: More rest, less intensive work
                    SetSchedulePattern(colonist,
                        sleepStart: 22, sleepEnd: 7,   // 9 hours sleep
                        workStart: 9, workEnd: 15,     // 6 hours work
                        mealTimes: new[] { 8, 12, 18 },
                        restHours: new[] { 14, 20 });  // Midday + evening rest
                }
                else
                {
                    // Standard adult schedule
                    SetSchedulePattern(colonist,
                        sleepStart: 23, sleepEnd: 6,   // 7 hours sleep
                        workStart: 8, workEnd: 18,     // 10 hours work
                        mealTimes: new[] { 7, 12, 19 }, // Breakfast, lunch, dinner
                        recreationHours: new[] { 20, 21 }); // 2 hours recreation
                }
            }
            
            RimWatchLogger.Debug($"WorkAutomation: Updated schedules for {colonists.Count} colonists");
        }
        
        /// <summary>
        /// Sets a schedule pattern for a colonist.
        /// </summary>
        private static void SetSchedulePattern(
            Pawn colonist,
            int sleepStart,
            int sleepEnd,
            int workStart,
            int workEnd,
            int[] mealTimes,
            int[] recreationHours = null,
            int[] restHours = null)
        {
            if (colonist.timetable == null) return;
            
            // Default: Anything (flexible)
            for (int hour = 0; hour < 24; hour++)
            {
                colonist.timetable.SetAssignment(hour, TimeAssignmentDefOf.Anything);
            }
            
            // Sleep hours
            SetHourRange(colonist, sleepStart, sleepEnd, TimeAssignmentDefOf.Sleep);
            
            // Work hours
            SetHourRange(colonist, workStart, workEnd, TimeAssignmentDefOf.Work);
            
            // Meal times (1 hour each)
            if (mealTimes != null)
            {
                foreach (int hour in mealTimes)
                {
                    colonist.timetable.SetAssignment(hour, TimeAssignmentDefOf.Anything);
                }
            }
            
            // Recreation hours
            if (recreationHours != null)
            {
                foreach (int hour in recreationHours)
                {
                    colonist.timetable.SetAssignment(hour, TimeAssignmentDefOf.Joy);
                }
            }
            
            // Rest hours
            if (restHours != null)
            {
                // RimWorld doesn't have a "Rest" assignment, use Anything
                foreach (int hour in restHours)
                {
                    colonist.timetable.SetAssignment(hour, TimeAssignmentDefOf.Anything);
                }
            }
        }
        
        /// <summary>
        /// Sets a time range with wraparound support (e.g., 23-6 wraps around midnight).
        /// </summary>
        private static void SetHourRange(Pawn colonist, int startHour, int endHour, TimeAssignmentDef assignment)
        {
            if (startHour <= endHour)
            {
                // Normal range (e.g., 8-18)
                for (int hour = startHour; hour < endHour; hour++)
                {
                    colonist.timetable.SetAssignment(hour, assignment);
                }
            }
            else
            {
                // Wraparound range (e.g., 23-6 = 23,0,1,2,3,4,5)
                for (int hour = startHour; hour < 24; hour++)
                {
                    colonist.timetable.SetAssignment(hour, assignment);
                }
                for (int hour = 0; hour < endHour; hour++)
                {
                    colonist.timetable.SetAssignment(hour, assignment);
                }
            }
        }
    }

    /// <summary>
    /// Структура для хранения текущих потребностей колонии.
    /// </summary>
    public class ColonyNeeds
    {
        /// <summary>
        /// Срочность еды (1-3, где 3 = критично).
        /// </summary>
        public int FoodUrgency { get; set; } = 1;

        /// <summary>
        /// Срочность строительства (1-3, где 3 = много незавершенных проектов).
        /// </summary>
        public int ConstructionUrgency { get; set; } = 1;

        /// <summary>
        /// Срочность исследований (1-3, где 3 = критично важно).
        /// </summary>
        public int ResearchUrgency { get; set; } = 1;

        /// <summary>
        /// Срочность сельского хозяйства (1-3, где 3 = много готовых растений).
        /// </summary>
        public int PlantUrgency { get; set; } = 1;

        /// <summary>
        /// Срочность медицины (1-3, где 3 = критично).
        /// </summary>
        public int MedicalUrgency { get; set; } = 1;

        /// <summary>
        /// Срочность обороны (1-3, где 3 = активная угроза).
        /// </summary>
        public int DefenseUrgency { get; set; } = 1;
    }
}

