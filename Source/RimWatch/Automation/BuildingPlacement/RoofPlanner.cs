using RimWatch.Utils;
using RimWorld;
using System;
using Verse;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// Designates roof build/no-roof areas over building footprints.
    /// </summary>
    public static class RoofPlanner
    {
        public static void BuildRoofOver(Map map, IntVec3 origin, ThingDef def, Rot4 rot, int margin = 0, string logLevel = "Moderate")
        {
            try
            {
                Designator_AreaBuildRoof des = new Designator_AreaBuildRoof();
                foreach (IntVec3 cell in GenAdj.OccupiedRect(origin, rot, def.Size))
                {
                    DesignateWithMargin(map, des, cell, margin);
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"RoofPlanner: Error BuildRoofOver {def?.defName}: {ex.Message}");
            }
        }

        public static void RemoveRoofOver(Map map, IntVec3 origin, ThingDef def, Rot4 rot, int margin = 0, string logLevel = "Moderate")
        {
            try
            {
                Designator_AreaNoRoof des = new Designator_AreaNoRoof();
                foreach (IntVec3 cell in GenAdj.OccupiedRect(origin, rot, def.Size))
                {
                    DesignateWithMargin(map, des, cell, margin);
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"RoofPlanner: Error RemoveRoofOver {def?.defName}: {ex.Message}");
            }
        }

        private static void DesignateWithMargin(Map map, Designator_AreaBuildRoof des, IntVec3 cell, int margin)
        {
            for (int dx = -margin; dx <= margin; dx++)
            {
                for (int dz = -margin; dz <= margin; dz++)
                {
                    IntVec3 c = new IntVec3(cell.x + dx, cell.y, cell.z + dz);
                    if (!c.InBounds(map)) continue;
                    if (des.CanDesignateCell(c).Accepted)
                    {
                        des.DesignateSingleCell(c);
                    }
                }
            }
        }

        private static void DesignateWithMargin(Map map, Designator_AreaNoRoof des, IntVec3 cell, int margin)
        {
            for (int dx = -margin; dx <= margin; dx++)
            {
                for (int dz = -margin; dz <= margin; dz++)
                {
                    IntVec3 c = new IntVec3(cell.x + dx, cell.y, cell.z + dz);
                    if (!c.InBounds(map)) continue;
                    if (des.CanDesignateCell(c).Accepted)
                    {
                        des.DesignateSingleCell(c);
                    }
                }
            }
        }
    }
}


