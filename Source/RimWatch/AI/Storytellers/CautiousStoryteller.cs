using RimWatch.Automation;
using RimWorld;
using Verse;

namespace RimWatch.AI.Storytellers
{
    /// <summary>
    /// 🛡️ Cautious Strategist (Осторожный Стратег)
    /// Philosophy: Minimum risk, maximum planning and stability.
    /// Perfect for hardcore scenarios and survival focus.
    /// </summary>
    public class CautiousStoryteller : AIStoryteller
    {
        public override string Name => "Cautious Strategist";
        public override string Icon => "🛡️";
        public override string Description =>
            "Minimum risk, maximum planning strategy.\n" +
            "• Defensive tactics and safety first\n" +
            "• Stockpile focus - 3x normal reserves\n" +
            "• Heavy fortifications and redundancy\n" +
            "• Conservative trade and resource management\n" +
            "• Retreat threshold: 50% casualties\n" +
            "• Perfect for hardcore survival";

        /// <summary>
        /// Cautious work priority determination - safety and survival first.
        /// </summary>
        public override int DetermineWorkPriority(WorkTypeDef workType, Pawn colonist, ColonyNeeds needs)
        {
            string defName = workType.defName.ToLower();

            // 1. CRITICAL: Doctor and Rescue - absolute highest priority
            if (defName.Contains("doctor"))
            {
                return 1; // Always maximum priority for medical
            }

            // 2. CRITICAL: Firefighting - immediate danger
            if (defName.Contains("firefight"))
            {
                return 1; // Fires are existential threats
            }

            // 3. CRITICAL: Defense and Warden - protect the colony
            if (defName.Contains("warden"))
            {
                return 1; // Cautious keeps tight control of prisoners
            }

            // 4. HIGH: Food production - stockpile focus (3x reserves)
            if (defName.Contains("cook"))
            {
                // Cautious wants abundant food reserves
                if (needs.FoodUrgency >= 2)
                    return 1; // High priority when food running low
                return 2; // Always keep cooking
            }

            if (defName.Contains("hunt") || defName.Contains("plant") || defName.Contains("grow"))
            {
                // Agriculture and hunting for food security
                if (needs.FoodUrgency >= 2)
                    return 1;
                return 2; // Maintain food production constantly
            }

            // 5. HIGH: Construction - defensive structures priority
            if (defName.Contains("construct"))
            {
                // Cautious prioritizes walls, defenses, and safety structures
                if (needs.ConstructionUrgency >= 2)
                    return 1; // Defensive buildings are critical
                return 2; // Always building defenses
            }

            // 6. MEDIUM: Crafting and Production - quality over speed
            if (IsProductionWork(workType))
            {
                // Cautious wants quality items (Normal+ apparel, Good+ weapons)
                // But not at the expense of safety
                if (needs.FoodUrgency >= 3 || needs.MedicalUrgency >= 3)
                    return 4; // Drop production during emergencies
                return 3; // Standard priority
            }

            // 7. MEDIUM: Research - steady progress
            if (defName.Contains("research"))
            {
                // Cautious researches steadily but carefully
                if (needs.ResearchUrgency >= 2)
                    return 2; // Important for long-term survival
                return 3; // Standard priority
            }

            // 8. LOW: Hauling and Cleaning - efficiency over cleanliness
            if (defName.Contains("haul"))
            {
                // Cautious wants organized stockpiles close to base
                return 3; // Important for logistics
            }

            if (defName.Contains("clean"))
            {
                return 4; // Lowest priority - survival > aesthetics
            }

            // 9. MEDIUM: Mining and stonecutting - resource gathering
            if (defName.Contains("mining") || defName.Contains("stone"))
            {
                // Resources for defensive structures
                return 3;
            }

            // 10. Handle animals - food security and defense animals
            if (defName.Contains("handle"))
            {
                // Cautious trains defensive and pack animals
                return 3;
            }

            // Default: medium priority
            return 3;
        }

        /// <summary>
        /// Cautious storyteller behavior - conservative and methodical.
        /// </summary>
        public override void Tick()
        {
            // Cautious doesn't make sudden changes
            // All decisions are slow, measured, and planned
            
            // Future: Could implement proactive warnings
            // - "Food stockpile below 3x target - increase farming"
            // - "Defense rating low - build more turrets"
            // - "Detected threat - prepare defenses"
            
            base.Tick();
        }

        /// <summary>
        /// Personality traits for Cautious Strategist.
        /// </summary>
        public override StorytellerPersonality GetPersonality()
        {
            return new StorytellerPersonality
            {
                RiskTolerance = 0.2f,         // Very low risk tolerance
                BuildingSpeed = 0.3f,         // Slow, methodical building
                TradeAggressiveness = 0.1f,   // Very conservative trading
                DefenseStyle = 0.1f,          // Purely defensive
                ResearchPriority = 0.6f,      // Moderate research for safety tech
                SocialFocus = 0.7f            // High social focus to prevent breaks
            };
        }

        /// <summary>
        /// v0.9.14: Crafting strategy for Cautious - stockpile everything!
        /// </summary>
        public override CraftingStrategy GetCraftingStrategy()
        {
            return new CraftingStrategy
            {
                StockpileMultiplier = 3.0f,   // Triple reserves for safety
                TradeFocus = 0.0f,            // No trade crafting
                MinimumQuality = "Normal",    // Normal quality minimum
                PrioritizeWeapons = true,     // Always have backup weapons
                PrioritizeApparel = true      // Always have backup clothes
            };
        }

        /// <summary>
        /// Building priorities for Cautious - defense and safety first.
        /// </summary>
        public string[] GetBuildingPriorities()
        {
            return new string[]
            {
                "Walls",           // Defensive perimeter
                "Turrets",         // Automated defense
                "Stockpiles",      // Resource security (close to base)
                "Bedrooms",        // Safety and rest
                "Kitchen",         // Food security
                "Hospital",        // Medical safety
                "Freezer",         // Food preservation (3x reserves)
                "Workshop",        // Production for essentials
                "Research",        // Defensive tech
                "Recreation"       // Mood management (prevent breaks)
            };
        }

        /// <summary>
        /// Retreat threshold for combat - 50% casualties.
        /// Cautious prioritizes colonist lives over victory.
        /// </summary>
        public float GetRetreatThreshold()
        {
            return 0.5f; // Retreat when 50% down/injured
        }

        /// <summary>
        /// Stockpile multiplier - Cautious wants 3x normal reserves.
        /// </summary>
        public float GetStockpileMultiplier()
        {
            return 3.0f; // Triple reserves for safety
        }

        /// <summary>
        /// Quality minimum for crafting.
        /// Cautious: Normal+ for apparel, Good+ for weapons.
        /// </summary>
        public string GetMinimumQuality(string itemType)
        {
            if (itemType.ToLower().Contains("weapon"))
                return "Good"; // Good+ weapons for reliability
            
            if (itemType.ToLower().Contains("apparel") || itemType.ToLower().Contains("armor"))
                return "Normal"; // Normal+ clothing for durability
            
            return "Poor"; // Acceptable for non-critical items
        }
    }

    /// <summary>
    /// v0.9.14: Crafting strategy framework for storytellers.
    /// Defines how much to craft and what to prioritize.
    /// </summary>
    public class CraftingStrategy
    {
        /// <summary>
        /// Stockpile multiplier (1.0 = normal, 2.0 = double reserves).
        /// How much extra inventory to maintain.
        /// </summary>
        public float StockpileMultiplier { get; set; } = 1.0f;
        
        /// <summary>
        /// Trade focus (0.0-1.0). Higher = craft more for selling.
        /// </summary>
        public float TradeFocus { get; set; } = 0.0f;
        
        /// <summary>
        /// Quality target for crafted items.
        /// "Poor", "Normal", "Good", "Excellent", "Masterwork", "Legendary"
        /// </summary>
        public string MinimumQuality { get; set; } = "Normal";
        
        /// <summary>
        /// Whether to prioritize weapon crafting.
        /// </summary>
        public bool PrioritizeWeapons { get; set; } = false;
        
        /// <summary>
        /// Whether to prioritize apparel crafting.
        /// </summary>
        public bool PrioritizeApparel { get; set; } = false;
    }
    
    /// <summary>
    /// Personality framework for storytellers.
    /// Defines behavioral parameters on 0.0-1.0 scale.
    /// </summary>
    public class StorytellerPersonality
    {
        /// <summary>
        /// Risk tolerance: 0.0 (cautious) to 1.0 (aggressive).
        /// Affects decision making in combat, trade, expansion.
        /// </summary>
        public float RiskTolerance { get; set; }
        
        /// <summary>
        /// Building speed: 0.0 (slow methodical) to 1.0 (fast expansion).
        /// Affects construction priorities and base layout.
        /// </summary>
        public float BuildingSpeed { get; set; }
        
        /// <summary>
        /// Trade aggressiveness: 0.0 (conservative) to 1.0 (aggressive).
        /// Affects buying/selling decisions and caravan formation.
        /// </summary>
        public float TradeAggressiveness { get; set; }
        
        /// <summary>
        /// Defense style: 0.0 (passive defensive) to 1.0 (offensive).
        /// Affects combat tactics and positioning.
        /// </summary>
        public float DefenseStyle { get; set; }
        
        /// <summary>
        /// Research priority: 0.0 (low) to 1.0 (high).
        /// Affects work priority for research tasks.
        /// </summary>
        public float ResearchPriority { get; set; }
        
        /// <summary>
        /// Social focus: 0.0 (ignore mood) to 1.0 (constant mood management).
        /// Affects recreation, social events, mood crisis prevention.
        /// </summary>
        public float SocialFocus { get; set; }
    }
}

