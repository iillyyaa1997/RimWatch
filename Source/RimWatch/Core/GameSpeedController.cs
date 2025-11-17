using RimWatch.Utils;
using RimWorld;
using System;
using System.Linq;
using Verse;

namespace RimWatch.Core
{
    /// <summary>
    /// v0.8.0: Game Speed Controller - Adaptive speed and auto-unpause.
    /// Adjusts game speed based on colony activity (combat, construction, idle).
    /// </summary>
    public static class GameSpeedController
    {
        private static int _lastSpeedChangeTick = 0;
        private const int SpeedChangeInterval = 300; // 5 seconds between speed changes
        
        private static bool _userPausedGame = false;
        private static TimeSpeed _lastSpeed = TimeSpeed.Normal;
        
        /// <summary>
        /// Main tick - call from MapComponent.
        /// </summary>
        public static void Tick(Map map)
        {
            try
            {
                // v0.8.1: Check if enabled in settings
                if (!RimWatchMod.Settings.gameSpeedControlEnabled) return;
                
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - _lastSpeedChangeTick < SpeedChangeInterval) return;
                
                // Detect user pause
                if (Find.TickManager.Paused)
                {
                    if (!_userPausedGame)
                    {
                        _userPausedGame = true;
                        RimWatchLogger.Debug("GameSpeedController: User paused detected");
                    }
                    
                    // Auto-unpause if emergencies resolved (from settings)
                    if (RimWatchMod.Settings.autoUnpause && ShouldUnpause(map))
                    {
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                        // Note: Can't directly set Paused property, but changing speed will unpause
                        _userPausedGame = false;
                        RimWatchLogger.Info("⏯️ GameSpeedController: Auto-unpaused (emergency resolved)");
                    }
                    
                    return; // Don't change speed while paused
                }
                else
                {
                    _userPausedGame = false;
                }
                
                // Determine optimal speed
                TimeSpeed targetSpeed = DetermineOptimalSpeed(map);
                
                // Change speed if needed
                if (Find.TickManager.CurTimeSpeed != targetSpeed)
                {
                    _lastSpeed = Find.TickManager.CurTimeSpeed;
                    Find.TickManager.CurTimeSpeed = targetSpeed;
                    _lastSpeedChangeTick = currentTick;
                    
                    RimWatchLogger.Info($"🎮 GameSpeedController: Speed changed to {targetSpeed}");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"GameSpeedController: Error in Tick: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Determines optimal game speed based on current situation.
        /// </summary>
        private static TimeSpeed DetermineOptimalSpeed(Map map)
        {
            try
            {
                // v0.8.4+: NO DEBUG LOGS HERE - вызывается каждые 5 секунд, создаёт спам
                
                // 1. EMERGENCY: Medical/Fire - Normal or Pause
                if (HasMedicalEmergency(map))
                {
                    return TimeSpeed.Normal;
                }
                
                if (HasFireEmergency(map))
                {
                    return TimeSpeed.Normal;
                }
                
                // 2. COMBAT: Normal speed (from settings)
                if (HasEnemies(map))
                {
                    return RimWatchMod.Settings.combatSpeed;
                }
                
                // 3. ACTIVE WORK: Fast speed (from settings)
                if (HasActiveConstruction(map) || HasActiveHarvesting(map))
                {
                    return RimWatchMod.Settings.workSpeed;
                }
                
                // 4. IDLE: Ultrafast speed (from settings)
                return RimWatchMod.Settings.idleSpeed;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"GameSpeedController: Error determining speed: {ex.Message}");
                return TimeSpeed.Normal; // Safe fallback
            }
        }
        
        /// <summary>
        /// Check if game should auto-unpause.
        /// </summary>
        private static bool ShouldUnpause(Map map)
        {
            // Only unpause if no critical situations
            return !HasMedicalEmergency(map) && 
                   !HasFireEmergency(map) && 
                   !HasEnemies(map);
        }
        
        /// <summary>
        /// Check for medical emergencies (downed/bleeding colonists).
        /// </summary>
        private static bool HasMedicalEmergency(Map map)
        {
            return map.mapPawns.FreeColonistsSpawned
                .Any(p => p.Downed || p.health.hediffSet.BleedRateTotal > 0.1f);
        }
        
        /// <summary>
        /// Check for fire emergency (>10 fires).
        /// </summary>
        private static bool HasFireEmergency(Map map)
        {
            int fireCount = map.listerThings.ThingsInGroup(ThingRequestGroup.Fire).Count;
            return fireCount > 10;
        }
        
        /// <summary>
        /// Check for enemies on map.
        /// </summary>
        private static bool HasEnemies(Map map)
        {
            return map.mapPawns.AllPawnsSpawned
                .Any(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed);
        }
        
        /// <summary>
        /// Check for active construction.
        /// </summary>
        private static bool HasActiveConstruction(Map map)
        {
            // Check if any colonist is currently constructing
            return map.mapPawns.FreeColonistsSpawned
                .Any(p => p.CurJob != null && 
                         (p.CurJob.def == JobDefOf.FinishFrame ||
                          p.CurJob.def == JobDefOf.Repair ||
                          p.CurJob.def.defName.Contains("Construct")));
        }
        
        /// <summary>
        /// Check for active harvesting.
        /// </summary>
        private static bool HasActiveHarvesting(Map map)
        {
            // Check if any colonist is harvesting or hunting
            return map.mapPawns.FreeColonistsSpawned
                .Any(p => p.CurJob != null && 
                         (p.CurJob.def == JobDefOf.Harvest ||
                          p.CurJob.def == JobDefOf.Hunt ||
                          p.CurJob.def == JobDefOf.HarvestDesignated));
        }
    }
}

