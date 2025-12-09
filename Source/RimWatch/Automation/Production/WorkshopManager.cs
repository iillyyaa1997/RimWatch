using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.Production
{
    /// <summary>
    /// Workshop specialization system - auto-assign workshops to specific tasks.
    /// Prevents conflicts and optimizes production efficiency.
    /// </summary>
    public static class WorkshopManager
    {
        // Track workshop specializations
        private static Dictionary<int, WorkshopSpecialization> _workshopRoles = new Dictionary<int, WorkshopSpecialization>();

        /// <summary>
        /// Assign workshops to specific roles based on colony needs.
        /// </summary>
        public static void AssignWorkshopSpecializations(Map map)
        {
            if (map == null) return;

            // Find all workshops
            var tailoringBenches = FindWorkshops(map, "Tailoring");
            var smithingBenches = FindWorkshops(map, "Smithing");
            var drugLabs = FindWorkshops(map, "DrugLab");
            var craftingSpots = FindWorkshops(map, "CraftingSpot");

            // Assign tailoring bench roles
            AssignTailoringRoles(tailoringBenches);

            // Assign smithing bench roles
            AssignSmithingRoles(smithingBenches);

            // Assign drug lab roles
            AssignDrugLabRoles(drugLabs);

            RimWatchLogger.Debug($"WorkshopManager: Assigned {_workshopRoles.Count} workshop specializations");
        }

        /// <summary>
        /// Assign tailoring bench specializations.
        /// </summary>
        private static void AssignTailoringRoles(List<Building_WorkTable> benches)
        {
            if (benches.Count == 0) return;

            if (benches.Count == 1)
            {
                // Single bench does everything
                SetSpecialization(benches[0], WorkshopRole.General, "Clothing & Trade Goods");
            }
            else if (benches.Count >= 2)
            {
                // First bench: colonist clothing
                SetSpecialization(benches[0], WorkshopRole.ColonistGear, "Colonist Clothing");

                // Second bench: trade goods
                SetSpecialization(benches[1], WorkshopRole.TradeGoods, "Trade Apparel");
            }
        }

        /// <summary>
        /// Assign smithing bench specializations.
        /// </summary>
        private static void AssignSmithingRoles(List<Building_WorkTable> benches)
        {
            if (benches.Count == 0) return;

            if (benches.Count == 1)
            {
                // Single bench does everything
                SetSpecialization(benches[0], WorkshopRole.General, "Weapons & Armor");
            }
            else if (benches.Count >= 2)
            {
                // First bench: weapons
                SetSpecialization(benches[0], WorkshopRole.Weapons, "Weapon Production");

                // Second bench: armor
                SetSpecialization(benches[1], WorkshopRole.Armor, "Armor Production");
            }
        }

        /// <summary>
        /// Assign drug lab specializations.
        /// </summary>
        private static void AssignDrugLabRoles(List<Building_WorkTable> labs)
        {
            if (labs.Count == 0) return;

            if (labs.Count == 1)
            {
                // Single lab: medicine focus
                SetSpecialization(labs[0], WorkshopRole.Medicine, "Medicine Production");
            }
            else if (labs.Count >= 2)
            {
                // First lab: medicine
                SetSpecialization(labs[0], WorkshopRole.Medicine, "Medicine Production");

                // Second lab: recreational drugs (if aggressive storyteller)
                SetSpecialization(labs[1], WorkshopRole.Drugs, "Recreational Drugs");
            }
        }

        /// <summary>
        /// Set workshop specialization.
        /// </summary>
        private static void SetSpecialization(Building_WorkTable workshop, WorkshopRole role, string description)
        {
            int thingID = workshop.thingIDNumber;

            if (_workshopRoles.ContainsKey(thingID))
            {
                _workshopRoles[thingID] = new WorkshopSpecialization
                {
                    Role = role,
                    Description = description
                };
            }
            else
            {
                _workshopRoles.Add(thingID, new WorkshopSpecialization
                {
                    Role = role,
                    Description = description
                });
            }

            RimWatchLogger.Debug($"WorkshopManager: {workshop.def.label} assigned to '{description}'");
        }

        /// <summary>
        /// Get workshop specialization.
        /// </summary>
        public static WorkshopSpecialization GetSpecialization(Building_WorkTable workshop)
        {
            if (workshop == null) return null;

            int thingID = workshop.thingIDNumber;
            if (_workshopRoles.TryGetValue(thingID, out WorkshopSpecialization spec))
            {
                return spec;
            }

            return null;
        }

        /// <summary>
        /// Check if a workshop should produce this item based on its role.
        /// </summary>
        public static bool ShouldProduceItem(Building_WorkTable workshop, RecipeDef recipe)
        {
            if (workshop == null || recipe == null) return true;

            var spec = GetSpecialization(workshop);
            if (spec == null) return true; // No specialization - produce anything

            // Check if recipe matches workshop role
            string recipeName = recipe.defName.ToLower();
            string recipeLabel = recipe.label.ToLower();

            switch (spec.Role)
            {
                case WorkshopRole.ColonistGear:
                    // Only basic/good quality clothing for colonists
                    return recipeName.Contains("apparel") && !recipeName.Contains("art");

                case WorkshopRole.TradeGoods:
                    // High-quality apparel, art clothing for trade
                    return recipeName.Contains("apparel") || recipeLabel.Contains("art");

                case WorkshopRole.Weapons:
                    // Only weapons
                    return recipeName.Contains("weapon") || recipeName.Contains("gun") || recipeName.Contains("melee");

                case WorkshopRole.Armor:
                    // Only armor pieces
                    return recipeName.Contains("armor") || recipeLabel.Contains("helmet") || recipeLabel.Contains("vest");

                case WorkshopRole.Medicine:
                    // Only medicine
                    return recipeName.Contains("medicine");

                case WorkshopRole.Drugs:
                    // Recreational drugs, not medicine
                    return (recipeName.Contains("drug") || recipeName.Contains("beer") || recipeName.Contains("joint")) &&
                           !recipeName.Contains("medicine");

                case WorkshopRole.General:
                default:
                    // General workshop produces anything
                    return true;
            }
        }

        /// <summary>
        /// Find workshops by def name pattern.
        /// </summary>
        private static List<Building_WorkTable> FindWorkshops(Map map, string defNamePattern)
        {
            return map.listerBuildings.allBuildingsColonist
                .Where(b => b is Building_WorkTable && b.def.defName.Contains(defNamePattern))
                .OfType<Building_WorkTable>()
                .ToList();
        }

        /// <summary>
        /// Clean up destroyed workshops from tracking.
        /// </summary>
        public static void CleanupDestroyedWorkshops(Map map)
        {
            var validIDs = map.listerBuildings.allBuildingsColonist
                .OfType<Building_WorkTable>()
                .Select(w => w.thingIDNumber)
                .ToHashSet();

            // Remove entries for destroyed workshops
            var keysToRemove = _workshopRoles.Keys.Where(id => !validIDs.Contains(id)).ToList();

            foreach (var key in keysToRemove)
            {
                _workshopRoles.Remove(key);
            }

            if (keysToRemove.Count > 0)
            {
                RimWatchLogger.Debug($"WorkshopManager: Cleaned up {keysToRemove.Count} destroyed workshops");
            }
        }
    }

    /// <summary>
    /// Workshop role enumeration.
    /// </summary>
    public enum WorkshopRole
    {
        General,        // Does everything
        ColonistGear,   // Clothing/weapons for colonists
        TradeGoods,     // Items for trade
        Weapons,        // Weapon production
        Armor,          // Armor production
        Medicine,       // Medicine production
        Drugs           // Recreational drugs
    }

    /// <summary>
    /// Workshop specialization data.
    /// </summary>
    public class WorkshopSpecialization
    {
        public WorkshopRole Role { get; set; }
        public string Description { get; set; }
    }
}

