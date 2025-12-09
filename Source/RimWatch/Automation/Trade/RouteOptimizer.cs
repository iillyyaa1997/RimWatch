using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWatch.Automation.Trade
{
    /// <summary>
    /// Route optimization for caravans.
    /// Plans efficient multi-stop trade routes and calculates optimal paths.
    /// v0.9.16: Caravan route optimization system.
    /// </summary>
    public static class RouteOptimizer
    {
        /// <summary>
        /// Plans an optimal multi-stop trade route.
        /// </summary>
        public static TradeRoute PlanOptimalRoute(int startTile, List<Settlement> settlements, float maxTravelDays)
        {
            if (settlements.Count == 0)
                return null;
            
            // Single settlement - simple route
            if (settlements.Count == 1)
            {
                return CreateSimpleRoute(startTile, settlements[0]);
            }
            
            // Multiple settlements - optimize order
            return CreateOptimizedRoute(startTile, settlements, maxTravelDays);
        }
        
        /// <summary>
        /// Creates a simple route to one settlement.
        /// </summary>
        private static TradeRoute CreateSimpleRoute(int startTile, Settlement target)
        {
            TradeRoute route = new TradeRoute
            {
                StartTile = startTile,
                Stops = new List<RouteStop>()
            };
            
            float travelDays = CalculateTravelTime(startTile, target.Tile);
            
            route.Stops.Add(new RouteStop
            {
                Settlement = target,
                TravelDaysFromPrevious = travelDays,
                ExpectedTradeValue = EstimateTradeValue(target)
            });
            
            route.TotalTravelDays = travelDays * 2; // Round trip
            route.TotalExpectedValue = route.Stops.Sum(s => s.ExpectedTradeValue);
            
            return route;
        }
        
        /// <summary>
        /// Creates an optimized multi-stop route.
        /// </summary>
        private static TradeRoute CreateOptimizedRoute(int startTile, List<Settlement> settlements, float maxTravelDays)
        {
            // Use greedy nearest-neighbor algorithm for route optimization
            List<Settlement> remaining = new List<Settlement>(settlements);
            List<RouteStop> stops = new List<RouteStop>();
            
            int currentTile = startTile;
            float totalTravelDays = 0f;
            
            while (remaining.Count > 0)
            {
                // Find nearest settlement
                Settlement nearest = null;
                float nearestDistance = float.MaxValue;
                
                foreach (var settlement in remaining)
                {
                    float distance = CalculateTravelTime(currentTile, settlement.Tile);
                    
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = settlement;
                    }
                }
                
                if (nearest == null)
                    break;
                
                // Check if adding this stop exceeds max travel days
                float returnDistance = CalculateTravelTime(nearest.Tile, startTile);
                if (totalTravelDays + nearestDistance + returnDistance > maxTravelDays)
                {
                    break; // Can't add more stops
                }
                
                // Add stop
                stops.Add(new RouteStop
                {
                    Settlement = nearest,
                    TravelDaysFromPrevious = nearestDistance,
                    ExpectedTradeValue = EstimateTradeValue(nearest)
                });
                
                totalTravelDays += nearestDistance;
                currentTile = nearest.Tile;
                remaining.Remove(nearest);
            }
            
            // Calculate return trip
            if (stops.Count > 0)
            {
                float returnDistance = CalculateTravelTime(currentTile, startTile);
                totalTravelDays += returnDistance;
            }
            
            TradeRoute route = new TradeRoute
            {
                StartTile = startTile,
                Stops = stops,
                TotalTravelDays = totalTravelDays,
                TotalExpectedValue = stops.Sum(s => s.ExpectedTradeValue)
            };
            
            return route;
        }
        
        /// <summary>
        /// Calculates travel time between two tiles.
        /// </summary>
        private static float CalculateTravelTime(int fromTile, int toTile)
        {
            if (fromTile < 0 || toTile < 0)
                return 999f;
            
            float distance = Find.WorldGrid.ApproxDistanceInTiles(fromTile, toTile);
            
            // Get terrain difficulty
            float terrainMultiplier = CalculateTerrainDifficulty(fromTile, toTile);
            
            // Base speed: 30 tiles per day, modified by terrain
            float daysNeeded = (distance / 30f) * terrainMultiplier;
            
            return daysNeeded;
        }
        
        /// <summary>
        /// Calculates terrain difficulty multiplier.
        /// </summary>
        private static float CalculateTerrainDifficulty(int fromTile, int toTile)
        {
            // Simplified terrain difficulty - just use biome as proxy
            Tile fromTileInfo = Find.WorldGrid[fromTile];
            Tile toTileInfo = Find.WorldGrid[toTile];
            
            float difficulty = 1.0f;
            
            // Check hilliness of both tiles
            if (fromTileInfo.hilliness == Hilliness.LargeHills || toTileInfo.hilliness == Hilliness.LargeHills)
                difficulty += 0.3f;
            
            if (fromTileInfo.hilliness == Hilliness.Mountainous || toTileInfo.hilliness == Hilliness.Mountainous)
                difficulty += 0.6f;
            
            if (fromTileInfo.hilliness == Hilliness.Impassable || toTileInfo.hilliness == Hilliness.Impassable)
                difficulty += 2.0f;
            
            return difficulty;
        }
        
        /// <summary>
        /// Estimates trade value at a settlement.
        /// </summary>
        private static float EstimateTradeValue(Settlement settlement)
        {
            float value = 500f; // Base value
            
            // Richer factions have more silver
            if (settlement.Faction != null)
            {
                if (settlement.Faction.def.techLevel >= TechLevel.Industrial)
                    value += 300f;
                
                if (settlement.Faction.def.techLevel >= TechLevel.Spacer)
                    value += 500f;
                
                // Better relations = better trades
                int goodwill = settlement.Faction.GoodwillWith(Faction.OfPlayer);
                value += goodwill * 2f;
            }
            
            return value;
        }
        
        /// <summary>
        /// Calculates route profitability score.
        /// </summary>
        public static float CalculateRouteProfitability(TradeRoute route)
        {
            if (route == null || route.Stops.Count == 0)
                return 0f;
            
            // Profit = Expected value - (Travel time cost)
            float profit = route.TotalExpectedValue;
            
            // Subtract travel time cost (longer trips are riskier and tie up resources)
            profit -= route.TotalTravelDays * 50f; // -50 silver per day of travel
            
            return profit;
        }
        
        /// <summary>
        /// Finds alternative routes avoiding dangerous tiles.
        /// </summary>
        public static List<TradeRoute> FindAlternativeRoutes(int startTile, Settlement target, int alternativeCount = 3)
        {
            List<TradeRoute> routes = new List<TradeRoute>();
            
            // Create main route
            TradeRoute mainRoute = CreateSimpleRoute(startTile, target);
            if (mainRoute != null)
                routes.Add(mainRoute);
            
            // TODO: Generate alternative routes by varying path
            // This would require more sophisticated pathfinding
            
            return routes;
        }
        
        /// <summary>
        /// Checks if a route is safe (no hostile settlements nearby).
        /// </summary>
        public static bool IsRouteSafe(TradeRoute route)
        {
            if (route == null || route.Stops.Count == 0)
                return false;
            
            // Check each stop for nearby hostile settlements
            foreach (var stop in route.Stops)
            {
                if (stop.Settlement == null)
                    continue;
                
                // Find nearby world objects
                var nearbyObjects = Find.WorldObjects.AllWorldObjects
                    .Where(wo => wo is Settlement s && 
                                s.Tile != stop.Settlement.Tile &&
                                Find.WorldGrid.ApproxDistanceInTiles(s.Tile, stop.Settlement.Tile) < 10)
                    .ToList();
                
                foreach (var obj in nearbyObjects)
                {
                    if (obj is Settlement nearbySettlement)
                    {
                        if (nearbySettlement.Faction != null && 
                            nearbySettlement.Faction.HostileTo(Faction.OfPlayer))
                        {
                            RimWatchLogger.Warning($"RouteOptimizer: Hostile settlement near {stop.Settlement.Name}");
                            return false;
                        }
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Estimates required supplies for route.
        /// </summary>
        public static RouteSupplies CalculateRequiredSupplies(TradeRoute route, int caravanSize)
        {
            float totalDays = route.TotalTravelDays;
            
            return new RouteSupplies
            {
                FoodRequired = Mathf.CeilToInt(caravanSize * totalDays * 2f), // 2 meals per pawn per day
                MedicineRequired = Mathf.CeilToInt(caravanSize * 0.5f), // 0.5 medicine per pawn
                SilverForTrade = Mathf.CeilToInt(route.TotalExpectedValue * 0.5f) // Bring half of expected value
            };
        }
    }
    
    /// <summary>
    /// Represents a planned trade route.
    /// </summary>
    public class TradeRoute
    {
        public int StartTile { get; set; }
        public List<RouteStop> Stops { get; set; }
        public float TotalTravelDays { get; set; }
        public float TotalExpectedValue { get; set; }
    }
    
    /// <summary>
    /// Represents a stop on a trade route.
    /// </summary>
    public class RouteStop
    {
        public Settlement Settlement { get; set; }
        public float TravelDaysFromPrevious { get; set; }
        public float ExpectedTradeValue { get; set; }
    }
    
    /// <summary>
    /// Required supplies for a route.
    /// </summary>
    public class RouteSupplies
    {
        public int FoodRequired { get; set; }
        public int MedicineRequired { get; set; }
        public int SilverForTrade { get; set; }
    }
}

