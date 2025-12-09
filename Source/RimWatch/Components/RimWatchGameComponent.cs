using RimWatch.Settings;
using RimWatch.Utils;
using Verse;

namespace RimWatch.Components
{
    /// <summary>
    /// GameComponent for storing per-save RimWatch settings.
    /// Automatically saves/loads settings with the save file.
    /// By default, per-save settings are ENABLED.
    /// </summary>
    public class RimWatchGameComponent : GameComponent
    {
        // Settings version for future migrations
        private int _settingsVersion = 1;
        
        // Master toggle for per-save settings (TRUE BY DEFAULT!)
        private bool _usePerSaveSettings = true;
        
        // Automation Categories (8) - ALL DISABLED BY DEFAULT
        private bool _buildingEnabled = false;
        private bool _workEnabled = false;
        private bool _farmingEnabled = false;
        private bool _defenseEnabled = false;
        private bool _tradeEnabled = false;
        private bool _medicalEnabled = false;
        private bool _socialEnabled = false;
        private bool _researchEnabled = false;
        
        // Building Details (8) - ALL DISABLED BY DEFAULT
        private bool _buildBeds = false;
        private bool _buildKitchen = false;
        private bool _buildPower = false;
        private bool _buildStorage = false;
        private bool _buildWorkshops = false;
        private bool _buildResearch = false;
        private bool _buildDefenses = false;
        private bool _buildRooms = false;
        
        // Farming Details (4) - ALL DISABLED BY DEFAULT
        private bool _autoPlantCrops = false;
        private bool _autoHarvest = false;
        private bool _autoTameAnimals = false;
        private bool _autoButcherAnimals = false;
        
        // Defense Details (4) - ALL DISABLED BY DEFAULT
        private bool _autoDraftColonists = false;
        private bool _autoEquipWeapons = false;
        private bool _autoEquipArmor = false;
        private bool _autoPositionDefenders = false;
        
        // Advanced Settings (6)
        private string _storytellerType = "Balanced";
        private bool _enableDebugLog = false;
        private int _tickInterval = 60;
        private bool _autoEnableAutopilot = false;
        private bool _useManualPriorities = true;
        private bool _fileLoggingEnabled = false;
        
        // Logging Settings (10)
        private bool _enableGlobalLogging = true;
        private bool _debugModeEnabled = false;
        private BuildingLogLevel _buildingLogLevel = BuildingLogLevel.Moderate;
        private SystemLogLevel _workLogLevel = SystemLogLevel.Moderate;
        private SystemLogLevel _farmingLogLevel = SystemLogLevel.Moderate;
        private SystemLogLevel _defenseLogLevel = SystemLogLevel.Moderate;
        private SystemLogLevel _medicalLogLevel = SystemLogLevel.Moderate;
        private SystemLogLevel _tradeLogLevel = SystemLogLevel.Moderate;
        private SystemLogLevel _resourceLogLevel = SystemLogLevel.Moderate;
        private SystemLogLevel _colonistCommandsLogLevel = SystemLogLevel.Moderate;
        private SystemLogLevel _colonyDevelopmentLogLevel = SystemLogLevel.Moderate;
        private SystemLogLevel _constructionLogLevel = SystemLogLevel.Moderate;
        
        // Debug & ML Systems (10) - ALL DISABLED BY DEFAULT
        private bool _enableDebugOverlay = false;
        private DebugOverlayMode _debugOverlayMode = DebugOverlayMode.Zones;
        private bool _enableDecisionLogging = false;
        private bool _gameSpeedControlEnabled = false;
        private bool _apparelAutomationEnabled = false;
        private bool _weaponAutomationEnabled = false;
        private bool _colonistCommandsEnabled = false;
        private bool _productionAutomationEnabled = false;
        private bool _constructionCommandsEnabled = false;
        private bool _decisionAnalyzerEnabled = false;
        private bool _colonyPredictorEnabled = false;
        private bool _playerStyleAnalyzerEnabled = false;
        
        // ML Configuration (3)
        private float _mlLearningRate = 0.1f;
        private float _predictionSensitivity = 0.7f;
        private int _mlAnalysisInterval = 60000;
        
        // Game Speed Settings (4)
        private TimeSpeed _idleSpeed = TimeSpeed.Ultrafast;
        private TimeSpeed _workSpeed = TimeSpeed.Fast;
        private TimeSpeed _combatSpeed = TimeSpeed.Normal;
        private bool _autoUnpause = true;
        
        // Hierarchical Settings (~20) - ALL DISABLED BY DEFAULT
        private bool _useSmartOutfits = false;
        private bool _useEmergencySchedules = false;
        private bool _useMoodBasedSchedules = false;
        private bool _useSeasonalSchedules = false;
        private bool _useDynamicWorkPriorities = false;
        private bool _autoDetectModWorkTypes = false;
        private bool _autoRelocateOutdoorBeds = false;
        private bool _autoInstallStoredBeds = false;
        private bool _buildBedrooms = false;
        private bool _buildKitchens = false;
        private bool _buildStorageRooms = false;
        private bool _buildFreezer = false;
        private bool _useNightOwlSchedules = false;
        private bool _useEmergencyScheduleType = false;
        private bool _useMoodBasedScheduleType = false;
        private bool _useSmartApparelMode = false;
        private bool _useAutoOutfitPolicies = false;
        private bool _useCombatVsCivilianClothing = false;
        
        // Track if settings were loaded from save file
        private bool _settingsLoadedFromSave = false;

        public RimWatchGameComponent(Game game) : base()
        {
        }

        /// <summary>
        /// Property to access the per-save settings toggle
        /// </summary>
        public bool UsePerSaveSettings
        {
            get => _usePerSaveSettings;
            set
            {
                if (_usePerSaveSettings != value)
                {
                    _usePerSaveSettings = value;
                    RimWatchLogger.Info($"[GameComponent] Per-save settings {(value ? "ENABLED" : "DISABLED")}");
                    
                    if (value)
                    {
                        // Copy current global settings to per-save
                        CopyFromGlobalSettings();
                        RimWatchLogger.Info("[GameComponent] Copied global settings to per-save");
                    }
                }
            }
        }

        /// <summary>
        /// Called after game is fully loaded. Apply per-save settings to global if enabled.
        /// </summary>
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            
            if (_usePerSaveSettings)
            {
                ApplyToGlobalSettings();
                
                if (_settingsLoadedFromSave)
                {
                    RimWatchLogger.Info("✓ [GameComponent] Applied per-save settings to global (loaded from save)");
                }
                else
                {
                    RimWatchLogger.Info("✓ [GameComponent] Per-save settings initialized (new save or migrated)");
                }
            }
            else
            {
                RimWatchLogger.Info("[GameComponent] Using global settings (per-save disabled)");
            }
        }

        /// <summary>
        /// Save/load per-save settings to/from save file
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            
            // Version for future migrations
            Scribe_Values.Look(ref _settingsVersion, "settingsVersion", 1);
            
            // Master toggle (TRUE BY DEFAULT!)
            Scribe_Values.Look(ref _usePerSaveSettings, "usePerSaveSettings", true);
            
            // Only save/load settings if per-save is enabled
            if (Scribe.mode == LoadSaveMode.LoadingVars && _usePerSaveSettings)
            {
                _settingsLoadedFromSave = true;
            }
            
            if (_usePerSaveSettings)
            {
                // Automation Categories (8) - ALL DISABLED BY DEFAULT
                Scribe_Values.Look(ref _buildingEnabled, "buildingEnabled", false);
                Scribe_Values.Look(ref _workEnabled, "workEnabled", false);
                Scribe_Values.Look(ref _farmingEnabled, "farmingEnabled", false);
                Scribe_Values.Look(ref _defenseEnabled, "defenseEnabled", false);
                Scribe_Values.Look(ref _tradeEnabled, "tradeEnabled", false);
                Scribe_Values.Look(ref _medicalEnabled, "medicalEnabled", false);
                Scribe_Values.Look(ref _socialEnabled, "socialEnabled", false);
                Scribe_Values.Look(ref _researchEnabled, "researchEnabled", false);
                
                // Building Details (8) - ALL DISABLED BY DEFAULT
                Scribe_Values.Look(ref _buildBeds, "buildBeds", false);
                Scribe_Values.Look(ref _buildKitchen, "buildKitchen", false);
                Scribe_Values.Look(ref _buildPower, "buildPower", false);
                Scribe_Values.Look(ref _buildStorage, "buildStorage", false);
                Scribe_Values.Look(ref _buildWorkshops, "buildWorkshops", false);
                Scribe_Values.Look(ref _buildResearch, "buildResearch", false);
                Scribe_Values.Look(ref _buildDefenses, "buildDefenses", false);
                Scribe_Values.Look(ref _buildRooms, "buildRooms", false);
                
                // Farming Details (4) - ALL DISABLED BY DEFAULT
                Scribe_Values.Look(ref _autoPlantCrops, "autoPlantCrops", false);
                Scribe_Values.Look(ref _autoHarvest, "autoHarvest", false);
                Scribe_Values.Look(ref _autoTameAnimals, "autoTameAnimals", false);
                Scribe_Values.Look(ref _autoButcherAnimals, "autoButcherAnimals", false);
                
                // Defense Details (4) - ALL DISABLED BY DEFAULT
                Scribe_Values.Look(ref _autoDraftColonists, "autoDraftColonists", false);
                Scribe_Values.Look(ref _autoEquipWeapons, "autoEquipWeapons", false);
                Scribe_Values.Look(ref _autoEquipArmor, "autoEquipArmor", false);
                Scribe_Values.Look(ref _autoPositionDefenders, "autoPositionDefenders", false);
                
                // Advanced Settings (6)
                Scribe_Values.Look(ref _storytellerType, "storytellerType", "Balanced");
                Scribe_Values.Look(ref _enableDebugLog, "enableDebugLog", false);
                Scribe_Values.Look(ref _tickInterval, "tickInterval", 60);
                Scribe_Values.Look(ref _autoEnableAutopilot, "autoEnableAutopilot", false);
                Scribe_Values.Look(ref _useManualPriorities, "useManualPriorities", true);
                Scribe_Values.Look(ref _fileLoggingEnabled, "fileLoggingEnabled", false);
                
                // Logging Settings (10)
                Scribe_Values.Look(ref _enableGlobalLogging, "enableGlobalLogging", true);
                Scribe_Values.Look(ref _debugModeEnabled, "debugModeEnabled", false);
                Scribe_Values.Look(ref _buildingLogLevel, "buildingLogLevel", BuildingLogLevel.Moderate);
                Scribe_Values.Look(ref _workLogLevel, "workLogLevel", SystemLogLevel.Moderate);
                Scribe_Values.Look(ref _farmingLogLevel, "farmingLogLevel", SystemLogLevel.Moderate);
                Scribe_Values.Look(ref _defenseLogLevel, "defenseLogLevel", SystemLogLevel.Moderate);
                Scribe_Values.Look(ref _medicalLogLevel, "medicalLogLevel", SystemLogLevel.Moderate);
                Scribe_Values.Look(ref _tradeLogLevel, "tradeLogLevel", SystemLogLevel.Moderate);
                Scribe_Values.Look(ref _resourceLogLevel, "resourceLogLevel", SystemLogLevel.Moderate);
                Scribe_Values.Look(ref _colonistCommandsLogLevel, "colonistCommandsLogLevel", SystemLogLevel.Moderate);
                Scribe_Values.Look(ref _colonyDevelopmentLogLevel, "colonyDevelopmentLogLevel", SystemLogLevel.Moderate);
                Scribe_Values.Look(ref _constructionLogLevel, "constructionLogLevel", SystemLogLevel.Moderate);
                
                // Debug & ML Systems (10) - ALL DISABLED BY DEFAULT
                Scribe_Values.Look(ref _enableDebugOverlay, "enableDebugOverlay", false);
                Scribe_Values.Look(ref _debugOverlayMode, "debugOverlayMode", DebugOverlayMode.Zones);
                Scribe_Values.Look(ref _enableDecisionLogging, "enableDecisionLogging", false);
                Scribe_Values.Look(ref _gameSpeedControlEnabled, "gameSpeedControlEnabled", false);
                Scribe_Values.Look(ref _apparelAutomationEnabled, "apparelAutomationEnabled", false);
                Scribe_Values.Look(ref _weaponAutomationEnabled, "weaponAutomationEnabled", false);
                Scribe_Values.Look(ref _colonistCommandsEnabled, "colonistCommandsEnabled", false);
                Scribe_Values.Look(ref _productionAutomationEnabled, "productionAutomationEnabled", false);
                Scribe_Values.Look(ref _constructionCommandsEnabled, "constructionCommandsEnabled", false);
                Scribe_Values.Look(ref _decisionAnalyzerEnabled, "decisionAnalyzerEnabled", false);
                Scribe_Values.Look(ref _colonyPredictorEnabled, "colonyPredictorEnabled", false);
                Scribe_Values.Look(ref _playerStyleAnalyzerEnabled, "playerStyleAnalyzerEnabled", false);
                
                // ML Configuration (3)
                Scribe_Values.Look(ref _mlLearningRate, "mlLearningRate", 0.1f);
                Scribe_Values.Look(ref _predictionSensitivity, "predictionSensitivity", 0.7f);
                Scribe_Values.Look(ref _mlAnalysisInterval, "mlAnalysisInterval", 60000);
                
                // Game Speed Settings (4)
                Scribe_Values.Look(ref _idleSpeed, "idleSpeed", TimeSpeed.Ultrafast);
                Scribe_Values.Look(ref _workSpeed, "workSpeed", TimeSpeed.Fast);
                Scribe_Values.Look(ref _combatSpeed, "combatSpeed", TimeSpeed.Normal);
                Scribe_Values.Look(ref _autoUnpause, "autoUnpause", true);
                
                // Hierarchical Settings (~20) - ALL DISABLED BY DEFAULT
                Scribe_Values.Look(ref _useSmartOutfits, "useSmartOutfits", false);
                Scribe_Values.Look(ref _useEmergencySchedules, "useEmergencySchedules", false);
                Scribe_Values.Look(ref _useMoodBasedSchedules, "useMoodBasedSchedules", false);
                Scribe_Values.Look(ref _useSeasonalSchedules, "useSeasonalSchedules", false);
                Scribe_Values.Look(ref _useDynamicWorkPriorities, "useDynamicWorkPriorities", false);
                Scribe_Values.Look(ref _autoDetectModWorkTypes, "autoDetectModWorkTypes", false);
                Scribe_Values.Look(ref _autoRelocateOutdoorBeds, "autoRelocateOutdoorBeds", false);
                Scribe_Values.Look(ref _autoInstallStoredBeds, "autoInstallStoredBeds", false);
                Scribe_Values.Look(ref _buildBedrooms, "buildBedrooms", false);
                Scribe_Values.Look(ref _buildKitchens, "buildKitchens", false);
                Scribe_Values.Look(ref _buildStorageRooms, "buildStorageRooms", false);
                Scribe_Values.Look(ref _buildFreezer, "buildFreezer", false);
                Scribe_Values.Look(ref _useNightOwlSchedules, "useNightOwlSchedules", false);
                Scribe_Values.Look(ref _useEmergencyScheduleType, "useEmergencyScheduleType", false);
                Scribe_Values.Look(ref _useMoodBasedScheduleType, "useMoodBasedScheduleType", false);
                Scribe_Values.Look(ref _useSmartApparelMode, "useSmartApparelMode", false);
                Scribe_Values.Look(ref _useAutoOutfitPolicies, "useAutoOutfitPolicies", false);
                Scribe_Values.Look(ref _useCombatVsCivilianClothing, "useCombatVsCivilianClothing", false);
            }
        }

        /// <summary>
        /// Copy current per-save settings to global RimWatchSettings
        /// </summary>
        public void ApplyToGlobalSettings()
        {
            var settings = RimWatchMod.Settings;
            if (settings == null) return;
            
            // Automation Categories (8)
            settings.buildingEnabled = _buildingEnabled;
            settings.workEnabled = _workEnabled;
            settings.farmingEnabled = _farmingEnabled;
            settings.defenseEnabled = _defenseEnabled;
            settings.tradeEnabled = _tradeEnabled;
            settings.medicalEnabled = _medicalEnabled;
            settings.socialEnabled = _socialEnabled;
            settings.researchEnabled = _researchEnabled;
            
            // Building Details (8)
            settings.buildBeds = _buildBeds;
            settings.buildKitchen = _buildKitchen;
            settings.buildPower = _buildPower;
            settings.buildStorage = _buildStorage;
            settings.buildWorkshops = _buildWorkshops;
            settings.buildResearch = _buildResearch;
            settings.buildDefenses = _buildDefenses;
            settings.buildRooms = _buildRooms;
            
            // Farming Details (4)
            settings.autoPlantCrops = _autoPlantCrops;
            settings.autoHarvest = _autoHarvest;
            settings.autoTameAnimals = _autoTameAnimals;
            settings.autoButcherAnimals = _autoButcherAnimals;
            
            // Defense Details (4)
            settings.autoDraftColonists = _autoDraftColonists;
            settings.autoEquipWeapons = _autoEquipWeapons;
            settings.autoEquipArmor = _autoEquipArmor;
            settings.autoPositionDefenders = _autoPositionDefenders;
            
            // Advanced Settings (6)
            settings.storytellerType = _storytellerType;
            settings.enableDebugLog = _enableDebugLog;
            settings.tickInterval = _tickInterval;
            settings.autoEnableAutopilot = _autoEnableAutopilot;
            settings.useManualPriorities = _useManualPriorities;
            settings.fileLoggingEnabled = _fileLoggingEnabled;
            
            // Logging Settings (10)
            settings.enableGlobalLogging = _enableGlobalLogging;
            settings.debugModeEnabled = _debugModeEnabled;
            settings.buildingLogLevel = _buildingLogLevel;
            settings.workLogLevel = _workLogLevel;
            settings.farmingLogLevel = _farmingLogLevel;
            settings.defenseLogLevel = _defenseLogLevel;
            settings.medicalLogLevel = _medicalLogLevel;
            settings.tradeLogLevel = _tradeLogLevel;
            settings.resourceLogLevel = _resourceLogLevel;
            settings.colonistCommandsLogLevel = _colonistCommandsLogLevel;
            settings.colonyDevelopmentLogLevel = _colonyDevelopmentLogLevel;
            settings.constructionLogLevel = _constructionLogLevel;
            
            // Debug & ML Systems (10)
            settings.enableDebugOverlay = _enableDebugOverlay;
            settings.debugOverlayMode = _debugOverlayMode;
            settings.enableDecisionLogging = _enableDecisionLogging;
            settings.gameSpeedControlEnabled = _gameSpeedControlEnabled;
            settings.apparelAutomationEnabled = _apparelAutomationEnabled;
            settings.weaponAutomationEnabled = _weaponAutomationEnabled;
            settings.colonistCommandsEnabled = _colonistCommandsEnabled;
            settings.productionAutomationEnabled = _productionAutomationEnabled;
            settings.constructionCommandsEnabled = _constructionCommandsEnabled;
            settings.decisionAnalyzerEnabled = _decisionAnalyzerEnabled;
            settings.colonyPredictorEnabled = _colonyPredictorEnabled;
            settings.playerStyleAnalyzerEnabled = _playerStyleAnalyzerEnabled;
            
            // ML Configuration (3)
            settings.mlLearningRate = _mlLearningRate;
            settings.predictionSensitivity = _predictionSensitivity;
            settings.mlAnalysisInterval = _mlAnalysisInterval;
            
            // Game Speed Settings (4)
            settings.idleSpeed = _idleSpeed;
            settings.workSpeed = _workSpeed;
            settings.combatSpeed = _combatSpeed;
            settings.autoUnpause = _autoUnpause;
            
            // Hierarchical Settings (~20)
            settings.useSmartOutfits = _useSmartOutfits;
            settings.useEmergencySchedules = _useEmergencySchedules;
            settings.useMoodBasedSchedules = _useMoodBasedSchedules;
            settings.useSeasonalSchedules = _useSeasonalSchedules;
            settings.useDynamicWorkPriorities = _useDynamicWorkPriorities;
            settings.autoDetectModWorkTypes = _autoDetectModWorkTypes;
            settings.autoRelocateOutdoorBeds = _autoRelocateOutdoorBeds;
            settings.autoInstallStoredBeds = _autoInstallStoredBeds;
            settings.buildBedrooms = _buildBedrooms;
            settings.buildKitchens = _buildKitchens;
            settings.buildStorageRooms = _buildStorageRooms;
            settings.buildFreezer = _buildFreezer;
            settings.useNightOwlSchedules = _useNightOwlSchedules;
            settings.useEmergencyScheduleType = _useEmergencyScheduleType;
            settings.useMoodBasedScheduleType = _useMoodBasedScheduleType;
            settings.useSmartApparelMode = _useSmartApparelMode;
            settings.useAutoOutfitPolicies = _useAutoOutfitPolicies;
            settings.useCombatVsCivilianClothing = _useCombatVsCivilianClothing;
            
            // Apply to Core
            settings.ApplyToCore();
        }

        /// <summary>
        /// Copy current global RimWatchSettings to per-save settings
        /// </summary>
        public void CopyFromGlobalSettings()
        {
            var settings = RimWatchMod.Settings;
            if (settings == null) return;
            
            // Automation Categories (8)
            _buildingEnabled = settings.buildingEnabled;
            _workEnabled = settings.workEnabled;
            _farmingEnabled = settings.farmingEnabled;
            _defenseEnabled = settings.defenseEnabled;
            _tradeEnabled = settings.tradeEnabled;
            _medicalEnabled = settings.medicalEnabled;
            _socialEnabled = settings.socialEnabled;
            _researchEnabled = settings.researchEnabled;
            
            // Building Details (8)
            _buildBeds = settings.buildBeds;
            _buildKitchen = settings.buildKitchen;
            _buildPower = settings.buildPower;
            _buildStorage = settings.buildStorage;
            _buildWorkshops = settings.buildWorkshops;
            _buildResearch = settings.buildResearch;
            _buildDefenses = settings.buildDefenses;
            _buildRooms = settings.buildRooms;
            
            // Farming Details (4)
            _autoPlantCrops = settings.autoPlantCrops;
            _autoHarvest = settings.autoHarvest;
            _autoTameAnimals = settings.autoTameAnimals;
            _autoButcherAnimals = settings.autoButcherAnimals;
            
            // Defense Details (4)
            _autoDraftColonists = settings.autoDraftColonists;
            _autoEquipWeapons = settings.autoEquipWeapons;
            _autoEquipArmor = settings.autoEquipArmor;
            _autoPositionDefenders = settings.autoPositionDefenders;
            
            // Advanced Settings (6)
            _storytellerType = settings.storytellerType;
            _enableDebugLog = settings.enableDebugLog;
            _tickInterval = settings.tickInterval;
            _autoEnableAutopilot = settings.autoEnableAutopilot;
            _useManualPriorities = settings.useManualPriorities;
            _fileLoggingEnabled = settings.fileLoggingEnabled;
            
            // Logging Settings (10)
            _enableGlobalLogging = settings.enableGlobalLogging;
            _debugModeEnabled = settings.debugModeEnabled;
            _buildingLogLevel = settings.buildingLogLevel;
            _workLogLevel = settings.workLogLevel;
            _farmingLogLevel = settings.farmingLogLevel;
            _defenseLogLevel = settings.defenseLogLevel;
            _medicalLogLevel = settings.medicalLogLevel;
            _tradeLogLevel = settings.tradeLogLevel;
            _resourceLogLevel = settings.resourceLogLevel;
            _colonistCommandsLogLevel = settings.colonistCommandsLogLevel;
            _colonyDevelopmentLogLevel = settings.colonyDevelopmentLogLevel;
            _constructionLogLevel = settings.constructionLogLevel;
            
            // Debug & ML Systems (10)
            _enableDebugOverlay = settings.enableDebugOverlay;
            _debugOverlayMode = settings.debugOverlayMode;
            _enableDecisionLogging = settings.enableDecisionLogging;
            _gameSpeedControlEnabled = settings.gameSpeedControlEnabled;
            _apparelAutomationEnabled = settings.apparelAutomationEnabled;
            _weaponAutomationEnabled = settings.weaponAutomationEnabled;
            _colonistCommandsEnabled = settings.colonistCommandsEnabled;
            _productionAutomationEnabled = settings.productionAutomationEnabled;
            _constructionCommandsEnabled = settings.constructionCommandsEnabled;
            _decisionAnalyzerEnabled = settings.decisionAnalyzerEnabled;
            _colonyPredictorEnabled = settings.colonyPredictorEnabled;
            _playerStyleAnalyzerEnabled = settings.playerStyleAnalyzerEnabled;
            
            // ML Configuration (3)
            _mlLearningRate = settings.mlLearningRate;
            _predictionSensitivity = settings.predictionSensitivity;
            _mlAnalysisInterval = settings.mlAnalysisInterval;
            
            // Game Speed Settings (4)
            _idleSpeed = settings.idleSpeed;
            _workSpeed = settings.workSpeed;
            _combatSpeed = settings.combatSpeed;
            _autoUnpause = settings.autoUnpause;
            
            // Hierarchical Settings (~20)
            _useSmartOutfits = settings.useSmartOutfits;
            _useEmergencySchedules = settings.useEmergencySchedules;
            _useMoodBasedSchedules = settings.useMoodBasedSchedules;
            _useSeasonalSchedules = settings.useSeasonalSchedules;
            _useDynamicWorkPriorities = settings.useDynamicWorkPriorities;
            _autoDetectModWorkTypes = settings.autoDetectModWorkTypes;
            _autoRelocateOutdoorBeds = settings.autoRelocateOutdoorBeds;
            _autoInstallStoredBeds = settings.autoInstallStoredBeds;
            _buildBedrooms = settings.buildBedrooms;
            _buildKitchens = settings.buildKitchens;
            _buildStorageRooms = settings.buildStorageRooms;
            _buildFreezer = settings.buildFreezer;
            _useNightOwlSchedules = settings.useNightOwlSchedules;
            _useEmergencyScheduleType = settings.useEmergencyScheduleType;
            _useMoodBasedScheduleType = settings.useMoodBasedScheduleType;
            _useSmartApparelMode = settings.useSmartApparelMode;
            _useAutoOutfitPolicies = settings.useAutoOutfitPolicies;
            _useCombatVsCivilianClothing = settings.useCombatVsCivilianClothing;
        }
    }
}
