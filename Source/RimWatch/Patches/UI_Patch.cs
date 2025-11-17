using HarmonyLib;
using RimWatch.Keybindings;
using RimWatch.UI;
using RimWatch.Utils;
using RimWorld;

namespace RimWatch.Patches
{
    /// <summary>
    /// Harmony патч для интеграции UI RimWatch в RimWorld
    /// </summary>
    [HarmonyPatch(typeof(UIRoot_Play), "UIRootOnGUI")]
    [HarmonyPriority(Priority.Last)]
    public static class UI_Integration_Patch
    {
        private static bool initialized = false;

        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                // Инициализация при первом вызове
                if (!initialized)
                {
                    RimWatchLogger.Info("UI integration initialized (hotkey only mode)");
                    initialized = true;
                }

                // Обработать горячие клавиши
                RimWatchKeybindings.HandleKeybindings();

                // Кнопка удалена - используй Shift+R для открытия панели
            }
            catch (System.Exception ex)
            {
                RimWatchLogger.Error("Error in UI integration", ex);
            }
        }
    }
}

