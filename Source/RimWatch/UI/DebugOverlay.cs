using RimWatch.Automation.BaseLayout;
using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWatch.UI
{
    /// <summary>
    /// Debug overlay for visualizing AI decisions and plans.
    /// v1.0: Multiple visualization layers (building plans, defense coverage, pathfinding).
    /// </summary>
    public static class DebugOverlay
    {
        // Visualization layers
        public static bool ShowBuildingPlans { get; set; } = false;
        public static bool ShowDefenseCoverage { get; set; } = false;
        public static bool ShowPathfinding { get; set; } = false;
        public static bool ShowRoomQuality { get; set; } = false;
        
        /// <summary>
        /// Draws all enabled overlay layers.
        /// </summary>
        public static void DrawOverlay()
        {
            if (Find.CurrentMap == null)
                return;
            
            Map map = Find.CurrentMap;
            
            try
            {
                if (ShowBuildingPlans)
                {
                    DrawBuildingPlans(map);
                }
                
                if (ShowDefenseCoverage)
                {
                    DrawDefenseCoverage(map);
                }
                
                if (ShowPathfinding)
                {
                    DrawPathfinding(map);
                }
                
                if (ShowRoomQuality)
                {
                    DrawRoomQuality(map);
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("DebugOverlay: Error drawing overlay", ex);
            }
        }
        
        /// <summary>
        /// Draws planned building locations.
        /// </summary>
        private static void DrawBuildingPlans(Map map)
        {
            var layout = BaseLayoutPlanner.GetCurrentLayout();
            
            if (layout == null)
                return;
            
            // Draw base center
            if (!string.IsNullOrEmpty(layout.Center))
            {
                Vector3 center = ParseIntVec3(layout.Center).ToVector3Shifted();
                GenDraw.DrawFieldEdges(new List<IntVec3> { center.ToIntVec3() }, Color.yellow);
                
                // Draw base size indicator
                float radius = layout.Size / 2f;
                GenDraw.DrawCircleOutline(center, radius, SimpleColor.Yellow);
            }
            
            // Label
            Vector3 labelPos = new Vector3(10f, Screen.height - 50f, 0f);
            Widgets.Label(new Rect(labelPos.x, labelPos.y, 300f, 30f), 
                $"Base Layout: {layout.Size}x{layout.Size}, {layout.RoomCount} rooms planned");
        }
        
        /// <summary>
        /// Draws defense coverage zones.
        /// </summary>
        private static void DrawDefenseCoverage(Map map)
        {
            // Draw defensive positions with colored overlays
            var colonists = map.mapPawns.FreeColonistsSpawned;
            
            foreach (var pawn in colonists)
            {
                if (pawn.Dead || !pawn.Spawned)
                    continue;
                
                // Draw pawn position
                Vector3 pawnPos = pawn.Position.ToVector3Shifted();
                GenDraw.DrawCircleOutline(pawnPos, 5f, SimpleColor.Green);
                
                // Draw firing range if has ranged weapon
                if (pawn.equipment?.Primary != null && pawn.equipment.Primary.def.IsRangedWeapon)
                {
                    float range = pawn.equipment.Primary.def.Verbs?.FirstOrDefault()?.range ?? 25f;
                    GenDraw.DrawCircleOutline(pawnPos, range, SimpleColor.Blue);
                }
            }
            
            // Label
            Vector3 labelPos = new Vector3(10f, Screen.height - 80f, 0f);
            Widgets.Label(new Rect(labelPos.x, labelPos.y, 300f, 30f), 
                $"Defense Coverage: {colonists.Count()} defenders");
        }
        
        /// <summary>
        /// Draws pathfinding data.
        /// </summary>
        private static void DrawPathfinding(Map map)
        {
            // Highlight high-traffic areas
            var cells = map.AllCells;
            
            int highlightCount = 0;
            foreach (var cell in cells)
            {
                if (!cell.InBounds(map))
                    continue;
                
                // Check if it's a walkable, high-traffic area (near center)
                if (cell.Standable(map) && !cell.Impassable(map))
                {
                    float distToCenter = cell.DistanceTo(map.Center);
                    
                    if (distToCenter < 20f)
                    {
                        // High traffic
                        GenDraw.DrawFieldEdges(new List<IntVec3> { cell }, new Color(1f, 0.5f, 0f, 0.2f));
                        highlightCount++;
                    }
                }
                
                if (highlightCount > 100) // Limit for performance
                    break;
            }
            
            // Label
            Vector3 labelPos = new Vector3(10f, Screen.height - 110f, 0f);
            Widgets.Label(new Rect(labelPos.x, labelPos.y, 300f, 30f), 
                "Pathfinding: Orange = High Traffic");
        }
        
        /// <summary>
        /// Draws room quality indicators.
        /// </summary>
        private static void DrawRoomQuality(Map map)
        {
            var rooms = new HashSet<Room>();
            
            // Get all rooms
            foreach (IntVec3 cell in map.AllCells)
            {
                var room = cell.GetRoom(map);
                if (room != null && !room.PsychologicallyOutdoors)
                {
                    rooms.Add(room);
                }
            }
            
            int roomCount = 0;
            foreach (var room in rooms)
            {
                if (room.CellCount < 9)
                    continue;
                
                // Get room impressiveness
                float impressiveness = room.GetStat(RoomStatDefOf.Impressiveness);
                
                // Choose color based on quality
                Color color;
                if (impressiveness >= 50f)
                    color = Color.green;
                else if (impressiveness >= 20f)
                    color = Color.yellow;
                else
                    color = Color.red;
                
                // Draw room outline
                GenDraw.DrawFieldEdges(room.Cells.Take(20).ToList(), color);
                
                roomCount++;
                if (roomCount > 10) // Limit for performance
                    break;
            }
            
            // Label
            Vector3 labelPos = new Vector3(10f, Screen.height - 140f, 0f);
            Widgets.Label(new Rect(labelPos.x, labelPos.y, 300f, 30f), 
                $"Room Quality: {roomCount} rooms (Green=Great, Yellow=OK, Red=Poor)");
        }
        
        /// <summary>
        /// Parses IntVec3 from string "(x, y, z)".
        /// </summary>
        private static IntVec3 ParseIntVec3(string str)
        {
            try
            {
                str = str.Trim('(', ')');
                var parts = str.Split(',');
                
                if (parts.Length == 3)
                {
                    int x = int.Parse(parts[0].Trim());
                    int y = 0;
                    int z = int.Parse(parts[2].Trim());
                    return new IntVec3(x, y, z);
                }
            }
            catch
            {
                // Ignore parse errors
            }
            
            return IntVec3.Invalid;
        }
    }
}

