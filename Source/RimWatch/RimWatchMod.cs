using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using RimWatch.Components;
using RimWatch.Core;
using RimWatch.Settings;
using RimWatch.Utils;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWatch
{
    /// <summary>
    /// RimWatch - ПОЛНЫЙ AI Autopilot для RimWorld 1.6
    /// AI-powered autopilot for RimWorld - watch your colony thrive
    /// </summary>
    public class RimWatchMod : Mod
    {
        public static RimWatchMod? Instance { get; private set; }
        public static RimWatchSettings Settings { get; private set; } = new RimWatchSettings();
        
        /// <summary>
        /// Get the GameComponent for per-save settings (only available in-game)
        /// </summary>
        public static RimWatchGameComponent? GameComponent
        {
            get
            {
                if (Current.Game == null) return null;
                return Current.Game.GetComponent<RimWatchGameComponent>();
            }
        }
        
        private static Harmony? _harmonyInstance;
        public static Harmony? HarmonyInstance => _harmonyInstance;
        
        // Scroll position for settings window
        private static Vector2 scrollPosition = Vector2.zero;

        public RimWatchMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<RimWatchSettings>();
            RimWatchLogger.Info("═══════════════════════════════════");
            RimWatchLogger.Info("Initializing RimWatch v0.1.0-dev");
            RimWatchLogger.Info("═══════════════════════════════════");

            try
            {
                // Инициализируем ядро
                RimWatchCore.Initialize();

                // ✅ NOTE: Settings tree will be lazily initialized on first UI draw (when language is loaded)
                // This avoids "No active language!" errors during mod loading

                // Применяем настройки к Core сразу при загрузке
                Settings.ApplyToCore();
                RimWatchLogger.Info("Initial settings applied to Core");
                
                // Apply debug settings immediately
                RimWatchLogger.DebugModeEnabled = Settings.debugModeEnabled;
                RimWatchLogger.FileLoggingEnabled = Settings.fileLoggingEnabled;
                
                if (Settings.debugModeEnabled)
                {
                    RimWatchLogger.Info("🐛 Debug Mode ENABLED");
                }
                
                if (Settings.fileLoggingEnabled)
                {
                    RimWatchLogger.Info("📝 File Logging ENABLED");
                }

                // Автовключение автопилота, если настройка активна
                if (Settings.autoEnableAutopilot)
                {
                    Core.RimWatchCore.AutopilotEnabled = true;
                    RimWatchLogger.Info("Autopilot auto-enabled (from settings)");
                }

                // Создаем Harmony instance
                _harmonyInstance = new Harmony("rimwatch.mod");
                RimWatchLogger.Info("Harmony instance created");

                // Применяем патчи
                _harmonyInstance.PatchAll();
                RimWatchLogger.Info("Harmony patches applied");

                RimWatchLogger.Info("═══════════════════════════════════");
                RimWatchLogger.Info("✓ Initialization completed successfully!");
                RimWatchLogger.Info("═══════════════════════════════════");
                RimWatchLogger.Info("⌨️ Press Shift+R in game to open RimWatch panel");
                RimWatchLogger.Info("⚙️ Or use: Esc → Options → Mod Settings → RimWatch");
                RimWatchLogger.Info("═══════════════════════════════════");
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error("✗ Critical initialization error", ex);
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Use unified settings UI
            RimWatch.UI.UnifiedSettingsUI.DrawAllSettings(inRect, Settings, isQuickPanel: false);
        }

        public override string SettingsCategory()
        {
            return "RimWatch";
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            RimWatchLogger.Info("[MOD] WriteSettings() called - settings saved to disk!");
            
            // ✅ CRITICAL FIX: Apply settings to Core after saving!
            Settings.ApplyToCore();
            
            // ✅ Sync to per-save settings if in-game and per-save is enabled
            var gameComponent = GameComponent;
            if (gameComponent != null && gameComponent.UsePerSaveSettings)
            {
                gameComponent.CopyFromGlobalSettings();
                RimWatchLogger.Info("[MOD] Synced settings to per-save (in-game)");
            }
        }
    }
}

