using RimWatch.Core;
using RimWatch.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.ML
{
    /// <summary>
    /// Machine Learning system to learn from player's manual overrides and adapt AI behavior.
    /// Tracks when player manually changes things and learns their preferences.
    /// v0.9.8: Player style learning and adaptation.
    /// </summary>
    public static class PlayerStyleAnalyzer
    {
        // Configuration Constants
        private const int LEARNING_INTERVAL_TICKS = 2500; // Every ~1 hour game time
        private const int MIN_SAMPLES_FOR_LEARNING = 10; // Minimum overrides before learning
        private const int MAX_OVERRIDE_HISTORY = 500; // Keep last 500 overrides
        private const float PREFERENCE_THRESHOLD = 0.6f; // 60% = strong preference
        private const float ADAPTATION_RATE = 0.1f; // How fast to adapt (0.0-1.0)
        
        // Override tracking
        private static List<PlayerOverride> _overrideHistory = new List<PlayerOverride>();
        private static Dictionary<string, PreferenceData> _learnedPreferences = new Dictionary<string, PreferenceData>();
        private static int _lastLearningTick = 0;
        
        // Player style profile
        private static PlayerStyleProfile _currentProfile = new PlayerStyleProfile();

        /// <summary>
        /// Record when player manually overrides an AI decision.
        /// </summary>
        public static void RecordOverride(string category, string aiAction, string playerAction, string context = "")
        {
            var overrideRecord = new PlayerOverride
            {
                Category = category,
                AIAction = aiAction,
                PlayerAction = playerAction,
                Context = context,
                Timestamp = Find.TickManager.TicksGame
            };

            _overrideHistory.Add(overrideRecord);

            // Limit history size
            if (_overrideHistory.Count > MAX_OVERRIDE_HISTORY)
            {
                _overrideHistory.RemoveAt(0);
            }

            RimWatchLogger.Debug($"PlayerStyleAnalyzer: Recorded override - AI wanted {aiAction}, Player chose {playerAction}");
        }

        /// <summary>
        /// Periodically analyze overrides and update learned preferences.
        /// </summary>
        public static void Tick()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            if (currentTick - _lastLearningTick < LEARNING_INTERVAL_TICKS)
                return;

            _lastLearningTick = currentTick;

            if (_overrideHistory.Count < MIN_SAMPLES_FOR_LEARNING)
            {
                RimWatchLogger.Debug($"PlayerStyleAnalyzer: Insufficient data for learning ({_overrideHistory.Count}/{MIN_SAMPLES_FOR_LEARNING})");
                return;
            }

            AnalyzeOverrides();
            UpdatePlayerProfile();
            AdaptAIBehavior();

            RimWatchLogger.Info($"PlayerStyleAnalyzer: Learning complete. {_learnedPreferences.Count} preferences identified.");
        }

        /// <summary>
        /// Analyze override patterns to learn preferences.
        /// </summary>
        private static void AnalyzeOverrides()
        {
            // Group overrides by category
            var categoryGroups = _overrideHistory
                .GroupBy(o => o.Category)
                .ToList();

            foreach (var group in categoryGroups)
            {
                string category = group.Key;
                var overrides = group.ToList();

                // Find most common player actions
                var actionFrequency = overrides
                    .GroupBy(o => o.PlayerAction)
                    .Select(g => new { Action = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                if (actionFrequency.Count == 0) continue;

                var mostCommon = actionFrequency.First();
                float preference = (float)mostCommon.Count / overrides.Count;

                // Update learned preferences
                if (!_learnedPreferences.ContainsKey(category))
                {
                    _learnedPreferences[category] = new PreferenceData
                    {
                        Category = category,
                        PreferredAction = mostCommon.Action,
                        Confidence = preference,
                        SampleCount = overrides.Count
                    };
                }
                else
                {
                    var existing = _learnedPreferences[category];
                    
                    // Adaptive learning: blend old and new preferences
                    if (mostCommon.Action == existing.PreferredAction)
                    {
                        // Reinforce existing preference
                        existing.Confidence = UnityEngine.Mathf.Lerp(existing.Confidence, preference, ADAPTATION_RATE);
                    }
                    else if (preference > existing.Confidence)
                    {
                        // Switch to new preference if stronger
                        existing.PreferredAction = mostCommon.Action;
                        existing.Confidence = preference;
                    }
                    
                    existing.SampleCount = overrides.Count;
                }

                if (preference >= PREFERENCE_THRESHOLD)
                {
                    RimWatchLogger.Debug($"PlayerStyleAnalyzer: Strong preference detected - {category}: {mostCommon.Action} ({preference:P0})");
                }
            }
        }

        /// <summary>
        /// Update overall player style profile based on learned preferences.
        /// </summary>
        private static void UpdatePlayerProfile()
        {
            // Analyze building preferences
            if (_learnedPreferences.TryGetValue("Building", out PreferenceData buildingPref))
            {
                if (buildingPref.PreferredAction.Contains("Wood"))
                    _currentProfile.PreferredMaterial = MaterialPreference.Wood;
                else if (buildingPref.PreferredAction.Contains("Stone"))
                    _currentProfile.PreferredMaterial = MaterialPreference.Stone;
                else if (buildingPref.PreferredAction.Contains("Steel"))
                    _currentProfile.PreferredMaterial = MaterialPreference.Steel;
            }

            // Analyze defense preferences
            if (_learnedPreferences.TryGetValue("Defense", out PreferenceData defensePref))
            {
                if (defensePref.PreferredAction.Contains("Aggressive") || defensePref.PreferredAction.Contains("Attack"))
                    _currentProfile.DefenseStyle = DefenseStyle.Aggressive;
                else if (defensePref.PreferredAction.Contains("Passive") || defensePref.PreferredAction.Contains("Defensive"))
                    _currentProfile.DefenseStyle = DefenseStyle.Defensive;
            }

            // Analyze expansion preferences
            if (_learnedPreferences.TryGetValue("Expansion", out PreferenceData expansionPref))
            {
                _currentProfile.ExpansionSpeed = expansionPref.PreferredAction.Contains("Fast") || expansionPref.PreferredAction.Contains("Aggressive")
                    ? ExpansionSpeed.Fast
                    : ExpansionSpeed.Slow;
            }

            // Analyze micromanagement level
            float totalOverrides = _overrideHistory.Count;
            float overridesPerHour = totalOverrides / UnityEngine.Mathf.Max(1f, Find.TickManager.TicksGame / 2500f);
            
            if (overridesPerHour > 5f)
                _currentProfile.MicromanagementLevel = MicromanagementLevel.High;
            else if (overridesPerHour > 2f)
                _currentProfile.MicromanagementLevel = MicromanagementLevel.Medium;
            else
                _currentProfile.MicromanagementLevel = MicromanagementLevel.Low;

            RimWatchLogger.Debug($"PlayerStyleAnalyzer: Profile updated - Material: {_currentProfile.PreferredMaterial}, " +
                $"Defense: {_currentProfile.DefenseStyle}, Expansion: {_currentProfile.ExpansionSpeed}, " +
                $"Micromanagement: {_currentProfile.MicromanagementLevel}");
        }

        /// <summary>
        /// Adapt AI behavior based on learned player style.
        /// </summary>
        private static void AdaptAIBehavior()
        {
            // This method would communicate learned preferences back to various AI systems
            // For now, we just log the adaptation
            
            foreach (var pref in _learnedPreferences.Values.Where(p => p.Confidence >= PREFERENCE_THRESHOLD))
            {
                RimWatchLogger.Info($"PlayerStyleAnalyzer: AI adapting to preference - {pref.Category}: {pref.PreferredAction}");
            }
        }

        /// <summary>
        /// Check if player has a strong preference for a specific action in a category.
        /// </summary>
        public static bool HasPreference(string category, string action)
        {
            if (!_learnedPreferences.TryGetValue(category, out PreferenceData pref))
                return false;

            return pref.PreferredAction == action && pref.Confidence >= PREFERENCE_THRESHOLD;
        }

        /// <summary>
        /// Get player's preferred action for a category, or null if no strong preference.
        /// </summary>
        public static string? GetPreferredAction(string category)
        {
            if (!_learnedPreferences.TryGetValue(category, out PreferenceData pref))
                return null;

            return pref.Confidence >= PREFERENCE_THRESHOLD ? pref.PreferredAction : null;
        }

        /// <summary>
        /// Get confidence level (0-1) for a specific preference.
        /// </summary>
        public static float GetConfidence(string category)
        {
            if (!_learnedPreferences.TryGetValue(category, out PreferenceData pref))
                return 0f;

            return pref.Confidence;
        }

        /// <summary>
        /// Get current player style profile.
        /// </summary>
        public static PlayerStyleProfile GetProfile()
        {
            return _currentProfile;
        }

        /// <summary>
        /// Get learning summary for UI/debugging.
        /// </summary>
        public static LearningSummary GetSummary()
        {
            return new LearningSummary
            {
                TotalOverrides = _overrideHistory.Count,
                LearnedPreferences = _learnedPreferences.Count,
                StrongPreferences = _learnedPreferences.Count(p => p.Value.Confidence >= PREFERENCE_THRESHOLD),
                Profile = _currentProfile
            };
        }

        /// <summary>
        /// Export learning data for analysis.
        /// </summary>
        public static string ExportLearningData()
        {
            var summary = GetSummary();

            string export = "=== RimWatch Player Style Analysis ===\n\n";
            export += $"Total Overrides: {summary.TotalOverrides}\n";
            export += $"Learned Preferences: {summary.LearnedPreferences}\n";
            export += $"Strong Preferences: {summary.StrongPreferences}\n\n";

            export += "=== Player Profile ===\n";
            export += $"Preferred Material: {summary.Profile.PreferredMaterial}\n";
            export += $"Defense Style: {summary.Profile.DefenseStyle}\n";
            export += $"Expansion Speed: {summary.Profile.ExpansionSpeed}\n";
            export += $"Micromanagement Level: {summary.Profile.MicromanagementLevel}\n\n";

            export += "=== Learned Preferences ===\n";
            foreach (var pref in _learnedPreferences.Values.OrderByDescending(p => p.Confidence))
            {
                string strength = pref.Confidence >= PREFERENCE_THRESHOLD ? "STRONG" : "WEAK";
                export += $"[{strength}] {pref.Category}: {pref.PreferredAction} ({pref.Confidence:P0}, n={pref.SampleCount})\n";
            }

            return export;
        }

        /// <summary>
        /// Reset learning data (for testing or when player changes playstyle).
        /// </summary>
        public static void ResetLearning()
        {
            _overrideHistory.Clear();
            _learnedPreferences.Clear();
            _currentProfile = new PlayerStyleProfile();
            RimWatchLogger.Info("PlayerStyleAnalyzer: Learning data reset");
        }
    }

    /// <summary>
    /// Record of a player override.
    /// </summary>
    public class PlayerOverride
    {
        public string Category { get; set; } = "";
        public string AIAction { get; set; } = "";
        public string PlayerAction { get; set; } = "";
        public string Context { get; set; } = "";
        public int Timestamp { get; set; }
    }

    /// <summary>
    /// Learned preference data.
    /// </summary>
    public class PreferenceData
    {
        public string Category { get; set; } = "";
        public string PreferredAction { get; set; } = "";
        public float Confidence { get; set; }
        public int SampleCount { get; set; }
    }

    /// <summary>
    /// Player style profile.
    /// </summary>
    public class PlayerStyleProfile
    {
        public MaterialPreference PreferredMaterial { get; set; } = MaterialPreference.Balanced;
        public DefenseStyle DefenseStyle { get; set; } = DefenseStyle.Balanced;
        public ExpansionSpeed ExpansionSpeed { get; set; } = ExpansionSpeed.Medium;
        public MicromanagementLevel MicromanagementLevel { get; set; } = MicromanagementLevel.Medium;
    }

    /// <summary>
    /// Learning summary data.
    /// </summary>
    public class LearningSummary
    {
        public int TotalOverrides { get; set; }
        public int LearnedPreferences { get; set; }
        public int StrongPreferences { get; set; }
        public PlayerStyleProfile Profile { get; set; } = new PlayerStyleProfile();
    }

    // Enums for player preferences
    public enum MaterialPreference { Balanced, Wood, Stone, Steel, Mixed }
    public enum DefenseStyle { Balanced, Defensive, Aggressive }
    public enum ExpansionSpeed { Slow, Medium, Fast }
    public enum MicromanagementLevel { Low, Medium, High }
}

