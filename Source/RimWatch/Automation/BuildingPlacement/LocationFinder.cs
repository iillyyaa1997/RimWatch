using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Verse;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// Finds optimal locations for building placement.
    /// Uses expanding ring search with scoring system.
    /// </summary>
    public static class LocationFinder
    {
        /// <summary>
        /// Building roles for specialized placement logic.
        /// </summary>
        public enum BuildingRole
        {
            Bedroom,      // Beds - near other beds, indoor
            Kitchen,      // Stoves - near storage, indoor
            Storage,      // Shelves/zones - near kitchen, indoor preferred
            Workshop,     // Crafting - near storage, indoor
            Power,        // Generators - outdoor, away from living areas
            Farm,         // Growing zones - outdoor, fertile soil
            Defense,      // Turrets - perimeter, good sight lines
            Recreation,   // Horseshoes, etc - outdoor, open area
            Research,     // Research bench - indoor, quiet
            Medical,      // Hospital beds - indoor, separate from bedrooms
            General       // Default - near base center
        }

        /// <summary>
        /// Finds best location for a building using expanding ring search.
        /// </summary>
        public static IntVec3 FindBestLocation(
            Map map,
            ThingDef buildingDef,
            BuildingRole role,
            string logLevel = "Moderate")
        {
            // Start timer for search time tracking
            Stopwatch stopwatch = Stopwatch.StartNew();
            
            // Get base center (average position of existing buildings)
            IntVec3 baseCenter = GetBaseCenter(map);

            // Determine search parameters based on role and colony size
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            SearchParameters searchParams = GetSearchParameters(role, colonistCount);

            if (logLevel == "Verbose" || logLevel == "Debug")
            {
                RimWatchLogger.Info($"🔍 LocationFinder: Searching for {buildingDef.label} ({role})");
                RimWatchLogger.Info($"   Base center: ({baseCenter.x}, {baseCenter.z})");
                RimWatchLogger.Info($"   Search radius: {searchParams.MinRadius}-{searchParams.MaxRadius}");
            }

            // Search in expanding rings
            List<ScoredLocation> candidates = new List<ScoredLocation>();

            for (int radius = searchParams.MinRadius; radius <= searchParams.MaxRadius; radius += searchParams.Step)
            {
                // Sample points in ring
                for (int angle = 0; angle < 360; angle += searchParams.AngleStep)
                {
                    float radians = angle * (float)Math.PI / 180f;
                    int x = baseCenter.x + (int)(radius * Math.Cos(radians));
                    int z = baseCenter.z + (int)(radius * Math.Sin(radians));
                    
                    // ✅ GRID ALIGNMENT: Snap to grid (multiples of 2) for aesthetics
                    x = (x / 2) * 2; // Round to nearest even number
                    z = (z / 2) * 2;
                    
                    IntVec3 candidate = new IntVec3(x, 0, z);

                    if (!candidate.InBounds(map)) continue;

                    // Early reject if no rotation is acceptable with comprehensive area validation
                    if (!HasAnyValidRotationWithAreaCheck(map, buildingDef, candidate, logLevel))
                    {
                        continue;
                    }

                    // Get complete score for this location
                    PlacementScore score = PlacementValidator.GetCompleteScore(map, candidate, buildingDef, logLevel);

                    if (!score.IsValid) continue;

                    // Add role-specific bonuses
                    ApplyRoleBonuses(map, candidate, buildingDef, role, score);

                    // Add proximity bonuses to related buildings
                    ApplyProximityBonuses(map, candidate, role, score);

                    candidates.Add(new ScoredLocation { Location = candidate, Score = score });

                    if (logLevel == "Verbose")
                    {
                        RimWatchLogger.Info($"   Candidate ({candidate.x}, {candidate.z}): {score.TotalScore}/100");
                    }

                    // Early exit if we found excellent locations
                    if (candidates.Count >= 10 && candidates.Any(c => c.Score.TotalScore >= 85))
                    {
                        break;
                    }
                }

                // If we have good candidates, no need to search further
                if (candidates.Count >= 5 && candidates.Any(c => c.Score.TotalScore >= 75))
                {
                    break;
                }
            }

            // Sort by score
            candidates = candidates.OrderByDescending(c => c.Score.TotalScore).ToList();

            if (candidates.Count == 0)
            {
                if (logLevel != "Minimal")
                {
                    RimWatchLogger.Warning($"LocationFinder: No valid locations found for {buildingDef.label}");
                }
                
                stopwatch.Stop();
                
                // Log failed decision
                if (RimWatchMod.Settings.enableDecisionLogging)
                {
                    DecisionLogger.LogBuildingDecision(
                        buildingDef.defName,
                        new List<CandidateLocation>(),
                        null,
                        0,
                        stopwatch.ElapsedMilliseconds
                    );
                }
                
                return IntVec3.Invalid;
            }

            // Get top 3 for logging
            var top3 = candidates.Take(3).ToList();
            
            stopwatch.Stop();

            // Log decision to JSON if enabled
            if (RimWatchMod.Settings.enableDecisionLogging)
            {
                var candidateLocations = top3.Select(c => new CandidateLocation
                {
                    Position = c.Location,
                    Score = c.Score.TotalScore,
                    Reasons = c.Score.GetTopFactors(3)
                }).ToList();
                
                DecisionLogger.LogBuildingDecision(
                    buildingDef.defName,
                    candidateLocations,
                    candidateLocations.FirstOrDefault(),
                    candidates.Count(c => !c.Score.IsValid),
                    stopwatch.ElapsedMilliseconds
                );
            }

            if (logLevel == "Moderate" || logLevel == "Verbose" || logLevel == "Debug")
            {
                RimWatchLogger.Info($"✅ LocationFinder: Found {candidates.Count} candidates for {buildingDef.label} (search: {stopwatch.ElapsedMilliseconds}ms)");
                RimWatchLogger.Info($"   Best: ({top3[0].Location.x}, {top3[0].Location.z}) [{top3[0].Score.TotalScore}/100]");
                
                if (logLevel == "Verbose" || logLevel == "Debug")
                {
                    for (int i = 0; i < Math.Min(3, top3.Count); i++)
                    {
                        RimWatchLogger.Info($"   #{i + 1}: ({top3[i].Location.x}, {top3[i].Location.z}) - {top3[i].Score}");
                    }
                }
            }

            return top3[0].Location;
        }

        /// <summary>
        /// Gets the center of the base (average position of existing buildings).
        /// </summary>
        private static IntVec3 GetBaseCenter(Map map)
        {
            var buildings = map.listerBuildings.allBuildingsColonist;

            if (buildings.Count == 0)
            {
                return map.Center;
            }

            int avgX = (int)buildings.Average(b => b.Position.x);
            int avgZ = (int)buildings.Average(b => b.Position.z);

            return new IntVec3(avgX, 0, avgZ);
        }

        /// <summary>
        /// Determines search parameters based on role and colony size.
        /// </summary>
        private static SearchParameters GetSearchParameters(BuildingRole role, int colonistCount)
        {
            SearchParameters params_ = new SearchParameters();

            // v0.7.9: COMPACT PLACEMENT - buildings should be 1-15 cells apart, not scattered
            if (colonistCount <= 2)
            {
                // Early game - VERY tight clustering
                params_.MinRadius = 1;
                params_.MaxRadius = 8;
                params_.Step = 2;
                params_.AngleStep = 30;
            }
            else if (colonistCount <= 6)
            {
                // Mid game - compact clustering
                params_.MinRadius = 1;
                params_.MaxRadius = 12;
                params_.Step = 2;
                params_.AngleStep = 30;
            }
            else
            {
                // Late game - still compact, max 15 cells
                params_.MinRadius = 1;
                params_.MaxRadius = 15;
                params_.Step = 3;
                params_.AngleStep = 30;
            }

            // v0.7.9: Adjust based on role - but keep COMPACT!
            switch (role)
            {
                case BuildingRole.Farm:
                    // Farms can be a bit further, but not too far
                    params_.MinRadius = 10;
                    params_.MaxRadius = 25;
                    break;

                case BuildingRole.Defense:
                    // Defense at perimeter, but closer than before
                    params_.MinRadius = 12;
                    params_.MaxRadius = 20;
                    break;

                case BuildingRole.Power:
                    // Power near base but not inside
                    params_.MinRadius = 5;
                    params_.MaxRadius = 12;
                    break;

                case BuildingRole.Bedroom:
                case BuildingRole.Kitchen:
                case BuildingRole.Storage:
                    // Core buildings - VERY close to base (1-10 cells)
                    params_.MaxRadius = Math.Min(params_.MaxRadius, 10);
                    break;
            }

            return params_;
        }

        /// <summary>
        /// Applies role-specific scoring bonuses.
        /// </summary>
        private static void ApplyRoleBonuses(Map map, IntVec3 location, ThingDef buildingDef, BuildingRole role, PlacementScore score)
        {
            bool isRoofed = location.Roofed(map);
            TerrainDef terrain = location.GetTerrain(map);

            switch (role)
            {
                case BuildingRole.Bedroom:
                    // ✅ CRITICAL: Beds MUST be inside an enclosed room (not psychologically outdoors)
                    Room room = location.GetRoom(map);
                    if (room == null || room.PsychologicallyOutdoors)
                    {
                        score.Reject("Role: Bedroom REQUIRES enclosed room (indoor only)");
                        return; // Critical rejection
                    }
                    score.AddFactor("Role: Enclosed room required for bed", 15);
                    break;

                case BuildingRole.Kitchen:
                case BuildingRole.Research:
                case BuildingRole.Medical:
                    // Prefer indoor, constructed floor
                    if (isRoofed) score.AddFactor("Role: Indoor preferred", 5);
                    break;

                case BuildingRole.Farm:
                    // Prefer outdoor, fertile soil
                    if (!isRoofed) score.AddFactor("Role: Outdoor required", 10);
                    if (terrain != null && terrain.fertility > 0.8f)
                    {
                        score.AddFactor("Role: Fertile soil", 15);
                    }
                    break;

                case BuildingRole.Power:
                case BuildingRole.Defense:
                case BuildingRole.Recreation:
                    // Prefer outdoor
                    if (!isRoofed) score.AddFactor("Role: Outdoor preferred", 10);
                    break;
            }
        }

        /// <summary>
        /// Applies proximity bonuses based on nearby buildings.
        /// </summary>
        private static void ApplyProximityBonuses(Map map, IntVec3 location, BuildingRole role, PlacementScore score)
        {
            var nearbyBuildings = map.listerBuildings.allBuildingsColonist
                .Where(b => location.DistanceTo(b.Position) <= 15f)
                .ToList();

            // ✅ NEW: Aesthetic alignment bonus - prefer locations aligned with nearby buildings
            foreach (var building in nearbyBuildings.Take(3)) // Check closest 3 buildings
            {
                // Check if aligned horizontally (same x coordinate)
                if (Math.Abs(building.Position.x - location.x) <= 1)
                {
                    score.AddFactor("Aligned horizontally", 3);
                    break;
                }
                // Check if aligned vertically (same z coordinate)
                if (Math.Abs(building.Position.z - location.z) <= 1)
                {
                    score.AddFactor("Aligned vertically", 3);
                    break;
                }
            }

            switch (role)
            {
                case BuildingRole.Kitchen:
                    // Kitchen near storage
                    foreach (var building in nearbyBuildings)
                    {
                        if (building.def.defName.Contains("Shelf") ||
                            building.def.defName.Contains("Stockpile"))
                        {
                            score.AddFactor("Near storage", 10);
                            break;
                        }
                    }
                    break;

                case BuildingRole.Bedroom:
                    // Bedrooms near other bedrooms (bedroom zone)
                    int nearbyBeds = nearbyBuildings.Count(b => b.def.defName.Contains("Bed"));
                    if (nearbyBeds > 0)
                    {
                        score.AddFactor($"Near bedroom zone ({nearbyBeds} beds)", 5);
                    }
                    break;

                case BuildingRole.Workshop:
                    // Workshops near storage
                    foreach (var building in nearbyBuildings)
                    {
                        if (building.def.defName.Contains("Shelf") ||
                            building.def.defName.Contains("Stockpile"))
                        {
                            score.AddFactor("Near storage", 8);
                            break;
                        }
                    }
                    break;

                case BuildingRole.Storage:
                    // Storage near kitchen and workshops
                    bool nearKitchen = nearbyBuildings.Any(b => b.def.defName.Contains("Stove"));
                    bool nearWorkshop = nearbyBuildings.Any(b => b.def.defName.Contains("Bench") ||
                                                                 b.def.defName.Contains("Table"));
                    if (nearKitchen) score.AddFactor("Near kitchen", 8);
                    if (nearWorkshop) score.AddFactor("Near workshop", 8);
                    break;
            }
        }

        /// <summary>
        /// Search parameters for location finding.
        /// </summary>
        private class SearchParameters
        {
            public int MinRadius { get; set; } = 5;
            public int MaxRadius { get; set; } = 30;
            public int Step { get; set; } = 5;
            public int AngleStep { get; set; } = 45;
        }

        /// <summary>
        /// Location with its score.
        /// </summary>
        private class ScoredLocation
        {
            public IntVec3 Location { get; set; }
            public PlacementScore Score { get; set; }
        }

        /// <summary>
        /// Returns true if there exists at least one rotation that passes comprehensive area validation.
        /// Checks footprint + buffer zone for buildings, blueprints, terrain, accessibility.
        /// </summary>
        private static bool HasAnyValidRotationWithAreaCheck(Map map, ThingDef buildingDef, IntVec3 cell, string logLevel = "Moderate")
        {
            if (!cell.InBounds(map)) return false;
            
            Rot4[] tryRots = new[] { Rot4.North, Rot4.East, Rot4.South, Rot4.West };
            
            foreach (Rot4 rot in tryRots)
            {
                // First check GenConstruct (faster, basic rules)
                AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(buildingDef, cell, rot, map);
                if (!report.Accepted) continue;
                
                // Then comprehensive area validation (footprint + buffer)
                ValidationResult areaCheck = AreaValidator.ValidateBuildingArea(
                    map, 
                    cell, 
                    buildingDef, 
                    rot, 
                    bufferSize: 1, 
                    logLevel: "Minimal"); // Minimal logging for candidate screening
                
                if (areaCheck.IsValid)
                {
                    // Found a valid rotation
                    return true;
                }
            }
            
            // No valid rotation found
            if (logLevel == "Debug")
            {
                RimWatchLogger.Debug($"LocationFinder: No valid rotation for {buildingDef.label} at ({cell.x}, {cell.z})");
            }
            
            return false;
        }

        /// <summary>
        /// Returns true if there exists at least one rotation that Designator_Build accepts.
        /// DEPRECATED: Use HasAnyValidRotationWithAreaCheck for comprehensive validation.
        /// </summary>
        private static bool HasAnyAcceptedRotation(Map map, ThingDef buildingDef, IntVec3 cell)
        {
            if (!cell.InBounds(map)) return false;
            Rot4[] tryRots = new[] { Rot4.North, Rot4.East, Rot4.South, Rot4.West };
            ThingDef? stuffDef = null; // Not needed for validation here
            foreach (Rot4 rot in tryRots)
            {
                AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(buildingDef, cell, rot, map);
                if (report.Accepted) return true;
            }
            return false;
        }
    }
}

