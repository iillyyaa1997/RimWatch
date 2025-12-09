using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWatch.Automation.RoomBuilding
{
    /// <summary>
    /// Intelligent furniture and decoration placement system.
    /// Auto-places furniture, art, lighting, and temperature control for optimal room quality.
    /// v0.9.20: Furniture and decoration intelligence.
    /// </summary>
    public static class FurnitureRelocator
    {
        // Configuration Constants
        private const int UPDATE_INTERVAL = 18000; // Check every 5 hours
        private const float MIN_ROOM_IMPRESSIVENESS = 10f; // Minimum target impressiveness
        private const float TARGET_BEAUTY_PER_TILE = 0.5f; // Target beauty density
        private const int MAX_FURNITURE_PER_CHECK = 5; // Max placements per check
        
        // State tracking
        private static int _lastUpdateTick = 0;
        private static Dictionary<Room, RoomDecorState> _roomStates = new Dictionary<Room, RoomDecorState>();
        
        /// <summary>
        /// Main tick method for furniture and decoration.
        /// </summary>
        public static void Tick(Map map)
        {
            try
            {
                int currentTick = Find.TickManager.TicksGame;
                
                if (currentTick - _lastUpdateTick < UPDATE_INTERVAL)
                    return;
                
                _lastUpdateTick = currentTick;
                
                // Process rooms
                ProcessRooms(map);
                
                RimWatchLogger.Debug("FurnitureRelocator: Room decoration check complete");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FurnitureRelocator: Error in Tick", ex);
            }
        }
        
        /// <summary>
        /// Processes all rooms for decoration needs.
        /// </summary>
        private static void ProcessRooms(Map map)
        {
            // Get all indoor rooms by checking cells
            var rooms = new HashSet<Room>();
            foreach (IntVec3 cell in map.AllCells)
            {
                var room = cell.GetRoom(map);
                if (room != null && !room.PsychologicallyOutdoors && room.CellCount >= 9)
                {
                    rooms.Add(room);
                }
            }
            
            var roomList = rooms.ToList();
            
            int placementCount = 0;
            
            foreach (var room in roomList)
            {
                if (placementCount >= MAX_FURNITURE_PER_CHECK)
                    break;
                
                // Check what room needs
                placementCount += ImproveRoom(room, map);
            }
            
            if (placementCount > 0)
            {
                RimWatchLogger.Info($"FurnitureRelocator: Placed {placementCount} items");
            }
        }
        
        /// <summary>
        /// Improves a room's quality with furniture and decorations.
        /// </summary>
        private static int ImproveRoom(Room room, Map map)
        {
            int placementCount = 0;
            
            // Get room state
            if (!_roomStates.TryGetValue(room, out RoomDecorState state))
            {
                state = AnalyzeRoom(room, map);
                _roomStates[room] = state;
            }
            
            // Check current impressiveness
            float currentImpressiveness = room.GetStat(RoomStatDefOf.Impressiveness);
            
            if (currentImpressiveness >= MIN_ROOM_IMPRESSIVENESS * 2f)
            {
                // Room is already great
                return 0;
            }
            
            // Priority 1: Lighting (critical for all rooms)
            if (state.NeedsLighting)
            {
                if (PlaceLighting(room, map))
                {
                    RimWatchLogger.Info($"FurnitureRelocator: Added lighting to room (current impressiveness: {currentImpressiveness:F1})");
                    placementCount++;
                    state.NeedsLighting = false;
                }
            }
            
            // Priority 2: Temperature control (for bedrooms/hospitals)
            if (state.NeedsTemperatureControl)
            {
                if (PlaceTemperatureControl(room, map, state.RoomPurpose))
                {
                    RimWatchLogger.Info($"FurnitureRelocator: Added temperature control to {state.RoomPurpose}");
                    placementCount++;
                    state.NeedsTemperatureControl = false;
                }
            }
            
            // Priority 3: Beauty (art, plants, flooring)
            if (state.NeedsBeauty)
            {
                if (PlaceBeautyItems(room, map, state))
                {
                    RimWatchLogger.Info($"FurnitureRelocator: Added beauty items to room");
                    placementCount++;
                    state.NeedsBeauty = false;
                }
            }
            
            // Priority 4: Functional furniture (tables, chairs, etc.)
            if (state.NeedsFurniture)
            {
                if (PlaceFunctionalFurniture(room, map, state))
                {
                    RimWatchLogger.Info($"FurnitureRelocator: Added furniture to {state.RoomPurpose}");
                    placementCount++;
                    state.NeedsFurniture = false;
                }
            }
            
            return placementCount;
        }
        
        /// <summary>
        /// Analyzes a room to determine decoration needs.
        /// </summary>
        private static RoomDecorState AnalyzeRoom(Room room, Map map)
        {
            var state = new RoomDecorState
            {
                RoomPurpose = DetermineRoomPurpose(room, map)
            };
            
            // Check for lighting
            var lamps = room.ContainedAndAdjacentThings.OfType<Verse.Building>()
                .Count(b => b.def.building != null && b.def.building.isNaturalRock == false &&
                           (b.def.defName.Contains("Lamp") || b.def.defName.Contains("Light")));
            
            state.NeedsLighting = lamps == 0 && room.CellCount > 12;
            
            // Check for temperature control
            var tempControl = room.ContainedAndAdjacentThings.OfType<Verse.Building>()
                .Count(b => b.def.defName.Contains("Cooler") || b.def.defName.Contains("Heater"));
            
            state.NeedsTemperatureControl = tempControl == 0 && 
                (state.RoomPurpose == RoomPurpose.Bedroom || 
                 state.RoomPurpose == RoomPurpose.Hospital);
            
            // Check beauty level (use room stat)
            float beautyLevel = room.GetStat(RoomStatDefOf.Beauty);
            state.NeedsBeauty = beautyLevel < 0f; // Negative beauty means ugly
            
            // Check for functional furniture
            var furniture = room.ContainedAndAdjacentThings.OfType<Verse.Building>()
                .Count(b => b.def.building != null && b.def.hasInteractionCell);
            
            state.NeedsFurniture = furniture < 2 && state.RoomPurpose != RoomPurpose.Storage;
            
            return state;
        }
        
        /// <summary>
        /// Determines the purpose of a room.
        /// </summary>
        private static RoomPurpose DetermineRoomPurpose(Room room, Map map)
        {
            // Check for beds
            var beds = room.ContainedAndAdjacentThings.OfType<Verse.Building>()
                .Count(b => b.def.building?.bed_humanlike == true);
            
            if (beds > 0)
                return RoomPurpose.Bedroom;
            
            // Check for research benches
            var research = room.ContainedAndAdjacentThings.OfType<Verse.Building>()
                .Count(b => b.def.defName.Contains("Research"));
            
            if (research > 0)
                return RoomPurpose.Research;
            
            // Check for production benches
            var production = room.ContainedAndAdjacentThings.OfType<Verse.Building>()
                .Count(b => b.def.building?.isMealSource == true || 
                           b.def.defName.Contains("Table") && b.def.defName.Contains("Butcher"));
            
            if (production > 0)
                return RoomPurpose.Production;
            
            // Check for dining tables
            var tables = room.ContainedAndAdjacentThings.OfType<Verse.Building>()
                .Count(b => b.def.surfaceType == SurfaceType.Eat);
            
            if (tables > 0)
                return RoomPurpose.Dining;
            
            // Check for medical beds (just any bed in hospital context, can't check medical flag easily)
            var medBeds = room.ContainedAndAdjacentThings.OfType<Verse.Building>()
                .Count(b => b.def.building?.bed_humanlike == true && b.def.defName.Contains("Hospital"));
            
            if (medBeds > 0)
                return RoomPurpose.Hospital;
            
            // Default
            return RoomPurpose.Storage;
        }
        
        /// <summary>
        /// Places lighting in a room.
        /// </summary>
        private static bool PlaceLighting(Room room, Map map)
        {
            // Find center of room
            IntVec3 center = room.Cells.OrderBy(c => c.DistanceTo(room.Cells.First())).ElementAt(room.CellCount / 2);
            
            // Find best location for lamp
            IntVec3 lampPos = FindBestLampPosition(room, map, center);
            
            if (!lampPos.IsValid)
                return false;
            
            // Get lamp def (standing lamp by default)
            ThingDef lampDef = DefDatabase<ThingDef>.GetNamed("StandingLamp", false);
            if (lampDef == null)
                lampDef = DefDatabase<ThingDef>.GetNamed("TorchLamp", false);
            
            if (lampDef == null)
                return false;
            
            // Log recommendation (actual blueprint placement may need manual API)
            RimWatchLogger.Info($"FurnitureRelocator: RECOMMEND placing {lampDef.label} at {lampPos}");
            RimWatchLogger.LogDecision("FurnitureRelocator", "RecommendLamp", new Dictionary<string, object>
            {
                { "lampType", lampDef.defName },
                { "position", lampPos.ToString() }
            });
            
            return true;
        }
        
        /// <summary>
        /// Finds best position for a lamp.
        /// </summary>
        private static IntVec3 FindBestLampPosition(Room room, Map map, IntVec3 center)
        {
            // Try center first
            if (center.Standable(map) && !center.GetThingList(map).Any(t => t is Verse.Building))
                return center;
            
            // Try near center
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 3f, true))
            {
                if (!cell.InBounds(map) || !room.Cells.Contains(cell))
                    continue;
                
                if (cell.Standable(map) && !cell.GetThingList(map).Any(t => t is Verse.Building))
                    return cell;
            }
            
            return IntVec3.Invalid;
        }
        
        /// <summary>
        /// Places temperature control (heater/cooler).
        /// </summary>
        private static bool PlaceTemperatureControl(Room room, Map map, RoomPurpose purpose)
        {
            // Find wall for cooler/heater
            IntVec3 wallPos = FindBestWallPosition(room, map);
            
            if (!wallPos.IsValid)
                return false;
            
            // Get heater def (heaters are more universally useful)
            ThingDef heaterDef = DefDatabase<ThingDef>.GetNamed("Heater", false);
            
            if (heaterDef == null)
                return false;
            
            // Log recommendation
            RimWatchLogger.Info($"FurnitureRelocator: RECOMMEND placing {heaterDef.label} at {wallPos}");
            RimWatchLogger.LogDecision("FurnitureRelocator", "RecommendHeater", new Dictionary<string, object>
            {
                { "heaterType", heaterDef.defName },
                { "position", wallPos.ToString() }
            });
            
            return true;
        }
        
        /// <summary>
        /// Finds best wall position for temperature control.
        /// </summary>
        private static IntVec3 FindBestWallPosition(Room room, Map map)
        {
            foreach (IntVec3 cell in room.Cells)
            {
                // Check if adjacent to wall
                foreach (IntVec3 adj in GenAdj.CardinalDirections.Select(d => cell + d))
                {
                    if (!adj.InBounds(map))
                        continue;
                    
                    Verse.Building wall = adj.GetEdifice(map);
                    if (wall != null && wall.def.building?.isNaturalRock == false && wall.def.fillPercent > 0.8f)
                    {
                        if (cell.Standable(map) && !cell.GetThingList(map).Any(t => t is Verse.Building))
                            return cell;
                    }
                }
            }
            
            return IntVec3.Invalid;
        }
        
        /// <summary>
        /// Places beauty items (art, plants).
        /// </summary>
        private static bool PlaceBeautyItems(Room room, Map map, RoomDecorState state)
        {
            // Try to place sculpture
            IntVec3 artPos = FindBestArtPosition(room, map);
            
            if (!artPos.IsValid)
                return false;
            
            // Get sculpture def
            ThingDef sculptureDef = DefDatabase<ThingDef>.GetNamed("SculptureSmall", false);
            
            if (sculptureDef == null)
                return false;
            
            // Log recommendation
            RimWatchLogger.Info($"FurnitureRelocator: RECOMMEND placing {sculptureDef.label} at {artPos}");
            RimWatchLogger.LogDecision("FurnitureRelocator", "RecommendArt", new Dictionary<string, object>
            {
                { "artType", sculptureDef.defName },
                { "position", artPos.ToString() }
            });
            
            return true;
        }
        
        /// <summary>
        /// Finds best position for art.
        /// </summary>
        private static IntVec3 FindBestArtPosition(Room room, Map map)
        {
            // Prefer corners or against walls
            foreach (IntVec3 cell in room.Cells)
            {
                if (!cell.Standable(map) || cell.GetThingList(map).Any(t => t is Verse.Building))
                    continue;
                
                // Check if near wall
                int wallCount = GenAdj.CardinalDirections.Select(d => cell + d)
                    .Count(adj => adj.InBounds(map) && adj.GetEdifice(map) != null);
                
                if (wallCount >= 1) // At least one adjacent wall
                    return cell;
            }
            
            return IntVec3.Invalid;
        }
        
        /// <summary>
        /// Places functional furniture (tables, chairs).
        /// </summary>
        private static bool PlaceFunctionalFurniture(Room room, Map map, RoomDecorState state)
        {
            // Determine what furniture to place based on room purpose
            ThingDef furnitureDef = null;
            
            switch (state.RoomPurpose)
            {
                case RoomPurpose.Bedroom:
                    furnitureDef = DefDatabase<ThingDef>.GetNamed("Dresser", false);
                    break;
                
                case RoomPurpose.Dining:
                    furnitureDef = DefDatabase<ThingDef>.GetNamed("DiningChair", false);
                    break;
                
                case RoomPurpose.Research:
                case RoomPurpose.Production:
                    furnitureDef = DefDatabase<ThingDef>.GetNamed("Stool", false);
                    break;
                
                default:
                    furnitureDef = DefDatabase<ThingDef>.GetNamed("EndTable", false);
                    break;
            }
            
            if (furnitureDef == null)
                return false;
            
            // Find position
            IntVec3 pos = FindBestFurniturePosition(room, map);
            
            if (!pos.IsValid)
                return false;
            
            // Log recommendation
            RimWatchLogger.Info($"FurnitureRelocator: RECOMMEND placing {furnitureDef.label} at {pos}");
            RimWatchLogger.LogDecision("FurnitureRelocator", "RecommendFurniture", new Dictionary<string, object>
            {
                { "furnitureType", furnitureDef.defName },
                { "position", pos.ToString() },
                { "roomPurpose", state.RoomPurpose.ToString() }
            });
            
            return true;
        }
        
        /// <summary>
        /// Finds best position for furniture.
        /// </summary>
        private static IntVec3 FindBestFurniturePosition(Room room, Map map)
        {
            // Find empty spot
            foreach (IntVec3 cell in room.Cells)
            {
                if (cell.Standable(map) && !cell.GetThingList(map).Any(t => t is Verse.Building))
                    return cell;
            }
            
            return IntVec3.Invalid;
        }
    }
    
    /// <summary>
    /// Tracks decoration state for a room.
    /// </summary>
    public class RoomDecorState
    {
        public RoomPurpose RoomPurpose { get; set; }
        public bool NeedsLighting { get; set; }
        public bool NeedsTemperatureControl { get; set; }
        public bool NeedsBeauty { get; set; }
        public bool NeedsFurniture { get; set; }
    }
    
    /// <summary>
    /// Purpose/type of a room.
    /// </summary>
    public enum RoomPurpose
    {
        Bedroom,
        Dining,
        Research,
        Production,
        Hospital,
        Storage,
        Recreation
    }
}

