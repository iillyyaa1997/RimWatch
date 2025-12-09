using RimWatch.Core;
using RimWatch.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// Intelligent material selection with cost-benefit analysis, fire safety, and beauty considerations.
    /// v0.9.4: Enhanced with multi-criteria decision making.
    /// </summary>
    public static class StuffSelector
    {
        // Configuration Constants
        private const int MIN_MATERIAL_THRESHOLD = 50; // Minimum materials before considering
        private const float BEAUTY_WEIGHT = 0.3f; // Weight for beauty in scoring
        private const float FIRE_SAFETY_WEIGHT = 0.4f; // Weight for fire resistance
        private const float COST_WEIGHT = 0.3f; // Weight for material cost
        private const float FLAMMABILITY_THRESHOLD = 0.5f; // Max acceptable flammability
        
        // Material properties cache
        private static Dictionary<ThingDef, MaterialProperties> _materialCache = new Dictionary<ThingDef, MaterialProperties>();
        /// <summary>
        /// Returns best available stuff for building with intelligent multi-criteria analysis.
        /// ✅ SMART: Multi-criteria scoring (beauty, fire safety, cost)
        /// ✅ ADAPTIVE: Context-aware (bedrooms prefer beauty, kitchens avoid flammable)
        /// ✅ SAFE: Fire-resistant materials for high-risk areas
        /// </summary>
        public static ThingDef? DefaultNonSteelStuffFor(ThingDef forDef, Map map)
        {
            return DefaultNonSteelStuffFor(forDef, map, BuildingContext.General);
        }

        /// <summary>
        /// Returns best stuff with context-specific preferences.
        /// </summary>
        public static ThingDef? DefaultNonSteelStuffFor(ThingDef forDef, Map map, BuildingContext context)
        {
            if (forDef == null || !forDef.MadeFromStuff) return null;
            if (map == null) return null;

            // Collect available materials with their properties
            var materialCandidates = new Dictionary<ThingDef, MaterialScore>();
            
            // Collect wood
            ThingDef? wood = DefDatabase<ThingDef>.GetNamedSilentFail("WoodLog");
            if (wood != null && IsStuffAllowed(forDef, wood))
            {
                int woodCount = map.resourceCounter.GetCount(wood);
                if (woodCount >= MIN_MATERIAL_THRESHOLD)
                {
                    var props = GetMaterialProperties(wood);
                    var score = CalculateMaterialScore(props, woodCount, context);
                    materialCandidates[wood] = score;
                }
            }

            // Collect stone blocks
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
                    
                    // Consider chunks as potential blocks
                    string chunkName = stoneName.Replace("Blocks", "Chunk");
                    ThingDef? chunk = DefDatabase<ThingDef>.GetNamedSilentFail(chunkName);
                    if (chunk != null)
                    {
                        int chunkCount = map.resourceCounter.GetCount(chunk);
                        blockCount += chunkCount * 20; // Each chunk → ~20 blocks
                    }
                    
                    if (blockCount >= MIN_MATERIAL_THRESHOLD)
                    {
                        var props = GetMaterialProperties(stone);
                        var score = CalculateMaterialScore(props, blockCount, context);
                        materialCandidates[stone] = score;
                    }
                }
            }

            // Select best material based on context-weighted scoring
            if (materialCandidates.Count > 0)
            {
                var bestMaterial = materialCandidates.OrderByDescending(kvp => kvp.Value.TotalScore).First();
                RimWatchLogger.Info($"✅ StuffSelector: Using {bestMaterial.Key.label} for {forDef.label} " +
                    $"(score: {bestMaterial.Value.TotalScore:F2}, beauty: {bestMaterial.Value.BeautyScore:F2}, " +
                    $"safety: {bestMaterial.Value.SafetyScore:F2}, cost: {bestMaterial.Value.CostScore:F2})");
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

        /// <summary>
        /// Get or calculate material properties.
        /// </summary>
        private static MaterialProperties GetMaterialProperties(ThingDef material)
        {
            if (_materialCache.TryGetValue(material, out MaterialProperties cached))
            {
                return cached;
            }

            // Calculate stats manually for materials
            float flammability = 0f;
            if (material.stuffProps != null)
            {
                flammability = material.stuffProps.statFactors?.FirstOrDefault(f => f.stat == RimWorld.StatDefOf.Flammability)?.value ?? 1.0f;
            }

            float beauty = material.stuffProps?.statFactors?.FirstOrDefault(f => f.stat == RimWorld.StatDefOf.Beauty)?.value ?? 1.0f;
            float hitPoints = material.stuffProps?.statFactors?.FirstOrDefault(f => f.stat == RimWorld.StatDefOf.MaxHitPoints)?.value ?? 1.0f;
            float workToBuild = material.stuffProps?.statFactors?.FirstOrDefault(f => f.stat == RimWorld.StatDefOf.WorkToBuild)?.value ?? 1.0f;

            var props = new MaterialProperties
            {
                Material = material,
                Flammability = flammability,
                Beauty = beauty,
                MarketValue = material.BaseMarketValue,
                HitPoints = hitPoints,
                WorkToBuild = workToBuild
            };

            _materialCache[material] = props;
            return props;
        }

        /// <summary>
        /// Calculate material score based on context.
        /// </summary>
        private static MaterialScore CalculateMaterialScore(MaterialProperties props, int availability, BuildingContext context)
        {
            float beautyScore = 0f;
            float safetyScore = 0f;
            float costScore = 0f;

            // Beauty scoring (0-1, higher is better)
            beautyScore = UnityEngine.Mathf.Max(0f, UnityEngine.Mathf.Min(1f, props.Beauty / 2f)); // Beauty factor typically 0-2

            // Fire safety scoring (0-1, higher is better = less flammable)
            safetyScore = 1f - UnityEngine.Mathf.Min(1f, props.Flammability);

            // Cost efficiency scoring (0-1, higher is better = more available and cheaper)
            float availabilityFactor = UnityEngine.Mathf.Min(1f, availability / 500f); // Normalize to 500 units
            float costFactor = 1f - UnityEngine.Mathf.Min(1f, props.MarketValue / 10f); // Lower market value = better
            costScore = (availabilityFactor + costFactor) / 2f;

            // Apply context-specific weights
            float beautyWeight = BEAUTY_WEIGHT;
            float safetyWeight = FIRE_SAFETY_WEIGHT;
            float costWeight = COST_WEIGHT;

            switch (context)
            {
                case BuildingContext.Bedroom:
                case BuildingContext.Recreation:
                    beautyWeight = 0.5f; // Beauty very important
                    safetyWeight = 0.2f;
                    costWeight = 0.3f;
                    break;

                case BuildingContext.Kitchen:
                case BuildingContext.Workshop:
                    beautyWeight = 0.1f;
                    safetyWeight = 0.6f; // Fire safety critical
                    costWeight = 0.3f;
                    
                    // Penalty for flammable materials in high-risk areas
                    if (props.Flammability > FLAMMABILITY_THRESHOLD)
                    {
                        safetyScore *= 0.3f; // Heavy penalty
                    }
                    break;

                case BuildingContext.Defense:
                    beautyWeight = 0.0f;
                    safetyWeight = 0.3f;
                    costWeight = 0.7f; // Cost and availability most important
                    break;

                case BuildingContext.Storage:
                    beautyWeight = 0.0f;
                    safetyWeight = 0.4f;
                    costWeight = 0.6f;
                    break;
            }

            float totalScore = (beautyScore * beautyWeight) + 
                              (safetyScore * safetyWeight) + 
                              (costScore * costWeight);

            return new MaterialScore
            {
                BeautyScore = beautyScore,
                SafetyScore = safetyScore,
                CostScore = costScore,
                TotalScore = totalScore
            };
        }
    }

    /// <summary>
    /// Material properties for intelligent selection.
    /// </summary>
    public class MaterialProperties
    {
        public ThingDef Material { get; set; } = null!;
        public float Flammability { get; set; }
        public float Beauty { get; set; }
        public float MarketValue { get; set; }
        public float HitPoints { get; set; }
        public float WorkToBuild { get; set; }
    }

    /// <summary>
    /// Material scoring result.
    /// </summary>
    public class MaterialScore
    {
        public float BeautyScore { get; set; }
        public float SafetyScore { get; set; }
        public float CostScore { get; set; }
        public float TotalScore { get; set; }
    }

    /// <summary>
    /// Building context for material selection.
    /// </summary>
    public enum BuildingContext
    {
        General,
        Bedroom,
        Kitchen,
        Workshop,
        Recreation,
        Defense,
        Storage
    }
}


