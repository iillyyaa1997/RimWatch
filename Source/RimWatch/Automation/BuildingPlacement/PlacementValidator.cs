using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// Validates potential building placement locations.
    /// Checks safety, terrain, power availability, and other critical factors.
    /// </summary>
    public static class PlacementValidator
    {
        /// <summary>
        /// Checks if a location is safe for building.
        /// Considers: Home Area, Fog of War, hostile territories, enemy proximity.
        /// </summary>
        public static PlacementScore IsSafeLocation(Map map, IntVec3 location, string logLevel = "Moderate")
        {
            PlacementScore score = new PlacementScore();
            
            // Base score
            score.AddFactor("Base", 50);

            // Check if location is in bounds
            if (!location.InBounds(map))
            {
                score.Reject("Out of bounds");
                return score;
            }

            // Check Fog of War
            if (map.fogGrid.IsFogged(location))
            {
                score.Reject("Fog of war");
                return score;
            }
            score.AddFactor("Visible", 5);

            // Check Home Area
            Area homeArea = map.areaManager.Home;
            bool inHomeArea = homeArea != null && homeArea[location];
            
            if (inHomeArea)
            {
                score.AddFactor("In home area", 20);
            }
            else
            {
                // If no home area defined, check proximity to existing buildings
                List<Building> colonistBuildings = map.listerBuildings.allBuildingsColonist;
                
                if (colonistBuildings.Count > 0)
                {
                    float minDistance = colonistBuildings.Min(b => location.DistanceTo(b.Position));
                    
                    if (minDistance <= 10f)
                    {
                        score.AddFactor("Near base (10 tiles)", 15);
                    }
                    else if (minDistance <= 20f)
                    {
                        score.AddFactor("Near base (20 tiles)", 10);
                    }
                    else if (minDistance <= 30f)
                    {
                        score.AddFactor("Near base (30 tiles)", 5);
                    }
                    else
                    {
                        score.AddFactor("Far from base", -10);
                    }
                }
            }

            // Check for nearby enemies
            List<Pawn> nearbyEnemies = map.mapPawns.AllPawnsSpawned
                .Where(p => p.HostileTo(Faction.OfPlayer) && 
                           !p.Dead && 
                           !p.Downed &&
                           location.DistanceTo(p.Position) < 30f)
                .ToList();

            if (nearbyEnemies.Count > 0)
            {
                float closestEnemyDist = nearbyEnemies.Min(e => location.DistanceTo(e.Position));
                
                if (closestEnemyDist < 15f)
                {
                    score.Reject($"Enemy too close ({closestEnemyDist:F1} tiles)");
                    return score;
                }
                else
                {
                    score.AddFactor("Enemy nearby", -5);
                }
            }

            // Check for dangerous structures (hives, ancient dangers, etc.)
            List<Thing> dangerousThings = map.listerThings.AllThings
                .Where(t => t.def.defName.Contains("Hive") || 
                           t.def.defName.Contains("AncientCryptosleepCasket") ||
                           t.def.defName.Contains("AncientStructure"))
                .ToList();

            foreach (Thing danger in dangerousThings)
            {
                float distance = location.DistanceTo(danger.Position);
                if (distance < 20f)
                {
                    score.AddFactor("Near danger", -10);
                    break;
                }
            }

            return score;
        }

        /// <summary>
        /// Checks if terrain is valid for building placement.
        /// Considers: Standable, water/lava, fertility (for farms), roof requirements.
        /// </summary>
        public static PlacementScore IsValidTerrain(Map map, IntVec3 location, ThingDef buildingDef, string logLevel = "Moderate")
        {
            PlacementScore score = new PlacementScore();
            score.AddFactor("Base", 50);

            if (!location.InBounds(map))
            {
                score.Reject("Out of bounds");
                return score;
            }

            // ✅ CRITICAL: Check terrain FIRST (water, lava, impassable)
            TerrainDef terrain = location.GetTerrain(map);
            if (terrain == null)
            {
                score.Reject("Null terrain");
                return score;
            }

            // ✅ Reject water (buildings sink!)
            if (terrain.IsWater)
            {
                score.Reject($"Water terrain: {terrain.label}");
                return score;
            }

            // ✅ Reject lava/dangerous
            if (terrain.defName.Contains("Lava") || terrain.defName.Contains("Magma"))
            {
                score.Reject($"Dangerous terrain: {terrain.label}");
                return score;
            }

            // v0.7.9: CRITICAL - NEVER build on ore veins! They need to be mined first
            Thing firstMineable = location.GetFirstMineable(map);
            if (firstMineable != null)
            {
                score.Reject($"Ore vein present: {firstMineable.def.label}");
                return score;
            }

            // ✅ Check if standable (most buildings need standable ground)
            bool needsStandable = !buildingDef.building?.isNaturalRock == true; // Rocks don't need standable
            
            // ✅ CRITICAL: For walls, check if location WOULD be standable after plant removal
            bool isWall = buildingDef.building != null && 
                          buildingDef.passability == Traversability.Impassable &&
                          buildingDef.fillPercent >= 0.75f;
            
            if (needsStandable && !location.Standable(map))
            {
                // ✅ For walls: allow if the ONLY problem is plants (which will be pre-cleared)
                if (isWall)
                {
                    // Check if terrain itself is standable (ignoring things on it)
                    if (terrain.passability == Traversability.Impassable)
                    {
                        score.Reject($"Impassable terrain: {terrain.label}");
                        return score;
                    }
                    
                    // Check if there's a plant blocking
                    Plant plant = location.GetPlant(map);
                    if (plant != null)
                    {
                        // Plant will be pre-cleared, so it's OK
                        score.AddFactor("Standable (after pre-clear)", 5);
                    }
                    else
                    {
                        // Something else is blocking (building, pawn, etc)
                        score.Reject($"Not standable (terrain: {terrain.label})");
                        return score;
                    }
                }
                else
                {
                    // Non-walls: reject if not standable
                    score.Reject($"Not standable (terrain: {terrain.label})");
                    return score;
                }
            }
            else
            {
                score.AddFactor("Standable", 5);
            }

            // Check for constructed floor (positive for buildings)
            bool isConstructedFloor = terrain.defName.Contains("Floor") ||
                                     terrain.defName.Contains("Smooth") ||
                                     terrain.defName.Contains("Tile") ||
                                     terrain.defName.Contains("Carpet");

            if (isConstructedFloor)
            {
                score.AddFactor("Constructed floor", 10);
            }

            // Check roof status
            bool isRoofed = location.Roofed(map);
            
            // Some buildings prefer/require roof
            if (buildingDef.defName.Contains("Bed") || 
                buildingDef.defName.Contains("Research") ||
                buildingDef.defName.Contains("Stove"))
            {
                if (isRoofed)
                {
                    score.AddFactor("Indoor (preferred)", 10);
                }
                else
                {
                    score.AddFactor("Outdoor (not ideal)", -5);
                }
            }

            // ✅ CRITICAL FIX: Distinguish wood-powered vs other generators
            if (buildingDef.defName.Contains("Generator") || buildingDef.defName.Contains("Solar"))
            {
                bool isWoodPowered = buildingDef.defName.Contains("Wood") || 
                                     buildingDef.defName.Contains("Fueled") ||
                                     buildingDef.defName == "Generator"; // Vanilla wood generator
                
                if (isWoodPowered)
                {
                    // Wood-powered generators REQUIRE indoor placement (fire safety)
                    if (isRoofed)
                    {
                        score.AddFactor("Indoor (required for safety)", 20);
                    }
                    else
                    {
                        score.AddFactor("Outdoor (fire hazard!)", -50); // Strong penalty
                    }
                }
                else
                {
                    // Solar/Geothermal/Chemfuel prefer outdoor
                    if (!isRoofed)
                    {
                        score.AddFactor("Outdoor (preferred)", 10);
                    }
                    else
                    {
                        score.AddFactor("Indoor (not ideal)", -10);
                    }
                }
            }
            
            // ✅ CRITICAL FIX: Fueled stove MUST be indoors
            if (buildingDef.defName == "FueledStove")
            {
                if (isRoofed)
                {
                    score.AddFactor("Indoor (required)", 30);
                }
                else
                {
                    PlacementScore rejectedScore = new PlacementScore();
                    rejectedScore.Reject("Fueled stove requires indoor placement");
                    return rejectedScore;
                }
            }

            // Check if already occupied (including building size)
            IntVec2 buildingSize = buildingDef.size;
            
            for (int x = 0; x < buildingSize.x; x++)
            {
                for (int z = 0; z < buildingSize.z; z++)
                {
                    IntVec3 checkCell = location + new IntVec3(x, 0, z);
                    
                    if (!checkCell.InBounds(map))
                    {
                        score.Reject($"Building extends out of bounds at ({checkCell.x}, {checkCell.z})");
                        return score;
                    }
                    
                    // Check for buildings
                    Building existingBuilding = checkCell.GetFirstBuilding(map);
                    if (existingBuilding != null)
                    {
                        score.Reject($"Cell ({checkCell.x}, {checkCell.z}) occupied by {existingBuilding.def.label}");
                        return score;
                    }
                    
                    // Check for blueprints
                    List<Thing> blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint);
                    if (blueprints.Any(t => t.Position == checkCell))
                    {
                        score.Reject($"Cell ({checkCell.x}, {checkCell.z}) has blueprint");
                        return score;
                    }
                    
                    // Check for frames
                    List<Thing> frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame);
                    if (frames.Any(t => t.Position == checkCell))
                    {
                        score.Reject($"Cell ({checkCell.x}, {checkCell.z}) has frame under construction");
                        return score;
                    }
                    
                    // Check if standable
                    if (!checkCell.Standable(map))
                    {
                        score.Reject($"Cell ({checkCell.x}, {checkCell.z}) not standable");
                        return score;
                    }
                    
                    // Terrain affordance check (support required by building)
                    if (buildingDef.terrainAffordanceNeeded != null)
                    {
                        TerrainDef cellTerrain = checkCell.GetTerrain(map);
                        // If no terrain or terrain doesn't support required affordance -> reject
                        if (cellTerrain == null || cellTerrain.affordances == null || !cellTerrain.affordances.Contains(buildingDef.terrainAffordanceNeeded))
                        {
                            score.Reject($"Cell ({checkCell.x}, {checkCell.z}) lacks required terrain affordance ({buildingDef.terrainAffordanceNeeded.label})");
                            return score;
                        }
                    }

                    // Check for blocking items
                    Thing blockingItem = checkCell.GetFirstItem(map);
                    if (blockingItem != null && blockingItem.def.passability == Traversability.Impassable)
                    {
                        score.Reject($"Cell ({checkCell.x}, {checkCell.z}) blocked by {blockingItem.def.label}");
                        return score;
                    }
                }
            }

            return score;
        }

        /// <summary>
        /// Checks if building has power access (if it needs power).
        /// Returns high score if building doesn't need power, or if power is available.
        /// </summary>
        public static PlacementScore HasPowerAccess(Map map, IntVec3 location, ThingDef buildingDef, string logLevel = "Moderate")
        {
            PlacementScore score = new PlacementScore();
            score.AddFactor("Base", 50);

            // Check if building needs power
            bool needsPower = buildingDef.defName.Contains("Electric") ||
                            buildingDef.defName.Contains("Powered") ||
                            (buildingDef.comps != null && 
                             buildingDef.comps.Any(c => c.compClass?.Name == "CompPowerTrader"));

            if (!needsPower)
            {
                score.AddFactor("No power required", 10);
                return score;
            }

            // Building needs power - check if generators exist
            List<Building> generators = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.defName.Contains("Generator") ||
                           b.def.defName.Contains("Solar") ||
                           b.def.defName.Contains("Geothermal"))
                .ToList();

            if (generators.Count == 0)
            {
                score.Reject("No power generators on map");
                return score;
            }

            score.AddFactor("Power generator exists", 10);

            // Check if there's a power grid nearby (check for power conduits or powered buildings)
            bool powerNearby = false;
            int checkRadius = 6; // Standard power conduit range

            foreach (IntVec3 nearbyCell in GenRadial.RadialCellsAround(location, checkRadius, true))
            {
                if (!nearbyCell.InBounds(map)) continue;

                Building building = nearbyCell.GetFirstBuilding(map);
                if (building == null) continue;

                // Check for power conduits
                if (building.def.defName.Contains("PowerConduit"))
                {
                    powerNearby = true;
                    break;
                }

                // Check for powered buildings
                if (building.def.comps != null && 
                    building.def.comps.Any(c => c.compClass?.Name == "CompPowerTrader"))
                {
                    powerNearby = true;
                    break;
                }
            }

            if (powerNearby)
            {
                score.AddFactor("Power grid nearby", 20);
            }
            else
            {
                // Power exists but not nearby - neutral (cables can be built)
                score.AddFactor("Power available (needs cable)", 5);
            }

            return score;
        }

        /// <summary>
        /// Checks if building can be placed at location (size check).
        /// </summary>
        public static bool CanPlaceBuildingAt(Map map, IntVec3 location, IntVec2 size)
        {
            if (!location.InBounds(map)) return false;

            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    IntVec3 checkCell = location + new IntVec3(x, 0, z);

                    if (!checkCell.InBounds(map)) return false;
                    if (!checkCell.Standable(map)) return false;
                    
                    // ✅ CRITICAL: Check for ANY buildings (including walls!)
                    Building existingBuilding = checkCell.GetFirstBuilding(map);
                    if (existingBuilding != null) return false;

                    // ✅ Check for blueprints AND frames
                    List<Thing> blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint);
                    if (blueprints.Any(t => t.Position == checkCell)) return false;
                    
                    List<Thing> frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame);
                    if (frames.Any(t => t.Position == checkCell)) return false;

                    // ✅ CRITICAL: Check for items on ground (don't build on stuff!)
                    List<Thing> items = checkCell.GetThingList(map);
                    foreach (Thing item in items)
                    {
                        // Ignore pawns, plants, filth - only block on actual items
                        if (item.def.category == ThingCategory.Item && item.def.EverHaulable)
                        {
                            return false; // Items blocking placement
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Combines all validation scores for a complete assessment.
        /// </summary>
        public static PlacementScore GetCompleteScore(Map map, IntVec3 location, ThingDef buildingDef, string logLevel = "Moderate")
        {
            PlacementScore safetyScore = IsSafeLocation(map, location, logLevel);
            if (!safetyScore.IsValid)
            {
                return safetyScore; // Critical failure
            }

            PlacementScore terrainScore = IsValidTerrain(map, location, buildingDef, logLevel);
            if (!terrainScore.IsValid)
            {
                return terrainScore; // Critical failure
            }

            PlacementScore powerScore = HasPowerAccess(map, location, buildingDef, logLevel);
            if (!powerScore.IsValid)
            {
                return powerScore; // Critical failure
            }

            // Combine all scores
            PlacementScore combinedScore = new PlacementScore();
            
            foreach (var factor in safetyScore.Factors)
            {
                combinedScore.AddFactor($"Safety: {factor.Key}", factor.Value / 3);
            }
            
            foreach (var factor in terrainScore.Factors)
            {
                combinedScore.AddFactor($"Terrain: {factor.Key}", factor.Value / 3);
            }
            
            foreach (var factor in powerScore.Factors)
            {
                combinedScore.AddFactor($"Power: {factor.Key}", factor.Value / 3);
            }

            return combinedScore;
        }
    }
}

