using RimWatch.Core;
using RimWatch.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWatch.Automation.Defense
{
    /// <summary>
    /// Manages combat formations for coordinated group tactics.
    /// v0.9.15: Formation system for tactical combat.
    /// </summary>
    public static class FormationManager
    {
        // Formation templates
        private static Dictionary<FormationType, FormationTemplate> _formationTemplates;
        
        // Active formation
        private static FormationType _currentFormation = FormationType.Line;
        private static IntVec3 _formationCenter = IntVec3.Invalid;
        private static float _formationRotation = 0f;
        
        static FormationManager()
        {
            InitializeFormationTemplates();
        }
        
        /// <summary>
        /// Initializes all formation templates.
        /// </summary>
        private static void InitializeFormationTemplates()
        {
            _formationTemplates = new Dictionary<FormationType, FormationTemplate>();
            
            // LINE FORMATION - Good for defensive positions
            _formationTemplates[FormationType.Line] = new FormationTemplate
            {
                Type = FormationType.Line,
                Name = "Line Formation",
                Description = "Defensive line with melee front, ranged back",
                Positions = new List<FormationPosition>
                {
                    // Front line (melee)
                    new FormationPosition { Offset = new Vector2(-3, 0), PreferredRole = CombatRole.Melee },
                    new FormationPosition { Offset = new Vector2(-1, 0), PreferredRole = CombatRole.Melee },
                    new FormationPosition { Offset = new Vector2(1, 0), PreferredRole = CombatRole.Melee },
                    new FormationPosition { Offset = new Vector2(3, 0), PreferredRole = CombatRole.Melee },
                    // Back line (ranged)
                    new FormationPosition { Offset = new Vector2(-4, -3), PreferredRole = CombatRole.Ranged },
                    new FormationPosition { Offset = new Vector2(-2, -3), PreferredRole = CombatRole.Ranged },
                    new FormationPosition { Offset = new Vector2(0, -3), PreferredRole = CombatRole.Sniper },
                    new FormationPosition { Offset = new Vector2(2, -3), PreferredRole = CombatRole.Ranged },
                    new FormationPosition { Offset = new Vector2(4, -3), PreferredRole = CombatRole.Ranged },
                    // Support line
                    new FormationPosition { Offset = new Vector2(-1, -5), PreferredRole = CombatRole.Support },
                    new FormationPosition { Offset = new Vector2(1, -5), PreferredRole = CombatRole.Support }
                }
            };
            
            // WEDGE FORMATION - Good for offensive pushes
            _formationTemplates[FormationType.Wedge] = new FormationTemplate
            {
                Type = FormationType.Wedge,
                Name = "Wedge Formation",
                Description = "Offensive wedge for breakthrough attacks",
                Positions = new List<FormationPosition>
                {
                    // Point (strongest melee)
                    new FormationPosition { Offset = new Vector2(0, 3), PreferredRole = CombatRole.Melee },
                    // Second row
                    new FormationPosition { Offset = new Vector2(-2, 2), PreferredRole = CombatRole.Melee },
                    new FormationPosition { Offset = new Vector2(2, 2), PreferredRole = CombatRole.Melee },
                    // Third row
                    new FormationPosition { Offset = new Vector2(-3, 0), PreferredRole = CombatRole.Ranged },
                    new FormationPosition { Offset = new Vector2(-1, 0), PreferredRole = CombatRole.Ranged },
                    new FormationPosition { Offset = new Vector2(1, 0), PreferredRole = CombatRole.Ranged },
                    new FormationPosition { Offset = new Vector2(3, 0), PreferredRole = CombatRole.Ranged },
                    // Back row (support)
                    new FormationPosition { Offset = new Vector2(-2, -2), PreferredRole = CombatRole.Sniper },
                    new FormationPosition { Offset = new Vector2(0, -2), PreferredRole = CombatRole.Support },
                    new FormationPosition { Offset = new Vector2(2, -2), PreferredRole = CombatRole.Sniper }
                }
            };
            
            // CIRCLE FORMATION - Good for surrounded situations
            _formationTemplates[FormationType.Circle] = new FormationTemplate
            {
                Type = FormationType.Circle,
                Name = "Circle Formation",
                Description = "Defensive circle, all-around protection",
                Positions = GenerateCirclePositions(8, 5f)
            };
            
            // SKIRMISH FORMATION - Spread out, good for guerrilla tactics
            _formationTemplates[FormationType.Skirmish] = new FormationTemplate
            {
                Type = FormationType.Skirmish,
                Name = "Skirmish Formation",
                Description = "Loose formation for mobility and cover",
                Positions = new List<FormationPosition>
                {
                    new FormationPosition { Offset = new Vector2(-6, 2), PreferredRole = CombatRole.Ranged },
                    new FormationPosition { Offset = new Vector2(-4, -1), PreferredRole = CombatRole.Ranged },
                    new FormationPosition { Offset = new Vector2(-2, 3), PreferredRole = CombatRole.Melee },
                    new FormationPosition { Offset = new Vector2(0, -2), PreferredRole = CombatRole.Sniper },
                    new FormationPosition { Offset = new Vector2(2, 1), PreferredRole = CombatRole.Ranged },
                    new FormationPosition { Offset = new Vector2(4, -3), PreferredRole = CombatRole.Ranged },
                    new FormationPosition { Offset = new Vector2(6, 0), PreferredRole = CombatRole.Melee },
                    new FormationPosition { Offset = new Vector2(0, -5), PreferredRole = CombatRole.Support }
                }
            };
        }
        
        /// <summary>
        /// Generates circular formation positions.
        /// </summary>
        private static List<FormationPosition> GenerateCirclePositions(int count, float radius)
        {
            List<FormationPosition> positions = new List<FormationPosition>();
            
            for (int i = 0; i < count; i++)
            {
                float angle = (float)i / count * 360f * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius
                );
                
                // Alternate between melee and ranged
                CombatRole role = (i % 2 == 0) ? CombatRole.Ranged : CombatRole.Melee;
                
                positions.Add(new FormationPosition
                {
                    Offset = offset,
                    PreferredRole = role
                });
            }
            
            return positions;
        }
        
        /// <summary>
        /// Sets the active formation type.
        /// </summary>
        public static void SetFormation(FormationType type, IntVec3 center, float rotation = 0f)
        {
            _currentFormation = type;
            _formationCenter = center;
            _formationRotation = rotation;
            
            RimWatchLogger.Info($"FormationManager: Set formation to {type} at {center}, rotation {rotation}°");
        }
        
        /// <summary>
        /// Automatically chooses best formation based on tactical situation.
        /// </summary>
        public static FormationType ChooseBestFormation(Map map, List<Pawn> colonists, List<Pawn> enemies)
        {
            if (colonists.Count == 0 || enemies.Count == 0)
                return FormationType.Line;
            
            // Calculate average positions
            Vector3 colonistCenter = GetAveragePosition(colonists);
            Vector3 enemyCenter = GetAveragePosition(enemies);
            
            // Calculate threat dispersion
            float enemySpread = CalculateSpread(enemies);
            
            // Calculate relative strength
            float strengthRatio = (float)colonists.Count / enemies.Count;
            
            // Decision logic
            if (enemySpread > 30f)
            {
                // Enemies are very spread out - use skirmish
                return FormationType.Skirmish;
            }
            else if (strengthRatio > 1.5f)
            {
                // We have numerical advantage - use wedge for offensive
                return FormationType.Wedge;
            }
            else if (strengthRatio < 0.7f)
            {
                // We're outnumbered - use defensive circle
                return FormationType.Circle;
            }
            else
            {
                // Balanced situation - use line formation
                return FormationType.Line;
            }
        }
        
        /// <summary>
        /// Gets formation positions for current squad.
        /// </summary>
        public static Dictionary<Pawn, IntVec3> GetFormationPositions(Map map, List<Pawn> colonists)
        {
            Dictionary<Pawn, IntVec3> assignments = new Dictionary<Pawn, IntVec3>();
            
            if (!_formationTemplates.ContainsKey(_currentFormation))
                return assignments;
            
            var template = _formationTemplates[_currentFormation];
            
            // Assign colonists to formation positions based on role matching
            var sortedColonists = SortColonistsByRole(colonists);
            var sortedPositions = template.Positions.OrderBy(p => GetRolePriority(p.PreferredRole)).ToList();
            
            for (int i = 0; i < Mathf.Min(sortedColonists.Count, sortedPositions.Count); i++)
            {
                Pawn colonist = sortedColonists[i];
                FormationPosition formPos = sortedPositions[i];
                
                // Calculate world position
                IntVec3 worldPos = CalculateWorldPosition(formPos.Offset, _formationCenter, _formationRotation, map);
                
                if (worldPos.IsValid && worldPos.Standable(map))
                {
                    assignments[colonist] = worldPos;
                }
            }
            
            return assignments;
        }
        
        /// <summary>
        /// Calculates world position from formation offset.
        /// </summary>
        private static IntVec3 CalculateWorldPosition(Vector2 offset, IntVec3 center, float rotation, Map map)
        {
            // Apply rotation
            float rad = rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            
            float rotatedX = offset.x * cos - offset.y * sin;
            float rotatedY = offset.x * sin + offset.y * cos;
            
            // Convert to world position
            int worldX = center.x + Mathf.RoundToInt(rotatedX);
            int worldZ = center.z + Mathf.RoundToInt(rotatedY);
            
            IntVec3 result = new IntVec3(worldX, 0, worldZ);
            
            // Find nearest standable cell if current is blocked
            if (!result.InBounds(map) || !result.Standable(map))
            {
                result = CellFinder.StandableCellNear(result, map, 3f);
            }
            
            return result;
        }
        
        /// <summary>
        /// Sorts colonists by their combat role preference.
        /// </summary>
        private static List<Pawn> SortColonistsByRole(List<Pawn> colonists)
        {
            return colonists.OrderBy(c =>
            {
                var role = TacticalPositioningSystem.GetAssignedRole(c);
                return role.HasValue ? GetRolePriority(role.Value) : 999;
            }).ToList();
        }
        
        /// <summary>
        /// Gets priority for role assignment (lower = higher priority).
        /// </summary>
        private static int GetRolePriority(CombatRole role)
        {
            switch (role)
            {
                case CombatRole.Melee:
                    return 1;
                case CombatRole.Ranged:
                    return 2;
                case CombatRole.Sniper:
                    return 3;
                case CombatRole.Support:
                    return 4;
                default:
                    return 999;
            }
        }
        
        /// <summary>
        /// Calculates average position of pawns.
        /// </summary>
        private static Vector3 GetAveragePosition(List<Pawn> pawns)
        {
            if (pawns.Count == 0)
                return Vector3.zero;
            
            Vector3 sum = Vector3.zero;
            foreach (var pawn in pawns)
            {
                sum += pawn.Position.ToVector3();
            }
            
            return sum / pawns.Count;
        }
        
        /// <summary>
        /// Calculates how spread out a group of pawns is.
        /// </summary>
        private static float CalculateSpread(List<Pawn> pawns)
        {
            if (pawns.Count < 2)
                return 0f;
            
            Vector3 center = GetAveragePosition(pawns);
            float totalDistance = 0f;
            
            foreach (var pawn in pawns)
            {
                totalDistance += Vector3.Distance(pawn.Position.ToVector3(), center);
            }
            
            return totalDistance / pawns.Count;
        }
        
        /// <summary>
        /// Gets information about current formation.
        /// </summary>
        public static FormationInfo GetCurrentFormationInfo()
        {
            if (!_formationTemplates.ContainsKey(_currentFormation))
                return null;
            
            var template = _formationTemplates[_currentFormation];
            
            return new FormationInfo
            {
                Type = _currentFormation,
                Name = template.Name,
                Description = template.Description,
                Center = _formationCenter,
                Rotation = _formationRotation,
                PositionCount = template.Positions.Count
            };
        }
    }
    
    /// <summary>
    /// Formation type enumeration.
    /// </summary>
    public enum FormationType
    {
        Line,       // Defensive line formation
        Wedge,      // Offensive wedge formation
        Circle,     // Defensive circle formation
        Skirmish    // Loose spread formation
    }
    
    /// <summary>
    /// Formation template definition.
    /// </summary>
    public class FormationTemplate
    {
        public FormationType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<FormationPosition> Positions { get; set; }
    }
    
    /// <summary>
    /// Individual position within a formation.
    /// </summary>
    public class FormationPosition
    {
        public Vector2 Offset { get; set; }
        public CombatRole PreferredRole { get; set; }
    }
    
    /// <summary>
    /// Information about active formation.
    /// </summary>
    public class FormationInfo
    {
        public FormationType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public IntVec3 Center { get; set; }
        public float Rotation { get; set; }
        public int PositionCount { get; set; }
    }
}

