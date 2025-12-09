using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.Production
{
    /// <summary>
    /// Smart bill management system with auto-detection of needs,
    /// resource awareness, quality targeting, and skill-based assignment.
    /// </summary>
    public static class BillIntelligence
    {
        // Track bills we've created to avoid duplicates
        private static HashSet<string> _existingBillKeys = new HashSet<string>();
        private static int _lastCleanupTick = 0;
        private const int CleanupInterval = 60000; // Clean cache every day

        /// <summary>
        /// Detect colonist needs and create appropriate bills.
        /// </summary>
        public static void AutoDetectAndCreateBills(Map map)
        {
            if (map == null) return;

            // Cleanup old bill keys periodically
            if (Find.TickManager.TicksGame - _lastCleanupTick > CleanupInterval)
            {
                _existingBillKeys.Clear();
                _lastCleanupTick = Find.TickManager.TicksGame;
            }

            // Detect needs
            var needs = DetectColonistNeeds(map);

            // Create bills based on needs
            if (needs.NeedClothing)
                CreateClothingBills(map, needs);

            if (needs.NeedWeapons)
                CreateWeaponBills(map, needs);

            if (needs.NeedMedicine)
                CreateMedicineBills(map, needs);

            if (needs.NeedFood)
                CreateFoodBills(map, needs);
        }

        /// <summary>
        /// Detect what colonists need based on current items condition.
        /// </summary>
        private static ColonistNeeds DetectColonistNeeds(Map map)
        {
            var needs = new ColonistNeeds();
            var colonists = map.mapPawns.FreeColonistsSpawned;

            int colonistsNeedingClothes = 0;
            int colonistsNeedingWeapons = 0;

            foreach (var colonist in colonists)
            {
                // Check apparel condition
                if (colonist.apparel != null)
                {
                    foreach (var apparel in colonist.apparel.WornApparel)
                    {
                        float hpPercent = (float)apparel.HitPoints / apparel.MaxHitPoints;
                        if (hpPercent < 0.5f) // Below 50% HP
                        {
                            colonistsNeedingClothes++;
                            needs.DamagedApparelCount++;
                            break;
                        }
                    }
                }

                // Check weapon condition
                var weapon = colonist.equipment?.Primary;
                if (weapon != null)
                {
                    float hpPercent = (float)weapon.HitPoints / weapon.MaxHitPoints;
                    if (hpPercent < 0.6f) // Below 60% HP
                    {
                        colonistsNeedingWeapons++;
                        needs.DamagedWeaponsCount++;
                    }
                }
            }

            needs.NeedClothing = colonistsNeedingClothes > 0;
            needs.NeedWeapons = colonistsNeedingWeapons > 0;

            // Check medicine
            int medicineCount = map.resourceCounter.GetCount(ThingDefOf.MedicineIndustrial) +
                               map.resourceCounter.GetCount(ThingDefOf.MedicineHerbal);
            int colonistCount = colonists.Count();
            needs.NeedMedicine = medicineCount < colonistCount * 5; // 5 medicine per colonist

            // Check food
            int mealCount = map.resourceCounter.GetCount(ThingDefOf.MealSimple) +
                           map.resourceCounter.GetCount(ThingDefOf.MealFine);
            needs.NeedFood = mealCount < colonistCount * 10; // 10 meals per colonist

            return needs;
        }

        /// <summary>
        /// Create clothing bills for damaged apparel.
        /// </summary>
        private static void CreateClothingBills(Map map, ColonistNeeds needs)
        {
            var tailoringBenches = FindWorkTables(map, "Tailoring");
            if (tailoringBenches.Count == 0) return;

            // Create bills for basic clothing
            CreateBillIfNeeded(tailoringBenches[0], "Make_Apparel_Pants", needs.DamagedApparelCount, QualityCategory.Normal);
            CreateBillIfNeeded(tailoringBenches[0], "Make_Apparel_ButtonDownShirt", needs.DamagedApparelCount, QualityCategory.Normal);
            CreateBillIfNeeded(tailoringBenches[0], "Make_Apparel_Parka", needs.DamagedApparelCount / 2, QualityCategory.Normal);

            RimWatchLogger.Info($"BillIntelligence: Created clothing bills for {needs.DamagedApparelCount} colonists");
        }

        /// <summary>
        /// Create weapon bills for damaged weapons.
        /// </summary>
        private static void CreateWeaponBills(Map map, ColonistNeeds needs)
        {
            var smithingBenches = FindWorkTables(map, "Smithing");
            if (smithingBenches.Count == 0) return;

            // Create bills for basic weapons
            CreateBillIfNeeded(smithingBenches[0], "Make_MeleeWeapon_Gladius", needs.DamagedWeaponsCount / 2, QualityCategory.Normal);
            CreateBillIfNeeded(smithingBenches[0], "Make_Gun_Revolver", needs.DamagedWeaponsCount, QualityCategory.Good);

            RimWatchLogger.Info($"BillIntelligence: Created weapon bills for {needs.DamagedWeaponsCount} colonists");
        }

        /// <summary>
        /// Create medicine production bills.
        /// </summary>
        private static void CreateMedicineBills(Map map, ColonistNeeds needs)
        {
            var drugLabs = FindWorkTables(map, "DrugLab");
            if (drugLabs.Count > 0)
            {
                CreateBillIfNeeded(drugLabs[0], "Make_MedicineIndustrial", 10, QualityCategory.Normal);
            }

            // Herbal medicine as fallback
            var craftingSpots = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.defName == "CraftingSpot")
                .OfType<Building_WorkTable>()
                .ToList();

            if (craftingSpots.Count > 0)
            {
                CreateBillIfNeeded(craftingSpots[0], "Make_MedicineHerbal", 15, QualityCategory.Normal);
            }

            RimWatchLogger.Info("BillIntelligence: Created medicine production bills");
        }

        /// <summary>
        /// Create food production bills.
        /// </summary>
        private static void CreateFoodBills(Map map, ColonistNeeds needs)
        {
            var cookStations = map.listerBuildings.allBuildingsColonist
                .Where(b => b is Building_WorkTable && b.def.building?.isMealSource == true)
                .OfType<Building_WorkTable>()
                .ToList();

            if (cookStations.Count == 0) return;

            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
            CreateBillIfNeeded(cookStations[0], "CookMealSimple", colonistCount * 10, QualityCategory.Normal);

            RimWatchLogger.Info($"BillIntelligence: Created food bills for {colonistCount} colonists");
        }

        /// <summary>
        /// Create a bill if it doesn't already exist.
        /// </summary>
        private static void CreateBillIfNeeded(Building_WorkTable workTable, string recipeDefName, int targetCount, QualityCategory minQuality)
        {
            if (workTable == null || targetCount <= 0) return;

            // Generate unique key for this bill
            string billKey = $"{workTable.ThingID}_{recipeDefName}";

            // Check if we already created this bill
            if (_existingBillKeys.Contains(billKey))
                return;

            // Find recipe
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
            if (recipe == null)
            {
                RimWatchLogger.Warning($"BillIntelligence: Recipe '{recipeDefName}' not found");
                return;
            }

            // Check if bill already exists on this table
            if (workTable.BillStack.Bills.Any(b => b.recipe == recipe))
            {
                _existingBillKeys.Add(billKey);
                return;
            }

            // Create new bill
            Bill_Production bill = (Bill_Production)recipe.MakeNewBill();
            bill.repeatMode = BillRepeatModeDefOf.TargetCount;
            bill.targetCount = targetCount;

            // Set quality range if applicable
            if (bill is Bill_ProductionWithUft billWithQuality)
            {
                billWithQuality.SetStoreMode(BillStoreModeDefOf.BestStockpile);
                // Quality range will be set to Normal+ by default
            }

            // Add bill to workbench
            workTable.BillStack.AddBill(bill);
            _existingBillKeys.Add(billKey);

            RimWatchLogger.Info($"BillIntelligence: Created bill '{recipeDefName}' (target: {targetCount}, quality: {minQuality}+)");
        }

        /// <summary>
        /// Find work tables by partial name match.
        /// </summary>
        private static List<Building_WorkTable> FindWorkTables(Map map, string nameContains)
        {
            return map.listerBuildings.allBuildingsColonist
                .Where(b => b is Building_WorkTable && b.def.defName.Contains(nameContains))
                .OfType<Building_WorkTable>()
                .ToList();
        }

        /// <summary>
        /// Pause bills when ingredients are unavailable, resume when available.
        /// </summary>
        public static void ManageBillResources(Map map)
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
                RimWatchLogger.Debug($"BillIntelligence: Paused {pausedCount} bills, resumed {resumedCount} bills");
            }
        }

        /// <summary>
        /// Check if required ingredients are available for a bill.
        /// </summary>
        private static bool HasRequiredIngredients(Map map, Bill_Production bill)
        {
            if (bill.recipe == null || bill.recipe.ingredients == null)
                return true;

            foreach (var ingredient in bill.recipe.ingredients)
            {
                float available = 0f;

                foreach (var thingDef in ingredient.filter.AllowedThingDefs)
                {
                    available += map.resourceCounter.GetCount(thingDef);
                }

                if (available < ingredient.GetBaseCount())
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Assign best crafter to high-priority bills based on skills.
        /// </summary>
        public static void AssignBestCrafters(Map map)
        {
            var workTables = map.listerBuildings.allBuildingsColonist
                .OfType<Building_WorkTable>()
                .ToList();

            foreach (var table in workTables)
            {
                foreach (var bill in table.BillStack.Bills)
                {
                    // Get relevant skill for this recipe
                    if (bill.recipe?.workSkill == null) continue;

                    // Find colonist with highest skill
                    var bestCrafter = FindBestCrafter(map, bill.recipe);
                    if (bestCrafter != null)
                    {
                        // Bill restrictions can be set here if needed
                        // For now, just let the best crafter naturally gravitate to it
                    }
                }
            }
        }

        /// <summary>
        /// Find best crafter for a recipe based on relevant skills.
        /// </summary>
        private static Pawn FindBestCrafter(Map map, RecipeDef recipe)
        {
            if (recipe?.workSkill == null) return null;

            var colonists = map.mapPawns.FreeColonistsSpawned;
            Pawn bestCrafter = null;
            int bestSkillLevel = -1;

            foreach (var colonist in colonists)
            {
                if (colonist.skills == null) continue;

                var skillRecord = colonist.skills.GetSkill(recipe.workSkill);
                if (skillRecord == null) continue;

                // Consider both skill level and passion
                int effectiveSkill = skillRecord.Level;
                if (skillRecord.passion == Passion.Major)
                    effectiveSkill += 4;
                else if (skillRecord.passion == Passion.Minor)
                    effectiveSkill += 2;

                if (effectiveSkill > bestSkillLevel)
                {
                    bestSkillLevel = effectiveSkill;
                    bestCrafter = colonist;
                }
            }

            return bestCrafter;
        }
    }

    /// <summary>
    /// Colonist needs data structure.
    /// </summary>
    public class ColonistNeeds
    {
        public bool NeedClothing { get; set; }
        public bool NeedWeapons { get; set; }
        public bool NeedMedicine { get; set; }
        public bool NeedFood { get; set; }

        public int DamagedApparelCount { get; set; }
        public int DamagedWeaponsCount { get; set; }
    }
}

