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
    /// Automatic caravan formation and trading system.
    /// Forms caravans, plans routes, and executes trade missions automatically.
    /// v0.9.16: Automatic caravan trading system.
    /// </summary>
    public static class CaravanManager
    {
        // Configuration Constants
        private const int UPDATE_INTERVAL = 36000; // Check every 10 hours (36000 ticks)
        private const int MIN_SILVER_FOR_CARAVAN = 1000; // Minimum silver to form caravan
        private const int MIN_COLONISTS_FOR_CARAVAN = 5; // Need at least 5 colonists
        private const int CARAVAN_SIZE_MIN = 2; // Minimum colonists in caravan
        private const int CARAVAN_SIZE_MAX = 4; // Maximum colonists in caravan
        private const float MAX_TRAVEL_DAYS = 10f; // Maximum travel time in days
        
        // State tracking
        private static int _lastUpdateTick = 0;
        private static List<CaravanMission> _activeMissions = new List<CaravanMission>();
        private static Dictionary<int, int> _colonistLastCaravanTick = new Dictionary<int, int>();
        private const int COLONIST_CARAVAN_COOLDOWN = 180000; // 3 days cooldown (5 days * 60000 ticks)
        
        /// <summary>
        /// Main tick method for caravan management.
        /// </summary>
        public static void Tick(Map map)
        {
            try
            {
                int currentTick = Find.TickManager.TicksGame;
                
                if (currentTick - _lastUpdateTick < UPDATE_INTERVAL)
                    return;
                
                _lastUpdateTick = currentTick;
                
                // Update active missions
                UpdateActiveMissions();
                
                // Check if we should form a new caravan
                if (ShouldFormCaravan(map))
                {
                    FormTradeCaravan(map);
                }
                
                RimWatchLogger.Debug($"CaravanManager: {_activeMissions.Count} active missions");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("CaravanManager: Error in Tick", ex);
            }
        }
        
        /// <summary>
        /// Checks if conditions are right to form a caravan.
        /// </summary>
        private static bool ShouldFormCaravan(Map map)
        {
            // Need minimum colonists
            int freeColonists = map.mapPawns.FreeColonistsSpawned.Count(p => !p.Downed && !p.InMentalState);
            if (freeColonists < MIN_COLONISTS_FOR_CARAVAN)
            {
                RimWatchLogger.Debug($"CaravanManager: Not enough colonists ({freeColonists} < {MIN_COLONISTS_FOR_CARAVAN})");
                return false;
            }
            
            // Need minimum silver
            int silver = map.resourceCounter.GetCount(ThingDefOf.Silver);
            if (silver < MIN_SILVER_FOR_CARAVAN)
            {
                RimWatchLogger.Debug($"CaravanManager: Not enough silver ({silver} < {MIN_SILVER_FOR_CARAVAN})");
                return false;
            }
            
            // Don't form too many caravans at once
            if (_activeMissions.Count >= 2)
            {
                RimWatchLogger.Debug($"CaravanManager: Too many active missions ({_activeMissions.Count})");
                return false;
            }
            
            // Check if there are nearby settlements to trade with
            var nearbySettlements = FindNearbyTradeSettlements(map, MAX_TRAVEL_DAYS);
            if (nearbySettlements.Count == 0)
            {
                RimWatchLogger.Debug("CaravanManager: No nearby trade settlements");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Forms a trade caravan.
        /// </summary>
        private static void FormTradeCaravan(Map map)
        {
            try
            {
                // Find best settlement to trade with
                var settlements = FindNearbyTradeSettlements(map, MAX_TRAVEL_DAYS);
                if (settlements.Count == 0)
                    return;
                
                Settlement targetSettlement = ChooseBestTradeSettlement(map, settlements);
                if (targetSettlement == null)
                    return;
                
                // Select colonists for caravan
                List<Pawn> caravanPawns = SelectCaravanColonists(map);
                if (caravanPawns.Count < CARAVAN_SIZE_MIN)
                {
                    RimWatchLogger.Warning($"CaravanManager: Not enough available colonists for caravan");
                    return;
                }
                
                // Select trade goods
                List<Thing> tradeGoods = SelectTradeGoods(map);
                
                // Calculate required pack animals
                float totalMass = CalculateTotalMass(tradeGoods);
                int requiredPackAnimals = CalculateRequiredPackAnimals(totalMass, caravanPawns.Count);
                
                // Select pack animals
                List<Pawn> packAnimals = SelectPackAnimals(map, requiredPackAnimals);
                
                RimWatchLogger.Info($"CaravanManager: Forming caravan to {targetSettlement.Name}");
                RimWatchLogger.Info($"  - Colonists: {caravanPawns.Count}");
                RimWatchLogger.Info($"  - Pack animals: {packAnimals.Count}");
                RimWatchLogger.Info($"  - Trade goods: {tradeGoods.Count} items ({totalMass:F1} kg)");
                
                // Create caravan mission
                CaravanMission mission = new CaravanMission
                {
                    TargetSettlement = targetSettlement,
                    Colonists = caravanPawns,
                    PackAnimals = packAnimals,
                    TradeGoods = tradeGoods,
                    StartTick = Find.TickManager.TicksGame,
                    Status = CaravanStatus.Forming
                };
                
                _activeMissions.Add(mission);
                
                // Mark colonists as in caravan
                foreach (var pawn in caravanPawns)
                {
                    _colonistLastCaravanTick[pawn.thingIDNumber] = Find.TickManager.TicksGame;
                }
                
                // TODO: Actually form the caravan using RimWorld's caravan system
                // This is a placeholder - actual implementation would use CaravanFormingUtility
                RimWatchLogger.Info($"CaravanManager: Mission created (placeholder - actual caravan formation not yet implemented)");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("CaravanManager: Error forming caravan", ex);
            }
        }
        
        /// <summary>
        /// Finds nearby settlements that accept traders.
        /// </summary>
        private static List<Settlement> FindNearbyTradeSettlements(Map map, float maxDays)
        {
            List<Settlement> settlements = new List<Settlement>();
            
            if (map.Tile < 0)
                return settlements;
            
            foreach (var worldObject in Find.WorldObjects.AllWorldObjects)
            {
                if (worldObject is Settlement settlement)
                {
                    // Check if settlement is reachable and accepts trade
                    if (settlement.Faction != null && 
                        !settlement.Faction.HostileTo(Faction.OfPlayer) &&
                        settlement.Faction != Faction.OfPlayer)
                    {
                        // Calculate travel time
                        float travelDays = CalculateTravelDays(map.Tile, settlement.Tile);
                        
                        if (travelDays <= maxDays)
                        {
                            settlements.Add(settlement);
                        }
                    }
                }
            }
            
            return settlements;
        }
        
        /// <summary>
        /// Calculates travel time in days between two tiles.
        /// </summary>
        private static float CalculateTravelDays(int fromTile, int toTile)
        {
            if (fromTile < 0 || toTile < 0)
                return 999f;
            
            // Rough estimate based on tile distance
            float distance = Find.WorldGrid.ApproxDistanceInTiles(fromTile, toTile);
            
            // Assume average caravan speed of ~30 tiles per day
            return distance / 30f;
        }
        
        /// <summary>
        /// Chooses the best settlement to trade with.
        /// </summary>
        private static Settlement ChooseBestTradeSettlement(Map map, List<Settlement> settlements)
        {
            if (settlements.Count == 0)
                return null;
            
            // Score each settlement
            Settlement best = null;
            float bestScore = float.MinValue;
            
            foreach (var settlement in settlements)
            {
                float score = ScoreSettlement(map, settlement);
                
                if (score > bestScore)
                {
                    bestScore = score;
                    best = settlement;
                }
            }
            
            return best;
        }
        
        /// <summary>
        /// Scores a settlement for trading priority.
        /// </summary>
        private static float ScoreSettlement(Map map, Settlement settlement)
        {
            float score = 100f;
            
            // Prefer closer settlements
            float travelDays = CalculateTravelDays(map.Tile, settlement.Tile);
            score -= travelDays * 5f; // -5 points per day of travel
            
            // Prefer friendly factions
            int goodwill = settlement.Faction.GoodwillWith(Faction.OfPlayer);
            score += goodwill * 0.1f; // +0.1 points per goodwill
            
            // Prefer rich settlements (more likely to have silver)
            if (settlement.Faction.def.techLevel >= TechLevel.Industrial)
            {
                score += 20f;
            }
            
            return score;
        }
        
        /// <summary>
        /// Selects colonists for the caravan.
        /// </summary>
        private static List<Pawn> SelectCaravanColonists(Map map)
        {
            List<Pawn> selected = new List<Pawn>();
            int currentTick = Find.TickManager.TicksGame;
            
            // Get available colonists
            var available = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && 
                           !p.InMentalState && 
                           (!_colonistLastCaravanTick.ContainsKey(p.thingIDNumber) ||
                            currentTick - _colonistLastCaravanTick[p.thingIDNumber] > COLONIST_CARAVAN_COOLDOWN))
                .ToList();
            
            if (available.Count < CARAVAN_SIZE_MIN)
                return selected;
            
            // Prioritize colonists with high social skill
            available = available.OrderByDescending(p => 
            {
                int socialSkill = p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                int shootingSkill = p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                return socialSkill * 2 + shootingSkill; // Social more important
            }).ToList();
            
            // Select top colonists
            int count = Mathf.Min(CARAVAN_SIZE_MAX, available.Count);
            for (int i = 0; i < count; i++)
            {
                selected.Add(available[i]);
            }
            
            return selected;
        }
        
        /// <summary>
        /// Selects trade goods to bring.
        /// </summary>
        private static List<Thing> SelectTradeGoods(Map map)
        {
            List<Thing> goods = new List<Thing>();
            
            // Get all tradeable items
            var allItems = map.listerThings.AllThings
                .Where(t => t.def.category == ThingCategory.Item && 
                           !t.IsForbidden(Faction.OfPlayer) &&
                           t.MarketValue > 5f) // Only valuable items
                .ToList();
            
            // Prioritize high-value, low-weight items
            allItems = allItems.OrderByDescending(t => t.MarketValue / Mathf.Max(t.GetStatValue(StatDefOf.Mass), 0.1f))
                .ToList();
            
            // Select items up to reasonable weight
            float totalWeight = 0f;
            const float MAX_WEIGHT = 200f; // 200kg of goods
            
            foreach (var item in allItems)
            {
                float itemWeight = item.GetStatValue(StatDefOf.Mass) * item.stackCount;
                
                if (totalWeight + itemWeight <= MAX_WEIGHT)
                {
                    goods.Add(item);
                    totalWeight += itemWeight;
                }
                
                if (totalWeight >= MAX_WEIGHT * 0.9f)
                    break;
            }
            
            return goods;
        }
        
        /// <summary>
        /// Calculates total mass of items.
        /// </summary>
        private static float CalculateTotalMass(List<Thing> items)
        {
            float total = 0f;
            
            foreach (var item in items)
            {
                total += item.GetStatValue(StatDefOf.Mass) * item.stackCount;
            }
            
            return total;
        }
        
        /// <summary>
        /// Calculates required pack animals for carrying capacity.
        /// </summary>
        private static int CalculateRequiredPackAnimals(float totalMass, int colonistCount)
        {
            // Assume colonists can carry ~35kg each
            float colonistCapacity = colonistCount * 35f;
            
            // Remaining mass needs pack animals
            float remainingMass = Mathf.Max(0f, totalMass - colonistCapacity);
            
            // Assume pack animals carry ~75kg each
            int needed = Mathf.CeilToInt(remainingMass / 75f);
            
            return needed;
        }
        
        /// <summary>
        /// Selects pack animals for the caravan.
        /// </summary>
        private static List<Pawn> SelectPackAnimals(Map map, int count)
        {
            List<Pawn> selected = new List<Pawn>();
            
            if (count <= 0)
                return selected;
            
            // Find available pack animals (any animals with good carrying capacity)
            var available = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)
                .Where(p => p.RaceProps.Animal && 
                           !p.Downed &&
                           p.GetStatValue(StatDefOf.CarryingCapacity) > 50f) // Only animals that can carry decent weight
                .OrderByDescending(p => p.GetStatValue(StatDefOf.CarryingCapacity))
                .ToList();
            
            // Select top animals
            for (int i = 0; i < Mathf.Min(count, available.Count); i++)
            {
                selected.Add(available[i]);
            }
            
            return selected;
        }
        
        /// <summary>
        /// Updates status of active missions.
        /// </summary>
        private static void UpdateActiveMissions()
        {
            // Remove completed missions
            _activeMissions.RemoveAll(m => m.Status == CaravanStatus.Completed || 
                                          m.Status == CaravanStatus.Failed);
            
            // Update mission status
            foreach (var mission in _activeMissions.ToList())
            {
                // Check if caravan still exists
                // TODO: Track actual caravans and update status
                
                // Placeholder: Auto-complete missions after travel time
                int currentTick = Find.TickManager.TicksGame;
                int elapsedTicks = currentTick - mission.StartTick;
                
                if (elapsedTicks > 120000) // 2 days
                {
                    mission.Status = CaravanStatus.Completed;
                    RimWatchLogger.Info($"CaravanManager: Mission to {mission.TargetSettlement?.Name} completed");
                }
            }
        }
        
        /// <summary>
        /// Gets information about active caravan missions.
        /// </summary>
        public static List<CaravanMissionInfo> GetActiveMissions()
        {
            List<CaravanMissionInfo> info = new List<CaravanMissionInfo>();
            
            foreach (var mission in _activeMissions)
            {
                info.Add(new CaravanMissionInfo
                {
                    Destination = mission.TargetSettlement?.Name ?? "Unknown",
                    Status = mission.Status.ToString(),
                    ColonistCount = mission.Colonists?.Count ?? 0,
                    AnimalCount = mission.PackAnimals?.Count ?? 0,
                    GoodsCount = mission.TradeGoods?.Count ?? 0
                });
            }
            
            return info;
        }
    }
    
    /// <summary>
    /// Represents a caravan trading mission.
    /// </summary>
    public class CaravanMission
    {
        public Settlement TargetSettlement { get; set; }
        public List<Pawn> Colonists { get; set; }
        public List<Pawn> PackAnimals { get; set; }
        public List<Thing> TradeGoods { get; set; }
        public int StartTick { get; set; }
        public CaravanStatus Status { get; set; }
    }
    
    /// <summary>
    /// Caravan mission status.
    /// </summary>
    public enum CaravanStatus
    {
        Forming,        // Preparing to leave
        Traveling,      // On the way
        Trading,        // At destination, trading
        Returning,      // Coming back
        Completed,      // Mission complete
        Failed          // Mission failed
    }
    
    /// <summary>
    /// Public info about a caravan mission.
    /// </summary>
    public class CaravanMissionInfo
    {
        public string Destination { get; set; }
        public string Status { get; set; }
        public int ColonistCount { get; set; }
        public int AnimalCount { get; set; }
        public int GoodsCount { get; set; }
    }
}

