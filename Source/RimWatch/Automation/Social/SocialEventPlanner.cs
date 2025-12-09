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
    /// Plans and schedules social events (parties, gatherings) to maintain colony mood.
    /// v0.9.18: Social event planning and scheduling system.
    /// </summary>
    public static class SocialEventPlanner
    {
        // Configuration Constants
        private const int UPDATE_INTERVAL = 3600; // Check every 1 hour
        private const float LOW_MORALE_THRESHOLD = 0.40f; // Below 40% avg mood = low morale
        private const int MIN_DAYS_BETWEEN_PARTIES = 3; // Wait at least 3 days between parties
        private const int PARTY_COOLDOWN_TICKS = MIN_DAYS_BETWEEN_PARTIES * 60000; // 3 days in ticks
        private const float PARTY_COST_THRESHOLD = 100f; // Minimum silver value for party
        
        // State tracking
        private static int _lastUpdateTick = 0;
        private static int _lastPartyTick = 0;
        private static Dictionary<string, int> _eventHistory = new Dictionary<string, int>();
        
        /// <summary>
        /// Main tick method for social event planning.
        /// </summary>
        public static void Tick(Map map)
        {
            try
            {
                int currentTick = Find.TickManager.TicksGame;
                
                if (currentTick - _lastUpdateTick < UPDATE_INTERVAL)
                    return;
                
                _lastUpdateTick = currentTick;
                
                // Analyze colony morale
                float avgMood = CalculateAverageMood(map);
                int colonistsAtRisk = CountColonistsAtRisk(map);
                
                // Check if we should schedule an event
                if (ShouldScheduleEvent(map, avgMood, colonistsAtRisk, currentTick))
                {
                    ScheduleEvent(map, avgMood, colonistsAtRisk);
                }
                
                RimWatchLogger.Debug($"SocialEventPlanner: Avg mood {avgMood:P0}, {colonistsAtRisk} at risk");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("SocialEventPlanner: Error in Tick", ex);
            }
        }
        
        /// <summary>
        /// Calculates average colony mood.
        /// </summary>
        private static float CalculateAverageMood(Map map)
        {
            var colonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.needs?.mood != null)
                .ToList();
            
            if (colonists.Count == 0)
                return 0.5f;
            
            return colonists.Average(p => p.needs.mood.CurLevelPercentage);
        }
        
        /// <summary>
        /// Counts colonists at mental break risk.
        /// </summary>
        private static int CountColonistsAtRisk(Map map)
        {
            return map.mapPawns.FreeColonistsSpawned
                .Count(p => p.needs?.mood != null && 
                           p.needs.mood.CurLevelPercentage < 0.30f);
        }
        
        /// <summary>
        /// Determines if we should schedule a social event.
        /// </summary>
        private static bool ShouldScheduleEvent(Map map, float avgMood, int colonistsAtRisk, int currentTick)
        {
            // Check cooldown
            if (currentTick - _lastPartyTick < PARTY_COOLDOWN_TICKS)
            {
                RimWatchLogger.Debug($"SocialEventPlanner: Party on cooldown ({(PARTY_COOLDOWN_TICKS - (currentTick - _lastPartyTick)) / 60000} days remaining)");
                return false;
            }
            
            // Critical situation: multiple colonists at risk
            if (colonistsAtRisk >= 3)
            {
                RimWatchLogger.Info($"SocialEventPlanner: CRITICAL - {colonistsAtRisk} colonists at break risk, scheduling emergency party");
                return true;
            }
            
            // Low morale
            if (avgMood < LOW_MORALE_THRESHOLD)
            {
                RimWatchLogger.Info($"SocialEventPlanner: Low morale ({avgMood:P0}), scheduling party");
                return true;
            }
            
            // Preventive party (every ~7 days if mood is OK)
            if (currentTick - _lastPartyTick > 7 * 60000 && avgMood < 0.70f)
            {
                RimWatchLogger.Info($"SocialEventPlanner: Preventive party (last party was {(currentTick - _lastPartyTick) / 60000} days ago)");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Schedules a social event.
        /// </summary>
        private static void ScheduleEvent(Map map, float avgMood, int colonistsAtRisk)
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // Determine event type
            EventType eventType = DetermineEventType(map, avgMood, colonistsAtRisk);
            
            // Check resources
            if (!HasSufficientResources(map, eventType))
            {
                RimWatchLogger.Warning($"SocialEventPlanner: Insufficient resources for {eventType}");
                return;
            }
            
            // Find suitable location
            IntVec3 location = FindEventLocation(map, eventType);
            if (!location.IsValid)
            {
                RimWatchLogger.Warning($"SocialEventPlanner: No suitable location for {eventType}");
                return;
            }
            
            // Schedule the event
            bool success = TryScheduleEvent(map, eventType, location);
            
            if (success)
            {
                _lastPartyTick = currentTick;
                _eventHistory[eventType.ToString()] = currentTick;
                
                RimWatchLogger.Info($"SocialEventPlanner: Scheduled {eventType} at {location} (Mood: {avgMood:P0}, At Risk: {colonistsAtRisk})");
                
                RimWatchLogger.LogDecision("SocialEventPlanner", "ScheduleEvent", new Dictionary<string, object>
                {
                    { "eventType", eventType.ToString() },
                    { "avgMood", avgMood },
                    { "colonistsAtRisk", colonistsAtRisk },
                    { "location", location.ToString() }
                });
            }
            else
            {
                RimWatchLogger.Warning($"SocialEventPlanner: Failed to schedule {eventType}");
            }
        }
        
        /// <summary>
        /// Determines the best event type for current situation.
        /// </summary>
        private static EventType DetermineEventType(Map map, float avgMood, int colonistsAtRisk)
        {
            // Critical situation = immediate party
            if (colonistsAtRisk >= 3 || avgMood < 0.30f)
                return EventType.Party;
            
            // Low morale = party
            if (avgMood < LOW_MORALE_THRESHOLD)
                return EventType.Party;
            
            // Default = gathering
            return EventType.Gathering;
        }
        
        /// <summary>
        /// Checks if map has sufficient resources for event.
        /// </summary>
        private static bool HasSufficientResources(Map map, EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Party:
                    // Check for food and recreation
                    int mealCount = map.resourceCounter.GetCount(ThingDefOf.MealSimple) +
                                   map.resourceCounter.GetCount(ThingDefOf.MealFine);
                    
                    int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
                    
                    // Need at least 2 meals per colonist for party
                    if (mealCount < colonistCount * 2)
                    {
                        RimWatchLogger.Debug($"SocialEventPlanner: Insufficient food for party ({mealCount} meals, need {colonistCount * 2})");
                        return false;
                    }
                    
                    // Check for recreation items
                    var recreationBuildings = map.listerBuildings.allBuildingsColonist
                        .Where(b => b.def.building?.artificialForMeditationPurposes == false &&
                                   (b.def.defName.Contains("Recreation") || 
                                    b.def.defName.Contains("Joy") ||
                                    b.def.defName.Contains("Chess") ||
                                    b.def.defName.Contains("Horseshoe")))
                        .ToList();
                    
                    if (recreationBuildings.Count == 0)
                    {
                        RimWatchLogger.Debug("SocialEventPlanner: No recreation buildings for party");
                        return false;
                    }
                    
                    return true;
                
                case EventType.Gathering:
                    // Gatherings need less resources
                    return true;
                
                default:
                    return true;
            }
        }
        
        /// <summary>
        /// Finds suitable location for event.
        /// </summary>
        private static IntVec3 FindEventLocation(Map map, EventType eventType)
        {
            // Simply find any indoor area with good impressiveness
            var indoorCells = map.AllCells
                .Where(c => c.Roofed(map) && c.Standable(map))
                .ToList();
            
            if (indoorCells.Count > 0)
            {
                // Try to find best cell by checking room quality
                var cellsByRoom = indoorCells
                    .GroupBy(c => c.GetRoom(map))
                    .Where(g => g.Key != null && !g.Key.PsychologicallyOutdoors)
                    .OrderByDescending(g => g.Key.GetStat(RoomStatDefOf.Impressiveness))
                    .ToList();
                
                if (cellsByRoom.Count > 0)
                {
                    var bestRoomCells = cellsByRoom[0].Where(c => c.Standable(map) && !c.GetThingList(map).Any(t => t is Building)).ToList();
                    if (bestRoomCells.Count > 0)
                        return bestRoomCells[bestRoomCells.Count / 2]; // Center of best room
                }
                
                // Fallback: Any indoor cell
                return indoorCells[Rand.Range(0, indoorCells.Count)];
            }
            
            // Last resort: Any standable cell
            var anyCells = map.AllCells.Where(c => c.Standable(map)).ToList();
            if (anyCells.Count > 0)
            {
                return anyCells[Rand.Range(0, anyCells.Count)];
            }
            
            return IntVec3.Invalid;
        }
        
        /// <summary>
        /// Attempts to schedule the event via game systems.
        /// </summary>
        private static bool TryScheduleEvent(Map map, EventType eventType, IntVec3 location)
        {
            // Note: RimWorld doesn't have a direct API to force parties
            // We can only recommend or trigger via letters
            
            // For now, we'll log the recommendation
            // In future, could try to manipulate LordManager or create custom incidents
            
            RimWatchLogger.Info($"🎉 SocialEventPlanner: RECOMMEND {eventType} at {location}");
            RimWatchLogger.Info($"   → Ensure colonists have recreation time scheduled");
            RimWatchLogger.Info($"   → Food and recreation facilities available");
            
            // TODO: Actually trigger party via game systems
            // This would require:
            // 1. Creating a Lord for the party
            // 2. Assigning colonists to party duties
            // 3. Managing party duration and effects
            
            return true; // For now, just log the recommendation
        }
        
        /// <summary>
        /// Gets upcoming events info for UI.
        /// </summary>
        public static List<EventInfo> GetUpcomingEvents()
        {
            List<EventInfo> events = new List<EventInfo>();
            
            int currentTick = Find.TickManager.TicksGame;
            int ticksUntilNextParty = PARTY_COOLDOWN_TICKS - (currentTick - _lastPartyTick);
            
            if (ticksUntilNextParty > 0)
            {
                events.Add(new EventInfo
                {
                    EventName = "Next Party Available",
                    ScheduledIn = ticksUntilNextParty,
                    Status = "Cooldown"
                });
            }
            else
            {
                events.Add(new EventInfo
                {
                    EventName = "Party Available Now",
                    ScheduledIn = 0,
                    Status = "Ready"
                });
            }
            
            return events;
        }
    }
    
    /// <summary>
    /// Types of social events.
    /// </summary>
    public enum EventType
    {
        Party,
        Gathering,
        Feast
    }
    
    /// <summary>
    /// Public event info for UI.
    /// </summary>
    public class EventInfo
    {
        public string EventName { get; set; }
        public int ScheduledIn { get; set; } // Ticks
        public string Status { get; set; }
    }
}

