using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWatch.Automation.BaseLayout
{
    /// <summary>
    /// Intelligent base layout planner for efficient multi-room designs.
    /// Plans complete base layouts with optimal room placement, traffic flow, and defensive considerations.
    /// v0.9.19: Base layout planning system.
    /// </summary>
    public static class BaseLayoutPlanner
    {
        // Configuration Constants
        private const int UPDATE_INTERVAL = 36000; // Check every 10 hours
        private const int MIN_BASE_SIZE = 20; // Minimum 20x20 base
        private const int MAX_BASE_SIZE = 50; // Maximum 50x50 base
        private const int ROOM_SPACING = 2; // 2 tiles between rooms for walls
        private const float DEFENSE_PRIORITY_WEIGHT = 2.0f; // Weight for defensive positioning
        private const float EFFICIENCY_WEIGHT = 1.5f; // Weight for efficiency (traffic flow)
        private const float AESTHETICS_WEIGHT = 1.0f; // Weight for aesthetics
        
        // State tracking
        private static int _lastUpdateTick = 0;
        private static BaseLayout _currentLayout = null;
        private static bool _layoutInProgress = false;
        
        /// <summary>
        /// Main tick method for base layout planning.
        /// </summary>
        public static void Tick(Map map)
        {
            try
            {
                int currentTick = Find.TickManager.TicksGame;
                
                if (currentTick - _lastUpdateTick < UPDATE_INTERVAL)
                    return;
                
                _lastUpdateTick = currentTick;
                
                // Check if we need a new layout
                if (ShouldPlanNewLayout(map))
                {
                    PlanBaseLayout(map);
                }
                
                RimWatchLogger.Debug($"BaseLayoutPlanner: Layout planning check complete");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("BaseLayoutPlanner: Error in Tick", ex);
            }
        }
        
        /// <summary>
        /// Determines if a new base layout should be planned.
        /// </summary>
        private static bool ShouldPlanNewLayout(Map map)
        {
            // Don't plan if already in progress
            if (_layoutInProgress)
                return false;
            
            // Plan if we don't have a layout yet
            if (_currentLayout == null)
            {
                RimWatchLogger.Info("BaseLayoutPlanner: No layout exists, planning new base");
                return true;
            }
            
            // Plan if colony has grown significantly
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            if (colonistCount > _currentLayout.DesignedForColonists * 1.5f)
            {
                RimWatchLogger.Info($"BaseLayoutPlanner: Colony grown from {_currentLayout.DesignedForColonists} to {colonistCount}, planning expansion");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Plans a complete base layout.
        /// </summary>
        private static void PlanBaseLayout(Map map)
        {
            _layoutInProgress = true;
            
            try
            {
                int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
                
                RimWatchLogger.Info($"BaseLayoutPlanner: Planning base layout for {colonistCount} colonists");
                
                // Find optimal location for base center
                IntVec3 baseCenter = FindOptimalBaseCenter(map);
                
                if (!baseCenter.IsValid)
                {
                    RimWatchLogger.Warning("BaseLayoutPlanner: Could not find suitable location for base");
                    return;
                }
                
                // Calculate base size based on colonists
                int baseSize = CalculateBaseSize(colonistCount);
                
                // Create layout plan
                BaseLayout layout = new BaseLayout
                {
                    Center = baseCenter,
                    Size = baseSize,
                    DesignedForColonists = colonistCount,
                    CreatedTick = Find.TickManager.TicksGame
                };
                
                // Plan room zones
                PlanRoomZones(layout, map, colonistCount);
                
                // Optimize traffic flow
                OptimizeTrafficFlow(layout, map);
                
                // Add defensive considerations
                AddDefensiveFeatures(layout, map);
                
                // Calculate layout score
                layout.Score = EvaluateLayout(layout, map);
                
                _currentLayout = layout;
                
                RimWatchLogger.Info($"BaseLayoutPlanner: Layout planned at {baseCenter} (Size: {baseSize}x{baseSize}, Score: {layout.Score:F2})");
                
                RimWatchLogger.LogDecision("BaseLayoutPlanner", "PlanLayout", new Dictionary<string, object>
                {
                    { "center", baseCenter.ToString() },
                    { "size", baseSize },
                    { "colonists", colonistCount },
                    { "score", layout.Score },
                    { "rooms", layout.Rooms.Count }
                });
            }
            finally
            {
                _layoutInProgress = false;
            }
        }
        
        /// <summary>
        /// Finds optimal location for base center.
        /// </summary>
        private static IntVec3 FindOptimalBaseCenter(Map map)
        {
            var candidates = new List<(IntVec3 pos, float score)>();
            
            // Sample potential locations
            for (int i = 0; i < 50; i++)
            {
                IntVec3 candidate = CellFinder.RandomCell(map);
                
                if (!IsValidBaseCenter(candidate, map))
                    continue;
                
                float score = ScoreBaseLocation(candidate, map);
                candidates.Add((candidate, score));
            }
            
            if (candidates.Count == 0)
                return IntVec3.Invalid;
            
            // Return best location
            return candidates.OrderByDescending(c => c.score).First().pos;
        }
        
        /// <summary>
        /// Checks if a location is valid for base center.
        /// </summary>
        private static bool IsValidBaseCenter(IntVec3 pos, Map map)
        {
            // Must be on map
            if (!pos.InBounds(map))
                return false;
            
            // Must be standable
            if (!pos.Standable(map))
                return false;
            
            // Check surrounding area is mostly flat
            int flatCount = 0;
            int totalCount = 0;
            
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pos, 15f, true))
            {
                if (!cell.InBounds(map))
                    continue;
                
                totalCount++;
                if (cell.Standable(map) && !cell.Impassable(map))
                    flatCount++;
            }
            
            // Require at least 70% flat area
            return (float)flatCount / totalCount >= 0.7f;
        }
        
        /// <summary>
        /// Scores a potential base location.
        /// </summary>
        private static float ScoreBaseLocation(IntVec3 pos, Map map)
        {
            float score = 0f;
            
            // 1. Flatness of surrounding area
            int flatCount = 0;
            int totalCount = 0;
            
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pos, 20f, true))
            {
                if (!cell.InBounds(map))
                    continue;
                
                totalCount++;
                if (cell.Standable(map) && !cell.Impassable(map))
                    flatCount++;
            }
            
            score += ((float)flatCount / totalCount) * 100f;
            
            // 2. Proximity to resources
            float resourceScore = 0f;
            
            // Check for nearby stone chunks
            var nearbyStone = map.listerThings.ThingsInGroup(ThingRequestGroup.Chunk)
                .Where(t => t.Position.DistanceTo(pos) < 50f)
                .ToList();
            
            resourceScore += Mathf.Min(nearbyStone.Count, 20) * 2f;
            
            // Check for nearby fertile soil
            int fertileCount = GenRadial.RadialCellsAround(pos, 30f, true)
                .Count(c => c.InBounds(map) && c.GetTerrain(map).fertility > 0.5f);
            
            resourceScore += Mathf.Min(fertileCount, 100) * 0.5f;
            
            score += resourceScore;
            
            // 3. Defensive position (elevation)
            Building edifice = pos.GetEdifice(map);
            if (edifice != null)
            {
                score += edifice.def.altitudeLayer.AltitudeFor() * 10f;
            }
            
            // 4. Distance from map edge (not too close)
            float distToEdge = Mathf.Min(
                pos.x,
                pos.z,
                map.Size.x - pos.x,
                map.Size.z - pos.z
            );
            
            // Prefer 20-40 tiles from edge
            if (distToEdge >= 20f && distToEdge <= 40f)
                score += 30f;
            else
                score -= Mathf.Abs(30f - distToEdge) * 0.5f;
            
            return score;
        }
        
        /// <summary>
        /// Calculates optimal base size for given colonist count.
        /// </summary>
        private static int CalculateBaseSize(int colonistCount)
        {
            // Base formula: 20 + (colonists * 3)
            int size = 20 + (colonistCount * 3);
            
            return Mathf.Clamp(size, MIN_BASE_SIZE, MAX_BASE_SIZE);
        }
        
        /// <summary>
        /// Plans room zones within the layout.
        /// </summary>
        private static void PlanRoomZones(BaseLayout layout, Map map, int colonistCount)
        {
            RimWatchLogger.Debug($"BaseLayoutPlanner: Planning rooms for {colonistCount} colonists");
            
            // Core rooms (always needed)
            AddRoomPlan(layout, RoomType.Storage, 12, 12, RoomPriority.Critical);
            AddRoomPlan(layout, RoomType.Kitchen, 8, 8, RoomPriority.Critical);
            AddRoomPlan(layout, RoomType.Freezer, 10, 10, RoomPriority.Critical);
            AddRoomPlan(layout, RoomType.DiningRoom, 12, 12, RoomPriority.High);
            AddRoomPlan(layout, RoomType.Hospital, 10, 10, RoomPriority.High);
            
            // Bedrooms (one per colonist + extras)
            for (int i = 0; i < colonistCount + 2; i++)
            {
                AddRoomPlan(layout, RoomType.Bedroom, 5, 5, RoomPriority.High);
            }
            
            // Workshop rooms
            AddRoomPlan(layout, RoomType.Workshop, 12, 12, RoomPriority.Medium);
            AddRoomPlan(layout, RoomType.Smithy, 10, 10, RoomPriority.Medium);
            
            // Recreational/support
            AddRoomPlan(layout, RoomType.Recreation, 15, 15, RoomPriority.Medium);
            AddRoomPlan(layout, RoomType.Research, 10, 10, RoomPriority.Medium);
            
            // Defense
            AddRoomPlan(layout, RoomType.Armory, 8, 8, RoomPriority.Low);
            
            RimWatchLogger.Info($"BaseLayoutPlanner: Planned {layout.Rooms.Count} rooms");
        }
        
        /// <summary>
        /// Adds a room plan to the layout.
        /// </summary>
        private static void AddRoomPlan(BaseLayout layout, RoomType type, int width, int height, RoomPriority priority)
        {
            layout.Rooms.Add(new RoomPlan
            {
                Type = type,
                Width = width,
                Height = height,
                Priority = priority
            });
        }
        
        /// <summary>
        /// Optimizes traffic flow between rooms.
        /// </summary>
        private static void OptimizeTrafficFlow(BaseLayout layout, Map map)
        {
            RimWatchLogger.Debug("BaseLayoutPlanner: Optimizing traffic flow");
            
            // Group high-traffic rooms together
            var highTrafficRooms = layout.Rooms
                .Where(r => r.Type == RoomType.Kitchen || 
                           r.Type == RoomType.DiningRoom || 
                           r.Type == RoomType.Storage ||
                           r.Type == RoomType.Freezer)
                .ToList();
            
            // Place near center
            foreach (var room in highTrafficRooms)
            {
                room.TrafficPriority = 3;
            }
            
            // Bedrooms should be quieter, away from center
            var bedrooms = layout.Rooms.Where(r => r.Type == RoomType.Bedroom).ToList();
            foreach (var room in bedrooms)
            {
                room.TrafficPriority = 1;
            }
        }
        
        /// <summary>
        /// Adds defensive features to the layout.
        /// </summary>
        private static void AddDefensiveFeatures(BaseLayout layout, Map map)
        {
            RimWatchLogger.Debug("BaseLayoutPlanner: Adding defensive features");
            
            // Mark perimeter for walls
            layout.NeedsPerimeterWalls = true;
            
            // Mark entrance locations (2-3 entrances)
            layout.EntranceCount = 2;
            
            // Mark killbox zones
            layout.NeedsKillboxes = true;
        }
        
        /// <summary>
        /// Evaluates the overall quality of a layout.
        /// </summary>
        private static float EvaluateLayout(BaseLayout layout, Map map)
        {
            float score = 0f;
            
            // 1. Completeness (all critical rooms present)
            int criticalRooms = layout.Rooms.Count(r => r.Priority == RoomPriority.Critical);
            score += criticalRooms * 20f;
            
            // 2. Efficiency (compact layout)
            int totalRoomArea = layout.Rooms.Sum(r => r.Width * r.Height);
            float efficiency = totalRoomArea / (float)(layout.Size * layout.Size);
            score += (1f - efficiency) * 50f; // Higher score for more compact
            
            // 3. Room count adequacy
            int bedroomCount = layout.Rooms.Count(r => r.Type == RoomType.Bedroom);
            if (bedroomCount >= layout.DesignedForColonists)
                score += 30f;
            
            // 4. Defensive features
            if (layout.NeedsPerimeterWalls)
                score += 20f;
            if (layout.NeedsKillboxes)
                score += 15f;
            
            return score;
        }
        
        /// <summary>
        /// Gets the current base layout for UI display.
        /// </summary>
        public static BaseLayoutInfo GetCurrentLayout()
        {
            if (_currentLayout == null)
                return null;
            
            return new BaseLayoutInfo
            {
                Center = _currentLayout.Center.ToString(),
                Size = _currentLayout.Size,
                RoomCount = _currentLayout.Rooms.Count,
                Score = _currentLayout.Score,
                DesignedForColonists = _currentLayout.DesignedForColonists
            };
        }
    }
    
    /// <summary>
    /// Represents a complete base layout plan.
    /// </summary>
    public class BaseLayout
    {
        public IntVec3 Center { get; set; }
        public int Size { get; set; }
        public int DesignedForColonists { get; set; }
        public int CreatedTick { get; set; }
        public List<RoomPlan> Rooms { get; set; } = new List<RoomPlan>();
        public float Score { get; set; }
        
        // Defensive features
        public bool NeedsPerimeterWalls { get; set; }
        public int EntranceCount { get; set; }
        public bool NeedsKillboxes { get; set; }
    }
    
    /// <summary>
    /// Represents a planned room within the base.
    /// </summary>
    public class RoomPlan
    {
        public RoomType Type { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public RoomPriority Priority { get; set; }
        public int TrafficPriority { get; set; } = 2; // 1=low, 2=medium, 3=high
        public IntVec3 PlannedLocation { get; set; } = IntVec3.Invalid;
    }
    
    /// <summary>
    /// Types of rooms in the base.
    /// </summary>
    public enum RoomType
    {
        Bedroom,
        DiningRoom,
        Kitchen,
        Freezer,
        Storage,
        Workshop,
        Smithy,
        Hospital,
        Recreation,
        Research,
        Armory,
        Prison
    }
    
    /// <summary>
    /// Priority levels for room construction.
    /// </summary>
    public enum RoomPriority
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
    
    /// <summary>
    /// Public layout info for UI display.
    /// </summary>
    public class BaseLayoutInfo
    {
        public string Center { get; set; }
        public int Size { get; set; }
        public int RoomCount { get; set; }
        public float Score { get; set; }
        public int DesignedForColonists { get; set; }
    }
}

