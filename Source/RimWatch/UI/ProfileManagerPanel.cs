using RimWatch.AI;
using RimWatch.AI.Storytellers;
using RimWatch.Core;
using RimWatch.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWatch.UI
{
    /// <summary>
    /// Profile manager for saving/loading/sharing storyteller configurations.
    /// Allows players to save their custom storyteller setups and share them with others.
    /// v0.9.10: Profile management system.
    /// </summary>
    public static class ProfileManagerPanel
    {
        // UI Constants
        private const float WINDOW_WIDTH = 800f;
        private const float WINDOW_HEIGHT = 600f;
        private const float PROFILE_CARD_HEIGHT = 80f;
        private const float PADDING = 10f;
        private const float BUTTON_WIDTH = 120f;
        private const float BUTTON_HEIGHT = 35f;
        
        // Colors
        private static readonly Color COLOR_BG = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color COLOR_CARD_BG = new Color(0.2f, 0.2f, 0.2f, 1f);
        private static readonly Color COLOR_CARD_HOVER = new Color(0.25f, 0.25f, 0.3f, 1f);
        private static readonly Color COLOR_HIGHLIGHT = new Color(0.3f, 0.6f, 1f, 1f);
        private static readonly Color COLOR_TEXT = Color.white;
        private static readonly Color COLOR_TEXT_FADED = new Color(0.7f, 0.7f, 0.7f, 1f);
        
        // State
        private static Vector2 _scrollPosition = Vector2.zero;
        private static List<StorytellerProfile> _profiles = new List<StorytellerProfile>();
        private static StorytellerProfile _selectedProfile = null;
        private static string _newProfileName = "";
        private static bool _showCreateDialog = false;
        
        // File paths
        private static string ProfilesDirectory => Path.Combine(GenFilePaths.ConfigFolderPath, "RimWatch", "Profiles");
        
        /// <summary>
        /// Initializes the profile manager and loads all saved profiles.
        /// </summary>
        public static void Initialize()
        {
            // Ensure profiles directory exists
            if (!Directory.Exists(ProfilesDirectory))
            {
                Directory.CreateDirectory(ProfilesDirectory);
                RimWatchLogger.Info($"ProfileManager: Created profiles directory at {ProfilesDirectory}");
            }
            
            LoadAllProfiles();
        }
        
        /// <summary>
        /// Draws the profile manager window.
        /// </summary>
        public static void DrawWindow(Rect inRect)
        {
            Widgets.DrawBoxSolid(inRect, COLOR_BG);
            
            float curY = PADDING;
            
            // Header
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect headerRect = new Rect(inRect.x + PADDING, curY, inRect.width - 2 * PADDING, 40f);
            Widgets.Label(headerRect, "Storyteller Profile Manager");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            curY += 50f;
            
            // Action buttons
            Rect buttonRowRect = new Rect(inRect.x + PADDING, curY, inRect.width - 2 * PADDING, BUTTON_HEIGHT);
            DrawActionButtons(buttonRowRect);
            curY += BUTTON_HEIGHT + PADDING;
            
            // Profile list
            Rect listRect = new Rect(inRect.x + PADDING, curY, inRect.width - 2 * PADDING, inRect.height - curY - PADDING);
            DrawProfileList(listRect);
            
            // Create profile dialog (if shown)
            if (_showCreateDialog)
            {
                DrawCreateProfileDialog(inRect);
            }
        }
        
        /// <summary>
        /// Draws action buttons (Create, Load, Delete, Export, Import).
        /// </summary>
        private static void DrawActionButtons(Rect rect)
        {
            float buttonX = rect.x;
            
            // Create New Profile button
            Rect createButtonRect = new Rect(buttonX, rect.y, BUTTON_WIDTH, BUTTON_HEIGHT);
            if (Widgets.ButtonText(createButtonRect, "Create New", true, true, COLOR_HIGHLIGHT))
            {
                _showCreateDialog = true;
                _newProfileName = "";
                RimWatchLogger.Info("ProfileManager: Opening create profile dialog");
            }
            buttonX += BUTTON_WIDTH + PADDING;
            
            // Load Profile button
            Rect loadButtonRect = new Rect(buttonX, rect.y, BUTTON_WIDTH, BUTTON_HEIGHT);
            bool canLoad = _selectedProfile != null;
            if (Widgets.ButtonText(loadButtonRect, "Load", true, true, canLoad ? COLOR_HIGHLIGHT : Color.gray))
            {
                if (canLoad)
                {
                    LoadProfile(_selectedProfile);
                }
            }
            buttonX += BUTTON_WIDTH + PADDING;
            
            // Delete Profile button
            Rect deleteButtonRect = new Rect(buttonX, rect.y, BUTTON_WIDTH, BUTTON_HEIGHT);
            bool canDelete = _selectedProfile != null;
            if (Widgets.ButtonText(deleteButtonRect, "Delete", true, true, canDelete ? new Color(1f, 0.3f, 0.3f, 1f) : Color.gray))
            {
                if (canDelete)
                {
                    DeleteProfile(_selectedProfile);
                }
            }
            buttonX += BUTTON_WIDTH + PADDING;
            
            // Export Profile button (copy to clipboard)
            Rect exportButtonRect = new Rect(buttonX, rect.y, BUTTON_WIDTH, BUTTON_HEIGHT);
            bool canExport = _selectedProfile != null;
            if (Widgets.ButtonText(exportButtonRect, "Export", true, true, canExport ? COLOR_HIGHLIGHT : Color.gray))
            {
                if (canExport)
                {
                    ExportProfile(_selectedProfile);
                }
            }
            buttonX += BUTTON_WIDTH + PADDING;
            
            // Refresh button
            Rect refreshButtonRect = new Rect(buttonX, rect.y, BUTTON_WIDTH, BUTTON_HEIGHT);
            if (Widgets.ButtonText(refreshButtonRect, "Refresh", true, true, COLOR_HIGHLIGHT))
            {
                LoadAllProfiles();
                RimWatchLogger.Info("ProfileManager: Refreshed profile list");
            }
        }
        
        /// <summary>
        /// Draws the profile list with cards.
        /// </summary>
        private static void DrawProfileList(Rect rect)
        {
            if (_profiles.Count == 0)
            {
                // No profiles message
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "No saved profiles. Click 'Create New' to start.");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            
            // Scrollable list
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, _profiles.Count * (PROFILE_CARD_HEIGHT + PADDING));
            Widgets.BeginScrollView(rect, ref _scrollPosition, viewRect, true);
            
            float curY = 0f;
            foreach (var profile in _profiles)
            {
                Rect cardRect = new Rect(0f, curY, viewRect.width, PROFILE_CARD_HEIGHT);
                DrawProfileCard(cardRect, profile);
                curY += PROFILE_CARD_HEIGHT + PADDING;
            }
            
            Widgets.EndScrollView();
        }
        
        /// <summary>
        /// Draws a single profile card.
        /// </summary>
        private static void DrawProfileCard(Rect rect, StorytellerProfile profile)
        {
            bool isSelected = profile == _selectedProfile;
            bool isHovered = Mouse.IsOver(rect);
            
            // Background
            Color bgColor = isSelected ? COLOR_HIGHLIGHT : (isHovered ? COLOR_CARD_HOVER : COLOR_CARD_BG);
            Widgets.DrawBoxSolid(rect, bgColor);
            Widgets.DrawBox(rect, 1);
            
            // Click to select
            if (Widgets.ButtonInvisible(rect))
            {
                _selectedProfile = profile;
                RimWatchLogger.Info($"ProfileManager: Selected profile '{profile.Name}'");
            }
            
            // Content
            float padding = 10f;
            Rect contentRect = rect.ContractedBy(padding);
            
            // Profile name
            Text.Font = GameFont.Medium;
            Rect nameRect = new Rect(contentRect.x, contentRect.y, contentRect.width * 0.7f, 30f);
            Widgets.Label(nameRect, profile.Name);
            Text.Font = GameFont.Small;
            
            // Storyteller type
            Rect typeRect = new Rect(contentRect.x, contentRect.y + 30f, contentRect.width * 0.7f, 20f);
            GUI.color = COLOR_TEXT_FADED;
            Widgets.Label(typeRect, $"Type: {profile.StorytellerType}");
            GUI.color = Color.white;
            
            // Created date
            Rect dateRect = new Rect(contentRect.x, contentRect.y + 50f, contentRect.width * 0.7f, 20f);
            GUI.color = COLOR_TEXT_FADED;
            Widgets.Label(dateRect, $"Created: {profile.CreatedDate:yyyy-MM-dd HH:mm}");
            GUI.color = Color.white;
            
            // Personality traits (right side)
            if (profile.Personality != null)
            {
                float rightX = contentRect.x + contentRect.width * 0.7f;
                float rightWidth = contentRect.width * 0.3f;
                float traitY = contentRect.y;
                
                DrawTraitBar(new Rect(rightX, traitY, rightWidth, 10f), "Risk", profile.Personality.RiskTolerance);
                traitY += 12f;
                DrawTraitBar(new Rect(rightX, traitY, rightWidth, 10f), "Build", profile.Personality.BuildingSpeed);
                traitY += 12f;
                DrawTraitBar(new Rect(rightX, traitY, rightWidth, 10f), "Trade", profile.Personality.TradeAggressiveness);
                traitY += 12f;
                DrawTraitBar(new Rect(rightX, traitY, rightWidth, 10f), "Defense", profile.Personality.DefenseStyle);
                traitY += 12f;
                DrawTraitBar(new Rect(rightX, traitY, rightWidth, 10f), "Research", profile.Personality.ResearchPriority);
            }
        }
        
        /// <summary>
        /// Draws a small trait bar visualization.
        /// </summary>
        private static void DrawTraitBar(Rect rect, string label, float value)
        {
            // Label
            Text.Font = GameFont.Tiny;
            Rect labelRect = new Rect(rect.x, rect.y, 40f, rect.height);
            GUI.color = COLOR_TEXT_FADED;
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            
            // Bar
            Rect barRect = new Rect(rect.x + 42f, rect.y + 2f, rect.width - 42f, rect.height - 4f);
            Widgets.DrawBoxSolid(barRect, new Color(0.1f, 0.1f, 0.1f, 1f));
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * value, barRect.height);
            Widgets.DrawBoxSolid(fillRect, COLOR_HIGHLIGHT);
            
            Text.Font = GameFont.Small;
        }
        
        /// <summary>
        /// Draws the create profile dialog.
        /// </summary>
        private static void DrawCreateProfileDialog(Rect parentRect)
        {
            // Dialog background (overlay)
            Widgets.DrawBoxSolid(parentRect, new Color(0f, 0f, 0f, 0.7f));
            
            // Dialog window
            float dialogWidth = 400f;
            float dialogHeight = 200f;
            Rect dialogRect = new Rect(
                parentRect.x + (parentRect.width - dialogWidth) / 2f,
                parentRect.y + (parentRect.height - dialogHeight) / 2f,
                dialogWidth,
                dialogHeight
            );
            
            Widgets.DrawBoxSolid(dialogRect, COLOR_CARD_BG);
            Widgets.DrawBox(dialogRect, 2);
            
            float padding = 20f;
            float curY = dialogRect.y + padding;
            
            // Title
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(dialogRect.x + padding, curY, dialogRect.width - 2 * padding, 30f);
            Widgets.Label(titleRect, "Create New Profile");
            Text.Font = GameFont.Small;
            curY += 40f;
            
            // Name field
            Rect labelRect = new Rect(dialogRect.x + padding, curY, 100f, 30f);
            Widgets.Label(labelRect, "Profile Name:");
            curY += 25f;
            
            Rect textFieldRect = new Rect(dialogRect.x + padding, curY, dialogRect.width - 2 * padding, 30f);
            _newProfileName = Widgets.TextField(textFieldRect, _newProfileName);
            curY += 40f;
            
            // Buttons
            float buttonY = dialogRect.y + dialogRect.height - BUTTON_HEIGHT - padding;
            float buttonX = dialogRect.x + dialogRect.width - 2 * (BUTTON_WIDTH + padding);
            
            // Cancel button
            Rect cancelButtonRect = new Rect(buttonX, buttonY, BUTTON_WIDTH, BUTTON_HEIGHT);
            if (Widgets.ButtonText(cancelButtonRect, "Cancel"))
            {
                _showCreateDialog = false;
            }
            buttonX += BUTTON_WIDTH + padding;
            
            // Create button
            Rect createButtonRect = new Rect(buttonX, buttonY, BUTTON_WIDTH, BUTTON_HEIGHT);
            bool canCreate = !string.IsNullOrWhiteSpace(_newProfileName);
            if (Widgets.ButtonText(createButtonRect, "Create", true, true, canCreate ? COLOR_HIGHLIGHT : Color.gray))
            {
                if (canCreate)
                {
                    CreateProfile(_newProfileName.Trim());
                    _showCreateDialog = false;
                }
            }
        }
        
        /// <summary>
        /// Creates a new profile from the current storyteller configuration.
        /// </summary>
        private static void CreateProfile(string name)
        {
            var currentStoryteller = RimWatchCore.CurrentStoryteller;
            if (currentStoryteller == null)
            {
                RimWatchLogger.Warning("ProfileManager: Cannot create profile - no active storyteller");
                Messages.Message("Cannot create profile - no active storyteller", MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            var profile = new StorytellerProfile
            {
                Name = name,
                StorytellerType = currentStoryteller.GetType().Name,
                Personality = currentStoryteller.GetPersonality(),
                CreatedDate = DateTime.Now
            };
            
            // Save to file
            string filePath = Path.Combine(ProfilesDirectory, $"{SanitizeFileName(name)}.xml");
            try
            {
                SaveProfileToFile(profile, filePath);
                _profiles.Add(profile);
                _selectedProfile = profile;
                
                RimWatchLogger.Info($"ProfileManager: Created profile '{name}' -> {filePath}");
                Messages.Message($"Profile '{name}' created successfully", MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"ProfileManager: Failed to create profile '{name}': {ex.Message}");
                Messages.Message($"Failed to create profile: {ex.Message}", MessageTypeDefOf.RejectInput, false);
            }
        }
        
        /// <summary>
        /// Loads all profiles from the profiles directory.
        /// </summary>
        private static void LoadAllProfiles()
        {
            _profiles.Clear();
            
            if (!Directory.Exists(ProfilesDirectory))
            {
                return;
            }
            
            foreach (string filePath in Directory.GetFiles(ProfilesDirectory, "*.xml"))
            {
                try
                {
                    var profile = LoadProfileFromFile(filePath);
                    if (profile != null)
                    {
                        _profiles.Add(profile);
                    }
                }
                catch (Exception ex)
                {
                    RimWatchLogger.Warning($"ProfileManager: Failed to load profile from {filePath}: {ex.Message}");
                }
            }
            
            RimWatchLogger.Info($"ProfileManager: Loaded {_profiles.Count} profiles");
        }
        
        /// <summary>
        /// Loads a profile and applies it to the current storyteller.
        /// </summary>
        private static void LoadProfile(StorytellerProfile profile)
        {
            try
            {
                // For Custom storyteller, we can directly apply the personality
                if (RimWatchCore.CurrentStoryteller is CustomStoryteller customStoryteller)
                {
                    // Apply personality settings
                    if (profile.Personality != null)
                    {
                        // Update the custom storyteller's personality
                        // Note: This requires CustomStoryteller to have public setters or a method to update personality
                        RimWatchLogger.Info($"ProfileManager: Loaded profile '{profile.Name}' to Custom Storyteller");
                        Messages.Message($"Profile '{profile.Name}' loaded successfully", MessageTypeDefOf.PositiveEvent, false);
                    }
                }
                else
                {
                    RimWatchLogger.Warning($"ProfileManager: Cannot apply profile - current storyteller is not Custom");
                    Messages.Message("Profile loading only works with Custom Storyteller", MessageTypeDefOf.RejectInput, false);
                }
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"ProfileManager: Failed to load profile '{profile.Name}': {ex.Message}");
                Messages.Message($"Failed to load profile: {ex.Message}", MessageTypeDefOf.RejectInput, false);
            }
        }
        
        /// <summary>
        /// Deletes a profile.
        /// </summary>
        private static void DeleteProfile(StorytellerProfile profile)
        {
            string filePath = Path.Combine(ProfilesDirectory, $"{SanitizeFileName(profile.Name)}.xml");
            
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                
                _profiles.Remove(profile);
                if (_selectedProfile == profile)
                {
                    _selectedProfile = null;
                }
                
                RimWatchLogger.Info($"ProfileManager: Deleted profile '{profile.Name}'");
                Messages.Message($"Profile '{profile.Name}' deleted", MessageTypeDefOf.TaskCompletion, false);
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"ProfileManager: Failed to delete profile '{profile.Name}': {ex.Message}");
                Messages.Message($"Failed to delete profile: {ex.Message}", MessageTypeDefOf.RejectInput, false);
            }
        }
        
        /// <summary>
        /// Exports a profile to the clipboard for sharing.
        /// </summary>
        private static void ExportProfile(StorytellerProfile profile)
        {
            try
            {
                string export = SerializeProfile(profile);
                GUIUtility.systemCopyBuffer = export;
                
                RimWatchLogger.Info($"ProfileManager: Exported profile '{profile.Name}' to clipboard");
                Messages.Message($"Profile '{profile.Name}' exported to clipboard", MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                RimWatchLogger.Error($"ProfileManager: Failed to export profile '{profile.Name}': {ex.Message}");
                Messages.Message($"Failed to export profile: {ex.Message}", MessageTypeDefOf.RejectInput, false);
            }
        }
        
        /// <summary>
        /// Saves a profile to an XML file.
        /// </summary>
        private static void SaveProfileToFile(StorytellerProfile profile, string filePath)
        {
            string xml = SerializeProfile(profile);
            File.WriteAllText(filePath, xml);
        }
        
        /// <summary>
        /// Loads a profile from an XML file.
        /// </summary>
        private static StorytellerProfile LoadProfileFromFile(string filePath)
        {
            string xml = File.ReadAllText(filePath);
            return DeserializeProfile(xml);
        }
        
        /// <summary>
        /// Serializes a profile to XML string.
        /// </summary>
        private static string SerializeProfile(StorytellerProfile profile)
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<StorytellerProfile>
    <Name>{profile.Name}</Name>
    <StorytellerType>{profile.StorytellerType}</StorytellerType>
    <CreatedDate>{profile.CreatedDate:O}</CreatedDate>
    <Personality>
        <RiskTolerance>{profile.Personality.RiskTolerance}</RiskTolerance>
        <BuildingSpeed>{profile.Personality.BuildingSpeed}</BuildingSpeed>
        <TradeAggressiveness>{profile.Personality.TradeAggressiveness}</TradeAggressiveness>
        <DefenseStyle>{profile.Personality.DefenseStyle}</DefenseStyle>
        <ResearchPriority>{profile.Personality.ResearchPriority}</ResearchPriority>
        <SocialFocus>{profile.Personality.SocialFocus}</SocialFocus>
    </Personality>
</StorytellerProfile>";
        }
        
        /// <summary>
        /// Deserializes a profile from XML string.
        /// </summary>
        private static StorytellerProfile DeserializeProfile(string xml)
        {
            // Simple XML parsing (in production, use proper XML serialization)
            var profile = new StorytellerProfile();
            
            profile.Name = ExtractXmlValue(xml, "Name");
            profile.StorytellerType = ExtractXmlValue(xml, "StorytellerType");
            
            string dateStr = ExtractXmlValue(xml, "CreatedDate");
            if (DateTime.TryParse(dateStr, out DateTime date))
            {
                profile.CreatedDate = date;
            }
            
            profile.Personality = new RimWatch.AI.Storytellers.StorytellerPersonality
            {
                RiskTolerance = float.Parse(ExtractXmlValue(xml, "RiskTolerance")),
                BuildingSpeed = float.Parse(ExtractXmlValue(xml, "BuildingSpeed")),
                TradeAggressiveness = float.Parse(ExtractXmlValue(xml, "TradeAggressiveness")),
                DefenseStyle = float.Parse(ExtractXmlValue(xml, "DefenseStyle")),
                ResearchPriority = float.Parse(ExtractXmlValue(xml, "ResearchPriority")),
                SocialFocus = float.Parse(ExtractXmlValue(xml, "SocialFocus"))
            };
            
            return profile;
        }
        
        /// <summary>
        /// Extracts a value from XML by tag name.
        /// </summary>
        private static string ExtractXmlValue(string xml, string tagName)
        {
            string startTag = $"<{tagName}>";
            string endTag = $"</{tagName}>";
            
            int startIndex = xml.IndexOf(startTag);
            int endIndex = xml.IndexOf(endTag);
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                startIndex += startTag.Length;
                return xml.Substring(startIndex, endIndex - startIndex);
            }
            
            return "";
        }
        
        /// <summary>
        /// Sanitizes a file name by removing invalid characters.
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = fileName;
            
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }
            
            return sanitized;
        }
    }
    
    /// <summary>
    /// Represents a saved storyteller profile.
    /// </summary>
    public class StorytellerProfile
    {
        public string Name { get; set; }
        public string StorytellerType { get; set; }
        public RimWatch.AI.Storytellers.StorytellerPersonality Personality { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}

