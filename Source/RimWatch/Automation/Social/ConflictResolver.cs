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
    /// Detects and resolves social conflicts between colonists.
    /// v0.9.18: Conflict detection and resolution system.
    /// </summary>
    public static class ConflictResolver
    {
        // Configuration Constants
        private const int UPDATE_INTERVAL = 1800; // Check every 30 seconds
        private const float HOSTILE_OPINION_THRESHOLD = -20f; // Below -20 opinion is hostile
        private const float RIVAL_OPINION_THRESHOLD = -40f; // Below -40 is rivalry
        private const int MIN_SEPARATION_DISTANCE = 10; // Min tiles between rivals
        
        // State tracking
        private static int _lastUpdateTick = 0;
        private static Dictionary<string, SocialConflict> _activeConflicts = new Dictionary<string, SocialConflict>();
        private static Dictionary<int, List<int>> _separationPairs = new Dictionary<int, List<int>>(); // Pawn ID -> List of rivals to avoid
        
        /// <summary>
        /// Main tick method for conflict resolution.
        /// </summary>
        public static void Tick(Map map)
        {
            try
            {
                int currentTick = Find.TickManager.TicksGame;
                
                if (currentTick - _lastUpdateTick < UPDATE_INTERVAL)
                    return;
                
                _lastUpdateTick = currentTick;
                
                // Detect conflicts
                DetectConflicts(map);
                
                // Resolve active conflicts
                ResolveConflicts(map);
                
                RimWatchLogger.Debug($"ConflictResolver: {_activeConflicts.Count} active conflicts");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ConflictResolver: Error in Tick", ex);
            }
        }
        
        /// <summary>
        /// Detects social conflicts between colonists.
        /// </summary>
        private static void DetectConflicts(Map map)
        {
            int currentTick = Find.TickManager.TicksGame;
            var colonists = map.mapPawns.FreeColonistsSpawned.ToList();
            
            foreach (var pawn1 in colonists)
            {
                if (pawn1.Dead || pawn1.relations == null)
                    continue;
                
                foreach (var pawn2 in colonists)
                {
                    if (pawn1 == pawn2 || pawn2.Dead || pawn2.relations == null)
                        continue;
                    
                    // Check opinion
                    int opinion = pawn1.relations.OpinionOf(pawn2);
                    
                    if (opinion < HOSTILE_OPINION_THRESHOLD)
                    {
                        string conflictId = GetConflictId(pawn1, pawn2);
                        
                        if (!_activeConflicts.ContainsKey(conflictId))
                        {
                            _activeConflicts[conflictId] = new SocialConflict
                            {
                                Pawn1 = pawn1,
                                Pawn2 = pawn2,
                                Opinion1to2 = opinion,
                                Opinion2to1 = pawn2.relations.OpinionOf(pawn1),
                                DetectedTick = currentTick,
                                Severity = CalculateConflictSeverity(opinion, pawn2.relations.OpinionOf(pawn1))
                            };
                            
                            RimWatchLogger.Warning($"ConflictResolver: Detected conflict between {pawn1.Name} and {pawn2.Name} (Opinions: {opinion} / {pawn2.relations.OpinionOf(pawn1)})");
                        }
                        else
                        {
                            // Update existing conflict
                            var conflict = _activeConflicts[conflictId];
                            conflict.Opinion1to2 = opinion;
                            conflict.Opinion2to1 = pawn2.relations.OpinionOf(pawn1);
                            conflict.Severity = CalculateConflictSeverity(opinion, pawn2.relations.OpinionOf(pawn1));
                        }
                    }
                }
            }
            
            // Clean up resolved conflicts
            var resolvedIds = _activeConflicts
                .Where(kvp => kvp.Value.Pawn1 == null || kvp.Value.Pawn1.Dead ||
                             kvp.Value.Pawn2 == null || kvp.Value.Pawn2.Dead ||
                             kvp.Value.Pawn1.relations.OpinionOf(kvp.Value.Pawn2) >= HOSTILE_OPINION_THRESHOLD)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (string id in resolvedIds)
            {
                var conflict = _activeConflicts[id];
                RimWatchLogger.Info($"ConflictResolver: Conflict resolved between {conflict.Pawn1?.Name} and {conflict.Pawn2?.Name}");
                _activeConflicts.Remove(id);
            }
        }
        
        /// <summary>
        /// Calculates conflict severity.
        /// </summary>
        private static ConflictSeverity CalculateConflictSeverity(int opinion1, int opinion2)
        {
            float avgOpinion = (opinion1 + opinion2) / 2f;
            
            if (avgOpinion < -60f)
                return ConflictSeverity.Extreme;
            
            if (avgOpinion < RIVAL_OPINION_THRESHOLD)
                return ConflictSeverity.High;
            
            return ConflictSeverity.Medium;
        }
        
        /// <summary>
        /// Resolves active conflicts.
        /// </summary>
        private static void ResolveConflicts(Map map)
        {
            foreach (var conflict in _activeConflicts.Values.ToList())
            {
                if (conflict.Pawn1 == null || conflict.Pawn1.Dead ||
                    conflict.Pawn2 == null || conflict.Pawn2.Dead)
                    continue;
                
                // Choose resolution strategy based on severity
                switch (conflict.Severity)
                {
                    case ConflictSeverity.Extreme:
                        ResolveExtremeConflict(conflict, map);
                        break;
                    
                    case ConflictSeverity.High:
                        ResolveHighConflict(conflict, map);
                        break;
                    
                    case ConflictSeverity.Medium:
                        ResolveMediumConflict(conflict, map);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Resolves extreme conflicts (rivalry, deep hatred).
        /// </summary>
        private static void ResolveExtremeConflict(SocialConflict conflict, Map map)
        {
            // Strategy: Physical separation
            
            // Track separation pairs
            if (!_separationPairs.ContainsKey(conflict.Pawn1.thingIDNumber))
                _separationPairs[conflict.Pawn1.thingIDNumber] = new List<int>();
            
            if (!_separationPairs[conflict.Pawn1.thingIDNumber].Contains(conflict.Pawn2.thingIDNumber))
            {
                _separationPairs[conflict.Pawn1.thingIDNumber].Add(conflict.Pawn2.thingIDNumber);
            }
            
            if (!_separationPairs.ContainsKey(conflict.Pawn2.thingIDNumber))
                _separationPairs[conflict.Pawn2.thingIDNumber] = new List<int>();
            
            if (!_separationPairs[conflict.Pawn2.thingIDNumber].Contains(conflict.Pawn1.thingIDNumber))
            {
                _separationPairs[conflict.Pawn2.thingIDNumber].Add(conflict.Pawn1.thingIDNumber);
            }
            
            RimWatchLogger.Info($"ConflictResolver: EXTREME conflict - {conflict.Pawn1.Name} and {conflict.Pawn2.Name} should be separated");
            RimWatchLogger.Info($"   → Assign to different work areas if possible");
            RimWatchLogger.Info($"   → Separate bedrooms");
            
            // TODO: Actually enforce separation via work zones, room assignments
        }
        
        /// <summary>
        /// Resolves high-severity conflicts.
        /// </summary>
        private static void ResolveHighConflict(SocialConflict conflict, Map map)
        {
            // Strategy: Minimize interaction
            
            RimWatchLogger.Info($"ConflictResolver: HIGH conflict - Minimize interaction between {conflict.Pawn1.Name} and {conflict.Pawn2.Name}");
            RimWatchLogger.Info($"   → Stagger work schedules");
            RimWatchLogger.Info($"   → Assign different tasks");
            
            // TODO: Implement schedule staggering
        }
        
        /// <summary>
        /// Resolves medium-severity conflicts.
        /// </summary>
        private static void ResolveMediumConflict(SocialConflict conflict, Map map)
        {
            // Strategy: Mood improvement to reduce tension
            
            RimWatchLogger.Debug($"ConflictResolver: MEDIUM conflict - Monitor {conflict.Pawn1.Name} and {conflict.Pawn2.Name}");
            
            // Check if either pawn has low mood
            if (conflict.Pawn1.needs?.mood != null && conflict.Pawn1.needs.mood.CurLevelPercentage < 0.5f)
            {
                RimWatchLogger.Info($"   → {conflict.Pawn1.Name} has low mood, improving conditions may help");
            }
            
            if (conflict.Pawn2.needs?.mood != null && conflict.Pawn2.needs.mood.CurLevelPercentage < 0.5f)
            {
                RimWatchLogger.Info($"   → {conflict.Pawn2.Name} has low mood, improving conditions may help");
            }
        }
        
        /// <summary>
        /// Gets unique ID for a conflict pair.
        /// </summary>
        private static string GetConflictId(Pawn pawn1, Pawn pawn2)
        {
            // Always use consistent ordering
            int id1 = pawn1.thingIDNumber;
            int id2 = pawn2.thingIDNumber;
            
            if (id1 < id2)
                return $"{id1}_{id2}";
            else
                return $"{id2}_{id1}";
        }
        
        /// <summary>
        /// Gets active conflicts for UI display.
        /// </summary>
        public static List<ConflictInfo> GetActiveConflicts()
        {
            return _activeConflicts.Values
                .OrderByDescending(c => (int)c.Severity)
                .Select(c => new ConflictInfo
                {
                    Pawn1Name = c.Pawn1?.Name?.ToString() ?? "Unknown",
                    Pawn2Name = c.Pawn2?.Name?.ToString() ?? "Unknown",
                    Opinion1to2 = c.Opinion1to2,
                    Opinion2to1 = c.Opinion2to1,
                    Severity = c.Severity.ToString(),
                    DurationTicks = Find.TickManager.TicksGame - c.DetectedTick
                })
                .ToList();
        }
        
        /// <summary>
        /// Gets recommended interventions for a conflict.
        /// </summary>
        public static List<string> GetRecommendedInterventions(SocialConflict conflict)
        {
            List<string> interventions = new List<string>();
            
            switch (conflict.Severity)
            {
                case ConflictSeverity.Extreme:
                    interventions.Add("Separate work areas");
                    interventions.Add("Assign different bedrooms");
                    interventions.Add("Stagger recreation time");
                    interventions.Add("Consider removing one from colony if persistent");
                    break;
                
                case ConflictSeverity.High:
                    interventions.Add("Minimize shared tasks");
                    interventions.Add("Stagger work schedules");
                    interventions.Add("Improve mood to reduce tension");
                    break;
                
                case ConflictSeverity.Medium:
                    interventions.Add("Monitor situation");
                    interventions.Add("Improve colony mood");
                    interventions.Add("Ensure both have good recreation");
                    break;
            }
            
            return interventions;
        }
    }
    
    /// <summary>
    /// Represents a social conflict between two colonists.
    /// </summary>
    public class SocialConflict
    {
        public Pawn Pawn1 { get; set; }
        public Pawn Pawn2 { get; set; }
        public int Opinion1to2 { get; set; }
        public int Opinion2to1 { get; set; }
        public ConflictSeverity Severity { get; set; }
        public int DetectedTick { get; set; }
    }
    
    /// <summary>
    /// Conflict severity levels.
    /// </summary>
    public enum ConflictSeverity
    {
        Medium = 1,
        High = 2,
        Extreme = 3
    }
    
    /// <summary>
    /// Public conflict info for UI.
    /// </summary>
    public class ConflictInfo
    {
        public string Pawn1Name { get; set; }
        public string Pawn2Name { get; set; }
        public int Opinion1to2 { get; set; }
        public int Opinion2to1 { get; set; }
        public string Severity { get; set; }
        public int DurationTicks { get; set; }
    }
}

