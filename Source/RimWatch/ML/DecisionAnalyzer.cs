using RimWatch.Core;
using RimWatch.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.ML
{
    /// <summary>
    /// Machine Learning system to analyze AI decision patterns and improve over time.
    /// Analyzes DecisionLogger data to identify successful and failed strategies.
    /// </summary>
    public static class DecisionAnalyzer
    {
        // Configuration Constants
        private const int MIN_SAMPLES_FOR_ANALYSIS = 50;
        private const int ANALYSIS_INTERVAL_TICKS = 60000; // Every game day
        private const float SUCCESS_THRESHOLD = 0.7f; // 70% success rate
        private const float FAILURE_THRESHOLD = 0.3f; // 30% success rate
        private const int MAX_PATTERN_HISTORY = 1000;
        private const int PATTERN_WINDOW_SIZE = 20; // Analyze last 20 decisions
        
        // Decision tracking
        private static Dictionary<string, DecisionPattern> _decisionPatterns = new Dictionary<string, DecisionPattern>();
        private static List<DecisionRecord> _recentDecisions = new List<DecisionRecord>();
        private static int _lastAnalysisTick = 0;
        
        // Analysis results
        private static Dictionary<string, StrategyEffectiveness> _strategyEffectiveness = new Dictionary<string, StrategyEffectiveness>();

        /// <summary>
        /// Record a decision made by the AI.
        /// </summary>
        public static void RecordDecision(string category, string action, Dictionary<string, float> context, bool success)
        {
            var record = new DecisionRecord
            {
                Category = category,
                Action = action,
                Context = context,
                Success = success,
                Timestamp = Find.TickManager.TicksGame
            };

            _recentDecisions.Add(record);

            // Limit history size
            if (_recentDecisions.Count > MAX_PATTERN_HISTORY)
            {
                _recentDecisions.RemoveAt(0);
            }

            // Track pattern
            string patternKey = $"{category}:{action}";
            if (!_decisionPatterns.ContainsKey(patternKey))
            {
                _decisionPatterns[patternKey] = new DecisionPattern
                {
                    Category = category,
                    Action = action,
                    TotalAttempts = 0,
                    SuccessfulAttempts = 0
                };
            }

            var pattern = _decisionPatterns[patternKey];
            pattern.TotalAttempts++;
            if (success)
            {
                pattern.SuccessfulAttempts++;
            }
            pattern.LastAttemptTick = Find.TickManager.TicksGame;

            RimWatchLogger.Debug($"DecisionAnalyzer: Recorded {category}/{action} (Success: {success})");
        }

        /// <summary>
        /// Analyze decision patterns periodically.
        /// </summary>
        public static void Tick()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            if (currentTick - _lastAnalysisTick < ANALYSIS_INTERVAL_TICKS)
                return;

            _lastAnalysisTick = currentTick;

            if (_recentDecisions.Count < MIN_SAMPLES_FOR_ANALYSIS)
            {
                RimWatchLogger.Debug($"DecisionAnalyzer: Insufficient data for analysis ({_recentDecisions.Count}/{MIN_SAMPLES_FOR_ANALYSIS})");
                return;
            }

            AnalyzePatterns();
            IdentifySuccessfulStrategies();
            IdentifyFailedStrategies();
            CleanupOldData();

            RimWatchLogger.Info($"DecisionAnalyzer: Analysis complete. {_decisionPatterns.Count} patterns tracked.");
        }

        /// <summary>
        /// Analyze decision patterns and calculate effectiveness.
        /// </summary>
        private static void AnalyzePatterns()
        {
            foreach (var pattern in _decisionPatterns.Values)
            {
                if (pattern.TotalAttempts == 0) continue;

                float successRate = (float)pattern.SuccessfulAttempts / pattern.TotalAttempts;
                pattern.SuccessRate = successRate;

                // Calculate confidence based on sample size
                pattern.Confidence = CalculateConfidence(pattern.TotalAttempts);
            }
        }

        /// <summary>
        /// Calculate statistical confidence based on sample size.
        /// </summary>
        private static float CalculateConfidence(int sampleSize)
        {
            // Simple confidence formula: more samples = higher confidence
            // Approaches 1.0 as sample size increases
            const int FULL_CONFIDENCE_SAMPLES = 100;
            return Math.Min(1.0f, (float)sampleSize / FULL_CONFIDENCE_SAMPLES);
        }

        /// <summary>
        /// Identify strategies that work well.
        /// </summary>
        private static void IdentifySuccessfulStrategies()
        {
            var successfulPatterns = _decisionPatterns.Values
                .Where(p => p.SuccessRate >= SUCCESS_THRESHOLD && p.Confidence >= 0.5f)
                .OrderByDescending(p => p.SuccessRate * p.Confidence)
                .Take(10)
                .ToList();

            foreach (var pattern in successfulPatterns)
            {
                string key = $"{pattern.Category}:{pattern.Action}";
                
                if (!_strategyEffectiveness.ContainsKey(key))
                {
                    _strategyEffectiveness[key] = new StrategyEffectiveness
                    {
                        Category = pattern.Category,
                        Action = pattern.Action,
                        Effectiveness = pattern.SuccessRate,
                        Confidence = pattern.Confidence,
                        Recommendation = StrategyRecommendation.Recommended
                    };
                }
                else
                {
                    _strategyEffectiveness[key].Effectiveness = pattern.SuccessRate;
                    _strategyEffectiveness[key].Confidence = pattern.Confidence;
                }

                RimWatchLogger.Debug($"DecisionAnalyzer: ✅ Successful strategy: {pattern.Category}/{pattern.Action} ({pattern.SuccessRate:P0} success, {pattern.Confidence:P0} confidence)");
            }
        }

        /// <summary>
        /// Identify strategies that don't work well.
        /// </summary>
        private static void IdentifyFailedStrategies()
        {
            var failedPatterns = _decisionPatterns.Values
                .Where(p => p.SuccessRate <= FAILURE_THRESHOLD && p.Confidence >= 0.5f)
                .OrderBy(p => p.SuccessRate)
                .Take(10)
                .ToList();

            foreach (var pattern in failedPatterns)
            {
                string key = $"{pattern.Category}:{pattern.Action}";
                
                if (!_strategyEffectiveness.ContainsKey(key))
                {
                    _strategyEffectiveness[key] = new StrategyEffectiveness
                    {
                        Category = pattern.Category,
                        Action = pattern.Action,
                        Effectiveness = pattern.SuccessRate,
                        Confidence = pattern.Confidence,
                        Recommendation = StrategyRecommendation.NotRecommended
                    };
                }
                else
                {
                    _strategyEffectiveness[key].Effectiveness = pattern.SuccessRate;
                    _strategyEffectiveness[key].Confidence = pattern.Confidence;
                    _strategyEffectiveness[key].Recommendation = StrategyRecommendation.NotRecommended;
                }

                RimWatchLogger.Debug($"DecisionAnalyzer: ❌ Failed strategy: {pattern.Category}/{pattern.Action} ({pattern.SuccessRate:P0} success, {pattern.Confidence:P0} confidence)");
            }
        }

        /// <summary>
        /// Get effectiveness rating for a specific action.
        /// </summary>
        public static float GetActionEffectiveness(string category, string action)
        {
            string key = $"{category}:{action}";
            
            if (_strategyEffectiveness.TryGetValue(key, out StrategyEffectiveness strategy))
            {
                // Return weighted effectiveness (effectiveness * confidence)
                return strategy.Effectiveness * strategy.Confidence;
            }

            // Return neutral effectiveness if no data
            return 0.5f;
        }

        /// <summary>
        /// Check if action is recommended based on historical data.
        /// </summary>
        public static bool IsActionRecommended(string category, string action)
        {
            string key = $"{category}:{action}";
            
            if (_strategyEffectiveness.TryGetValue(key, out StrategyEffectiveness strategy))
            {
                return strategy.Recommendation == StrategyRecommendation.Recommended;
            }

            // If no data, allow action (neutral stance)
            return true;
        }

        /// <summary>
        /// Get analysis summary for debugging.
        /// </summary>
        public static AnalysisSummary GetSummary()
        {
            int totalDecisions = _recentDecisions.Count;
            int successfulDecisions = _recentDecisions.Count(d => d.Success);
            float overallSuccessRate = totalDecisions > 0 ? (float)successfulDecisions / totalDecisions : 0f;

            return new AnalysisSummary
            {
                TotalDecisions = totalDecisions,
                SuccessfulDecisions = successfulDecisions,
                OverallSuccessRate = overallSuccessRate,
                TrackedPatterns = _decisionPatterns.Count,
                SuccessfulStrategies = _strategyEffectiveness.Count(s => s.Value.Recommendation == StrategyRecommendation.Recommended),
                FailedStrategies = _strategyEffectiveness.Count(s => s.Value.Recommendation == StrategyRecommendation.NotRecommended)
            };
        }

        /// <summary>
        /// Get top performing strategies.
        /// </summary>
        public static List<StrategyEffectiveness> GetTopStrategies(int count = 10)
        {
            return _strategyEffectiveness.Values
                .OrderByDescending(s => s.Effectiveness * s.Confidence)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Get worst performing strategies.
        /// </summary>
        public static List<StrategyEffectiveness> GetWorstStrategies(int count = 10)
        {
            return _strategyEffectiveness.Values
                .Where(s => s.Confidence >= 0.3f) // Only show strategies with enough data
                .OrderBy(s => s.Effectiveness * s.Confidence)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Clean up old decision records.
        /// </summary>
        private static void CleanupOldData()
        {
            const int MAX_AGE_TICKS = 600000; // 10 game days
            int currentTick = Find.TickManager.TicksGame;

            // Remove old decisions
            _recentDecisions.RemoveAll(d => currentTick - d.Timestamp > MAX_AGE_TICKS);

            // Remove inactive patterns
            var inactivePatterns = _decisionPatterns
                .Where(kvp => currentTick - kvp.Value.LastAttemptTick > MAX_AGE_TICKS)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in inactivePatterns)
            {
                _decisionPatterns.Remove(key);
                _strategyEffectiveness.Remove(key);
            }

            if (inactivePatterns.Count > 0)
            {
                RimWatchLogger.Debug($"DecisionAnalyzer: Cleaned up {inactivePatterns.Count} inactive patterns");
            }
        }

        /// <summary>
        /// Export analysis data for external review.
        /// </summary>
        public static string ExportData()
        {
            var summary = GetSummary();
            var topStrategies = GetTopStrategies(5);
            var worstStrategies = GetWorstStrategies(5);

            string export = "=== RimWatch Decision Analysis ===\n\n";
            export += $"Total Decisions: {summary.TotalDecisions}\n";
            export += $"Successful: {summary.SuccessfulDecisions} ({summary.OverallSuccessRate:P1})\n";
            export += $"Tracked Patterns: {summary.TrackedPatterns}\n";
            export += $"Successful Strategies: {summary.SuccessfulStrategies}\n";
            export += $"Failed Strategies: {summary.FailedStrategies}\n\n";

            export += "=== Top 5 Strategies ===\n";
            foreach (var strategy in topStrategies)
            {
                export += $"✅ {strategy.Category}/{strategy.Action}: {strategy.Effectiveness:P1} (Confidence: {strategy.Confidence:P0})\n";
            }

            export += "\n=== Worst 5 Strategies ===\n";
            foreach (var strategy in worstStrategies)
            {
                export += $"❌ {strategy.Category}/{strategy.Action}: {strategy.Effectiveness:P1} (Confidence: {strategy.Confidence:P0})\n";
            }

            return export;
        }
    }

    /// <summary>
    /// Decision record for tracking.
    /// </summary>
    public class DecisionRecord
    {
        public string Category { get; set; } = "";
        public string Action { get; set; } = "";
        public Dictionary<string, float> Context { get; set; } = new Dictionary<string, float>();
        public bool Success { get; set; }
        public int Timestamp { get; set; }
    }

    /// <summary>
    /// Decision pattern tracking.
    /// </summary>
    public class DecisionPattern
    {
        public string Category { get; set; } = "";
        public string Action { get; set; } = "";
        public int TotalAttempts { get; set; }
        public int SuccessfulAttempts { get; set; }
        public float SuccessRate { get; set; }
        public float Confidence { get; set; }
        public int LastAttemptTick { get; set; }
    }

    /// <summary>
    /// Strategy effectiveness data.
    /// </summary>
    public class StrategyEffectiveness
    {
        public string Category { get; set; } = "";
        public string Action { get; set; } = "";
        public float Effectiveness { get; set; }
        public float Confidence { get; set; }
        public StrategyRecommendation Recommendation { get; set; }
    }

    /// <summary>
    /// Strategy recommendation enum.
    /// </summary>
    public enum StrategyRecommendation
    {
        Recommended,
        Neutral,
        NotRecommended
    }

    /// <summary>
    /// Analysis summary data.
    /// </summary>
    public class AnalysisSummary
    {
        public int TotalDecisions { get; set; }
        public int SuccessfulDecisions { get; set; }
        public float OverallSuccessRate { get; set; }
        public int TrackedPatterns { get; set; }
        public int SuccessfulStrategies { get; set; }
        public int FailedStrategies { get; set; }
    }
}

