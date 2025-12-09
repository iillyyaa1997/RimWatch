using RimWatch.Automation;
using RimWorld;
using Verse;

namespace RimWatch.AI.Storytellers
{
    /// <summary>
    /// 🎨 Custom Storyteller (Кастомный Рассказчик)
    /// Philosophy: User-defined - complete control over personality.
    /// Perfect for creating unique playstyles and sharing profiles.
    /// </summary>
    public class CustomStoryteller : AIStoryteller
    {
        // User-configurable personality traits (0.0 - 1.0)
        private StorytellerPersonality _personality;
        
        // Profile name for saving/loading
        public string ProfileName { get; set; } = "My Custom Storyteller";
        
        public override string Name => ProfileName;
        public override string Icon => "🎨";
        public override string Description =>
            "Fully customizable storyteller personality.\n" +
            "• Adjust all personality sliders to your preference\n" +
            "• Save and load custom profiles\n" +
            "• Export/import profiles for community sharing\n" +
            "• Create unique playstyles\n" +
            "• Perfect for fine-tuning your experience";

        /// <summary>
        /// Constructor - initialize with balanced defaults.
        /// </summary>
        public CustomStoryteller()
        {
            // Default to balanced personality
            _personality = new StorytellerPersonality
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
        /// Work priority determination based on user-configured personality.
        /// </summary>
        public override int DetermineWorkPriority(WorkTypeDef workType, Pawn colonist, ColonyNeeds needs)
        {
            string defName = workType.defName.ToLower();

            // 1. CRITICAL WORK - always high priority regardless of settings
            if (defName.Contains("firefight"))
                return 1; // Firefighting always critical
            
            // 2. MEDICAL - scaled by social focus (higher social = more care)
            if (defName.Contains("doctor"))
            {
                if (needs.MedicalUrgency >= 3)
                    return 1; // Always critical when urgent
                
                // Scale by social focus
                return _personality.SocialFocus >= 0.7f ? 2 : 3;
            }

            // 3. FOOD PRODUCTION - scaled by risk tolerance (lower risk = more food)
            if (defName.Contains("cook") || defName.Contains("hunt"))
            {
                if (needs.FoodUrgency >= 3)
                    return 1; // Critical food shortage
                
                // Lower risk = higher food priority
                if (_personality.RiskTolerance < 0.3f)
                    return 2; // Cautious - stockpile food
                else if (_personality.RiskTolerance > 0.7f)
                    return 3; // Aggressive - minimal food stocks
                else
                    return 2; // Balanced
            }

            // 4. CONSTRUCTION - scaled by building speed
            if (defName.Contains("construct"))
            {
                if (needs.ConstructionUrgency >= 3)
                    return 1;
                
                // Higher building speed = higher construction priority
                if (_personality.BuildingSpeed >= 0.7f)
                    return 1; // Fast builder
                else if (_personality.BuildingSpeed >= 0.4f)
                    return 2; // Moderate builder
                else
                    return 3; // Slow builder
            }

            // 5. RESEARCH - scaled by research priority
            if (defName.Contains("research"))
            {
                if (needs.ResearchUrgency >= 3)
                    return 1;
                
                // Direct mapping to research priority
                if (_personality.ResearchPriority >= 0.7f)
                    return 1; // High research focus
                else if (_personality.ResearchPriority >= 0.4f)
                    return 2; // Moderate research
                else
                    return 3; // Low research priority
            }

            // 6. PRODUCTION - scaled by trade aggressiveness
            if (IsProductionWork(workType))
            {
                // Higher trade = more production for profit
                if (_personality.TradeAggressiveness >= 0.7f)
                    return 2; // High production for trade
                else if (_personality.TradeAggressiveness >= 0.4f)
                    return 3; // Moderate production
                else
                    return 4; // Low production priority
            }

            // 7. FARMING - inverse of risk tolerance
            if (defName.Contains("plant") || defName.Contains("grow"))
            {
                if (needs.PlantUrgency >= 3)
                    return 2;
                
                // Lower risk = more farming
                if (_personality.RiskTolerance < 0.3f)
                    return 2; // Cautious farms a lot
                else
                    return 3; // Others farm moderately
            }

            // 8. WARDEN - scaled by social focus
            if (defName.Contains("warden"))
            {
                return _personality.SocialFocus >= 0.5f ? 2 : 3;
            }

            // 9. ANIMALS - scaled by risk (high risk = war animals)
            if (defName.Contains("handle"))
            {
                return 3; // Standard priority
            }

            // 10. HAULING - inverse of building speed
            if (defName.Contains("haul"))
            {
                // Slower building = more organized hauling
                return _personality.BuildingSpeed >= 0.7f ? 4 : 3;
            }

            // 11. CLEANING - scaled by social focus (mood management)
            if (defName.Contains("clean"))
            {
                return _personality.SocialFocus >= 0.7f ? 3 : 4;
            }

            // Default: medium priority
            return 3;
        }

        /// <summary>
        /// Get current personality configuration.
        /// </summary>
        public override StorytellerPersonality GetPersonality()
        {
            return _personality;
        }

        /// <summary>
        /// v0.9.14: Crafting strategy for Custom - user configurable!
        /// </summary>
        public override CraftingStrategy GetCraftingStrategy()
        {
            // Based on personality traits
            float stockpile = UnityEngine.Mathf.Lerp(3.0f, 1.0f, _personality.RiskTolerance);
            string quality = _personality.TradeAggressiveness >= 0.7f ? "Poor" :
                           _personality.TradeAggressiveness >= 0.4f ? "Normal" : "Good";
            
            return new CraftingStrategy
            {
                StockpileMultiplier = stockpile,
                TradeFocus = _personality.TradeAggressiveness,
                MinimumQuality = quality,
                PrioritizeWeapons = _personality.DefenseStyle >= 0.7f,
                PrioritizeApparel = _personality.SocialFocus >= 0.7f
            };
        }

        /// <summary>
        /// Set personality trait value (0.0 - 1.0).
        /// </summary>
        public void SetPersonalityTrait(string traitName, float value)
        {
            // Clamp value to valid range
            value = UnityEngine.Mathf.Clamp01(value);
            
            switch (traitName.ToLower())
            {
                case "risktolerance":
                    _personality.RiskTolerance = value;
                    break;
                case "buildingspeed":
                    _personality.BuildingSpeed = value;
                    break;
                case "tradeaggressiveness":
                    _personality.TradeAggressiveness = value;
                    break;
                case "defensestyle":
                    _personality.DefenseStyle = value;
                    break;
                case "researchpriority":
                    _personality.ResearchPriority = value;
                    break;
                case "socialfocus":
                    _personality.SocialFocus = value;
                    break;
                default:
                    Utils.RimWatchLogger.Warning($"CustomStoryteller: Unknown trait '{traitName}'");
                    break;
            }
            
            Utils.RimWatchLogger.Debug($"CustomStoryteller: Set {traitName} = {value:F2}");
        }

        /// <summary>
        /// Load preset personality (Cautious/Balanced/Aggressive templates).
        /// </summary>
        public void LoadPreset(string presetName)
        {
            switch (presetName.ToLower())
            {
                case "cautious":
                    _personality = new StorytellerPersonality
                    {
                        RiskTolerance = 0.2f,
                        BuildingSpeed = 0.3f,
                        TradeAggressiveness = 0.1f,
                        DefenseStyle = 0.1f,
                        ResearchPriority = 0.6f,
                        SocialFocus = 0.7f
                    };
                    ProfileName = "Custom (Cautious Template)";
                    break;
                
                case "aggressive":
                    _personality = new StorytellerPersonality
                    {
                        RiskTolerance = 0.9f,
                        BuildingSpeed = 0.9f,
                        TradeAggressiveness = 0.9f,
                        DefenseStyle = 0.9f,
                        ResearchPriority = 0.9f,
                        SocialFocus = 0.3f
                    };
                    ProfileName = "Custom (Aggressive Template)";
                    break;
                
                case "balanced":
                default:
                    _personality = new StorytellerPersonality
                    {
                        RiskTolerance = 0.5f,
                        BuildingSpeed = 0.5f,
                        TradeAggressiveness = 0.5f,
                        DefenseStyle = 0.5f,
                        ResearchPriority = 0.5f,
                        SocialFocus = 0.5f
                    };
                    ProfileName = "Custom (Balanced Template)";
                    break;
            }
            
            Utils.RimWatchLogger.Info($"CustomStoryteller: Loaded preset '{presetName}'");
        }

        /// <summary>
        /// Get retreat threshold based on risk tolerance and defense style.
        /// </summary>
        public float GetRetreatThreshold()
        {
            // High risk + offensive = fight to the end (0.8-0.9)
            // Low risk + defensive = retreat early (0.3-0.5)
            float baseThreshold = 0.5f;
            baseThreshold += (_personality.RiskTolerance - 0.5f) * 0.4f; // ±0.2
            baseThreshold += (_personality.DefenseStyle - 0.5f) * 0.2f;   // ±0.1
            
            return UnityEngine.Mathf.Clamp(baseThreshold, 0.3f, 0.9f);
        }

        /// <summary>
        /// Get stockpile multiplier based on risk tolerance.
        /// </summary>
        public float GetStockpileMultiplier()
        {
            // Low risk = 3x reserves
            // High risk = 1.5x reserves
            return UnityEngine.Mathf.Lerp(3.0f, 1.5f, _personality.RiskTolerance);
        }

        /// <summary>
        /// Get minimum quality for crafting based on trade aggressiveness.
        /// </summary>
        public string GetMinimumQuality(string itemType)
        {
            // High trade = accept poor quality for speed
            // Low trade = demand high quality for self-sufficiency
            
            if (_personality.TradeAggressiveness >= 0.7f)
                return "Poor"; // Speed over quality
            else if (_personality.TradeAggressiveness >= 0.4f)
                return "Normal"; // Balanced quality
            else
                return "Good"; // High quality for self-reliance
        }

        /// <summary>
        /// Export personality to string (for saving/sharing).
        /// </summary>
        public string ExportToString()
        {
            return $"{ProfileName}|" +
                $"{_personality.RiskTolerance:F3}|" +
                $"{_personality.BuildingSpeed:F3}|" +
                $"{_personality.TradeAggressiveness:F3}|" +
                $"{_personality.DefenseStyle:F3}|" +
                $"{_personality.ResearchPriority:F3}|" +
                $"{_personality.SocialFocus:F3}";
        }

        /// <summary>
        /// Import personality from string.
        /// </summary>
        public bool ImportFromString(string data)
        {
            try
            {
                string[] parts = data.Split('|');
                if (parts.Length != 7)
                {
                    Utils.RimWatchLogger.Error($"CustomStoryteller: Invalid import data format (expected 7 parts, got {parts.Length})");
                    return false;
                }
                
                ProfileName = parts[0];
                _personality.RiskTolerance = float.Parse(parts[1]);
                _personality.BuildingSpeed = float.Parse(parts[2]);
                _personality.TradeAggressiveness = float.Parse(parts[3]);
                _personality.DefenseStyle = float.Parse(parts[4]);
                _personality.ResearchPriority = float.Parse(parts[5]);
                _personality.SocialFocus = float.Parse(parts[6]);
                
                Utils.RimWatchLogger.Info($"CustomStoryteller: Imported profile '{ProfileName}'");
                return true;
            }
            catch (System.Exception ex)
            {
                Utils.RimWatchLogger.Error($"CustomStoryteller: Import failed", ex);
                return false;
            }
        }

        /// <summary>
        /// Get personality description for UI display.
        /// </summary>
        public string GetPersonalityDescription()
        {
            string risk = _personality.RiskTolerance >= 0.7f ? "High Risk" : 
                         _personality.RiskTolerance >= 0.3f ? "Moderate Risk" : "Low Risk";
            
            string building = _personality.BuildingSpeed >= 0.7f ? "Fast Expansion" : 
                             _personality.BuildingSpeed >= 0.3f ? "Moderate Expansion" : "Slow Growth";
            
            string trade = _personality.TradeAggressiveness >= 0.7f ? "Aggressive Trade" : 
                          _personality.TradeAggressiveness >= 0.3f ? "Balanced Trade" : "Conservative Trade";
            
            string defense = _personality.DefenseStyle >= 0.7f ? "Offensive Tactics" : 
                            _personality.DefenseStyle >= 0.3f ? "Balanced Defense" : "Defensive Tactics";
            
            return $"{risk}, {building}, {trade}, {defense}";
        }
    }
}

