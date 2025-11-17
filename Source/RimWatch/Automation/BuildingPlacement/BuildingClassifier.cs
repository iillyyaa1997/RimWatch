using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// Intelligently classifies buildings based on ThingDef properties, comps, and behavior.
    /// Works with vanilla and modded content by analyzing actual building characteristics.
    /// </summary>
    public static class BuildingClassifier
    {
        /// <summary>
        /// Building categories for intelligent placement and room planning.
        /// </summary>
        public enum BuildingCategory
        {
            Unknown,
            Bed,
            MedicalBed,
            Stove,
            Workbench,
            ResearchBench,
            RecreationBuilding,
            StorageBuilding,
            ProductionBuilding,
            DefenseBuilding,
            PowerGenerator,
            PowerConduit,
            LightSource,
            Temperature,
            Table,
            Chair,
            Decoration,
            Door,
            Wall
        }

        // Cache for performance
        private static Dictionary<ThingDef, BuildingCategory> _classificationCache = new Dictionary<ThingDef, BuildingCategory>();

        /// <summary>
        /// Classifies a building based on its properties and behavior.
        /// Uses comps, thingClass, recipes, and other characteristics.
        /// </summary>
        public static BuildingCategory ClassifyBuilding(ThingDef def)
        {
            if (def == null)
                return BuildingCategory.Unknown;

            // Check cache first
            if (_classificationCache.ContainsKey(def))
                return _classificationCache[def];

            BuildingCategory category = DetermineCategory(def);
            _classificationCache[def] = category;

            return category;
        }

        /// <summary>
        /// Determines building category through comprehensive analysis.
        /// </summary>
        private static BuildingCategory DetermineCategory(ThingDef def)
        {
            try
            {
                // 1. Check thingClass (most reliable)
                if (def.thingClass != null)
                {
                    string className = def.thingClass.Name;

                    if (className.Contains("Building_Bed") || def.thingClass == typeof(Building_Bed))
                    {
                        // Check if medical bed
                        if (def.building?.bed_humanlike == true && def.building.bed_defaultMedical)
                            return BuildingCategory.MedicalBed;
                        return BuildingCategory.Bed;
                    }

                    if (className.Contains("Building_Door") || className.Contains("Door"))
                        return BuildingCategory.Door;

                    if (className.Contains("Building_TurretGun") || className.Contains("Turret"))
                        return BuildingCategory.DefenseBuilding;
                }

                // 2. Check comps (component analysis)
                if (def.comps != null && def.comps.Count > 0)
                {
                    // Power generator
                    if (def.comps.Any(c => c.compClass?.Name.Contains("CompPowerPlant") == true))
                        return BuildingCategory.PowerGenerator;

                    // Power conduit
                    if (def.comps.Any(c => c.compClass?.Name.Contains("CompPowerTransmitter") == true))
                        return BuildingCategory.PowerConduit;

                    // Light source
                    if (def.comps.Any(c => c.compClass?.Name.Contains("CompGlower") == true))
                        return BuildingCategory.LightSource;

                    // Temperature control (heater, cooler, vent)
                    if (def.comps.Any(c => c.compClass?.Name.Contains("CompTempControl") == true ||
                                          c.compClass?.Name.Contains("CompHeatPusher") == true))
                        return BuildingCategory.Temperature;

                    // Workbenches (affected by facilities)
                    if (def.comps.Any(c => c.compClass?.Name.Contains("CompAffectedByFacilities") == true))
                    {
                        // Is it research or production?
                        if (IsResearchBench(def))
                            return BuildingCategory.ResearchBench;
                        return BuildingCategory.Workbench;
                    }
                }

                // 3. Check recipes (cooking/crafting analysis)
                if (def.AllRecipes != null && def.AllRecipes.Count > 0)
                {
                    // Stove: produces ingestible items (meals)
                    if (def.AllRecipes.Any(r => r.products != null && 
                                               r.products.Any(p => p.thingDef?.IsIngestible == true)))
                        return BuildingCategory.Stove;

                    // Research bench: consumes research-related items or has research work type
                    if (IsResearchBench(def))
                        return BuildingCategory.ResearchBench;

                    // Generic workbench/production
                    return BuildingCategory.Workbench;
                }

                // 4. Check building properties
                if (def.building != null)
                {
                    // Wall
                    if (def.building.isNaturalRock == false && def.fillPercent >= 0.99f && def.passability == Traversability.Impassable)
                        return BuildingCategory.Wall;

                    // Storage (thingClass check for storage building types)
                    if (def.thingClass?.Name.Contains("Storage") == true || def.building.fixedStorageSettings != null)
                        return BuildingCategory.StorageBuilding;
                }

                // 5. Check designation category
                if (def.designationCategory != null)
                {
                    string category = def.designationCategory.defName.ToLower();

                    if (category.Contains("production"))
                        return BuildingCategory.ProductionBuilding;

                    if (category.Contains("security") || category.Contains("defense"))
                        return BuildingCategory.DefenseBuilding;

                    if (category.Contains("recreation") || category.Contains("joy"))
                        return BuildingCategory.RecreationBuilding;

                    if (category.Contains("furniture"))
                    {
                        // Check if table or chair
                        if (IsTable(def))
                            return BuildingCategory.Table;
                        if (IsChair(def))
                            return BuildingCategory.Chair;
                    }
                }

                // 6. Check defName as fallback (for vanilla compatibility)
                string defName = def.defName.ToLower();

                if (defName.Contains("table") && !defName.Contains("butcher"))
                    return BuildingCategory.Table;

                if (defName.Contains("chair") || defName.Contains("stool") || defName.Contains("armchair"))
                    return BuildingCategory.Chair;

                if (defName.Contains("sculpture") || defName.Contains("art") || defName.Contains("statue"))
                    return BuildingCategory.Decoration;

                // 7. Check if it provides joy/recreation
                if (def.building?.joyKind != null)
                    return BuildingCategory.RecreationBuilding;

                return BuildingCategory.Unknown;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"BuildingClassifier: Error classifying {def?.defName}", ex);
                return BuildingCategory.Unknown;
            }
        }

        /// <summary>
        /// Determines if a ThingDef is a research bench.
        /// </summary>
        private static bool IsResearchBench(ThingDef def)
        {
            // Check for research-specific comps
            if (def.comps?.Any(c => c.compClass?.Name.Contains("CompResearch") == true) == true)
                return true;

            // Check if recipes consume research materials
            if (def.AllRecipes?.Any(r => r.workSkill == SkillDefOf.Intellectual) == true)
                return true;

            // Check defName
            if (def.defName.ToLower().Contains("research"))
                return true;

            return false;
        }

        /// <summary>
        /// Checks if ThingDef is a table.
        /// </summary>
        private static bool IsTable(ThingDef def)
        {
            // Tables have surface/interaction spot and are furniture
            if (def.hasInteractionCell && def.surfaceType == SurfaceType.Eat)
                return true;

            if (def.defName.ToLower().Contains("table"))
                return true;

            return false;
        }

        /// <summary>
        /// Checks if ThingDef is a chair/seat.
        /// </summary>
        private static bool IsChair(ThingDef def)
        {
            // Chairs provide sitting comfort
            if (def.comps?.Any(c => c.compClass?.Name.Contains("CompSittable") == true) == true)
                return true;

            if (def.building?.isSittable == true)
                return true;

            if (def.defName.ToLower().Contains("chair") || def.defName.ToLower().Contains("stool"))
                return true;

            return false;
        }

        /// <summary>
        /// Gets all buildings of a specific category on the map.
        /// </summary>
        public static List<Building> GetBuildingsByCategory(Map map, BuildingCategory category)
        {
            List<Building> result = new List<Building>();

            foreach (Building building in map.listerBuildings.allBuildingsColonist)
            {
                if (ClassifyBuilding(building.def) == category)
                {
                    result.Add(building);
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if a colonist has access to a specific building type.
        /// </summary>
        public static bool ColonyHasBuilding(Map map, BuildingCategory category)
        {
            return GetBuildingsByCategory(map, category).Count > 0;
        }

        /// <summary>
        /// Finds the best ThingDef for a building category (prefers vanilla, then mods).
        /// </summary>
        public static ThingDef GetBestThingDefForCategory(BuildingCategory category)
        {
            List<ThingDef> allBuildings = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.category == ThingCategory.Building && d.building != null)
                .ToList();

            foreach (ThingDef def in allBuildings)
            {
                if (ClassifyBuilding(def) == category)
                {
                    // Prefer vanilla defs (shorter defNames usually)
                    if (def.defName.Length < 20)
                        return def;
                }
            }

            // Return any matching def
            return allBuildings.FirstOrDefault(d => ClassifyBuilding(d) == category);
        }

        /// <summary>
        /// Clears classification cache (call on game load or after mod changes).
        /// </summary>
        public static void ClearCache()
        {
            _classificationCache.Clear();
            RimWatchLogger.Info("BuildingClassifier: Cache cleared");
        }
    }
}

