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
    /// </summary>
    public static class UnifiedSettingsUI
    {
        private static Vector2 _scrollPosition = Vector2.zero;
        
        // Minimalist color scheme - simple and clean
        private static readonly Color HeaderBgColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        private static readonly Color SectionBgColor = new Color(0.15f, 0.15f, 0.15f, 0.3f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color MutedTextColor = new Color(0.6f, 0.6f, 0.6f);

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

            // Header with gradient background
            Rect headerRect = listing.GetRect(36f);
            DrawGradientBox(headerRect, SectionBgColor, SectionBgColor * 0.8f);
            Widgets.DrawBox(headerRect, 1);
            
            // Arrow button
            Rect arrowRect = new Rect(headerRect.x + 8f, headerRect.y + 8f, 20f, 20f);
            string arrow = isCollapsed ? "▶" : "▼";
            Text.Font = GameFont.Medium;
            Widgets.Label(arrowRect, arrow);
            
            // Title
            Rect titleRect = new Rect(headerRect.x + 35f, headerRect.y, headerRect.width - 35f, headerRect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, title);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            
            // Click to toggle
            if (Widgets.ButtonInvisible(headerRect))
            {
                _sectionCollapsed[id] = !isCollapsed;
            }

            listing.Gap(4f);

            // Content
            if (!isCollapsed)
            {
                Rect contentBg = listing.GetRect(0f); // Will be adjusted
                float contentStartY = listing.CurHeight;
                
                drawContent();
                
                float contentEndY = listing.CurHeight;
                contentBg.y = contentStartY;
                contentBg.height = contentEndY - contentStartY;
                
                // Draw subtle background behind content
                Widgets.DrawBoxSolid(contentBg, new Color(0.1f, 0.1f, 0.1f, 0.2f));
                
                listing.Gap(8f);
            }
            else
            {
                listing.Gap(4f);
            }
        }

        private static void DrawHeader(Listing_Standard listing, bool isQuickPanel)
        {
            Rect headerRect = listing.GetRect(50f);
            DrawGradientBox(headerRect, HeaderBgColor, HeaderBgColor * 0.7f);
            
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            string title = isQuickPanel ? "RimWatch.UI.QuickPanel".Translate() : "RimWatch.UI.Settings".Translate();
            Widgets.Label(headerRect, title);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            
            listing.Gap(12f);
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
            
            GUI.color = MutedTextColor;
            listing.Label("RimWatch.UI.ActiveModules".Translate(activeModules));
            GUI.color = Color.white;
            
            listing.Gap(8f);
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
                GUI.color = MutedTextColor;
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
                GUI.color = MutedTextColor;
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
                GUI.color = MutedTextColor;
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
        /// Draws a gradient box for beautiful backgrounds.
        /// </summary>
        private static void DrawGradientBox(Rect rect, Color colorTop, Color colorBottom)
        {
            // Simple gradient effect using two boxes
            Rect topHalf = rect;
            topHalf.height /= 2f;
            Widgets.DrawBoxSolid(topHalf, colorTop);
            
            Rect bottomHalf = rect;
            bottomHalf.y += bottomHalf.height / 2f;
            bottomHalf.height /= 2f;
            Widgets.DrawBoxSolid(bottomHalf, colorBottom);
        }
    }
}

