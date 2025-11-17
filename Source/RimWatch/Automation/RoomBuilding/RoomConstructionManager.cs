using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.RoomBuilding
{
    /// <summary>
    /// Manages room construction lifecycle from planning to completion.
    /// Tracks construction stages and coordinates furniture placement timing.
    /// </summary>
    public static class RoomConstructionManager
    {
        /// <summary>
        /// Construction stages for room building.
        /// </summary>
        public enum ConstructionStage
        {
            PLANNED,           // Room planned, walls not yet placed
            WALLS_BUILDING,    // Wall blueprints placed, construction in progress
            WALLS_COMPLETE,    // 80%+ walls built, ready for secondary furniture
            FURNISHING,        // Adding furniture and decorations
            COMPLETE           // Room fully furnished and ready
        }

        /// <summary>
        /// State tracking for active room construction.
        /// </summary>
        public class RoomConstructionState
        {
            public RoomPlanner.RoomPlan Plan { get; set; }
            public ConstructionStage Stage { get; set; }
            public int TickPlanned { get; set; }
            public int WallsBuilt { get; set; }
            public int WallsTotal { get; set; }
            public List<string> FurniturePlaced { get; set; } = new List<string>();
            public bool CriticalFurniturePlaced { get; set; } = false;
            public bool SecondaryFurniturePlaced { get; set; } = false;
        }

        // Active room constructions per map
        private static Dictionary<Map, List<RoomConstructionState>> _activeConstructions = new Dictionary<Map, List<RoomConstructionState>>();

        /// <summary>
        /// Registers a new room construction plan.
        /// </summary>
        public static void RegisterRoomConstruction(Map map, RoomPlanner.RoomPlan plan)
        {
            if (!_activeConstructions.ContainsKey(map))
            {
                _activeConstructions[map] = new List<RoomConstructionState>();
            }

            RoomConstructionState state = new RoomConstructionState
            {
                Plan = plan,
                Stage = ConstructionStage.PLANNED,
                TickPlanned = Find.TickManager.TicksGame,
                WallsBuilt = 0,
                WallsTotal = plan.WallCells?.Count ?? 0
            };

            _activeConstructions[map].Add(state);
            RimWatchLogger.Info($"RoomConstructionManager: Registered {plan.Role} room at ({plan.Origin.x}, {plan.Origin.z}) - {state.WallsTotal} walls");
        }

        /// <summary>
        /// Gets all active room constructions for a map.
        /// </summary>
        public static List<RoomConstructionState> GetActiveConstructions(Map map)
        {
            if (!_activeConstructions.ContainsKey(map))
            {
                return new List<RoomConstructionState>();
            }
            
            return _activeConstructions[map];
        }

        /// <summary>
        /// Updates construction state for all active rooms.
        /// Should be called periodically (every few seconds).
        /// </summary>
        public static void UpdateConstructionStates(Map map)
        {
            if (!_activeConstructions.ContainsKey(map))
                return;

            List<RoomConstructionState> states = _activeConstructions[map];
            List<RoomConstructionState> completedRooms = new List<RoomConstructionState>();

            foreach (RoomConstructionState state in states)
            {
                ConstructionStage previousStage = state.Stage;

                switch (state.Stage)
                {
                    case ConstructionStage.PLANNED:
                        // Check if walls have been placed (blueprints, frames, or built)
                        ConstructionStateChecker.WallStateCount wallState = ConstructionStateChecker.CountWalls(map, state.Plan.WallCells);
                        if (wallState.HasAny)
                        {
                            state.Stage = ConstructionStage.WALLS_BUILDING;
                            RimWatchLogger.Info($"RoomConstructionManager: {state.Plan.Role} → WALLS_BUILDING ({wallState})");
                        }
                        break;

                    case ConstructionStage.WALLS_BUILDING:
                        // Count how many walls are in all states (built, frames, blueprints)
                        ConstructionStateChecker.WallStateCount currentState = ConstructionStateChecker.CountWalls(map, state.Plan.WallCells);
                        state.WallsBuilt = currentState.Built;
                        float completionPercent = currentState.CompletionPercent;

                        // Transition to WALLS_COMPLETE when 80% BUILT (not just blueprints/frames)
                        if (completionPercent >= 0.8f)
                        {
                            state.Stage = ConstructionStage.WALLS_COMPLETE;
                            RimWatchLogger.Info($"RoomConstructionManager: {state.Plan.Role} → WALLS_COMPLETE ({currentState})");
                        }
                        break;

                    case ConstructionStage.WALLS_COMPLETE:
                        // Ready for secondary furniture
                        state.Stage = ConstructionStage.FURNISHING;
                        RimWatchLogger.Info($"RoomConstructionManager: {state.Plan.Role} → FURNISHING");
                        break;

                    case ConstructionStage.FURNISHING:
                        // Mark as complete after furnishing phase
                        if (state.SecondaryFurniturePlaced)
                        {
                            state.Stage = ConstructionStage.COMPLETE;
                            RimWatchLogger.Info($"RoomConstructionManager: {state.Plan.Role} → COMPLETE");
                            completedRooms.Add(state);
                        }
                        break;

                    case ConstructionStage.COMPLETE:
                        // Already complete
                        completedRooms.Add(state);
                        break;
                }
            }

            // Remove completed rooms after a delay (keep for 5 minutes for reference)
            int currentTick = Find.TickManager.TicksGame;
            completedRooms.RemoveAll(state => currentTick - state.TickPlanned < 18000); // Keep for 5 minutes

            foreach (RoomConstructionState completed in completedRooms)
            {
                states.Remove(completed);
                RimWatchLogger.Debug($"RoomConstructionManager: Removed completed {completed.Plan.Role} from tracking");
            }
        }

        /// <summary>
        /// Checks if a room is ready for critical furniture (beds, stoves).
        /// Returns true if at least 1 wall is built.
        /// </summary>
        public static bool IsReadyForCriticalFurniture(RoomConstructionState state, Map map)
        {
            if (state.Stage == ConstructionStage.PLANNED)
                return false; // Wait for walls to be blueprinted at least

            if (state.CriticalFurniturePlaced)
                return false; // Already placed

            // At least 1 wall built OR in WALLS_BUILDING stage
            return state.Stage >= ConstructionStage.WALLS_BUILDING;
        }

        /// <summary>
        /// Checks if a room is ready for secondary furniture (tables, chairs, decorations).
        /// Returns true if 80%+ walls are complete.
        /// </summary>
        public static bool IsReadyForSecondaryFurniture(RoomConstructionState state)
        {
            if (state.SecondaryFurniturePlaced)
                return false; // Already placed

            return state.Stage >= ConstructionStage.WALLS_COMPLETE;
        }

        /// <summary>
        /// Marks critical furniture as placed for a room.
        /// </summary>
        public static void MarkCriticalFurniturePlaced(RoomConstructionState state, string furnitureLabel)
        {
            state.FurniturePlaced.Add(furnitureLabel);
            state.CriticalFurniturePlaced = true;
            RimWatchLogger.Debug($"RoomConstructionManager: Critical furniture placed in {state.Plan.Role}: {furnitureLabel}");
        }

        /// <summary>
        /// Marks secondary furniture as placed for a room.
        /// </summary>
        public static void MarkSecondaryFurniturePlaced(RoomConstructionState state, string furnitureLabel)
        {
            state.FurniturePlaced.Add(furnitureLabel);
            state.SecondaryFurniturePlaced = true;
            RimWatchLogger.Debug($"RoomConstructionManager: Secondary furniture placed in {state.Plan.Role}: {furnitureLabel}");
        }

        /// <summary>
        /// Finds room construction states that contain a specific cell.
        /// Used to place beds inside rooms being built.
        /// </summary>
        public static RoomConstructionState FindRoomContainingCell(Map map, IntVec3 cell)
        {
            if (!_activeConstructions.ContainsKey(map))
                return null;

            foreach (RoomConstructionState state in _activeConstructions[map])
            {
                if (state.Plan.FloorCells != null && state.Plan.FloorCells.Contains(cell))
                    return state;

                if (state.Plan.WallCells != null && state.Plan.WallCells.Contains(cell))
                    return state;
            }

            return null;
        }

        /// <summary>
        /// Gets floor cells of rooms that are being built (for placing furniture inside).
        /// </summary>
        public static List<IntVec3> GetFloorCellsOfRoomsInProgress(Map map, RoomPlanner.RoomRole role)
        {
            List<IntVec3> cells = new List<IntVec3>();

            if (!_activeConstructions.ContainsKey(map))
                return cells;

            foreach (RoomConstructionState state in _activeConstructions[map])
            {
                if (state.Plan.Role == role && state.Plan.FloorCells != null)
                {
                    cells.AddRange(state.Plan.FloorCells);
                }
            }

            return cells;
        }

        /// <summary>
        /// Checks if wall blueprints exist for given cells.
        /// UPDATED: Now uses ConstructionStateChecker for all states.
        /// </summary>
        private static bool HasWallBlueprints(Map map, List<IntVec3> wallCells)
        {
            if (wallCells == null || wallCells.Count == 0)
                return false;

            ConstructionStateChecker.WallStateCount state = ConstructionStateChecker.CountWalls(map, wallCells);
            
            // At least 50% of walls have SOMETHING (blueprint, frame, or built)
            int totalCells = wallCells.Count;
            return state.Total >= totalCells * 0.5f;
        }

        /// <summary>
        /// Counts how many walls are actually built (not blueprints/frames).
        /// UPDATED: Now uses ConstructionStateChecker.
        /// </summary>
        private static int CountBuiltWalls(Map map, List<IntVec3> wallCells)
        {
            if (wallCells == null)
                return 0;

            ConstructionStateChecker.WallStateCount state = ConstructionStateChecker.CountWalls(map, wallCells);
            return state.Built;
        }

        /// <summary>
        /// Checks if cell has a wall blueprint, frame, or built wall.
        /// UPDATED: Now uses ConstructionStateChecker for consistency.
        /// </summary>
        private static bool HasWallBlueprintOrBuilding(Map map, IntVec3 cell)
        {
            return ConstructionStateChecker.HasWallInAnyState(map, cell);
        }

        /// <summary>
        /// Clears completed rooms from tracking (cleanup).
        /// </summary>
        public static void ClearCompletedRooms(Map map)
        {
            if (!_activeConstructions.ContainsKey(map))
                return;

            _activeConstructions[map].RemoveAll(state => state.Stage == ConstructionStage.COMPLETE);
        }
    }
}

