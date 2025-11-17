using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation
{
    /// <summary>
    /// Colony state for decision making.
    /// </summary>
    public class ColonyState
    {
        public bool IsCombatActive { get; set; }
        public bool IsEmergency { get; set; }
        public bool IsPeaceful { get; set; }
        public bool IsLowFood { get; set; }
        public bool IsWinterApproaching { get; set; }
        public bool HasInjuredColonists { get; set; }
        public float AverageMood { get; set; }
        public int EnemyCount { get; set; }
    }

    /// <summary>
    /// Outfit policy types.
    /// </summary>
    public enum OutfitPolicy
    {
        Combat,    // Full armor, protection priority
        Work,      // Balanced protection and mobility
        Casual     // Comfort and beauty priority
    }

    /// <summary>
    /// Smart outfit and apparel automation system.
    /// Manages outfit policies based on colony state.
    /// </summary>
    public static class OutfitAutomation
    {
        private static int _tickCounter = 0;
        private const int UpdateInterval = 600; // Every 10 seconds
        private static Dictionary<Pawn, OutfitPolicy> _currentPolicies = new Dictionary<Pawn, OutfitPolicy>();

        public static bool IsEnabled { get; set; } = false;

        /// <summary>
        /// Main tick method.
        /// </summary>
        public static void Tick(Map map)
        {
            if (!IsEnabled) return;

            try
            {
                _tickCounter++;
                if (_tickCounter >= UpdateInterval)
                {
                    _tickCounter = 0;
                    ManageColonistOutfits(map);
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("OutfitAutomation: Error in Tick", ex);
            }
        }

        /// <summary>
        /// Manages outfits for all colonists.
        /// </summary>
        private static void ManageColonistOutfits(Map map)
        {
            try
            {
                // Analyze colony state
                ColonyState state = AnalyzeColonyState(map);

                List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
                int policyChanges = 0;

                foreach (Pawn colonist in colonists)
                {
                    if (colonist.Dead || colonist.apparel == null) continue;

                    // Determine appropriate outfit policy
                    OutfitPolicy newPolicy = DetermineOutfitPolicy(colonist, state);

                    // Check if policy changed
                    OutfitPolicy oldPolicy = _currentPolicies.ContainsKey(colonist) ? 
                        _currentPolicies[colonist] : OutfitPolicy.Casual;

                    if (newPolicy != oldPolicy)
                    {
                        ApplyOutfitPolicy(colonist, newPolicy);
                        _currentPolicies[colonist] = newPolicy;
                        policyChanges++;

                        RimWatchLogger.Info($"OutfitAutomation: {colonist.LabelShort} policy changed: {oldPolicy} → {newPolicy}");
                    }
                }

                if (policyChanges > 0)
                {
                    RimWatchLogger.Info($"OutfitAutomation: Updated outfit policies for {policyChanges} colonists");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("OutfitAutomation: Error in ManageColonistOutfits", ex);
            }
        }

        /// <summary>
        /// Analyzes current colony state.
        /// </summary>
        public static ColonyState AnalyzeColonyState(Map map)
        {
            ColonyState state = new ColonyState();

            try
            {
                // Check for enemies (combat)
                state.EnemyCount = map.mapPawns.AllPawnsSpawned
                    .Count(p => p.HostileTo(Faction.OfPlayer) && !p.Dead && !p.Downed);
                state.IsCombatActive = state.EnemyCount > 0;

                // Check for emergencies
                state.IsEmergency = state.EnemyCount > 5 || 
                                   map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count > 10;

                // Check for injured colonists
                state.HasInjuredColonists = map.mapPawns.FreeColonistsSpawned
                    .Any(p => p.health.hediffSet.HasNaturallyHealingInjury() || 
                             p.health.hediffSet.BleedRateTotal > 0.01f);

                // Check food situation
                int mealCount = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Count;
                int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;
                state.IsLowFood = mealCount < colonistCount * 3;

                // Check season (winter approaching)
                // Quadrum 3 (Decem) is late fall/early winter
                int currentQuadrum = (int)(GenDate.DayOfYear(Find.TickManager.TicksAbs, map.Tile) / 15f % 4f);
                state.IsWinterApproaching = currentQuadrum == 3; // Late fall

                // Calculate average mood
                var colonists = map.mapPawns.FreeColonistsSpawned.ToList();
                if (colonists.Count > 0)
                {
                    state.AverageMood = colonists.Average(p => p.needs?.mood?.CurLevel ?? 0.5f);
                }

                // Peaceful if no combat and no emergency
                state.IsPeaceful = !state.IsCombatActive && !state.IsEmergency;

            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("OutfitAutomation: Error analyzing colony state", ex);
            }

            return state;
        }

        /// <summary>
        /// Determines appropriate outfit policy for a colonist.
        /// </summary>
        private static OutfitPolicy DetermineOutfitPolicy(Pawn colonist, ColonyState state)
        {
            // Combat mode during raids/threats
            if (state.IsCombatActive || state.IsEmergency)
            {
                return OutfitPolicy.Combat;
            }

            // Work mode during critical tasks
            if (state.IsLowFood || state.IsWinterApproaching || state.HasInjuredColonists)
            {
                return OutfitPolicy.Work;
            }

            // Casual mode during peaceful times
            return OutfitPolicy.Casual;
        }

        /// <summary>
        /// Applies outfit policy to a colonist.
        /// NOTE: Outfit management simplified - full outfit switching will be added in future version.
        /// Currently just logs policy changes for awareness.
        /// </summary>
        private static void ApplyOutfitPolicy(Pawn colonist, OutfitPolicy policy)
        {
            try
            {
                if (colonist.outfits == null) return;

                // TODO: Full outfit policy management requires deeper RimWorld API integration
                // For now, log the policy change
                RimWatchLogger.Debug($"OutfitAutomation: {colonist.LabelShort} should use {policy} policy");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"OutfitAutomation: Error applying policy to {colonist.LabelShort}", ex);
            }
        }

        /// <summary>
        /// Equips all available armor pieces for a colonist.
        /// Checks all apparel layers and body part groups.
        /// </summary>
        public static void EquipAllArmorPieces(Pawn colonist, List<Apparel> availableArmor)
        {
            try
            {
                if (colonist.apparel == null) return;

                int equipped = 0;

                // Get all apparel layers from game/mods
                var allLayers = DefDatabase<ApparelLayerDef>.AllDefsListForReading;

                foreach (var layer in allLayers)
                {
                    // Find best armor for this layer
                    var armorForLayer = availableArmor
                        .Where(a => a.def.apparel.layers.Contains(layer))
                        .OrderByDescending(a => GetArmorRating(a))
                        .FirstOrDefault();

                    if (armorForLayer == null) continue;

                    // Check if can wear without dropping current items
                    if (colonist.apparel.CanWearWithoutDroppingAnything(armorForLayer.def))
                    {
                        // Create wear job
                        Verse.AI.Job wearJob = JobMaker.MakeJob(JobDefOf.Wear, armorForLayer);
                        colonist.jobs.TryTakeOrderedJob(wearJob, Verse.AI.JobTag.Misc);
                        
                        availableArmor.Remove(armorForLayer);
                        equipped++;

                        RimWatchLogger.Debug($"OutfitAutomation: {colonist.LabelShort} equipped {armorForLayer.LabelShort} ({layer.defName})");
                    }
                }

                if (equipped > 0)
                {
                    RimWatchLogger.Info($"OutfitAutomation: Equipped {equipped} armor pieces for {colonist.LabelShort}");
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"OutfitAutomation: Error equipping armor for {colonist.LabelShort}", ex);
            }
        }

        /// <summary>
        /// Gets armor rating for an apparel piece.
        /// </summary>
        private static float GetArmorRating(Apparel apparel)
        {
            try
            {
                float sharp = apparel.GetStatValue(StatDefOf.ArmorRating_Sharp);
                float blunt = apparel.GetStatValue(StatDefOf.ArmorRating_Blunt);
                return (sharp + blunt) / 2f;
            }
            catch
            {
                return 0f;
            }
        }
    }
}

