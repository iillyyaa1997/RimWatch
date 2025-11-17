using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace RimWatch.Automation
{
    /// <summary>
    /// 🌲 Resource Automation - автоматическая добыча ресурсов (дерево, камень, железо, охота).
    /// Manages resource gathering: woodcutting, mining, hunting.
    /// </summary>
    public static class ResourceAutomation
    {
        private static int _lastResourceCheckTick = 0;
        private const int ResourceCheckInterval = 300; // Check every 5 seconds

        /// <summary>
        /// Main entry point for resource automation.
        /// </summary>
        public static void AutoManageResources(Map map)
        {
            try
            {
                // Throttle resource checks
                if (Find.TickManager.TicksGame - _lastResourceCheckTick < ResourceCheckInterval)
                    return;

                _lastResourceCheckTick = Find.TickManager.TicksGame;

                // Analyze resource needs
                ResourceNeeds needs = AnalyzeResourceNeeds(map);

                // Prioritize critical resources
                // ✅ EMERGENCY MODE: If NO wood - designate MANY trees immediately!
                if (needs.EmergencyWood)
                {
                    AutoDesignateTreesForCutting(map);
                }
                else if (needs.NeedsWood)
                {
                    AutoDesignateTreesForCutting(map);
                }

                if (needs.NeedsMetal)
                {
                    AutoDesignateMining(map);
                }
                
                // ✅ NEW: Auto-process stone into blocks if wood is low
                if (needs.NeedsWood || needs.NeedsStone)
                {
                    AutoProcessStoneBlocks(map);
                }

                if (needs.NeedsFood)
                {
                    AutoDesignateHunting(map);
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ResourceAutomation: Error in AutoManageResources", ex);
            }
        }

        /// <summary>
        /// Analyzes what resources the colony needs.
        /// </summary>
        private static ResourceNeeds AnalyzeResourceNeeds(Map map)
        {
            ResourceNeeds needs = new ResourceNeeds();

            try
            {
                // Count available resources
                List<Thing> allItems = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);

                int woodCount = allItems.Where(t => t.def.defName == "WoodLog").Sum(t => t.stackCount);
                int steelCount = allItems.Where(t => t.def.defName == "Steel").Sum(t => t.stackCount);
                int componentCount = allItems.Where(t => t.def.defName.Contains("Component")).Sum(t => t.stackCount);
                
                // ✅ Count stone chunks and blocks
                int stoneChunkCount = allItems.Where(t => t.def.defName.Contains("Chunk")).Sum(t => t.stackCount);
                int stoneBlockCount = allItems.Where(t => t.def.defName.Contains("Blocks")).Sum(t => t.stackCount);
                
                // Count raw meat/meals for food
                int meatCount = allItems.Where(t => t.def.defName.Contains("Meat")).Sum(t => t.stackCount);
                int mealCount = allItems.Where(t => t.def.IsIngestible && t.def.ingestible != null && t.def.ingestible.preferability >= FoodPreferability.MealAwful).Sum(t => t.stackCount);
                
                int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;

                // ✅ Wood thresholds (critical for early game building)
                needs.NeedsWood = woodCount < (colonistCount * 100); // 100 wood per colonist
                
                // ✅ CRITICAL: Emergency wood mode if NO wood at all!
                needs.EmergencyWood = (woodCount == 0 && colonistCount > 0);

                // ✅ Metal thresholds (for tools, weapons, advanced structures)
                needs.NeedsMetal = steelCount < (colonistCount * 50); // 50 steel per colonist
                
                // ✅ Stone blocks (alternative to wood for building)
                // Need stone if wood is low AND we have chunks to process
                needs.NeedsStone = (woodCount < 50 && stoneChunkCount > 10 && stoneBlockCount < 50);

                // ✅ Food thresholds (meat for cooking)
                int foodTotal = meatCount + (mealCount * 2); // Meals count double
                needs.NeedsFood = foodTotal < (colonistCount * 20); // 20 food per colonist

                if (needs.NeedsWood)
                    RimWatchLogger.Info($"ResourceAutomation: Need wood! Current: {woodCount}, colonists: {colonistCount}");
                    
                if (needs.EmergencyWood)
                    RimWatchLogger.WarningThrottledByKey(
                        "resource_emergency_no_wood",
                        "🚨 ResourceAutomation: EMERGENCY - NO WOOD! Forcing tree cutting!",
                        cooldownTicks: 1200);

                if (needs.NeedsMetal)
                    RimWatchLogger.Info($"ResourceAutomation: Need metal! Current: {steelCount}, colonists: {colonistCount}");
                    
                if (needs.NeedsStone)
                    RimWatchLogger.Info($"ResourceAutomation: Processing stone! Chunks: {stoneChunkCount}, Blocks: {stoneBlockCount}");

                if (needs.NeedsFood)
                    RimWatchLogger.Info($"ResourceAutomation: Need food! Current meat: {meatCount}, meals: {mealCount}");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ResourceAutomation: Error analyzing resource needs", ex);
            }

            return needs;
        }

        /// <summary>
        /// Automatically designates trees for cutting near the base.
        /// </summary>
        private static void AutoDesignateTreesForCutting(Map map)
        {
            try
            {
                // Find base center
                IntVec3 baseCenter = GetBaseCenter(map);

                // Search for trees in expanding radius
                List<Plant> treesToCut = new List<Plant>();
                
                for (int radius = 10; radius < 40 && treesToCut.Count < 10; radius += 5)
                {
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(baseCenter, radius, false))
                    {
                        if (!cell.InBounds(map)) continue;
                        if (map.fogGrid.IsFogged(cell)) continue;

                        // Find trees
                        Plant plant = cell.GetPlant(map);
                        if (plant != null && plant.def.plant != null && plant.def.plant.IsTree)
                        {
                            // Check if already designated or harvested
                            Designation cutDesignation = map.designationManager.DesignationOn(plant, DesignationDefOf.CutPlant);
                            Designation harvestDesignation = map.designationManager.DesignationOn(plant, DesignationDefOf.HarvestPlant);
                            
                            if (cutDesignation == null && harvestDesignation == null)
                            {
                                treesToCut.Add(plant);
                                
                                if (treesToCut.Count >= 10) // Limit to 10 trees at a time
                                    break;
                            }
                        }
                    }
                }

                // Designate trees for cutting
                foreach (Plant tree in treesToCut)
                {
                    // ✅ CRITICAL: Double-check before adding (race condition protection)
                    Designation existing = map.designationManager.DesignationOn(tree, DesignationDefOf.CutPlant);
                    if (existing == null)
                    {
                        Designation designation = new Designation(tree, DesignationDefOf.CutPlant);
                        map.designationManager.AddDesignation(designation);
                    }
                }

                if (treesToCut.Count > 0)
                {
                    RimWatchLogger.Info($"🌲 ResourceAutomation: Designated {treesToCut.Count} trees for cutting");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ResourceAutomation: Error in AutoDesignateTreesForCutting", ex);
            }
        }

        /// <summary>
        /// Automatically designates rocks/ore for mining.
        /// </summary>
        private static void AutoDesignateMining(Map map)
        {
            try
            {
                IntVec3 baseCenter = GetBaseCenter(map);

                // Search for mineable rocks (steel, components, etc.)
                List<Thing> rocksToMine = new List<Thing>();

                for (int radius = 10; radius < 40 && rocksToMine.Count < 5; radius += 5)
                {
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(baseCenter, radius, false))
                    {
                        if (!cell.InBounds(map)) continue;
                        if (map.fogGrid.IsFogged(cell)) continue;

                        // Find mineable things
                        Thing mineable = cell.GetFirstMineable(map);
                        if (mineable != null && mineable.def.mineable)
                        {
                            // Check if already designated
                            Designation mineDesignation = map.designationManager.DesignationOn(mineable, DesignationDefOf.Mine);
                            
                            if (mineDesignation == null)
                            {
                                rocksToMine.Add(mineable);
                                
                                if (rocksToMine.Count >= 5) // Limit to 5 mining jobs at a time
                                    break;
                            }
                        }
                    }
                }

                // Designate for mining
                foreach (Thing rock in rocksToMine)
                {
                    // ✅ CRITICAL: Double-check before adding
                    Designation existing = map.designationManager.DesignationOn(rock, DesignationDefOf.Mine);
                    if (existing == null)
                    {
                        Designation designation = new Designation(rock, DesignationDefOf.Mine);
                        map.designationManager.AddDesignation(designation);
                    }
                }

                if (rocksToMine.Count > 0)
                {
                    RimWatchLogger.Info($"⛏️ ResourceAutomation: Designated {rocksToMine.Count} rocks for mining");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ResourceAutomation: Error in AutoDesignateMining", ex);
            }
        }

        /// <summary>
        /// Automatically designates animals for hunting.
        /// </summary>
        private static void AutoDesignateHunting(Map map)
        {
            try
            {
                IntVec3 baseCenter = GetBaseCenter(map);

                // Find suitable prey (herbivores, small animals - not predators!)
                List<Pawn> preyToHunt = new List<Pawn>();

                List<Pawn> wildAnimals = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.RaceProps.Animal && p.Faction == null && !p.Dead)
                    .OrderBy(p => p.Position.DistanceTo(baseCenter))
                    .ToList();

                foreach (Pawn animal in wildAnimals)
                {
                    if (preyToHunt.Count >= 3) break; // Limit to 3 hunts at a time

                    // Skip if in fog
                    if (map.fogGrid.IsFogged(animal.Position)) continue;

                    // Skip if already designated
                    if (map.designationManager.DesignationOn(animal, DesignationDefOf.Hunt) != null)
                        continue;

                    // ✅ Safety check: Only hunt herbivores or small animals
                    bool isPredator = animal.RaceProps.predator;
                    bool isSmall = animal.BodySize < 1.0f; // Small animals (rabbits, chickens, etc.)
                    bool isHerbivore = (animal.RaceProps.foodType & FoodTypeFlags.Plant) != FoodTypeFlags.None ||
                                      (animal.RaceProps.foodType & FoodTypeFlags.VegetarianRoughAnimal) != FoodTypeFlags.None;

                    if (!isPredator && (isSmall || isHerbivore))
                    {
                        // Check distance (not too far)
                        if (animal.Position.DistanceTo(baseCenter) < 50f)
                        {
                            preyToHunt.Add(animal);
                        }
                    }
                }

                // Designate for hunting
                foreach (Pawn prey in preyToHunt)
                {
                    // ✅ CRITICAL: Double-check before adding
                    Designation existing = map.designationManager.DesignationOn(prey, DesignationDefOf.Hunt);
                    if (existing == null)
                    {
                        Designation designation = new Designation(prey, DesignationDefOf.Hunt);
                        map.designationManager.AddDesignation(designation);
                    }
                }

                if (preyToHunt.Count > 0)
                {
                    RimWatchLogger.Info($"🦌 ResourceAutomation: Designated {preyToHunt.Count} animals for hunting");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ResourceAutomation: Error in AutoDesignateHunting", ex);
            }
        }

        /// <summary>
        /// Gets the center of the colony base.
        /// </summary>
        private static IntVec3 GetBaseCenter(Map map)
        {
            try
            {
                List<Building> buildings = map.listerBuildings.allBuildingsColonist.ToList();

                if (buildings.Count == 0)
                    return map.Center;

                int avgX = (int)buildings.Average(b => b.Position.x);
                int avgZ = (int)buildings.Average(b => b.Position.z);

                return new IntVec3(avgX, 0, avgZ);
            }
            catch
            {
                return map.Center;
            }
        }

        /// <summary>
        /// Автоматически создаёт билы на обработку камня в блоки.
        /// </summary>
        private static void AutoProcessStoneBlocks(Map map)
        {
            try
            {
                // Находим стол каменщика (Stonecutter's table)
                Building_WorkTable stonecutterTable = map.listerBuildings.allBuildingsColonist
                    .OfType<Building_WorkTable>()
                    .FirstOrDefault(b => b.def.defName.Contains("Stonecutter") || 
                                         b.def.defName.Contains("Table") && b.def.inspectorTabs != null);

                if (stonecutterTable == null)
                {
                    RimWatchLogger.Debug("ResourceAutomation: No stonecutter table found for stone processing");
                    return;
                }

                // Проверяем есть ли уже билы на обработку камня
                if (stonecutterTable.BillStack.Count >= 4) // Не переполняем билы
                {
                    RimWatchLogger.Debug("ResourceAutomation: Stonecutter table already has bills");
                    return;
                }

                // Ищем доступные типы камня (chunks)
                List<Thing> stoneChunks = map.listerThings.ThingsInGroup(ThingRequestGroup.Chunk).ToList();
                if (stoneChunks.Count == 0)
                {
                    RimWatchLogger.Debug("ResourceAutomation: No stone chunks available");
                    return;
                }

                // Получаем рецепт на изготовление каменных блоков
                RecipeDef makeBlocksRecipe = DefDatabase<RecipeDef>.AllDefs
                    .FirstOrDefault(r => r.defName.Contains("MakeStone") || 
                                        (r.products != null && r.products.Any(p => p.thingDef.defName.Contains("Blocks"))));

                if (makeBlocksRecipe == null)
                {
                    RimWatchLogger.Warning("ResourceAutomation: No stone blocks recipe found");
                    return;
                }

                // Создаём бил на изготовление блоков
                Bill_Production newBill = (Bill_Production)makeBlocksRecipe.MakeNewBill();
                newBill.repeatMode = BillRepeatModeDefOf.TargetCount;
                newBill.targetCount = 100; // Сделать 100 блоков
                newBill.pauseWhenSatisfied = true;

                stonecutterTable.BillStack.AddBill(newBill);
                
                RimWatchLogger.Info($"🪨 ResourceAutomation: Added stone processing bill to {stonecutterTable.def.label}");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("ResourceAutomation: Error in AutoProcessStoneBlocks", ex);
            }
        }

        /// <summary>
        /// Resource needs analysis structure.
        /// </summary>
        private class ResourceNeeds
        {
            public bool NeedsWood { get; set; } = false;
            public bool EmergencyWood { get; set; } = false;  // ✅ NEW: No wood at all!
            public bool NeedsMetal { get; set; } = false;
            public bool NeedsStone { get; set; } = false;
            public bool NeedsFood { get; set; } = false;
        }
    }
}

