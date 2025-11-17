using RimWatch.Utils;
using System.Linq;
using Verse;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// Selects building stuff materials with the policy: 
    /// 1. Wood (if available on map)
    /// 2. Common stone blocks (if available on map)
    /// 3. Any other non-steel stuff (if available)
    /// 4. Never use rare/exotic materials
    /// </summary>
    public static class StuffSelector
    {
        /// <summary>
        /// Returns best available stuff for building based on ACTUAL map availability.
        /// ✅ SMART: Chooses the MOST ABUNDANT material (wood, stone blocks, chunks)
        /// ✅ ADAPTIVE: If little wood → uses stone, if little blocks → uses chunks
        /// </summary>
        public static ThingDef? DefaultNonSteelStuffFor(ThingDef forDef, Map map)
        {
            if (forDef == null || !forDef.MadeFromStuff) return null;
            if (map == null) return null;

            // ✅ SMART SELECTION: Count ALL available materials and pick the most abundant
            var materialCounts = new System.Collections.Generic.Dictionary<ThingDef, int>();
            
            // Check wood availability
            ThingDef? wood = DefDatabase<ThingDef>.GetNamedSilentFail("WoodLog");
            if (wood != null && IsStuffAllowed(forDef, wood))
            {
                int woodCount = map.resourceCounter.GetCount(wood);
                if (woodCount > 0)
                {
                    materialCounts[wood] = woodCount;
                }
            }

            // Check stone blocks availability
            string[] commonStones = new[]
            {
                "BlocksGranite",
                "BlocksLimestone", 
                "BlocksSlate",
                "BlocksMarble",
                "BlocksSandstone"
            };
            
            foreach (string stoneName in commonStones)
            {
                ThingDef? stone = DefDatabase<ThingDef>.GetNamedSilentFail(stoneName);
                if (stone != null && IsStuffAllowed(forDef, stone))
                {
                    int blockCount = map.resourceCounter.GetCount(stone);
                    
                    // ✅ SMART: If blocks are low, check for chunks that can be processed
                    if (blockCount < 20)
                    {
                        string chunkName = stoneName.Replace("Blocks", "Chunk");
                        ThingDef? chunk = DefDatabase<ThingDef>.GetNamedSilentFail(chunkName);
                        if (chunk != null)
                        {
                            int chunkCount = map.resourceCounter.GetCount(chunk);
                            // Each chunk → ~20 blocks
                            blockCount += chunkCount * 20;
                        }
                    }
                    
                    if (blockCount > 0)
                    {
                        materialCounts[stone] = blockCount;
                    }
                }
            }

            // ✅ Select the MOST ABUNDANT material
            if (materialCounts.Count > 0)
            {
                var bestMaterial = materialCounts.OrderByDescending(kvp => kvp.Value).First();
                RimWatchLogger.Info($"✅ StuffSelector: Using {bestMaterial.Key.label} for {forDef.label} (available: {bestMaterial.Value})");
                return bestMaterial.Key;
            }

            // Fallback: Wood even if not on map (colonists will gather it)
            if (wood != null && IsStuffAllowed(forDef, wood))
            {
                RimWatchLogger.Warning($"StuffSelector: No materials stockpiled, using Wood as fallback for {forDef.label} (colonists will gather)");
                return wood;
            }

            RimWatchLogger.Warning($"StuffSelector: No suitable stuff found for {forDef.label}");
            return null;
        }

        /// <summary>
        /// Checks if stuff is allowed for building.
        /// </summary>
        private static bool IsStuffAllowed(ThingDef forDef, ThingDef stuff)
        {
            if (!stuff.IsStuff || forDef.stuffCategories == null || stuff.stuffProps?.categories == null)
                return false;
            
            // Explicitly reject steel
            if (stuff.defName == "Steel")
                return false;
            
            return forDef.stuffCategories.Any(cat => stuff.stuffProps.categories.Contains(cat));
        }

        /// <summary>
        /// Checks if stuff is available on the map (stockpiled or mineable nearby).
        /// </summary>
        private static bool IsStuffAvailable(Map map, ThingDef stuff)
        {
            // Check stockpiles for materials
            int available = map.resourceCounter.GetCount(stuff);
            
            if (available > 0)
                return true;

            // For stone blocks - check if corresponding stone chunks exist
            if (stuff.defName.StartsWith("Blocks"))
            {
                // BlocksGranite -> ChunkGranite
                string chunkName = stuff.defName.Replace("Blocks", "Chunk");
                ThingDef? chunk = DefDatabase<ThingDef>.GetNamedSilentFail(chunkName);
                
                if (chunk != null)
                {
                    int chunks = map.resourceCounter.GetCount(chunk);
                    if (chunks > 0)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if stuff is rare/exotic and should be avoided.
        /// </summary>
        private static bool IsRareOrExoticStuff(ThingDef stuff)
        {
            // Rare stones
            string[] rareStones = new[]
            {
                "BlocksJade",
                "BlocksBioferrite",  // Anomaly DLC - rare
                "BlocksLimescale"    // Non-standard
            };

            if (rareStones.Contains(stuff.defName))
                return true;

            // Precious materials
            string[] precious = new[]
            {
                "Gold",
                "Silver",
                "Jade",
                "Uranium",
                "Plasteel"
            };

            if (precious.Contains(stuff.defName))
                return true;

            return false;
        }

        /// <summary>
        /// Scores stuff for preference (higher = better).
        /// </summary>
        private static int GetStuffScore(ThingDef stuff)
        {
            // Wood = best
            if (stuff.defName == "WoodLog")
                return 100;

            // Common stone blocks = good
            string[] commonStones = new[] { "BlocksGranite", "BlocksLimestone", "BlocksSlate", "BlocksMarble", "BlocksSandstone" };
            if (commonStones.Contains(stuff.defName))
                return 50;

            // Everything else = acceptable
            return 10;
        }
    }
}


