using RimWatch.Automation.Social;
using RimWatch.Core;
using RimWatch.ML;
using RimWatch.Settings;
using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWatch.UI
{
    /// <summary>
    /// Главная панель управления RimWatch с современным дизайном и статистикой.
    /// v1.0: Modern dashboard with tabs, real-time stats, and visual indicators.
    /// </summary>
    public class RimWatchMainPanel : Window
    {
        // UI Constants - Unified Design System
        private const float TAB_HEIGHT = 40f;
        private const float CARD_HEIGHT = 120f; // Unified card height
        private const float SMALL_CARD_HEIGHT = 60f; // For compact cards
        private const float PADDING = 12f; // Consistent padding
        private const float CARD_SPACING = 8f; // Space between cards
        private const float SECTION_SPACING = 16f; // Space between sections
        
        // Unified Color Scheme - Яркие, насыщенные цвета
        private static readonly Color CARD_BG_PURPLE = new Color(0.4f, 0.3f, 0.6f, 0.95f); // Storyteller
        private static readonly Color CARD_BG_BLUE = new Color(0.2f, 0.4f, 0.7f, 0.95f); // Status
        private static readonly Color CARD_BG_GREEN = new Color(0.2f, 0.5f, 0.3f, 0.95f); // Automation
        private static readonly Color CARD_BG_ORANGE = new Color(0.7f, 0.4f, 0.2f, 0.95f); // Decisions
        private static readonly Color CARD_BG_RED = new Color(0.8f, 0.2f, 0.2f, 0.9f); // Alerts
        private static readonly Color CARD_BG_CYAN = new Color(0.2f, 0.6f, 0.7f, 0.95f); // Settings
        private static readonly Color CARD_BG_YELLOW = new Color(0.7f, 0.6f, 0.2f, 0.95f); // Statistics
        private static readonly Color TAB_ACTIVE = new Color(0.3f, 0.6f, 0.9f, 0.9f);
        private static readonly Color TAB_INACTIVE = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        
        // State
        private DashboardTab _currentTab = DashboardTab.Overview;
        private Vector2 _scrollPosition = Vector2.zero;
        
        public override Vector2 InitialSize => new Vector2(900f, 700f);

        public RimWatchMainPanel()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            closeOnAccept = false;
            closeOnCancel = true;
            closeOnClickedOutside = false; // Keep open for dashboard
            absorbInputAroundWindow = false;

            // Center on screen
            windowRect = new Rect(
                (UnityEngine.Screen.width - 900f) / 2f,
                (UnityEngine.Screen.height - 700f) / 2f,
                900f,
                700f
            );
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Header with title and version
            DrawHeader(inRect);
            
            // Tab buttons
            Rect tabRect = new Rect(inRect.x, inRect.y + 50f, inRect.width, TAB_HEIGHT);
            DrawTabs(tabRect);
            
            // Content area
            Rect contentRect = new Rect(inRect.x, inRect.y + 90f, inRect.width, inRect.height - 90f);
            
            // Draw content based on selected tab
            switch (_currentTab)
            {
                case DashboardTab.Overview:
                    DrawOverviewTab(contentRect);
                    break;
                
                case DashboardTab.Statistics:
                    DrawStatisticsTab(contentRect);
                    break;
                
                case DashboardTab.Settings:
                    DrawSettingsTab(contentRect);
                    break;
                
                case DashboardTab.Alerts:
                    DrawAlertsTab(contentRect);
                    break;
            }
        }
        
        /// <summary>
        /// Draws the header with title and version.
        /// </summary>
        private void DrawHeader(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 100f, 40f), "🤖 RimWatch AI Dashboard");
            
            Text.Font = GameFont.Tiny;
            string status = RimWatchCore.AutopilotEnabled ? "ACTIVE" : "INACTIVE";
            Color statusColor = RimWatchCore.AutopilotEnabled ? Color.green : Color.gray;
            
            GUI.color = statusColor;
            Widgets.Label(new Rect(inRect.x + inRect.width - 100f, inRect.y + 10f, 100f, 30f), $"Status: {status}");
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// Draws tab buttons.
        /// </summary>
        private void DrawTabs(Rect tabRect)
        {
            float tabWidth = tabRect.width / 4f;
            
            DrawTab(new Rect(tabRect.x, tabRect.y, tabWidth, tabRect.height), "📊 Overview", DashboardTab.Overview);
            DrawTab(new Rect(tabRect.x + tabWidth, tabRect.y, tabWidth, tabRect.height), "📈 Statistics", DashboardTab.Statistics);
            DrawTab(new Rect(tabRect.x + tabWidth * 2, tabRect.y, tabWidth, tabRect.height), "⚙️ Settings", DashboardTab.Settings);
            DrawTab(new Rect(tabRect.x + tabWidth * 3, tabRect.y, tabWidth, tabRect.height), "🚨 Alerts", DashboardTab.Alerts);
        }
        
        /// <summary>
        /// Draws a single tab button with unified style.
        /// </summary>
        private void DrawTab(Rect rect, string label, DashboardTab tab)
        {
            bool isActive = _currentTab == tab;
            
            // Draw background
            Widgets.DrawBoxSolid(rect, isActive ? TAB_ACTIVE : TAB_INACTIVE);
            
            // Draw border for active tab
            if (isActive)
            {
                Widgets.DrawBox(rect, 2);
            }
            
            // Draw label with better centering
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = isActive ? GameFont.Small : GameFont.Tiny;
            GUI.color = isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            
            // Click handler
            if (Widgets.ButtonInvisible(rect))
            {
                _currentTab = tab;
            }
        }
        
        /// <summary>
        /// Draws the Overview tab with key stats.
        /// </summary>
        private void DrawOverviewTab(Rect contentRect)
        {
            Widgets.BeginScrollView(contentRect, ref _scrollPosition, new Rect(0, 0, contentRect.width - 20f, 1400f));
            
            float y = PADDING;
            float width = contentRect.width - 20f;
            
            // === SECTION 1: QUICK CONTROLS (NEW!) ===
            y = DrawQuickControlsCard(y, width);
            y += SECTION_SPACING;
            
            // === SECTION 2: STORYTELLER INFO ===
            y = DrawStorytellerCard(y, width);
            y += SECTION_SPACING;
            
            // === SECTION 3: COLONY STATS ===
            y = DrawColonyStats(y, width);
            y += SECTION_SPACING;
            
            // === SECTION 4: AUTOMATION STATUS ===
            y = DrawAutomationStatus(y, width);
            y += SECTION_SPACING;
            
            // === SECTION 5: RECENT DECISIONS ===
            y = DrawRecentDecisions(y, width);
            
            Widgets.EndScrollView();
        }
        
        /// <summary>
        /// Draws quick control toggles for automation categories - NEW!
        /// </summary>
        private float DrawQuickControlsCard(float y, float width)
        {
            Rect cardRect = new Rect(PADDING, y, width - PADDING * 2, CARD_HEIGHT);
            
            // Draw card background - Cyan for settings
            Widgets.DrawBoxSolid(cardRect, CARD_BG_CYAN);
            Widgets.DrawBox(cardRect, 1);
            
            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(cardRect.x + PADDING, cardRect.y + PADDING, cardRect.width - PADDING * 2, 30f), 
                "⚡ Quick Controls - Automation Categories");
            Text.Font = GameFont.Small;
            
            // Two rows of checkboxes (4 per row)
            float checkboxY = cardRect.y + 45f;
            float checkboxWidth = (cardRect.width - PADDING * 5) / 4f;
            float checkboxHeight = 30f;
            
            var settings = RimWatchMod.Settings;
            
            // Row 1
            DrawQuickToggle(new Rect(cardRect.x + PADDING, checkboxY, checkboxWidth, checkboxHeight), 
                "🏗️ Building", ref settings.buildingEnabled);
            DrawQuickToggle(new Rect(cardRect.x + PADDING + checkboxWidth + PADDING, checkboxY, checkboxWidth, checkboxHeight), 
                "👷 Work", ref settings.workEnabled);
            DrawQuickToggle(new Rect(cardRect.x + PADDING + (checkboxWidth + PADDING) * 2, checkboxY, checkboxWidth, checkboxHeight), 
                "🌾 Farming", ref settings.farmingEnabled);
            DrawQuickToggle(new Rect(cardRect.x + PADDING + (checkboxWidth + PADDING) * 3, checkboxY, checkboxWidth, checkboxHeight), 
                "🛡️ Defense", ref settings.defenseEnabled);
            
            // Row 2
            checkboxY += checkboxHeight + PADDING / 2;
            DrawQuickToggle(new Rect(cardRect.x + PADDING, checkboxY, checkboxWidth, checkboxHeight), 
                "💰 Trade", ref settings.tradeEnabled);
            DrawQuickToggle(new Rect(cardRect.x + PADDING + checkboxWidth + PADDING, checkboxY, checkboxWidth, checkboxHeight), 
                "🏥 Medical", ref settings.medicalEnabled);
            DrawQuickToggle(new Rect(cardRect.x + PADDING + (checkboxWidth + PADDING) * 2, checkboxY, checkboxWidth, checkboxHeight), 
                "😊 Social", ref settings.socialEnabled);
            DrawQuickToggle(new Rect(cardRect.x + PADDING + (checkboxWidth + PADDING) * 3, checkboxY, checkboxWidth, checkboxHeight), 
                "🔬 Research", ref settings.researchEnabled);
            
            return y + CARD_HEIGHT;
        }
        
        /// <summary>
        /// Draws a quick toggle checkbox with label - NEW!
        /// </summary>
        private void DrawQuickToggle(Rect rect, string label, ref bool value)
        {
            bool oldValue = value;
            Text.Font = GameFont.Tiny;
            Widgets.CheckboxLabeled(rect, label, ref value);
            Text.Font = GameFont.Small;
            
            if (oldValue != value)
            {
                // Apply changes immediately
                RimWatchMod.Settings.ApplyToCore();
                RimWatchMod.Settings.Write();
                RimWatchLogger.Info($"[QuickControls] Toggled {label}: {oldValue} → {value}");
            }
        }
        
        /// <summary>
        /// Draws storyteller info card.
        /// </summary>
        private float DrawStorytellerCard(float y, float width)
        {
            Rect cardRect = new Rect(PADDING, y, width - PADDING * 2, CARD_HEIGHT);
            // Unified purple for storyteller
            Widgets.DrawBoxSolid(cardRect, CARD_BG_PURPLE);
            Widgets.DrawBox(cardRect, 1);
            
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(cardRect.x + 10f, cardRect.y + 5f, cardRect.width - 20f, 25f), 
                $"📖 Current Storyteller: {RimWatchCore.CurrentStoryteller?.GetType().Name ?? "None"}");
            
            Text.Font = GameFont.Tiny;
            var personality = RimWatchCore.CurrentStoryteller?.GetPersonality();
            if (personality != null)
            {
                string traits = $"Risk: {personality.RiskTolerance:P0} | Build: {personality.BuildingSpeed:P0} | Trade: {personality.TradeAggressiveness:P0}";
                Widgets.Label(new Rect(cardRect.x + 10f, cardRect.y + 30f, cardRect.width - 20f, 20f), traits);
            }
            
            return y + CARD_HEIGHT;
        }
        
        /// <summary>
        /// Draws colony statistics.
        /// </summary>
        private float DrawColonyStats(float y, float width)
        {
            if (Find.CurrentMap == null)
                return y;
            
            var map = Find.CurrentMap;
            int colonists = map.mapPawns.FreeColonistsSpawnedCount;
            int prisoners = map.mapPawns.PrisonersOfColonySpawnedCount;
            float avgMood = map.mapPawns.FreeColonistsSpawned.Average(p => p.needs?.mood?.CurLevelPercentage ?? 0.5f);
            
            Rect cardRect = new Rect(PADDING, y, width - PADDING * 2, CARD_HEIGHT);
            // Unified blue for colony status
            Widgets.DrawBoxSolid(cardRect, CARD_BG_BLUE);
            Widgets.DrawBox(cardRect, 1);
            
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(cardRect.x + 10f, cardRect.y + 5f, cardRect.width - 20f, 25f), "🏘️ Colony Status");
            
            Text.Font = GameFont.Tiny;
            string stats = $"Colonists: {colonists} | Prisoners: {prisoners} | Avg Mood: {avgMood:P0}";
            Widgets.Label(new Rect(cardRect.x + 10f, cardRect.y + 30f, cardRect.width - 20f, 20f), stats);
            
            return y + CARD_HEIGHT;
        }
        
        /// <summary>
        /// Draws automation status indicators.
        /// </summary>
        private float DrawAutomationStatus(float y, float width)
        {
            float cardHeight = 220f; // Taller card for 8 systems
            Rect cardRect = new Rect(PADDING, y, width - PADDING * 2, cardHeight);
            // Unified green for automation systems
            Widgets.DrawBoxSolid(cardRect, CARD_BG_GREEN);
            Widgets.DrawBox(cardRect, 1);
            
            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(cardRect.x + PADDING, cardRect.y + PADDING, cardRect.width - PADDING * 2, 30f), 
                "⚡ Automation Systems");
            
            float statusY = cardRect.y + 45f;
            Text.Font = GameFont.Tiny;
            
            DrawSystemStatus(cardRect.x + 10f, statusY, "Building", RimWatch.Automation.BuildingAutomation.IsEnabled);
            DrawSystemStatus(cardRect.x + 10f, statusY + 25f, "Work", RimWatch.Automation.WorkAutomation.IsEnabled);
            DrawSystemStatus(cardRect.x + 10f, statusY + 50f, "Farming", RimWatch.Automation.FarmingAutomation.IsEnabled);
            DrawSystemStatus(cardRect.x + 10f, statusY + 75f, "Defense", RimWatch.Automation.DefenseAutomation.IsEnabled);
            DrawSystemStatus(cardRect.x + 10f, statusY + 100f, "Trade", RimWatch.Automation.TradeAutomation.IsEnabled);
            DrawSystemStatus(cardRect.x + 10f, statusY + 125f, "Medical", RimWatch.Automation.MedicalAutomation.IsEnabled);
            DrawSystemStatus(cardRect.x + 10f, statusY + 150f, "Social", RimWatch.Automation.SocialAutomation.IsEnabled);
            
            return y + 200f + PADDING;
        }
        
        /// <summary>
        /// Draws a system status indicator.
        /// </summary>
        private void DrawSystemStatus(float x, float y, string name, bool enabled)
        {
            GUI.color = enabled ? Color.green : Color.red;
            Widgets.Label(new Rect(x, y, 20f, 20f), enabled ? "●" : "○");
            GUI.color = Color.white;
            Widgets.Label(new Rect(x + 25f, y, 200f, 20f), name);
        }
        
        /// <summary>
        /// Draws recent decisions.
        /// </summary>
        private float DrawRecentDecisions(float y, float width)
        {
            float cardHeight = 180f; // Room for more decisions
            Rect cardRect = new Rect(PADDING, y, width - PADDING * 2, cardHeight);
            // Unified orange for decisions
            Widgets.DrawBoxSolid(cardRect, CARD_BG_ORANGE);
            Widgets.DrawBox(cardRect, 1);
            
            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(cardRect.x + PADDING, cardRect.y + PADDING, cardRect.width - PADDING * 2, 30f), 
                "📝 Recent AI Decisions");
            
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(cardRect.x + 10f, cardRect.y + 35f, cardRect.width - 20f, 100f), 
                "Check Decision History panel for detailed logs...");
            
            return y + 150f + PADDING;
        }
        
        /// <summary>
        /// Draws the Statistics tab.
        /// </summary>
        private void DrawStatisticsTab(Rect contentRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(contentRect.x + PADDING, contentRect.y + PADDING, contentRect.width - 20f, 30f), 
                "📈 Performance & Statistics");
            
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(contentRect.x + PADDING, contentRect.y + 50f, contentRect.width - 20f, 200f), 
                "Performance metrics and detailed statistics will be displayed here.\n\n" +
                "Coming soon:\n" +
                "• TPS impact measurement\n" +
                "• Memory usage tracking\n" +
                "• Decision success rates\n" +
                "• Automation efficiency scores");
        }
        
        /// <summary>
        /// Draws the Settings tab.
        /// </summary>
        private void DrawSettingsTab(Rect contentRect)
        {
            // Use existing UnifiedSettingsUI
            RimWatch.UI.UnifiedSettingsUI.DrawAllSettings(contentRect, RimWatchMod.Settings, isQuickPanel: true);
        }
        
        /// <summary>
        /// Draws the Alerts tab.
        /// </summary>
        private void DrawAlertsTab(Rect contentRect)
        {
            Widgets.BeginScrollView(contentRect, ref _scrollPosition, new Rect(0, 0, contentRect.width - 20f, 800f));
            
            float y = PADDING;
            
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(PADDING, y, contentRect.width - 40f, 30f), "🚨 Active Alerts & Warnings");
            y += 40f;
            
            Text.Font = GameFont.Tiny;
            
            // Mood crisis alerts
            var crises = MoodCrisisDetector.GetActiveCrises();
            if (crises != null && crises.Count > 0)
            {
                // Яркий красный для кризисов
                Widgets.DrawBoxSolid(new Rect(PADDING, y, contentRect.width - 40f, 30f + crises.Count * 25f), 
                    new Color(0.8f, 0.2f, 0.2f, 0.85f));
                
                Widgets.Label(new Rect(PADDING + 5f, y + 5f, contentRect.width - 50f, 20f), 
                    $"⚠️ {crises.Count} Mood Crisis Alert(s)");
                
                y += 30f;
                foreach (var crisis in crises.Take(5))
                {
                    Widgets.Label(new Rect(PADDING + 10f, y, contentRect.width - 60f, 20f), 
                        $"• {crisis.PawnName}: {crisis.Level} ({crisis.Mood:P0})");
                    y += 25f;
                }
                
                y += PADDING;
            }
            
            // All good message
            if (crises == null || crises.Count == 0)
            {
                GUI.color = Color.green;
                Widgets.Label(new Rect(PADDING, y, contentRect.width - 40f, 30f), "✅ No active alerts - Colony running smoothly!");
                GUI.color = Color.white;
            }
            
            Widgets.EndScrollView();
        }
        
        /// <summary>
        /// Dashboard tab enum.
        /// </summary>
        private enum DashboardTab
        {
            Overview,
            Statistics,
            Settings,
            Alerts
        }
    }
}

