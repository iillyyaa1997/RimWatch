using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace RimWatch.Automation.BuildingPlacement
{
    /// <summary>
    /// Designates plants blocking a building footprint for cutting.
    /// </summary>
    public static class PreClearPlanner
    {
        public static int DesignateBlockingPlants(Map map, IntVec3 origin, ThingDef def, Rot4 rot, string logLevel = "Moderate")
        {
            try
            {
                int designated = 0;
                Designator_PlantsCut cut = new Designator_PlantsCut();
                foreach (IntVec3 c in GenAdj.OccupiedRect(origin, rot, def.Size))
                {
                    if (!c.InBounds(map)) continue;
                    List<Thing> list = c.GetThingList(map);
                    foreach (Thing t in list)
                    {
                        if (t is Plant plant)
                        {
                            if (cut.CanDesignateThing(plant).Accepted)
                            {
                                cut.DesignateThing(plant);
                                designated++;
                            }
                        }
                    }
                }

                if (designated > 0 && logLevel == "Debug")
                {
                    RimWatchLogger.Debug($"PreClearPlanner: Designated {designated} plants for cutting");
                }
                return designated;
            }
            catch (Exception ex)
            {
                RimWatchLogger.Debug($"PreClearPlanner: Error during pre-clear: {ex.Message}");
                return 0;
            }
        }
    }
}


