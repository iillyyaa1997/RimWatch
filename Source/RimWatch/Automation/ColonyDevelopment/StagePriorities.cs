using System.Collections.Generic;
using Verse;

namespace RimWatch.Automation.ColonyDevelopment
{
    /// <summary>
    /// Определяет приоритеты задач для каждого этапа развития колонии.
    /// </summary>
    public static class StagePriorities
    {
        /// <summary>
        /// Возвращает список приоритетных задач для данного этапа развития.
        /// </summary>
        public static List<ColonyTask> GetPrioritiesForStage(DevelopmentStage stage, Map map)
        {
            switch (stage)
            {
                case DevelopmentStage.Emergency:
                    return new List<ColonyTask>
                    {
                        new ColonyTask("Ensure all colonists have roofed beds", 100),
                        new ColonyTask("Establish food source (berries/hunting)", 95),
                        new ColonyTask("Build basic storage (prevent deterioration)", 90),
                        new ColonyTask("Set up cooking station", 85),
                        new ColonyTask("Create basic defenses (sandbags)", 80)
                    };
                
                case DevelopmentStage.EarlyGame:
                    return new List<ColonyTask>
                    {
                        new ColonyTask("Build proper bedrooms (4x4+ with beds)", 95),
                        new ColonyTask("Establish farming zones (rice/corn)", 90),
                        new ColonyTask("Build dedicated kitchen + freezer", 85),
                        new ColonyTask("Set up power generation (wood/solar)", 80),
                        new ColonyTask("Build workshop for crafting", 75),
                        new ColonyTask("Create rec room (prevent breaks)", 70),
                        new ColonyTask("Build perimeter wall", 65),
                        new ColonyTask("Research: Electricity, Machining", 60)
                    };
                
                case DevelopmentStage.MidGame:
                    return new List<ColonyTask>
                    {
                        new ColonyTask("Upgrade bedrooms (better furniture)", 85),
                        new ColonyTask("Build hospital (medical beds)", 80),
                        new ColonyTask("Expand farming (multiple crops)", 75),
                        new ColonyTask("Set up drug production (medicine)", 70),
                        new ColonyTask("Build defensive turrets", 65),
                        new ColonyTask("Create dedicated research lab", 60),
                        new ColonyTask("Build prison + conversion room", 55),
                        new ColonyTask("Research: Microelectronics, Gunsmithing", 50)
                    };
                
                case DevelopmentStage.LateGame:
                    return new List<ColonyTask>
                    {
                        new ColonyTask("Build advanced defense (mortars, traps)", 80),
                        new ColonyTask("Set up production chains (art, weapons)", 75),
                        new ColonyTask("Create trade caravans", 70),
                        new ColonyTask("Build luxuries (fine meals, drugs)", 65),
                        new ColonyTask("Establish satellite bases", 60),
                        new ColonyTask("Research: Ship reactors, Advanced fabrication", 55)
                    };
                
                case DevelopmentStage.EndGame:
                    return new List<ColonyTask>
                    {
                        new ColonyTask("Build ship components", 90),
                        new ColonyTask("Maximize wealth & comfort", 80),
                        new ColonyTask("Complete all research", 70),
                        new ColonyTask("Achieve victory condition", 100)
                    };
                
                default:
                    return new List<ColonyTask>();
            }
        }
    }
}

