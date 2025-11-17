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
    /// Monitors colonist activity and assigns productive work to idle colonists.
    /// Ensures colonists always have something meaningful to do.
    /// </summary>
    public static class ColonistActivityMonitor
    {
        // Track idle colonists to avoid spam
        private static Dictionary<Pawn, int> _idleSinceTick = new Dictionary<Pawn, int>();
        private static Dictionary<Pawn, int> _lastPriorityChangeTick = new Dictionary<Pawn, int>();
        private const int IdleThresholdTicks = 300; // 5 seconds
        private const int PriorityChangeCooldown = 1800; // 30 seconds

        /// <summary>
        /// Monitors all colonist activity and assigns work to idle colonists.
        /// Should be called every 5 seconds (250 ticks).
        /// </summary>
        public static void MonitorColonistActivity(Map map)
        {
            try
            {
                List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
                int currentTick = Find.TickManager.TicksGame;

                foreach (Pawn colonist in colonists)
                {
                    if (colonist.Dead || colonist.Downed || colonist.InMentalState)
                        continue;

                    // Check if colonist is idle
                    if (IsIdle(colonist))
                    {
                        // Track how long they've been idle
                        if (!_idleSinceTick.ContainsKey(colonist))
                        {
                            _idleSinceTick[colonist] = currentTick;
                        }

                        int idleDuration = currentTick - _idleSinceTick[colonist];

                        // Only intervene if idle for more than threshold
                        if (idleDuration >= IdleThresholdTicks)
                        {
                            RimWatchLogger.Debug($"ColonistActivityMonitor: {colonist.LabelShort} idle for {idleDuration} ticks");
                            AssignProductiveWork(colonist, map, currentTick);
                        }
                    }
                    else
                    {
                        // Colonist is busy - clear idle tracking
                        if (_idleSinceTick.ContainsKey(colonist))
                        {
                            _idleSinceTick.Remove(colonist);
                        }
                    }
                }

                // Cleanup old entries
                _idleSinceTick = _idleSinceTick.Where(kvp => colonists.Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ColonistActivityMonitor: Error in MonitorColonistActivity", ex);
            }
        }

        /// <summary>
        /// Checks if a colonist is idle (no job or wandering).
        /// </summary>
        private static bool IsIdle(Pawn pawn)
        {
            try
            {
                // Check current job
                Job currentJob = pawn.jobs?.curJob;
                
                if (currentJob == null)
                    return true;

                // Check for idle job types
                string jobDefName = currentJob.def?.defName?.ToLower() ?? "";
                
                // Common idle jobs
                if (jobDefName.Contains("wait") || 
                    jobDefName.Contains("wander") || 
                    jobDefName.Contains("idle") ||
                    jobDefName == "gotostandby")
                {
                    return true;
                }

                // Check if pawn thinks it's idle
                if (pawn.mindState?.IsIdle == true)
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Assigns productive work to an idle colonist based on colony needs.
        /// </summary>
        private static void AssignProductiveWork(Pawn pawn, Map map, int currentTick)
        {
            try
            {
                // Check cooldown
                if (_lastPriorityChangeTick.ContainsKey(pawn))
                {
                    int ticksSince = currentTick - _lastPriorityChangeTick[pawn];
                    if (ticksSince < PriorityChangeCooldown)
                    {
                        return; // Too soon to change priorities again
                    }
                }

                // Priority 1: EMERGENCY - Colonists sleeping outside
                if (AnyColonistSleepingOutside(map))
                {
                    // v0.8.2: Use throttled warning to prevent spam (warn once per minute)
                    RimWatchLogger.WarningThrottledByKey("activity_sleeping_outside", $"ColonistActivityMonitor: EMERGENCY - Colonists sleeping outside! Assigning {pawn.LabelShort} to construction");
                    SetWorkPriority(pawn, WorkTypeDefOf.Construction, 1);
                    _lastPriorityChangeTick[pawn] = currentTick;
                    return;
                }

                // Priority 2: Unfinished construction (blueprints/frames exist)
                if (HasUnfinishedConstruction(map))
                {
                    RimWatchLogger.Info($"ColonistActivityMonitor: Assigning {pawn.LabelShort} to unfinished construction");
                    SetWorkPriority(pawn, WorkTypeDefOf.Construction, 2);
                    _lastPriorityChangeTick[pawn] = currentTick;
                    return;
                }

                // Priority 3: Unharvested mature crops
                if (HasMatureCrops(map))
                {
                    RimWatchLogger.Info($"ColonistActivityMonitor: Assigning {pawn.LabelShort} to harvest mature crops");
                    SetWorkPriority(pawn, WorkTypeDefOf.Growing, 2);
                    _lastPriorityChangeTick[pawn] = currentTick;
                    return;
                }

                // Priority 4: Research (if bench available and colonist is smart)
                if (HasResearchBench(map) && !IsDumbLabor(pawn))
                {
                    RimWatchLogger.Info($"ColonistActivityMonitor: Assigning {pawn.LabelShort} to research");
                    SetWorkPriority(pawn, WorkTypeDefOf.Research, 2);
                    _lastPriorityChangeTick[pawn] = currentTick;
                    return;
                }

                // Priority 5: Hauling (scattered items need organizing)
                if (HasItemsToHaul(map))
                {
                    RimWatchLogger.Info($"ColonistActivityMonitor: Assigning {pawn.LabelShort} to hauling");
                    SetWorkPriority(pawn, WorkTypeDefOf.Hauling, 2);
                    _lastPriorityChangeTick[pawn] = currentTick;
                    return;
                }

                // Priority 6: Cleaning (if nothing else to do)
                RimWatchLogger.Debug($"ColonistActivityMonitor: Assigning {pawn.LabelShort} to cleaning");
                SetWorkPriority(pawn, WorkTypeDefOf.Cleaning, 3);
                _lastPriorityChangeTick[pawn] = currentTick;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"ColonistActivityMonitor: Error assigning work to {pawn.LabelShort}", ex);
            }
        }

        /// <summary>
        /// Sets work priority for a colonist (respects their capabilities).
        /// </summary>
        private static void SetWorkPriority(Pawn pawn, WorkTypeDef workType, int priority)
        {
            try
            {
                if (pawn.workSettings == null)
                    return;

                // Check if colonist can do this work
                if (pawn.WorkTypeIsDisabled(workType))
                {
                    RimWatchLogger.Debug($"ColonistActivityMonitor: {pawn.LabelShort} cannot do {workType.labelShort} (disabled)");
                    return;
                }

                int oldPriority = pawn.workSettings.GetPriority(workType);
                pawn.workSettings.SetPriority(workType, priority);
                
                RimWatchLogger.Info($"👷 ColonistActivityMonitor: {pawn.LabelShort} - {workType.labelShort}: {oldPriority} → {priority}");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"ColonistActivityMonitor: Error setting work priority for {pawn.LabelShort}", ex);
            }
        }

        /// <summary>
        /// Checks if any colonists are sleeping outside (no roof).
        /// </summary>
        private static bool AnyColonistSleepingOutside(Map map)
        {
            try
            {
                foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
                {
                    Building_Bed bed = colonist.ownership?.OwnedBed;
                    
                    if (bed == null || !bed.Position.Roofed(map))
                    {
                        return true; // Colonist has no roofed bed!
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if there are unfinished construction projects.
        /// </summary>
        private static bool HasUnfinishedConstruction(Map map)
        {
            try
            {
                // Check for blueprints
                int blueprintCount = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).Count;
                if (blueprintCount > 0)
                    return true;

                // Check for frames under construction
                int frameCount = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).Count;
                if (frameCount > 0)
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if there are mature crops ready for harvest.
        /// </summary>
        private static bool HasMatureCrops(Map map)
        {
            try
            {
                List<Zone> growingZones = map.zoneManager.AllZones
                    .Where(z => z is Zone_Growing)
                    .ToList();

                foreach (Zone_Growing zone in growingZones.Cast<Zone_Growing>())
                {
                    foreach (IntVec3 cell in zone.Cells)
                    {
                        Plant plant = cell.GetPlant(map);
                        if (plant != null && plant.HarvestableNow)
                        {
                            return true; // Found mature crop!
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if colony has a research bench.
        /// </summary>
        private static bool HasResearchBench(Map map)
        {
            try
            {
                return RimWatch.Automation.BuildingPlacement.BuildingClassifier.ColonyHasBuilding(
                    map,
                    RimWatch.Automation.BuildingPlacement.BuildingClassifier.BuildingCategory.ResearchBench);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if colonist is suitable for intellectual work (not "dumb labor").
        /// </summary>
        private static bool IsDumbLabor(Pawn pawn)
        {
            try
            {
                // Check intellectual skill
                SkillRecord intellectualSkill = pawn.skills?.GetSkill(SkillDefOf.Intellectual);
                
                if (intellectualSkill == null)
                    return true;

                // If intellectual is disabled or very low, consider "dumb labor"
                if (intellectualSkill.TotallyDisabled || intellectualSkill.Level < 3)
                    return true;

                return false;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Checks if there are items scattered on the ground that need hauling.
        /// </summary>
        private static bool HasItemsToHaul(Map map)
        {
            try
            {
                // Count items in home area that should be hauled
                List<Thing> items = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
                
                int unhauledCount = 0;
                foreach (Thing item in items)
                {
                    // Only count items in home area and not forbidden
                    if (map.areaManager.Home[item.Position] && !item.IsForbidden(Faction.OfPlayer))
                    {
                        unhauledCount++;
                        
                        if (unhauledCount > 5) // At least 5 items need hauling
                            return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Clears tracking data (call on game load).
        /// </summary>
        public static void ClearTracking()
        {
            _idleSinceTick.Clear();
            _lastPriorityChangeTick.Clear();
            RimWatchLogger.Info("ColonistActivityMonitor: Tracking data cleared");
        }
    }
}

