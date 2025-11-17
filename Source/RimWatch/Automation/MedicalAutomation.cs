using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWatch.Automation
{
    /// <summary>
    /// 🏥 Medical Automation - автоматическое управление медициной.
    /// Мониторит здоровье колонистов, управляет лечением и медицинскими ресурсами.
    /// </summary>
    public static class MedicalAutomation
    {
        private static int _tickCounter = 0;
        private static int _emergencyTickCounter = 0;
        private static bool _isEnabled = false;
        private const int UpdateInterval = 600; // Обновление каждые 10 секунд (600 тиков)
        private const int EmergencyCheckInterval = 120; // Проверка экстренных случаев каждые 2 секунды (120 тиков)
        
        // v0.8.4: State-based logging to prevent spam for same patients
        private class PatientState
        {
            public bool WasDowned;
            public bool WasBleeding;
            public bool WasCritical;
            public int LastLogTick;
        }
        private static readonly Dictionary<string, PatientState> _patientStates = new Dictionary<string, PatientState>();
        private const int PatientLogCooldown = 1800; // 30 секунд между логами для одного пациента

        /// <summary>
        /// Включена ли автоматизация медицины.
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                RimWatchLogger.Info($"MedicalAutomation: {(value ? "Enabled" : "Disabled")}");
            }
        }

        /// <summary>
        /// Вызывается каждый тик для обновления автоматизации.
        /// </summary>
        public static void Tick()
        {
            if (!IsEnabled) return;
            if (!RimWatchCore.AutopilotEnabled) return;

            // ✅ NEW: Check for medical emergencies every 2 seconds
            _emergencyTickCounter++;
            if (_emergencyTickCounter >= EmergencyCheckInterval)
            {
                _emergencyTickCounter = 0;
                HandleMedicalEmergencies();
            }

            _tickCounter++;
            if (_tickCounter >= UpdateInterval)
            {
                _tickCounter = 0;
                RimWatchLogger.Info("[MedicalAutomation] Tick! Running medical check...");
                ManageMedical();
            }
        }
        
        /// <summary>
        /// ✅ NEW: Handles critical medical emergencies - downed/bleeding colonists.
        /// This runs every 2 seconds to ensure immediate response.
        /// </summary>
        private static void HandleMedicalEmergencies()
        {
            // v0.8.3: Log execution start
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            Map map = Find.CurrentMap;
            if (map == null) return;

            // v0.8.4: Find emergencies with PRIORITY SORTING
            // Priority: Downed+Bleeding > Downed > Critical Bleeding > Low Health
            var emergencies = map.mapPawns.FreeColonistsSpawned
                .Where(p => p.Downed || 
                           (p.health?.hediffSet?.BleedRateTotal ?? 0f) > 0.05f ||
                           p.health.summaryHealth.SummaryHealthPercent < 0.3f)
                .OrderByDescending(p => {
                    // Priority score: higher = more urgent
                    float score = 0f;
                    
                    if (p.Downed)
                    {
                        score += 1000f; // Downed is top priority
                        if ((p.health?.hediffSet?.BleedRateTotal ?? 0f) > 0.1f)
                            score += 500f; // Downed + bleeding = CRITICAL
                    }
                    
                    float bleedRate = p.health?.hediffSet?.BleedRateTotal ?? 0f;
                    if (bleedRate > 0.3f)
                        score += 300f; // Heavy bleeding
                    else if (bleedRate > 0.1f)
                        score += 150f; // Moderate bleeding
                    else if (bleedRate > 0.05f)
                        score += 50f; // Light bleeding
                    
                    // Low health bonus
                    float healthPercent = p.health.summaryHealth.SummaryHealthPercent;
                    if (healthPercent < 0.2f)
                        score += 100f; // Very low health
                    else if (healthPercent < 0.3f)
                        score += 50f; // Low health
                    
                    return score;
                })
                .ToList();

            if (emergencies.Count == 0) return;
            
            int currentTick = Find.TickManager.TicksGame;
            
            // v0.8.5: Only log emergency detection when count changes significantly
            bool shouldLogEmergency = false;
            string emergencyKey = "medical_emergency_active";
            
            if (!_patientStates.ContainsKey(emergencyKey))
            {
                _patientStates[emergencyKey] = new PatientState { LastLogTick = 0 };
                shouldLogEmergency = true;
            }
            else
            {
                int ticksSinceLastLog = currentTick - _patientStates[emergencyKey].LastLogTick;
                if (ticksSinceLastLog > 3600) // Log at most once per minute
                {
                    shouldLogEmergency = true;
                }
            }
            
            if (shouldLogEmergency)
            {
                // v0.8.3: Log emergency detection to JSON only
                RimWatchLogger.LogDecision("MedicalAutomation", "EmergencyDetected", new Dictionary<string, object>
                {
                    { "emergencyCount", emergencies.Count },
                    { "downed", emergencies.Count(p => p.Downed) },
                    { "bleeding", emergencies.Count(p => (p.health?.hediffSet?.BleedRateTotal ?? 0f) > 0.05f) },
                    { "critical", emergencies.Count(p => p.health.summaryHealth.SummaryHealthPercent < 0.3f) }
                });
                
                _patientStates[emergencyKey].LastLogTick = currentTick;
            }
            
            foreach (Pawn emergency in emergencies)
            {
                string pid = emergency.ThingID;
                bool isDowned = emergency.Downed;
                bool isBleeding = emergency.health.hediffSet.BleedRateTotal > 0.05f;
                bool isCritical = emergency.health.summaryHealth.SummaryHealthPercent < 0.3f;
                
                string status = isDowned ? "DOWNED" : 
                               isBleeding ? "BLEEDING" : 
                               "CRITICAL";
                
                // v0.8.4: State-based logging - only log when state changes or after cooldown
                if (!_patientStates.TryGetValue(pid, out PatientState prevState))
                {
                    prevState = new PatientState();
                    _patientStates[pid] = prevState;
                }
                
                bool stateChanged = prevState.WasDowned != isDowned ||
                                   prevState.WasBleeding != isBleeding ||
                                   prevState.WasCritical != isCritical;
                
                bool cooldownExpired = currentTick - prevState.LastLogTick > PatientLogCooldown;
                
                if (stateChanged || cooldownExpired)
                {
                    // v0.8.5: Log only when state changes or after 30 seconds
                    float bleedRate = emergency.health.hediffSet.BleedRateTotal;
                    
                    RimWatchLogger.LogStateChange(
                        "MedicalAutomation",
                        $"{prevState.WasDowned}|{prevState.WasBleeding}|{prevState.WasCritical}",
                        $"{isDowned}|{isBleeding}|{isCritical}",
                        $"{emergency.LabelShort}: {status}");
                    
                    RimWatchLogger.LogDecision("MedicalAutomation", "PatientEmergency", new Dictionary<string, object>
                    {
                        { "patient", emergency.LabelShort },
                        { "status", status },
                        { "healthPercent", emergency.health.summaryHealth.SummaryHealthPercent },
                        { "bleedRate", bleedRate },
                        { "downed", isDowned },
                        { "stateChanged", stateChanged }
                    });
                    
                    // v0.8.5: Removed Info log - already logged via LogDecision above
                    
                    // Update state
                    prevState.WasDowned = isDowned;
                    prevState.WasBleeding = isBleeding;
                    prevState.WasCritical = isCritical;
                    prevState.LastLogTick = currentTick;
                }
            }

            // Activate emergency response
            int doctorsAssigned = 0;
            var capableColonists = map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Downed && 
                           !p.Dead &&
                           !p.InMentalState &&
                           p.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) == true &&
                           p.workSettings != null)
                .ToList();

            foreach (Pawn colonist in capableColonists)
            {
                // Enable and prioritize Doctor work
                int currentDoctorPriority = colonist.workSettings.GetPriority(WorkTypeDefOf.Doctor);
                if (currentDoctorPriority != 1)
                {
                    colonist.workSettings.SetPriority(WorkTypeDefOf.Doctor, 1);
                    doctorsAssigned++;
                }
                
                // Also ensure firefighting is enabled (fires can cause emergencies)
                int currentFirefightingPriority = colonist.workSettings.GetPriority(WorkTypeDefOf.Firefighter);
                if (currentFirefightingPriority == 0)
                {
                    colonist.workSettings.SetPriority(WorkTypeDefOf.Firefighter, 2);
                }
            }

            if (doctorsAssigned > 0)
            {
                // v0.8.3: Log execution end with results
                stopwatch.Stop();
                RimWatchLogger.LogExecutionEnd("MedicalAutomation", "HandleMedicalEmergencies", true, stopwatch.ElapsedMilliseconds,
                    $"Assigned {doctorsAssigned} doctors for {emergencies.Count} emergencies");
                
                // v0.8.5: Use throttled warning to avoid spam
                RimWatchLogger.WarningThrottledByKey(
                    "medical_doctors_assigned",
                    $"🚨 MedicalAutomation: Emergency mode activated! Assigned {doctorsAssigned} colonists to Doctor (priority 1)",
                    3600); // Log at most once per minute
            }
            else
            {
                // v0.8.3: Log failure to assign doctors
                stopwatch.Stop();
                RimWatchLogger.LogFailure("MedicalAutomation", "HandleMedicalEmergencies", "No capable colonists to assign",
                    new Dictionary<string, object>
                    {
                        { "emergencies", emergencies.Count },
                        { "capableColonists", capableColonists.Count }
                    });
            }
        }

        /// <summary>
        /// Manages medical operations.
        /// </summary>
        private static void ManageMedical()
        {
            // v0.8.3: Log execution start
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            Map map = Find.CurrentMap;
            if (map == null) return;

            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.ToList();
            if (colonists.Count == 0) return;
            
            RimWatchLogger.LogExecutionStart("MedicalAutomation", "ManageMedical", new Dictionary<string, object>
            {
                { "colonists", colonists.Count }
            });

            MedicalStatus status = AnalyzeMedicalStatus(map, colonists);
            
            // v0.8.3: Log medical status analysis
            RimWatchLogger.LogDecision("MedicalAutomation", "MedicalStatusAnalysis", new Dictionary<string, object>
            {
                { "criticallyInjured", status.CriticallyInjured },
                { "injured", status.Injured },
                { "sick", status.Sick },
                { "medicineCount", status.MedicineCount },
                { "hasHospital", status.HasHospital }
            });

            // Report critical patients
            if (status.CriticallyInjured > 0)
            {
                RimWatchLogger.Info($"MedicalAutomation: 🚨 {status.CriticallyInjured} critically injured colonists!");
            }

            if (status.Injured > 0)
            {
                RimWatchLogger.Info($"MedicalAutomation: ⚠️ {status.Injured} injured colonists need treatment");
            }

            if (status.Sick > 0)
            {
                RimWatchLogger.Info($"MedicalAutomation: ⚠️ {status.Sick} sick colonists");
            }

            // Check medical supplies
            CheckMedicalSupplies(map, status);

            // Check hospital beds
            if (status.HospitalBeds == 0 && colonists.Count > 3)
            {
                RimWatchLogger.Info("MedicalAutomation: ℹ️ No hospital beds - medical treatment will be slower");
            }

            // All healthy
            if (status.Injured == 0 && status.Sick == 0)
            {
                RimWatchLogger.Debug($"MedicalAutomation: All colonists healthy ✓ (Medicine: {status.MedicineCount})");
            }

            // **NEW: Execute medical actions**
            // Note: Medical operations are complex and risky to automate
            // We'll implement basic logging for now
            AutoScheduleOperations(map, colonists);
            
            // v0.8.3: Log execution end
            stopwatch.Stop();
            RimWatchLogger.LogExecutionEnd("MedicalAutomation", "ManageMedical", true, stopwatch.ElapsedMilliseconds,
                $"Critical={status.CriticallyInjured}, Injured={status.Injured}, Sick={status.Sick}");
        }

        /// <summary>
        /// Analyzes medical status.
        /// </summary>
        private static MedicalStatus AnalyzeMedicalStatus(Map map, List<Pawn> colonists)
        {
            MedicalStatus status = new MedicalStatus();

            foreach (Pawn colonist in colonists)
            {
                if (colonist.health == null) continue;

                // Check if injured
                if (colonist.health.HasHediffsNeedingTend())
                {
                    status.Injured++;

                    // Check if critically injured (below 50% health)
                    float healthPercent = colonist.health.summaryHealth.SummaryHealthPercent;
                    if (healthPercent < 0.5f)
                    {
                        status.CriticallyInjured++;
                    }
                }

                // Check if sick (disease)
                if (colonist.health.hediffSet.HasImmunizableNotImmuneHediff())
                {
                    status.Sick++;
                }
            }

            // Count medicine
            status.MedicineCount = map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine).Count;

            // Count hospital beds
            status.HospitalBeds = map.listerBuildings.allBuildingsColonist
                .Count(b => b is Building_Bed bed && 
                           bed.def.building.bed_humanlike && 
                           bed.GetRoom()?.Role == RoomRoleDefOf.Hospital);

            return status;
        }

        /// <summary>
        /// Checks medical supply levels.
        /// </summary>
        private static void CheckMedicalSupplies(Map map, MedicalStatus status)
        {
            int colonistCount = map.mapPawns.FreeColonistsSpawnedCount;

            if (status.MedicineCount == 0)
            {
                RimWatchLogger.Info("MedicalAutomation: ⚠️ NO MEDICINE! Colonists will heal slowly");
            }
            else if (status.MedicineCount < colonistCount * 2)
            {
                RimWatchLogger.Info($"MedicalAutomation: ℹ️ Low medicine ({status.MedicineCount}) - consider producing more");
            }
        }

        /// <summary>
        /// Structure for medical status.
        /// </summary>
        private class MedicalStatus
        {
            public int Injured { get; set; } = 0;
            public int CriticallyInjured { get; set; } = 0;
            public int Sick { get; set; } = 0;
            public int MedicineCount { get; set; } = 0;
            public int HospitalBeds { get; set; } = 0;
            public bool HasHospital { get; set; } = false;
        }

        // ==================== ACTION METHODS ====================

        /// <summary>
        /// Automatically manages medical care quality based on colony needs.
        /// Conservative approach - ensures colonists receive appropriate care.
        /// </summary>
        private static void AutoScheduleOperations(Map map, List<Pawn> colonists)
        {
            // Medical automation is intentionally conservative
            // We focus on ensuring proper care quality, not performing risky surgeries
            
            AutoManageMedicalCare(map, colonists);
            
            // Future: Auto-schedule operations for prosthetics, bionic replacements
            // This requires careful planning and will be implemented in v0.7+
            CheckForSurgicalNeeds(map, colonists);
        }

        /// <summary>
        /// Automatically adjusts medical care quality for colonists based on injury severity.
        /// Ensures critically injured get best care, while healthy get standard care.
        /// </summary>
        private static void AutoManageMedicalCare(Map map, List<Pawn> colonists)
        {
            try
            {
                int careChanged = 0;
                List<string> changes = new List<string>();

                foreach (Pawn colonist in colonists)
                {
                    if (colonist.health == null) continue;
                    if (colonist.playerSettings == null) continue;

                    // Determine appropriate medical care level
                    MedicalCareCategory desiredCare = DetermineMedicalCareLevel(colonist, map);
                    MedicalCareCategory currentCare = colonist.playerSettings.medCare;

                    // Only change if different
                    if (currentCare != desiredCare)
                    {
                        colonist.playerSettings.medCare = desiredCare;
                        careChanged++;
                        changes.Add($"{colonist.LabelShort}: {currentCare} → {desiredCare}");
                    }
                }

                if (careChanged > 0)
                {
                    RimWatchLogger.Info($"⚕️ MedicalAutomation: Adjusted medical care for {careChanged} colonists:");
                    foreach (string change in changes)
                    {
                        RimWatchLogger.Info($"   • {change}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("MedicalAutomation: Error in AutoManageMedicalCare", ex);
            }
        }

        /// <summary>
        /// Determines appropriate medical care level for a colonist.
        /// Prioritizes bleeding, infections, and critical conditions.
        /// </summary>
        private static MedicalCareCategory DetermineMedicalCareLevel(Pawn colonist, Map map)
        {
            // Count available medicine
            int medicineCount = map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine).Count;
            
            // If no medicine at all, use herbal medicine
            if (medicineCount == 0)
            {
                return MedicalCareCategory.HerbalOrWorse;
            }

            // Check colonist health status
            float healthPercent = colonist.health.summaryHealth.SummaryHealthPercent;
            
            // CRITICAL: Check for bleeding (most urgent)
            bool hasBleeding = colonist.health.hediffSet.hediffs
                .Any(h => h.Bleeding);
            
            // Check for infections or diseases
            bool hasDisease = colonist.health.hediffSet.HasImmunizableNotImmuneHediff();
            bool hasInfection = colonist.health.hediffSet.hediffs
                .Any(h => h.def.defName.Contains("Infection") || h.def.defName.Contains("Flu") || h.def.defName.Contains("Plague"));
            
            // Check for critical injuries
            bool hasCriticalInjury = colonist.health.HasHediffsNeedingTend() && healthPercent < 0.5f;
            
            // PRIORITY 1: Bleeding + low health = IMMEDIATE BEST CARE
            if (hasBleeding && healthPercent < 0.7f)
            {
                RimWatchLogger.Debug($"MedicalAutomation: {colonist.LabelShort} has BLEEDING (HP: {healthPercent:P0}) - elevating to BEST care");
                return MedicalCareCategory.Best;
            }

            // PRIORITY 2: Diseases/Infections = BEST CARE (to boost immunity)
            if (hasDisease || hasInfection)
            {
                if (medicineCount >= 5)
                {
                    RimWatchLogger.Debug($"MedicalAutomation: {colonist.LabelShort} has disease/infection - elevating to BEST care");
                    return MedicalCareCategory.Best;
                }
                else
                {
                    return MedicalCareCategory.NormalOrWorse;
                }
            }

            // PRIORITY 3: Critical injuries (low health)
            if (hasCriticalInjury)
            {
                // If we have good medicine stockpile, use best care
                if (medicineCount >= 10)
                {
                    return MedicalCareCategory.Best;
                }
                else
                {
                    return MedicalCareCategory.NormalOrWorse;
                }
            }

            // Injured but not critical - normal care
            if (colonist.health.HasHediffsNeedingTend())
            {
                return MedicalCareCategory.NormalOrWorse;
            }

            // Healthy colonists don't need active care, but keep doctor care available
            return MedicalCareCategory.HerbalOrWorse;
        }

        /// <summary>
        /// Checks for surgical needs and logs recommendations.
        /// Actual surgery scheduling is NOT automated for safety.
        /// </summary>
        private static void CheckForSurgicalNeeds(Map map, List<Pawn> colonists)
        {
            List<string> surgicalNeeds = new List<string>();
            List<string> prostheticNeeds = new List<string>();
            List<string> infections = new List<string>();
            
            foreach (Pawn colonist in colonists)
            {
                if (colonist.health == null) continue;

                // Check for serious bleeding
                foreach (var hediff in colonist.health.hediffSet.hediffs)
                {
                    if (hediff.Bleeding && hediff.BleedRate > 0.1f)
                    {
                        surgicalNeeds.Add($"{colonist.LabelShort}: {hediff.Part?.Label ?? "unknown"} - bleeding ({hediff.BleedRate:F2}/day)");
                    }
                    
                    // Check for infections
                    if (hediff.def.defName.Contains("Infection") || hediff.def.defName.Contains("Plague"))
                    {
                        infections.Add($"{colonist.LabelShort}: {hediff.def.label} (severity: {hediff.Severity:P0})");
                    }
                    
                    // Check for scars that can be fixed with medicine
                    if (hediff.def.defName.Contains("Scar") && hediff.Visible && hediff.Part != null)
                    {
                        // Only log major scars (brain, heart, spine)
                        string partName = hediff.Part.Label.ToLower();
                        if (partName.Contains("brain") || partName.Contains("heart") || partName.Contains("spine"))
                        {
                            surgicalNeeds.Add($"{colonist.LabelShort}: {hediff.def.label} on {hediff.Part.Label} (consider advanced surgery)");
                        }
                    }
                }

                // Check for missing limbs
                foreach (var hediff in colonist.health.hediffSet.hediffs)
                {
                    if (hediff.def.defName.Contains("Missing") && hediff.Visible && hediff.Part != null)
                    {
                        prostheticNeeds.Add($"{colonist.LabelShort}: missing {hediff.Part.Label}");
                    }
                }
            }
            
            // Check for available doctors
            int doctors = colonists.Count(c => c.workSettings != null && 
                                              c.workSettings.WorkIsActive(WorkTypeDefOf.Doctor) &&
                                              !c.Downed && !c.Dead);
            
            // Log surgical recommendations
            if (surgicalNeeds.Count > 0)
            {
                RimWatchLogger.Info($"🔪 MedicalAutomation: {surgicalNeeds.Count} colonist(s) need surgery:");
                foreach (string need in surgicalNeeds.Take(5)) // Limit to 5 to avoid spam
                {
                    RimWatchLogger.Info($"   • {need}");
                }
                
                if (doctors == 0)
                {
                    RimWatchLogger.Info("   ⚠️ NO DOCTORS AVAILABLE! Assign doctor work priority to colonists!");
                }
                else
                {
                    RimWatchLogger.Info($"   ✓ {doctors} doctor(s) available");
                }
            }
            
            // Log infection alerts
            if (infections.Count > 0)
            {
                RimWatchLogger.Info($"⚠️ MedicalAutomation: {infections.Count} colonist(s) have infections:");
                foreach (string infection in infections.Take(5))
                {
                    RimWatchLogger.Info($"   • {infection}");
                }
            }
            
            // Log prosthetic opportunities (less urgent)
            if (prostheticNeeds.Count > 0 && _tickCounter % 3 == 0) // Only log every 3rd check (30 seconds)
            {
                RimWatchLogger.Debug($"MedicalAutomation: {prostheticNeeds.Count} colonist(s) could benefit from prosthetics:");
                foreach (string need in prostheticNeeds.Take(3))
                {
                    RimWatchLogger.Debug($"   • {need}");
                }
            }

            // NOTE: Actual surgery scheduling requires:
            // 1. Finding or building a medical bed
            // 2. Checking for doctors with sufficient Medical skill
            // 3. Creating bills on medical beds
            // 4. Managing operation priority
            // This is complex and will be implemented in v0.7+
        }
    }
}
