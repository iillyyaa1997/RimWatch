using RimWatch.Automation;
using RimWorld;
using System;
using Verse;

namespace RimWatch.AI.Storytellers
{
    /// <summary>
    /// 🔀 Random Storyteller (Случайный Рассказчик)
    /// Philosophy: Changes style every day/week - maximum variety.
    /// Switches randomly between Cautious, Balanced, Aggressive, and Chaotic.
    /// </summary>
    public class RandomStoryteller : AIStoryteller
    {
        private Random _random = new Random();
        private AIStoryteller _currentStoryteller;
        private int _lastSwitchTick = 0;
        private int _switchInterval = 180000; // 3 days default (180k ticks = 3 days)
        private int _daysUntilSwitch = 3;
        
        // Available storytellers to switch between
        private BalancedStoryteller _balanced = new BalancedStoryteller();
        private CautiousStoryteller _cautious = new CautiousStoryteller();
        private AggressiveStoryteller _aggressive = new AggressiveStoryteller();
        private ChaoticStoryteller _chaotic = new ChaoticStoryteller();
        
        public override string Name => "Random Storyteller";
        public override string Icon => "🔀";
        public override string Description =>
            "Unpredictable style switching for maximum variety.\n" +
            "• Randomly switches between all storyteller types\n" +
            "• Changes every 1-7 days (random duration)\n" +
            "• Experience all playstyles in one game\n" +
            "• Never know what's coming next\n" +
            "• Perfect for variety and replayability";

        /// <summary>
        /// Constructor - start with random storyteller.
        /// </summary>
        public RandomStoryteller()
        {
            // Start with random storyteller
            SwitchToRandomStoryteller();
        }

        /// <summary>
        /// Work priority determined by current active storyteller.
        /// </summary>
        public override int DetermineWorkPriority(WorkTypeDef workType, Pawn colonist, ColonyNeeds needs)
        {
            // Delegate to current storyteller
            if (_currentStoryteller == null)
                SwitchToRandomStoryteller();
            
            return _currentStoryteller.DetermineWorkPriority(workType, colonist, needs);
        }

        /// <summary>
        /// Random storyteller behavior - periodically switch styles.
        /// </summary>
        public override void Tick()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // Check if it's time to switch
            if (currentTick - _lastSwitchTick >= _switchInterval)
            {
                SwitchToRandomStoryteller();
                _lastSwitchTick = currentTick;
                
                // Randomize next switch interval (1-7 days)
                int daysToNextSwitch = _random.Next(1, 8); // 1-7 days
                _switchInterval = daysToNextSwitch * 60000; // 60k ticks per day
                _daysUntilSwitch = daysToNextSwitch;
                
                Utils.RimWatchLogger.Info($"RandomStoryteller: Switched to {_currentStoryteller.GetFullName()}. " +
                    $"Next switch in {daysToNextSwitch} day(s).");
            }
            
            // Delegate tick to current storyteller
            _currentStoryteller?.Tick();
            
            base.Tick();
        }

        /// <summary>
        /// Switch to a random storyteller from available options.
        /// </summary>
        private void SwitchToRandomStoryteller()
        {
            AIStoryteller[] storytellers = new AIStoryteller[]
            {
                _balanced,
                _cautious,
                _aggressive,
                _chaotic
            };
            
            // Pick random storyteller (but not the same as current)
            AIStoryteller newStoryteller;
            int attempts = 0;
            do
            {
                int index = _random.Next(storytellers.Length);
                newStoryteller = storytellers[index];
                attempts++;
                
                // After 10 attempts, just take any
                if (attempts > 10)
                    break;
            }
            while (newStoryteller == _currentStoryteller && storytellers.Length > 1);
            
            // Switch!
            AIStoryteller oldStoryteller = _currentStoryteller;
            _currentStoryteller = newStoryteller;
            
            // Log transition
            if (oldStoryteller != null)
            {
                Utils.RimWatchLogger.Info($"RandomStoryteller: Transition {oldStoryteller.GetFullName()} → {_currentStoryteller.GetFullName()}");
            }
            else
            {
                Utils.RimWatchLogger.Info($"RandomStoryteller: Initial storyteller set to {_currentStoryteller.GetFullName()}");
            }
        }

        /// <summary>
        /// Get current active storyteller.
        /// </summary>
        public AIStoryteller GetCurrentStoryteller()
        {
            if (_currentStoryteller == null)
                SwitchToRandomStoryteller();
            
            return _currentStoryteller;
        }

        /// <summary>
        /// Get days until next switch.
        /// </summary>
        public int GetDaysUntilNextSwitch()
        {
            int currentTick = Find.TickManager.TicksGame;
            int ticksRemaining = _switchInterval - (currentTick - _lastSwitchTick);
            int daysRemaining = Math.Max(0, ticksRemaining / 60000);
            return daysRemaining;
        }

        /// <summary>
        /// Get hours until next switch.
        /// </summary>
        public float GetHoursUntilNextSwitch()
        {
            int currentTick = Find.TickManager.TicksGame;
            int ticksRemaining = _switchInterval - (currentTick - _lastSwitchTick);
            float hoursRemaining = Math.Max(0f, ticksRemaining / 2500f); // 2500 ticks per hour
            return hoursRemaining;
        }

        /// <summary>
        /// Personality traits - aggregate of current storyteller.
        /// </summary>
        public override StorytellerPersonality GetPersonality()
        {
            // Return personality of current active storyteller
            if (_currentStoryteller is CautiousStoryteller cautious)
                return cautious.GetPersonality();
            
            if (_currentStoryteller is AggressiveStoryteller aggressive)
                return aggressive.GetPersonality();
            
            if (_currentStoryteller is ChaoticStoryteller chaotic)
                return chaotic.GetPersonality();
            
            if (_currentStoryteller is BalancedStoryteller balanced)
                return balanced.GetPersonality();
            
            // Default balanced personality
            return new StorytellerPersonality
            {
                RiskTolerance = 0.5f,
                BuildingSpeed = 0.5f,
                TradeAggressiveness = 0.5f,
                DefenseStyle = 0.5f,
                ResearchPriority = 0.5f,
                SocialFocus = 0.5f
            };
        }

        /// <summary>
        /// v0.9.14: Crafting strategy - delegates to current storyteller.
        /// </summary>
        public override CraftingStrategy GetCraftingStrategy()
        {
            // Delegate to current storyteller
            if (_currentStoryteller != null)
                return _currentStoryteller.GetCraftingStrategy();
            
            // Default balanced strategy
            return new CraftingStrategy
            {
                StockpileMultiplier = 1.5f,
                TradeFocus = 0.3f,
                MinimumQuality = "Normal",
                PrioritizeWeapons = false,
                PrioritizeApparel = false
            };
        }

        /// <summary>
        /// Get status string for UI display.
        /// </summary>
        public string GetStatusString()
        {
            AIStoryteller current = GetCurrentStoryteller();
            float hoursRemaining = GetHoursUntilNextSwitch();
            
            if (hoursRemaining < 1f)
            {
                return $"Currently: {current.GetFullName()} (switching soon!)";
            }
            else if (hoursRemaining < 24f)
            {
                return $"Currently: {current.GetFullName()} ({hoursRemaining:F1}h remaining)";
            }
            else
            {
                int daysRemaining = GetDaysUntilNextSwitch();
                return $"Currently: {current.GetFullName()} ({daysRemaining}d remaining)";
            }
        }

        /// <summary>
        /// Force immediate switch (for testing or user request).
        /// </summary>
        public void ForceSwitchNow()
        {
            SwitchToRandomStoryteller();
            _lastSwitchTick = Find.TickManager.TicksGame;
            
            // Randomize next interval
            int daysToNextSwitch = _random.Next(1, 8);
            _switchInterval = daysToNextSwitch * 60000;
            _daysUntilSwitch = daysToNextSwitch;
        }
    }
}

