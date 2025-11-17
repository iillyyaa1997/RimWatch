using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// Lightweight cache of important base zones for faster decision-making.
    /// Updates on construction events to stay current.
    /// </summary>
    public static class BaseZoneCache
    {
        // Cached zones
        private static IntVec3 _baseCenter = IntVec3.Invalid;
        private static List<IntVec3> _livingZone = new List<IntVec3>();
        private static List<IntVec3> _workshopZone = new List<IntVec3>();
        private static List<IntVec3> _farmZone = new List<IntVec3>();
        private static int _lastUpdateTick = 0;
        
        // Cache settings
        private const int MaxZoneCells = 200; // Limit zone size for performance
        
        /// <summary>
        /// Base center (average of all colony buildings).
        /// </summary>
        public static IntVec3 BaseCenter => _baseCenter;
        
        /// <summary>
        /// Living zone cells (bedrooms, barracks).
        /// </summary>
        public static List<IntVec3> LivingZone => _livingZone;
        
        /// <summary>
        /// Workshop zone cells (crafting, production).
        /// </summary>
        public static List<IntVec3> WorkshopZone => _workshopZone;
        
        /// <summary>
        /// Farm zone cells (growing zones).
        /// </summary>
        public static List<IntVec3> FarmZone => _farmZone;
        
        /// <summary>
        /// Last update tick.
        /// </summary>
        public static int LastUpdateTick => _lastUpdateTick;
        
        /// <summary>
        /// Updates the zone cache by analyzing current map state.
        /// Called after construction events.
        /// </summary>
        public static void UpdateCache(Map map)
        {
            if (map == null) return;
            
            int currentTick = Find.TickManager.TicksGame;
            
            RimWatchLogger.Debug($"BaseZoneCache: Updating cache at tick {currentTick}");
            
            try
            {
                // 1. Calculate base center
                _baseCenter = CalculateBaseCenter(map);
                
                // 2. Find living zone (bedrooms)
                _livingZone = FindLivingZone(map);
                
                // 3. Find workshop zone (production)
                _workshopZone = FindWorkshopZone(map);
                
                // 4. Find farm zone (growing)
                _farmZone = FindFarmZone(map);
                
                _lastUpdateTick = currentTick;
                
                RimWatchLogger.Debug($"BaseZoneCache: Updated - Center: ({_baseCenter.x}, {_baseCenter.z}), " +
                                    $"Living: {_livingZone.Count} cells, Workshop: {_workshopZone.Count} cells, " +
                                    $"Farm: {_farmZone.Count} cells");
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("BaseZoneCache: Error updating cache", ex);
            }
        }
        
        /// <summary>
        /// Gets the appropriate zone for a building role.
        /// </summary>
        public static List<IntVec3> GetZoneForRole(LocationFinder.BuildingRole role)
        {
            switch (role)
            {
                case LocationFinder.BuildingRole.Bedroom:
                case LocationFinder.BuildingRole.Medical:
                    return _livingZone;
                    
                case LocationFinder.BuildingRole.Workshop:
                case LocationFinder.BuildingRole.Kitchen:
                case LocationFinder.BuildingRole.Research:
                    return _workshopZone;
                    
                case LocationFinder.BuildingRole.Farm:
                    return _farmZone;
                    
                default:
                    return new List<IntVec3>();
            }
        }
        
        /// <summary>
        /// Checks if cache needs update (hasn't been updated recently).
        /// </summary>
        public static bool NeedsUpdate(int currentTick, int updateIntervalTicks = 1800)
        {
            return currentTick - _lastUpdateTick > updateIntervalTicks;
        }
        
        /// <summary>
        /// Calculates base center from existing colony buildings.
        /// v0.7.9: If no buildings exist, analyzes map for optimal starting location.
        /// </summary>
        private static IntVec3 CalculateBaseCenter(Map map)
        {
            var buildings = map.listerBuildings.allBuildingsColonist;
            
            if (buildings.Count == 0)
            {
                // v0.7.9: No buildings yet - analyze map for optimal starting location
                IntVec3 optimalStart = AnalyzeOptimalStartingLocation(map);
                if (optimalStart != IntVec3.Invalid)
                {
                    RimWatchLogger.Info($"🎯 BaseZoneCache: Analyzed optimal starting location at ({optimalStart.x}, {optimalStart.z})");
                    return optimalStart;
                }
                return map.Center; // Fallback
            }
            
            int avgX = (int)buildings.Average(b => b.Position.x);
            int avgZ = (int)buildings.Average(b => b.Position.z);
            
            return new IntVec3(avgX, 0, avgZ);
        }
        
        /// <summary>
        /// v0.7.9: Analyzes map to find optimal starting location for base.
        /// Considers: fertile soil, resources (ore, water), flat terrain, safety.
        /// </summary>
        private static IntVec3 AnalyzeOptimalStartingLocation(Map map)
        {
            try
            {
                RimWatchLogger.Info("🔍 BaseZoneCache: Analyzing map for optimal starting location...");
                
                List<IntVec3> candidates = new List<IntVec3>();
                
                // Sample points across the map (every 20 cells)
                for (int x = 20; x < map.Size.x - 20; x += 20)
                {
                    for (int z = 20; z < map.Size.z - 20; z += 20)
                    {
                        IntVec3 candidate = new IntVec3(x, 0, z);
                        if (candidate.InBounds(map) && !map.fogGrid.IsFogged(candidate))
                        {
                            candidates.Add(candidate);
                        }
                    }
                }
                
                if (candidates.Count == 0) return IntVec3.Invalid;
                
                // Score each candidate
                IntVec3 bestLocation = IntVec3.Invalid;
                float bestScore = 0f;
                
                foreach (IntVec3 candidate in candidates)
                {
                    float score = ScoreStartingLocation(map, candidate);
                    
                    RimWatchLogger.Debug($"  Candidate ({candidate.x}, {candidate.z}): score={score:F1}");
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestLocation = candidate;
                    }
                }
                
                RimWatchLogger.Info($"✅ BaseZoneCache: Best starting location ({bestLocation.x}, {bestLocation.z}) score: {bestScore:F1}");
                return bestLocation;
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("BaseZoneCache: Error analyzing starting location", ex);
                return IntVec3.Invalid;
            }
        }
        
        /// <summary>
        /// Scores a potential starting location based on multiple factors.
        /// </summary>
        private static float ScoreStartingLocation(Map map, IntVec3 location)
        {
            float score = 0f;
            
            // v0.8.0: EXPANDED RADIUS - Check 50-cell radius (was 30)
            List<IntVec3> areaIntel = GenRadial.RadialCellsAround(location, 50, true)
                .Where(c => c.InBounds(map))
                .ToList();
            
            int fertileCells = 0;
            int richSoilCells = 0; // v0.8.0: NEW - Track rich soil separately
            int waterCells = 0;
            int oreCells = 0;
            int flatCells = 0;
            int standableCells = 0;
            
            foreach (IntVec3 cell in areaIntel)
            {
                TerrainDef terrain = cell.GetTerrain(map);
                
                // v0.8.0: IMPROVED - Distinguish between fertile and rich soil
                if (terrain != null)
                {
                    if (terrain.fertility >= 1.4f) // Rich soil (140%+)
                    {
                        richSoilCells++;
                        fertileCells++; // Also count as fertile
                    }
                    else if (terrain.fertility > 0.8f) // Regular fertile (80%+)
                    {
                        fertileCells++;
                    }
                }
                
                // Water access (for geothermal, rice, etc.)
                if (terrain != null && (terrain.IsWater || terrain.defName.Contains("Marsh")))
                {
                    waterCells++;
                }
                
                // Ore veins nearby (important for early mining)
                Thing mineable = cell.GetFirstMineable(map);
                if (mineable != null)
                {
                    oreCells++;
                }
                
                // Flat terrain (easy to build)
                if (terrain != null && terrain.passability != Traversability.Impassable)
                {
                    flatCells++;
                }
                
                // Standable (buildable)
                if (cell.Standable(map))
                {
                    standableCells++;
                }
            }
            
            // v0.8.0: IMPROVED SCORING - Rich soil gets much higher weight
            float fertileScore = fertileCells * 2f;      // Regular fertile soil
            float richScore = richSoilCells * 5f;        // Rich soil is MUCH more valuable!
            float waterScore = waterCells * 0.5f;        // Water is nice but not critical
            float oreScore = oreCells * 1f;              // Ore is important for early game
            float flatScore = flatCells * 0.5f;          // Flat terrain is good
            float standableScore = standableCells * 1f;  // Buildable space is critical
            
            score += fertileScore + richScore + waterScore + oreScore + flatScore + standableScore;
            
            RimWatchLogger.Debug($"    Scoring ({location.x},{location.z}): fertile={fertileCells}({fertileScore:F1}) rich={richSoilCells}({richScore:F1}) water={waterCells}({waterScore:F1}) ore={oreCells}({oreScore:F1}) flat={flatCells}({flatScore:F1}) standable={standableCells}({standableScore:F1})");
            
            // Penalty for map edges (too close to danger)
            float edgeDistance = System.Math.Min(
                System.Math.Min(location.x, map.Size.x - location.x),
                System.Math.Min(location.z, map.Size.z - location.z)
            );
            
            if (edgeDistance < 20f)
            {
                score *= 0.5f; // Heavy penalty for edge locations
            }
            
            return score;
        }
        
        /// <summary>
        /// Finds cells in living zone (around bedrooms/barracks).
        /// </summary>
        private static List<IntVec3> FindLivingZone(Map map)
        {
            List<IntVec3> zone = new List<IntVec3>();
            
            // Find all beds
            var beds = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.building?.isSittable == false && 
                           (b.def.defName.Contains("Bed") || b.def.defName.Contains("SleepingSpot")))
                .ToList();
            
            if (beds.Count == 0)
                return zone;
            
            // Add cells around each bed (room area)
            foreach (Building bed in beds)
            {
                Room room = bed.GetRoom();
                
                if (room != null && !room.PsychologicallyOutdoors)
                {
                    // Add room cells (limited by MaxZoneCells)
                    foreach (IntVec3 cell in room.Cells)
                    {
                        if (!zone.Contains(cell))
                        {
                            zone.Add(cell);
                            
                            if (zone.Count >= MaxZoneCells)
                                return zone;
                        }
                    }
                }
                else
                {
                    // No room - add radius around bed
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(bed.Position, 8, true))
                    {
                        if (cell.InBounds(map) && !zone.Contains(cell))
                        {
                            zone.Add(cell);
                            
                            if (zone.Count >= MaxZoneCells)
                                return zone;
                        }
                    }
                }
            }
            
            return zone;
        }
        
        /// <summary>
        /// Finds cells in workshop zone (around production buildings).
        /// </summary>
        private static List<IntVec3> FindWorkshopZone(Map map)
        {
            List<IntVec3> zone = new List<IntVec3>();
            
            // Find production buildings
            var workshops = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.defName.Contains("Bench") ||
                           b.def.defName.Contains("Table") ||
                           b.def.defName.Contains("Stove") ||
                           b.def.defName.Contains("Smithy") ||
                           b.def.defName.Contains("Tailor"))
                .ToList();
            
            if (workshops.Count == 0)
                return zone;
            
            // Add cells around each workshop
            foreach (Building workshop in workshops)
            {
                Room room = workshop.GetRoom();
                
                if (room != null && !room.PsychologicallyOutdoors)
                {
                    foreach (IntVec3 cell in room.Cells)
                    {
                        if (!zone.Contains(cell))
                        {
                            zone.Add(cell);
                            
                            if (zone.Count >= MaxZoneCells)
                                return zone;
                        }
                    }
                }
                else
                {
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(workshop.Position, 6, true))
                    {
                        if (cell.InBounds(map) && !zone.Contains(cell))
                        {
                            zone.Add(cell);
                            
                            if (zone.Count >= MaxZoneCells)
                                return zone;
                        }
                    }
                }
            }
            
            return zone;
        }
        
        /// <summary>
        /// Finds cells in farm zone (growing zones).
        /// </summary>
        private static List<IntVec3> FindFarmZone(Map map)
        {
            List<IntVec3> zone = new List<IntVec3>();
            
            // Find growing zones
            var zones = map.zoneManager.AllZones
                .OfType<Zone_Growing>()
                .ToList();
            
            if (zones.Count == 0)
                return zone;
            
            // Add all growing zone cells
            foreach (Zone_Growing growZone in zones)
            {
                foreach (IntVec3 cell in growZone.Cells)
                {
                    if (!zone.Contains(cell))
                    {
                        zone.Add(cell);
                        
                        if (zone.Count >= MaxZoneCells)
                            return zone;
                    }
                }
            }
            
            return zone;
        }
        
        /// <summary>
        /// Clears the cache (useful for testing).
        /// </summary>
        public static void ClearCache()
        {
            _baseCenter = IntVec3.Invalid;
            _livingZone.Clear();
            _workshopZone.Clear();
            _farmZone.Clear();
            _lastUpdateTick = 0;
            
            RimWatchLogger.Debug("BaseZoneCache: Cache cleared");
        }
    }
}

