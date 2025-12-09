using RimWatch.Automation;
using RimWorld;
using Verse;

namespace RimWatch.AI.Storytellers
{
    /// <summary>
    /// ⚔️ Aggressive Conqueror (Агрессивный Завоеватель)
    /// Philosophy: Fast development, high risk, aggressive expansion.
    /// Perfect for experienced players seeking action and wealth.
    /// </summary>
    public class AggressiveStoryteller : AIStoryteller
    {
        public override string Name => "Aggressive Conqueror";
        public override string Icon => "⚔️";
        public override string Description =>
            "Fast development and aggressive expansion strategy.\n" +
            "• Offensive tactics and high risk tolerance\n" +
            "• Trade focus - production for profit\n" +
            "• Minimal defenses, maximum growth speed\n" +
            "• Aggressive trading and wealth accumulation\n" +
            "• Retreat threshold: 80% casualties (fight to the end!)\n" +
            "• Perfect for experienced action seekers";

        /// <summary>
        /// Aggressive work priority determination - expansion and profit first.
        /// </summary>
        public override int DetermineWorkPriority(WorkTypeDef workType, Pawn colonist, ColonyNeeds needs)
        {
            string defName = workType.defName.ToLower();

            // 1. HIGH: Construction - fast expansion is key
            if (defName.Contains("construct"))
            {
                // Aggressive builds fast and expands rapidly
                return 1; // Always highest priority for growth
            }

            // 2. HIGH: Research - technology advantage
            if (defName.Contains("research"))
            {
                // Fast tech progression for better equipment and production
                return 1; // Maximum priority for tech edge
            }

            // 3. HIGH: Production - trade goods and wealth generation
            if (IsProductionWork(workType))
            {
                // Aggressive focuses on crafting for profit
                // Art, drugs, fine apparel for trade
                return 2; // High priority for wealth
            }

            // 4. MEDIUM: Medical - acceptable risk
            if (defName.Contains("doctor"))
            {
                // Aggressive accepts medical risks for speed
                if (needs.MedicalUrgency >= 3)
                    return 1; // Only critical medical gets top priority
                return 3; // Standard priority otherwise
            }

            // 5. MEDIUM: Food - minimal reserves (1.5x only)
            if (defName.Contains("cook"))
            {
                // Aggressive maintains minimal food stocks
                if (needs.FoodUrgency >= 3)
                    return 2; // Priority when critically low
                return 3; // Otherwise standard
            }

            if (defName.Contains("hunt"))
            {
                // Hunting focus over farming (faster)
                if (needs.FoodUrgency >= 2)
                    return 2;
                return 3;
            }

            if (defName.Contains("plant") || defName.Contains("grow"))
            {
                // Less farming, more hunting
                if (needs.FoodUrgency >= 3)
                    return 3; // Only when critical
                return 4; // Low priority normally
            }

            // 6. LOW: Firefighting - manage but don't obsess
            if (defName.Contains("firefight"))
            {
                return 2; // Important but not paralyzing
            }

            // 7. HIGH: Mining and resources - for production
            if (defName.Contains("mining") || defName.Contains("stone"))
            {
                // Resources for crafting and trade goods
                return 2;
            }

            // 8. LOW: Hauling - efficiency over organization
            if (defName.Contains("haul"))
            {
                return 4; // Low priority - colonists will haul when needed
            }

            // 9. LOW: Cleaning - ignore until necessary
            if (defName.Contains("clean"))
            {
                return 4; // Lowest priority - aesthetics don't matter
            }

            // 10. MEDIUM: Warden and animals
            if (defName.Contains("warden"))
            {
                // Recruit valuable prisoners fast
                return 3;
            }

            if (defName.Contains("handle"))
            {
                // Train hauling and combat animals
                return 3;
            }

            // Default: medium priority
            return 3;
        }

        /// <summary>
        /// Aggressive storyteller behavior - bold and fast-paced.
        /// </summary>
        public override void Tick()
        {
            // Aggressive makes rapid decisions
            // Focuses on:
            // - Fast construction and expansion
            // - Immediate production for trade
            // - Offensive tactics in combat
            
            // Future: Could implement aggressive actions
            // - "Enemy spotted - prepare counter-attack"
            // - "Trader arrived - sell everything non-critical"
            // - "New tech researched - immediately build upgrade"
            
            base.Tick();
        }

        /// <summary>
        /// Personality traits for Aggressive Conqueror.
        /// </summary>
        public override StorytellerPersonality GetPersonality()
        {
            return new StorytellerPersonality
            {
                RiskTolerance = 0.9f,         // Very high risk tolerance
                BuildingSpeed = 0.9f,         // Fast, aggressive expansion
                TradeAggressiveness = 0.9f,   // Highly aggressive trading
                DefenseStyle = 0.9f,          // Offensive tactics
                ResearchPriority = 0.9f,      // High research for tech advantage
                SocialFocus = 0.3f            // Low social focus - mood is secondary
            };
        }

        /// <summary>
        /// v0.9.14: Crafting strategy for Aggressive - trade focus!
        /// </summary>
        public override CraftingStrategy GetCraftingStrategy()
        {
            return new CraftingStrategy
            {
                StockpileMultiplier = 1.0f,   // Minimal reserves
                TradeFocus = 0.9f,            // Heavy trade crafting
                MinimumQuality = "Poor",      // Speed over quality
                PrioritizeWeapons = false,    // No hoarding
                PrioritizeApparel = false
            };
        }

        /// <summary>
        /// Building priorities for Aggressive - expansion and trade first.
        /// </summary>
        public string[] GetBuildingPriorities()
        {
            return new string[]
            {
                "Workshops",       // Production for trade
                "Trade beacon",    // Enable orbital trading
                "Research",        // Fast tech progression
                "Stockpiles",      // For trade goods (near edge for caravans)
                "Bedrooms",        // Minimal comfort
                "Kitchen",         // Basic food only
                "Turrets",         // Minimal defense
                "Recreation",      // Basic mood management
                "Walls",           // Minimal fortification
                "Hospital"         // Last priority
            };
        }

        /// <summary>
        /// Retreat threshold for combat - 80% casualties.
        /// Aggressive fights to the bitter end for victory.
        /// </summary>
        public float GetRetreatThreshold()
        {
            return 0.8f; // Only retreat when 80% down - fight hard!
        }

        /// <summary>
        /// Stockpile multiplier - Aggressive wants minimal reserves (1.5x).
        /// </summary>
        public float GetStockpileMultiplier()
        {
            return 1.5f; // Minimal reserves - focus on growth
        }

        /// <summary>
        /// Quality minimum for crafting.
        /// Aggressive: Poor+ acceptable for speed.
        /// </summary>
        public string GetMinimumQuality(string itemType)
        {
            // Aggressive accepts lower quality for faster production
            return "Poor"; // Poor+ is fine - quantity and speed over quality
        }

        /// <summary>
        /// Trade strategy - sell everything non-critical.
        /// </summary>
        public bool ShouldSellItem(string itemType, int currentStock, int normalReserve)
        {
            // Aggressive sells excess aggressively
            // Keep only minimal reserves (1.5x normal)
            float threshold = normalReserve * 1.5f;
            
            // Sell if above threshold
            if (currentStock > threshold)
                return true;
            
            // Always keep some critical items
            string lower = itemType.ToLower();
            if (lower.Contains("medicine") && currentStock < 10)
                return false; // Keep minimum medicine
            
            if (lower.Contains("component") && currentStock < 5)
                return false; // Keep minimum components
            
            // Sell everything else
            return currentStock > threshold * 0.8f; // Sell if close to threshold
        }

        /// <summary>
        /// Production focus - items for trade.
        /// </summary>
        public string[] GetTradeProductionPriorities()
        {
            return new string[]
            {
                "Art",             // High value sculptures and art
                "Drugs",           // Profitable drug production
                "Fine apparel",    // Quality clothes for trade
                "Weapons",         // Weapons for profit
                "Furniture"        // Decorative items
            };
        }
    }
}

