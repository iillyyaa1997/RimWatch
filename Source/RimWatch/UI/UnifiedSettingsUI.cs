using RimWatch.Settings;
using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWatch.UI
{
    /// <summary>
    /// Unified settings UI component used by both Mod Settings and Shift+R panel.
    /// Single source of truth for all settings rendering.
    /// v1.3.1: Uses UIDesignSystem for consistent styling.
    /// </summary>
    public static class UnifiedSettingsUI
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        /// <summary>
        /// Main settings drawing method - used by both Mod Settings and Quick Panel.
        /// </summary>
        public static void DrawAllSettings(Rect inRect, RimWatchSettings settings, bool isQuickPanel = false)
        {
            // Initialize tree if needed
            if (settings.settingsTree == null)
            {
                settings.InitializeSettingsTree();
            }

            // v0.8.4: Увеличенная высота для всех настроек + логи
            float contentHeight = isQuickPanel ? 2400f : 3000f;
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);
            Rect outRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height);
            
            Widgets.BeginScrollView(outRect, ref _scrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            // === HEADER ===
            DrawHeader(listing, isQuickPanel);

            // === PER-SAVE SETTINGS (v1.3.0 - only in-game) ===
            if (Current.Game != null)
            {
                DrawPerSaveSettingsSection(listing, settings);
                listing.Gap(12f);
            }

            // === QUICK STATUS (only in quick panel) ===
            if (isQuickPanel)
            {
                DrawQuickStatus(listing, settings);
                listing.Gap(12f);
            }

            // === AUTOPILOT CONTROL ===
            DrawCollapsibleSection(listing, "RimWatch.UI.AutopilotControl".Translate(), "autopilot", () =>
            {
                DrawAutopilotSection(listing, settings);
            });

            // === HIERARCHICAL AUTOMATION TREE ===
            DrawCollapsibleSection(listing, "RimWatch.UI.Automation".Translate(), "automation", () =>
            {
                DrawAutomationTree(listing, settings);
            });

            // === v0.8.1: AI SYSTEMS ===
            DrawCollapsibleSection(listing, "Advanced AI Systems (v0.8.1)", "ai_systems", () =>
            {
                DrawAISystemsSection(listing, settings);
            });
            
            // === v1.4.0: NOTIFICATION SYSTEM ===
            DrawCollapsibleSection(listing, "Notification System", "notifications", () =>
            {
                DrawNotificationSection(listing, settings);
            });

            // === DEBUG & LOGGING ===
            DrawCollapsibleSection(listing, "RimWatch.UI.Debug".Translate(), "debug", () =>
            {
                DrawDebugSection(listing, settings);
            });

            // === VISUALIZATION ===
            DrawCollapsibleSection(listing, "RimWatch.UI.Visualization".Translate(), "visualization", () =>
            {
                DrawVisualizationSection(listing, settings);
            });

            // === ACTIONS ===
            DrawActionsSection(listing, settings);

            // === FOOTER ===
            DrawFooter(listing, isQuickPanel);

            listing.End();
            Widgets.EndScrollView();
        }

        private static Dictionary<string, bool> _sectionCollapsed = new Dictionary<string, bool>();

        private static void DrawCollapsibleSection(Listing_Standard listing, string title, string id, System.Action drawContent)
        {
            if (!_sectionCollapsed.ContainsKey(id))
            {
                _sectionCollapsed[id] = false; // Expanded by default
            }

            bool isCollapsed = _sectionCollapsed[id];

            // Header with clean card style
            Rect headerRect = listing.GetRect(UIDesignSystem.HEIGHT_Header);
            
            bool clicked = UIDesignSystem.DrawCollapsibleHeader(headerRect, title, isCollapsed);
            if (clicked)
            {
                _sectionCollapsed[id] = !isCollapsed;
            }

            listing.Gap(UIDesignSystem.SPACE_XS);

            // Content
            if (!isCollapsed)
            {
                float contentStartY = listing.CurHeight;
                try
                {
                    drawContent();
                }
                catch (System.Exception ex)
                {
                    RimWatchLogger.Error($"DrawCollapsibleSection '{id}' failed", ex);
                    GUI.color = Color.red;
                    listing.Label($"ERROR in {id}: {ex.Message}");
                    GUI.color = Color.white;
                }
                float contentEndY = listing.CurHeight;
                
                // Draw subtle background behind content
                Rect contentBg = new Rect(0f, contentStartY, listing.ColumnWidth, contentEndY - contentStartY);
                UIDesignSystem.DrawSection(contentBg);
                
                listing.Gap(UIDesignSystem.SPACE_SM);
            }
            else
            {
                listing.Gap(UIDesignSystem.SPACE_XS);
            }
        }

        private static void DrawHeader(Listing_Standard listing, bool isQuickPanel)
        {
            Rect headerRect = listing.GetRect(UIDesignSystem.HEIGHT_HeaderLarge);
            string title = isQuickPanel ? "RimWatch.UI.QuickPanel".Translate() : "RimWatch.UI.Settings".Translate();
            UIDesignSystem.DrawHeader(headerRect, title, GameFont.Medium);
            listing.Gap(UIDesignSystem.SPACE_MD);
        }

        private static void DrawQuickStatus(Listing_Standard listing, RimWatchSettings settings)
        {
            bool autopilotActive = RimWatch.Core.RimWatchCore.AutopilotEnabled;
            
            string status = autopilotActive ? "RimWatch.UI.StatusOn".Translate() : "RimWatch.UI.StatusOff".Translate();
            listing.Label("RimWatch.UI.AutopilotStatus".Translate(status));
            
            int activeModules = 0;
            if (settings.buildingEnabled) activeModules++;
            if (settings.workEnabled) activeModules++;
            if (settings.farmingEnabled) activeModules++;
            if (settings.defenseEnabled) activeModules++;
            if (settings.medicalEnabled) activeModules++;
            if (settings.socialEnabled) activeModules++;
            if (settings.researchEnabled) activeModules++;
            if (settings.tradeEnabled) activeModules++;
            
            GUI.color = UIDesignSystem.Text_Secondary;
            listing.Label("RimWatch.UI.ActiveModules".Translate(activeModules));
            GUI.color = Color.white;
            
            listing.Gap(UIDesignSystem.SPACE_SM);
        }

        private static void DrawAutopilotSection(Listing_Standard listing, RimWatchSettings settings)
        {
            bool oldAutoEnable = settings.autoEnableAutopilot;
            listing.CheckboxLabeled("RimWatch.UI.AutoEnableOnLoad".Translate(), ref settings.autoEnableAutopilot);
            
            if (oldAutoEnable != settings.autoEnableAutopilot)
            {
                settings.ApplyToCore();
                settings.Write();
            }
            
            listing.Gap(6f);
            
            if (listing.ButtonText("RimWatch.UI.ApplySettings".Translate()))
            {
                settings.ApplyToCore();
                settings.Write();
                Messages.Message("RimWatch.Message.SettingsApplied".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        private static void DrawAutomationTree(Listing_Standard listing, RimWatchSettings settings)
        {
            Rect treeRect = listing.GetRect(700f);
            HierarchicalSettingsUI.DrawSettingsTree(treeRect, settings.settingsTree, settings);
        }

        /// <summary>
        /// v0.8.1: Draw AI Systems settings (Game Speed, Apparel, Weapon, Commands).
        /// </summary>
        private static void DrawAISystemsSection(Listing_Standard listing, RimWatchSettings settings)
        {
            // Game Speed Control
            listing.CheckboxLabeled("🎮 Adaptive Game Speed Control", ref settings.gameSpeedControlEnabled, 
                "Automatically adjusts game speed based on colony events (combat, emergencies, idle time)");
            
            if (settings.gameSpeedControlEnabled)
            {
                listing.Gap(4f);
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                
                // Idle Speed
                listing.Label($"  Idle Speed: {settings.idleSpeed}");
                Rect idleRect = listing.GetRect(20f);
                idleRect.xMin += 20f;
                if (Widgets.ButtonText(idleRect, $"Change ({settings.idleSpeed})"))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    options.Add(new FloatMenuOption("Normal", () => settings.idleSpeed = TimeSpeed.Normal));
                    options.Add(new FloatMenuOption("Fast", () => settings.idleSpeed = TimeSpeed.Fast));
                    options.Add(new FloatMenuOption("Superfast", () => settings.idleSpeed = TimeSpeed.Superfast));
                    options.Add(new FloatMenuOption("Ultrafast", () => settings.idleSpeed = TimeSpeed.Ultrafast));
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                
                // Work Speed
                listing.Label($"  Work Speed: {settings.workSpeed}");
                Rect workRect = listing.GetRect(20f);
                workRect.xMin += 20f;
                if (Widgets.ButtonText(workRect, $"Change ({settings.workSpeed})"))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    options.Add(new FloatMenuOption("Normal", () => settings.workSpeed = TimeSpeed.Normal));
                    options.Add(new FloatMenuOption("Fast", () => settings.workSpeed = TimeSpeed.Fast));
                    options.Add(new FloatMenuOption("Superfast", () => settings.workSpeed = TimeSpeed.Superfast));
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                
                // Combat Speed
                listing.Label($"  Combat Speed: {settings.combatSpeed}");
                Rect combatRect = listing.GetRect(20f);
                combatRect.xMin += 20f;
                if (Widgets.ButtonText(combatRect, $"Change ({settings.combatSpeed})"))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    options.Add(new FloatMenuOption("Paused", () => settings.combatSpeed = TimeSpeed.Paused));
                    options.Add(new FloatMenuOption("Normal", () => settings.combatSpeed = TimeSpeed.Normal));
                    options.Add(new FloatMenuOption("Fast", () => settings.combatSpeed = TimeSpeed.Fast));
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                
                // Auto-unpause
                listing.CheckboxLabeled("  Auto-unpause when safe", ref settings.autoUnpause, 
                    "Automatically resume game when emergencies are resolved");
                
                GUI.color = Color.white;
            }
            
            listing.Gap(8f);
            
            // Apparel Automation
            listing.CheckboxLabeled("👔 Smart Clothing Management", ref settings.apparelAutomationEnabled,
                "Auto-equip colonists with best available apparel (quality >50%, no corpse clothes)");
            
            // Weapon Automation
            listing.CheckboxLabeled("🔫 Auto Weapon Upgrades", ref settings.weaponAutomationEnabled,
                "Automatically upgrade colonists to better weapons from storage");
            
            // Colonist Commands
            listing.CheckboxLabeled("👤 Emergency Task Priority", ref settings.colonistCommandsEnabled,
                "Force colonists to handle emergencies (rescue, firefighting, medical)");
            
            listing.Gap(8f);
            
            // Save settings
            settings.Write();
        }

        private static void DrawDebugSection(Listing_Standard listing, RimWatchSettings settings)
        {
            // === v0.8.4: GLOBAL LOGGING MASTER SWITCH ===
            bool oldGlobalLogging = settings.enableGlobalLogging;
            listing.CheckboxLabeled("🌐 Enable All Logging (Master Switch)", ref settings.enableGlobalLogging, 
                "Turn on/off all logging at once. When OFF, only critical errors are logged.");
            
            if (oldGlobalLogging != settings.enableGlobalLogging)
            {
                settings.Write();
            }
            
            if (!settings.enableGlobalLogging)
            {
                listing.Gap(6f);
                GUI.color = new Color(1f, 0.7f, 0.2f);
                listing.Label("⚠️ All logging is disabled. Enable master switch to configure individual log levels.");
                GUI.color = Color.white;
                return;
            }
            
            listing.Gap(12f);
            
            // === LOGGING SETTINGS GROUP ===
            DrawCollapsibleSection(listing, "📋 Logging Settings", "logging_settings", () =>
            {
                // Building Log Level
                listing.Gap(4f);
                listing.Label("Building Construction Log Level:");
                GUI.color = UIDesignSystem.Text_Secondary;
                listing.Label("Controls verbosity for building placement and construction.");
                GUI.color = Color.white;
                
                Rect logLevelRect = listing.GetRect(28f);
                string logLevelText = settings.buildingLogLevel.ToString();
                if (Widgets.ButtonText(logLevelRect, logLevelText))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (BuildingLogLevel lvl in System.Enum.GetValues(typeof(BuildingLogLevel)))
                    {
                        BuildingLogLevel captured = lvl;
                        options.Add(new FloatMenuOption(captured.ToString(), () => { 
                            settings.buildingLogLevel = captured;
                            settings.Write();
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                
                listing.Gap(10f);
                
                // Per-System Log Levels
                listing.Label("Per-System Log Levels:");
                GUI.color = UIDesignSystem.Text_Secondary;
                listing.Label("Individual verbosity for each automation system:");
                GUI.color = Color.white;
                
                DrawSystemLogLevelRow(listing, "Work", (lvl) => { settings.workLogLevel = lvl; settings.Write(); }, settings.workLogLevel);
                DrawSystemLogLevelRow(listing, "Farming", (lvl) => { settings.farmingLogLevel = lvl; settings.Write(); }, settings.farmingLogLevel);
                DrawSystemLogLevelRow(listing, "Defense", (lvl) => { settings.defenseLogLevel = lvl; settings.Write(); }, settings.defenseLogLevel);
                DrawSystemLogLevelRow(listing, "Medical", (lvl) => { settings.medicalLogLevel = lvl; settings.Write(); }, settings.medicalLogLevel);
                DrawSystemLogLevelRow(listing, "Trade", (lvl) => { settings.tradeLogLevel = lvl; settings.Write(); }, settings.tradeLogLevel);
                DrawSystemLogLevelRow(listing, "Resource", (lvl) => { settings.resourceLogLevel = lvl; settings.Write(); }, settings.resourceLogLevel);
                DrawSystemLogLevelRow(listing, "ColonistCommands", (lvl) => { settings.colonistCommandsLogLevel = lvl; settings.Write(); }, settings.colonistCommandsLogLevel);
                DrawSystemLogLevelRow(listing, "ColonyDevelopment", (lvl) => { settings.colonyDevelopmentLogLevel = lvl; settings.Write(); }, settings.colonyDevelopmentLogLevel);
                DrawSystemLogLevelRow(listing, "Construction", (lvl) => { settings.constructionLogLevel = lvl; settings.Write(); }, settings.constructionLogLevel);
            });
            
            listing.Gap(12f);
            
            // === DEBUG MODE ===
            bool oldDebug = settings.debugModeEnabled;
            listing.CheckboxLabeled("RimWatch.UI.DebugMode".Translate(), ref settings.debugModeEnabled);
            
            if (oldDebug != settings.debugModeEnabled)
            {
                settings.Write();
            }
            
            listing.Gap(8f);
            
            // === FILE LOGGING ===
            bool oldFileLogging = settings.fileLoggingEnabled;
            listing.CheckboxLabeled("RimWatch.UI.FileLogging".Translate(), ref settings.fileLoggingEnabled);
            
            if (oldFileLogging != settings.fileLoggingEnabled)
            {
                settings.Write();
            }
            
            if (settings.debugModeEnabled || settings.fileLoggingEnabled)
            {
                listing.Gap(6f);
                GUI.color = UIDesignSystem.Text_Secondary;
                listing.Label("RimWatch.UI.DebugWarning".Translate());
                GUI.color = Color.white;
            }
        }


        /// <summary>
        /// Draw single row for per-system log level selector.
        /// </summary>
        private static void DrawSystemLogLevelRow(Listing_Standard listing, string systemLabel, System.Action<SystemLogLevel> setLevel, SystemLogLevel currentLevel)
        {
            Rect rowRect = listing.GetRect(24f);
            Rect labelRect = new Rect(rowRect.x, rowRect.y, rowRect.width / 2f, rowRect.height);
            Rect buttonRect = new Rect(rowRect.x + rowRect.width / 2f + 4f, rowRect.y, rowRect.width / 2f - 4f, rowRect.height);
            
            Widgets.Label(labelRect, $"  {systemLabel}");
            
            if (Widgets.ButtonText(buttonRect, currentLevel.ToString()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (SystemLogLevel lvl in System.Enum.GetValues(typeof(SystemLogLevel)))
                {
                    SystemLogLevel captured = lvl;
                    options.Add(new FloatMenuOption(captured.ToString(), () =>
                    {
                        setLevel(captured);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private static void DrawVisualizationSection(Listing_Standard listing, RimWatchSettings settings)
        {
            bool oldOverlay = settings.enableDebugOverlay;
            bool oldDecision = settings.enableDecisionLogging;
            
            listing.CheckboxLabeled("RimWatch.UI.DebugOverlay".Translate(), ref settings.enableDebugOverlay);
            
            if (oldOverlay != settings.enableDebugOverlay)
            {
                settings.ApplyToCore();
                settings.Write();
            }
            
            if (settings.enableDebugOverlay)
            {
                listing.Gap(4f);
                listing.Label("RimWatch.UI.DisplayMode".Translate());
                
                Rect modeRect = listing.GetRect(28f);
                string modeKey = $"RimWatch.UI.OverlayMode.{settings.debugOverlayMode}";
                if (Widgets.ButtonText(modeRect, modeKey.Translate()))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    options.Add(new FloatMenuOption("RimWatch.UI.OverlayMode.Zones".Translate(), () => { 
                        settings.debugOverlayMode = DebugOverlayMode.Zones;
                        settings.ApplyToCore();
                        settings.Write();
                    }));
                    options.Add(new FloatMenuOption("RimWatch.UI.OverlayMode.PlacementScores".Translate(), () => { 
                        settings.debugOverlayMode = DebugOverlayMode.PlacementScores;
                        settings.ApplyToCore();
                        settings.Write();
                    }));
                    options.Add(new FloatMenuOption("RimWatch.UI.OverlayMode.Both".Translate(), () => { 
                        settings.debugOverlayMode = DebugOverlayMode.Both;
                        settings.ApplyToCore();
                        settings.Write();
                    }));
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }
            
            listing.Gap(8f);
            listing.CheckboxLabeled("RimWatch.UI.DecisionLogging".Translate(), ref settings.enableDecisionLogging);
            
            if (oldDecision != settings.enableDecisionLogging)
            {
                settings.ApplyToCore();
                settings.Write();
            }
        }

        private static void DrawActionsSection(Listing_Standard listing, RimWatchSettings settings)
        {
            listing.Gap(8f);
            
            if (listing.ButtonText("RimWatch.UI.ResetToDefaults".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RimWatch.Dialog.ResetConfirm".Translate(),
                    () => {
                        settings.ResetToDefaults();
                        Messages.Message("RimWatch.Message.SettingsReset".Translate(), MessageTypeDefOf.NeutralEvent, false);
                    },
                    true
                ));
            }
        }

        private static void DrawFooter(Listing_Standard listing, bool isQuickPanel)
        {
            listing.Gap(12f);
            Widgets.DrawLineHorizontal(0f, listing.CurHeight, listing.ColumnWidth);
            listing.Gap(6f);
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.7f, 0.8f);
            
            if (!isQuickPanel)
            {
                listing.Label("RimWatch.UI.TipShiftR".Translate());
            }
            
            listing.Label("RimWatch.UI.TipParentChild".Translate());
            listing.Label("RimWatch.UI.TipAutoApply".Translate());
            
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        /// <summary>
        /// Draws Per-Save Settings section (v1.3.0 - only in-game)
        /// </summary>
        private static void DrawPerSaveSettingsSection(Listing_Standard listing, RimWatchSettings settings)
        {
            var gameComponent = RimWatchMod.GameComponent;
            if (gameComponent == null) return;
            
            // Section header
            Rect headerRect = listing.GetRect(UIDesignSystem.HEIGHT_Header);
            UIDesignSystem.DrawCard(headerRect, UIDesignSystem.Accent_Purple);
            
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = UIDesignSystem.Text_Primary;
            Widgets.Label(headerRect, "💾 Per-Save Settings (v1.3.0)");
            UIDesignSystem.ResetTextState();
            
            listing.Gap(UIDesignSystem.SPACE_SM);
            
            // Main checkbox - enabled by default
            bool oldUsePerSave = gameComponent.UsePerSaveSettings;
            bool newUsePerSave = oldUsePerSave;
            
            Rect checkboxRect = listing.GetRect(UIDesignSystem.HEIGHT_Checkbox);
            Widgets.CheckboxLabeled(checkboxRect, "Use per-save settings (recommended)", ref newUsePerSave);
            
            if (newUsePerSave != oldUsePerSave)
            {
                gameComponent.UsePerSaveSettings = newUsePerSave;
                
                if (newUsePerSave)
                {
                    Messages.Message("✓ Per-save settings enabled. Settings will be saved with this save file.", MessageTypeDefOf.NeutralEvent, false);
                }
                else
                {
                    Messages.Message("⚠ Per-save settings disabled. Using global settings for all saves.", MessageTypeDefOf.CautionInput, false);
                }
            }
            
            // Description text
            GUI.color = UIDesignSystem.Text_Secondary;
            listing.Label("  Each save file will remember its own settings");
            GUI.color = Color.white;
            
            listing.Gap(UIDesignSystem.SPACE_XS);
            
            // Status indicator
            if (gameComponent.UsePerSaveSettings)
            {
                GUI.color = UIDesignSystem.Status_Active;
                listing.Label("  Status: ✓ Per-save settings active");
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = UIDesignSystem.Status_Warning;
                listing.Label("  Status: ⚠ Using global settings");
                GUI.color = Color.white;
            }
            
            listing.Gap(UIDesignSystem.SPACE_SM);
            
            // Copy buttons (only visible if per-save enabled)
            if (gameComponent.UsePerSaveSettings)
            {
                Rect buttonsRect = listing.GetRect(UIDesignSystem.HEIGHT_Button);
                float buttonWidth = (buttonsRect.width - UIDesignSystem.SPACE_MD) / 2f;
                
                // Button: Copy global → this save
                if (UIDesignSystem.DrawButton(
                    new Rect(buttonsRect.x, buttonsRect.y, buttonWidth, buttonsRect.height),
                    "Copy global → this save"))
                {
                    gameComponent.CopyFromGlobalSettings();
                    Messages.Message("✓ Copied global settings to this save", MessageTypeDefOf.NeutralEvent, false);
                }
                
                // Button: Copy this save → global
                if (UIDesignSystem.DrawButton(
                    new Rect(buttonsRect.x + buttonWidth + UIDesignSystem.SPACE_MD, buttonsRect.y, buttonWidth, buttonsRect.height),
                    "Copy this save → global"))
                {
                    gameComponent.ApplyToGlobalSettings();
                    settings.Write(); // Save to disk
                    Messages.Message("✓ Copied save settings to global", MessageTypeDefOf.NeutralEvent, false);
                }
            }
            
            listing.Gap(UIDesignSystem.SPACE_XS);
        }

        /// <summary>
        /// v1.4.0: Draw notification system settings section
        /// </summary>
        private static void DrawNotificationSection(Listing_Standard listing, RimWatchSettings settings)
        {
            try
            {
                // Master toggle
                bool notificationEnabled = settings.notificationSystemEnabled;
                listing.CheckboxLabeled(
                    "Enable Notification System",
                    ref notificationEnabled,
                    "Show in-game notifications for all RimWatch actions"
                );
                settings.notificationSystemEnabled = notificationEnabled;
                
                if (notificationEnabled)
                {
                    listing.Gap(UIDesignSystem.SPACE_SM);
                    
                    // Per-category notification levels
                    GUI.color = UIDesignSystem.Text_Secondary;
                    listing.Label("Configure notification detail level for each category:");
                    GUI.color = Color.white;
                    listing.Gap(UIDesignSystem.SPACE_XS);
                    
                    // Building
                    DrawNotificationLevelDropdown(listing, settings, "buildingNotificationLevel",
                        "🏗️ Building", "Construction, blueprints, rooms");
                    
                    // Work
                    DrawNotificationLevelDropdown(listing, settings, "workNotificationLevel",
                        "👷 Work", "Work priorities, schedules");
                    
                    // Farming
                    DrawNotificationLevelDropdown(listing, settings, "farmingNotificationLevel",
                        "🌾 Farming", "Crops, animals, taming");
                    
                    // Resources
                    DrawNotificationLevelDropdown(listing, settings, "resourcesNotificationLevel",
                        "⛏️ Resources", "Mining, woodcutting, hunting");
                    
                    // Defense
                    DrawNotificationLevelDropdown(listing, settings, "defenseNotificationLevel",
                        "⚔️ Defense", "Draft, equipment, positioning");
                    
                    // Medical
                    DrawNotificationLevelDropdown(listing, settings, "medicalNotificationLevel",
                        "🏥 Medical", "Rescue, treatment, operations");
                    
                    // Trade
                    DrawNotificationLevelDropdown(listing, settings, "tradeNotificationLevel",
                        "💰 Trade", "Forbid/allow, trading");
                    
                    // Social
                    DrawNotificationLevelDropdown(listing, settings, "socialNotificationLevel",
                        "👥 Social", "Prisoners, mood, events");
                    
                    // Research
                    DrawNotificationLevelDropdown(listing, settings, "researchNotificationLevel",
                        "🔬 Research", "Research projects");
                    
                    listing.Gap(UIDesignSystem.SPACE_MD);
                    
                    // Format options
                    GUI.color = UIDesignSystem.Text_Secondary;
                    listing.Label("Format Options:");
                    GUI.color = Color.white;
                    listing.Gap(UIDesignSystem.SPACE_XS);
                    
                    bool useEmojis = settings.useEmojisInNotifications;
                    listing.CheckboxLabeled("Use emojis in notifications", ref useEmojis, 
                        "Show emoji icons (🏗️, 👷, 🌾, etc.)");
                    settings.useEmojisInNotifications = useEmojis;
                    
                    bool showCoordinates = settings.showCoordinates;
                    listing.CheckboxLabeled("Show coordinates", ref showCoordinates, 
                        "Display position coordinates (e.g., at (120, 45))");
                    settings.showCoordinates = showCoordinates;
                    
                    bool showPawnNames = settings.showPawnNames;
                    listing.CheckboxLabeled("Show colonist names", ref showPawnNames, 
                        "Include colonist names (e.g., for John)");
                    settings.showPawnNames = showPawnNames;
                    
                    bool showMaterials = settings.showMaterialsInBuilding;
                    listing.CheckboxLabeled("Show materials/quality", ref showMaterials, 
                        "Display materials and quality in building notifications");
                    settings.showMaterialsInBuilding = showMaterials;
                }
                
                // Apply and write settings immediately on change
                settings.ApplyToCore();
                settings.Write();
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("DrawNotificationSection failed", ex);
                GUI.color = Color.red;
                listing.Label("ERROR: " + ex.Message);
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// Draw notification level dropdown for a category
        /// </summary>
        private static void DrawNotificationLevelDropdown(
            Listing_Standard listing,
            RimWatchSettings settings,
            string fieldName,
            string label, 
            string tooltip)
        {
            Rect rect = listing.GetRect(UIDesignSystem.HEIGHT_Checkbox);
            Rect labelRect = new Rect(rect.x, rect.y, rect.width * 0.5f, rect.height);
            Rect dropdownRect = new Rect(rect.x + rect.width * 0.5f, rect.y, rect.width * 0.5f, rect.height);
            
            // Get current value via reflection
            var field = typeof(RimWatchSettings).GetField(fieldName);
            NotificationLevel currentLevel = (NotificationLevel)field.GetValue(settings);
            
            // Label with tooltip
            TooltipHandler.TipRegion(labelRect, tooltip);
            Widgets.Label(labelRect, label);
            
            // Dropdown
            if (Widgets.ButtonText(dropdownRect, GetNotificationLevelLabel(currentLevel)))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                
                foreach (NotificationLevel level in System.Enum.GetValues(typeof(NotificationLevel)))
                {
                    NotificationLevel levelCopy = level;
                    options.Add(new FloatMenuOption(
                        GetNotificationLevelLabel(level), 
                        delegate { 
                            field.SetValue(settings, levelCopy);
                        }
                    ));
                }
                
                Find.WindowStack.Add(new FloatMenu(options));
            }
            
            listing.Gap(UIDesignSystem.SPACE_XS);
        }

        /// <summary>
        /// Get translated label for notification level
        /// </summary>
        private static string GetNotificationLevelLabel(NotificationLevel level)
        {
            return level switch
            {
                NotificationLevel.Off => "RimWatch.NotificationLevel.Off".Translate(),
                NotificationLevel.Critical => "RimWatch.NotificationLevel.Critical".Translate(),
                NotificationLevel.Important => "RimWatch.NotificationLevel.Important".Translate(),
                NotificationLevel.Moderate => "RimWatch.NotificationLevel.Moderate".Translate(),
                NotificationLevel.Verbose => "RimWatch.NotificationLevel.Verbose".Translate(),
                NotificationLevel.Debug => "RimWatch.NotificationLevel.Debug".Translate(),
                _ => level.ToString()
            };
        }

    }
}

