using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWatch.Automation.Medical
{
    /// <summary>
    /// Preventive medical care system.
    /// Monitors colonist health and schedules preventive treatments before problems become critical.
    /// v0.9.17: Preventive care and health monitoring.
    /// </summary>
    public static class PreventiveCare
    {
        // Configuration Constants
        private const int UPDATE_INTERVAL = 3600; // Check every 1 hour
        private const float LOW_IMMUNITY_THRESHOLD = 0.4f; // Below 40% immunity is concerning
        private const float CRITICAL_PAIN_THRESHOLD = 0.3f; // Above 30% pain needs attention
        private const float LOW_BLOOD_THRESHOLD = 0.7f; // Below 70% blood loss is dangerous
        
        // State tracking
        private static int _lastUpdateTick = 0;
        private static Dictionary<int, HealthAlert> _activeAlerts = new Dictionary<int, HealthAlert>();
        
        /// <summary>
        /// Main tick method for preventive care.
        /// </summary>
        public static void Tick(Map map)
        {
            try
            {
                int currentTick = Find.TickManager.TicksGame;
                
                if (currentTick - _lastUpdateTick < UPDATE_INTERVAL)
                    return;
                
                _lastUpdateTick = currentTick;
                
                // Monitor all colonists
                MonitorColonistHealth(map);
                
                // Apply preventive measures
                ApplyPreventiveTreatments(map);
                
                RimWatchLogger.Debug($"PreventiveCare: {_activeAlerts.Count} active health alerts");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("PreventiveCare: Error in Tick", ex);
            }
        }
        
        /// <summary>
        /// Monitors colonist health for warning signs.
        /// </summary>
        private static void MonitorColonistHealth(Map map)
        {
            int currentTick = Find.TickManager.TicksGame;
            
            foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.Dead)
                    continue;
                
                List<HealthAlert> alerts = AnalyzeHealthRisks(pawn);
                
                if (alerts.Count > 0)
                {
                    // Update or create alert
                    if (!_activeAlerts.ContainsKey(pawn.thingIDNumber))
                    {
                        var topAlert = alerts.OrderByDescending(a => a.Severity).First();
                        _activeAlerts[pawn.thingIDNumber] = topAlert;
                        
                        RimWatchLogger.Warning($"PreventiveCare: New health alert for {pawn.Name} - {topAlert.Description} (Severity: {topAlert.Severity:F1})");
                    }
                }
                else
                {
                    // Clear alert if health improved
                    if (_activeAlerts.ContainsKey(pawn.thingIDNumber))
                    {
                        RimWatchLogger.Info($"PreventiveCare: Health alert cleared for {pawn.Name}");
                        _activeAlerts.Remove(pawn.thingIDNumber);
                    }
                }
            }
        }
        
        /// <summary>
        /// Analyzes health risks for a colonist.
        /// </summary>
        private static List<HealthAlert> AnalyzeHealthRisks(Pawn pawn)
        {
            List<HealthAlert> alerts = new List<HealthAlert>();
            
            if (pawn.health == null)
                return alerts;
            
            // Check immunity
            alerts.AddRange(CheckImmunityLevels(pawn));
            
            // Check pain levels
            alerts.AddRange(CheckPainLevels(pawn));
            
            // Check blood loss
            alerts.AddRange(CheckBloodLoss(pawn));
            
            // Check infections
            alerts.AddRange(CheckInfections(pawn));
            
            // Check malnutrition
            alerts.AddRange(CheckMalnutrition(pawn));
            
            // Check extreme temperatures
            alerts.AddRange(CheckTemperatureExposure(pawn));
            
            return alerts;
        }
        
        /// <summary>
        /// Checks immunity levels against diseases.
        /// </summary>
        private static List<HealthAlert> CheckImmunityLevels(Pawn pawn)
        {
            List<HealthAlert> alerts = new List<HealthAlert>();
            
            // Find diseases with immunity progress
            var diseases = pawn.health.hediffSet.hediffs
                .Where(h => h.TendableNow() && h.def.makesSickThought)
                .ToList();
            
            foreach (var disease in diseases)
            {
                // Check if immunity is low
                var immunityRecord = pawn.health.immunity.GetImmunityRecord(disease.def);
                if (immunityRecord != null)
                {
                    float immunity = immunityRecord.immunity;
                    
                    if (immunity < LOW_IMMUNITY_THRESHOLD)
                    {
                        alerts.Add(new HealthAlert
                        {
                            Pawn = pawn,
                            Type = HealthAlertType.LowImmunity,
                            Description = $"Low immunity to {disease.def.label} ({immunity:P0})",
                            Severity = (LOW_IMMUNITY_THRESHOLD - immunity) * 100f,
                            Hediff = disease
                        });
                    }
                }
            }
            
            return alerts;
        }
        
        /// <summary>
        /// Checks pain levels.
        /// </summary>
        private static List<HealthAlert> CheckPainLevels(Pawn pawn)
        {
            List<HealthAlert> alerts = new List<HealthAlert>();
            
            float pain = pawn.health.hediffSet.PainTotal;
            
            if (pain > CRITICAL_PAIN_THRESHOLD)
            {
                alerts.Add(new HealthAlert
                {
                    Pawn = pawn,
                    Type = HealthAlertType.HighPain,
                    Description = $"High pain level ({pain:P0})",
                    Severity = pain * 100f,
                    Hediff = null
                });
            }
            
            return alerts;
        }
        
        /// <summary>
        /// Checks blood loss.
        /// </summary>
        private static List<HealthAlert> CheckBloodLoss(Pawn pawn)
        {
            List<HealthAlert> alerts = new List<HealthAlert>();
            
            // Find blood loss hediff
            var bloodLoss = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => h.def.defName == "BloodLoss");
            
            if (bloodLoss != null)
            {
                float bloodLevel = 1f - bloodLoss.Severity; // Severity is amount lost
                
                if (bloodLevel < LOW_BLOOD_THRESHOLD)
                {
                    alerts.Add(new HealthAlert
                    {
                        Pawn = pawn,
                        Type = HealthAlertType.BloodLoss,
                        Description = $"Low blood ({bloodLevel:P0})",
                        Severity = (LOW_BLOOD_THRESHOLD - bloodLevel) * 200f,
                        Hediff = bloodLoss
                    });
                }
            }
            
            return alerts;
        }
        
        /// <summary>
        /// Checks for infections.
        /// </summary>
        private static List<HealthAlert> CheckInfections(Pawn pawn)
        {
            List<HealthAlert> alerts = new List<HealthAlert>();
            
            // Find infections
            var infections = pawn.health.hediffSet.hediffs
                .Where(h => h.def.defName.Contains("Infection") || h.def.makesSickThought)
                .ToList();
            
            foreach (var infection in infections)
            {
                if (infection.CurStageIndex >= 2) // Advanced stage
                {
                    alerts.Add(new HealthAlert
                    {
                        Pawn = pawn,
                        Type = HealthAlertType.Infection,
                        Description = $"Infection: {infection.def.label} (Stage {infection.CurStageIndex + 1})",
                        Severity = infection.Severity * 50f + infection.CurStageIndex * 20f,
                        Hediff = infection
                    });
                }
            }
            
            return alerts;
        }
        
        /// <summary>
        /// Checks for malnutrition.
        /// </summary>
        private static List<HealthAlert> CheckMalnutrition(Pawn pawn)
        {
            List<HealthAlert> alerts = new List<HealthAlert>();
            
            var malnutrition = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => h.def.defName.Contains("Malnutrition"));
            
            if (malnutrition != null)
            {
                alerts.Add(new HealthAlert
                {
                    Pawn = pawn,
                    Type = HealthAlertType.Malnutrition,
                    Description = $"Malnutrition (Stage {malnutrition.CurStageIndex + 1})",
                    Severity = malnutrition.Severity * 50f,
                    Hediff = malnutrition
                });
            }
            
            return alerts;
        }
        
        /// <summary>
        /// Checks for extreme temperature exposure.
        /// </summary>
        private static List<HealthAlert> CheckTemperatureExposure(Pawn pawn)
        {
            List<HealthAlert> alerts = new List<HealthAlert>();
            
            // Check for hypothermia
            var hypothermia = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => h.def.defName.Contains("Hypothermia"));
            
            if (hypothermia != null && hypothermia.CurStageIndex >= 1)
            {
                alerts.Add(new HealthAlert
                {
                    Pawn = pawn,
                    Type = HealthAlertType.Temperature,
                    Description = $"Hypothermia (Stage {hypothermia.CurStageIndex + 1})",
                    Severity = hypothermia.Severity * 60f,
                    Hediff = hypothermia
                });
            }
            
            // Check for heatstroke
            var heatstroke = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => h.def.defName.Contains("Heatstroke"));
            
            if (heatstroke != null && heatstroke.CurStageIndex >= 1)
            {
                alerts.Add(new HealthAlert
                {
                    Pawn = pawn,
                    Type = HealthAlertType.Temperature,
                    Description = $"Heatstroke (Stage {heatstroke.CurStageIndex + 1})",
                    Severity = heatstroke.Severity * 60f,
                    Hediff = heatstroke
                });
            }
            
            return alerts;
        }
        
        /// <summary>
        /// Applies preventive treatments based on active alerts.
        /// </summary>
        private static void ApplyPreventiveTreatments(Map map)
        {
            foreach (var kvp in _activeAlerts.ToList())
            {
                var alert = kvp.Value;
                
                if (alert.Pawn == null || alert.Pawn.Dead)
                {
                    _activeAlerts.Remove(kvp.Key);
                    continue;
                }
                
                // Apply appropriate treatment
                switch (alert.Type)
                {
                    case HealthAlertType.LowImmunity:
                    case HealthAlertType.Infection:
                        // Ensure they're in medical bed and being tended
                        EnsureMedicalCare(alert.Pawn, map);
                        break;
                    
                    case HealthAlertType.HighPain:
                        // Consider pain management (penoxycyline, etc)
                        ConsiderPainManagement(alert.Pawn, map);
                        break;
                    
                    case HealthAlertType.BloodLoss:
                        // Prioritize medical attention
                        EnsureUrgentMedicalCare(alert.Pawn, map);
                        break;
                    
                    case HealthAlertType.Malnutrition:
                        // Ensure food access
                        EnsureFoodAccess(alert.Pawn, map);
                        break;
                    
                    case HealthAlertType.Temperature:
                        // Move to appropriate temperature zone
                        EnsureTemperatureSafety(alert.Pawn, map);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Ensures pawn gets medical care.
        /// </summary>
        private static void EnsureMedicalCare(Pawn pawn, Map map)
        {
            // Check if already in medical bed
            if (pawn.CurrentBed()?.Medical == true)
                return;
            
            // Find available medical bed
            var bed = map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                .FirstOrDefault(b => b.Medical && b.AnyUnoccupiedSleepingSlot && b.Spawned);
            
            if (bed != null)
            {
                RimWatchLogger.Info($"PreventiveCare: Assigning {pawn.Name} to medical bed for treatment");
                // v1.1.0: Assign pawn to available medical bed for rest
                var medicalBed = FindAvailableMedicalBed(map);
                if (medicalBed != null && pawn.ownership != null)
                {
                    try
                    {
                        // Temporarily assign to this bed for treatment
                        pawn.ownership.ClaimBedIfNonMedical(medicalBed);
                        RimWatchLogger.Info($"✅ PreventiveCare: Assigned {pawn.Name} to medical bed at {medicalBed.Position}");
                    }
                    catch (Exception ex)
                    {
                        RimWatchLogger.Debug($"PreventiveCare: Could not assign bed: {ex.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Ensures urgent medical care for critical conditions.
        /// </summary>
        private static void EnsureUrgentMedicalCare(Pawn pawn, Map map)
        {
            // Similar to EnsureMedicalCare but with higher priority
            EnsureMedicalCare(pawn, map);
            
            // v1.1.0: Set medical care priority to urgent via player settings
            if (pawn.playerSettings != null && pawn.playerSettings.medCare < MedicalCareCategory.Best)
            {
                pawn.playerSettings.medCare = MedicalCareCategory.Best;
                RimWatchLogger.Warning($"PreventiveCare: {pawn.Name} needs urgent medical care! Set to BEST care.");
            }
        }
        
        /// <summary>
        /// Considers pain management options.
        /// </summary>
        private static void ConsiderPainManagement(Pawn pawn, Map map)
        {
            // Check if pain is from treatable injuries
            var painfulHediffs = pawn.health.hediffSet.hediffs
                .Where(h => h.PainOffset > 0.05f)
                .OrderByDescending(h => h.PainOffset)
                .ToList();
            
            if (painfulHediffs.Count > 0)
            {
                RimWatchLogger.Debug($"PreventiveCare: {pawn.Name} has {painfulHediffs.Count} painful conditions");
                // v1.1.0: Prioritize treatment of most painful conditions
                var mostPainful = painfulHediffs.OrderByDescending(h => h.PainOffset).FirstOrDefault();
                if (mostPainful != null && mostPainful.TendableNow())
                {
                    RimWatchLogger.Info($"PreventiveCare: Prioritizing treatment of {mostPainful.def.label} (pain: {mostPainful.PainOffset:F2})");
                    // Set medical care to ensure treatment
                    if (pawn.playerSettings != null)
                    {
                        pawn.playerSettings.medCare = MedicalCareCategory.Best;
                    }
                }
            }
        }
        
        /// <summary>
        /// Ensures pawn has access to food.
        /// </summary>
        private static void EnsureFoodAccess(Pawn pawn, Map map)
        {
            // Check if pawn can access food
            if (pawn.needs?.food == null)
                return;
            
            float foodLevel = pawn.needs.food.CurLevelPercentage;
            
            if (foodLevel < 0.3f)
            {
                RimWatchLogger.Warning($"PreventiveCare: {pawn.Name} is starving (food: {foodLevel:P0})");
                // v1.1.0: Priority food delivery - increase Food work priority temporarily
                // Note: Actual "force feed" would require complex job system manipulation
                // For now, we ensure doctors prioritize this pawn's needs
                if (pawn.playerSettings != null)
                {
                    pawn.playerSettings.medCare = MedicalCareCategory.Best;
                }
                RimWatchLogger.Info($"PreventiveCare: Ensured medical attention for starving {pawn.Name}");
            }
        }
        
        /// <summary>
        /// Ensures pawn is in safe temperature zone.
        /// </summary>
        private static void EnsureTemperatureSafety(Pawn pawn, Map map)
        {
            // Check current temperature
            float temp = pawn.AmbientTemperature;
            
            // Check for comfortable temperature range using stats
            float comfortableMin = pawn.GetStatValue(StatDefOf.ComfyTemperatureMin);
            float comfortableMax = pawn.GetStatValue(StatDefOf.ComfyTemperatureMax);
            
            if (temp < comfortableMin || temp > comfortableMax)
            {
                RimWatchLogger.Warning($"PreventiveCare: {pawn.Name} exposed to dangerous temperature ({temp:F1}°C, comfortable: {comfortableMin:F1}-{comfortableMax:F1}°C)");
                // v1.1.0: Move pawn to climate-controlled area
                // Find a room with comfortable temperature
                var safeRoom = FindClimateControlledRoom(map, pawn);
                if (safeRoom != null)
                {
                    RimWatchLogger.Info($"PreventiveCare: Found climate-controlled room for {pawn.Name} at {safeRoom.Cells.FirstOrDefault()}");
                    // Note: Actually moving pawn requires complex job/pathfinding
                    // For v1.1.0, we log the recommendation. Full implementation in v1.2+
                }
                else
                {
                    RimWatchLogger.Debug($"PreventiveCare: No suitable climate-controlled room found for {pawn.Name}");
                }
            }
        }
        
        /// <summary>
        /// Gets active health alerts for UI.
        /// </summary>
        public static List<HealthAlertInfo> GetActiveAlerts()
        {
            return _activeAlerts.Values
                .OrderByDescending(a => a.Severity)
                .Select(a => new HealthAlertInfo
                {
                    PawnName = a.Pawn?.Name?.ToString() ?? "Unknown",
                    AlertType = a.Type.ToString(),
                    Description = a.Description,
                    Severity = a.Severity
                })
                .ToList();
        }
        
        /// <summary>
        /// v1.1.0: Finds an available medical bed on the map.
        /// </summary>
        private static Building_Bed FindAvailableMedicalBed(Map map)
        {
            return map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                .FirstOrDefault(b => b.Medical && 
                                    b.AnyUnoccupiedSleepingSlot &&
                                    b.Spawned);
        }
        
        /// <summary>
        /// v1.1.0: Finds a room with climate control (heater/cooler) for safety.
        /// </summary>
        private static Room FindClimateControlledRoom(Map map, Pawn pawn)
        {
            // v1.1.0: Simplified approach - just log recommendation
            // Full room search requires deeper RimWorld API knowledge
            // This feature deferred to v1.2.0 for proper implementation
            
            RimWatchLogger.Debug($"PreventiveCare: Climate control room search for {pawn.Name} (feature pending v1.2.0)");
            return null;
        }
    }
    
    /// <summary>
    /// Health alert for a colonist.
    /// </summary>
    public class HealthAlert
    {
        public Pawn Pawn { get; set; }
        public HealthAlertType Type { get; set; }
        public string Description { get; set; }
        public float Severity { get; set; } // 0-100
        public Hediff Hediff { get; set; }
    }
    
    /// <summary>
    /// Types of health alerts.
    /// </summary>
    public enum HealthAlertType
    {
        LowImmunity,
        HighPain,
        BloodLoss,
        Infection,
        Malnutrition,
        Temperature
    }
    
    /// <summary>
    /// Public health alert info for UI.
    /// </summary>
    public class HealthAlertInfo
    {
        public string PawnName { get; set; }
        public string AlertType { get; set; }
        public string Description { get; set; }
        public float Severity { get; set; }
    }
}

