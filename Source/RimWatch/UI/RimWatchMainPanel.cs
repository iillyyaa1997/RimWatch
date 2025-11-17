using RimWatch.Core;
using RimWatch.Settings;
using RimWatch.Utils;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWatch.UI
{
    /// <summary>
    /// Главная панель управления RimWatch - Now uses UnifiedSettingsUI!
    /// </summary>
    public class RimWatchMainPanel : Window
    {
        public override Vector2 InitialSize => new Vector2(600f, 800f);

        public RimWatchMainPanel()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            closeOnAccept = false;
            closeOnCancel = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;

            // Center on screen
            windowRect = new Rect(
                (UnityEngine.Screen.width - 600f) / 2f,
                (UnityEngine.Screen.height - 800f) / 2f,
                600f,
                800f
            );
        }

        public override void DoWindowContents(Rect inRect)
        {
            // ✅ Use UnifiedSettingsUI - Same UI as Mod Settings and Quick Menu!
            RimWatch.UI.UnifiedSettingsUI.DrawAllSettings(inRect, RimWatchMod.Settings, isQuickPanel: true);
        }
    }
}

