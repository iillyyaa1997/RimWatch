using RimWatch.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Core
{
    /// <summary>
    /// Performance monitoring and dynamic interval optimization.
    /// Tracks execution times and automatically adjusts update intervals to maintain <5% TPS overhead.
    /// v0.9.13: Performance interval optimization system.
    /// </summary>
    public static class PerformanceMonitor
    {
        // Configuration Constants
        private const float TARGET_OVERHEAD_PERCENT = 5.0f; // Target: <5% TPS overhead
        private const int MEASUREMENT_WINDOW = 60000; // 1 minute window (60000 ticks)
        private const int MIN_SAMPLES = 10; // Minimum samples before optimization
        private const int OPTIMIZATION_INTERVAL = 30000; // Optimize every 30 seconds
        
        // Interval bounds (in ticks)
        private const int MIN_INTERVAL = 60; // 1 second minimum
        private const int MAX_INTERVAL = 18000; // 5 minutes maximum
        
        // Tracking state
        private static Dictionary<string, SystemPerformance> _systemPerformance = new Dictionary<string, SystemPerformance>();
        private static int _lastOptimizationTick = 0;
        private static int _ticksInCurrentSecond = 0;
        private static float _currentTPS = 60.0f; // Default TPS
        
        /// <summary>
        /// Records execution time for a system.
        /// </summary>
        public static void RecordExecution(string systemName, long executionMilliseconds, int currentInterval)
        {
            if (!_systemPerformance.ContainsKey(systemName))
            {
                _systemPerformance[systemName] = new SystemPerformance
                {
                    SystemName = systemName,
                    CurrentInterval = currentInterval,
                    RecommendedInterval = currentInterval,
                    ExecutionSamples = new List<long>(),
                    LastExecutionTime = 0
                };
            }
            
            var perf = _systemPerformance[systemName];
            perf.ExecutionSamples.Add(executionMilliseconds);
            perf.LastExecutionTime = executionMilliseconds;
            perf.TotalExecutions++;
            
            // Keep only recent samples (last minute)
            int currentTick = Find.TickManager.TicksGame;
            if (perf.ExecutionSamples.Count > 100)
            {
                perf.ExecutionSamples.RemoveAt(0);
            }
        }
        
        /// <summary>
        /// Updates TPS measurement.
        /// </summary>
        public static void Tick()
        {
            _ticksInCurrentSecond++;
            
            int currentTick = Find.TickManager.TicksGame;
            
            // Calculate TPS every second
            if (_ticksInCurrentSecond >= 60)
            {
                _currentTPS = _ticksInCurrentSecond / 1.0f;
                _ticksInCurrentSecond = 0;
            }
            
            // Optimize intervals periodically
            if (currentTick - _lastOptimizationTick >= OPTIMIZATION_INTERVAL)
            {
                _lastOptimizationTick = currentTick;
                OptimizeAllIntervals();
            }
        }
        
        /// <summary>
        /// Optimizes update intervals for all systems.
        /// </summary>
        private static void OptimizeAllIntervals()
        {
            try
            {
                // Calculate total overhead
                float totalOverheadPercent = CalculateTotalOverhead();
                
                RimWatchLogger.Debug($"PerformanceMonitor: Total overhead: {totalOverheadPercent:F2}% (target: {TARGET_OVERHEAD_PERCENT}%)");
                
                // If overhead is too high, increase intervals
                if (totalOverheadPercent > TARGET_OVERHEAD_PERCENT)
                {
                    float scaleFactor = totalOverheadPercent / TARGET_OVERHEAD_PERCENT;
                    IncreaseIntervals(scaleFactor);
                }
                // If overhead is low, we can decrease intervals for better responsiveness
                else if (totalOverheadPercent < TARGET_OVERHEAD_PERCENT * 0.5f)
                {
                    float scaleFactor = 0.9f; // Decrease by 10%
                    DecreaseIntervals(scaleFactor);
                }
                
                // Log recommendations
                LogPerformanceReport();
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"PerformanceMonitor: Error in OptimizeAllIntervals: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Calculates total overhead percentage.
        /// </summary>
        private static float CalculateTotalOverhead()
        {
            float totalTimePerSecond = 0f;
            
            foreach (var kvp in _systemPerformance)
            {
                var perf = kvp.Value;
                
                if (perf.ExecutionSamples.Count < MIN_SAMPLES)
                    continue;
                
                // Average execution time
                float avgExecutionMs = (float)perf.ExecutionSamples.Average();
                
                // How many times per second does this system run?
                float executionsPerSecond = 60.0f / perf.CurrentInterval;
                
                // Total time spent per second
                totalTimePerSecond += avgExecutionMs * executionsPerSecond;
            }
            
            // Convert to percentage of 1 second (1000ms = 100%)
            float overheadPercent = (totalTimePerSecond / 1000.0f) * 100.0f;
            
            return overheadPercent;
        }
        
        /// <summary>
        /// Increases intervals to reduce overhead.
        /// </summary>
        private static void IncreaseIntervals(float scaleFactor)
        {
            RimWatchLogger.Info($"PerformanceMonitor: Increasing intervals by {scaleFactor:F2}x to reduce overhead");
            
            foreach (var kvp in _systemPerformance)
            {
                var perf = kvp.Value;
                
                // Increase interval
                int newInterval = (int)(perf.CurrentInterval * scaleFactor);
                newInterval = UnityEngine.Mathf.Min(newInterval, MAX_INTERVAL);
                
                if (newInterval != perf.CurrentInterval)
                {
                    perf.RecommendedInterval = newInterval;
                    RimWatchLogger.Debug($"  {perf.SystemName}: {perf.CurrentInterval} → {newInterval} ticks");
                }
            }
        }
        
        /// <summary>
        /// Decreases intervals for better responsiveness.
        /// </summary>
        private static void DecreaseIntervals(float scaleFactor)
        {
            RimWatchLogger.Debug($"PerformanceMonitor: Decreasing intervals by {scaleFactor:F2}x for better responsiveness");
            
            foreach (var kvp in _systemPerformance)
            {
                var perf = kvp.Value;
                
                // Decrease interval
                int newInterval = (int)(perf.CurrentInterval * scaleFactor);
                newInterval = UnityEngine.Mathf.Max(newInterval, MIN_INTERVAL);
                
                if (newInterval != perf.CurrentInterval)
                {
                    perf.RecommendedInterval = newInterval;
                }
            }
        }
        
        /// <summary>
        /// Gets recommended interval for a system.
        /// </summary>
        public static int GetRecommendedInterval(string systemName, int defaultInterval)
        {
            if (_systemPerformance.ContainsKey(systemName))
            {
                return _systemPerformance[systemName].RecommendedInterval;
            }
            
            return defaultInterval;
        }
        
        /// <summary>
        /// Logs performance report.
        /// </summary>
        private static void LogPerformanceReport()
        {
            if (_systemPerformance.Count == 0)
                return;
            
            RimWatchLogger.Info("=== PERFORMANCE REPORT ===");
            RimWatchLogger.Info($"Current TPS: {_currentTPS:F1}");
            RimWatchLogger.Info($"Total Overhead: {CalculateTotalOverhead():F2}%");
            
            foreach (var kvp in _systemPerformance.OrderByDescending(x => x.Value.LastExecutionTime))
            {
                var perf = kvp.Value;
                
                if (perf.ExecutionSamples.Count < MIN_SAMPLES)
                    continue;
                
                float avgMs = (float)perf.ExecutionSamples.Average();
                float maxMs = (float)perf.ExecutionSamples.Max();
                float executionsPerSec = 60.0f / perf.CurrentInterval;
                float overheadPercent = (avgMs * executionsPerSec / 1000.0f) * 100.0f;
                
                RimWatchLogger.Info($"  {perf.SystemName}:");
                RimWatchLogger.Info($"    Interval: {perf.CurrentInterval} ticks (→ {perf.RecommendedInterval})");
                RimWatchLogger.Info($"    Avg: {avgMs:F2}ms, Max: {maxMs:F2}ms");
                RimWatchLogger.Info($"    Overhead: {overheadPercent:F2}%");
                RimWatchLogger.Info($"    Executions: {perf.TotalExecutions}");
            }
            
            RimWatchLogger.Info("=========================");
        }
        
        /// <summary>
        /// Gets performance statistics.
        /// </summary>
        public static Dictionary<string, SystemPerformance> GetStatistics()
        {
            return new Dictionary<string, SystemPerformance>(_systemPerformance);
        }
        
        /// <summary>
        /// Resets all statistics.
        /// </summary>
        public static void Reset()
        {
            _systemPerformance.Clear();
            _lastOptimizationTick = 0;
            _ticksInCurrentSecond = 0;
            _currentTPS = 60.0f;
            
            RimWatchLogger.Info("PerformanceMonitor: Reset all statistics");
        }
    }
    
    /// <summary>
    /// Performance tracking data for a system.
    /// </summary>
    public class SystemPerformance
    {
        public string SystemName { get; set; }
        public int CurrentInterval { get; set; }
        public int RecommendedInterval { get; set; }
        public long LastExecutionTime { get; set; }
        public List<long> ExecutionSamples { get; set; }
        public int TotalExecutions { get; set; }
    }
}

