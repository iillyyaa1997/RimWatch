using RimWatch.Automation.BuildingPlacement;
using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation
{
    /// <summary>
    /// 🌾 Farming Automation - автоматическое управление сельским хозяйством.
    /// Управляет зонами выращивания, животноводством и продовольственными запасами.
    /// </summary>
    public static class FarmingAutomation
    {
        private static int _tickCounter = 0;
        private static bool _isEnabled = false;
        private const int UpdateInterval = 900; // Обновление каждые 15 секунд (900 тиков)
        
        // Cooldowns for specific actions (in game ticks)
        private static int lastHuntingTick = -9999;
        private static int lastTamingTick = -9999;
        private static int lastSlaughterTick = -9999;
        private const int HuntingCooldown = 1800; // 30 seconds
        private const int TamingCooldown = 3600; // 60 seconds (taming takes time)
        private const int SlaughterCooldown = 1800; // 30 seconds

        /// <summary>
        /// Включена ли автоматизация фермерства.
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                RimWatchLogger.Info($"FarmingAutomation: {(value ? "Enabled" : "Disabled")}");
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
                RimWatchLogger.Info("[FarmingAutomation] Tick! Running farming analysis...");
                ManageFarming();
            }
        }

        /// <summary>
        /// Manages farming operations.
        /// </summary>
        private static void ManageFarming()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            if (colonistCount == 0) return;

            // v0.8.3: Log execution start
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            RimWatchLogger.LogExecutionStart("FarmingAutomation", "ManageFarming", new Dictionary<string, object>
            {
                { "colonists", colonistCount },
                { "map", map.uniqueID }
            });

            // Analyze food situation
            FarmingNeeds needs = AnalyzeFarmingNeeds(map, colonistCount);
            
            // v0.8.3: Log farming needs analysis
            RimWatchLogger.LogDecision("FarmingAutomation", "FarmingNeedsAnalysis", new Dictionary<string, object>
            {
                { "needsMoreFields", needs.NeedsMoreFields },
                { "harvestablePlants", needs.HarvestablePlants },
                { "hasTamableAnimals", needs.HasTamableAnimals },
                { "mealCount", needs.MealCount },
                { "rawFoodCount", needs.RawFoodCount },
                { "wildAnimalCount", needs.WildAnimalCount },
                { "tamedAnimalCount", needs.TamedAnimalCount }
            });

            // Check growing zones
            if (needs.NeedsMoreFields)
            {
                RimWatchLogger.Info("FarmingAutomation: ⚠️ Need more growing zones for food production!");
            }

            // Check harvestable plants
            if (needs.HarvestablePlants > 0)
            {
                RimWatchLogger.Info($"FarmingAutomation: 🌾 {needs.HarvestablePlants} plants ready to harvest");
            }

            // Check animals
            if (needs.HasTamableAnimals)
            {
                RimWatchLogger.Debug("FarmingAutomation: Wild animals available for taming");
            }

            // Food reserves status
            ReportFoodStatus(needs);

            // **NEW: Execute actions based on analysis**
            AutoEnableCooking(map, needs); // v0.8.0: CRITICAL FIX - Auto-enable cooking if no meals
            AutoDesignateBerryHarvest(map, needs);  // ✅ NEW: Сбор ягод
            AutoDesignateHunting(map, needs);
            AutoDesignateSlaughter(map);
            AutoDesignateTaming(map);
            AutoCreateGrowingZones(map, needs, colonistCount);
            AutoSowCrops(map, needs);
            
            // **v0.7 ADVANCED: Animal management and hay preparation**
            AutoManageAnimals(map);
            AutoPrepareHayForWinter(map, needs);
            
            // v0.8.3: Log execution end with performance metrics
            stopwatch.Stop();
            RimWatchLogger.LogExecutionEnd("FarmingAutomation", "ManageFarming", true, stopwatch.ElapsedMilliseconds, 
                $"Meals={needs.MealCount}, RawFood={needs.RawFoodCount}, Animals={needs.TamedAnimalCount}");
        }

        /// <summary>
        /// v0.8.0: CRITICAL FIX - Auto-enable cooking if no meals available.
        /// </summary>
        private static void AutoEnableCooking(Map map, FarmingNeeds needs)
        {
            try
            {
                // Check if we have meals
                if (needs.MealCount > 0)
                {
                    return; // We have meals, cooking is working
                }

                // Check if we have raw food to cook
                if (needs.RawFoodCount == 0)
                {
                    // No raw food, can't cook anyway
                    return;
                }

                int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;

                // Find colonists who can cook
                var potentialCooks = map.mapPawns.FreeColonistsSpawned
                    .Where(p => p.workSettings != null &&
                               !p.Downed && !p.Dead &&
                               p.skills != null &&
                               p.skills.GetSkill(SkillDefOf.Cooking) != null)
                    .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Cooking).Level)
                    .ToList();

                if (potentialCooks.Count == 0)
                {
                    RimWatchLogger.Warning("⚠️ FarmingAutomation: No colonists can cook!");
                    return;
                }

                // Check if cooking is enabled for anyone
                WorkTypeDef cookingWork = DefDatabase<WorkTypeDef>.GetNamed("Cooking");
                if (cookingWork == null) return; // Cooking work type not found
                
                bool hasCooks = potentialCooks.Any(p => p.workSettings.WorkIsActive(cookingWork));

                // v0.8.0: Auto-enable cooking for best cooks if no meals
                if (!hasCooks)
                {
                    // Enable cooking for top 2 cooks
                    var topCooks = potentialCooks.Take(Math.Min(2, potentialCooks.Count)).ToList();

                    foreach (var cook in topCooks)
                    {
                        if (cook.workSettings.GetPriority(cookingWork) == 0)
                        {
                            cook.workSettings.SetPriority(cookingWork, 1); // Highest priority
                            int cookingSkill = cook.skills.GetSkill(SkillDefOf.Cooking).Level;
                            RimWatchLogger.Warning($"⚠️ FarmingAutomation: AUTO-ENABLED cooking for {cook.LabelShort} (Cooking {cookingSkill}) - no meals available!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"FarmingAutomation: Error in AutoEnableCooking: {ex.Message}");
            }
        }

        /// <summary>
        /// Автоматически назначает сбор ягод (диких съедобных растений).
        /// </summary>
        private static void AutoDesignateBerryHarvest(Map map, FarmingNeeds needs)
        {
            try
            {
                // Если еды достаточно - не нужно собирать ягоды
                int totalFood = needs.MealCount + needs.RawFoodCount;
                int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
                
                if (totalFood > colonistCount * 15)
                {
                    // Еды достаточно
                    return;
                }

                IntVec3 baseCenter = BaseZoneCache.BaseCenter != IntVec3.Invalid 
                    ? BaseZoneCache.BaseCenter 
                    : map.Center;
                List<Plant> berriesToHarvest = new List<Plant>();

                // Ищем съедобные дикие растения (ягоды) в радиусе 60 клеток
                for (int radius = 10; radius < 60 && berriesToHarvest.Count < 20; radius += 10)
                {
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(baseCenter, radius, false))
                    {
                        if (!cell.InBounds(map)) continue;
                        if (map.fogGrid.IsFogged(cell)) continue;

                        // Ищем растения на клетке
                        List<Thing> things = cell.GetThingList(map);
                        foreach (Thing thing in things)
                        {
                            if (thing is Plant plant)
                            {
                                // Проверяем что это дикое съедобное растение (ягода)
                                if (plant.def.plant != null && 
                                    plant.def.plant.harvestedThingDef != null &&
                                    plant.def.plant.harvestedThingDef.IsNutritionGivingIngestible &&
                                    plant.HarvestableNow &&
                                    !plant.IsCrop) // НЕ посевы!
                                {
                                    // Проверяем нет ли уже designation
                                    if (map.designationManager.DesignationOn(plant, DesignationDefOf.HarvestPlant) == null)
                                    {
                                        berriesToHarvest.Add(plant);
                                        
                                        if (berriesToHarvest.Count >= 20) // Ограничение 20 ягодных кустов
                                            break;
                                    }
                                }
                            }
                        }
                    }
                }

                // Назначаем сбор найденных ягод
                if (berriesToHarvest.Count > 0)
                {
                    foreach (Plant berry in berriesToHarvest)
                    {
                        map.designationManager.AddDesignation(new Designation(berry, DesignationDefOf.HarvestPlant));
                    }
                    
                    RimWatchLogger.Info($"🫐 FarmingAutomation: Designated {berriesToHarvest.Count} wild berries for harvest (food: {totalFood})");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FarmingAutomation: Error in AutoDesignateBerryHarvest", ex);
            }
        }

        /// <summary>
        /// Analyzes farming needs.
        /// </summary>
        private static FarmingNeeds AnalyzeFarmingNeeds(Map map, int colonistCount)
        {
            FarmingNeeds needs = new FarmingNeeds();

            // Count food
            needs.MealCount = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Count;
            needs.RawFoodCount = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSource).Count - needs.MealCount;

            // Count growing zones
            needs.GrowingZones = map.zoneManager.AllZones
                .Count(z => z is Zone_Growing);

            // Check if we need more fields (rule of thumb: 1 zone per 2 colonists)
            needs.NeedsMoreFields = needs.GrowingZones < colonistCount / 2;

            // Count harvestable plants
            needs.HarvestablePlants = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant)
                .Count(t => t is Plant p && p.HarvestableNow);

            // Check for tamable animals
            needs.HasTamableAnimals = map.mapPawns.AllPawnsSpawned
                .Any(p => p.RaceProps.Animal && p.Faction == null);

            // Current season
            needs.CurrentSeason = GenLocalDate.Season(map);

            return needs;
        }

        /// <summary>
        /// Reports food status.
        /// </summary>
        private static void ReportFoodStatus(FarmingNeeds needs)
        {
            int totalFood = needs.MealCount + needs.RawFoodCount;
            
            if (totalFood < 10)
            {
                RimWatchLogger.Info($"FarmingAutomation: ⚠️ LOW FOOD! Only {totalFood} meals/raw food available");
            }
            else if (totalFood < 30)
            {
                RimWatchLogger.Info($"FarmingAutomation: ℹ️ Food reserves moderate: {totalFood} meals");
            }
            else
            {
                RimWatchLogger.Debug($"FarmingAutomation: Food reserves good: {totalFood} meals ✓");
            }
        }

        /// <summary>
        /// Structure for storing farming needs.
        /// </summary>
        private class FarmingNeeds
        {
            public int MealCount { get; set; } = 0;
            public int RawFoodCount { get; set; } = 0;
            public int GrowingZones { get; set; } = 0;
            public bool NeedsMoreFields { get; set; } = false;
            public int HarvestablePlants { get; set; } = 0;
            public bool HasTamableAnimals { get; set; } = false;
            public int WildAnimalCount { get; set; } = 0;
            public int TamedAnimalCount { get; set; } = 0;
            public Season CurrentSeason { get; set; } = Season.Undefined;
        }

        // ==================== ACTION METHODS ====================

        /// <summary>
        /// Automatically designates wild animals for hunting when food is low.
        /// </summary>
        private static void AutoDesignateHunting(Map map, FarmingNeeds needs)
        {
            // Check cooldown to avoid spamming hunting designations
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastHuntingTick < HuntingCooldown)
            {
                return; // Too soon since last hunting designation
            }
            
            // Check if we need food
            int totalFood = needs.MealCount + needs.RawFoodCount;
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            int foodThreshold = colonistCount * 10; // 10 meals per colonist

            if (totalFood >= foodThreshold)
            {
                return; // We have enough food
            }

            // v0.8.0: CRITICAL FIX - Auto-enable hunting if no active hunters but food is low
            var potentialHunters = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.workSettings != null && 
                           !p.Downed && !p.Dead &&
                           p.skills != null &&
                           p.skills.GetSkill(SkillDefOf.Shooting) != null &&
                           p.skills.GetSkill(SkillDefOf.Shooting).Level >= 3) // At least shooting 3
                .ToList();

            if (potentialHunters.Count == 0)
            {
                RimWatchLogger.Warning("⚠️ FarmingAutomation: No colonists with Shooting 3+ available for hunting!");
                return; // No one can hunt
            }

            // Check if hunting is enabled for anyone
            bool hasActiveHunters = potentialHunters.Any(p => p.workSettings.WorkIsActive(WorkTypeDefOf.Hunting));

            // v0.8.0: Auto-enable hunting for best shooters if food is critical
            if (!hasActiveHunters && totalFood < colonistCount * 5) // Critical: < 5 meals per colonist
            {
                // Enable hunting for top 2 shooters
                var topShooters = potentialHunters
                    .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Shooting).Level)
                    .Take(2)
                    .ToList();

                foreach (var shooter in topShooters)
                {
                    if (shooter.workSettings.GetPriority(WorkTypeDefOf.Hunting) == 0)
                    {
                        shooter.workSettings.SetPriority(WorkTypeDefOf.Hunting, 1); // Highest priority
                        RimWatchLogger.Warning($"⚠️ FarmingAutomation: AUTO-ENABLED hunting for {shooter.LabelShort} (Shooting {shooter.skills.GetSkill(SkillDefOf.Shooting).Level}) - food crisis!");
                    }
                }
                
                hasActiveHunters = true; // Now we have hunters
            }

            if (!hasActiveHunters)
            {
                return; // Still no hunters available
            }

            // Find wild animals suitable for hunting
            List<Pawn> wildAnimals = map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Animal &&
                           p.Faction == null && // Wild
                           !p.Dead && !p.Downed &&
                           p.RaceProps.baseBodySize >= 0.3f && // Not too small
                           map.designationManager.DesignationOn(p, DesignationDefOf.Hunt) == null) // Not already designated
                .OrderByDescending(p => p.GetStatValue(StatDefOf.MeatAmount)) // Prefer animals with more meat
                .ThenBy(p => p.RaceProps.predator ? 1 : 0) // Prefer herbivores (safer)
                .ToList();

            if (wildAnimals.Count == 0)
            {
                return; // No suitable animals
            }

            // Designate up to 3 animals for hunting
            int designated = 0;
            int maxToDesignate = Math.Min(3, wildAnimals.Count);

            List<string> huntTargets = new List<string>();
            for (int i = 0; i < maxToDesignate; i++)
            {
                Pawn animal = wildAnimals[i];
                Designation designation = new Designation(animal, DesignationDefOf.Hunt);
                map.designationManager.AddDesignation(designation);
                designated++;

                float meat = animal.GetStatValue(StatDefOf.MeatAmount);
                string animalType = animal.RaceProps.predator ? "predator" : "herbivore";
                huntTargets.Add($"{animal.LabelShort} ({animalType}, meat: {meat:F0})");
            }

            if (designated > 0)
            {
                lastHuntingTick = currentTick; // Update cooldown
                RimWatchLogger.Info($"🏹 FarmingAutomation: Hunting {designated} animals (food: {totalFood}/{foodThreshold})");
                foreach (string target in huntTargets)
                {
                    RimWatchLogger.Info($"   • {target}");
                }
            }
        }

        /// <summary>
        /// Automatically designates excess tamed animals for slaughter.
        /// </summary>
        private static void AutoDesignateSlaughter(Map map)
        {
            // v0.8.3: Log execution start
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Check cooldown to avoid spamming slaughter designations
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastSlaughterTick < SlaughterCooldown)
            {
                return; // Too soon since last slaughter designation
            }
            
            // Count tamed animals by type
            Dictionary<ThingDef, List<Pawn>> tamedByType = new Dictionary<ThingDef, List<Pawn>>();

            foreach (Pawn animal in map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Animal && 
                           p.Faction == Faction.OfPlayer && 
                           !p.Dead && !p.Downed))
            {
                if (!tamedByType.ContainsKey(animal.def))
                {
                    tamedByType[animal.def] = new List<Pawn>();
                }
                tamedByType[animal.def].Add(animal);
            }

            int totalDesignated = 0;
            int speciesOverLimit = 0;

            // Check each type for excess
            foreach (var kvp in tamedByType)
            {
                ThingDef animalDef = kvp.Key;
                List<Pawn> animals = kvp.Value;

                // Define max animals per type based on utility
                int maxPerType = 10; // Default
                
                // Adjust based on animal utility
                if (animalDef.race.packAnimal) maxPerType = 5; // Pack animals - keep fewer
                if (animalDef.race.predator) maxPerType = 3; // Predators - keep few
                if (animalDef.race.herdAnimal) maxPerType = 20; // Herd animals - can keep more

                if (animals.Count <= maxPerType)
                {
                    continue; // Not over limit
                }

                speciesOverLimit++;
                int excessCount = animals.Count - maxPerType;

                // Select animals for slaughter (oldest, weakest, already wounded)
                List<Pawn> candidates = animals
                    .Where(a => map.designationManager.DesignationOn(a, DesignationDefOf.Slaughter) == null)
                    .OrderByDescending(a => a.health.hediffSet.PainTotal) // Most wounded first
                    .ThenByDescending(a => a.ageTracker.AgeBiologicalYears) // Then oldest
                    .Take(animals.Count - maxPerType)
                    .ToList();
                
                // v0.8.3: Log slaughter decision for this species
                if (candidates.Count > 0)
                {
                    RimWatchLogger.LogDecision("FarmingAutomation", "SlaughterExcess", new Dictionary<string, object>
                    {
                        { "species", animalDef.defName },
                        { "currentCount", animals.Count },
                        { "maxPerType", maxPerType },
                        { "excessCount", excessCount },
                        { "toSlaughter", candidates.Count },
                        { "isPack", animalDef.race.packAnimal },
                        { "isPredator", animalDef.race.predator },
                        { "isHerd", animalDef.race.herdAnimal }
                    });
                }

                foreach (Pawn animal in candidates)
                {
                    Designation designation = new Designation(animal, DesignationDefOf.Slaughter);
                    map.designationManager.AddDesignation(designation);
                    totalDesignated++;

                    RimWatchLogger.Info($"FarmingAutomation: 🔪 Designated {animal.LabelShort} for slaughter (excess {animalDef.label})");
                }
            }

            if (totalDesignated > 0)
            {
                lastSlaughterTick = currentTick; // Update cooldown
                
                // v0.8.3: Log execution end with results
                stopwatch.Stop();
                RimWatchLogger.LogExecutionEnd("FarmingAutomation", "AutoDesignateSlaughter", true, stopwatch.ElapsedMilliseconds,
                    $"Designated {totalDesignated} animals from {speciesOverLimit} species");
                
                RimWatchLogger.Info($"FarmingAutomation: Designated {totalDesignated} animals for slaughter (population control)");
            }
            else
            {
                // v0.8.3: Log execution end (no action)
                stopwatch.Stop();
                RimWatchLogger.LogExecutionEnd("FarmingAutomation", "AutoDesignateSlaughter", true, stopwatch.ElapsedMilliseconds,
                    "No animals over limit");
            }
        }

        /// <summary>
        /// Automatically designates useful wild animals for taming.
        /// </summary>
        private static void AutoDesignateTaming(Map map)
        {
            // v0.8.3: Log execution start
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Check cooldown to avoid spamming taming designations
            // Taming takes time, so we use a longer cooldown
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastTamingTick < TamingCooldown)
            {
                return; // Too soon since last taming designation
            }
            
            // Check if we have tamers
            bool hasTamers = map.mapPawns.FreeColonistsSpawned
                .Any(p => p.workSettings != null && 
                         p.workSettings.WorkIsActive(WorkTypeDefOf.Handling) &&
                         !p.Downed && !p.Dead);

            if (!hasTamers)
            {
                RimWatchLogger.LogDecision("FarmingAutomation", "SkipTaming", new Dictionary<string, object>
                {
                    { "reason", "NoTamersAvailable" }
                });
                return; // No tamers available
            }

            // v0.8.0: CRITICAL FIX - Improved taming limits
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;

            // Count current tamed animals
            int currentTamed = map.mapPawns.AllPawnsSpawned
                .Count(p => p.RaceProps.Animal && p.Faction == Faction.OfPlayer);

            // v0.8.0: STRICTER LIMIT - max 5 animals per colonist (not 3)
            int maxTamed = colonistCount * 5;

            if (currentTamed >= maxTamed)
            {
                RimWatchLogger.LogDecision("FarmingAutomation", "SkipTaming", new Dictionary<string, object>
                {
                    { "reason", "MaxLimitReached" },
                    { "currentTamed", currentTamed },
                    { "maxTamed", maxTamed },
                    { "colonists", colonistCount }
                });
                RimWatchLogger.Debug($"FarmingAutomation: Already have {currentTamed} animals (max {maxTamed} for {colonistCount} colonists)");
                return; // We have enough animals
            }
            
            // v0.8.0: Count by type to avoid too many of same animal
            Dictionary<ThingDef, int> tamedByType = new Dictionary<ThingDef, int>();
            foreach (Pawn tamed in map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Animal && p.Faction == Faction.OfPlayer))
            {
                if (!tamedByType.ContainsKey(tamed.def))
                    tamedByType[tamed.def] = 0;
                tamedByType[tamed.def]++;
            }

            // Find useful wild animals for taming
            // NOTE: Wildness check temporarily disabled due to API uncertainty
            List<Pawn> wildAnimals = map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Animal &&
                           p.Faction == null && // Wild
                           !p.Dead && !p.Downed &&
                           map.designationManager.DesignationOn(p, DesignationDefOf.Tame) == null) // Not already designated
                .OrderByDescending(p => GetAnimalUtility(p)) // Most useful first
                .ToList();

            if (wildAnimals.Count == 0)
            {
                return; // No suitable animals
            }

            // v0.8.0: Designate up to 2 animals for taming, with type limits
            int designated = 0;
            int maxToDesignate = Math.Min(2, Math.Min(wildAnimals.Count, maxTamed - currentTamed));

            List<string> tameTargets = new List<string>();
            for (int i = 0; i < wildAnimals.Count && designated < maxToDesignate; i++)
            {
                Pawn animal = wildAnimals[i];
                
                // ✅ CRITICAL: Check if already designated
                Designation existing = map.designationManager.DesignationOn(animal, DesignationDefOf.Tame);
                if (existing != null) continue;
                
                // v0.8.0: CRITICAL FIX - Don't tame more than 3 of same type
                int currentOfType = tamedByType.ContainsKey(animal.def) ? tamedByType[animal.def] : 0;
                if (currentOfType >= 3)
                {
                    RimWatchLogger.Debug($"FarmingAutomation: Skipping {animal.LabelShort} - already have {currentOfType} of this type (max 3)");
                    continue;
                }
                
                // Calculate utility score for decision logging
                float utility = GetAnimalUtility(animal);
                
                // v0.8.3: Log taming decision with detailed context
                RimWatchLogger.LogDecision("FarmingAutomation", "TameAnimal", new Dictionary<string, object>
                {
                    { "animal", animal.LabelShort },
                    { "species", animal.def.defName },
                    { "utilityScore", utility },
                    { "currentOfType", currentOfType },
                    { "currentTotalTamed", currentTamed },
                    { "maxTamed", maxTamed }
                });
                
                // Designate for taming
                    Designation designation = new Designation(animal, DesignationDefOf.Tame);
                    map.designationManager.AddDesignation(designation);
                    designated++;
                
                // Update tracking
                if (!tamedByType.ContainsKey(animal.def))
                    tamedByType[animal.def] = 0;
                tamedByType[animal.def]++;

                    string utilityType = utility >= 3 ? "pack/battle" : (utility >= 2 ? "producer" : "basic");
                    tameTargets.Add($"{animal.LabelShort} ({utilityType}, utility: {utility:F1})");
            }

            if (designated > 0)
            {
                lastTamingTick = currentTick; // Update cooldown
                
                // v0.8.3: Log execution end with results
                stopwatch.Stop();
                RimWatchLogger.LogExecutionEnd("FarmingAutomation", "AutoDesignateTaming", true, stopwatch.ElapsedMilliseconds, 
                    $"Designated {designated} animals for taming");
                
                RimWatchLogger.Info($"🐾 FarmingAutomation: Taming {designated} animals ({currentTamed}/{maxTamed} currently tamed)");
                foreach (string target in tameTargets)
                {
                    RimWatchLogger.Info($"   • {target}");
                }
            }
            else
            {
                // v0.8.3: Log execution end (no actions taken)
                stopwatch.Stop();
                RimWatchLogger.LogExecutionEnd("FarmingAutomation", "AutoDesignateTaming", true, stopwatch.ElapsedMilliseconds, 
                    "No suitable animals found for taming");
            }
        }

        /// <summary>
        /// Automatically creates growing zones when food production is insufficient.
        /// Conservative approach: Creates medium-sized zones (11x11) in fertile soil near the base.
        /// </summary>
        /// <summary>
        /// Automatically creates growing zones based on colony needs.
        /// ✅ UPDATED: Creates multiple smaller zones closer to base for different crops.
        /// </summary>
        private static void AutoCreateGrowingZones(Map map, FarmingNeeds needs, int colonistCount)
        {
            try
            {
                // Only create zones if food is low or we don't have enough zones
                int totalFood = needs.MealCount + needs.RawFoodCount;
                int foodThreshold = colonistCount * 5; // 5 meals per colonist minimum
                
                if (totalFood >= foodThreshold && !needs.NeedsMoreFields)
                {
                    return; // We have enough food and fields
                }

                // Check if we have growers
                bool hasGrowers = map.mapPawns.FreeColonistsSpawned
                    .Any(p => p.workSettings != null && 
                             p.workSettings.WorkIsActive(WorkTypeDefOf.Growing) &&
                             !p.Downed && !p.Dead);

                if (!hasGrowers)
                {
                    RimWatchLogger.Info("FarmingAutomation: ⚠️ No growers available to work growing zones!");
                    return;
                }

                // ✅ NEW: Create multiple smaller zones (2-3) closer to base for diversity
                int zonesToCreate = Math.Min(3, colonistCount); // 1 zone per colonist, max 3
                int existingZoneCount = map.zoneManager.AllZones.Count(z => z is Zone_Growing);
                
                // Don't spam zones
                if (existingZoneCount >= zonesToCreate * 2)
                {
                    RimWatchLogger.Debug($"FarmingAutomation: Already have {existingZoneCount} zones, enough for now");
                    return;
                }
                
                // Create up to 2 zones per call (not too many at once)
                int zonesToCreateThisCall = Math.Min(2, zonesToCreate - existingZoneCount);
                if (zonesToCreateThisCall <= 0)
                    return;
                
                List<CellRect> createdZones = new List<CellRect>();
                List<ThingDef> plantsToGrow = GetDiverseCrops(map, zonesToCreateThisCall);

                for (int i = 0; i < zonesToCreateThisCall; i++)
                {
                    // ✅ NEW: Find location closer to base on each iteration
                    IntVec3 zoneCenter = FindGrowingZoneLocationNearBase(map, createdZones);
                    
                    if (zoneCenter == IntVec3.Invalid)
                    {
                        RimWatchLogger.Warning("FarmingAutomation: Could not find suitable location for growing zone");
                        break;
                    }

                    // ✅ SMALLER zones: 9x9 or 7x7 depending on iteration
                    int zoneSize = i == 0 ? 9 : 7; // First zone larger
                    IntVec3 zoneMin = new IntVec3(
                        zoneCenter.x - zoneSize / 2,
                        zoneCenter.y,
                        zoneCenter.z - zoneSize / 2
                    );
                    IntVec3 zoneMax = new IntVec3(
                        zoneCenter.x + zoneSize / 2,
                        zoneCenter.y,
                        zoneCenter.z + zoneSize / 2
                    );

                    // Create the growing zone
                    Zone_Growing zone = new Zone_Growing(map.zoneManager);
                    
                    // Add cells to zone
                    int cellsAdded = 0;
                    for (int x = zoneMin.x; x <= zoneMax.x; x++)
                    {
                        for (int z = zoneMin.z; z <= zoneMax.z; z++)
                        {
                            IntVec3 cell = new IntVec3(x, 0, z);
                            if (cell.InBounds(map) && CanPlantAt(map, cell))
                            {
                                zone.AddCell(cell);
                                cellsAdded++;
                            }
                        }
                    }

                    // Only register zone if we added enough cells
                    int minCells = zoneSize * zoneSize / 2; // At least 50% of zone must be plantable
                    if (cellsAdded >= minCells)
                    {
                        map.zoneManager.RegisterZone(zone);
                        
                        // ✅ NEW: Set DIFFERENT plant for each zone (diversity!)
                        ThingDef plantToGrow = i < plantsToGrow.Count ? plantsToGrow[i] : ThingDef.Named("Plant_Rice");
                        if (plantToGrow != null)
                        {
                            zone.SetPlantDefToGrow(plantToGrow);
                            RimWatchLogger.Info($"🌱 FarmingAutomation: Created {zoneSize}x{zoneSize} growing zone at ({zoneCenter.x}, {zoneCenter.z}) for {plantToGrow.label} - {cellsAdded} cells");
                        }
                        else
                        {
                            RimWatchLogger.Info($"🌱 FarmingAutomation: Created {zoneSize}x{zoneSize} growing zone at ({zoneCenter.x}, {zoneCenter.z}) - {cellsAdded} cells");
                        }
                        
                        createdZones.Add(new CellRect(zoneMin.x, zoneMin.z, zoneSize, zoneSize));
                    }
                    else
                    {
                        RimWatchLogger.Debug($"FarmingAutomation: Zone location had insufficient plantable cells ({cellsAdded}/{minCells})");
                    }
                }

                if (createdZones.Count > 0)
                {
                    RimWatchLogger.Info($"🌱 FarmingAutomation: Created {createdZones.Count} new growing zone(s) (food: {totalFood}/{foodThreshold})");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FarmingAutomation: Error in AutoCreateGrowingZones", ex);
            }
        }

        /// <summary>
        /// Gets a list of diverse crops for multiple zones (rice, corn, potatoes).
        /// ✅ NEW: Returns different plants for crop diversity.
        /// </summary>
        private static List<ThingDef> GetDiverseCrops(Map map, int count)
        {
            List<ThingDef> crops = new List<ThingDef>();
            
            // Standard crops in priority order
            ThingDef rice = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Rice");
            ThingDef corn = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Corn");
            ThingDef potatoes = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Potatoes");
            
            // Add available crops
            if (rice != null) crops.Add(rice);
            if (corn != null) crops.Add(corn);
            if (potatoes != null) crops.Add(potatoes);
            
            // If we need more than 3, repeat
            while (crops.Count < count && crops.Count > 0)
            {
                crops.Add(crops[crops.Count % 3]);
            }
            
            return crops.Take(count).ToList();
        }

        /// <summary>
        /// Finds growing zone location prioritizing proximity to base.
        /// ✅ NEW: Prefers locations CLOSER to base center instead of maximum fertility.
        /// </summary>
        private static IntVec3 FindGrowingZoneLocationNearBase(Map map, List<CellRect> existingZones)
        {
            try
            {
                // Get base center (average of all buildings)
                List<Building> buildings = map.listerBuildings.allBuildingsColonist.ToList();
                IntVec3 baseCenter = map.Center;
                
                if (buildings.Count > 0)
                {
                    int avgX = (int)buildings.Average(b => b.Position.x);
                    int avgZ = (int)buildings.Average(b => b.Position.z);
                    baseCenter = new IntVec3(avgX, 0, avgZ);
                }
                
                RimWatchLogger.Debug($"FarmingAutomation: Base center at ({baseCenter.x}, {baseCenter.z})");
                
                // Get existing growing zones to avoid overlap
                List<Zone_Growing> existingGrowingZones = map.zoneManager.AllZones
                    .OfType<Zone_Growing>()
                    .ToList();
                
                List<FertileAreaCandidate> candidates = new List<FertileAreaCandidate>();
                
                // ✅ Search in expanding rings from base (10, 20, 30, 40 tiles radius)
                int[] radiuses = { 10, 15, 20, 25, 30, 35, 40 };
                int sampleStep = 5; // Check every 5 cells
                
                foreach (int radius in radiuses)
                {
                    for (int angle = 0; angle < 360; angle += 30) // Every 30 degrees
                    {
                        float radians = angle * (float)Math.PI / 180f;
                        int centerX = baseCenter.x + (int)(radius * Math.Cos(radians));
                        int centerZ = baseCenter.z + (int)(radius * Math.Sin(radians));
                        IntVec3 candidateCenter = new IntVec3(centerX, 0, centerZ);
                        
                        if (!candidateCenter.InBounds(map))
                            continue;
                        
                        // Check 9x9 area around this candidate
                        int checkSize = 9;
                        float totalFertility = 0f;
                        int plantableCells = 0;
                        
                        for (int dx = -checkSize/2; dx <= checkSize/2; dx++)
                        {
                            for (int dz = -checkSize/2; dz <= checkSize/2; dz++)
                            {
                                IntVec3 cell = new IntVec3(candidateCenter.x + dx, 0, candidateCenter.z + dz);
                                if (!cell.InBounds(map))
                                    continue;
                                
                                if (CanPlantAt(map, cell))
                                {
                                    totalFertility += map.fertilityGrid.FertilityAt(cell);
                                    plantableCells++;
                                }
                            }
                        }
                        
                        if (plantableCells < checkSize * checkSize / 2)
                            continue; // Not enough plantable cells
                        
                        float avgFertility = plantableCells > 0 ? totalFertility / plantableCells : 0f;
                        
                        // Require minimum fertility (0.5+)
                        if (avgFertility < 0.5f)
                            continue;
                        
                        // Check if overlaps with existing zones
                        bool overlaps = false;
                        CellRect candidateRect = new CellRect(candidateCenter.x - checkSize/2, candidateCenter.z - checkSize/2, checkSize, checkSize);
                        
                        foreach (Zone_Growing existing in existingGrowingZones)
                        {
                            if (existing.Cells.Any(c => candidateRect.Contains(c)))
                            {
                                overlaps = true;
                                break;
                            }
                        }
                        
                        foreach (CellRect newZone in existingZones)
                        {
                            if (candidateRect.Overlaps(newZone))
                            {
                                overlaps = true;
                                break;
                            }
                        }
                        
                        if (overlaps)
                            continue;
                        
                        candidates.Add(new FertileAreaCandidate
                        {
                            Center = candidateCenter,
                            AvgFertility = avgFertility,
                            PlantableCells = plantableCells
                        });
                    }
                    
                    // If we found good candidates at this radius, use them (closer is better!)
                    if (candidates.Count >= 3)
                        break;
                }
                
                if (candidates.Count == 0)
                {
                    RimWatchLogger.Warning("FarmingAutomation: No suitable locations found near base");
                    return IntVec3.Invalid;
                }
                
                // Sort by fertility (still want good soil)
                candidates = candidates.OrderByDescending(c => c.AvgFertility).ToList();
                
                FertileAreaCandidate best = candidates.First();
                RimWatchLogger.Info($"FarmingAutomation: Found zone location near base at ({best.Center.x}, {best.Center.z}) - fertility {best.AvgFertility:F2}, {best.PlantableCells} plantable cells");
                
                return best.Center;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FarmingAutomation: Error in FindGrowingZoneLocationNearBase", ex);
                return IntVec3.Invalid;
            }
        }

        /// <summary>
        /// Finds a suitable location for a new growing zone.
        /// ✅ SCANS ENTIRE MAP to find THE MOST FERTILE areas!
        /// Prioritizes rich soil (1.4 fertility) > fertile soil (1.0) > regular soil (0.8+).
        /// </summary>
        private static IntVec3 FindGrowingZoneLocation(Map map)
        {
            try
            {
                // Get existing growing zones to avoid overlap
                List<Zone_Growing> existingZones = map.zoneManager.AllZones
                    .OfType<Zone_Growing>()
                    .ToList();

                // ✅ SCAN ENTIRE MAP to find most fertile areas
                RimWatchLogger.Info("FarmingAutomation: Scanning entire map for fertile soil...");
                
                List<FertileAreaCandidate> candidates = new List<FertileAreaCandidate>();
                
                // Sample grid across entire map (every 10 cells for performance)
                int sampleStep = 10;
                for (int x = 10; x < map.Size.x - 10; x += sampleStep)
                {
                    for (int z = 10; z < map.Size.z - 10; z += sampleStep)
                    {
                        IntVec3 center = new IntVec3(x, 0, z);
                        
                        // Check if too close to existing zones
                        bool tooClose = existingZones.Any(zone => 
                            zone.Cells.Any(c => c.DistanceTo(center) < 15));
                        
                        if (tooClose) continue;
                        
                        // Calculate average fertility in 11x11 area
                        float totalFertility = 0f;
                        int fertileCount = 0;
                        int plantableCount = 0;
                        int checkRadius = 6; // 11x11 area
                        
                        for (int dx = -checkRadius; dx <= checkRadius; dx++)
                        {
                            for (int dz = -checkRadius; dz <= checkRadius; dz++)
                            {
                                IntVec3 cell = center + new IntVec3(dx, 0, dz);
                                if (!cell.InBounds(map)) continue;
                                
                                TerrainDef terrain = cell.GetTerrain(map);
                                if (terrain != null && terrain.fertility > 0f)
                                {
                                    totalFertility += terrain.fertility;
                                    fertileCount++;
                                    
                                    if (CanPlantAt(map, cell))
                                    {
                                        plantableCount++;
                                    }
                                }
                            }
                        }
                        
                        // Require at least 50 plantable cells
                        if (plantableCount < 50) continue;
                        
                        float avgFertility = fertileCount > 0 ? (totalFertility / fertileCount) : 0f;
                        
                        // Only consider areas with decent fertility (0.6+)
                        if (avgFertility >= 0.6f)
                        {
                            candidates.Add(new FertileAreaCandidate
                            {
                                Center = center,
                                AvgFertility = avgFertility,
                                PlantableCells = plantableCount
                            });
                        }
                    }
                }
                
                if (candidates.Count == 0)
                {
                    RimWatchLogger.Warning("FarmingAutomation: No suitable fertile areas found on map!");
                    return IntVec3.Invalid;
                }
                
                // ✅ Sort by fertility (HIGHEST first!)
                candidates = candidates.OrderByDescending(c => c.AvgFertility)
                                      .ThenByDescending(c => c.PlantableCells)
                                      .ToList();
                
                IntVec3 bestLocation = candidates.First().Center;
                
                RimWatchLogger.Info($"🌱 FarmingAutomation: Found {candidates.Count} fertile areas");
                RimWatchLogger.Info($"   Best: ({bestLocation.x}, {bestLocation.z}) - Fertility: {candidates.First().AvgFertility:F2}, Plantable: {candidates.First().PlantableCells} cells");
                
                if (candidates.Count >= 3)
                {
                    RimWatchLogger.Debug($"   Top 3 fertility scores: {candidates[0].AvgFertility:F2}, {candidates[1].AvgFertility:F2}, {candidates[2].AvgFertility:F2}");
                }
                
                return bestLocation;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FarmingAutomation: Error in FindGrowingZoneLocation", ex);
                return IntVec3.Invalid;
            }
        }
        
        /// <summary>
        /// Helper struct for fertile area scoring.
        /// </summary>
        private struct FertileAreaCandidate
        {
            public IntVec3 Center;
            public float AvgFertility;
            public int PlantableCells;
        }

        /// <summary>
        /// Checks if a location is suitable for a growing zone.
        /// ✅ IMPROVED: Now prefers RICH SOIL (fertility >= 1.0) over normal soil
        /// </summary>
        private static bool IsGoodGrowingLocation(Map map, IntVec3 center, List<Zone_Growing> existingZones)
        {
            // Check if too close to existing zones
            foreach (Zone_Growing zone in existingZones)
            {
                if (zone.Cells.Any(c => c.DistanceTo(center) < 15))
                {
                    return false; // Too close to existing zone
                }
            }

            // ✅ NEW: Check average fertility FIRST - must be >= 0.8 (prefer rich soil areas)
            float totalFertility = 0f;
            int fertileCount = 0;
            int checkRadius = 6;

            for (int x = center.x - checkRadius; x <= center.x + checkRadius; x++)
            {
                for (int z = center.z - checkRadius; z <= center.z + checkRadius; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map)) continue;
                    
                    TerrainDef terrain = cell.GetTerrain(map);
                    if (terrain != null && terrain.fertility > 0f)
                    {
                        totalFertility += terrain.fertility;
                        fertileCount++;
                    }
                }
            }

            // ✅ Require average fertility >= 0.8 (prefer rich soil zones)
            float avgFertility = fertileCount > 0 ? (totalFertility / fertileCount) : 0f;
            if (avgFertility < 0.8f)
            {
                RimWatchLogger.Debug($"FarmingAutomation: Rejecting location ({center.x}, {center.z}) - low avg fertility: {avgFertility:F2}");
                return false; // Too low fertility for farming zone
            }

            // Check if area is mostly outdoor and plantable
            int plantableCells = 0;
            int totalCells = 0;

            for (int x = center.x - checkRadius; x <= center.x + checkRadius; x++)
            {
                for (int z = center.z - checkRadius; z <= center.z + checkRadius; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map)) continue;
                    
                    totalCells++;
                    if (CanPlantAt(map, cell))
                    {
                        plantableCells++;
                    }
                }
            }

            // At least 70% of area should be plantable
            bool enoughPlantable = totalCells > 0 && ((float)plantableCells / totalCells) >= 0.7f;
            
            if (enoughPlantable)
            {
                RimWatchLogger.Debug($"FarmingAutomation: ✓ Found good location ({center.x}, {center.z}) - fertility: {avgFertility:F2}, plantable: {plantableCells}/{totalCells}");
            }
            
            return enoughPlantable;
        }

        /// <summary>
        /// Checks if a cell can be used for planting.
        /// Now with improved checks for terrain, fog, and home area.
        /// </summary>
        private static bool CanPlantAt(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map)) return false;
            
            // ✅ NEW: Check fog of war
            if (map.fogGrid.IsFogged(cell))
            {
                return false; // Don't plant in fog
            }
            
            if (cell.Roofed(map)) return false; // No roof (plants need sunlight)
            
            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain == null) return false;
            
            // ✅ IMPROVED: Better terrain checking
            // Reject water/lava
            if (terrain.IsWater || terrain.defName.Contains("Lava"))
            {
                return false;
            }
            
            // ✅ VERY STRICT: Require at least 0.9 fertility (good soil only!)
            // RimWorld fertility values: Gravel=0.05, Soil=1.0, Rich Soil=1.4, Stony=0.55
            // Only accept Soil (1.0) or better
            if (terrain.fertility < 0.9f)
            {
                // Too low fertility - reject
                if (terrain.fertility > 0f) // Only log if terrain is somewhat fertile
                {
                    RimWatchLogger.Debug($"FarmingAutomation: Rejecting mediocre fertility at ({cell.x}, {cell.z}): {terrain.fertility:F2} < 0.9 (need good soil!)");
                }
                return false;
            }
            
            // Log excellent fertility
            if (terrain.fertility >= 1.3f)
            {
                RimWatchLogger.Debug($"FarmingAutomation: ✓✓ RICH SOIL at ({cell.x}, {cell.z}): {terrain.fertility:F2}");
            }
            else if (terrain.fertility >= 1.0f)
            {
                RimWatchLogger.Debug($"FarmingAutomation: ✓ Good soil at ({cell.x}, {cell.z}): {terrain.fertility:F2}");
            }
            
            // Check if cell is blocked by buildings or other objects
            if (cell.GetFirstBuilding(map) != null) return false;
            if (cell.GetThingList(map).Any(t => t.def.passability == Traversability.Impassable)) return false;
            
            return true;
        }

        /// <summary>
        /// Gets soil fertility rating for a location.
        /// </summary>
        private static float GetSoilFertility(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map)) return 0f;
            
            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain == null) return 0f;
            
            return terrain.fertility;
        }

        /// <summary>
        /// Automatically selects and assigns appropriate crops to growing zones based on season and food needs.
        /// Priority: Rice (fast) > Corn (high yield) > Potatoes (cold-resistant)
        /// </summary>
        private static void AutoSowCrops(Map map, FarmingNeeds needs)
        {
            try
            {
                // v0.8.3: Log execution start
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Get all growing zones
                List<Zone_Growing> growingZones = map.zoneManager.AllZones
                    .OfType<Zone_Growing>()
                    .ToList();

                if (growingZones.Count == 0)
                {
                    stopwatch.Stop();
                    RimWatchLogger.LogExecutionEnd("FarmingAutomation", "AutoSowCrops", true, stopwatch.ElapsedMilliseconds,
                        "No growing zones found");
                    return; // No growing zones to configure
                }

                // Determine best crop for current season
                ThingDef bestCrop = ChooseBestCrop(map, needs);
                
                if (bestCrop == null)
                {
                    stopwatch.Stop();
                    RimWatchLogger.LogFailure("FarmingAutomation", "AutoSowCrops", "Could not determine suitable crop", 
                        new Dictionary<string, object>
                        {
                            { "season", needs.CurrentSeason },
                            { "lowFood", needs.MealCount + needs.RawFoodCount < map.mapPawns.FreeColonistsSpawnedCount * 5 }
                        });
                    RimWatchLogger.Warning("FarmingAutomation: Could not determine suitable crop for season");
                    return;
                }

                int zonesUpdated = 0;
                List<string> updates = new List<string>();

                foreach (Zone_Growing zone in growingZones)
                {
                    // Check if zone needs crop assignment or optimization
                    ThingDef currentPlant = zone.GetPlantDefToGrow();
                    
                    // Only update if:
                    // 1. Zone has no plant assigned
                    // 2. Current plant is not optimal for season
                    if (currentPlant == null || !IsCropSuitableForSeason(currentPlant, needs.CurrentSeason, map))
                    {
                        // v0.8.3: Log crop change decision
                        RimWatchLogger.LogDecision("FarmingAutomation", "ChangeCrop", new Dictionary<string, object>
                        {
                            { "oldCrop", currentPlant?.defName ?? "none" },
                            { "newCrop", bestCrop.defName },
                            { "season", needs.CurrentSeason },
                            { "zoneCells", zone.cells.Count },
                            { "reason", currentPlant == null ? "NoPlant" : "NotSuitableForSeason" }
                        });
                        
                        zone.SetPlantDefToGrow(bestCrop);
                        zonesUpdated++;
                        
                        string oldCrop = currentPlant?.LabelCap ?? "none";
                        updates.Add($"{oldCrop} → {bestCrop.LabelCap}");
                    }
                }

                if (zonesUpdated > 0)
                {
                    Season season = needs.CurrentSeason;
                    
                    // v0.8.3: Log execution end with results
                    stopwatch.Stop();
                    RimWatchLogger.LogExecutionEnd("FarmingAutomation", "AutoSowCrops", true, stopwatch.ElapsedMilliseconds,
                        $"Updated {zonesUpdated}/{growingZones.Count} zones to {bestCrop.LabelCap}");
                    
                    RimWatchLogger.Info($"🌾 FarmingAutomation: Updated {zonesUpdated} growing zone(s) for {season} season:");
                    foreach (string update in updates)
                    {
                        RimWatchLogger.Info($"   • {update}");
                    }
                }
                else
                {
                    // v0.8.3: Log execution end (no changes)
                    stopwatch.Stop();
                    RimWatchLogger.LogExecutionEnd("FarmingAutomation", "AutoSowCrops", true, stopwatch.ElapsedMilliseconds,
                        $"All {growingZones.Count} zones already optimal");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FarmingAutomation: Error in AutoSowCrops", ex);
            }
        }

        /// <summary>
        /// Chooses the best crop for current season and food needs.
        /// </summary>
        private static ThingDef ChooseBestCrop(Map map, FarmingNeeds needs)
        {
            Season season = needs.CurrentSeason;
            int totalFood = needs.MealCount + needs.RawFoodCount;
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            bool lowFood = totalFood < colonistCount * 5; // Less than 5 meals per colonist
            
            // Get available crops
            ThingDef rice = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Rice");
            ThingDef corn = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Corn");
            ThingDef potatoes = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Potatoes");
            
            // Fallback to any plant_food if standard crops not available
            if (rice == null && corn == null && potatoes == null)
            {
                return DefDatabase<ThingDef>.AllDefs
                    .FirstOrDefault(d => d.plant != null && 
                                        d.plant.sowTags != null && 
                                        d.plant.sowTags.Contains("Ground"));
            }

            // Decision logic based on season and needs
            switch (season)
            {
                case Season.Spring:
                case Season.Summer:
                    // Warm seasons: prefer fast crops if low food, otherwise high yield
                    if (lowFood && rice != null)
                        return rice; // Rice: 6 days, good for emergencies
                    else if (corn != null)
                        return corn; // Corn: 15 days, best yield
                    else if (rice != null)
                        return rice;
                    else if (potatoes != null)
                        return potatoes;
                    break;

                case Season.Fall:
                    // Fall: Balance between yield and growing time before winter
                    if (potatoes != null)
                        return potatoes; // Potatoes: 8 days, cold-resistant
                    else if (rice != null)
                        return rice;
                    else if (corn != null)
                        return corn;
                    break;

                case Season.Winter:
                    // Winter: Only cold-hardy crops in temperate, or nothing in extreme cold
                    // Check if temperature allows growing
                    float temp = map.mapTemperature.OutdoorTemp;
                    if (temp < 0f)
                    {
                        // Too cold for most crops - return nothing or fallback
                        RimWatchLogger.Debug($"FarmingAutomation: Winter temp {temp}°C too cold for growing");
                        // Still return something in case there's indoor/greenhouse growing
                        if (rice != null) return rice;
                    }
                    
                    if (potatoes != null)
                        return potatoes; // Most cold-resistant
                    else if (rice != null)
                        return rice;
                    break;

                default:
                    // Undefined season: default to rice
                    if (rice != null)
                        return rice;
                    else if (corn != null)
                        return corn;
                    else if (potatoes != null)
                        return potatoes;
                    break;
            }

            // Ultimate fallback
            return rice ?? corn ?? potatoes;
        }

        /// <summary>
        /// Checks if a crop is suitable for the current season.
        /// </summary>
        private static bool IsCropSuitableForSeason(ThingDef plant, Season season, Map map)
        {
            if (plant == null) return false;
            
            string plantName = plant.defName.ToLower();
            
            switch (season)
            {
                case Season.Spring:
                case Season.Summer:
                    // All crops are suitable
                    return true;

                case Season.Fall:
                    // Avoid very slow crops (corn) in fall
                    return !plantName.Contains("corn");

                case Season.Winter:
                    // Only cold-hardy crops or if temp allows
                    float temp = map.mapTemperature.OutdoorTemp;
                    if (temp >= 0f)
                    {
                        // Mild winter, potatoes and rice OK
                        return plantName.Contains("potato") || plantName.Contains("rice");
                    }
                    else
                    {
                        // Severe winter, very limited options
                        return plantName.Contains("potato"); // Most cold-resistant
                    }

                default:
                    return true;
            }
        }

        /// <summary>
        /// v0.8.0: Advanced animal evaluation system.
        /// Evaluates animals based on milk/wool/eggs/pack/combat value with penalties for wildness/age.
        /// </summary>
        private static float GetAnimalUtility(Pawn animal)
        {
            float score = 0f;

            try
            {
                // 1. Milk production (HIGH VALUE - consistent food/trade good)
                CompMilkable milkComp = animal.GetComp<CompMilkable>();
                if (milkComp != null)
                {
                    score += 30f;
                }

                // 2. Wool/leather production (HIGH VALUE - materials for clothing)
                CompShearable shearComp = animal.GetComp<CompShearable>();
                if (shearComp != null)
                {
                    score += 25f;
                }

                // 3. Eggs (VERY HIGH VALUE - consistent food without killing)
                CompEggLayer eggComp = animal.GetComp<CompEggLayer>();
                if (eggComp != null)
                {
                    score += 20f;
                }

                // 4. Pack capacity (CRITICAL - enables caravans)
            if (animal.RaceProps.packAnimal)
            {
                    score += 35f;
                }

                // 5. Combat power (for defense/hunting)
                if (animal.RaceProps.baseHealthScale > 1.5f && animal.RaceProps.predator)
                {
                    score += 20f; // War animals
            }

                // 6. Size bonus (bigger = more meat/leather/products)
                score += animal.RaceProps.baseBodySize * 5f;

                // === PENALTIES ===

                // 7. Wildness penalty (hard to tame/train) - v0.8.0: Removed due to API incompatibility
                // Note: Wildness checking disabled for RimWorld 1.6 compatibility
                // This was causing compilation errors as the API changed between versions

                // 8. Age penalty (old animals die soon) - Simplified version
                if (animal.ageTracker != null)
                {
                    float currentAge = animal.ageTracker.AgeBiologicalYearsFloat;
                    if (currentAge > 10f) // Rough estimate - old = bad
                    {
                        score -= 20f;
                    }
                }

                // 9. Already bonded penalty (avoid stealing bonded animals from other factions)
                if (animal.relations != null && animal.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond) != null)
                {
                    score -= 100f; // Don't tame bonded animals
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Warning($"FarmingAutomation: Error evaluating animal {animal?.LabelShort}: {ex.Message}");
                return 0f;
            }

            return score;
        }

        // ==================== v0.7 ADVANCED FEATURES ====================

        private static int lastAnimalManagementTick = -9999;
        private const int AnimalManagementCooldown = 3600; // 60 seconds
        
        private static int lastHayCreationTick = -9999;
        private const int HayCreationCooldown = 36000; // 10 minutes (hay is seasonal)

        /// <summary>
        /// Advanced animal management - breeding, training, feeding.
        /// NEW in v0.7 - Full automation of animal husbandry.
        /// </summary>
        private static void AutoManageAnimals(Map map)
        {
            try
            {
                // Cooldown check
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - lastAnimalManagementTick < AnimalManagementCooldown)
                {
                    return; // Too soon
                }

                AutoManageBreeding(map);
                AutoTrainAnimals(map);
                AutoManageAnimalFood(map);

                lastAnimalManagementTick = currentTick;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FarmingAutomation: Error in AutoManageAnimals", ex);
            }
        }

        /// <summary>
        /// Automatically manages animal breeding zones and assignments.
        /// </summary>
        private static void AutoManageBreeding(Map map)
        {
            try
            {
                // v0.8.3: Log execution start
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Get all tamed animals
                List<Pawn> tamedAnimals = map.mapPawns.PawnsInFaction(Faction.OfPlayer)
                    .Where(p => p.RaceProps.Animal && !p.Dead && !p.Downed)
                    .ToList();

                if (tamedAnimals.Count < 2)
                {
                    stopwatch.Stop();
                    RimWatchLogger.LogExecutionEnd("FarmingAutomation", "AutoManageBreeding", true, stopwatch.ElapsedMilliseconds,
                        $"Not enough animals ({tamedAnimals.Count}/2 min)");
                    return; // Need at least 2 animals to breed
                }

                // Group by species
                var speciesGroups = tamedAnimals.GroupBy(p => p.def).ToList();
                
                int totalSpeciesManaged = 0;
                int totalBreedingEnabled = 0;

                foreach (var group in speciesGroups)
                {
                    List<Pawn> species = group.ToList();
                    if (species.Count < 2) continue;

                    // Check if species can breed
                    if (!species[0].RaceProps.hasGenders) continue;

                    // Count males and females
                    int males = species.Count(p => p.gender == Gender.Male);
                    int females = species.Count(p => p.gender == Gender.Female);

                    if (males == 0 || females == 0)
                    {
                        RimWatchLogger.LogDecision("FarmingAutomation", "SkipBreeding", new Dictionary<string, object>
                        {
                            { "species", species[0].def.defName },
                            { "reason", "MissingGender" },
                            { "males", males },
                            { "females", females },
                            { "total", species.Count }
                        });
                        RimWatchLogger.Debug($"FarmingAutomation: {species[0].def.LabelCap} - need both genders for breeding (M:{males}, F:{females})");
                        continue;
            }

                    // Enable breeding for animals of breeding age
                    int breedingEnabled = 0;
                    foreach (Pawn animal in species)
                    {
                        // Check if animal is adult
                        if (!animal.ageTracker.Adult) continue;

                        // Check current breeding setting
                        bool currentlySterilized = animal.health.hediffSet.HasHediff(HediffDefOf.Sterilized);
                        
                        if (!currentlySterilized)
                        {
                            breedingEnabled++;
                        }
                    }

                    if (breedingEnabled > 0)
                    {
                        totalSpeciesManaged++;
                        totalBreedingEnabled += breedingEnabled;
                        
                        // v0.8.3: Log breeding decision
                        RimWatchLogger.LogDecision("FarmingAutomation", "EnableBreeding", new Dictionary<string, object>
                        {
                            { "species", species[0].def.defName },
                            { "breedingAdults", breedingEnabled },
                            { "males", males },
                            { "females", females },
                            { "totalAnimals", species.Count }
                        });
                        
                        RimWatchLogger.Debug($"🐾 FarmingAutomation: {species[0].def.LabelCap} breeding enabled ({breedingEnabled} adults, M:{males}/F:{females})");
                    }
                }
                
                // v0.8.3: Log execution end with summary
                stopwatch.Stop();
                RimWatchLogger.LogExecutionEnd("FarmingAutomation", "AutoManageBreeding", true, stopwatch.ElapsedMilliseconds,
                    $"Managed {totalSpeciesManaged} species, {totalBreedingEnabled} breeding animals");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FarmingAutomation: Error in AutoManageBreeding", ex);
            }
        }

        /// <summary>
        /// Automatically trains animals based on their utility and colonist availability.
        /// </summary>
        private static void AutoTrainAnimals(Map map)
        {
            try
            {
                // Find trainers (colonists with Animals skill)
                List<Pawn> trainers = map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.Dead && !p.Downed && 
                               p.workSettings != null &&
                               p.workSettings.WorkIsActive(WorkTypeDefOf.Handling) &&
                               p.skills.GetSkill(SkillDefOf.Animals).Level >= 3)
                    .ToList();

                if (trainers.Count == 0)
                {
                    return; // No one can train animals
                }

                // Get all tamed animals
                List<Pawn> tamedAnimals = map.mapPawns.PawnsInFaction(Faction.OfPlayer)
                    .Where(p => p.RaceProps.Animal && !p.Dead && !p.Downed && p.training != null)
                    .ToList();

                if (tamedAnimals.Count == 0) return;

                int animalsScheduledForTraining = 0;

                foreach (Pawn animal in tamedAnimals)
                {
                    if (animal.training == null) continue;

                    // Prioritize Obedience and Release training (most useful)
                    TrainableDef[] priorityTraining = new[]
                    {
                        TrainableDefOf.Obedience,
                        TrainableDefOf.Release
                    };

                    foreach (TrainableDef trainable in priorityTraining)
                    {
                        if (animal.training.CanBeTrained(trainable) && 
                            !animal.training.HasLearned(trainable))
                        {
                            // Check if training is enabled
                            bool wantsTraining = animal.training.GetWanted(trainable);
                            
                            if (!wantsTraining)
                            {
                                // Enable training
                                animal.training.SetWantedRecursive(trainable, true);
                                animalsScheduledForTraining++;
                                
                                RimWatchLogger.Debug($"🎓 FarmingAutomation: Enabled {trainable.LabelCap} training for {animal.LabelShort}");
                            }
                        }
                    }

                    // Limit to prevent spam
                    if (animalsScheduledForTraining >= 5) break;
                }

                if (animalsScheduledForTraining > 0)
                {
                    RimWatchLogger.Info($"🐾 FarmingAutomation: Scheduled {animalsScheduledForTraining} animal(s) for training");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FarmingAutomation: Error in AutoTrainAnimals", ex);
            }
        }

        /// <summary>
        /// Automatically manages animal feeding - creates animal zones and hay storage.
        /// </summary>
        private static void AutoManageAnimalFood(Map map)
        {
            try
            {
                // Count tamed animals
                int tamedCount = map.mapPawns.PawnsInFaction(Faction.OfPlayer)
                    .Count(p => p.RaceProps.Animal && !p.Dead && !p.Downed);

                if (tamedCount == 0) return;

                // Check if we have hay (for winter feeding)
                int hayCount = map.listerThings.ThingsOfDef(ThingDefOf.Hay)?.Sum(h => h.stackCount) ?? 0;

                if (hayCount < tamedCount * 5) // Need 5 hay per animal minimum
                {
                    RimWatchLogger.Info($"🌾 FarmingAutomation: Low hay reserves ({hayCount}) for {tamedCount} animals - need to plant hay!");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FarmingAutomation: Error in AutoManageAnimalFood", ex);
            }
        }

        /// <summary>
        /// Automatically creates hay growing zones before winter in cold climates.
        /// NEW in v0.7 - Hay preparation for winter animal feeding.
        /// </summary>
        private static void AutoPrepareHayForWinter(Map map, FarmingNeeds needs)
        {
            try
            {
                // Only relevant in cold biomes (check if winter temperature drops below freezing)
                float winterTemp = map.mapTemperature.OutdoorTemp; // Simplified check
                if (winterTemp > 0f && needs.CurrentSeason != Season.Winter)
                {
                    // Warm biome/season, no need for hay preparation yet
                    return;
                }

                // Cooldown check
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - lastHayCreationTick < HayCreationCooldown)
                {
                    return; // Too soon
            }

                // Check if we're approaching winter (late fall)
                Season currentSeason = needs.CurrentSeason;
                if (currentSeason != Season.Fall && currentSeason != Season.Summer)
                {
                    return; // Not the right time
                }

                // Count tamed animals that need feeding
                int tamedCount = map.mapPawns.PawnsInFaction(Faction.OfPlayer)
                    .Count(p => p.RaceProps.Animal && !p.Dead && !p.Downed);

                if (tamedCount == 0)
                {
                    return; // No animals to feed
                }

                // Check existing hay reserves
                int hayCount = map.listerThings.ThingsOfDef(ThingDefOf.Hay)?.Sum(h => h.stackCount) ?? 0;
                int hayNeeded = tamedCount * 30; // 30 hay per animal for winter

                if (hayCount >= hayNeeded)
                {
                    RimWatchLogger.Debug($"FarmingAutomation: Sufficient hay reserves ({hayCount}/{hayNeeded}) for winter ✓");
                    return; // Already have enough
                }

                // Check existing hay zones
                ThingDef hayPlantDef = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Hay");
                if (hayPlantDef == null)
                {
                    RimWatchLogger.Debug("FarmingAutomation: Hay plant not found in game defs");
                    return; // Hay not available (vanilla RimWorld doesn't have it without mods)
                }
                
                List<Zone_Growing> hayZones = map.zoneManager.AllZones
                    .OfType<Zone_Growing>()
                    .Where(z => z.GetPlantDefToGrow() == hayPlantDef)
                    .ToList();

                int hayZoneSize = hayZones.Sum(z => z.cells.Count);
                int hayZoneNeeded = tamedCount * 20; // 20 cells per animal

                if (hayZoneSize >= hayZoneNeeded)
                {
                    RimWatchLogger.Debug($"FarmingAutomation: Sufficient hay growing zones ({hayZoneSize}/{hayZoneNeeded} cells) ✓");
                    return; // Already have enough hay zones
                }

                // CREATE HAY ZONE!
                RimWatchLogger.Info($"🌾 FarmingAutomation: Creating hay growing zone for {tamedCount} animals (need {hayNeeded} hay for winter)");

                IntVec3 baseCenter = BaseZoneCache.BaseCenter != IntVec3.Invalid 
                    ? BaseZoneCache.BaseCenter 
                    : map.Center;

                // Find suitable location (similar to crop zone creation)
                List<IntVec3> zoneCells = new List<IntVec3>();
                int targetSize = Math.Min(100, hayZoneNeeded); // Max 100 cells per zone

                for (int radius = 15; radius < 50 && zoneCells.Count < targetSize; radius += 5)
                {
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(baseCenter, radius, true))
                    {
                        if (!cell.InBounds(map)) continue;
                        if (map.fogGrid.IsFogged(cell)) continue;
                        if (!CanPlantAt(map, cell)) continue;

                        zoneCells.Add(cell);

                        if (zoneCells.Count >= targetSize) break;
                    }
                }

                if (zoneCells.Count >= 20) // Minimum 20 cells for a zone
                {
                    // Create the zone
                    Zone_Growing hayZone = new Zone_Growing(map.zoneManager);
                    hayZone.SetPlantDefToGrow(hayPlantDef);
                    
                    foreach (IntVec3 cell in zoneCells)
                    {
                        hayZone.AddCell(cell);
                    }

                    map.zoneManager.RegisterZone(hayZone);

                    RimWatchLogger.Info($"✅ FarmingAutomation: Created hay growing zone ({zoneCells.Count} cells) at distance ~{(int)baseCenter.DistanceTo(zoneCells[0])} from base");
                    
                    lastHayCreationTick = currentTick;
                }
                else
                {
                    RimWatchLogger.Warning($"FarmingAutomation: Could not find suitable location for hay zone (found only {zoneCells.Count} cells)");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("FarmingAutomation: Error in AutoPrepareHayForWinter", ex);
            }
        }
    }
}
