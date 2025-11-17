using RimWatch.Automation.ColonyDevelopment;
using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation
{
    /// <summary>
    /// v0.8.4: Manages production bills and manufacturing based on colony development stage.
    /// Automatically creates and manages bills for essential items.
    /// </summary>
    public static class ProductionAutomation
    {
        private static int _tickCounter = 0;
        private static bool _isEnabled = false;
        private const int UpdateInterval = 3600; // Check every minute (3600 ticks)
        
        /// <summary>
        /// Enabled state for production automation.
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                RimWatchLogger.Info($"ProductionAutomation: {(value ? "Enabled" : "Disabled")}");
            }
        }
        
        /// <summary>
        /// Main tick method.
        /// v0.8.5: Added settings check and improved logging.
        /// </summary>
        public static void Tick()
        {
            if (!IsEnabled) return;
            if (!Core.RimWatchCore.AutopilotEnabled) return;
            
            // v0.8.5: Check settings
            if (RimWatchMod.Settings != null && !RimWatchMod.Settings.productionAutomationEnabled)
            {
                return;
            }
            
            _tickCounter++;
            if (_tickCounter >= UpdateInterval)
            {
                _tickCounter = 0;
                ManageProduction();
            }
        }
        
        /// <summary>
        /// Manages production bills based on current stage.
        /// v0.8.5: Enhanced with decision logging.
        /// </summary>
        private static void ManageProduction()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            
            DevelopmentStage stage = DevelopmentStageManager.GetCurrentStage(map);
            
            RimWatchLogger.LogDecision("ProductionAutomation", "ManageProduction", new Dictionary<string, object>
            {
                { "stage", stage.ToString() },
                { "colonists", map.mapPawns.FreeColonistsSpawnedCount },
                { "tick", Find.TickManager.TicksGame }
            });
            
            RimWatchLogger.Info($"[ProductionAutomation] Managing production for {stage} stage");
            
            // Create bills based on stage
            AutoCreateBills(map, stage);
            
            // v0.9.0: NEW - Enhanced production systems
            CreateSurvivalBills(map);
            CreateMedicineBills(map);
            CreateFoodVarietyBills(map, stage);
            ManageBillResources(map);
        }
        
        /// <summary>
        /// Automatically creates production bills for necessary items.
        /// </summary>
        public static void AutoCreateBills(Map map, DevelopmentStage stage)
        {
            switch (stage)
            {
                case DevelopmentStage.Emergency:
                    CreateEmergencyBills(map);
                    break;
                    
                case DevelopmentStage.EarlyGame:
                    CreateEarlyGameBills(map);
                    break;
                    
                case DevelopmentStage.MidGame:
                    CreateMidGameBills(map);
                    break;
                    
                case DevelopmentStage.LateGame:
                    CreateLateGameBills(map);
                    break;
                    
                case DevelopmentStage.EndGame:
                    CreateEndGameBills(map);
                    break;
            }
        }
        
        /// <summary>
        /// Emergency: Simple meals only.
        /// </summary>
        private static void CreateEmergencyBills(Map map)
        {
            // Simple meals at campfire
            CreateMealBill(map, "CookMealSimple", 10);
        }
        
        /// <summary>
        /// Early Game: Basic production (clothes, weapons, stone blocks).
        /// </summary>
        private static void CreateEarlyGameBills(Map map)
        {
            // Meals
            CreateMealBill(map, "CookMealSimple", 20);
            
            // Clothes - basic shirts and pants
            CreateApparelBill(map, "MakeTribalwear", 5);
            
            // Stone blocks for construction
            CreateStoneBill(map, "MakeStoneBlocks", 50);
        }
        
        /// <summary>
        /// Mid Game: Medicine, components, fine meals.
        /// </summary>
        private static void CreateMidGameBills(Map map)
        {
            // Fine meals
            CreateMealBill(map, "CookMealFine", 15);
            
            // Medicine production
            CreateDrugBill(map, "Make_MedicineIndustrial", 10);
            
            // Penoxycline (preventive medicine)
            CreateDrugBill(map, "Make_Penoxycline", 5);
            
            // Component production (if researched)
            if (ResearchProjectDef.Named("Fabrication")?.IsFinished == true)
            {
                CreateComponentBill(map, "Make_ComponentIndustrial", 20);
            }
        }
        
        /// <summary>
        /// Late Game: Advanced components, bionics, sculptures.
        /// </summary>
        private static void CreateLateGameBills(Map map)
        {
            // Lavish meals
            CreateMealBill(map, "CookMealLavish", 10);
            
            // Advanced components
            CreateComponentBill(map, "Make_ComponentSpacer", 10);
            
            // Sculptures for beauty
            CreateArtBill(map, "MakeSculpture", 3);
        }
        
        /// <summary>
        /// End Game: Maximum production for ship/wealth.
        /// </summary>
        private static void CreateEndGameBills(Map map)
        {
            // All high-tier production
            CreateLateGameBills(map);
        }
        
        // ===== BILL CREATION HELPERS =====
        
        private static void CreateMealBill(Map map, string recipeDefName, int count)
        {
            var cookStations = map.listerBuildings.allBuildingsColonist
                .Where(b => b is Building_WorkTable && b.def.building?.isMealSource == true)
                .Cast<Building_WorkTable>()
                .ToList();
            
            if (cookStations.Count == 0) return;
            
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
            if (recipe == null) return;
            
            foreach (var station in cookStations)
            {
                // Check if bill already exists
                if (station.BillStack.Bills.Any(b => b.recipe == recipe))
                    continue;
                
                Bill_Production bill = (Bill_Production)recipe.MakeNewBill();
                bill.repeatMode = BillRepeatModeDefOf.TargetCount;
                bill.targetCount = count;
                station.BillStack.AddBill(bill);
                
                RimWatchLogger.Info($"ProductionAutomation: Created {recipeDefName} bill (x{count})");
            }
        }
        
        private static void CreateApparelBill(Map map, string recipeDefName, int count)
        {
            var tailoringBenches = map.listerBuildings.allBuildingsColonist
                .Where(b => b is Building_WorkTable && b.def.defName.Contains("Tailoring"))
                .Cast<Building_WorkTable>()
                .ToList();
            
            if (tailoringBenches.Count == 0) return;
            
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
            if (recipe == null) return;
            
            foreach (var bench in tailoringBenches)
            {
                if (bench.BillStack.Bills.Any(b => b.recipe == recipe))
                    continue;
                
                Bill_Production bill = (Bill_Production)recipe.MakeNewBill();
                bill.repeatMode = BillRepeatModeDefOf.TargetCount;
                bill.targetCount = count;
                bench.BillStack.AddBill(bill);
                
                RimWatchLogger.Info($"ProductionAutomation: Created {recipeDefName} bill (x{count})");
            }
        }
        
        private static void CreateStoneBill(Map map, string recipeDefName, int count)
        {
            var stonecutters = map.listerBuildings.allBuildingsColonist
                .Where(b => b is Building_WorkTable && b.def.defName.Contains("Stonecutter"))
                .Cast<Building_WorkTable>()
                .ToList();
            
            if (stonecutters.Count == 0) return;
            
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
            if (recipe == null) return;
            
            foreach (var cutter in stonecutters)
            {
                if (cutter.BillStack.Bills.Any(b => b.recipe == recipe))
                    continue;
                
                Bill_Production bill = (Bill_Production)recipe.MakeNewBill();
                bill.repeatMode = BillRepeatModeDefOf.TargetCount;
                bill.targetCount = count;
                cutter.BillStack.AddBill(bill);
                
                RimWatchLogger.Info($"ProductionAutomation: Created {recipeDefName} bill (x{count})");
            }
        }
        
        private static void CreateDrugBill(Map map, string recipeDefName, int count)
        {
            var drugLabs = map.listerBuildings.allBuildingsColonist
                .Where(b => b is Building_WorkTable && b.def.defName.Contains("DrugLab"))
                .Cast<Building_WorkTable>()
                .ToList();
            
            if (drugLabs.Count == 0) return;
            
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
            if (recipe == null) return;
            
            foreach (var lab in drugLabs)
            {
                if (lab.BillStack.Bills.Any(b => b.recipe == recipe))
                    continue;
                
                Bill_Production bill = (Bill_Production)recipe.MakeNewBill();
                bill.repeatMode = BillRepeatModeDefOf.TargetCount;
                bill.targetCount = count;
                lab.BillStack.AddBill(bill);
                
                RimWatchLogger.Info($"ProductionAutomation: Created {recipeDefName} bill (x{count})");
            }
        }
        
        private static void CreateComponentBill(Map map, string recipeDefName, int count)
        {
            var fabricationBenches = map.listerBuildings.allBuildingsColonist
                .Where(b => b is Building_WorkTable && b.def.defName.Contains("Fabrication"))
                .Cast<Building_WorkTable>()
                .ToList();
            
            if (fabricationBenches.Count == 0) return;
            
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
            if (recipe == null) return;
            
            foreach (var bench in fabricationBenches)
            {
                if (bench.BillStack.Bills.Any(b => b.recipe == recipe))
                    continue;
                
                Bill_Production bill = (Bill_Production)recipe.MakeNewBill();
                bill.repeatMode = BillRepeatModeDefOf.TargetCount;
                bill.targetCount = count;
                bench.BillStack.AddBill(bill);
                
                RimWatchLogger.Info($"ProductionAutomation: Created {recipeDefName} bill (x{count})");
            }
        }
        
        private static void CreateArtBill(Map map, string recipeDefName, int count)
        {
            var artBenches = map.listerBuildings.allBuildingsColonist
                .Where(b => b is Building_WorkTable && b.def.defName.Contains("ArtBench"))
                .Cast<Building_WorkTable>()
                .ToList();
            
            if (artBenches.Count == 0) return;
            
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
            if (recipe == null) return;
            
            foreach (var bench in artBenches)
            {
                if (bench.BillStack.Bills.Any(b => b.recipe == recipe))
                    continue;
                
                Bill_Production bill = (Bill_Production)recipe.MakeNewBill();
                bill.repeatMode = BillRepeatModeDefOf.TargetCount;
                bill.targetCount = count;
                bench.BillStack.AddBill(bill);
                
                RimWatchLogger.Info($"ProductionAutomation: Created {recipeDefName} bill (x{count})");
            }
        }
        
        // ===== v0.9.0: ENHANCED PRODUCTION SYSTEMS =====
        
        /// <summary>
        /// Creates survival bills: clothing repair, weapon repair when items are damaged.
        /// </summary>
        private static void CreateSurvivalBills(Map map)
        {
            // Check colonist apparel condition
            var colonists = map.mapPawns.FreeColonistsSpawned;
            bool needsClothing = colonists.Any(p => 
                p.apparel?.WornApparel?.Any(a => a.HitPoints < a.MaxHitPoints * 0.5f) == true);
            
            if (needsClothing)
            {
                RimWatchLogger.LogDecision("ProductionAutomation", "SurvivalClothing", new Dictionary<string, object>
                {
                    { "reason", "Colonists have damaged apparel (<50% HP)" }
                });
                
                // Create basic clothing bills
                CreateApparelBill(map, "Make_Apparel_Parka", 3);
                CreateApparelBill(map, "Make_Apparel_Pants", 3);
                CreateApparelBill(map, "Make_Apparel_ButtonDownShirt", 3);
            }
            
            // Check weapon condition
            bool needsWeapons = colonists.Any(p => 
            {
                var weapon = p.equipment?.Primary;
                return weapon != null && weapon.HitPoints < weapon.MaxHitPoints * 0.6f;
            });
            
            if (needsWeapons)
            {
                RimWatchLogger.LogDecision("ProductionAutomation", "SurvivalWeapons", new Dictionary<string, object>
                {
                    { "reason", "Colonists have damaged weapons (<60% HP)" }
                });
                
                // Create basic weapon bills (simple weapons)
                var smithingBenches = map.listerBuildings.allBuildingsColonist
                    .Where(b => b is Building_WorkTable && b.def.defName.Contains("Smithing"))
                    .Cast<Building_WorkTable>()
                    .ToList();
                
                if (smithingBenches.Count > 0)
                {
                    CreateWeaponBill(map, "Make_MeleeWeapon_Gladius", 2);
                    CreateWeaponBill(map, "Make_Gun_Revolver", 2);
                }
            }
        }
        
        /// <summary>
        /// Creates medicine bills: herbal medicine if no industrial medicine available.
        /// </summary>
        private static void CreateMedicineBills(Map map)
        {
            // Count available medicine
            int industrialMedicine = map.resourceCounter.GetCount(ThingDefOf.MedicineIndustrial);
            int herbalMedicine = map.resourceCounter.GetCount(ThingDefOf.MedicineHerbal);
            
            int totalMedicine = industrialMedicine + herbalMedicine;
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            
            // Need at least 5 medicine per colonist
            int medicineNeeded = colonistCount * 5;
            
            if (totalMedicine < medicineNeeded)
            {
                RimWatchLogger.LogDecision("ProductionAutomation", "MedicineProduction", new Dictionary<string, object>
                {
                    { "totalMedicine", totalMedicine },
                    { "needed", medicineNeeded },
                    { "deficit", medicineNeeded - totalMedicine }
                });
                
                // Create herbal medicine bills (always possible if neutroamine not available)
                var drugLabs = map.listerBuildings.allBuildingsColonist
                    .Where(b => b is Building_WorkTable && b.def.defName.Contains("DrugLab"))
                    .Cast<Building_WorkTable>()
                    .ToList();
                
                if (drugLabs.Count > 0)
                {
                    // Try industrial medicine first
                    if (industrialMedicine < medicineNeeded / 2)
                    {
                        CreateDrugBill(map, "Make_MedicineIndustrial", medicineNeeded - totalMedicine);
                    }
                }
                
                // Herbal medicine as fallback (crafting spot)
                var craftingSpots = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.defName == "CraftingSpot" || b.def.defName == "DrugLab")
                    .Cast<Building_WorkTable>()
                    .ToList();
                
                if (craftingSpots.Count > 0 && herbalMedicine < medicineNeeded / 3)
                {
                    RecipeDef herbalRecipe = DefDatabase<RecipeDef>.GetNamedSilentFail("Make_MedicineHerbal");
                    if (herbalRecipe != null)
                    {
                        foreach (var spot in craftingSpots.Take(1))
                        {
                            if (!spot.BillStack.Bills.Any(b => b.recipe == herbalRecipe))
                            {
                                Bill_Production bill = (Bill_Production)herbalRecipe.MakeNewBill();
                                bill.repeatMode = BillRepeatModeDefOf.TargetCount;
                                bill.targetCount = medicineNeeded - totalMedicine;
                                spot.BillStack.AddBill(bill);
                                
                                RimWatchLogger.Info($"ProductionAutomation: Created herbal medicine bill (x{bill.targetCount})");
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Creates food variety bills: simple → fine → lavish based on stage.
        /// </summary>
        private static void CreateFoodVarietyBills(Map map, DevelopmentStage stage)
        {
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            
            // Count existing meals
            int simpleMeals = map.resourceCounter.GetCount(ThingDefOf.MealSimple);
            int fineMeals = map.resourceCounter.GetCount(ThingDefOf.MealFine);
            int lavishMeals = map.resourceCounter.GetCount(ThingDefOf.MealLavish);
            
            // Determine what meals to produce based on stage
            string primaryMealType = "CookMealSimple";
            int targetCount = colonistCount * 10; // 10 meals per colonist
            
            switch (stage)
            {
                case DevelopmentStage.Emergency:
                case DevelopmentStage.EarlyGame:
                    primaryMealType = "CookMealSimple";
                    break;
                    
                case DevelopmentStage.MidGame:
                    primaryMealType = "CookMealFine";
                    targetCount = colonistCount * 8; // Fine meals for mood
                    break;
                    
                case DevelopmentStage.LateGame:
                case DevelopmentStage.EndGame:
                    primaryMealType = "CookMealLavish";
                    targetCount = colonistCount * 5; // Lavish meals for maximum mood
                    break;
            }
            
            RimWatchLogger.LogDecision("ProductionAutomation", "FoodVariety", new Dictionary<string, object>
            {
                { "stage", stage.ToString() },
                { "mealType", primaryMealType },
                { "targetCount", targetCount },
                { "simple", simpleMeals },
                { "fine", fineMeals },
                { "lavish", lavishMeals }
            });
            
            CreateMealBill(map, primaryMealType, targetCount);
        }
        
        /// <summary>
        /// Manages bill resources: pause/resume bills when resources are unavailable.
        /// </summary>
        private static void ManageBillResources(Map map)
        {
            var allWorkTables = map.listerBuildings.allBuildingsColonist
                .OfType<Building_WorkTable>()
                .ToList();
            
            int pausedCount = 0;
            int resumedCount = 0;
            
            foreach (var table in allWorkTables)
            {
                foreach (var bill in table.BillStack.Bills.OfType<Bill_Production>())
                {
                    // Check if ingredients are available
                    bool hasIngredients = HasRequiredIngredients(map, bill);
                    
                    if (!hasIngredients && !bill.suspended)
                    {
                        bill.suspended = true;
                        pausedCount++;
                    }
                    else if (hasIngredients && bill.suspended)
                    {
                        bill.suspended = false;
                        resumedCount++;
                    }
                }
            }
            
            if (pausedCount > 0 || resumedCount > 0)
            {
                RimWatchLogger.LogDecision("ProductionAutomation", "BillResourceManagement", new Dictionary<string, object>
                {
                    { "paused", pausedCount },
                    { "resumed", resumedCount }
                });
            }
        }
        
        /// <summary>
        /// Checks if required ingredients are available for a bill.
        /// </summary>
        private static bool HasRequiredIngredients(Map map, Bill_Production bill)
        {
            if (bill.recipe == null || bill.recipe.ingredients == null)
                return true; // No ingredients required
            
            foreach (var ingredient in bill.recipe.ingredients)
            {
                // Check if we have enough of this ingredient
                float available = 0f;
                
                foreach (var thingDef in ingredient.filter.AllowedThingDefs)
                {
                    available += map.resourceCounter.GetCount(thingDef);
                }
                
                if (available < ingredient.GetBaseCount())
                    return false; // Not enough of this ingredient
            }
            
            return true; // All ingredients available
        }
        
        /// <summary>
        /// Helper: Creates weapon bills.
        /// </summary>
        private static void CreateWeaponBill(Map map, string recipeDefName, int count)
        {
            var smithingBenches = map.listerBuildings.allBuildingsColonist
                .Where(b => b is Building_WorkTable && b.def.defName.Contains("Smithing"))
                .Cast<Building_WorkTable>()
                .ToList();
            
            if (smithingBenches.Count == 0) return;
            
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
            if (recipe == null) return;
            
            foreach (var bench in smithingBenches)
            {
                if (bench.BillStack.Bills.Any(b => b.recipe == recipe))
                    continue;
                
                Bill_Production bill = (Bill_Production)recipe.MakeNewBill();
                bill.repeatMode = BillRepeatModeDefOf.TargetCount;
                bill.targetCount = count;
                bench.BillStack.AddBill(bill);
                
                RimWatchLogger.Info($"ProductionAutomation: Created {recipeDefName} bill (x{count})");
            }
        }
    }
}

