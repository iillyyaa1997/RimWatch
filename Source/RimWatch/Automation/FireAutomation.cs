using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation
{
    /// <summary>
    /// Уровень угрозы пожара.
    /// </summary>
    public enum ThreatLevel
    {
        Low,        // Небольшой пожар вдали от построек
        Medium,     // Пожар близко к постройкам или горючим материалам
        High,       // Множество пожаров или близко к постройкам
        Critical    // Пожар угрожает колонистам или критичным постройкам
    }
    
    /// <summary>
    /// Информация об угрозе пожара.
    /// </summary>
    public class FireThreat
    {
        public ThreatLevel Level { get; set; } = ThreatLevel.Low;
        public bool IsSpreadingRapidly { get; set; } = false;
    }
    
    /// <summary>
    /// Автоматическая система борьбы с пожарами.
    /// </summary>
    public static class FireAutomation
    {
        private static int _lastCheckTick = 0;
        private static ThreatLevel _lastThreatLevel = ThreatLevel.Low;
        
        /// <summary>
        /// Главная функция - управление тушением пожаров.
        /// </summary>
        public static void AutoManageFires(Map map)
        {
            try
            {
                // Check every 2 seconds
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - _lastCheckTick < 120) return;
                _lastCheckTick = currentTick;
                
                // Detect fires
                var fires = map.listerThings.ThingsOfDef(ThingDefOf.Fire)?.ToList();
                
                if (fires == null || fires.Count == 0)
                {
                    // No fires - reset threat level
                    if (_lastThreatLevel != ThreatLevel.Low)
                    {
                        RimWatchLogger.Info("✅ FireAutomation: All fires extinguished");
                        _lastThreatLevel = ThreatLevel.Low;
                        
                        // Reset firefighting priorities to normal
                        ResetFirefightingPriorities(map);
                    }
                    return;
                }
                
                // Assess fire threat level
                FireThreat threat = AssessFireThreat(map, fires);
                
                // Log only if threat level changed or is high
                if (threat.Level != _lastThreatLevel || threat.Level >= ThreatLevel.High)
                {
                    RimWatchLogger.Warning($"🔥 FIRE DETECTED: {fires.Count} fires, threat level: {threat.Level}");
                }
                _lastThreatLevel = threat.Level;
                
                // Take action based on threat level
                if (threat.Level >= ThreatLevel.High)
                {
                    // EMERGENCY: All hands on deck!
                    AssignAllColonistsToFirefighting(map);
                }
                else if (threat.Level == ThreatLevel.Medium)
                {
                    // Assign nearby available colonists
                    AssignNearbyColonistsToFirefighting(map, fires);
                }
                else
                {
                    // Low threat: rely on automatic firefighting priority
                    EnsureFirefightingEnabled(map);
                }
                
                // Create firebreak zones if fire spreading rapidly
                if (threat.IsSpreadingRapidly && fires.Count > 10)
                {
                    RimWatchLogger.Warning("FireAutomation: Fire spreading rapidly! Consider manual intervention.");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FireAutomation: Error in AutoManageFires", ex);
            }
        }
        
        /// <summary>
        /// Оценивает угрозу пожара.
        /// </summary>
        private static FireThreat AssessFireThreat(Map map, List<Thing> fires)
        {
            FireThreat threat = new FireThreat();
            
            try
            {
                // Count fires near critical structures
                int firesNearBuildings = fires.Count(f =>
                {
                    var thingsAtPos = f.Position.GetThingList(map);
                    return thingsAtPos.Any(t => t is Building && t.def.category == ThingCategory.Building);
                });
                
                // Count fires near colonists
                int firesNearColonists = fires.Count(f =>
                    map.mapPawns.FreeColonistsSpawned.Any(p =>
                        p.Position.DistanceTo(f.Position) < 10f));
                
                // Assess spread rate (fires near flammable materials)
                int firesNearFlammables = fires.Count(f =>
                {
                    var thingsAtPos = f.Position.GetThingList(map);
                    return thingsAtPos.Any(t => t.def.BaseFlammability > 0.5f);
                });
                
                // Calculate threat level
                if (fires.Count > 20 || firesNearBuildings > 5 || firesNearColonists > 0)
                {
                    threat.Level = ThreatLevel.Critical;
                }
                else if (fires.Count > 10 || firesNearBuildings > 2)
                {
                    threat.Level = ThreatLevel.High;
                }
                else if (fires.Count > 5 || firesNearFlammables > 3)
                {
                    threat.Level = ThreatLevel.Medium;
                }
                else
                {
                    threat.Level = ThreatLevel.Low;
                }
                
                threat.IsSpreadingRapidly = firesNearFlammables > fires.Count * 0.5f;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FireAutomation: Error in AssessFireThreat", ex);
                threat.Level = ThreatLevel.High; // Assume high if error
            }
            
            return threat;
        }
        
        /// <summary>
        /// Назначает ВСЕХ колонистов на тушение пожара (критическая угроза).
        /// </summary>
        private static void AssignAllColonistsToFirefighting(Map map)
        {
            try
            {
                var colonists = map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.Downed && !p.Dead && !p.InMentalState)
                    .ToList();
                
                int assigned = 0;
                foreach (Pawn colonist in colonists)
                {
                    if (colonist.workSettings == null) continue;
                    
                    // Force firefighting priority to 1 (highest)
                    int currentPriority = colonist.workSettings.GetPriority(WorkTypeDefOf.Firefighter);
                    if (currentPriority != 1)
                    {
                        colonist.workSettings.SetPriority(WorkTypeDefOf.Firefighter, 1);
                        assigned++;
                    }
                    
                    // If drafted, undraft to allow firefighting
                    if (colonist.drafter?.Drafted == true)
                    {
                        colonist.drafter.Drafted = false;
                    }
                }
                
                if (assigned > 0)
                {
                    RimWatchLogger.Warning($"🚨 FireAutomation: CRITICAL FIRE! Assigned ALL {assigned} colonists to firefighting");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FireAutomation: Error in AssignAllColonistsToFirefighting", ex);
            }
        }
        
        /// <summary>
        /// Назначает ближайших доступных колонистов на тушение.
        /// </summary>
        private static void AssignNearbyColonistsToFirefighting(Map map, List<Thing> fires)
        {
            try
            {
                // Find fires center
                if (fires.Count == 0) return;
                
                IntVec3 fireCenter = new IntVec3(
                    (int)fires.Average(f => f.Position.x),
                    0,
                    (int)fires.Average(f => f.Position.z));
                
                // Get colonists sorted by distance to fire
                var colonists = map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.Downed && !p.Dead &&
                               !p.InMentalState &&
                               p.workSettings != null &&
                               p.workSettings.GetPriority(WorkTypeDefOf.Firefighter) > 0)
                    .OrderBy(p => p.Position.DistanceTo(fireCenter))
                    .ToList();
                
                if (colonists.Count == 0) return;
                
                // Assign closest 3-5 colonists
                int assignCount = Math.Min(colonists.Count, Math.Max(3, fires.Count / 5));
                
                int assigned = 0;
                for (int i = 0; i < assignCount; i++)
                {
                    Pawn colonist = colonists[i];
                    
                    // Set firefighting priority to 1
                    int currentPriority = colonist.workSettings.GetPriority(WorkTypeDefOf.Firefighter);
                    if (currentPriority != 1)
                    {
                        colonist.workSettings.SetPriority(WorkTypeDefOf.Firefighter, 1);
                        assigned++;
                    }
                    
                    // If drafted, undraft to allow firefighting
                    if (colonist.drafter?.Drafted == true)
                    {
                        colonist.drafter.Drafted = false;
                    }
                }
                
                if (assigned > 0)
                {
                    RimWatchLogger.Info($"🔥 FireAutomation: Assigned {assigned} nearby colonists to firefighting");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FireAutomation: Error in AssignNearbyColonistsToFirefighting", ex);
            }
        }
        
        /// <summary>
        /// Убеждается что у всех колонистов включено тушение пожаров.
        /// </summary>
        private static void EnsureFirefightingEnabled(Map map)
        {
            try
            {
                var colonists = map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.Downed && !p.Dead && p.workSettings != null)
                    .ToList();
                
                foreach (Pawn colonist in colonists)
                {
                    int currentPriority = colonist.workSettings.GetPriority(WorkTypeDefOf.Firefighter);
                    if (currentPriority == 0)
                    {
                        // Enable firefighting with priority 2 (after high-priority tasks)
                        colonist.workSettings.SetPriority(WorkTypeDefOf.Firefighter, 2);
                    }
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FireAutomation: Error in EnsureFirefightingEnabled", ex);
            }
        }
        
        /// <summary>
        /// Сбрасывает приоритеты тушения пожаров к нормальным значениям.
        /// </summary>
        private static void ResetFirefightingPriorities(Map map)
        {
            try
            {
                var colonists = map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.Downed && !p.Dead && p.workSettings != null)
                    .ToList();
                
                foreach (Pawn colonist in colonists)
                {
                    int currentPriority = colonist.workSettings.GetPriority(WorkTypeDefOf.Firefighter);
                    if (currentPriority == 1)
                    {
                        // Reset to priority 2 (normal)
                        colonist.workSettings.SetPriority(WorkTypeDefOf.Firefighter, 2);
                    }
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FireAutomation: Error in ResetFirefightingPriorities", ex);
            }
        }
    }
}

