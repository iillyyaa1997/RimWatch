using RimWatch.Automation;
using RimWorld;
using System;
using Verse;

namespace RimWatch.AI.Storytellers
{
    /// <summary>
    /// 🎲 Chaotic Experimenter (Хаотичный Экспериментатор)
    /// Philosophy: Unpredictability, experimentation, "what if...?"
    /// Perfect for players seeking chaos and unexpected outcomes.
    /// </summary>
    public class ChaoticStoryteller : AIStoryteller
    {
        private Random _random = new Random();
        private int _lastPersonalityChangeTick = 0;
        private const int PersonalityChangeInterval = 60000; // Change every day (60k ticks = 1 day)
        
        // Current chaotic personality settings
        private float _currentRiskTolerance = 0.5f;
        private float _currentBuildingSpeed = 0.5f;
        private float _currentTradeAggressiveness = 0.5f;
        
        public override string Name => "Chaotic Experimenter";
        public override string Icon => "🎲";
        public override string Description =>
            "Unpredictable and experimental strategy.\n" +
            "• Random priority changes every day\n" +
            "• Exotic crop and animal preferences\n" +
            "• Crazy tactics and unusual base layouts\n" +
            "• Risky trading and wild experiments\n" +
            "• \"What if...?\" philosophy\n" +
            "• Perfect for chaos and fun!";

        /// <summary>
        /// Chaotic work priority - changes randomly!
        /// </summary>
        public override int DetermineWorkPriority(WorkTypeDef workType, Pawn colonist, ColonyNeeds needs)
        {
            string defName = workType.defName.ToLower();

            // ALWAYS keep critical work at reasonable priority
            if (defName.Contains("doctor") && needs.MedicalUrgency >= 3)
                return 1; // Don't let colonists die from chaos!
            
            if (defName.Contains("firefight"))
                return 1; // Don't let the base burn down!

            // For everything else - CHAOS!
            // Roll dice for priority
            int basePriority = _random.Next(1, 5); // Random 1-4

            // But apply SOME logic based on urgency
            if (defName.Contains("cook") && needs.FoodUrgency >= 3)
                return Math.Max(1, basePriority - 1); // Boost food when starving
            
            if (defName.Contains("construct") && needs.ConstructionUrgency >= 3)
                return Math.Max(1, basePriority - 1); // Boost construction when critical

            // Pure chaos for non-critical work
            return basePriority;
        }

        /// <summary>
        /// Chaotic storyteller behavior - random changes and experiments.
        /// </summary>
        public override void Tick()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // Change personality randomly every day
            if (currentTick - _lastPersonalityChangeTick >= PersonalityChangeInterval)
            {
                _lastPersonalityChangeTick = currentTick;
                RandomizePersonality();
            }
            
            base.Tick();
        }

        /// <summary>
        /// Randomize personality traits - chaos incarnate!
        /// </summary>
        private void RandomizePersonality()
        {
            _currentRiskTolerance = (float)(_random.NextDouble()); // 0.0 - 1.0
            _currentBuildingSpeed = (float)(_random.NextDouble());
            _currentTradeAggressiveness = (float)(_random.NextDouble());
            
            Utils.RimWatchLogger.Info($"ChaoticStoryteller: Personality changed! Risk={_currentRiskTolerance:F2}, " +
                $"BuildSpeed={_currentBuildingSpeed:F2}, Trade={_currentTradeAggressiveness:F2}");
        }

        /// <summary>
        /// Personality traits for Chaotic Experimenter - constantly changing!
        /// </summary>
        public override StorytellerPersonality GetPersonality()
        {
            return new StorytellerPersonality
            {
                RiskTolerance = _currentRiskTolerance,
                BuildingSpeed = _currentBuildingSpeed,
                TradeAggressiveness = _currentTradeAggressiveness,
                DefenseStyle = (float)(_random.NextDouble()),      // Random defense!
                ResearchPriority = (float)(_random.NextDouble()),  // Random research!
                SocialFocus = (float)(_random.NextDouble())        // Random social!
            };
        }

        /// <summary>
        /// v0.9.14: Crafting strategy for Chaotic - COMPLETELY RANDOM!
        /// </summary>
        public override CraftingStrategy GetCraftingStrategy()
        {
            string[] qualities = { "Awful", "Poor", "Normal", "Good", "Excellent" };
            return new CraftingStrategy
            {
                StockpileMultiplier = 1.0f + (float)(_random.NextDouble() * 3.0f), // 1.0x-4.0x random!
                TradeFocus = (float)(_random.NextDouble()),                          // Random trade focus!
                MinimumQuality = qualities[_random.Next(qualities.Length)],         // Random quality!
                PrioritizeWeapons = _random.NextDouble() > 0.5,                     // Random!
                PrioritizeApparel = _random.NextDouble() > 0.5                      // Random!
            };
        }

        /// <summary>
        /// Building priorities for Chaotic - RANDOM ORDER!
        /// </summary>
        public string[] GetBuildingPriorities()
        {
            // Base list
            string[] buildings = new string[]
            {
                "Bedrooms", "Kitchen", "Workshop", "Research", "Hospital",
                "Walls", "Turrets", "Stockpiles", "Recreation", "Freezer"
            };
            
            // SHUFFLE IT!
            ShuffleArray(buildings);
            
            return buildings;
        }

        /// <summary>
        /// Shuffle array - Fisher-Yates algorithm.
        /// </summary>
        private void ShuffleArray<T>(T[] array)
        {
            int n = array.Length;
            for (int i = n - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                T temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }
        }

        /// <summary>
        /// Retreat threshold for combat - RANDOM!
        /// </summary>
        public float GetRetreatThreshold()
        {
            // Random between 0.3 and 0.9
            return 0.3f + (float)(_random.NextDouble() * 0.6);
        }

        /// <summary>
        /// Stockpile multiplier - RANDOM!
        /// </summary>
        public float GetStockpileMultiplier()
        {
            // Random between 1.0x and 4.0x
            return 1.0f + (float)(_random.NextDouble() * 3.0);
        }

        /// <summary>
        /// Quality minimum for crafting - RANDOM!
        /// </summary>
        public string GetMinimumQuality(string itemType)
        {
            string[] qualities = { "Awful", "Poor", "Normal", "Good", "Excellent" };
            return qualities[_random.Next(qualities.Length)];
        }

        /// <summary>
        /// Should tame this animal? - CHAOS LOGIC!
        /// </summary>
        public bool ShouldTameAnimal(Pawn animal)
        {
            // Chaotic storyteller: Completely random animal taming decisions
            // Wildness check disabled for RimWorld 1.6 compatibility
            return _random.NextDouble() > 0.3; // 70% chance to attempt taming (chaotic!)
        }

        /// <summary>
        /// Crop selection - RANDOM exotic crops preferred!
        /// </summary>
        public string GetPreferredCrop()
        {
            // Chaotic prefers unusual crops
            string[] exoticCrops = 
            {
                "Devilstrand", "Smokeleaf", "Psychoid", "Ambrosia",
                "Corn", "Rice", "Potatoes", "Healroot"
            };
            
            return exoticCrops[_random.Next(exoticCrops.Length)];
        }

        /// <summary>
        /// Building layout style - CHAOS!
        /// </summary>
        public string GetBuildingLayoutStyle()
        {
            string[] styles = 
            {
                "Circular rooms",
                "Zigzag corridors",
                "Random scattered buildings",
                "Labyrinth design",
                "Honeycomb pattern",
                "Spiral layout",
                "Asymmetric chaos"
            };
            
            return styles[_random.Next(styles.Length)];
        }

        /// <summary>
        /// Combat tactics - INSANE!
        /// </summary>
        public string GetCombatTactic()
        {
            string[] tactics = 
            {
                "All melee charge",
                "Psychic powers focus",
                "Explosive spam",
                "Random positioning",
                "Berserk rage mode",
                "Retreat and ambush",
                "Kamikaze attack"
            };
            
            return tactics[_random.Next(tactics.Length)];
        }

        /// <summary>
        /// Should buy this item from trader? - GAMBLE!
        /// </summary>
        public bool ShouldBuyItem(string itemName, float price, float silver)
        {
            // Chaotic sometimes buys random expensive stuff
            // or ignores useful cheap items
            
            // Roll the dice!
            double buyChance = _random.NextDouble();
            
            // Higher price = MORE likely to buy (chaos loves expensive risks!)
            if (price > 1000)
                buyChance += 0.3; // +30% chance for expensive items
            
            // Can afford?
            if (silver < price)
                return false; // Even chaos has limits
            
            return buyChance > 0.5; // 50% base chance + modifiers
        }
    }
}

