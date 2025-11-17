using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace RimWatch.Automation
{
    /// <summary>
    /// v0.8.0: Apparel Automation - Smart clothing management.
    /// Auto-equips colonists with best available apparel based on quality, condition, and role.
    /// </summary>
    public static class ApparelAutomation
    {
        private static int _lastCheckTick = 0;
        private const int CheckInterval = 3600; // Check every minute
        
        // v0.8.4: Per-colonist cooldown to avoid spammy re-equipping loops
        // Key: colonist ThingID, Value: last tick when we issued any Wear job
        private static readonly Dictionary<string, int> _lastEquipTickByColonist = new Dictionary<string, int>();
        private const int PerColonistEquipCooldownTicks = 60000; // ~1 in-game day (60k тиков)
        
        /// <summary>
        /// Main tick - call from MapComponent.
        /// </summary>
        public static void Tick(Map map)
        {
            try
            {
                // v0.8.1: Check if enabled in settings
                if (!RimWatchMod.Settings.apparelAutomationEnabled) return;
                
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - _lastCheckTick < CheckInterval) return;
                _lastCheckTick = currentTick;
                
                // v0.8.1: CRITICAL FIX - Create a copy of the list to avoid "Collection was modified" error
                // This happens when jobs are assigned during iteration
                List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
                
                // Check each colonist
                foreach (Pawn colonist in colonists)
                {
                    if (colonist.Dead || colonist.Downed) continue;
                    CheckAndUpgradeApparel(colonist, map);
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"ApparelAutomation: Error in Tick: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check and upgrade colonist apparel.
        /// </summary>
        private static void CheckAndUpgradeApparel(Pawn colonist, Map map)
        {
            try
            {
                if (colonist.apparel == null) return;
                
                // v0.8.4: Skip if this colonist was already auto-equipped recently
                int currentTick = Find.TickManager.TicksGame;
                string key = colonist.ThingID;
                if (_lastEquipTickByColonist.TryGetValue(key, out int lastTick))
                {
                    int delta = currentTick - lastTick;
                    if (delta >= 0 && delta < PerColonistEquipCooldownTicks)
                    {
                        // We equipped this pawn recently – don't spam Wear jobs every minute
                        return;
                    }
                }
                
                // 1. Remove damaged/corpse apparel
                var toRemove = colonist.apparel.WornApparel
                    .Where(a => a.HitPoints < a.MaxHitPoints * 0.5f || // <50% HP
                               (a.Stuff != null && a.Stuff.defName.Contains("Human"))) // Corpse material
                    .ToList();
                
                foreach (var badApparel in toRemove)
                {
                    colonist.apparel.Remove(badApparel);
                    RimWatchLogger.Info($"👔 ApparelAutomation: {colonist.LabelShort} removed damaged/corpse apparel {badApparel.Label}");
                }
                
                // 2. Find better apparel in storage for each body part group
                var storage = map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel)
                    .OfType<Apparel>()
                    .Where(a => !a.IsForbidden(Faction.OfPlayer) &&
                               a.HitPoints > a.MaxHitPoints * 0.5f && // >50% HP
                               ApparelUtility.HasPartsToWear(colonist, a.def)) // Can wear this type
                    .ToList();
                
                if (storage.Count == 0) return;
                
                // Group by body part coverage (shirt, pants, helmet, etc)
                bool issuedWearJob = false;
                
                var byBodyPart = storage
                    .GroupBy(a => string.Join(",", a.def.apparel.bodyPartGroups.Select(bp => bp.defName)))
                    .ToList();
                
                foreach (var group in byBodyPart)
                {
                    var best = group.OrderByDescending(a => ScoreApparel(a, colonist)).First();
                    
                    if (ShouldUpgrade(colonist, best))
                    {
                        Job wearJob = JobMaker.MakeJob(JobDefOf.Wear, best);
                        colonist.jobs.TryTakeOrderedJob(wearJob, JobTag.Misc);
                        RimWatchLogger.Info($"👔 ApparelAutomation: {colonist.LabelShort} equipping {best.Label}");
                        issuedWearJob = true;
                    }
                }

                // Only update cooldown if мы действительно дали хотя бы один Wear-job
                if (issuedWearJob)
                {
                    _lastEquipTickByColonist[key] = currentTick;
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Warning($"ApparelAutomation: Error checking {colonist.LabelShort}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Score apparel quality.
        /// </summary>
        private static float ScoreApparel(Apparel apparel, Pawn wearer)
        {
            float score = 0f;
            
            // HP condition
            score += (float)apparel.HitPoints / apparel.MaxHitPoints * 50f;
            
            // Quality
            if (apparel.TryGetQuality(out QualityCategory quality))
            {
                score += (int)quality * 10f;
            }
            
            // Armor rating
            score += apparel.GetStatValue(StatDefOf.ArmorRating_Sharp) * 100f;
            score += apparel.GetStatValue(StatDefOf.ArmorRating_Blunt) * 50f;
            
            return score;
        }
        
        /// <summary>
        /// Check if should upgrade to new apparel.
        /// </summary>
        private static bool ShouldUpgrade(Pawn colonist, Apparel newApparel)
        {
            // If no similar item worn, upgrade
            var current = colonist.apparel.WornApparel
                .FirstOrDefault(a => a.def.apparel.bodyPartGroups.Intersect(newApparel.def.apparel.bodyPartGroups).Any());
            
            if (current == null) return true;
            
            // Compare scores
            float currentScore = ScoreApparel(current, colonist);
            float newScore = ScoreApparel(newApparel, colonist);
            
            return newScore > currentScore * 1.2f; // 20% better to avoid constant swapping
        }
    }
}

