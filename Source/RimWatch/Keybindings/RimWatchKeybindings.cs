using RimWatch.Core;
using RimWatch.UI;
using RimWatch.Utils;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWatch.Keybindings
{
    /// <summary>
    /// Горячие клавиши для RimWatch
    /// </summary>
    public static class RimWatchKeybindings
    {
        /// <summary>
        /// Обработать горячие клавиши
        /// </summary>
        public static void HandleKeybindings()
        {
            // Нужно быть в игре
            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            // Shift + R - Open RimWatch Settings Panel (ЕДИНСТВЕННАЯ КОМАНДА)
            if (currentEvent.shift && !currentEvent.control && !currentEvent.alt && currentEvent.keyCode == KeyCode.R)
            {
                RimWatchLogger.Debug("Hotkey: Shift+R - Opening RimWatch Main Panel");
                Find.WindowStack.Add(new RimWatchMainPanel());
                currentEvent.Use();
            }
        }
    }
}

