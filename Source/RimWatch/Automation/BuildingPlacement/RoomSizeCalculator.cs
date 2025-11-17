using RimWatch.Automation.ColonyDevelopment;
using Verse;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// v0.8.4: Calculates optimal room sizes based on room type, colonist count, and development stage.
    /// Based on RimWorld community best practices and wiki guides.
    /// </summary>
    public static class RoomSizeCalculator
    {
        /// <summary>
        /// Room types with different size requirements.
        /// </summary>
        public enum RoomType
        {
            Bedroom,      // Individual bedroom: 4x4 → 5x5 → 6x6
            Barracks,     // Shared sleeping: 6x8 → 8x10
            Kitchen,      // Cooking area: 5x6 → 6x8 → 8x10
            DiningRoom,   // Eating area: 6x6 → 8x10 → 10x12
            Storage,      // General storage: 6x8 → 10x12 → 15x15
            Freezer,      // Food storage: 5x5 → 7x7 → 10x10
            Workshop,     // Crafting area: 6x6 → 8x10 → 10x12
            Hospital,     // Medical: 5x6 → 7x10 → 10x12
            RecRoom,      // Recreation: 6x8 → 10x10 → 12x15
            Research,     // Research lab: 4x5 → 6x8 → 8x10
            Prison,       // Prison cell: 3x4 (per cell)
            ShipRoom      // Ship component room: variable
        }
        
        /// <summary>
        /// Gets the optimal room size for the given parameters.
        /// </summary>
        /// <param name="roomType">Type of room to build</param>
        /// <param name="colonistCount">Current number of colonists</param>
        /// <param name="stage">Current development stage</param>
        /// <returns>Optimal width and height for the room</returns>
        public static IntVec2 GetOptimalSize(RoomType roomType, int colonistCount, DevelopmentStage stage)
        {
            switch (roomType)
            {
                case RoomType.Bedroom:
                    return GetBedroomSize(stage);
                    
                case RoomType.Barracks:
                    return GetBarracksSize(colonistCount, stage);
                    
                case RoomType.Kitchen:
                    return GetKitchenSize(colonistCount, stage);
                    
                case RoomType.DiningRoom:
                    return GetDiningRoomSize(colonistCount, stage);
                    
                case RoomType.Storage:
                    return GetStorageSize(colonistCount, stage);
                    
                case RoomType.Freezer:
                    return GetFreezerSize(colonistCount, stage);
                    
                case RoomType.Workshop:
                    return GetWorkshopSize(colonistCount, stage);
                    
                case RoomType.Hospital:
                    return GetHospitalSize(colonistCount, stage);
                    
                case RoomType.RecRoom:
                    return GetRecRoomSize(colonistCount, stage);
                    
                case RoomType.Research:
                    return GetResearchSize(colonistCount, stage);
                    
                case RoomType.Prison:
                    return new IntVec2(3, 4); // Standard cell size
                    
                case RoomType.ShipRoom:
                    return new IntVec2(8, 10); // Standard ship component room
                    
                default:
                    return new IntVec2(6, 8); // Default fallback
            }
        }
        
        /// <summary>
        /// Gets bedroom size based on development stage.
        /// Emergency/Early: 4x4 (minimal comfort)
        /// Mid: 5x5 (good impressiveness)
        /// Late/End: 6x6 (luxury for leaders)
        /// </summary>
        private static IntVec2 GetBedroomSize(DevelopmentStage stage)
        {
            switch (stage)
            {
                case DevelopmentStage.Emergency:
                case DevelopmentStage.EarlyGame:
                    return new IntVec2(4, 4); // 16 tiles - minimal
                    
                case DevelopmentStage.MidGame:
                    return new IntVec2(5, 5); // 25 tiles - comfortable
                    
                case DevelopmentStage.LateGame:
                case DevelopmentStage.EndGame:
                    return new IntVec2(6, 6); // 36 tiles - luxurious
                    
                default:
                    return new IntVec2(4, 4);
            }
        }
        
        /// <summary>
        /// Gets barracks size based on colonist count and stage.
        /// Emergency: 6x8 for 3 colonists
        /// Early: 8x10 for 5 colonists
        /// </summary>
        private static IntVec2 GetBarracksSize(int colonistCount, DevelopmentStage stage)
        {
            if (colonistCount <= 3 || stage == DevelopmentStage.Emergency)
            {
                return new IntVec2(6, 8); // 48 tiles for 3 colonists
            }
            else
            {
                return new IntVec2(8, 10); // 80 tiles for 5+ colonists
            }
        }
        
        /// <summary>
        /// Gets kitchen size based on colonist count and stage.
        /// Small: 5x6 (1 stove)
        /// Medium: 6x8 (2 stoves + butcher table)
        /// Large: 8x10 (3+ stoves + multiple tables)
        /// </summary>
        private static IntVec2 GetKitchenSize(int colonistCount, DevelopmentStage stage)
        {
            if (colonistCount <= 3 || stage == DevelopmentStage.Emergency || stage == DevelopmentStage.EarlyGame)
            {
                return new IntVec2(5, 6); // 30 tiles - small kitchen
            }
            else if (colonistCount <= 10 || stage == DevelopmentStage.MidGame)
            {
                return new IntVec2(6, 8); // 48 tiles - medium kitchen
            }
            else
            {
                return new IntVec2(8, 10); // 80 tiles - large kitchen
            }
        }
        
        /// <summary>
        /// Gets dining room size based on colonist count.
        /// Small: 6x6 (3-5 colonists)
        /// Medium: 8x10 (10 colonists)
        /// Large: 10x12+ (whole colony)
        /// </summary>
        private static IntVec2 GetDiningRoomSize(int colonistCount, DevelopmentStage stage)
        {
            if (colonistCount <= 5)
            {
                return new IntVec2(6, 6); // 36 tiles
            }
            else if (colonistCount <= 10)
            {
                return new IntVec2(8, 10); // 80 tiles
            }
            else
            {
                return new IntVec2(10, 12); // 120 tiles
            }
        }
        
        /// <summary>
        /// Gets storage size based on colonist count and stage.
        /// Small: 6x8 (initial)
        /// Medium: 10x12 (mid-game)
        /// Large: 15x15 (late-game)
        /// </summary>
        private static IntVec2 GetStorageSize(int colonistCount, DevelopmentStage stage)
        {
            if (stage == DevelopmentStage.Emergency || stage == DevelopmentStage.EarlyGame)
            {
                return new IntVec2(6, 8); // 48 tiles
            }
            else if (stage == DevelopmentStage.MidGame)
            {
                return new IntVec2(10, 12); // 120 tiles
            }
            else
            {
                return new IntVec2(15, 15); // 225 tiles - massive storage
            }
        }
        
        /// <summary>
        /// Gets freezer size based on colonist count.
        /// Small: 5x5 (3-5 colonists)
        /// Medium: 7x7 (10 colonists)
        /// Large: 10x10 (large colony)
        /// </summary>
        private static IntVec2 GetFreezerSize(int colonistCount, DevelopmentStage stage)
        {
            if (colonistCount <= 5)
            {
                return new IntVec2(5, 5); // 25 tiles
            }
            else if (colonistCount <= 10)
            {
                return new IntVec2(7, 7); // 49 tiles
            }
            else
            {
                return new IntVec2(10, 10); // 100 tiles
            }
        }
        
        /// <summary>
        /// Gets workshop size based on colonist count and stage.
        /// Small: 6x6 (1-2 workbenches)
        /// Medium: 8x10 (3-4 workbenches)
        /// Large: 10x12 (specialized workshop)
        /// </summary>
        private static IntVec2 GetWorkshopSize(int colonistCount, DevelopmentStage stage)
        {
            if (stage == DevelopmentStage.Emergency || stage == DevelopmentStage.EarlyGame)
            {
                return new IntVec2(6, 6); // 36 tiles
            }
            else if (stage == DevelopmentStage.MidGame)
            {
                return new IntVec2(8, 10); // 80 tiles
            }
            else
            {
                return new IntVec2(10, 12); // 120 tiles
            }
        }
        
        /// <summary>
        /// Gets hospital size based on colonist count.
        /// Small: 5x6 (2 beds)
        /// Medium: 7x10 (4-6 beds)
        /// Large: 10x12 (mass casualties)
        /// </summary>
        private static IntVec2 GetHospitalSize(int colonistCount, DevelopmentStage stage)
        {
            if (colonistCount <= 5)
            {
                return new IntVec2(5, 6); // 30 tiles - 2 beds
            }
            else if (colonistCount <= 10)
            {
                return new IntVec2(7, 10); // 70 tiles - 4-6 beds
            }
            else
            {
                return new IntVec2(10, 12); // 120 tiles - 8+ beds
            }
        }
        
        /// <summary>
        /// Gets rec room size based on colonist count.
        /// Small: 6x8 (3-5 colonists)
        /// Medium: 10x10 (10+ colonists)
        /// Large: 12x15 (whole colony)
        /// </summary>
        private static IntVec2 GetRecRoomSize(int colonistCount, DevelopmentStage stage)
        {
            if (colonistCount <= 5)
            {
                return new IntVec2(6, 8); // 48 tiles
            }
            else if (colonistCount <= 10)
            {
                return new IntVec2(10, 10); // 100 tiles
            }
            else
            {
                return new IntVec2(12, 15); // 180 tiles - legendary rec room
            }
        }
        
        /// <summary>
        /// Gets research lab size based on colonist count and stage.
        /// Small: 4x5 (1 research bench)
        /// Medium: 6x8 (2-3 benches)
        /// Large: 8x10 (specialized lab)
        /// </summary>
        private static IntVec2 GetResearchSize(int colonistCount, DevelopmentStage stage)
        {
            if (stage == DevelopmentStage.Emergency || stage == DevelopmentStage.EarlyGame)
            {
                return new IntVec2(4, 5); // 20 tiles
            }
            else if (stage == DevelopmentStage.MidGame)
            {
                return new IntVec2(6, 8); // 48 tiles
            }
            else
            {
                return new IntVec2(8, 10); // 80 tiles
            }
        }
        
        /// <summary>
        /// Helper: Gets minimum size for a room type (used for fallback).
        /// </summary>
        public static IntVec2 GetMinimumSize(RoomType roomType)
        {
            switch (roomType)
            {
                case RoomType.Bedroom:
                    return new IntVec2(3, 3);
                case RoomType.Prison:
                    return new IntVec2(3, 4);
                case RoomType.Research:
                    return new IntVec2(4, 5);
                default:
                    return new IntVec2(5, 5);
            }
        }
    }
}

