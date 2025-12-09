using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.ML
{
    /// <summary>
    /// Predictive analytics system for anticipating colony needs.
    /// Predicts food shortages, raids, resource depletion, and other critical events.
    /// </summary>
    public static class ColonyPredictor
    {
        // Prediction Configuration
        private const int PREDICTION_INTERVAL_TICKS = 2500; // Every ~1 hour game time
        private const int HISTORY_SIZE = 100; // Track last 100 data points
        private const int FOOD_PREDICTION_DAYS = 3; // Predict 3 days ahead
        private const int RESOURCE_PREDICTION_DAYS = 5; // Predict 5 days ahead
        private const float CRITICAL_THRESHOLD = 0.2f; // 20% remaining = critical
        private const float WARNING_THRESHOLD = 0.4f; // 40% remaining = warning
        private const int TICKS_PER_DAY = 60000;
        
        // Historical data
        private static Queue<ResourceSnapshot> _foodHistory = new Queue<ResourceSnapshot>();
        private static Queue<ResourceSnapshot> _medicineHistory = new Queue<ResourceSnapshot>();
        private static Queue<ResourceSnapshot> _materialHistory = new Queue<ResourceSnapshot>();
        private static int _lastPredictionTick = 0;
        
        // Current predictions
        private static PredictionResult _foodPrediction = null;
        private static PredictionResult _medicinePrediction = null;
        private static PredictionResult _materialPrediction = null;
        private static ThreatPrediction _threatPrediction = null;

        /// <summary>
        /// Update predictions periodically.
        /// </summary>
        public static void Tick(Map map)
        {
            if (map == null) return;

            int currentTick = Find.TickManager.TicksGame;
            
            if (currentTick - _lastPredictionTick < PREDICTION_INTERVAL_TICKS)
                return;

            _lastPredictionTick = currentTick;

            // Collect current state
            CollectSnapshot(map);

            // Generate predictions
            _foodPrediction = PredictFood(map);
            _medicinePrediction = PredictMedicine(map);
            _materialPrediction = PredictMaterials(map);
            _threatPrediction = PredictThreats(map);

            // Log critical predictions
            LogCriticalPredictions();
        }

        /// <summary>
        /// Collect resource snapshot for historical tracking.
        /// </summary>
        private static void CollectSnapshot(Map map)
        {
            int currentTick = Find.TickManager.TicksGame;

            // Food snapshot
            int foodCount = map.resourceCounter.GetCount(ThingDefOf.MealSimple) +
                           map.resourceCounter.GetCount(ThingDefOf.MealFine) +
                           map.resourceCounter.GetCount(ThingDefOf.RawPotatoes);

            AddSnapshot(_foodHistory, new ResourceSnapshot
            {
                Timestamp = currentTick,
                Amount = foodCount
            });

            // Medicine snapshot
            int medicineCount = map.resourceCounter.GetCount(ThingDefOf.MedicineIndustrial) +
                               map.resourceCounter.GetCount(ThingDefOf.MedicineHerbal);

            AddSnapshot(_medicineHistory, new ResourceSnapshot
            {
                Timestamp = currentTick,
                Amount = medicineCount
            });

            // Materials snapshot (steel + wood)
            int materialsCount = map.resourceCounter.GetCount(ThingDefOf.Steel) +
                                map.resourceCounter.GetCount(ThingDefOf.WoodLog);

            AddSnapshot(_materialHistory, new ResourceSnapshot
            {
                Timestamp = currentTick,
                Amount = materialsCount
            });
        }

        /// <summary>
        /// Add snapshot to history queue.
        /// </summary>
        private static void AddSnapshot(Queue<ResourceSnapshot> history, ResourceSnapshot snapshot)
        {
            history.Enqueue(snapshot);
            
            if (history.Count > HISTORY_SIZE)
            {
                history.Dequeue();
            }
        }

        /// <summary>
        /// Predict food situation.
        /// </summary>
        private static PredictionResult PredictFood(Map map)
        {
            if (_foodHistory.Count < 2)
            {
                return new PredictionResult
                {
                    ResourceType = "Food",
                    CurrentAmount = _foodHistory.LastOrDefault()?.Amount ?? 0,
                    Trend = ResourceTrend.Stable,
                    DaysUntilDepletion = -1,
                    Severity = PredictionSeverity.Unknown
                };
            }

            var current = _foodHistory.Last();
            var old = _foodHistory.First();
            
            // Calculate consumption rate
            int timeDiff = current.Timestamp - old.Timestamp;
            int amountDiff = old.Amount - current.Amount; // Positive = consuming
            float consumptionPerTick = timeDiff > 0 ? (float)amountDiff / timeDiff : 0f;

            // Predict depletion
            int daysUntilDepletion = -1;
            if (consumptionPerTick > 0)
            {
                int ticksUntilDepletion = (int)(current.Amount / consumptionPerTick);
                daysUntilDepletion = ticksUntilDepletion / TICKS_PER_DAY;
            }

            // Determine severity
            var severity = DetermineSeverity(daysUntilDepletion, FOOD_PREDICTION_DAYS);

            // Determine trend
            var trend = consumptionPerTick > 0.1f ? ResourceTrend.Decreasing :
                       consumptionPerTick < -0.1f ? ResourceTrend.Increasing :
                       ResourceTrend.Stable;

            return new PredictionResult
            {
                ResourceType = "Food",
                CurrentAmount = current.Amount,
                ConsumptionRate = consumptionPerTick * TICKS_PER_DAY, // Per day
                Trend = trend,
                DaysUntilDepletion = daysUntilDepletion,
                Severity = severity
            };
        }

        /// <summary>
        /// Predict medicine situation.
        /// </summary>
        private static PredictionResult PredictMedicine(Map map)
        {
            if (_medicineHistory.Count < 2)
            {
                return CreateUnknownPrediction("Medicine", _medicineHistory.LastOrDefault()?.Amount ?? 0);
            }

            return PredictResource(_medicineHistory, "Medicine", RESOURCE_PREDICTION_DAYS);
        }

        /// <summary>
        /// Predict materials situation.
        /// </summary>
        private static PredictionResult PredictMaterials(Map map)
        {
            if (_materialHistory.Count < 2)
            {
                return CreateUnknownPrediction("Materials", _materialHistory.LastOrDefault()?.Amount ?? 0);
            }

            return PredictResource(_materialHistory, "Materials", RESOURCE_PREDICTION_DAYS);
        }

        /// <summary>
        /// Generic resource prediction.
        /// </summary>
        private static PredictionResult PredictResource(Queue<ResourceSnapshot> history, string resourceType, int predictionDays)
        {
            var current = history.Last();
            var old = history.First();
            
            int timeDiff = current.Timestamp - old.Timestamp;
            int amountDiff = old.Amount - current.Amount;
            float consumptionPerTick = timeDiff > 0 ? (float)amountDiff / timeDiff : 0f;

            int daysUntilDepletion = -1;
            if (consumptionPerTick > 0)
            {
                int ticksUntilDepletion = (int)(current.Amount / consumptionPerTick);
                daysUntilDepletion = ticksUntilDepletion / TICKS_PER_DAY;
            }

            var severity = DetermineSeverity(daysUntilDepletion, predictionDays);
            
            var trend = consumptionPerTick > 0.05f ? ResourceTrend.Decreasing :
                       consumptionPerTick < -0.05f ? ResourceTrend.Increasing :
                       ResourceTrend.Stable;

            return new PredictionResult
            {
                ResourceType = resourceType,
                CurrentAmount = current.Amount,
                ConsumptionRate = consumptionPerTick * TICKS_PER_DAY,
                Trend = trend,
                DaysUntilDepletion = daysUntilDepletion,
                Severity = severity
            };
        }

        /// <summary>
        /// Create unknown prediction result.
        /// </summary>
        private static PredictionResult CreateUnknownPrediction(string resourceType, int currentAmount)
        {
            return new PredictionResult
            {
                ResourceType = resourceType,
                CurrentAmount = currentAmount,
                Trend = ResourceTrend.Stable,
                DaysUntilDepletion = -1,
                Severity = PredictionSeverity.Unknown
            };
        }

        /// <summary>
        /// Determine prediction severity based on days until depletion.
        /// </summary>
        private static PredictionSeverity DetermineSeverity(int daysUntilDepletion, int predictionThreshold)
        {
            if (daysUntilDepletion < 0)
                return PredictionSeverity.Safe; // Increasing or stable

            if (daysUntilDepletion <= 1)
                return PredictionSeverity.Critical;
            else if (daysUntilDepletion <= predictionThreshold / 2)
                return PredictionSeverity.Warning;
            else if (daysUntilDepletion <= predictionThreshold)
                return PredictionSeverity.Caution;
            else
                return PredictionSeverity.Safe;
        }

        /// <summary>
        /// Predict incoming threats (raids, etc).
        /// </summary>
        private static ThreatPrediction PredictThreats(Map map)
        {
            // Calculate colony wealth as raid predictor
            float colonyWealth = map.wealthWatcher.WealthTotal;
            float avgRaidInterval = CalculateAverageRaidInterval();
            
            const float BASE_RAID_INTERVAL_DAYS = 15f;
            const float WEALTH_MULTIPLIER = 0.0001f;
            
            // Higher wealth = more frequent raids
            float expectedIntervalDays = BASE_RAID_INTERVAL_DAYS - (colonyWealth * WEALTH_MULTIPLIER);
            expectedIntervalDays = Math.Max(3f, expectedIntervalDays); // Min 3 days between raids

            var severity = expectedIntervalDays <= 5f ? PredictionSeverity.Warning : PredictionSeverity.Safe;

            return new ThreatPrediction
            {
                ColonyWealth = colonyWealth,
                ExpectedRaidIntervalDays = expectedIntervalDays,
                Severity = severity,
                Recommendation = severity == PredictionSeverity.Warning ? "Укрепите оборону" : "Оборона стабильна"
            };
        }

        /// <summary>
        /// Calculate average raid interval from story tracker.
        /// </summary>
        private static float CalculateAverageRaidInterval()
        {
            // Simplified - would need StoryTracker integration for real data
            const float DEFAULT_INTERVAL = 15f;
            return DEFAULT_INTERVAL;
        }

        /// <summary>
        /// Log critical predictions.
        /// </summary>
        private static void LogCriticalPredictions()
        {
            if (_foodPrediction?.Severity == PredictionSeverity.Critical)
            {
                RimWatchLogger.Warning($"ColonyPredictor: ⚠️ CRITICAL - Food will run out in {_foodPrediction.DaysUntilDepletion} days!");
            }

            if (_medicinePrediction?.Severity == PredictionSeverity.Critical)
            {
                RimWatchLogger.Warning($"ColonyPredictor: ⚠️ CRITICAL - Medicine will run out in {_medicinePrediction.DaysUntilDepletion} days!");
            }

            if (_materialPrediction?.Severity == PredictionSeverity.Warning)
            {
                RimWatchLogger.Info($"ColonyPredictor: ⚠️ Materials running low ({_materialPrediction.CurrentAmount} remaining)");
            }
        }

        /// <summary>
        /// Get food prediction.
        /// </summary>
        public static PredictionResult GetFoodPrediction()
        {
            return _foodPrediction ?? CreateUnknownPrediction("Food", 0);
        }

        /// <summary>
        /// Get medicine prediction.
        /// </summary>
        public static PredictionResult GetMedicinePrediction()
        {
            return _medicinePrediction ?? CreateUnknownPrediction("Medicine", 0);
        }

        /// <summary>
        /// Get materials prediction.
        /// </summary>
        public static PredictionResult GetMaterialsPrediction()
        {
            return _materialPrediction ?? CreateUnknownPrediction("Materials", 0);
        }

        /// <summary>
        /// Get threat prediction.
        /// </summary>
        public static ThreatPrediction GetThreatPrediction()
        {
            return _threatPrediction ?? new ThreatPrediction
            {
                ColonyWealth = 0f,
                ExpectedRaidIntervalDays = 15f,
                Severity = PredictionSeverity.Unknown,
                Recommendation = "Недостаточно данных"
            };
        }

        /// <summary>
        /// Get all critical predictions.
        /// </summary>
        public static List<PredictionResult> GetCriticalPredictions()
        {
            var critical = new List<PredictionResult>();

            if (_foodPrediction?.Severity >= PredictionSeverity.Warning)
                critical.Add(_foodPrediction);

            if (_medicinePrediction?.Severity >= PredictionSeverity.Warning)
                critical.Add(_medicinePrediction);

            if (_materialPrediction?.Severity >= PredictionSeverity.Warning)
                critical.Add(_materialPrediction);

            return critical;
        }
    }

    /// <summary>
    /// Resource snapshot for historical tracking.
    /// </summary>
    public class ResourceSnapshot
    {
        public int Timestamp { get; set; }
        public int Amount { get; set; }
    }

    /// <summary>
    /// Prediction result.
    /// </summary>
    public class PredictionResult
    {
        public string ResourceType { get; set; } = "";
        public int CurrentAmount { get; set; }
        public float ConsumptionRate { get; set; }
        public ResourceTrend Trend { get; set; }
        public int DaysUntilDepletion { get; set; }
        public PredictionSeverity Severity { get; set; }
    }

    /// <summary>
    /// Threat prediction.
    /// </summary>
    public class ThreatPrediction
    {
        public float ColonyWealth { get; set; }
        public float ExpectedRaidIntervalDays { get; set; }
        public PredictionSeverity Severity { get; set; }
        public string Recommendation { get; set; } = "";
    }

    /// <summary>
    /// Resource trend enum.
    /// </summary>
    public enum ResourceTrend
    {
        Increasing,
        Stable,
        Decreasing
    }

    /// <summary>
    /// Prediction severity enum.
    /// </summary>
    public enum PredictionSeverity
    {
        Unknown,
        Safe,
        Caution,
        Warning,
        Critical
    }
}

