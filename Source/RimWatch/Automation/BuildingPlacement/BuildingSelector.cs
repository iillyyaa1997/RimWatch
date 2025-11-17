using RimWatch.Utils;
using RimWorld;
using System.Linq;
using Verse;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// Intelligently selects the best building type based on current colony state.
    /// Considers: available resources, research, power availability, colony size.
    /// </summary>
    public static class BuildingSelector
    {
        /// <summary>
        /// Selects appropriate stove type (Fueled vs Electric).
        /// </summary>
        public static ThingDef SelectStove(Map map, IntVec3 location)
        {
            // Check if generators exist
            bool hasPowerGenerator = map.listerBuildings.allBuildingsColonist
                .Any(b => b.def.defName.Contains("Generator") ||
                         b.def.defName.Contains("Solar") ||
                         b.def.defName.Contains("Geothermal"));

            // Check if Electricity research is complete
            ResearchProjectDef electricityResearch = DefDatabase<ResearchProjectDef>
                .GetNamedSilentFail("Electricity");
            bool electricityResearched = electricityResearch != null && electricityResearch.IsFinished;

            // If no power or research not done → Fueled Stove
            if (!hasPowerGenerator || !electricityResearched)
            {
                ThingDef fueledStove = DefDatabase<ThingDef>.GetNamedSilentFail("FueledStove");
                if (fueledStove != null)
                {
                    RimWatchLogger.Debug($"BuildingSelector: Selected FueledStove (no power: {!hasPowerGenerator}, no research: {!electricityResearched})");
                    return fueledStove;
                }
            }

            // Check if power grid is nearby (within 6 tiles = conduit range)
            bool powerNearby = CheckPowerGridNearby(map, location, 6);

            ThingDef electricStove = DefDatabase<ThingDef>.GetNamedSilentFail("ElectricStove");
            ThingDef fueledStoveBackup = DefDatabase<ThingDef>.GetNamedSilentFail("FueledStove");

            if (powerNearby && electricStove != null)
            {
                RimWatchLogger.Debug($"BuildingSelector: Selected ElectricStove (power nearby)");
                return electricStove;
            }
            else if (fueledStoveBackup != null)
            {
                RimWatchLogger.Debug($"BuildingSelector: Selected FueledStove (power exists but not nearby)");
                return fueledStoveBackup;
            }

            // Fallback
            return electricStove ?? fueledStoveBackup;
        }

        /// <summary>
        /// Selects appropriate bed type based on colony size and relationships.
        /// </summary>
        public static ThingDef SelectBed(Map map, int colonistCount)
        {
            // TODO: In future, check for couples and place double beds
            // For now, just return standard bed

            ThingDef bed = ThingDefOf.Bed;
            if (bed != null)
            {
                RimWatchLogger.Debug($"BuildingSelector: Selected standard Bed");
                return bed;
            }

            return null;
        }

        /// <summary>
        /// Selects appropriate power generator based on research and resources.
        /// v0.8.1: Fixed to exclude Ship/Spaceship parts.
        /// </summary>
        public static ThingDef SelectPowerGenerator(Map map)
        {
            // Check research
            ResearchProjectDef electricityResearch = DefDatabase<ResearchProjectDef>
                .GetNamedSilentFail("Electricity");
            bool electricityResearched = electricityResearch != null && electricityResearch.IsFinished;

            ResearchProjectDef solarResearch = DefDatabase<ResearchProjectDef>
                .GetNamedSilentFail("SolarPanels");
            bool solarResearched = solarResearch != null && solarResearch.IsFinished;

            // Prefer solar if researched (clean, no fuel needed)
            if (solarResearched)
            {
                ThingDef solar = DefDatabase<ThingDef>.GetNamedSilentFail("SolarGenerator");
                if (solar != null && IsResearchedOrNoResearch(solar) && IsRegularGenerator(solar))
                {
                    RimWatchLogger.Debug($"BuildingSelector: Selected SolarGenerator (researched)");
                    return solar;
                }
            }

            // Wood-fired generator (no research needed)
            ThingDef woodGenerator = DefDatabase<ThingDef>.GetNamedSilentFail("WoodFiredGenerator");
            if (woodGenerator != null && IsResearchedOrNoResearch(woodGenerator) && IsRegularGenerator(woodGenerator))
            {
                RimWatchLogger.Debug($"BuildingSelector: Selected WoodFiredGenerator (basic option)");
                return woodGenerator;
            }

            // Chemfuel generator (if researched)
            if (electricityResearched)
            {
                ThingDef chemfuelGenerator = DefDatabase<ThingDef>.GetNamedSilentFail("ChemfuelPoweredGenerator");
                if (chemfuelGenerator != null && IsResearchedOrNoResearch(chemfuelGenerator) && IsRegularGenerator(chemfuelGenerator))
                {
                    RimWatchLogger.Debug($"BuildingSelector: Selected ChemfuelGenerator");
                    return chemfuelGenerator;
                }
            }

            return woodGenerator; // Fallback
        }
        
        /// <summary>
        /// v0.8.1: Check if generator is regular (not Ship/Spaceship part).
        /// Prevents placing endgame ship parts that require special research and resources.
        /// </summary>
        private static bool IsRegularGenerator(ThingDef def)
        {
            if (def == null) return false;
            
            string defName = def.defName.ToLower();
            string label = (def.label ?? "").ToLower();
            
            // BLACKLIST: Ship/Spaceship parts
            if (defName.Contains("ship") || label.Contains("ship")) return false;
            if (defName.Contains("reactor") || label.Contains("reactor")) return false;
            if (defName.Contains("gravicapacitor") || label.Contains("gravicapacitor")) return false;
            if (defName.Contains("vanometric") || label.Contains("vanometric")) return false;
            
            return true;
        }

        /// <summary>
        /// Selects appropriate storage type based on colony size and resources.
        /// </summary>
        public static string SelectStorageType(Map map, int colonistCount)
        {
            // Count available resources
            int woodCount = map.resourceCounter.GetCount(ThingDefOf.WoodLog);
            int steelCount = map.resourceCounter.GetCount(ThingDefOf.Steel);

            // Shelf costs: 50 wood OR 25 steel + 25 wood
            bool canAffordShelf = woodCount >= 50 || (steelCount >= 25 && woodCount >= 25);

            // Early game (0-3 colonists) → Stockpile Zone (free)
            if (colonistCount <= 3)
            {
                if (canAffordShelf)
                {
                    RimWatchLogger.Debug($"BuildingSelector: Selected Shelf (early game, resources available)");
                    return "Shelf";
                }
                else
                {
                    RimWatchLogger.Debug($"BuildingSelector: Selected StockpileZone (early game, no resources)");
                    return "StockpileZone";
                }
            }

            // Mid-late game → prefer Shelves (3x capacity)
            if (canAffordShelf)
            {
                RimWatchLogger.Debug($"BuildingSelector: Selected Shelf (resources available)");
                return "Shelf";
            }
            else
            {
                RimWatchLogger.Debug($"BuildingSelector: Selected StockpileZone (no resources for shelf)");
                return "StockpileZone";
            }
        }

        /// <summary>
        /// Checks if there's a power grid nearby (conduits or powered buildings).
        /// </summary>
        private static bool CheckPowerGridNearby(Map map, IntVec3 location, int radius)
        {
            foreach (IntVec3 nearbyCell in GenRadial.RadialCellsAround(location, radius, true))
            {
                if (!nearbyCell.InBounds(map)) continue;

                Building building = nearbyCell.GetFirstBuilding(map);
                if (building == null) continue;

                // Check for power conduits
                if (building.def.defName.Contains("PowerConduit"))
                {
                    return true;
                }

                // Check for powered buildings (generators, batteries, etc.)
                if (building.def.comps != null &&
                    building.def.comps.Any(c => c.compClass?.Name == "CompPowerTrader" ||
                                               c.compClass?.Name == "CompPower"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a building's research prerequisites are met.
        /// </summary>
        private static bool IsResearchedOrNoResearch(ThingDef thingDef)
        {
            if (thingDef == null) return false;

            if (thingDef.researchPrerequisites == null || thingDef.researchPrerequisites.Count == 0)
            {
                return true;
            }

            foreach (ResearchProjectDef research in thingDef.researchPrerequisites)
            {
                if (!research.IsFinished)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

