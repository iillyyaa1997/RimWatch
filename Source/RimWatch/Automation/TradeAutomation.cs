using RimWatch.Core;
using RimWatch.Utils;
using RimWatch.Automation.ColonyDevelopment;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation
{
    /// <summary>
    /// 🛒 Trade Automation - автоматическое управление торговлей.
    /// Мониторит торговцев и управляет торговыми операциями.
    /// </summary>
    public static class TradeAutomation
    {
        private static int _tickCounter = 0;
        private static bool _isEnabled = false;
        private const int UpdateInterval = 900; // Обновление каждые 15 секунд (900 тиков)
        
        // v0.7.9: Cooldown system to prevent forbid/unforbid spam
        private static Dictionary<int, int> _itemLastToggledTick = new Dictionary<int, int>();
        private const int ForbidToggleCooldown = 18000; // 5 minutes (300 seconds * 60 ticks)

        /// <summary>
        /// Включена ли автоматизация торговли.
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                RimWatchLogger.Info($"TradeAutomation: {(value ? "Enabled" : "Disabled")}");
            }
        }

        /// <summary>
        /// Вызывается каждый тик для обновления автоматизации.
        /// </summary>
        public static void Tick()
        {
            if (!IsEnabled) return;
            if (!RimWatchCore.AutopilotEnabled) return;

            _tickCounter++;
            if (_tickCounter >= UpdateInterval)
            {
                _tickCounter = 0;
                ManageTrade();
            }
        }

        /// <summary>
        /// Manages trade operations.
        /// </summary>
        private static void ManageTrade()
        {
            // v0.8.3: Log execution start
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            RimWatchLogger.LogExecutionStart("TradeAutomation", "ManageTrade", new Dictionary<string, object>
            {
                { "map", map.uniqueID }
            });

            TradeStatus status = AnalyzeTradeStatus(map);
            
            // v0.8.3: Log trade status analysis
            RimWatchLogger.LogDecision("TradeAutomation", "TradeStatusAnalysis", new Dictionary<string, object>
            {
                { "activeTraders", status.ActiveTraders },
                { "traderTypes", string.Join(", ", status.TraderTypes) },
                { "silverCount", status.SilverCount }
            });

            // Report active traders
            if (status.ActiveTraders > 0)
            {
                RimWatchLogger.Info($"[TradeAutomation] 🛒 {status.ActiveTraders} traders available on map!");
                
                foreach (string traderType in status.TraderTypes)
                {
                    RimWatchLogger.Info($"TradeAutomation: - {traderType}");
                }
            }

            // Check silver reserves
            CheckSilverReserves(map, status);

            // Periodic report
            if (_tickCounter == 0 && status.ActiveTraders == 0)
            {
                RimWatchLogger.Debug($"TradeAutomation: No traders present (Silver: {status.SilverCount})");
            }

            // **NEW: Execute actions**
            AutoManageForbiddenItems(map);
            AutoTrade(map, status);
            
            // **v0.7 ADVANCED: Production for trade**
            AutoProduceTradeGoods(map);
            
            // v0.8.3: Log execution end
            stopwatch.Stop();
            RimWatchLogger.LogExecutionEnd("TradeAutomation", "ManageTrade", true, stopwatch.ElapsedMilliseconds,
                $"Traders={status.ActiveTraders}, Silver={status.SilverCount}");
        }

        /// <summary>
        /// Analyzes trade status.
        /// </summary>
        private static TradeStatus AnalyzeTradeStatus(Map map)
        {
            TradeStatus status = new TradeStatus();

            // Find traders on map
            List<Pawn> traders = map.mapPawns.AllPawnsSpawned
                .Where(p => p.trader != null && 
                           p.trader.traderKind != null && 
                           !p.HostileTo(Faction.OfPlayer))
                .ToList();

            status.ActiveTraders = traders.Count;

            // Get trader types
            status.TraderTypes = traders
                .Select(t => t.trader.traderKind.label)
                .Distinct()
                .ToList();

            // Count silver
            status.SilverCount = map.listerThings.ThingsOfDef(ThingDefOf.Silver)
                .Sum(t => t.stackCount);

            return status;
        }

        /// <summary>
        /// Checks silver reserves.
        /// </summary>
        private static void CheckSilverReserves(Map map, TradeStatus status)
        {
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            int recommendedSilver = colonistCount * 100;

            if (status.SilverCount < 100)
            {
                RimWatchLogger.Info($"TradeAutomation: ⚠️ Very low silver! ({status.SilverCount})");
            }
            else if (status.SilverCount < recommendedSilver)
            {
                RimWatchLogger.Debug($"TradeAutomation: Silver reserves moderate ({status.SilverCount})");
            }
        }

        /// <summary>
        /// Structure for trade status.
        /// </summary>
        private class TradeStatus
        {
            public int ActiveTraders { get; set; } = 0;
            public List<string> TraderTypes { get; set; } = new List<string>();
            public int SilverCount { get; set; } = 0;
        }

        // ==================== ACTION METHODS ====================

        /// <summary>
        /// Automatically manages forbidden/allowed status of items on the map.
        /// v0.7.9: Added cooldown system to prevent spam toggling.
        /// </summary>
        private static void AutoManageForbiddenItems(Map map)
        {
            // v0.8.3: Log execution start
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            int currentTick = Find.TickManager.TicksGame;
            
            // Check if there are enemies on the map
            bool enemiesPresent = map.mapPawns.AllPawnsSpawned
                .Any(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed);

            // Get all things on the map
            List<Thing> allThings = map.listerThings.AllThings
                .Where(t => t.def.EverHaulable && // Can be hauled
                           !t.IsInAnyStorage() && // Not already in storage
                           t.Spawned) // Spawned on map
                .ToList();
                
            // v0.8.3: Log state tracking start
            RimWatchLogger.LogStateChange("TradeAutomation", "ItemManagementStart", "Processing", 
                $"Processing {allThings.Count} items (enemies: {enemiesPresent})");

            int allowed = 0;
            int forbidden = 0;
            int skippedCooldown = 0;

            foreach (Thing thing in allThings)
            {
                // v0.7.9: Check cooldown - DON'T touch items that were recently toggled
                int thingHash = thing.GetHashCode();
                if (_itemLastToggledTick.TryGetValue(thingHash, out int lastToggle))
                {
                    int timeSinceToggle = currentTick - lastToggle;
                    if (timeSinceToggle < ForbidToggleCooldown)
                    {
                        skippedCooldown++;
                        continue; // Still on cooldown
                    }
                }
                
                bool currentlyForbidden = thing.IsForbidden(Faction.OfPlayer);
                
                // If enemies are present, forbid items EXCEPT weapons/medicine (colonists need them!)
                if (enemiesPresent)
                {
                    // ✅ NEVER forbid weapons - colonists need to equip them during raids
                    if (thing.def.IsWeapon)
                    {
                        if (currentlyForbidden)
                        {
                            // v0.8.3: Log state change
                            RimWatchLogger.LogStateChange("TradeAutomation", "Forbidden", "Allowed",
                                $"{thing.LabelShort} (weapon, combat mode)");
                            
                            thing.SetForbidden(false, warnOnFail: false);
                            _itemLastToggledTick[thingHash] = currentTick;
                            allowed++;
                        }
                        continue;
                    }
                    
                    // ✅ NEVER forbid medicine - colonists need it for healing
                    if (thing.def.IsMedicine)
                    {
                        if (currentlyForbidden)
                        {
                            // v0.8.3: Log state change
                            RimWatchLogger.LogStateChange("TradeAutomation", "Forbidden", "Allowed",
                                $"{thing.LabelShort} (medicine, combat mode)");
                            
                            thing.SetForbidden(false, warnOnFail: false);
                            _itemLastToggledTick[thingHash] = currentTick;
                            allowed++;
                        }
                        continue;
                    }
                    
                    // Forbid everything else during combat
                    if (!currentlyForbidden)
                    {
                        thing.SetForbidden(true, warnOnFail: false);
                        _itemLastToggledTick[thingHash] = currentTick;
                        forbidden++;
                    }
                    continue;
                }

                // v0.8.1: NEW APPROACH - Allow everything by default, forbid ONLY explicit junk
                bool isJunk = IsJunkItem(thing);

                // Forbid only confirmed junk
                if (isJunk && !currentlyForbidden)
                {
                    // v0.8.3: Log state change for junk detection
                    if (forbidden < 5) // Only log first few to avoid spam
                    {
                        RimWatchLogger.LogStateChange("TradeAutomation", "Allowed", "Forbidden",
                            $"{thing.LabelShort} (detected as junk)");
                    }
                    
                    thing.SetForbidden(true, warnOnFail: false);
                    _itemLastToggledTick[thingHash] = currentTick;
                    forbidden++;
                }
                // Unforbid everything else
                else if (!isJunk && currentlyForbidden)
                {
                    // v0.8.3: Log state change for allowing useful items
                    if (allowed < 5) // Only log first few to avoid spam
                    {
                        RimWatchLogger.LogStateChange("TradeAutomation", "Forbidden", "Allowed",
                            $"{thing.LabelShort} (useful item)");
                    }
                    
                    thing.SetForbidden(false, warnOnFail: false);
                    _itemLastToggledTick[thingHash] = currentTick;
                    allowed++;
                }
            }
            
            // v0.8.3: Log execution end with comprehensive summary
            stopwatch.Stop();
            RimWatchLogger.LogExecutionEnd("TradeAutomation", "AutoManageForbiddenItems", true, stopwatch.ElapsedMilliseconds,
                $"Allowed={allowed}, Forbidden={forbidden}, Skipped={skippedCooldown}/{allThings.Count}");

            // Log activity (only if significant changes)
            if (allowed > 10 || forbidden > 10)
            {
                RimWatchLogger.Info($"TradeAutomation: Managed items - Allowed: {allowed}, Forbade: {forbidden}");
            }
            
            // Cleanup old entries (every 10 minutes)
            if (currentTick % 36000 == 0)
            {
                List<int> toRemove = _itemLastToggledTick.Where(kvp => currentTick - kvp.Value > 36000).Select(kvp => kvp.Key).ToList();
                foreach (int hash in toRemove) _itemLastToggledTick.Remove(hash);
            }
        }

        /// <summary>
        /// v0.8.1: REMOVED - No longer needed with blacklist approach.
        /// Old whitelist method replaced with "forbid only junk" strategy.
        /// </summary>
        [System.Obsolete("No longer used - switched to blacklist approach")]
        private static bool ShouldAllowItem(Thing thing)
        {
            // This method is no longer called
            return true;
        }

        /// <summary>
        /// v0.8.1: BLACKLIST approach - Forbid ONLY explicit junk, allow everything else.
        /// This is much safer and more universal than whitelist approach.
        /// </summary>
        private static bool IsJunkItem(Thing thing)
        {
            // ========== CRITICAL: NEVER FORBID USEFUL ITEMS ==========
            
            // NEVER forbid minified buildings (furniture, beds, etc)
            if (thing is MinifiedThing) return false;
            
            // NEVER forbid any weapons, apparel, or medicine
            if (thing.def.IsWeapon) return false;
            if (thing.def.IsApparel && thing.HitPoints >= thing.MaxHitPoints * 0.3f) return false; // Keep if >30% HP
            if (thing.def.IsMedicine) return false;
            
            // NEVER forbid food (unless rotten)
            if (thing.def.IsNutritionGivingIngestible)
            {
                CompRottable rottable = thing.TryGetComp<CompRottable>();
                if (rottable == null || rottable.Stage != RotStage.Rotting)
                {
                    return false; // Good food - keep it!
                }
                // Rotten food - junk
                return true;
            }
            
            // NEVER forbid resources, materials, components
            if (thing.def.IsStuff) return false;
            if (thing.def == ThingDefOf.Steel) return false;
            if (thing.def == ThingDefOf.WoodLog) return false;
            if (thing.def == ThingDefOf.ComponentIndustrial) return false;
            if (thing.def.defName == "ComponentSpacer") return false;
            if (thing.def.defName == "Plasteel") return false;
            if (thing.def == ThingDefOf.Silver) return false;
            if (thing.def == ThingDefOf.Gold) return false;
            
            // NEVER forbid anything with market value >= 10 silver
            if (thing.MarketValue >= 10f) return false;
            
            // ========== BLACKLIST: These ARE junk ==========
            
            // 1. Worthless items (< 2 silver)
            if (thing.MarketValue < 2f) return true;
            
            // 2. Stone chunks - REMOVED v0.8.1: User feedback - chunks don't interfere, leave them alone
            // They're scattered around and colonists won't haul them unless needed anyway
            
            // 3. Enemy corpses (not colonist corpses!)
            if (thing is Corpse corpse)
            {
                if (corpse.InnerPawn != null && corpse.InnerPawn.Faction != Faction.OfPlayer)
                {
                    return true; // Enemy corpse - junk
                }
            }
            
            // 4. Completely destroyed apparel (< 30% HP)
            if (thing.def.IsApparel && thing.HitPoints < thing.MaxHitPoints * 0.3f)
            {
                return true; // Tattered rags - junk
            }
            
            // 5. Awful quality weapons/armor (only if there are better alternatives)
            // NOTE: This is conservative - only awful quality AND low HP
            if (thing.TryGetQuality(out QualityCategory quality))
            {
                if (quality == QualityCategory.Awful && thing.HitPoints < thing.MaxHitPoints * 0.5f)
                {
                    return true; // Awful + damaged - junk
                }
            }
            
            // ========== DEFAULT: NOT JUNK ==========
            // If we're not sure, DON'T forbid it!
            return false;
        }

        /// <summary>
        /// Automatically analyzes trade opportunities and logs recommendations.
        /// NOTE: Actual automated trading requires UI interaction, which is complex in RimWorld 1.6.
        /// This implementation provides intelligent analysis and recommendations.
        /// </summary>
        private static void AutoTrade(Map map, TradeStatus status)
        {
            // Only analyze when traders are present
            if (status.ActiveTraders == 0) return;

            // Find traders
            List<Pawn> traders = map.mapPawns.AllPawnsSpawned
                .Where(p => p.trader != null && 
                           p.trader.traderKind != null && 
                           !p.HostileTo(Faction.OfPlayer))
                .ToList();

            foreach (Pawn trader in traders)
            {
                AnalyzeTradeOpportunity(map, trader, status);
            }
        }

        /// <summary>
        /// Analyzes a specific trader and logs trade recommendations.
        /// </summary>
        private static void AnalyzeTradeOpportunity(Map map, Pawn trader, TradeStatus status)
        {
            string traderType = trader.trader.traderKind.label;
            
            // Analyze what we need
            ColonyNeeds needs = AnalyzeColonyNeeds(map);
            
            // Analyze what we can sell
            List<Thing> sellableItems = GetSellableItems(map, needs);
            
            // Log trade recommendation
            if (needs.HasCriticalNeeds || sellableItems.Count > 0)
            {
                RimWatchLogger.Info($"🛒 TradeAutomation: Trade opportunity with {traderType}");
                
                // Log what to buy
                if (needs.NeedsMedicine)
                {
                    RimWatchLogger.Info($"   📦 BUY: Medicine (current: {needs.MedicineCount}, need: {needs.MedicineNeeded})");
                }
                if (needs.NeedsComponents)
                {
                    RimWatchLogger.Info($"   📦 BUY: Components (current: {needs.ComponentCount}, need: {needs.ComponentsNeeded})");
                }
                if (needs.NeedsMeals && traderType.Contains("food"))
                {
                    RimWatchLogger.Info($"   📦 BUY: Food (current meals: {needs.MealCount}, colonists: {needs.ColonistCount})");
                }
                
                // Log what to sell
                if (sellableItems.Count > 0)
                {
                    int totalValue = sellableItems.Sum(t => (int)t.MarketValue);
                    RimWatchLogger.Info($"   💰 SELL: {sellableItems.Count} items (est. value: {totalValue} silver)");
                    
                    // Group by category for cleaner logging
                    var grouped = sellableItems.GroupBy(t => GetItemCategory(t));
                    foreach (var group in grouped.Take(5)) // Show top 5 categories
                    {
                        int count = group.Count();
                        int value = group.Sum(t => (int)t.MarketValue);
                        RimWatchLogger.Info($"      • {group.Key}: {count} items ({value} silver)");
                    }
                }
                
                // Log silver status
                if (status.SilverCount < 500)
                {
                    RimWatchLogger.Info($"   ⚠️ Low silver reserves ({status.SilverCount}) - prioritize selling");
                }
            }
        }

        /// <summary>
        /// Analyzes colony needs for trading decisions.
        /// </summary>
        private static ColonyNeeds AnalyzeColonyNeeds(Map map)
        {
            ColonyNeeds needs = new ColonyNeeds();
            
            needs.ColonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            
            // Medicine
            needs.MedicineCount = map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine).Sum(t => t.stackCount);
            needs.MedicineNeeded = needs.ColonistCount * 5; // 5 medicine per colonist
            needs.NeedsMedicine = needs.MedicineCount < needs.MedicineNeeded;
            
            // Components
            needs.ComponentCount = map.listerThings.ThingsOfDef(ThingDefOf.ComponentIndustrial)
                .Sum(t => t.stackCount);
            needs.ComponentsNeeded = 10; // Always keep 10 components
            needs.NeedsComponents = needs.ComponentCount < needs.ComponentsNeeded;
            
            // Food
            needs.MealCount = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Count;
            needs.NeedsMeals = needs.MealCount < needs.ColonistCount * 5;
            
            needs.HasCriticalNeeds = needs.NeedsMedicine || needs.NeedsComponents || needs.NeedsMeals;
            
            return needs;
        }

        /// <summary>
        /// Gets list of items that can be sold without impacting colony.
        /// </summary>
        private static List<Thing> GetSellableItems(Map map, ColonyNeeds needs)
        {
            List<Thing> sellable = new List<Thing>();
            
            // Get all hauled/stored items
            List<Thing> allItems = map.listerThings.AllThings
                .Where(t => t.def.EverHaulable && 
                           t.Spawned &&
                           !t.IsForbidden(Faction.OfPlayer) &&
                           t.MarketValue > 10f) // Only sell items worth >10 silver
                .ToList();
            
            foreach (Thing thing in allItems)
            {
                // Sell surplus food (keep 20 meals per colonist)
                if (thing.def.IsNutritionGivingIngestible)
                {
                    int totalFood = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Count;
                    int keepFood = needs.ColonistCount * 20;
                    if (totalFood > keepFood)
                    {
                        sellable.Add(thing);
                    }
                    continue;
                }
                
                // Sell tattered apparel
                if (thing.def.IsApparel)
                {
                    float hpPercent = (float)thing.HitPoints / thing.MaxHitPoints;
                    if (hpPercent < 0.5f) // Less than 50% HP
                    {
                        sellable.Add(thing);
                        continue;
                    }
                    
                    // Sell awful quality
                    if (thing.TryGetQuality(out QualityCategory quality) && quality == QualityCategory.Awful)
                    {
                        sellable.Add(thing);
                        continue;
                    }
                }
                
                // Sell excess drugs (keep 10 per type)
                if (thing.def.IsDrug)
                {
                    int totalOfType = map.listerThings.ThingsOfDef(thing.def).Sum(t => t.stackCount);
                    if (totalOfType > 10)
                    {
                        sellable.Add(thing);
                    }
                    continue;
                }
                
                // Sell art (luxury items)
                if (thing.def.IsArt)
                {
                    sellable.Add(thing);
                    continue;
                }
                
                // Sell excess materials (keep reasonable amounts)
                if (thing.def.IsStuff)
                {
                    int totalOfType = map.listerThings.ThingsOfDef(thing.def).Sum(t => t.stackCount);
                    int keepAmount = GetKeepAmount(thing.def);
                    if (totalOfType > keepAmount)
                    {
                        sellable.Add(thing);
                    }
                }
            }
            
            return sellable;
        }

        /// <summary>
        /// Determines how much of a material to keep (rest can be sold).
        /// </summary>
        private static int GetKeepAmount(ThingDef def)
        {
            // Steel: keep 500
            if (def == ThingDefOf.Steel) return 500;
            
            // Wood: keep 1000
            if (def == ThingDefOf.WoodLog) return 1000;
            
            // Plasteel: keep 100
            if (def.defName == "Plasteel") return 100;
            
            // Silver: keep 1000
            if (def == ThingDefOf.Silver) return 1000;
            
            // Gold: keep 100
            if (def == ThingDefOf.Gold) return 100;
            
            // Other materials: keep 200
            return 200;
        }

        /// <summary>
        /// Gets display category for an item.
        /// </summary>
        private static string GetItemCategory(Thing thing)
        {
            if (thing.def.IsApparel) return "Apparel";
            if (thing.def.IsWeapon) return "Weapons";
            if (thing.def.IsNutritionGivingIngestible) return "Food";
            if (thing.def.IsMedicine) return "Medicine";
            if (thing.def.IsDrug) return "Drugs";
            if (thing.def.IsStuff) return "Materials";
            if (thing.def.IsArt) return "Art";
            return "Other";
        }

        /// <summary>
        /// Structure for colony needs analysis.
        /// </summary>
        private class ColonyNeeds
        {
            public int ColonistCount { get; set; } = 0;
            public int MedicineCount { get; set; } = 0;
            public int MedicineNeeded { get; set; } = 0;
            public bool NeedsMedicine { get; set; } = false;
            public int ComponentCount { get; set; } = 0;
            public int ComponentsNeeded { get; set; } = 0;
            public bool NeedsComponents { get; set; } = false;
            public int MealCount { get; set; } = 0;
            public bool NeedsMeals { get; set; } = false;
            public bool HasCriticalNeeds { get; set; } = false;
        }

        // ==================== v0.7 ADVANCED FEATURES ====================

        private static int lastProductionBillTick = -9999;
        private const int ProductionBillCooldown = 9000; // 150 seconds

        /// <summary>
        /// Automatically creates production bills for profitable trade goods.
        /// NEW in v0.7 - Production for trade system.
        /// </summary>
        private static void AutoProduceTradeGoods(Map map)
        {
            try
            {
                // Cooldown check
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - lastProductionBillTick < ProductionBillCooldown)
                {
                    return;
                }

                int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
                if (colonistCount < 3)
                {
                    return; // Too early for trade production
                }

                // Check silver reserves - if low, produce trade goods
                int silverCount = map.listerThings.ThingsOfDef(ThingDefOf.Silver)?.Sum(t => t.stackCount) ?? 0;
                if (silverCount > 2000)
                {
                    return; // Sufficient silver, no need to produce for trade
                }

                // Determine current development stage for smarter production choices
                DevelopmentStage stage = DevelopmentStageManager.GetCurrentStage(map);
                
                // Log decision snapshot for production system
                RimWatchLogger.LogDecision("TradeAutomation", "ProductionForTradeCheck", new Dictionary<string, object>
                {
                    { "stage", stage.ToString() },
                    { "colonists", colonistCount },
                    { "silver", silverCount }
                });

                // Find production buildings
                List<Building> craftingBenches = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.defName.Contains("TableTailor") || 
                               b.def.defName.Contains("TableSculpting") ||
                               b.def.defName.Contains("DrugLab"))
                    .ToList();

                if (craftingBenches.Count == 0)
                {
                    RimWatchLogger.Debug("TradeAutomation: No crafting benches found for trade production");
                    return; // No production facilities
                }

                int billsCreated = 0;

                foreach (Building bench in craftingBenches.Take(2)) // Limit to 2 benches
                {
                    if (!(bench is Building_WorkTable workTable)) continue;

                    // Check existing bills
                    if (workTable.BillStack != null && workTable.BillStack.Count >= 3)
                    {
                        RimWatchLogger.Debug($"TradeAutomation: Skipping {workTable.def.label} - already has {workTable.BillStack.Count} bills");
                        continue; // Too many bills already
                    }

                    // Try to add profitable production bill
                    RecipeDef? profitableRecipe = FindProfitableRecipe(workTable, map, stage, silverCount);
                    if (profitableRecipe != null)
                    {
                        // Avoid duplicate bills for the same recipe
                        if (workTable.BillStack != null && workTable.BillStack.Bills.Any(b => b.recipe == profitableRecipe))
                        {
                            RimWatchLogger.Debug($"TradeAutomation: {workTable.def.label} already has bill for {profitableRecipe.label}, skipping");
                            continue;
                        }

                        // Create production bill
                        Bill_Production bill = new Bill_Production(profitableRecipe);
                        bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                        bill.repeatCount = 5; // Produce 5 items
                        bill.suspended = false;

                        workTable.BillStack.AddBill(bill);
                        billsCreated++;

                        RimWatchLogger.Info($"💰 TradeAutomation: Added production bill for {profitableRecipe.label} at {workTable.def.label}");
                        
                        // Log structured decision for production bill
                        RimWatchLogger.LogDecision("TradeAutomation", "AddProductionBill", new Dictionary<string, object>
                        {
                            { "stage", stage.ToString() },
                            { "bench", workTable.def.defName },
                            { "recipe", profitableRecipe.defName },
                            { "silver", silverCount }
                        });
                    }
                }

                if (billsCreated > 0)
                {
                    lastProductionBillTick = currentTick;
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("TradeAutomation: Error in AutoProduceTradeGoods", ex);
            }
        }

        /// <summary>
        /// Finds a profitable recipe for a workbench.
        /// Takes into account current development stage and silver reserves.
        /// </summary>
        private static RecipeDef? FindProfitableRecipe(
            Building_WorkTable workTable,
            Map map,
            DevelopmentStage stage,
            int silverCount)
        {
            try
            {
                // Get all recipes for this workbench
                List<RecipeDef> availableRecipes = workTable.def.AllRecipes
                    .Where(r => r.AvailableNow && r.products != null && r.products.Count > 0)
                    .ToList();

                if (availableRecipes.Count == 0)
                {
                    return null;
                }

                // Helper predicates
                bool IsClothing(RecipeDef r) =>
                    r.defName.Contains("Make") && r.defName.Contains("Apparel");

                bool IsSculpture(RecipeDef r) =>
                    r.defName.Contains("Sculpture");

                bool IsSoftDrug(RecipeDef r) =>
                    r.defName.Contains("Drug") && !r.defName.Contains("Hard");

                // Stage-based priority:
                // Emergency/Early: prioritize clothing (warmth / value), then sculptures, then drugs
                // MidGame+: prioritize sculptures, then clothing, then drugs
                IEnumerable<RecipeDef> ordered;
                if (stage == DevelopmentStage.Emergency || stage == DevelopmentStage.EarlyGame)
                {
                    ordered = availableRecipes
                        .OrderByDescending(r => IsClothing(r))
                        .ThenByDescending(r => IsSculpture(r))
                        .ThenByDescending(r => IsSoftDrug(r));
                }
                else
                {
                    ordered = availableRecipes
                        .OrderByDescending(r => IsSculpture(r))
                        .ThenByDescending(r => IsClothing(r))
                        .ThenByDescending(r => IsSoftDrug(r));
                }

                RecipeDef? best = ordered.FirstOrDefault(r => IsClothing(r) || IsSculpture(r) || IsSoftDrug(r));

                if (best != null)
                {
                    RimWatchLogger.LogDecision("TradeAutomation", "SelectProductionRecipe", new Dictionary<string, object>
                    {
                        { "stage", stage.ToString() },
                        { "bench", workTable.def.defName },
                        { "recipe", best.defName },
                        { "silver", silverCount }
                    });
                }

                return best;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"TradeAutomation: Error in FindProfitableRecipe: {ex.Message}");
                return null;
            }
        }
    }
}
