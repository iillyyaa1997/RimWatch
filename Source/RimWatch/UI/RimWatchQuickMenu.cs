using RimWatch.Core;
using RimWatch.Utils;
using UnityEngine;
using Verse;

namespace RimWatch.UI
{
    /// <summary>
    /// Быстрое меню RimWatch (открывается по ПКМ на кнопку)
    /// Now uses UnifiedSettingsUI for consistency!
    /// </summary>
    public class RimWatchQuickMenu : Window
    {
        public override Vector2 InitialSize => new Vector2(600f, 800f);

        public RimWatchQuickMenu()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
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
            // Use the same unified UI as Mod Settings!
            RimWatch.UI.UnifiedSettingsUI.DrawAllSettings(inRect, RimWatchMod.Settings, isQuickPanel: true);
        }
    }
}

