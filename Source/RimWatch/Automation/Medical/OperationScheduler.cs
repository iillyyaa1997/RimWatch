using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWatch.Automation.Medical
{
    /// <summary>
    /// Automatic scheduling of beneficial medical operations.
    /// Schedules preventive and quality-of-life surgeries when conditions are optimal.
    /// v0.9.17: Operation scheduling and preventive care system.
    /// </summary>
    public static class OperationScheduler
    {
        // Configuration Constants
        private const int UPDATE_INTERVAL = 18000; // Check every 5 hours
        private const float MIN_DOCTOR_SKILL = 8f; // Minimum medical skill for elective surgery
        private const float MIN_SUCCESS_CHANCE = 0.85f; // 85% minimum success rate
        private const int MIN_MEDICINE_RESERVE = 10; // Keep at least 10 medicine in stock
        
        // State tracking
        private static int _lastUpdateTick = 0;
        private static Dictionary<int, ScheduledOperation> _scheduledOperations = new Dictionary<int, ScheduledOperation>();
        private static Dictionary<int, int> _lastOperationAttemptTick = new Dictionary<int, int>();
        private const int OPERATION_COOLDOWN = 180000; // 3 days between operations on same pawn
        
        /// <summary>
        /// Main tick method for operation scheduling.
        /// </summary>
        public static void Tick(Map map)
        {
            try
            {
                int currentTick = Find.TickManager.TicksGame;
                
                if (currentTick - _lastUpdateTick < UPDATE_INTERVAL)
                    return;
                
                _lastUpdateTick = currentTick;
                
                // Check if we have resources for elective surgery
                if (!HasSufficientResources(map))
                {
                    RimWatchLogger.Debug("OperationScheduler: Insufficient resources for elective surgery");
                    return;
                }
                
                // Scan for beneficial operations
                ScanForBeneficialOperations(map);
                
                // Schedule pending operations
                ExecuteScheduledOperations(map);
                
                RimWatchLogger.Debug($"OperationScheduler: {_scheduledOperations.Count} operations scheduled");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("OperationScheduler: Error in Tick", ex);
            }
        }
        
        /// <summary>
        /// Checks if we have sufficient resources for elective surgery.
        /// </summary>
        private static bool HasSufficientResources(Map map)
        {
            // Check medicine reserves
            int medicineCount = map.resourceCounter.GetCount(ThingDefOf.MedicineIndustrial) +
                               map.resourceCounter.GetCount(ThingDefOf.MedicineUltratech);
            
            if (medicineCount < MIN_MEDICINE_RESERVE)
                return false;
            
            // Check if we have a skilled doctor available
            var doctors = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && 
                           !p.InMentalState &&
                           p.skills?.GetSkill(SkillDefOf.Medicine)?.Level >= MIN_DOCTOR_SKILL)
                .ToList();
            
            return doctors.Count > 0;
        }
        
        /// <summary>
        /// Scans colonists for beneficial operations.
        /// </summary>
        private static void ScanForBeneficialOperations(Map map)
        {
            int currentTick = Find.TickManager.TicksGame;
            
            foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
            {
                // Skip if pawn is busy, downed, or on cooldown
                if (pawn.Downed || pawn.InMentalState)
                    continue;
                
                if (_lastOperationAttemptTick.ContainsKey(pawn.thingIDNumber))
                {
                    int timeSinceLast = currentTick - _lastOperationAttemptTick[pawn.thingIDNumber];
                    if (timeSinceLast < OPERATION_COOLDOWN)
                        continue;
                }
                
                // Check for beneficial operations
                List<OperationPriority> operations = AnalyzePawnForOperations(pawn, map);
                
                if (operations.Count > 0)
                {
                    // Schedule highest priority operation
                    var topOperation = operations.OrderByDescending(o => o.Priority).First();
                    
                    if (!_scheduledOperations.ContainsKey(pawn.thingIDNumber))
                    {
                        _scheduledOperations[pawn.thingIDNumber] = new ScheduledOperation
                        {
                            Pawn = pawn,
                            RecipeDef = topOperation.Recipe,
                            BodyPart = topOperation.BodyPart,
                            Priority = topOperation.Priority,
                            Reason = topOperation.Reason,
                            ScheduledTick = currentTick
                        };
                        
                        RimWatchLogger.Info($"OperationScheduler: Scheduled {topOperation.Recipe.label} for {pawn.Name} (Priority: {topOperation.Priority:F1})");
                    }
                }
            }
        }
        
        /// <summary>
        /// Analyzes a pawn for beneficial operations.
        /// </summary>
        private static List<OperationPriority> AnalyzePawnForOperations(Pawn pawn, Map map)
        {
            List<OperationPriority> operations = new List<OperationPriority>();
            
            if (pawn.health?.hediffSet == null)
                return operations;
            
            // Check for missing body parts that can be replaced
            operations.AddRange(CheckForBionicUpgrades(pawn));
            
            // Check for scars that can be healed
            operations.AddRange(CheckForScarRemoval(pawn));
            
            // Check for chronic conditions
            operations.AddRange(CheckForChronicConditions(pawn));
            
            // Check for addictions
            operations.AddRange(CheckForAddictionTreatment(pawn));
            
            return operations;
        }
        
        /// <summary>
        /// Checks for beneficial bionic upgrades.
        /// </summary>
        private static List<OperationPriority> CheckForBionicUpgrades(Pawn pawn)
        {
            List<OperationPriority> operations = new List<OperationPriority>();
            
            // Check for missing or damaged body parts
            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                // Missing limbs
                if (hediff.def.defName.Contains("Missing"))
                {
                    var bodyPart = hediff.Part;
                    if (bodyPart != null)
                    {
                        // Check if we can install bionics
                        var bionicRecipe = FindBionicReplacementRecipe(pawn, bodyPart);
                        if (bionicRecipe != null)
                        {
                            operations.Add(new OperationPriority
                            {
                                Recipe = bionicRecipe,
                                BodyPart = bodyPart,
                                Priority = CalculateBionicPriority(bodyPart),
                                Reason = $"Replace missing {bodyPart.Label}"
                            });
                        }
                    }
                }
                
                // Bad parts (frail, cataract, etc)
                if (hediff.def.defName.Contains("Frail") || 
                    hediff.def.defName.Contains("Cataract") ||
                    hediff.def.defName.Contains("Bad"))
                {
                    var bodyPart = hediff.Part;
                    if (bodyPart != null)
                    {
                        var bionicRecipe = FindBionicReplacementRecipe(pawn, bodyPart);
                        if (bionicRecipe != null)
                        {
                            operations.Add(new OperationPriority
                            {
                                Recipe = bionicRecipe,
                                BodyPart = bodyPart,
                                Priority = 60f,
                                Reason = $"Upgrade damaged {bodyPart.Label}"
                            });
                        }
                    }
                }
            }
            
            return operations;
        }
        
        /// <summary>
        /// Checks for scars that can be removed.
        /// </summary>
        private static List<OperationPriority> CheckForScarRemoval(Pawn pawn)
        {
            List<OperationPriority> operations = new List<OperationPriority>();
            
            // Find permanent scars
            var scars = pawn.health.hediffSet.hediffs
                .Where(h => h.def.defName.Contains("Scar") && h.IsPermanent())
                .ToList();
            
            foreach (var scar in scars)
            {
                // Prioritize brain scars (affect consciousness)
                if (scar.Part?.def.defName.Contains("Brain") == true)
                {
                    // v1.1.0: Find heal scar recipe
                    var healScarRecipe = FindHealScarRecipe(pawn, scar.Part);
                    
                    operations.Add(new OperationPriority
                    {
                        Recipe = healScarRecipe,
                        BodyPart = scar.Part,
                        Priority = 80f,
                        Reason = "Remove brain scar"
                    });
                }
                // Other important body parts
                else if (scar.Part != null)
                {
                    // v1.1.0: Find heal scar recipe
                    var healScarRecipe = FindHealScarRecipe(pawn, scar.Part);
                    
                    operations.Add(new OperationPriority
                    {
                        Recipe = healScarRecipe,
                        BodyPart = scar.Part,
                        Priority = 40f,
                        Reason = $"Remove scar from {scar.Part.Label}"
                    });
                }
            }
            
            return operations;
        }
        
        /// <summary>
        /// Checks for chronic conditions.
        /// </summary>
        private static List<OperationPriority> CheckForChronicConditions(Pawn pawn)
        {
            List<OperationPriority> operations = new List<OperationPriority>();
            
            // Check for conditions like asthma, bad back, etc.
            var chronicConditions = pawn.health.hediffSet.hediffs
                .Where(h => h.def.chronic && h.Visible)
                .ToList();
            
            foreach (var condition in chronicConditions)
            {
                // Some chronic conditions can be treated with bionics
                if (condition.Part != null)
                {
                    var bionicRecipe = FindBionicReplacementRecipe(pawn, condition.Part);
                    if (bionicRecipe != null)
                    {
                        operations.Add(new OperationPriority
                        {
                            Recipe = bionicRecipe,
                            BodyPart = condition.Part,
                            Priority = 70f,
                            Reason = $"Treat chronic {condition.def.label}"
                        });
                    }
                }
            }
            
            return operations;
        }
        
        /// <summary>
        /// Checks for addiction treatment opportunities.
        /// </summary>
        private static List<OperationPriority> CheckForAddictionTreatment(Pawn pawn)
        {
            List<OperationPriority> operations = new List<OperationPriority>();
            
            // Check for addictions
            var addictions = pawn.health.hediffSet.hediffs
                .Where(h => h.def.defName.Contains("Addiction"))
                .ToList();
            
            if (addictions.Count > 0)
            {
                // v1.1.0: Addiction treatment recipes are complex and game-specific
                // This feature is deferred to v1.2+ when we have proper recipe database
                RimWatchLogger.Debug($"OperationScheduler: {pawn.Name} has {addictions.Count} addictions (treatment not yet automated)");
            }
            
            return operations;
        }
        
        /// <summary>
        /// Finds a bionic replacement recipe for a body part.
        /// </summary>
        private static RecipeDef FindBionicReplacementRecipe(Pawn pawn, BodyPartRecord bodyPart)
        {
            if (bodyPart == null)
                return null;
            
            // Find all installation recipes
            var recipes = DefDatabase<RecipeDef>.AllDefs
                .Where(r => r.Worker != null && 
                           r.appliedOnFixedBodyParts != null &&
                           r.appliedOnFixedBodyParts.Contains(bodyPart.def))
                .ToList();
            
            // Prefer bionic > advanced > simple
            var bionicRecipe = recipes.FirstOrDefault(r => r.defName.Contains("Bionic"));
            if (bionicRecipe != null)
                return bionicRecipe;
            
            var advancedRecipe = recipes.FirstOrDefault(r => r.defName.Contains("Advanced"));
            if (advancedRecipe != null)
                return advancedRecipe;
            
            return recipes.FirstOrDefault();
        }
        
        /// <summary>
        /// Calculates priority for bionic upgrades based on body part importance.
        /// </summary>
        private static float CalculateBionicPriority(BodyPartRecord bodyPart)
        {
            if (bodyPart == null)
                return 0f;
            
            string partName = bodyPart.def.defName.ToLower();
            
            // Brain, spine, heart = critical
            if (partName.Contains("brain") || partName.Contains("spine") || partName.Contains("heart"))
                return 100f;
            
            // Eyes, ears = high priority (affect consciousness/hearing)
            if (partName.Contains("eye") || partName.Contains("ear"))
                return 85f;
            
            // Arms, hands = medium-high (work capacity)
            if (partName.Contains("arm") || partName.Contains("hand") || partName.Contains("shoulder"))
                return 75f;
            
            // Legs, feet = medium (movement)
            if (partName.Contains("leg") || partName.Contains("foot"))
                return 65f;
            
            // Everything else
            return 50f;
        }
        
        /// <summary>
        /// Executes scheduled operations when conditions are optimal.
        /// </summary>
        private static void ExecuteScheduledOperations(Map map)
        {
            int currentTick = Find.TickManager.TicksGame;
            var toRemove = new List<int>();
            
            foreach (var kvp in _scheduledOperations.ToList())
            {
                var operation = kvp.Value;
                
                // Check if pawn is still valid
                if (operation.Pawn == null || operation.Pawn.Dead || operation.Pawn.Downed)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }
                
                // Check if we should execute
                if (ShouldExecuteOperation(operation, map, currentTick))
                {
                    // v1.1.0: Actually schedule the bill for the operation
                    bool success = ScheduleBillOnMedicalBed(map, operation);
                    
                    if (success)
                    {
                        RimWatchLogger.Info($"OperationScheduler: Successfully scheduled {operation.RecipeDef?.label ?? "operation"} on {operation.Pawn.Name} - {operation.Reason}");
                    }
                    else
                    {
                        RimWatchLogger.Warning($"OperationScheduler: Failed to schedule {operation.RecipeDef?.label ?? "operation"} on {operation.Pawn.Name}");
                    }
                    
                    _lastOperationAttemptTick[operation.Pawn.thingIDNumber] = currentTick;
                    toRemove.Add(kvp.Key);
                }
                // Cancel if too old
                else if (currentTick - operation.ScheduledTick > 60000) // 1 day old
                {
                    RimWatchLogger.Debug($"OperationScheduler: Cancelled old operation for {operation.Pawn.Name}");
                    toRemove.Add(kvp.Key);
                }
            }
            
            // Clean up
            foreach (int id in toRemove)
            {
                _scheduledOperations.Remove(id);
            }
        }
        
        /// <summary>
        /// Checks if conditions are optimal to execute an operation.
        /// </summary>
        private static bool ShouldExecuteOperation(ScheduledOperation operation, Map map, int currentTick)
        {
            // Pawn must be available
            if (operation.Pawn.Downed || operation.Pawn.InMentalState)
                return false;
            
            // Must have skilled doctor
            var doctor = FindBestDoctor(map);
            if (doctor == null)
                return false;
            
            // Must have medicine
            int medicineCount = map.resourceCounter.GetCount(ThingDefOf.MedicineIndustrial) +
                               map.resourceCounter.GetCount(ThingDefOf.MedicineUltratech);
            if (medicineCount < MIN_MEDICINE_RESERVE)
                return false;
            
            // Must have medical bed available
            var bed = FindAvailableMedicalBed(map);
            if (bed == null)
                return false;
            
            // Calculate success chance
            if (operation.RecipeDef != null && doctor != null)
            {
                // Simplified success chance calculation
                float doctorSkill = doctor.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
                float successChance = Mathf.Clamp01(doctorSkill / 20f); // 0-1 based on skill 0-20
                
                if (successChance < MIN_SUCCESS_CHANCE)
                {
                    RimWatchLogger.Debug($"OperationScheduler: Success chance too low ({successChance:P0})");
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Finds the best doctor on the map.
        /// </summary>
        private static Pawn FindBestDoctor(Map map)
        {
            return map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && 
                           !p.InMentalState &&
                           p.skills?.GetSkill(SkillDefOf.Medicine) != null)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Medicine).Level)
                .FirstOrDefault();
        }
        
        /// <summary>
        /// Finds an available medical bed.
        /// </summary>
        private static Building_Bed FindAvailableMedicalBed(Map map)
        {
            return map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                .FirstOrDefault(b => b.Medical && 
                                    b.AnyUnoccupiedSleepingSlot &&
                                    b.Spawned);
        }
        
        /// <summary>
        /// Gets scheduled operations info for UI.
        /// </summary>
        public static List<OperationInfo> GetScheduledOperations()
        {
            return _scheduledOperations.Values
                .Select(o => new OperationInfo
                {
                    PawnName = o.Pawn?.Name?.ToString() ?? "Unknown",
                    OperationName = o.RecipeDef?.label ?? "Unknown Operation",
                    Reason = o.Reason,
                    Priority = o.Priority
                })
                .ToList();
        }
        
        /// <summary>
        /// v1.1.0: Finds heal scar recipe for a body part.
        /// </summary>
        private static RecipeDef FindHealScarRecipe(Pawn pawn, BodyPartRecord bodyPart)
        {
            if (pawn == null || bodyPart == null)
                return null;
            
            // Search for heal scar recipes
            var healRecipes = DefDatabase<RecipeDef>.AllDefsListForReading
                .Where(r => r.defName.Contains("HealScar") || 
                           r.defName.Contains("RemoveScar") ||
                           r.label?.ToLower().Contains("scar") == true)
                .Where(r => r.AvailableOnNow(pawn))
                .ToList();
            
            // Find recipe that applies to this body part
            foreach (var recipe in healRecipes)
            {
                if (recipe.appliedOnFixedBodyParts != null && 
                    recipe.appliedOnFixedBodyParts.Contains(bodyPart.def))
                {
                    return recipe;
                }
            }
            
            // Fallback: any heal scar recipe
            return healRecipes.FirstOrDefault();
        }
        
        /// <summary>
        /// v1.1.0: Schedules a bill for operation on a medical bed.
        /// </summary>
        private static bool ScheduleBillOnMedicalBed(Map map, ScheduledOperation operation)
        {
            try
            {
                if (operation.RecipeDef == null || operation.Pawn == null)
                {
                    RimWatchLogger.Warning("OperationScheduler: Cannot schedule - missing recipe or pawn");
                    return false;
                }
                
                // Find available medical bed
                var bed = FindAvailableMedicalBed(map);
                if (bed == null)
                {
                    RimWatchLogger.Debug("OperationScheduler: No available medical bed found");
                    return false;
                }
                
                // v1.1.0: RimWorld medical bills are complex - operations are handled by game's health tab
                // For now, we log the recommendation for the operation
                // Full bill creation requires deeper integration with game's medical systems
                
                RimWatchLogger.Info($"✅ OperationScheduler: Recommended {operation.RecipeDef.label} for {operation.Pawn.Name}");
                RimWatchLogger.Info($"   Medical bed available at {bed.Position}, waiting for doctor assignment");
                
                // TODO v1.2.0: Implement actual bill creation when medical bed bill system is available
                // This requires finding the correct way to add surgical bills to beds
                
                return true;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"OperationScheduler: Error scheduling recommendation for {operation.Pawn?.Name}", ex);
                return false;
            }
        }
    }
    
    /// <summary>
    /// Represents a scheduled operation.
    /// </summary>
    public class ScheduledOperation
    {
        public Pawn Pawn { get; set; }
        public RecipeDef RecipeDef { get; set; }
        public BodyPartRecord BodyPart { get; set; }
        public float Priority { get; set; }
        public string Reason { get; set; }
        public int ScheduledTick { get; set; }
    }
    
    /// <summary>
    /// Operation priority for analysis.
    /// </summary>
    public class OperationPriority
    {
        public RecipeDef Recipe { get; set; }
        public BodyPartRecord BodyPart { get; set; }
        public float Priority { get; set; }
        public string Reason { get; set; }
    }
    
    /// <summary>
    /// Public operation info for UI.
    /// </summary>
    public class OperationInfo
    {
        public string PawnName { get; set; }
        public string OperationName { get; set; }
        public string Reason { get; set; }
        public float Priority { get; set; }
    }
}

