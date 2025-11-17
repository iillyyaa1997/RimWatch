using RimWatch.Utils;
using RimWorld;
using System.Linq;
using Verse;

namespace RimWatch.Automation.ColonyDevelopment
{
    /// <summary>
    /// Этапы развития колонии.
    /// </summary>
    public enum DevelopmentStage
    {
        Emergency,      // День 1-3: Базовое выживание
        EarlyGame,      // День 4-30: Базовая инфраструктура
        MidGame,        // День 31-120: Расширение и специализация
        LateGame,       // День 121-365: Продвинутые технологии и оборона
        EndGame         // Год 2+: Корабль / строительство империи
    }
    
    /// <summary>
    /// Управляет определением текущего этапа развития колонии.
    /// </summary>
    public static class DevelopmentStageManager
    {
        /// <summary>
        /// Определяет текущий этап развития колонии.
        /// </summary>
        public static DevelopmentStage GetCurrentStage(Map map)
        {
            try
            {
                int daysPassed = GenDate.DaysPassed;
                int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
                float wealth = map.wealthWatcher.WealthTotal;
                
                // Emergency stage: Первые 3 дня ИЛИ критически низкие ресурсы
                if (daysPassed <= 3 || HasCriticalNeeds(map))
                {
                    return DevelopmentStage.Emergency;
                }
                
                // Early game: Базовое выживание достигнуто
                if (daysPassed <= 30 || colonistCount < 5 || wealth < 10000)
                {
                    return DevelopmentStage.EarlyGame;
                }
                
                // Mid game: Устоявшаяся колония
                if (daysPassed <= 120 || colonistCount < 10 || wealth < 50000)
                {
                    return DevelopmentStage.MidGame;
                }
                
                // Late game: Продвинутая колония
                if (daysPassed <= 365 || wealth < 200000)
                {
                    return DevelopmentStage.LateGame;
                }
                
                return DevelopmentStage.EndGame;
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("DevelopmentStageManager: Error in GetCurrentStage", ex);
                return DevelopmentStage.EarlyGame; // Fallback
            }
        }
        
        /// <summary>
        /// Проверяет наличие критических нужд, угрожающих выживанию.
        /// </summary>
        private static bool HasCriticalNeeds(Map map)
        {
            try
            {
                // Проверка на колонистов без крытых кроватей
                bool colonistsSleepingOutside = GetColonistsWithoutRoofedBeds(map).Count > 0;
                
                // Проверка на критически низкий запас еды
                bool foodCritical = GetFoodDays(map) < 2.0f;
                
                // Проверка на отсутствие обороны
                bool noDefense = !HasAnyDefense(map);
                
                return colonistsSleepingOutside || foodCritical || noDefense;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Получает список колонистов без крытых кроватей.
        /// </summary>
        private static System.Collections.Generic.List<Pawn> GetColonistsWithoutRoofedBeds(Map map)
        {
            try
            {
                return map.mapPawns.FreeColonistsSpawned
                    .Where(p => {
                        if (p.ownership?.OwnedBed == null)
                            return true; // Нет кровати
                        
                        Building_Bed bed = p.ownership.OwnedBed;
                        if (!bed.Position.Roofed(map))
                            return true; // Кровать без крыши
                        
                        Room room = bed.GetRoom();
                        if (room?.PsychologicallyOutdoors == true)
                            return true; // Психологически на улице
                        
                        return false;
                    })
                    .ToList();
            }
            catch
            {
                return new System.Collections.Generic.List<Pawn>();
            }
        }
        
        /// <summary>
        /// Подсчитывает на сколько дней хватит еды.
        /// </summary>
        private static float GetFoodDays(Map map)
        {
            try
            {
                // Подсчёт всей доступной еды
                float totalFood = 0f;
                
                foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
                {
                    if (thing.def.IsNutritionGivingIngestible)
                    {
                        totalFood += thing.GetStatValue(StatDefOf.Nutrition) * thing.stackCount;
                    }
                }
                
                // Расчёт потребления (1.6 питания на колониста в день)
                int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
                if (colonistCount == 0) return 999f; // Нет колонистов
                
                float dailyConsumption = colonistCount * 1.6f;
                float daysRemaining = totalFood / dailyConsumption;
                
                return daysRemaining;
            }
            catch
            {
                return 10f; // Fallback: предполагаем что еды достаточно
            }
        }
        
        /// <summary>
        /// Проверяет наличие хоть какой-то обороны.
        /// </summary>
        private static bool HasAnyDefense(Map map)
        {
            try
            {
                // Проверка на турели
                bool hasTurrets = map.listerBuildings.allBuildingsColonist
                    .Any(b => b.def.building?.IsTurret == true);
                
                // Проверка на вооружённых колонистов
                bool hasArmedColonists = map.mapPawns.FreeColonistsSpawned
                    .Any(p => p.equipment?.Primary != null);
                
                // Проверка на стены (хоть минимальная защита)
                bool hasWalls = map.listerBuildings.allBuildingsColonist
                    .Any(b => b.def.building != null && 
                             b.def.passability == Traversability.Impassable &&
                             b.def.fillPercent >= 0.75f);
                
                return hasTurrets || hasArmedColonists || hasWalls;
            }
            catch
            {
                return true; // Предполагаем что оборона есть при ошибке
            }
        }
        
        /// <summary>
        /// Получает описание текущего этапа развития.
        /// </summary>
        public static string GetStageDescription(DevelopmentStage stage)
        {
            switch (stage)
            {
                case DevelopmentStage.Emergency:
                    return "Emergency: Survival basics (Days 1-3)";
                case DevelopmentStage.EarlyGame:
                    return "Early Game: Basic infrastructure (Days 4-30)";
                case DevelopmentStage.MidGame:
                    return "Mid Game: Expansion & specialization (Days 31-120)";
                case DevelopmentStage.LateGame:
                    return "Late Game: Advanced tech & defense (Days 121-365)";
                case DevelopmentStage.EndGame:
                    return "End Game: Victory conditions (Year 2+)";
                default:
                    return "Unknown stage";
            }
        }
    }
}

