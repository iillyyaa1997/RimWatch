using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation
{
    /// <summary>
    /// Automatically upgrades buildings when better technology becomes available.
    /// v0.9.5: Building upgrade system for progressive colony improvement.
    /// </summary>
    public static class BuildingUpgradeManager
    {
        // Configuration Constants
        private const int UPGRADE_CHECK_INTERVAL = 5000; // Every ~2 hours game time
        private const float UPGRADE_PRIORITY_THRESHOLD = 0.7f; // Only upgrade if significantly better
        private const int MAX_UPGRADES_PER_CHECK = 3; // Limit concurrent upgrades
        
        // Upgrade tracking
        private static int _lastCheckTick = 0;
        private static Dictionary<IntVec3, UpgradeTask> _pendingUpgrades = new Dictionary<IntVec3, UpgradeTask>();
        private static HashSet<ThingDef> _availableTech = new HashSet<ThingDef>();

        // Upgrade rules (old building -> new building when tech available)
        private static readonly Dictionary<string, UpgradeRule> _upgradeRules = new Dictionary<string, UpgradeRule>
        {
            // Furniture upgrades
            { "Bed", new UpgradeRule { TargetDef = "Bed", RequiredResearch = null, Priority = 0.5f } },
            { "Bed", new UpgradeRule { TargetDef = "DoubleBed", RequiredResearch = "ComplexFurniture", Priority = 0.8f } },
            { "Torch", new UpgradeRule { TargetDef = "StandingLamp_Electric", RequiredResearch = "Electricity", Priority = 0.9f } },
            
            // Production upgrades
            { "HandTailoringBench", new UpgradeRule { TargetDef = "ElectricTailoringBench", RequiredResearch = "Electricity", Priority = 0.8f } },
            { "CraftingSpot", new UpgradeRule { TargetDef = "TableMachining", RequiredResearch = "Machining", Priority = 0.7f } },
            { "FueledSmithy", new UpgradeRule { TargetDef = "ElectricSmithy", RequiredResearch = "Smithing", Priority = 0.8f } },
            
            // Defense upgrades
            { "Sandbags", new UpgradeRule { TargetDef = "Barricade", RequiredResearch = "Smithing", Priority = 0.6f } },
            
            // Medical upgrades
            { "Bed", new UpgradeRule { TargetDef = "HospitalBed", RequiredResearch = "HospitalBed", Priority = 0.9f } },
            
            // Research upgrades
            { "SimpleResearchBench", new UpgradeRule { TargetDef = "HiTechResearchBench", RequiredResearch = "MultianalyzerBuildingPrerequisite", Priority = 1.0f } },
        };

        /// <summary>
        /// Tick upgrade system periodically.
        /// </summary>
        public static void Tick(Map map)
        {
            if (map == null) return;

            int currentTick = Find.TickManager.TicksGame;
            
            if (currentTick - _lastCheckTick < UPGRADE_CHECK_INTERVAL)
                return;

            _lastCheckTick = currentTick;

            // Update available tech
            UpdateAvailableTech();

            // Check for upgrade opportunities
            CheckForUpgrades(map);

            // Process pending upgrades
            ProcessPendingUpgrades(map);
        }

        /// <summary>
        /// Update available tech based on research progress.
        /// </summary>
        private static void UpdateAvailableTech()
        {
            _availableTech.Clear();

            foreach (var buildingDef in DefDatabase<ThingDef>.AllDefs.Where(d => d.building != null))
            {
                // Check if building is available (research completed or no research required)
                if (buildingDef.researchPrerequisites == null || buildingDef.researchPrerequisites.Count == 0)
                {
                    _availableTech.Add(buildingDef);
                    continue;
                }

                bool allResearchComplete = true;
                foreach (var research in buildingDef.researchPrerequisites)
                {
                    if (research == null || !research.IsFinished)
                    {
                        allResearchComplete = false;
                        break;
                    }
                }

                if (allResearchComplete)
                {
                    _availableTech.Add(buildingDef);
                }
            }

            RimWatchLogger.Debug($"BuildingUpgradeManager: {_availableTech.Count} building types available with current tech");
        }

        /// <summary>
        /// Check for buildings that can be upgraded.
        /// </summary>
        private static void CheckForUpgrades(Map map)
        {
            var upgradeCandidates = new List<UpgradeCandidate>();

            // Scan all buildings on map
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (building?.def == null) continue;

                // Skip if already pending upgrade
                if (_pendingUpgrades.ContainsKey(building.Position))
                    continue;

                // Check for applicable upgrade rules
                var applicableRules = _upgradeRules
                    .Where(rule => rule.Key == building.def.defName)
                    .Select(rule => rule.Value)
                    .Where(rule => IsUpgradeAvailable(rule))
                    .ToList();

                if (applicableRules.Count == 0)
                    continue;

                // Pick best upgrade
                var bestUpgrade = applicableRules.OrderByDescending(r => r.Priority).First();
                var targetDef = DefDatabase<ThingDef>.GetNamedSilentFail(bestUpgrade.TargetDef);

                if (targetDef == null)
                    continue;

                // Calculate upgrade benefit
                float benefit = CalculateUpgradeBenefit(building.def, targetDef);

                if (benefit >= UPGRADE_PRIORITY_THRESHOLD)
                {
                    upgradeCandidates.Add(new UpgradeCandidate
                    {
                        Building = building,
                        TargetDef = targetDef,
                        Benefit = benefit,
                        Rule = bestUpgrade
                    });
                }
            }

            // Sort by benefit and take top candidates
            var topUpgrades = upgradeCandidates
                .OrderByDescending(c => c.Benefit)
                .Take(MAX_UPGRADES_PER_CHECK)
                .ToList();

            foreach (var candidate in topUpgrades)
            {
                QueueUpgrade(candidate);
            }

            if (topUpgrades.Count > 0)
            {
                RimWatchLogger.Info($"BuildingUpgradeManager: Queued {topUpgrades.Count} building upgrades");
            }
        }

        /// <summary>
        /// Check if upgrade is available (research complete).
        /// </summary>
        private static bool IsUpgradeAvailable(UpgradeRule rule)
        {
            if (string.IsNullOrEmpty(rule.RequiredResearch))
                return true;

            var research = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(rule.RequiredResearch);
            return research?.IsFinished ?? false;
        }

        /// <summary>
        /// Calculate benefit of upgrading (0-1 scale).
        /// </summary>
        private static float CalculateUpgradeBenefit(ThingDef oldDef, ThingDef newDef)
        {
            float benefit = 0f;
            int comparisons = 0;

            // Compare stats
            var statsToCompare = new[]
            {
                StatDefOf.MaxHitPoints,
                StatDefOf.WorkSpeedGlobal,
                StatDefOf.ResearchSpeedFactor,
                StatDefOf.MedicalTendQualityOffset,
                StatDefOf.Beauty,
                StatDefOf.Comfort
            };

            foreach (var stat in statsToCompare)
            {
                if (stat == null) continue;

                float oldValue = oldDef.GetStatValueAbstract(stat);
                float newValue = newDef.GetStatValueAbstract(stat);

                if (oldValue > 0f)
                {
                    float improvement = (newValue - oldValue) / oldValue;
                    benefit += improvement;
                    comparisons++;
                }
            }

            // Average benefit
            if (comparisons > 0)
            {
                benefit /= comparisons;
            }

            // Bonus for electricity (non-fuel)
            if (oldDef.building?.buildingTags?.Contains("Production") == true)
            {
                bool oldUsesFuel = oldDef.comps?.Any(c => c.compClass?.Name == "CompRefuelable") ?? false;
                bool newUsesElectricity = newDef.comps?.Any(c => c.compClass?.Name == "CompPowerTrader") ?? false;

                if (oldUsesFuel && newUsesElectricity)
                {
                    benefit += 0.3f; // Bonus for switching to electricity
                }
            }

            return UnityEngine.Mathf.Max(0f, UnityEngine.Mathf.Min(1f, benefit));
        }

        /// <summary>
        /// Queue building for upgrade.
        /// </summary>
        private static void QueueUpgrade(UpgradeCandidate candidate)
        {
            var task = new UpgradeTask
            {
                OldBuilding = candidate.Building,
                TargetDef = candidate.TargetDef,
                Benefit = candidate.Benefit,
                QueuedTick = Find.TickManager.TicksGame
            };

            _pendingUpgrades[candidate.Building.Position] = task;

            RimWatchLogger.Info($"BuildingUpgradeManager: Queued upgrade {candidate.Building.def.label} -> {candidate.TargetDef.label} " +
                $"at {candidate.Building.Position} (benefit: {candidate.Benefit:P0})");
        }

        /// <summary>
        /// Process pending upgrades (deconstruct old, build new).
        /// </summary>
        private static void ProcessPendingUpgrades(Map map)
        {
            var completedUpgrades = new List<IntVec3>();

            foreach (var kvp in _pendingUpgrades)
            {
                var pos = kvp.Key;
                var task = kvp.Value;

                // Check if old building still exists
                if (task.OldBuilding == null || task.OldBuilding.Destroyed)
                {
                    completedUpgrades.Add(pos);
                    continue;
                }

                // Designate for deconstruction if not already
                if (map.designationManager.DesignationOn(task.OldBuilding, DesignationDefOf.Deconstruct) == null)
                {
                    map.designationManager.AddDesignation(new Designation(task.OldBuilding, DesignationDefOf.Deconstruct));
                    RimWatchLogger.Info($"BuildingUpgradeManager: Designated {task.OldBuilding.def.label} for deconstruction (upgrade to {task.TargetDef.label})");
                }

                // Check if building is gone (deconstructed)
                var thingAtPos = pos.GetFirstThing(map, task.OldBuilding.def);
                if (thingAtPos == null)
                {
                    // Old building gone, place blueprint for new one
                    PlaceUpgradeBlueprint(pos, task.TargetDef, map);
                    completedUpgrades.Add(pos);
                }
            }

            // Remove completed upgrades
            foreach (var pos in completedUpgrades)
            {
                _pendingUpgrades.Remove(pos);
            }
        }

        /// <summary>
        /// Place blueprint for upgraded building.
        /// </summary>
        private static void PlaceUpgradeBlueprint(IntVec3 pos, ThingDef targetDef, Map map)
        {
            // Check if position is valid
            if (!GenConstruct.CanPlaceBlueprintAt(targetDef, pos, Rot4.North, map).Accepted)
            {
                RimWatchLogger.Warning($"BuildingUpgradeManager: Cannot place {targetDef.label} at {pos}");
                return;
            }

            // Place blueprint
            GenConstruct.PlaceBlueprintForBuild(targetDef, pos, map, Rot4.North, Faction.OfPlayer, null);
            RimWatchLogger.Info($"BuildingUpgradeManager: Placed blueprint for {targetDef.label} at {pos}");
        }

        /// <summary>
        /// Get upgrade status summary.
        /// </summary>
        public static UpgradeSummary GetSummary()
        {
            return new UpgradeSummary
            {
                PendingUpgrades = _pendingUpgrades.Count,
                AvailableTech = _availableTech.Count
            };
        }
    }

    /// <summary>
    /// Upgrade rule definition.
    /// </summary>
    public class UpgradeRule
    {
        public string TargetDef { get; set; } = "";
        public string? RequiredResearch { get; set; }
        public float Priority { get; set; }
    }

    /// <summary>
    /// Upgrade candidate.
    /// </summary>
    public class UpgradeCandidate
    {
        public Thing Building { get; set; } = null!;
        public ThingDef TargetDef { get; set; } = null!;
        public float Benefit { get; set; }
        public UpgradeRule Rule { get; set; } = null!;
    }

    /// <summary>
    /// Pending upgrade task.
    /// </summary>
    public class UpgradeTask
    {
        public Thing OldBuilding { get; set; } = null!;
        public ThingDef TargetDef { get; set; } = null!;
        public float Benefit { get; set; }
        public int QueuedTick { get; set; }
    }

    /// <summary>
    /// Upgrade system summary.
    /// </summary>
    public class UpgradeSummary
    {
        public int PendingUpgrades { get; set; }
        public int AvailableTech { get; set; }
    }
}

