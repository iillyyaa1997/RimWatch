using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWatch.Automation.Social
{
    /// <summary>
    /// Detects and prevents mood crises before they become mental breaks.
    /// v0.9.18: Mood crisis detection and prevention system.
    /// </summary>
    public static class MoodCrisisDetector
    {
        // Configuration Constants
        private const int UPDATE_INTERVAL = 600; // Check every 10 seconds
        private const float CRITICAL_MOOD_THRESHOLD = 0.15f; // Below 15% is critical
        private const float WARNING_MOOD_THRESHOLD = 0.30f; // Below 30% is warning
        private const float MAJOR_BREAK_RISK_THRESHOLD = 0.40f; // Above 40% break chance is high risk
        public const int CRISIS_HISTORY_SIZE = 20; // Track last 20 mood checks
        
        // State tracking
        private static int _lastUpdateTick = 0;
        private static Dictionary<int, MoodHistory> _colonistMoodHistory = new Dictionary<int, MoodHistory>();
        private static Dictionary<int, CrisisAlert> _activeAlerts = new Dictionary<int, CrisisAlert>();
        
        /// <summary>
        /// Gets list of active mood crises for UI display.
        /// </summary>
        public static List<CrisisAlertInfo> GetActiveCrises()
        {
            var alerts = new List<CrisisAlertInfo>();
            int currentTick = Find.TickManager.TicksGame;
            
            foreach (var alert in _activeAlerts.Values)
            {
                alerts.Add(new CrisisAlertInfo
                {
                    PawnName = alert.Pawn.Name?.ToStringShort ?? "Unknown",
                    Level = alert.Level.ToString(),
                    Mood = alert.Pawn.needs?.mood?.CurLevel ?? 0f,
                    Reasons = alert.Reasons,
                    DurationTicks = currentTick - alert.DetectedTick
                });
            }
            
            return alerts;
        }
        
        /// <summary>
        /// Main tick method for mood crisis detection.
        /// </summary>
        public static void Tick(Map map)
        {
            try
            {
                int currentTick = Find.TickManager.TicksGame;
                
                if (currentTick - _lastUpdateTick < UPDATE_INTERVAL)
                    return;
                
                _lastUpdateTick = currentTick;
                
                // Scan all colonists for mood problems
                ScanForMoodCrises(map);
                
                // Update trending analysis
                UpdateMoodTrends();
                
                RimWatchLogger.Debug($"MoodCrisisDetector: {_activeAlerts.Count} active mood alerts");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("MoodCrisisDetector: Error in Tick", ex);
            }
        }
        
        /// <summary>
        /// Scans colonists for mood crises.
        /// </summary>
        private static void ScanForMoodCrises(Map map)
        {
            int currentTick = Find.TickManager.TicksGame;
            var colonists = map.mapPawns.FreeColonistsSpawned.ToList();
            
            foreach (var pawn in colonists)
            {
                if (pawn.Dead || pawn.needs?.mood == null)
                    continue;
                
                // Get mood data
                float currentMood = pawn.needs.mood.CurLevel;
                float breakThreshold = pawn.mindState.mentalBreaker.BreakThresholdMinor;
                
                // Track mood history
                if (!_colonistMoodHistory.ContainsKey(pawn.thingIDNumber))
                {
                    _colonistMoodHistory[pawn.thingIDNumber] = new MoodHistory { Pawn = pawn };
                }
                
                var history = _colonistMoodHistory[pawn.thingIDNumber];
                history.AddMoodSample(currentMood, currentTick);
                
                // Analyze crisis level
                CrisisLevel level = DetermineCrisisLevel(pawn, currentMood, breakThreshold, history);
                
                if (level != CrisisLevel.None)
                {
                    // Create or update alert
                    if (!_activeAlerts.ContainsKey(pawn.thingIDNumber))
                    {
                        _activeAlerts[pawn.thingIDNumber] = new CrisisAlert
                        {
                            Pawn = pawn,
                            Level = level,
                            DetectedTick = currentTick,
                            Reasons = IdentifyMoodProblems(pawn)
                        };
                        
                        RimWatchLogger.Warning($"MoodCrisisDetector: New {level} alert for {pawn.Name} (Mood: {currentMood:P0}, Threshold: {breakThreshold:P0})");
                    }
                    else
                    {
                        // Update existing alert
                        var alert = _activeAlerts[pawn.thingIDNumber];
                        if (alert.Level != level)
                        {
                            RimWatchLogger.Warning($"MoodCrisisDetector: {pawn.Name} crisis escalated from {alert.Level} to {level}");
                            alert.Level = level;
                        }
                    }
                }
                else
                {
                    // Clear alert if mood improved
                    if (_activeAlerts.ContainsKey(pawn.thingIDNumber))
                    {
                        RimWatchLogger.Info($"MoodCrisisDetector: Crisis resolved for {pawn.Name}");
                        _activeAlerts.Remove(pawn.thingIDNumber);
                    }
                }
            }
        }
        
        /// <summary>
        /// Determines the crisis level for a colonist.
        /// </summary>
        private static CrisisLevel DetermineCrisisLevel(Pawn pawn, float currentMood, float breakThreshold, MoodHistory history)
        {
            // Check for imminent break
            if (currentMood <= breakThreshold)
            {
                return CrisisLevel.Critical;
            }
            
            // Check for very low mood
            if (currentMood < CRITICAL_MOOD_THRESHOLD)
            {
                return CrisisLevel.Critical;
            }
            
            // Check for low mood
            if (currentMood < WARNING_MOOD_THRESHOLD)
            {
                return CrisisLevel.High;
            }
            
            // Check for declining trend
            if (history.Samples.Count >= 5)
            {
                float trend = history.CalculateTrend();
                
                // Rapidly declining mood
                if (trend < -0.05f && currentMood < 0.50f) // Dropping 5%+ per check and below 50%
                {
                    return CrisisLevel.Medium;
                }
            }
            
            // Check for mental break risk
            var breakerState = pawn.mindState?.mentalBreaker;
            if (breakerState != null)
            {
                float breakChance = Mathf.Max(
                    breakerState.BreakMinorIsImminent ? 1f : 0f,
                    breakerState.BreakMajorIsImminent ? 1f : 0f,
                    breakerState.BreakExtremeIsImminent ? 1f : 0f
                );
                
                if (breakChance > MAJOR_BREAK_RISK_THRESHOLD)
                {
                    return CrisisLevel.High;
                }
            }
            
            return CrisisLevel.None;
        }
        
        /// <summary>
        /// Identifies the main reasons for mood problems.
        /// </summary>
        private static List<string> IdentifyMoodProblems(Pawn pawn)
        {
            List<string> problems = new List<string>();
            
            if (pawn.needs?.mood == null)
                return problems;
            
            // Analyze mood thoughts - get all distinct thoughts
            var thoughts = new List<Thought>();
            pawn.needs.mood.thoughts.GetAllMoodThoughts(thoughts);
            
            // Group by mood impact
            var negativeThoughts = thoughts
                .Where(t => t.MoodOffset() < 0)
                .OrderBy(t => t.MoodOffset())
                .Take(5)
                .ToList();
            
            foreach (var thought in negativeThoughts)
            {
                problems.Add($"{thought.LabelCap} ({thought.MoodOffset():+0;-0})");
            }
            
            // Check for critical needs
            if (pawn.needs.food != null && pawn.needs.food.CurLevelPercentage < 0.3f)
                problems.Add("Hungry");
            
            if (pawn.needs.rest != null && pawn.needs.rest.CurLevelPercentage < 0.3f)
                problems.Add("Tired");
            
            if (pawn.needs.joy != null && pawn.needs.joy.CurLevelPercentage < 0.3f)
                problems.Add("Bored");
            
            if (pawn.needs.comfort != null && pawn.needs.comfort.CurLevelPercentage < 0.3f)
                problems.Add("Uncomfortable");
            
            if (pawn.needs.beauty != null && pawn.needs.beauty.CurLevelPercentage < 0.3f)
                problems.Add("Ugly environment");
            
            return problems;
        }
        
        /// <summary>
        /// Updates mood trend analysis for all tracked colonists.
        /// </summary>
        private static void UpdateMoodTrends()
        {
            // Clean up dead colonists
            var deadIds = _colonistMoodHistory
                .Where(kvp => kvp.Value.Pawn == null || kvp.Value.Pawn.Dead)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (int id in deadIds)
            {
                _colonistMoodHistory.Remove(id);
                _activeAlerts.Remove(id);
            }
        }
        
        /// <summary>
        /// Gets recommended interventions for a colonist.
        /// </summary>
        public static List<string> GetRecommendedInterventions(Pawn pawn)
        {
            List<string> interventions = new List<string>();
            
            if (pawn?.needs == null)
                return interventions;
            
            // Check needs
            if (pawn.needs.food != null && pawn.needs.food.CurLevelPercentage < 0.5f)
                interventions.Add("Ensure access to food");
            
            if (pawn.needs.rest != null && pawn.needs.rest.CurLevelPercentage < 0.5f)
                interventions.Add("Allow time to sleep");
            
            if (pawn.needs.joy != null && pawn.needs.joy.CurLevelPercentage < 0.3f)
                interventions.Add("Schedule recreation time");
            
            if (pawn.needs.comfort != null && pawn.needs.comfort.CurLevelPercentage < 0.3f)
                interventions.Add("Provide comfortable furniture");
            
            if (pawn.needs.beauty != null && pawn.needs.beauty.CurLevelPercentage < 0.3f)
                interventions.Add("Improve room beauty (art, flooring)");
            
            // Check room
            var room = pawn.GetRoom();
            if (room != null)
            {
                if (room.GetStat(RoomStatDefOf.Impressiveness) < 0)
                    interventions.Add("Improve room quality");
                
                if (room.GetStat(RoomStatDefOf.Cleanliness) < -1f)
                    interventions.Add("Clean the room");
            }
            
            // Check social
            if (pawn.needs.mood != null)
            {
                var allThoughts = new List<Thought>();
                pawn.needs.mood.thoughts.GetAllMoodThoughts(allThoughts);
                
                var negativeThoughts = allThoughts
                    .Where(t => t.def.defName.Contains("Rival") || t.def.defName.Contains("Annoying"))
                    .ToList();
                
                if (negativeThoughts.Count > 0)
                    interventions.Add("Separate from rivals");
            }
            
            // Emergency interventions
            if (pawn.needs.mood.CurLevel < CRITICAL_MOOD_THRESHOLD)
            {
                interventions.Add("URGENT: Schedule party or recreation");
                interventions.Add("URGENT: Arrest if break imminent");
            }
            
            return interventions;
        }
    }
    
    /// <summary>
    /// Tracks mood history for a colonist.
    /// </summary>
    public class MoodHistory
    {
        public Pawn Pawn { get; set; }
        public List<MoodSample> Samples { get; set; } = new List<MoodSample>();
        
        public void AddMoodSample(float mood, int tick)
        {
            Samples.Add(new MoodSample { Mood = mood, Tick = tick });
            
            // Keep only recent samples
            if (Samples.Count > MoodCrisisDetector.CRISIS_HISTORY_SIZE)
            {
                Samples.RemoveAt(0);
            }
        }
        
        /// <summary>
        /// Calculates mood trend (positive = improving, negative = declining).
        /// </summary>
        public float CalculateTrend()
        {
            if (Samples.Count < 2)
                return 0f;
            
            // Simple linear regression
            float avgMood = Samples.Average(s => s.Mood);
            float avgIndex = Samples.Count / 2f;
            
            float numerator = 0f;
            float denominator = 0f;
            
            for (int i = 0; i < Samples.Count; i++)
            {
                numerator += (i - avgIndex) * (Samples[i].Mood - avgMood);
                denominator += (i - avgIndex) * (i - avgIndex);
            }
            
            return denominator > 0 ? numerator / denominator : 0f;
        }
    }
    
    /// <summary>
    /// A single mood measurement.
    /// </summary>
    public class MoodSample
    {
        public float Mood { get; set; }
        public int Tick { get; set; }
    }
    
    /// <summary>
    /// Crisis severity levels.
    /// </summary>
    public enum CrisisLevel
    {
        None = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }
    
    /// <summary>
    /// Active crisis alert for a colonist.
    /// </summary>
    public class CrisisAlert
    {
        public Pawn Pawn { get; set; }
        public CrisisLevel Level { get; set; }
        public int DetectedTick { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
    }
    
    /// <summary>
    /// Public crisis info for UI display.
    /// </summary>
    public class CrisisAlertInfo
    {
        public string PawnName { get; set; }
        public string Level { get; set; }
        public float Mood { get; set; }
        public List<string> Reasons { get; set; }
        public int DurationTicks { get; set; }
    }
}

