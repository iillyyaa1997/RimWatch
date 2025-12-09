using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimWatch.Automation.Defense
{
    /// <summary>
    /// Advanced tactical positioning system for combat.
    /// Assigns combat roles, manages formations, and optimizes defensive positions.
    /// v0.9.15: Tactical positioning and combat roles.
    /// </summary>
    public static class TacticalPositioningSystem
    {
        // Configuration Constants
        private const int UPDATE_INTERVAL = 120; // Update every 2 seconds
        private const float COVER_BONUS_WEIGHT = 3.0f;
        private const float RANGE_OPTIMAL_WEIGHT = 2.0f;
        private const float DISTANCE_TO_THREAT_WEIGHT = 1.5f;
        private const float FORMATION_COHESION_WEIGHT = 1.0f;
        
        // Combat role ranges
        private const float MELEE_ENGAGE_RANGE = 3.0f;
        private const float RANGED_OPTIMAL_RANGE = 25.0f;
        private const float SNIPER_OPTIMAL_RANGE = 40.0f;
        private const float SUPPORT_SAFE_RANGE = 30.0f;
        
        // State tracking
        private static int _lastUpdateTick = 0;
        private static Dictionary<Pawn, CombatRole> _assignedRoles = new Dictionary<Pawn, CombatRole>();
        private static Dictionary<Pawn, IntVec3> _assignedPositions = new Dictionary<Pawn, IntVec3>();
        private static List<ThreatInfo> _currentThreats = new List<ThreatInfo>();
        
        /// <summary>
        /// Main tick method for tactical positioning.
        /// </summary>
        public static void Tick(Map map)
        {
            try
            {
                int currentTick = Find.TickManager.TicksGame;
                
                if (currentTick - _lastUpdateTick < UPDATE_INTERVAL)
                    return;
                
                _lastUpdateTick = currentTick;
                
                // Analyze threats
                UpdateThreatAnalysis(map);
                
                if (_currentThreats.Count == 0)
                {
                    // No threats - clear assignments
                    _assignedRoles.Clear();
                    _assignedPositions.Clear();
                    return;
                }
                
                // Assign combat roles
                AssignCombatRoles(map);
                
                // Calculate optimal positions
                CalculateOptimalPositions(map);
                
                // Issue movement orders
                IssuePositioningOrders(map);
                
                RimWatchLogger.Debug($"TacticalPositioning: Updated {_assignedRoles.Count} colonists, {_currentThreats.Count} threats");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("TacticalPositioningSystem: Error in Tick", ex);
            }
        }
        
        /// <summary>
        /// Analyzes all threats on the map.
        /// </summary>
        private static void UpdateThreatAnalysis(Map map)
        {
            _currentThreats.Clear();
            
            // Find all hostile pawns
            var hostiles = map.mapPawns.AllPawnsSpawned
                .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Downed && p.Spawned)
                .ToList();
            
            foreach (var hostile in hostiles)
            {
                _currentThreats.Add(new ThreatInfo
                {
                    Pawn = hostile,
                    Position = hostile.Position,
                    ThreatLevel = CalculateThreatLevel(hostile),
                    IsRanged = HasRangedWeapon(hostile),
                    IsMelee = HasMeleeWeapon(hostile)
                });
            }
            
            // Sort by threat level (highest first)
            _currentThreats = _currentThreats.OrderByDescending(t => t.ThreatLevel).ToList();
        }
        
        /// <summary>
        /// Calculates threat level of a hostile pawn.
        /// </summary>
        private static float CalculateThreatLevel(Pawn hostile)
        {
            float threat = 1.0f;
            
            // Health factor
            threat *= hostile.health.summaryHealth.SummaryHealthPercent;
            
            // Weapon factor
            if (hostile.equipment?.Primary != null)
            {
                var weapon = hostile.equipment.Primary;
                var verb = weapon.GetComp<CompEquippable>()?.PrimaryVerb;
                
                if (verb != null)
                {
                    // DPS approximation
                    float damage = verb.verbProps.AdjustedMeleeDamageAmount(verb, hostile);
                    float cooldown = verb.verbProps.AdjustedCooldownTicks(verb, hostile) / 60f;
                    float dps = cooldown > 0 ? damage / cooldown : damage;
                    
                    threat *= (1.0f + dps * 0.1f);
                }
            }
            
            // Skills factor
            if (hostile.skills != null)
            {
                int shootingSkill = hostile.skills.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                int meleeSkill = hostile.skills.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                
                threat *= (1.0f + Mathf.Max(shootingSkill, meleeSkill) * 0.05f);
            }
            
            return threat;
        }
        
        /// <summary>
        /// Assigns combat roles to all colonists based on their equipment and skills.
        /// </summary>
        private static void AssignCombatRoles(Map map)
        {
            _assignedRoles.Clear();
            
            var colonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && !p.InMentalState)
                .ToList();
            
            foreach (var colonist in colonists)
            {
                CombatRole role = DetermineCombatRole(colonist);
                _assignedRoles[colonist] = role;
                
                RimWatchLogger.Debug($"  {colonist.LabelShort}: {role}");
            }
        }
        
        /// <summary>
        /// Determines the best combat role for a colonist.
        /// </summary>
        private static CombatRole DetermineCombatRole(Pawn colonist)
        {
            // Check equipment
            var weapon = colonist.equipment?.Primary;
            
            if (weapon == null)
                return CombatRole.Support; // No weapon = support
            
            var verb = weapon.GetComp<CompEquippable>()?.PrimaryVerb;
            
            if (verb == null)
                return CombatRole.Support;
            
            // Determine role based on weapon type and range
            if (verb.verbProps.range < 5f)
            {
                // Melee weapon
                return CombatRole.Melee;
            }
            else if (verb.verbProps.range >= 35f)
            {
                // Long-range weapon
                int shootingSkill = colonist.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                
                if (shootingSkill >= 10)
                    return CombatRole.Sniper;
                else
                    return CombatRole.Ranged;
            }
            else
            {
                // Medium-range weapon
                return CombatRole.Ranged;
            }
        }
        
        /// <summary>
        /// Calculates optimal positions for all colonists.
        /// </summary>
        private static void CalculateOptimalPositions(Map map)
        {
            _assignedPositions.Clear();
            
            foreach (var kvp in _assignedRoles)
            {
                Pawn colonist = kvp.Key;
                CombatRole role = kvp.Value;
                
                IntVec3 optimalPos = FindOptimalPosition(colonist, role, map);
                
                if (optimalPos.IsValid)
                {
                    _assignedPositions[colonist] = optimalPos;
                }
            }
        }
        
        /// <summary>
        /// Finds optimal position for a colonist based on their role.
        /// </summary>
        private static IntVec3 FindOptimalPosition(Pawn colonist, CombatRole role, Map map)
        {
            if (_currentThreats.Count == 0)
                return IntVec3.Invalid;
            
            // Get primary threat (closest high-threat enemy)
            ThreatInfo primaryThreat = GetPrimaryThreat(colonist);
            
            // Define search radius based on role
            float searchRadius = GetSearchRadiusForRole(role);
            
            // Find candidates
            List<IntVec3> candidates = new List<IntVec3>();
            
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(colonist.Position, searchRadius, true))
            {
                if (!cell.InBounds(map) || !cell.Standable(map))
                    continue;
                
                if (!colonist.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                    continue;
                
                candidates.Add(cell);
            }
            
            if (candidates.Count == 0)
                return IntVec3.Invalid;
            
            // Score each candidate
            IntVec3 bestPosition = IntVec3.Invalid;
            float bestScore = float.MinValue;
            
            foreach (var candidate in candidates)
            {
                float score = ScorePosition(candidate, colonist, role, primaryThreat, map);
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPosition = candidate;
                }
            }
            
            return bestPosition;
        }
        
        /// <summary>
        /// Scores a position based on tactical factors.
        /// </summary>
        private static float ScorePosition(IntVec3 pos, Pawn colonist, CombatRole role, ThreatInfo threat, Map map)
        {
            float score = 0f;
            
            // 1. Cover bonus
            Thing cover = pos.GetCover(map);
            if (cover != null)
            {
                // Check if it provides cover
                if (cover.def.fillPercent > 0.3f)
                {
                    score += cover.def.fillPercent * COVER_BONUS_WEIGHT;
                }
            }
            
            // 2. Distance to threat (role-specific optimal range)
            float distanceToThreat = pos.DistanceTo(threat.Position);
            float optimalRange = GetOptimalRangeForRole(role);
            float rangeDelta = Mathf.Abs(distanceToThreat - optimalRange);
            score -= rangeDelta * 0.1f * RANGE_OPTIMAL_WEIGHT;
            
            // 3. Line of sight to threat
            if (GenSight.LineOfSight(pos, threat.Position, map, true))
            {
                score += 5.0f;
            }
            else
            {
                score -= 10.0f; // Penalty for no LOS
            }
            
            // 4. Distance from current position (prefer closer positions)
            float distanceFromCurrent = pos.DistanceTo(colonist.Position);
            score -= distanceFromCurrent * 0.5f;
            
            // 5. Elevation bonus (using altitude from EdificeGrid)
            Building edificeAtPos = pos.GetEdifice(map);
            Building edificeAtThreat = threat.Position.GetEdifice(map);
            
            float altitudePos = edificeAtPos != null ? edificeAtPos.def.altitudeLayer.AltitudeFor() : 0f;
            float altitudeThreat = edificeAtThreat != null ? edificeAtThreat.def.altitudeLayer.AltitudeFor() : 0f;
            
            if (altitudePos > altitudeThreat)
            {
                score += (altitudePos - altitudeThreat) * 0.5f; // High ground advantage
            }
            
            // 6. Formation cohesion (stay near allies)
            float allyDistance = GetAverageDistanceToAllies(pos, colonist, map);
            if (allyDistance < 15f)
            {
                score += (15f - allyDistance) * 0.2f * FORMATION_COHESION_WEIGHT;
            }
            
            return score;
        }
        
        /// <summary>
        /// Issues movement orders to colonists.
        /// </summary>
        private static void IssuePositioningOrders(Map map)
        {
            foreach (var kvp in _assignedPositions)
            {
                Pawn colonist = kvp.Key;
                IntVec3 targetPos = kvp.Value;
                
                // Only issue order if colonist is far from target
                if (colonist.Position.DistanceTo(targetPos) > 3f)
                {
                    // Check if colonist is already moving or fighting
                    if (colonist.CurJob != null && 
                        (colonist.CurJob.def == JobDefOf.AttackStatic ||
                         colonist.CurJob.def == JobDefOf.AttackMelee))
                    {
                        continue; // Don't interrupt combat
                    }
                    
                    // Issue goto order
                    Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, targetPos);
                    gotoJob.playerForced = true;
                    gotoJob.locomotionUrgency = LocomotionUrgency.Jog;
                    
                    colonist.jobs.StartJob(gotoJob, JobCondition.InterruptForced);
                    
                    RimWatchLogger.Debug($"  Ordering {colonist.LabelShort} to {targetPos}");
                }
            }
        }
        
        // ======================================
        // HELPER METHODS
        // ======================================
        
        private static ThreatInfo GetPrimaryThreat(Pawn colonist)
        {
            if (_currentThreats.Count == 0)
                return null;
            
            // Return closest high-threat enemy
            return _currentThreats
                .OrderBy(t => colonist.Position.DistanceTo(t.Position))
                .FirstOrDefault();
        }
        
        private static float GetSearchRadiusForRole(CombatRole role)
        {
            switch (role)
            {
                case CombatRole.Melee:
                    return 10f;
                case CombatRole.Ranged:
                    return 20f;
                case CombatRole.Sniper:
                    return 30f;
                case CombatRole.Support:
                    return 25f;
                default:
                    return 15f;
            }
        }
        
        private static float GetOptimalRangeForRole(CombatRole role)
        {
            switch (role)
            {
                case CombatRole.Melee:
                    return MELEE_ENGAGE_RANGE;
                case CombatRole.Ranged:
                    return RANGED_OPTIMAL_RANGE;
                case CombatRole.Sniper:
                    return SNIPER_OPTIMAL_RANGE;
                case CombatRole.Support:
                    return SUPPORT_SAFE_RANGE;
                default:
                    return 20f;
            }
        }
        
        private static float GetAverageDistanceToAllies(IntVec3 pos, Pawn self, Map map)
        {
            var allies = map.mapPawns.FreeColonistsSpawned
                .Where(p => p != self && !p.Downed)
                .ToList();
            
            if (allies.Count == 0)
                return 999f;
            
            float totalDistance = 0f;
            
            foreach (var ally in allies)
            {
                totalDistance += pos.DistanceTo(ally.Position);
            }
            
            return totalDistance / allies.Count;
        }
        
        private static bool HasRangedWeapon(Pawn pawn)
        {
            var weapon = pawn.equipment?.Primary;
            if (weapon == null) return false;
            
            var verb = weapon.GetComp<CompEquippable>()?.PrimaryVerb;
            if (verb == null) return false;
            
            return verb.verbProps.range > 5f;
        }
        
        private static bool HasMeleeWeapon(Pawn pawn)
        {
            var weapon = pawn.equipment?.Primary;
            if (weapon == null) return true; // Fists are melee
            
            var verb = weapon.GetComp<CompEquippable>()?.PrimaryVerb;
            if (verb == null) return true;
            
            return verb.verbProps.range <= 5f;
        }
        
        /// <summary>
        /// Gets current combat role assignment for a pawn.
        /// </summary>
        public static CombatRole? GetAssignedRole(Pawn pawn)
        {
            if (_assignedRoles.TryGetValue(pawn, out CombatRole role))
                return role;
            
            return null;
        }
        
        /// <summary>
        /// Gets current position assignment for a pawn.
        /// </summary>
        public static IntVec3? GetAssignedPosition(Pawn pawn)
        {
            if (_assignedPositions.TryGetValue(pawn, out IntVec3 pos))
                return pos;
            
            return null;
        }
    }
    
    /// <summary>
    /// Combat role for tactical positioning.
    /// </summary>
    public enum CombatRole
    {
        Melee,      // Front-line melee fighters
        Ranged,     // Medium-range shooters
        Sniper,     // Long-range precision shooters
        Support     // Non-combat or low-skill colonists
    }
    
    /// <summary>
    /// Information about a threat.
    /// </summary>
    public class ThreatInfo
    {
        public Pawn Pawn { get; set; }
        public IntVec3 Position { get; set; }
        public float ThreatLevel { get; set; }
        public bool IsRanged { get; set; }
        public bool IsMelee { get; set; }
    }
}

