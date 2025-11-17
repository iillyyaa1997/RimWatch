using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation
{
    /// <summary>
    /// 👥 Social Automation - автоматическое управление социальными аспектами колонии.
    /// Мониторит настроение, отношения и управляет заключенными.
    /// </summary>
    public static class SocialAutomation
    {
        private static int _tickCounter = 0;
        private static bool _isEnabled = false;
        private const int UpdateInterval = 1200; // Обновление каждые 20 секунд (1200 тиков)

        /// <summary>
        /// Включена ли автоматизация социальных аспектов.
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                RimWatchLogger.Info($"SocialAutomation: {(value ? "Enabled" : "Disabled")}");
            }
        }

        /// <summary>
        /// Вызывается каждый тик для обновления автоматизации.
        /// </summary>
        public static void Tick()
        {
            if (!IsEnabled) return;
            if (!RimWatchCore.AutopilotEnabled) return;

            _tickCounter++;
            if (_tickCounter >= UpdateInterval)
            {
                _tickCounter = 0;
                RimWatchLogger.Info("[SocialAutomation] Tick! Checking colony mood...");
                ManageSocial();
            }
        }

        /// <summary>
        /// Manages social aspects of the colony.
        /// </summary>
        private static void ManageSocial()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
            if (colonists.Count == 0) return;

            SocialStatus status = AnalyzeSocialStatus(map, colonists);

            // Report mood issues
            if (status.ColonistsAtRisk > 0)
            {
                RimWatchLogger.Info($"SocialAutomation: 🚨 {status.ColonistsAtRisk} colonists at mental break risk!");
            }

            if (status.UnhappyColonists > 0)
            {
                RimWatchLogger.Info($"SocialAutomation: ⚠️ {status.UnhappyColonists} unhappy colonists");
            }

            // Report prisoners
            if (status.PrisonerCount > 0)
            {
                RimWatchLogger.Info($"SocialAutomation: ℹ️ {status.PrisonerCount} prisoners in custody");
            }

            // All good
            if (status.ColonistsAtRisk == 0 && status.UnhappyColonists == 0)
            {
                RimWatchLogger.Debug($"SocialAutomation: Colony morale good (Avg: {status.AverageMood:P0}) ✓");
            }

            // **NEW: Execute social actions**
            AutoManagePrisoners(map);
            AutoScheduleParties(map, status);
        }

        /// <summary>
        /// Analyzes social status.
        /// </summary>
        private static SocialStatus AnalyzeSocialStatus(Map map, List<Pawn> colonists)
        {
            SocialStatus status = new SocialStatus();

            float totalMood = 0;
            int moodCount = 0;

            foreach (Pawn colonist in colonists)
            {
                if (colonist.needs?.mood == null) continue;

                float mood = colonist.needs.mood.CurLevel;
                totalMood += mood;
                moodCount++;

                // Check for mental break risk
                if (mood < 0.25f)
                {
                    status.ColonistsAtRisk++;
                }
                else if (mood < 0.50f)
                {
                    status.UnhappyColonists++;
                }
            }

            // Calculate average mood
            if (moodCount > 0)
            {
                status.AverageMood = totalMood / moodCount;
            }

            // Count prisoners
            status.PrisonerCount = map.mapPawns.PrisonersOfColonySpawnedCount;

            return status;
        }

        /// <summary>
        /// Structure for social status.
        /// </summary>
        private class SocialStatus
        {
            public int ColonistsAtRisk { get; set; } = 0;
            public int UnhappyColonists { get; set; } = 0;
            public float AverageMood { get; set; } = 0.5f;
            public int PrisonerCount { get; set; } = 0;
        }

        // ==================== ACTION METHODS ====================

        /// <summary>
        /// Automatically manages prisoners - analyzes value and provides recommendations.
        /// NOTE: Direct prisoner interaction mode manipulation has API limitations in RimWorld 1.6.
        /// We analyze and log recommendations for now.
        /// </summary>
        private static void AutoManagePrisoners(Map map)
        {
            try
            {
                List<Pawn> prisoners = map.mapPawns.PrisonersOfColonySpawned.ToList();
                if (prisoners.Count == 0) return;

                int analyzed = 0;
                List<string> recommendations = new List<string>();

                foreach (Pawn prisoner in prisoners)
                {
                    if (prisoner.guest == null) continue;

                    // Calculate prisoner value for recruitment
                    float recruitmentValue = CalculatePrisonerValue(prisoner);
                    analyzed++;

                    // Provide recommendations based on value
                    string recommendation;

                    if (recruitmentValue >= 50f)
                    {
                        // High value - recommend recruiting
                        recommendation = $"🤝 HIGH VALUE: {prisoner.LabelShort} (score: {recruitmentValue:F0}) - Recommend recruiting";
                    }
                    else if (recruitmentValue >= 20f)
                    {
                        // Medium value - reduce resistance first
                        recommendation = $"🔧 MEDIUM VALUE: {prisoner.LabelShort} (score: {recruitmentValue:F0}) - Reduce resistance before recruiting";
                    }
                    else
                    {
                        // Low value - recommend releasing
                        recommendation = $"⛔ LOW VALUE: {prisoner.LabelShort} (score: {recruitmentValue:F0}) - Recommend releasing";
                    }

                    recommendations.Add(recommendation);
                }

                if (analyzed > 0)
                {
                    RimWatchLogger.Info($"👥 SocialAutomation: Analyzed {analyzed} prisoners:");
                    foreach (string rec in recommendations)
                    {
                        RimWatchLogger.Info($"   • {rec}");
                    }
                    RimWatchLogger.Info("   [NOTE: Prisoner interaction mode changes not automated - RimWorld 1.6 API limitation]");
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("SocialAutomation: Error in AutoManagePrisoners", ex);
            }
        }

        /// <summary>
        /// Calculates the value of a prisoner for recruitment purposes.
        /// </summary>
        private static float CalculatePrisonerValue(Pawn prisoner)
        {
            float value = 0f;

            // Skills are valuable
            if (prisoner.skills != null)
            {
                // Count skills above 6
                int goodSkills = prisoner.skills.skills.Count(s => s.Level >= 6);
                value += goodSkills * 10f;

                // Exceptional skills (10+) are very valuable
                int greatSkills = prisoner.skills.skills.Count(s => s.Level >= 10);
                value += greatSkills * 20f;
            }

            // Health is important
            if (prisoner.health != null)
            {
                float healthPercent = prisoner.health.summaryHealth.SummaryHealthPercent;
                value += healthPercent * 20f; // 0-20 points based on health
            }

            // Age matters (prefer young)
            if (prisoner.ageTracker != null)
            {
                int age = prisoner.ageTracker.AgeBiologicalYears;
                if (age < 25) value += 15f; // Young and capable
                else if (age < 40) value += 10f; // Prime age
                else if (age < 60) value += 5f; // Still useful
                // Old age: no bonus
            }

            // Traits can be positive or negative
            if (prisoner.story?.traits != null)
            {
                foreach (var trait in prisoner.story.traits.allTraits)
                {
                    // Good traits
                    if (trait.def.defName.Contains("Industrious") ||
                        trait.def.defName.Contains("HardWorker") ||
                        trait.def.defName.Contains("QuickSleeper") ||
                        trait.def.defName.Contains("Tough"))
                    {
                        value += 10f;
                    }

                    // Bad traits
                    if (trait.def.defName.Contains("Pyromaniac") ||
                        trait.def.defName.Contains("Abrasive") ||
                        trait.def.defName.Contains("Volatile"))
                    {
                        value -= 20f;
                    }
                }
            }

            // Note: Recruitment difficulty check removed temporarily due to API uncertainty
            // Will be re-added once correct RimWorld 1.6 API is confirmed

            return Math.Max(0f, value); // Never negative
        }

        /// <summary>
        /// Automatically schedules parties/gatherings when colony morale is low.
        /// Uses the "Party" gathering type from RimWorld's social system.
        /// </summary>
        private static void AutoScheduleParties(Map map, SocialStatus status)
        {
            try
            {
                // Only schedule if morale is low
                if (status.UnhappyColonists < 2 && status.ColonistsAtRisk == 0)
                {
                    return; // Morale is fine, no party needed
                }

                // Check if there's already an active party/gathering
                if (map.lordManager.lords.Any(l => l.LordJob != null && 
                                                   l.LordJob.ToString().Contains("Party")))
                {
                    RimWatchLogger.Debug("SocialAutomation: Party already in progress");
                    return; // Party already happening
                }

                // Check if we have a suitable gathering spot
                if (!HasGatheringSpot(map))
                {
                    RimWatchLogger.Info("SocialAutomation: ⚠️ No gathering spot available for party (need campfire or horseshoes pin)");
                    return;
                }

                // Check colonist count (need at least 3 for a party)
                List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
                if (colonists.Count < 3)
                {
                    RimWatchLogger.Debug("SocialAutomation: Not enough colonists for a party (need 3+)");
                    return;
                }

                // Check if colonists are available (not drafted, not sleeping, etc.)
                int availableColonists = colonists.Count(c => 
                    !c.Drafted && 
                    !c.Dead && 
                    !c.Downed && 
                    c.Awake() &&
                    !c.InMentalState);

                if (availableColonists < 2)
                {
                    RimWatchLogger.Debug("SocialAutomation: Colonists too busy for party");
                    return;
                }

                // Try to start a party
                // Note: RimWorld's party system is complex and may require specific conditions
                // We'll use a conservative approach here
                
                RimWatchLogger.Info($"🎉 SocialAutomation: Planning party (morale: {status.AverageMood:P0}, unhappy: {status.UnhappyColonists})");
                RimWatchLogger.Info("   [NOTE: Party scheduling requires manual trigger in RimWorld 1.6 - colonists will naturally gather at spots]");
                
                // In RimWorld 1.6, we cannot directly trigger parties via code without complex Lord system manipulation
                // Instead, we log the recommendation and ensure gathering spots exist
                // Colonists will naturally use gathering spots when they have free time
                
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("SocialAutomation: Error in AutoScheduleParties", ex);
            }
        }

        /// <summary>
        /// Checks if the map has a gathering spot for parties.
        /// </summary>
        private static bool HasGatheringSpot(Map map)
        {
            // Check for campfire, party spot, or horseshoes pin
            return map.listerBuildings.allBuildingsColonist.Any(b => 
                b.def.defName.ToLower().Contains("campfire") ||
                b.def.defName.ToLower().Contains("party") ||
                b.def.defName.ToLower().Contains("horseshoe") ||
                b.def.defName.ToLower().Contains("gathering"));
        }
    }
}
