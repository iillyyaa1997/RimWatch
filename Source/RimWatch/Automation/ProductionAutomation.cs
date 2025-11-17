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
    }
}

