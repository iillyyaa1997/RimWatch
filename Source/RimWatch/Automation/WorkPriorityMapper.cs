using RimWatch.Utils;
using RimWatch.Automation;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using static RimWatch.Automation.WorkAutomation;

namespace RimWatch.Automation
{
    /// <summary>
    /// Work category classification.
    /// </summary>
    public enum WorkCategory
    {
        Survival,       // Food, Doctor → Priority 1-2
        Infrastructure, // Construction, Repair → Priority 2-3
        Production,     // Crafting, Smithing → Priority 3
        Maintenance,    // Hauling, Cleaning → Priority 3-4
        Luxury,         // Art, Research → Priority 4
        Unknown         // Mod-added, analyze dynamically
    }

    /// <summary>
    /// Universal work priority mapper supporting all WorkTypeDef from game and mods.
    /// </summary>
    public static class WorkPriorityMapper
    {
        private static Dictionary<WorkTypeDef, WorkCategory> _categoryCache = new Dictionary<WorkTypeDef, WorkCategory>();
        private static bool _firstRun = true;

        /// <summary>
        /// Gets all work types including from mods.
        /// </summary>
        public static List<WorkTypeDef> GetAllModdedWorkTypes()
        {
            var allWork = DefDatabase<WorkTypeDef>.AllDefsListForReading;

            // Log discovered work types on first run
            if (_firstRun)
            {
                _firstRun = false;
                RimWatchLogger.Info($"WorkPriorityMapper: Discovered {allWork.Count} work types:");
                
                foreach (var work in allWork)
                {
                    bool isVanilla = work.modContentPack?.PackageId == "ludeon.rimworld" || 
                                    work.modContentPack == null;
                    string source = isVanilla ? "[Vanilla]" : $"[{work.modContentPack?.Name ?? "Unknown"}]";
                    RimWatchLogger.Info($"  - {work.defName} {source}");
                }
            }

            return allWork;
        }

        /// <summary>
        /// Maps work type to category.
        /// </summary>
        public static WorkCategory MapWorkTypeToCategory(WorkTypeDef workType)
        {
            // Check cache first
            if (_categoryCache.TryGetValue(workType, out WorkCategory cached))
            {
                return cached;
            }

            WorkCategory category = AnalyzeWorkType(workType);
            _categoryCache[workType] = category;
            
            return category;
        }

        /// <summary>
        /// Analyzes work type to determine category.
        /// </summary>
        private static WorkCategory AnalyzeWorkType(WorkTypeDef workType)
        {
            string defName = workType.defName.ToLower();
            string label = workType.labelShort?.ToLower() ?? workType.label?.ToLower() ?? "";
            string desc = workType.description?.ToLower() ?? "";

            // Survival keywords
            if (ContainsAny(defName, label, desc, "doctor", "medical", "cook", "cooking", "hunt", "hunting", "food", "medicine"))
            {
                return WorkCategory.Survival;
            }

            // Infrastructure keywords
            if (ContainsAny(defName, label, desc, "construct", "build", "repair", "mining", "mine", "plant cutting"))
            {
                return WorkCategory.Infrastructure;
            }

            // Production keywords
            if (ContainsAny(defName, label, desc, "craft", "smith", "tailor", "brew", "drug", "production", "fabrica"))
            {
                return WorkCategory.Production;
            }

            // Maintenance keywords
            if (ContainsAny(defName, label, desc, "haul", "clean", "warden", "handling", "basic"))
            {
                return WorkCategory.Maintenance;
            }

            // Luxury keywords
            if (ContainsAny(defName, label, desc, "art", "research", "social", "recreation"))
            {
                return WorkCategory.Luxury;
            }

            return WorkCategory.Unknown;
        }

        /// <summary>
        /// Helper to check if any string contains keywords.
        /// </summary>
        private static bool ContainsAny(string str1, string str2, string str3, params string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (str1.Contains(keyword) || str2.Contains(keyword) || str3.Contains(keyword))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets base priority for work based on category and colony needs.
        /// </summary>
        public static int GetBasePriorityForWork(WorkTypeDef workType, ColonyNeeds needs)
        {
            WorkCategory category = MapWorkTypeToCategory(workType);

            switch (category)
            {
                case WorkCategory.Survival:
                    // Always high priority
                    return needs.MedicalUrgency >= 2 || needs.FoodUrgency >= 2 ? 1 : 2;

                case WorkCategory.Infrastructure:
                    // Varies based on construction needs
                    return needs.ConstructionUrgency >= 3 ? 1 : (needs.ConstructionUrgency >= 2 ? 2 : 3);

                case WorkCategory.Production:
                    // Medium priority
                    return 3;

                case WorkCategory.Maintenance:
                    // Lower priority
                    return needs.ConstructionUrgency >= 3 ? 3 : 4;

                case WorkCategory.Luxury:
                    // Lowest priority
                    return 4;

                case WorkCategory.Unknown:
                    // Conservative default + analyze
                    RimWatchLogger.Debug($"WorkPriorityMapper: Unknown work type '{workType.defName}' - using conservative priority 3");
                    return 3;

                default:
                    return 3;
            }
        }

        /// <summary>
        /// Clears category cache.
        /// </summary>
        public static void ClearCache()
        {
            _categoryCache.Clear();
            _firstRun = true;
        }
    }
}

