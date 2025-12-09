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
    /// Main RimWatch dashboard with clean, minimalist design (Shift+R).
    /// v1.3.1: Unified design system - clean, readable, professional.
    /// </summary>
    public class RimWatchMainPanel : Window
    {
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
            closeOnClickedOutside = false;
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
            // Main header
            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, UIDesignSystem.HEIGHT_HeaderLarge);
            DrawMainHeader(headerRect);
            
            // Tab navigation
            float tabY = headerRect.yMax + UIDesignSystem.SPACE_SM;
            Rect tabRect = new Rect(inRect.x, tabY, inRect.width, UIDesignSystem.HEIGHT_Tab);
            DrawTabs(tabRect);
            
            // Content area with subtle background
            float contentY = tabRect.yMax + UIDesignSystem.SPACE_MD;
            Rect contentRect = new Rect(inRect.x, contentY, inRect.width, inRect.height - contentY);
            UIDesignSystem.DrawSection(contentRect);
            
            // Draw selected tab content
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
            
            UIDesignSystem.ResetTextState();
        }
        
        /// <summary>
        /// Draws main header with title and status
        /// </summary>
        private void DrawMainHeader(Rect rect)
        {
            UIDesignSystem.DrawHeader(rect, "🤖 RimWatch AI Dashboard", GameFont.Medium);
            
            // Status badge in corner
            bool isActive = RimWatchCore.AutopilotEnabled;
            string status = isActive ? "● ACTIVE" : "○ INACTIVE";
            Color statusColor = isActive ? UIDesignSystem.Status_Active : UIDesignSystem.Status_Inactive;
            
            Rect statusRect = new Rect(rect.xMax - 120f, rect.y + UIDesignSystem.SPACE_SM, 110f, 24f);
            GUI.color = statusColor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(statusRect, status);
            UIDesignSystem.ResetTextState();
        }
        
        /// <summary>
        /// Draws tab navigation buttons
        /// </summary>
        private void DrawTabs(Rect rect)
        {
            float tabWidth = rect.width / 4f;
            
            if (UIDesignSystem.DrawTab(
                new Rect(rect.x, rect.y, tabWidth, rect.height),
                "📊 Overview", _currentTab == DashboardTab.Overview))
            {
                _currentTab = DashboardTab.Overview;
            }
            
            if (UIDesignSystem.DrawTab(
                new Rect(rect.x + tabWidth, rect.y, tabWidth, rect.height),
                "📈 Statistics", _currentTab == DashboardTab.Statistics))
            {
                _currentTab = DashboardTab.Statistics;
            }
            
            if (UIDesignSystem.DrawTab(
                new Rect(rect.x + tabWidth * 2, rect.y, tabWidth, rect.height),
                "⚙️ Settings", _currentTab == DashboardTab.Settings))
            {
                _currentTab = DashboardTab.Settings;
            }
            
            if (UIDesignSystem.DrawTab(
                new Rect(rect.x + tabWidth * 3, rect.y, tabWidth, rect.height),
                "🚨 Alerts", _currentTab == DashboardTab.Alerts))
            {
                _currentTab = DashboardTab.Alerts;
            }
        }
        
        /// <summary>
        /// Overview Tab - Clean, minimal, essential info only
        /// </summary>
        private void DrawOverviewTab(Rect contentRect)
        {
            Rect scrollView = contentRect.ContractedBy(UIDesignSystem.SPACE_MD);
            Rect viewRect = new Rect(0, 0, scrollView.width - 20f, 1000f);
            
            Widgets.BeginScrollView(scrollView, ref _scrollPosition, viewRect);
            
            float y = 0f;
            float width = viewRect.width;
            
            // === QUICK CONTROLS ===
            y = DrawQuickControlsSection(y, width);
            y += UIDesignSystem.SPACE_XL;
            
            // === COLONY STATUS ===
            y = DrawColonyStatusSection(y, width);
            y += UIDesignSystem.SPACE_XL;
            
            // === AUTOMATION SYSTEMS ===
            y = DrawAutomationSystemsSection(y, width);
            y += UIDesignSystem.SPACE_XL;
            
            // === STORYTELLER INFO ===
            y = DrawStorytellerSection(y, width);
            
            Widgets.EndScrollView();
        }
        
        /// <summary>
        /// Quick Controls - Toggle automation categories
        /// </summary>
        private float DrawQuickControlsSection(float y, float width)
        {
            float sectionHeight = UIDesignSystem.HEIGHT_Header + UIDesignSystem.HEIGHT_Checkbox * 2 + UIDesignSystem.SPACE_MD * 3;
            Rect sectionRect = new Rect(0, y, width, sectionHeight);
            
            // Section header
            Rect headerRect = new Rect(sectionRect.x, sectionRect.y, sectionRect.width, UIDesignSystem.HEIGHT_Header);
            UIDesignSystem.DrawCard(headerRect, UIDesignSystem.BG_Dark);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIDesignSystem.Text_Primary;
            Widgets.Label(headerRect.ContractedBy(UIDesignSystem.SPACE_MD), "⚡ Quick Controls");
            UIDesignSystem.ResetTextState();
            
            // Content area
            float contentY = headerRect.yMax + UIDesignSystem.SPACE_MD;
            float checkboxWidth = (width - UIDesignSystem.SPACE_MD * 5) / 4f;
            
            var settings = RimWatchMod.Settings;
            
            // Row 1
            DrawQuickToggle(new Rect(UIDesignSystem.SPACE_SM, contentY, checkboxWidth, UIDesignSystem.HEIGHT_Checkbox), 
                "🏗️ Building", ref settings.buildingEnabled);
            DrawQuickToggle(new Rect(UIDesignSystem.SPACE_SM + (checkboxWidth + UIDesignSystem.SPACE_MD), contentY, checkboxWidth, UIDesignSystem.HEIGHT_Checkbox), 
                "👷 Work", ref settings.workEnabled);
            DrawQuickToggle(new Rect(UIDesignSystem.SPACE_SM + (checkboxWidth + UIDesignSystem.SPACE_MD) * 2, contentY, checkboxWidth, UIDesignSystem.HEIGHT_Checkbox), 
                "🌾 Farming", ref settings.farmingEnabled);
            DrawQuickToggle(new Rect(UIDesignSystem.SPACE_SM + (checkboxWidth + UIDesignSystem.SPACE_MD) * 3, contentY, checkboxWidth, UIDesignSystem.HEIGHT_Checkbox), 
                "🛡️ Defense", ref settings.defenseEnabled);
            
            // Row 2
            contentY += UIDesignSystem.HEIGHT_Checkbox + UIDesignSystem.SPACE_SM;
            DrawQuickToggle(new Rect(UIDesignSystem.SPACE_SM, contentY, checkboxWidth, UIDesignSystem.HEIGHT_Checkbox), 
                "💰 Trade", ref settings.tradeEnabled);
            DrawQuickToggle(new Rect(UIDesignSystem.SPACE_SM + (checkboxWidth + UIDesignSystem.SPACE_MD), contentY, checkboxWidth, UIDesignSystem.HEIGHT_Checkbox), 
                "🏥 Medical", ref settings.medicalEnabled);
            DrawQuickToggle(new Rect(UIDesignSystem.SPACE_SM + (checkboxWidth + UIDesignSystem.SPACE_MD) * 2, contentY, checkboxWidth, UIDesignSystem.HEIGHT_Checkbox), 
                "😊 Social", ref settings.socialEnabled);
            DrawQuickToggle(new Rect(UIDesignSystem.SPACE_SM + (checkboxWidth + UIDesignSystem.SPACE_MD) * 3, contentY, checkboxWidth, UIDesignSystem.HEIGHT_Checkbox), 
                "🔬 Research", ref settings.researchEnabled);
            
            return sectionRect.yMax;
        }
        
        /// <summary>
        /// Quick toggle checkbox helper
        /// </summary>
        private void DrawQuickToggle(Rect rect, string label, ref bool value)
        {
            bool oldValue = value;
            Text.Font = GameFont.Tiny;
            Widgets.CheckboxLabeled(rect, label, ref value);
            
            if (oldValue != value)
            {
                RimWatchMod.Settings.ApplyToCore();
                RimWatchMod.Settings.Write();
                RimWatchLogger.Info($"[QuickControls] {label}: {oldValue} → {value}");
            }
            
            UIDesignSystem.ResetTextState();
        }
        
        /// <summary>
        /// Colony Status - Essential colony stats
        /// </summary>
        private float DrawColonyStatusSection(float y, float width)
        {
            if (Find.CurrentMap == null)
                return y;
            
            float sectionHeight = UIDesignSystem.HEIGHT_Card;
            Rect sectionRect = new Rect(0, y, width, sectionHeight);
            
            var map = Find.CurrentMap;
            int colonists = map.mapPawns.FreeColonistsSpawnedCount;
            int prisoners = map.mapPawns.PrisonersOfColonySpawnedCount;
            float avgMood = map.mapPawns.FreeColonistsSpawned.Any() 
                ? map.mapPawns.FreeColonistsSpawned.Average(p => p.needs?.mood?.CurLevelPercentage ?? 0.5f) 
                : 0.5f;
            
            UIDesignSystem.DrawCard(sectionRect, UIDesignSystem.BG_Medium);
            
            // Icon + title
            Rect iconRect = new Rect(sectionRect.x + UIDesignSystem.SPACE_MD, 
                sectionRect.y + UIDesignSystem.SPACE_MD, 24f, 24f);
            Text.Font = GameFont.Medium;
            Widgets.Label(iconRect, "🏘️");
            
            Rect titleRect = new Rect(iconRect.xMax + UIDesignSystem.SPACE_SM, 
                sectionRect.y + UIDesignSystem.SPACE_MD, 200f, 24f);
            GUI.color = UIDesignSystem.Text_Primary;
            Widgets.Label(titleRect, "Colony Status");
            
            // Stats in rows
            float statsY = titleRect.yMax + UIDesignSystem.SPACE_SM;
            Rect statRect = new Rect(sectionRect.x + UIDesignSystem.SPACE_MD, statsY, 
                sectionRect.width - UIDesignSystem.SPACE_MD * 2, 20f);
            
            GUI.color = UIDesignSystem.Text_Secondary;
            Text.Font = GameFont.Tiny;
            Widgets.Label(statRect, $"Colonists: {colonists}  •  Prisoners: {prisoners}  •  Average Mood: {avgMood:P0}");
            
            UIDesignSystem.ResetTextState();
            
            return sectionRect.yMax;
        }
        
        /// <summary>
        /// Automation Systems - Status indicators for all 8 systems
        /// </summary>
        private float DrawAutomationSystemsSection(float y, float width)
        {
            float rowHeight = 22f;
            float sectionHeight = UIDesignSystem.HEIGHT_Header + rowHeight * 8 + UIDesignSystem.SPACE_MD * 2;
            Rect sectionRect = new Rect(0, y, width, sectionHeight);
            
            // Header
            Rect headerRect = new Rect(sectionRect.x, sectionRect.y, sectionRect.width, UIDesignSystem.HEIGHT_Header);
            UIDesignSystem.DrawCard(headerRect, UIDesignSystem.BG_Dark);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIDesignSystem.Text_Primary;
            Widgets.Label(headerRect.ContractedBy(UIDesignSystem.SPACE_MD), "🤖 Automation Systems");
            UIDesignSystem.ResetTextState();
            
            // System status rows
            float statusY = headerRect.yMax + UIDesignSystem.SPACE_MD;
            
            DrawSystemStatusRow(sectionRect.x + UIDesignSystem.SPACE_MD, statusY, 
                "Building", RimWatch.Automation.BuildingAutomation.IsEnabled);
            statusY += rowHeight;
            
            DrawSystemStatusRow(sectionRect.x + UIDesignSystem.SPACE_MD, statusY, 
                "Work", RimWatch.Automation.WorkAutomation.IsEnabled);
            statusY += rowHeight;
            
            DrawSystemStatusRow(sectionRect.x + UIDesignSystem.SPACE_MD, statusY, 
                "Farming", RimWatch.Automation.FarmingAutomation.IsEnabled);
            statusY += rowHeight;
            
            DrawSystemStatusRow(sectionRect.x + UIDesignSystem.SPACE_MD, statusY, 
                "Defense", RimWatch.Automation.DefenseAutomation.IsEnabled);
            statusY += rowHeight;
            
            DrawSystemStatusRow(sectionRect.x + UIDesignSystem.SPACE_MD, statusY, 
                "Trade", RimWatch.Automation.TradeAutomation.IsEnabled);
            statusY += rowHeight;
            
            DrawSystemStatusRow(sectionRect.x + UIDesignSystem.SPACE_MD, statusY, 
                "Medical", RimWatch.Automation.MedicalAutomation.IsEnabled);
            statusY += rowHeight;
            
            DrawSystemStatusRow(sectionRect.x + UIDesignSystem.SPACE_MD, statusY, 
                "Social", RimWatch.Automation.SocialAutomation.IsEnabled);
            statusY += rowHeight;
            
            DrawSystemStatusRow(sectionRect.x + UIDesignSystem.SPACE_MD, statusY, 
                "Research", RimWatch.Automation.ResearchAutomation.IsEnabled);
            
            return sectionRect.yMax;
        }
        
        /// <summary>
        /// Single system status row with dot indicator
        /// </summary>
        private void DrawSystemStatusRow(float x, float y, string name, bool enabled)
        {
            Rect dotRect = new Rect(x, y, 16f, 16f);
            UIDesignSystem.DrawStatusDot(dotRect, enabled);
            
            Rect labelRect = new Rect(x + 22f, y, 200f, 20f);
            GUI.color = enabled ? UIDesignSystem.Text_Primary : UIDesignSystem.Text_Secondary;
            Text.Font = GameFont.Tiny;
            Widgets.Label(labelRect, name);
            UIDesignSystem.ResetTextState();
        }
        
        /// <summary>
        /// Storyteller Info - Current AI storyteller
        /// </summary>
        private float DrawStorytellerSection(float y, float width)
        {
            float sectionHeight = UIDesignSystem.HEIGHT_Card;
            Rect sectionRect = new Rect(0, y, width, sectionHeight);
            
            UIDesignSystem.DrawCard(sectionRect, UIDesignSystem.BG_Medium);
            
            // Icon + title
            Rect iconRect = new Rect(sectionRect.x + UIDesignSystem.SPACE_MD, 
                sectionRect.y + UIDesignSystem.SPACE_MD, 24f, 24f);
            Text.Font = GameFont.Medium;
            Widgets.Label(iconRect, "📖");
            
            Rect titleRect = new Rect(iconRect.xMax + UIDesignSystem.SPACE_SM, 
                sectionRect.y + UIDesignSystem.SPACE_MD, sectionRect.width - iconRect.xMax - UIDesignSystem.SPACE_MD * 2, 24f);
            GUI.color = UIDesignSystem.Text_Primary;
            Text.Font = GameFont.Small;
            string storytellerName = RimWatchCore.CurrentStoryteller?.GetType().Name ?? "None";
            Widgets.Label(titleRect, $"Storyteller: {storytellerName}");
            
            // Personality traits
            var personality = RimWatchCore.CurrentStoryteller?.GetPersonality();
            if (personality != null)
            {
                float traitsY = titleRect.yMax + UIDesignSystem.SPACE_SM;
                Rect traitsRect = new Rect(sectionRect.x + UIDesignSystem.SPACE_MD, traitsY, 
                    sectionRect.width - UIDesignSystem.SPACE_MD * 2, 20f);
                
                GUI.color = UIDesignSystem.Text_Secondary;
                Text.Font = GameFont.Tiny;
                string traits = $"Risk: {personality.RiskTolerance:P0}  •  Build Speed: {personality.BuildingSpeed:P0}  •  Trade: {personality.TradeAggressiveness:P0}";
                Widgets.Label(traitsRect, traits);
            }
            
            UIDesignSystem.ResetTextState();
            
            return sectionRect.yMax;
        }
        
        /// <summary>
        /// Statistics Tab - Performance metrics
        /// </summary>
        private void DrawStatisticsTab(Rect contentRect)
        {
            Rect innerRect = contentRect.ContractedBy(UIDesignSystem.SPACE_MD);
            
            Text.Font = GameFont.Small;
            GUI.color = UIDesignSystem.Text_Primary;
            Widgets.Label(new Rect(innerRect.x, innerRect.y, innerRect.width, 30f), 
                "📈 Performance & Statistics");
            
            Text.Font = GameFont.Tiny;
            GUI.color = UIDesignSystem.Text_Secondary;
            Widgets.Label(new Rect(innerRect.x, innerRect.y + 50f, innerRect.width, 200f), 
                "Performance metrics and detailed statistics will be displayed here.\n\n" +
                "Coming soon:\n" +
                "• TPS impact measurement\n" +
                "• Memory usage tracking\n" +
                "• Decision success rates\n" +
                "• Automation efficiency scores");
            
            UIDesignSystem.ResetTextState();
        }
        
        /// <summary>
        /// Settings Tab - Uses UnifiedSettingsUI
        /// </summary>
        private void DrawSettingsTab(Rect contentRect)
        {
            UnifiedSettingsUI.DrawAllSettings(contentRect, RimWatchMod.Settings, isQuickPanel: true);
        }
        
        /// <summary>
        /// Alerts Tab - Active warnings and crises
        /// </summary>
        private void DrawAlertsTab(Rect contentRect)
        {
            Rect scrollView = contentRect.ContractedBy(UIDesignSystem.SPACE_MD);
            Rect viewRect = new Rect(0, 0, scrollView.width - 20f, 800f);
            
            Widgets.BeginScrollView(scrollView, ref _scrollPosition, viewRect);
            
            float y = 0f;
            
            // Header
            Rect headerRect = new Rect(0, y, viewRect.width, UIDesignSystem.HEIGHT_Header);
            UIDesignSystem.DrawCard(headerRect, UIDesignSystem.BG_Dark);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIDesignSystem.Text_Primary;
            Widgets.Label(headerRect.ContractedBy(UIDesignSystem.SPACE_MD), "🚨 Active Alerts");
            UIDesignSystem.ResetTextState();
            
            y = headerRect.yMax + UIDesignSystem.SPACE_MD;
            
            // Check for mood crises
            var crises = MoodCrisisDetector.GetActiveCrises();
            
            if (crises != null && crises.Count > 0)
            {
                float alertHeight = 30f + crises.Count * 24f;
                Rect alertRect = new Rect(0, y, viewRect.width, alertHeight);
                UIDesignSystem.DrawCard(alertRect, new Color(0.3f, 0.15f, 0.15f, 0.8f)); // Dark red
                
                Rect alertTitleRect = new Rect(alertRect.x + UIDesignSystem.SPACE_MD, 
                    alertRect.y + UIDesignSystem.SPACE_SM, alertRect.width - UIDesignSystem.SPACE_MD * 2, 20f);
                GUI.color = UIDesignSystem.Status_Error;
                Text.Font = GameFont.Small;
                Widgets.Label(alertTitleRect, $"⚠️ {crises.Count} Mood Crisis Alert(s)");
                
                float crisisY = alertTitleRect.yMax + UIDesignSystem.SPACE_XS;
                GUI.color = UIDesignSystem.Text_Primary;
                Text.Font = GameFont.Tiny;
                
                foreach (var crisis in crises.Take(5))
                {
                    Rect crisisRect = new Rect(alertRect.x + UIDesignSystem.SPACE_MD * 2, crisisY, 
                        alertRect.width - UIDesignSystem.SPACE_MD * 3, 22f);
                    Widgets.Label(crisisRect, $"• {crisis.PawnName}: {crisis.Level} ({crisis.Mood:P0})");
                    crisisY += 22f;
                }
                
                y = alertRect.yMax + UIDesignSystem.SPACE_MD;
            }
            else
            {
                // No alerts - all good
                Rect goodRect = new Rect(0, y, viewRect.width, UIDesignSystem.HEIGHT_Button);
                GUI.color = UIDesignSystem.Status_Active;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(goodRect.x + UIDesignSystem.SPACE_MD, goodRect.y, 
                    goodRect.width - UIDesignSystem.SPACE_MD * 2, goodRect.height), 
                    "✅ No active alerts - Colony running smoothly!");
            }
            
            UIDesignSystem.ResetTextState();
            Widgets.EndScrollView();
        }
        
        private enum DashboardTab
        {
            Overview,
            Statistics,
            Settings,
            Alerts
        }
    }
}
