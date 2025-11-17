using UnityEngine;
using Verse;

namespace RimWatch.Settings
{
    /// <summary>
    /// Building logging verbosity levels.
    /// </summary>
    public enum BuildingLogLevel
    {
        Minimal,    // Only successes/failures
        Moderate,   // + rejection reasons
        Verbose,    // All candidates + scoring
        Debug       // Full diagnostics
    }
    
    /// <summary>
    /// Generic per-system log level.
    /// Controls how chatty individual automation systems are.
    /// </summary>
    public enum SystemLogLevel
    {
        Off,        // Completely silent (except hard errors)
        Minimal,    // Only key info and warnings
        Moderate,   // Default amount of information
        Verbose,    // Detailed info + some debug-style traces
        Debug       // Everything the system can log
    }
    
    /// <summary>
    /// Debug overlay display modes.
    /// </summary>
    public enum DebugOverlayMode
    {
        Zones = 0,           // Show zone boundaries
        PlacementScores = 1, // Show placement scores
        Both = 2             // Show both
    }

    /// <summary>
    /// Настройки мода RimWatch
    /// </summary>
    public class RimWatchSettings : ModSettings
    {
        // Automation Categories (по умолчанию Work включен для тестирования)
        // ✅ Building enabled by default for better UX!
        public bool buildingEnabled = true;  // ✅ ВКЛЮЧЕНО для автоматического строительства
        public bool workEnabled = true;      // ✅ ВКЛЮЧЕНО для управления работой
        public bool farmingEnabled = true;   // ✅ ВКЛЮЧЕНО для фермерства
        public bool defenseEnabled = false;
        public bool tradeEnabled = false;
        public bool medicalEnabled = false;
        public bool socialEnabled = false;
        public bool researchEnabled = false;

        // ✅ NEW: Detailed building automation settings (hierarchical)
        public bool buildBeds = true;
        public bool buildKitchen = true;
        public bool buildPower = true;
        public bool buildStorage = true;
        public bool buildWorkshops = true;
        public bool buildResearch = true;
        public bool buildDefenses = true;
        public bool buildRooms = true; // Full room building (walls + doors)

        // ✅ NEW: Detailed farming settings
        public bool autoPlantCrops = true;
        public bool autoHarvest = true;
        public bool autoTameAnimals = true;
        public bool autoButcherAnimals = true;

        // ✅ NEW: Detailed defense settings
        public bool autoDraftColonists = true;
        public bool autoEquipWeapons = true;
        public bool autoEquipArmor = true;
        public bool autoPositionDefenders = true;

        // AI Storyteller
        public string storytellerType = "Balanced"; // Balanced, Aggressive, Cautious, etc.

        // Advanced Settings
        public bool enableDebugLog = false;
        public int tickInterval = 60; // Как часто AI принимает решения (в тиках)
        public bool autoEnableAutopilot = false; // Автоматически включать автопилот при загрузке
        
        // v0.8.4: Global logging master switch
        public bool enableGlobalLogging = true; // Master switch for all logging
        
        // Work Priority Settings
        public bool useManualPriorities = true; // Использовать точную настройку работы (1-4) вместо галочек
        
        // Debug Mode Settings
        public bool debugModeEnabled = false; // Включить debug режим (показывает Debug логи)
        public bool fileLoggingEnabled = false; // Включить запись логов в файл
        
        // Building Automation Logging
        public BuildingLogLevel buildingLogLevel = BuildingLogLevel.Moderate; // Уровень логирования для строительства
        
        // Per-system log levels (v0.8.4)
        public SystemLogLevel workLogLevel = SystemLogLevel.Moderate;
        public SystemLogLevel farmingLogLevel = SystemLogLevel.Moderate;
        public SystemLogLevel defenseLogLevel = SystemLogLevel.Moderate;
        public SystemLogLevel medicalLogLevel = SystemLogLevel.Moderate;
        public SystemLogLevel tradeLogLevel = SystemLogLevel.Moderate;
        public SystemLogLevel resourceLogLevel = SystemLogLevel.Moderate;
        public SystemLogLevel colonistCommandsLogLevel = SystemLogLevel.Moderate;
        public SystemLogLevel colonyDevelopmentLogLevel = SystemLogLevel.Moderate;
        public SystemLogLevel constructionLogLevel = SystemLogLevel.Moderate;
        
        // Debug Visualization & Decision Logging
        public bool enableDebugOverlay = false; // Включить визуализацию зон и scores на карте
        public DebugOverlayMode debugOverlayMode = DebugOverlayMode.Zones; // Режим отображения overlay
        public bool enableDecisionLogging = false; // Логировать AI решения в JSON файл

        // v0.8.1: New AI Systems Settings
        public bool gameSpeedControlEnabled = true;  // ✅ Adaptive game speed control
        public bool apparelAutomationEnabled = true; // ✅ Smart clothing management
        public bool weaponAutomationEnabled = true;  // ✅ Auto weapon upgrades
        public bool colonistCommandsEnabled = true;  // ✅ Emergency task priority
        public bool productionAutomationEnabled = true; // v0.8.5: ✅ Automatic bill management
        public bool constructionCommandsEnabled = true; // v0.9.0: ✅ Force assign builders to critical construction
        
        // v0.8.1: Game Speed Settings
        public TimeSpeed idleSpeed = TimeSpeed.Ultrafast; // Speed when colony is idle (default: Ultrafast)
        public TimeSpeed workSpeed = TimeSpeed.Fast;      // Speed during active work (default: Fast)
        public TimeSpeed combatSpeed = TimeSpeed.Normal;  // Speed during combat (default: Normal)
        public bool autoUnpause = true;                   // Auto-unpause when emergencies resolved

        // NEW: Hierarchical Settings
        [Unsaved]
        public SettingsTree settingsTree;
        private bool _treeInitialized = false;

        // Level 2 & 3 additional settings
        public bool useSmartOutfits = true;
        public bool useEmergencySchedules = true;
        public bool useMoodBasedSchedules = true;
        public bool useSeasonalSchedules = true;
        public bool useDynamicWorkPriorities = true;
        public bool autoDetectModWorkTypes = true;

        // Level 3: Bed management details
        public bool autoRelocateOutdoorBeds = true;
        public bool autoInstallStoredBeds = true;

        // Level 3: Room types
        public bool buildBedrooms = true;
        public bool buildKitchens = true;
        public bool buildStorageRooms = true;
        public bool buildFreezer = true;

        // Level 3: Schedule types
        public bool useNightOwlSchedules = true;
        public bool useEmergencyScheduleType = true;
        public bool useMoodBasedScheduleType = true;

        // Level 3: Armor details
        public bool useSmartApparelMode = true;
        public bool useAutoOutfitPolicies = true;
        public bool useCombatVsCivilianClothing = true;

        /// <summary>
        /// Сохранение настроек
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            // Automation Categories
            Scribe_Values.Look(ref buildingEnabled, "buildingEnabled", true);  // ✅ CHANGED: default true
            Scribe_Values.Look(ref workEnabled, "workEnabled", true);          // ✅ CHANGED: default true
            Scribe_Values.Look(ref farmingEnabled, "farmingEnabled", true);    // ✅ CHANGED: default true
            Scribe_Values.Look(ref defenseEnabled, "defenseEnabled", false);
            Scribe_Values.Look(ref tradeEnabled, "tradeEnabled", false);
            Scribe_Values.Look(ref medicalEnabled, "medicalEnabled", false);
            Scribe_Values.Look(ref socialEnabled, "socialEnabled", false);
            Scribe_Values.Look(ref researchEnabled, "researchEnabled", false);
            
            // ✅ CRITICAL: Reinitialize tree after loading from XML!
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Utils.RimWatchLogger.Info("[Settings] Loading settings from XML - will reinitialize tree");
                _treeInitialized = false; // Force tree rebuild
            }

            // Detailed Building Settings
            Scribe_Values.Look(ref buildBeds, "buildBeds", true);
            Scribe_Values.Look(ref buildKitchen, "buildKitchen", true);
            Scribe_Values.Look(ref buildPower, "buildPower", true);
            Scribe_Values.Look(ref buildStorage, "buildStorage", true);
            Scribe_Values.Look(ref buildWorkshops, "buildWorkshops", true);
            Scribe_Values.Look(ref buildResearch, "buildResearch", true);
            Scribe_Values.Look(ref buildDefenses, "buildDefenses", true);
            Scribe_Values.Look(ref buildRooms, "buildRooms", true);

            // Detailed Farming Settings
            Scribe_Values.Look(ref autoPlantCrops, "autoPlantCrops", true);
            Scribe_Values.Look(ref autoHarvest, "autoHarvest", true);
            Scribe_Values.Look(ref autoTameAnimals, "autoTameAnimals", true);
            Scribe_Values.Look(ref autoButcherAnimals, "autoButcherAnimals", true);

            // Detailed Defense Settings
            Scribe_Values.Look(ref autoDraftColonists, "autoDraftColonists", true);
            Scribe_Values.Look(ref autoEquipWeapons, "autoEquipWeapons", true);
            Scribe_Values.Look(ref autoEquipArmor, "autoEquipArmor", true);
            Scribe_Values.Look(ref autoPositionDefenders, "autoPositionDefenders", true);

            // AI Storyteller
            Scribe_Values.Look(ref storytellerType, "storytellerType", "Balanced");

            // Advanced Settings
            Scribe_Values.Look(ref enableDebugLog, "enableDebugLog", false);
            Scribe_Values.Look(ref tickInterval, "tickInterval", 60);
            Scribe_Values.Look(ref autoEnableAutopilot, "autoEnableAutopilot", false);
            Scribe_Values.Look(ref enableGlobalLogging, "enableGlobalLogging", true); // v0.8.4
            
            // Work Priority Settings
            Scribe_Values.Look(ref useManualPriorities, "useManualPriorities", true);
            
            // Debug Mode Settings
            Scribe_Values.Look(ref debugModeEnabled, "debugModeEnabled", false);
            Scribe_Values.Look(ref fileLoggingEnabled, "fileLoggingEnabled", false);
            
            // Building Automation Logging
            Scribe_Values.Look(ref buildingLogLevel, "buildingLogLevel", BuildingLogLevel.Moderate);
            
            // Per-system log levels
            Scribe_Values.Look(ref workLogLevel, "workLogLevel", SystemLogLevel.Moderate);
            Scribe_Values.Look(ref farmingLogLevel, "farmingLogLevel", SystemLogLevel.Moderate);
            Scribe_Values.Look(ref defenseLogLevel, "defenseLogLevel", SystemLogLevel.Moderate);
            Scribe_Values.Look(ref medicalLogLevel, "medicalLogLevel", SystemLogLevel.Moderate);
            Scribe_Values.Look(ref tradeLogLevel, "tradeLogLevel", SystemLogLevel.Moderate);
            Scribe_Values.Look(ref resourceLogLevel, "resourceLogLevel", SystemLogLevel.Moderate);
            Scribe_Values.Look(ref colonistCommandsLogLevel, "colonistCommandsLogLevel", SystemLogLevel.Moderate);
            Scribe_Values.Look(ref colonyDevelopmentLogLevel, "colonyDevelopmentLogLevel", SystemLogLevel.Moderate);
            Scribe_Values.Look(ref constructionLogLevel, "constructionLogLevel", SystemLogLevel.Moderate);
            
            // Debug Visualization & Decision Logging
            Scribe_Values.Look(ref enableDebugOverlay, "enableDebugOverlay", false);
            Scribe_Values.Look(ref debugOverlayMode, "debugOverlayMode", DebugOverlayMode.Zones);
            Scribe_Values.Look(ref enableDecisionLogging, "enableDecisionLogging", false);

            // Level 2 & 3 settings
            Scribe_Values.Look(ref useSmartOutfits, "useSmartOutfits", true);
            Scribe_Values.Look(ref useEmergencySchedules, "useEmergencySchedules", true);
            Scribe_Values.Look(ref useMoodBasedSchedules, "useMoodBasedSchedules", true);
            Scribe_Values.Look(ref useSeasonalSchedules, "useSeasonalSchedules", true);
            Scribe_Values.Look(ref useDynamicWorkPriorities, "useDynamicWorkPriorities", true);
            Scribe_Values.Look(ref autoDetectModWorkTypes, "autoDetectModWorkTypes", true);

            // Level 3: Bed management
            Scribe_Values.Look(ref autoRelocateOutdoorBeds, "autoRelocateOutdoorBeds", true);
            Scribe_Values.Look(ref autoInstallStoredBeds, "autoInstallStoredBeds", true);

            // Level 3: Room types
            Scribe_Values.Look(ref buildBedrooms, "buildBedrooms", true);
            Scribe_Values.Look(ref buildKitchens, "buildKitchens", true);
            Scribe_Values.Look(ref buildStorageRooms, "buildStorageRooms", true);
            Scribe_Values.Look(ref buildFreezer, "buildFreezer", true);

            // Level 3: Schedule types
            Scribe_Values.Look(ref useNightOwlSchedules, "useNightOwlSchedules", true);
            Scribe_Values.Look(ref useEmergencyScheduleType, "useEmergencyScheduleType", true);
            Scribe_Values.Look(ref useMoodBasedScheduleType, "useMoodBasedScheduleType", true);

            // Level 3: Armor details
            Scribe_Values.Look(ref useSmartApparelMode, "useSmartApparelMode", true);
            Scribe_Values.Look(ref useAutoOutfitPolicies, "useAutoOutfitPolicies", true);
            Scribe_Values.Look(ref useCombatVsCivilianClothing, "useCombatVsCivilianClothing", true);
        }

        /// <summary>
        /// Применить настройки к RimWatchCore
        /// </summary>
        public void ApplyToCore()
        {
            Utils.RimWatchLogger.Info($"[Settings] ApplyToCore() called!");
            Utils.RimWatchLogger.Info($"[Settings] Work={workEnabled}, Building={buildingEnabled}, Farming={farmingEnabled}");
            Utils.RimWatchLogger.Info($"[Settings] Defense={defenseEnabled}, Trade={tradeEnabled}, Medical={medicalEnabled}");
            Utils.RimWatchLogger.Info($"[Settings] Social={socialEnabled}, Research={researchEnabled}");

            // Apply to Core flags
            Core.RimWatchCore.BuildingEnabled = buildingEnabled;
            Core.RimWatchCore.WorkEnabled = workEnabled;
            Core.RimWatchCore.FarmingEnabled = farmingEnabled;
            Core.RimWatchCore.DefenseEnabled = defenseEnabled;
            Core.RimWatchCore.TradeEnabled = tradeEnabled;
            Core.RimWatchCore.MedicalEnabled = medicalEnabled;
            Core.RimWatchCore.SocialEnabled = socialEnabled;
            Core.RimWatchCore.ResearchEnabled = researchEnabled;

            // CRITICAL: Apply to Automation IsEnabled flags
            Automation.BuildingAutomation.IsEnabled = buildingEnabled;
            Automation.WorkAutomation.IsEnabled = workEnabled;
            Automation.FarmingAutomation.IsEnabled = farmingEnabled;
            Automation.DefenseAutomation.IsEnabled = defenseEnabled;
            Automation.TradeAutomation.IsEnabled = tradeEnabled;
            Automation.MedicalAutomation.IsEnabled = medicalEnabled;
            Automation.SocialAutomation.IsEnabled = socialEnabled;
            Automation.ResearchAutomation.IsEnabled = researchEnabled;

            // Apply debug mode settings to logger
            Utils.RimWatchLogger.DebugModeEnabled = debugModeEnabled;
            Utils.RimWatchLogger.FileLoggingEnabled = fileLoggingEnabled;
            
            // Apply decision logging settings
            Utils.DecisionLogger.IsEnabled = enableDecisionLogging;

            Utils.RimWatchLogger.Info($"[Settings] Settings applied to Core AND Automations!");
            Utils.RimWatchLogger.Info($"[Settings] Debug Mode: {debugModeEnabled}, File Logging: {fileLoggingEnabled}");
            Utils.RimWatchLogger.Info($"[Settings] Debug Overlay: {enableDebugOverlay}, Decision Logging: {enableDecisionLogging}");

            // TODO: Применить рассказчика и другие настройки
        }

        /// <summary>
        /// Initializes the hierarchical settings tree from flat bool fields.
        /// IMPORTANT: Called lazily when language system is ready (not in constructor!)
        /// </summary>
        public void InitializeSettingsTree()
        {
            if (_treeInitialized && settingsTree != null)
            {
                Utils.RimWatchLogger.Debug("[Settings] Tree already initialized, skipping");
                return;
            }

            // Safety check: don't initialize if language system isn't ready yet
            if (Verse.LanguageDatabase.activeLanguage == null)
            {
                Utils.RimWatchLogger.Debug("[Settings] Language not ready yet, deferring tree initialization");
                return;
            }

            Utils.RimWatchLogger.Info("[Settings] Initializing settings tree...");
            Utils.RimWatchLogger.Info($"[Settings] Current flat values: Building={buildingEnabled}, Work={workEnabled}, Farming={farmingEnabled}");

            settingsTree = new SettingsTree();
            
            // LEVEL 1: Automation Categories
            settingsTree.AddNode(new SettingNode("building", "RimWatch.Settings.Building.Name".Translate(), "RimWatch.Settings.Building.Desc".Translate(), 1)
            {
                Enabled = buildingEnabled,
                OnToggle = (enabled) => { buildingEnabled = enabled; OnSettingChanged("building", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("work", "RimWatch.Settings.Work.Name".Translate(), "RimWatch.Settings.Work.Desc".Translate(), 1)
            {
                Enabled = workEnabled,
                OnToggle = (enabled) => { workEnabled = enabled; OnSettingChanged("work", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("farming", "RimWatch.Settings.Farming.Name".Translate(), "RimWatch.Settings.Farming.Desc".Translate(), 1)
            {
                Enabled = farmingEnabled,
                OnToggle = (enabled) => { farmingEnabled = enabled; OnSettingChanged("farming", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("defense", "RimWatch.Settings.Defense.Name".Translate(), "RimWatch.Settings.Defense.Desc".Translate(), 1)
            {
                Enabled = defenseEnabled,
                OnToggle = (enabled) => { defenseEnabled = enabled; OnSettingChanged("defense", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("medical", "RimWatch.Settings.Medical.Name".Translate(), "RimWatch.Settings.Medical.Desc".Translate(), 1)
            {
                Enabled = medicalEnabled,
                OnToggle = (enabled) => { medicalEnabled = enabled; OnSettingChanged("medical", enabled); }
            });

            // LEVEL 2: Building sub-categories
            settingsTree.AddNode(new SettingNode("building-beds", "RimWatch.Settings.Building.Beds.Name".Translate(), "RimWatch.Settings.Building.Beds.Desc".Translate(), 2, "building")
            {
                Enabled = buildBeds,
                OnToggle = (enabled) => { buildBeds = enabled; OnSettingChanged("building-beds", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("building-rooms", "RimWatch.Settings.Building.Rooms.Name".Translate(), "RimWatch.Settings.Building.Rooms.Desc".Translate(), 2, "building")
            {
                Enabled = buildRooms,
                OnToggle = (enabled) => { buildRooms = enabled; OnSettingChanged("building-rooms", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("building-power", "RimWatch.Settings.Building.Power.Name".Translate(), "RimWatch.Settings.Building.Power.Desc".Translate(), 2, "building")
            {
                Enabled = buildPower,
                OnToggle = (enabled) => { buildPower = enabled; OnSettingChanged("building-power", enabled); }
            });

            // LEVEL 3: Bed management details
            settingsTree.AddNode(new SettingNode("beds-relocate", "RimWatch.Settings.Beds.Relocate.Name".Translate(), "RimWatch.Settings.Beds.Relocate.Desc".Translate(), 3, "building-beds")
            {
                Enabled = autoRelocateOutdoorBeds,
                OnToggle = (enabled) => { autoRelocateOutdoorBeds = enabled; OnSettingChanged("beds-relocate", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("beds-install", "RimWatch.Settings.Beds.Install.Name".Translate(), "RimWatch.Settings.Beds.Install.Desc".Translate(), 3, "building-beds")
            {
                Enabled = autoInstallStoredBeds,
                OnToggle = (enabled) => { autoInstallStoredBeds = enabled; OnSettingChanged("beds-install", enabled); }
            });

            // LEVEL 3: Room types
            settingsTree.AddNode(new SettingNode("rooms-bedrooms", "RimWatch.Settings.Rooms.Bedrooms.Name".Translate(), "RimWatch.Settings.Rooms.Bedrooms.Desc".Translate(), 3, "building-rooms")
            {
                Enabled = buildBedrooms,
                OnToggle = (enabled) => { buildBedrooms = enabled; OnSettingChanged("rooms-bedrooms", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("rooms-kitchen", "RimWatch.Settings.Rooms.Kitchen.Name".Translate(), "RimWatch.Settings.Rooms.Kitchen.Desc".Translate(), 3, "building-rooms")
            {
                Enabled = buildKitchens,
                OnToggle = (enabled) => { buildKitchens = enabled; OnSettingChanged("rooms-kitchen", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("rooms-storage", "RimWatch.Settings.Rooms.Storage.Name".Translate(), "RimWatch.Settings.Rooms.Storage.Desc".Translate(), 3, "building-rooms")
            {
                Enabled = buildStorageRooms,
                OnToggle = (enabled) => { buildStorageRooms = enabled; OnSettingChanged("rooms-storage", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("rooms-freezer", "RimWatch.Settings.Rooms.Freezer.Name".Translate(), "RimWatch.Settings.Rooms.Freezer.Desc".Translate(), 3, "building-rooms")
            {
                Enabled = buildFreezer,
                OnToggle = (enabled) => { buildFreezer = enabled; OnSettingChanged("rooms-freezer", enabled); }
            });

            // LEVEL 2: Work features
            settingsTree.AddNode(new SettingNode("work-priorities", "RimWatch.Settings.Work.Priorities.Name".Translate(), "RimWatch.Settings.Work.Priorities.Desc".Translate(), 2, "work")
            {
                Enabled = useManualPriorities,
                OnToggle = (enabled) => { useManualPriorities = enabled; OnSettingChanged("work-priorities", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("work-schedules", "RimWatch.Settings.Work.Schedules.Name".Translate(), "RimWatch.Settings.Work.Schedules.Desc".Translate(), 2, "work")
            {
                Enabled = useEmergencySchedules,
                OnToggle = (enabled) => { useEmergencySchedules = enabled; OnSettingChanged("work-schedules", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("work-dynamic", "RimWatch.Settings.Work.Dynamic.Name".Translate(), "RimWatch.Settings.Work.Dynamic.Desc".Translate(), 2, "work")
            {
                Enabled = useDynamicWorkPriorities,
                OnToggle = (enabled) => { useDynamicWorkPriorities = enabled; OnSettingChanged("work-dynamic", enabled); }
            });

            // LEVEL 3: Schedule types
            settingsTree.AddNode(new SettingNode("schedules-nightowl", "RimWatch.Settings.Schedules.NightOwl.Name".Translate(), "RimWatch.Settings.Schedules.NightOwl.Desc".Translate(), 3, "work-schedules")
            {
                Enabled = useNightOwlSchedules,
                OnToggle = (enabled) => { useNightOwlSchedules = enabled; OnSettingChanged("schedules-nightowl", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("schedules-emergency", "RimWatch.Settings.Schedules.Emergency.Name".Translate(), "RimWatch.Settings.Schedules.Emergency.Desc".Translate(), 3, "work-schedules")
            {
                Enabled = useEmergencyScheduleType,
                OnToggle = (enabled) => { useEmergencyScheduleType = enabled; OnSettingChanged("schedules-emergency", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("schedules-mood", "RimWatch.Settings.Schedules.Mood.Name".Translate(), "RimWatch.Settings.Schedules.Mood.Desc".Translate(), 3, "work-schedules")
            {
                Enabled = useMoodBasedScheduleType,
                OnToggle = (enabled) => { useMoodBasedScheduleType = enabled; OnSettingChanged("schedules-mood", enabled); }
            });

            // LEVEL 2: Defense features
            settingsTree.AddNode(new SettingNode("defense-draft", "RimWatch.Settings.Defense.Draft.Name".Translate(), "RimWatch.Settings.Defense.Draft.Desc".Translate(), 2, "defense")
            {
                Enabled = autoDraftColonists,
                OnToggle = (enabled) => { autoDraftColonists = enabled; OnSettingChanged("defense-draft", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("defense-weapons", "RimWatch.Settings.Defense.Weapons.Name".Translate(), "RimWatch.Settings.Defense.Weapons.Desc".Translate(), 2, "defense")
            {
                Enabled = autoEquipWeapons,
                OnToggle = (enabled) => { autoEquipWeapons = enabled; OnSettingChanged("defense-weapons", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("defense-armor", "RimWatch.Settings.Defense.Armor.Name".Translate(), "RimWatch.Settings.Defense.Armor.Desc".Translate(), 2, "defense")
            {
                Enabled = autoEquipArmor,
                OnToggle = (enabled) => { autoEquipArmor = enabled; OnSettingChanged("defense-armor", enabled); }
            });

            // LEVEL 3: Armor details
            settingsTree.AddNode(new SettingNode("armor-smart", "RimWatch.Settings.Armor.Smart.Name".Translate(), "RimWatch.Settings.Armor.Smart.Desc".Translate(), 3, "defense-armor")
            {
                Enabled = useSmartApparelMode,
                OnToggle = (enabled) => { useSmartApparelMode = enabled; OnSettingChanged("armor-smart", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("armor-policies", "RimWatch.Settings.Armor.Policies.Name".Translate(), "RimWatch.Settings.Armor.Policies.Desc".Translate(), 3, "defense-armor")
            {
                Enabled = useAutoOutfitPolicies,
                OnToggle = (enabled) => { useAutoOutfitPolicies = enabled; OnSettingChanged("armor-policies", enabled); }
            });
            
            settingsTree.AddNode(new SettingNode("armor-combat", "RimWatch.Settings.Armor.Combat.Name".Translate(), "RimWatch.Settings.Armor.Combat.Desc".Translate(), 3, "defense-armor")
            {
                Enabled = useCombatVsCivilianClothing,
                OnToggle = (enabled) => { useCombatVsCivilianClothing = enabled; OnSettingChanged("armor-combat", enabled); }
            });

            _treeInitialized = true;
            Utils.RimWatchLogger.Info("[Settings] Settings tree initialized with hierarchical structure");
        }

        /// <summary>
        /// Syncs tree state back to flat bool fields.
        /// </summary>
        public void SyncTreeToFlat()
        {
            if (settingsTree == null) return;

            // LEVEL 1: Main categories
            var buildingNode = settingsTree.GetNode("building");
            if (buildingNode != null) buildingEnabled = buildingNode.Enabled;

            var workNode = settingsTree.GetNode("work");
            if (workNode != null) workEnabled = workNode.Enabled;

            var farmingNode = settingsTree.GetNode("farming");
            if (farmingNode != null) farmingEnabled = farmingNode.Enabled;

            var defenseNode = settingsTree.GetNode("defense");
            if (defenseNode != null) defenseEnabled = defenseNode.Enabled;

            var medicalNode = settingsTree.GetNode("medical");
            if (medicalNode != null) medicalEnabled = medicalNode.Enabled;

            // LEVEL 2: Building sub-categories
            var bedsNode = settingsTree.GetNode("building-beds");
            if (bedsNode != null) buildBeds = bedsNode.Enabled;

            var roomsNode = settingsTree.GetNode("building-rooms");
            if (roomsNode != null) buildRooms = roomsNode.Enabled;
            
            var powerNode = settingsTree.GetNode("building-power");
            if (powerNode != null) buildPower = powerNode.Enabled;

            // LEVEL 2: Work sub-categories
            var workPrioritiesNode = settingsTree.GetNode("work-priorities");
            if (workPrioritiesNode != null) useManualPriorities = workPrioritiesNode.Enabled;
            
            var schedulesNode = settingsTree.GetNode("work-schedules");
            if (schedulesNode != null) useEmergencySchedules = schedulesNode.Enabled;

            // LEVEL 2: Defense sub-categories
            var autoDraftNode = settingsTree.GetNode("defense-draft");
            if (autoDraftNode != null) autoDraftColonists = autoDraftNode.Enabled;
            
            var weaponsNode = settingsTree.GetNode("defense-weapons");
            if (weaponsNode != null) autoEquipWeapons = weaponsNode.Enabled;
            
            var armorNode = settingsTree.GetNode("defense-armor");
            if (armorNode != null) autoEquipArmor = armorNode.Enabled;

            // LEVEL 3: Bed management details
            var relocateBedsNode = settingsTree.GetNode("beds-relocate");
            if (relocateBedsNode != null) autoRelocateOutdoorBeds = relocateBedsNode.Enabled;
            
            var installBedsNode = settingsTree.GetNode("beds-install");
            if (installBedsNode != null) autoInstallStoredBeds = installBedsNode.Enabled;
            
            // LEVEL 3: Room types
            var bedroomsNode = settingsTree.GetNode("rooms-bedrooms");
            if (bedroomsNode != null) buildBedrooms = bedroomsNode.Enabled;
            
            var kitchensNode = settingsTree.GetNode("rooms-kitchens");
            if (kitchensNode != null) buildKitchens = kitchensNode.Enabled;
            
            var storageNode = settingsTree.GetNode("rooms-storage");
            if (storageNode != null) buildStorageRooms = storageNode.Enabled;
            
            var freezerNode = settingsTree.GetNode("rooms-freezer");
            if (freezerNode != null) buildFreezer = freezerNode.Enabled;

            // LEVEL 3: Schedule types  
            var nightOwlNode = settingsTree.GetNode("schedules-nightowl");
            if (nightOwlNode != null) useNightOwlSchedules = nightOwlNode.Enabled;
            
            var emergencyScheduleNode = settingsTree.GetNode("schedules-emergency");
            if (emergencyScheduleNode != null) useEmergencyScheduleType = emergencyScheduleNode.Enabled;
            
            var moodScheduleNode = settingsTree.GetNode("schedules-mood");
            if (moodScheduleNode != null) useMoodBasedScheduleType = moodScheduleNode.Enabled;

            // LEVEL 3: Armor details
            var smartApparelNode = settingsTree.GetNode("armor-smart");
            if (smartApparelNode != null) useSmartApparelMode = smartApparelNode.Enabled;

            Utils.RimWatchLogger.Debug("[Settings] Synced tree state to flat bools");
        }

        /// <summary>
        /// Called when a setting node is toggled.
        /// </summary>
        public void OnSettingChanged(string nodeId, bool newValue)
        {
            Utils.RimWatchLogger.Debug($"[Settings] Node '{nodeId}' flat value updated to {newValue}");
            
            // ✅ OPTIMIZATION: This method is now ONLY for updating flat bool values!
            // The actual flat bool assignment is done by OnToggle lambdas above.
            // ApplyToCore() and Write() are called ONCE by the UI after all OnToggle calls complete.
            // This prevents multiple redundant saves during hierarchical updates.
        }

        /// <summary>
        /// Resets all settings to defaults.
        /// </summary>
        public void ResetToDefaults()
        {
            buildingEnabled = false;
            workEnabled = false;
            farmingEnabled = false;
            defenseEnabled = false;
            tradeEnabled = false;
            medicalEnabled = false;
            socialEnabled = false;
            researchEnabled = false;
            
            autoEnableAutopilot = false;
            
            buildBeds = true;
            buildRooms = true;
            buildPower = true;
            buildStorage = true;
            buildDefenses = true;
            
            useManualPriorities = true;
            
            autoDraftColonists = true;
            autoEquipWeapons = true;
            autoEquipArmor = true;
            
            debugModeEnabled = false;
            fileLoggingEnabled = false;
            buildingLogLevel = BuildingLogLevel.Moderate;
            
            // Per-system log levels
            workLogLevel = SystemLogLevel.Moderate;
            farmingLogLevel = SystemLogLevel.Moderate;
            defenseLogLevel = SystemLogLevel.Moderate;
            medicalLogLevel = SystemLogLevel.Moderate;
            tradeLogLevel = SystemLogLevel.Moderate;
            resourceLogLevel = SystemLogLevel.Moderate;
            colonistCommandsLogLevel = SystemLogLevel.Moderate;
            colonyDevelopmentLogLevel = SystemLogLevel.Moderate;
            constructionLogLevel = SystemLogLevel.Moderate;
            
            enableDebugOverlay = false;
            debugOverlayMode = DebugOverlayMode.Zones;
            enableDecisionLogging = false;
            
            useSmartOutfits = true;
            useEmergencySchedules = true;
            useMoodBasedSchedules = true;
            useSeasonalSchedules = true;
            useDynamicWorkPriorities = true;
            autoDetectModWorkTypes = true;
            
            autoRelocateOutdoorBeds = true;
            autoInstallStoredBeds = true;
            
            buildBedrooms = true;
            buildKitchens = true;
            buildStorageRooms = true;
            buildFreezer = true;
            
            useNightOwlSchedules = true;
            useEmergencyScheduleType = true;
            useMoodBasedScheduleType = true;
            
            useSmartApparelMode = true;
            useAutoOutfitPolicies = true;
            useCombatVsCivilianClothing = true;
            
            _treeInitialized = false;
            settingsTree = null;
            InitializeSettingsTree();
            
            ApplyToCore();
            Write();
            
            Utils.RimWatchLogger.Info("[Settings] Reset to defaults");
        }
    }
}

