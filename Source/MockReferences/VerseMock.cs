// Mock references for development without RimWorld installed
// These are ONLY used in Debug builds when RimWorld libs are not available
// DO NOT use in Release builds!

#if USE_MOCK_REFERENCES

namespace Verse
{
    public static class Log
    {
        public static void Message(string text) { }
        public static void Warning(string text) { }
        public static void Error(string text) { }
    }

    public class Mod
    {
        public Mod(ModContentPack content) { }
        public virtual void DoSettingsWindowContents(UnityEngine.Rect inRect) { }
        public virtual string SettingsCategory() { return string.Empty; }
        public virtual void WriteSettings() { }
    }

    public class ModContentPack { }

    public static class Find
    {
        public static WindowStack WindowStack { get; } = new WindowStack();
        public static Map CurrentMap { get; set; }
    }

    public class WindowStack
    {
        public void Add(Window window) { }
    }

    public class Window
    {
        public bool doCloseButton;
        public bool doCloseX;
        public bool closeOnClickedOutside;
        public bool absorbInputAroundWindow;
        public bool draggable;
        public bool resizeable;
        public UnityEngine.Rect windowRect;
        public virtual UnityEngine.Vector2 InitialSize => UnityEngine.Vector2.zero;
        public virtual void DoWindowContents(UnityEngine.Rect inRect) { }
        public void Close() { }
    }

    public class Listing_Standard
    {
        public void Begin(UnityEngine.Rect rect) { }
        public void End() { }
        public void Label(string label) { }
        public void Gap(float height) { }
        public bool ButtonText(string label) { return false; }
        public void CheckboxLabeled(string label, ref bool checkOn, string tooltip = null) { }
        public UnityEngine.Rect GetRect(float height) { return UnityEngine.Rect.zero; }
    }

    public enum GameFont { Tiny, Small, Medium }
    
    public static class Text
    {
        public static GameFont Font { get; set; }
    }

    public static class Widgets
    {
        public static bool ButtonText(UnityEngine.Rect rect, string label) { return false; }
        public static bool ButtonImage(UnityEngine.Rect rect, UnityEngine.Texture2D tex) { return false; }
        public static void Label(UnityEngine.Rect rect, string label) { }
        public static void CheckboxLabeled(UnityEngine.Rect rect, string label, ref bool checkOn, bool disabled = false) { }
    }

    public static class TooltipHandler
    {
        public static void TipRegion(UnityEngine.Rect rect, string tip) { }
    }

    public static class ContentFinder<T>
    {
        public static T Get(string path) { return default(T); }
    }

    public class MapComponent
    {
        protected Map map;
        public MapComponent(Map map) { this.map = map; }
        public virtual void MapComponentTick() { }
        public virtual void ExposeData() { }
    }

    public class Map
    {
        public int uniqueID;
        public MapPawns mapPawns = new MapPawns();
        public ListerThings listerThings = new ListerThings();
    }

    public class MapPawns
    {
        public int FreeColonistsSpawnedCount => 0;
        public System.Collections.Generic.List<Pawn> FreeColonistsSpawned => new System.Collections.Generic.List<Pawn>();
    }

    public class ListerThings
    {
        public System.Collections.Generic.List<Thing> ThingsInGroup(ThingRequestGroup group) 
        { 
            return new System.Collections.Generic.List<Thing>(); 
        }
    }

    public enum ThingRequestGroup
    {
        FoodSourceNotPlantOrTree,
        BuildingFrame,
        Plant
    }

    public class Thing
    {
        public ThingDef def;
    }

    public class ThingDef
    {
        public string defName;
    }

    public class Pawn : Thing
    {
        public bool Dead => false;
        public bool Downed => false;
        public Pawn_WorkSettings workSettings = new Pawn_WorkSettings();
        public Pawn_SkillTracker skills = new Pawn_SkillTracker();
        public bool WorkTypeIsDisabled(RimWorld.WorkTypeDef workType) { return false; }
    }

    public class Pawn_WorkSettings
    {
        public void SetPriority(RimWorld.WorkTypeDef workType, int priority) { }
        public void Disable(RimWorld.WorkTypeDef workType) { }
    }

    public class Pawn_SkillTracker
    {
        public SkillRecord GetSkill(RimWorld.SkillDef skillDef) { return null; }
    }

    public class SkillRecord
    {
        public int Level => 0;
        public Passion passion => Passion.None;
    }

    public enum Passion { None, Minor, Major }

    public class Plant : Thing
    {
        public bool HarvestableNow => false;
    }

    public class DefDatabase<T> where T : Def
    {
        public static System.Collections.Generic.List<T> AllDefsListForReading => new System.Collections.Generic.List<T>();
    }

    public class Def
    {
        public string defName;
    }

    public static class Prefs
    {
        public static bool DevMode => true;
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class StaticConstructorOnStartupAttribute : System.Attribute { }
}

namespace RimWorld
{
    public class WorkTypeDef : Verse.Def
    {
        public System.Collections.Generic.List<SkillDef> relevantSkills = new System.Collections.Generic.List<SkillDef>();
    }

    public class SkillDef : Verse.Def { }

    public class ResearchProjectDef : Verse.Def
    {
        public bool CanStartNow => false;
        public bool IsFinished => false;
    }

    public class UIRoot_Play
    {
        public void UIRootOnGUI() { }
    }
}

namespace UnityEngine
{
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
        public bool Contains(Vector2 point) { return false; }
        public static Rect zero => new Rect(0, 0, 0, 0);
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
        public static Vector2 zero => new Vector2(0, 0);
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }
        public static Color white => new Color(1, 1, 1);
        public static Color red => new Color(1, 0, 0);
        public static Color green => new Color(0, 1, 0);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f);
    }

    public class Texture2D { }

    public static class GUI
    {
        public static Color color { get; set; }
    }

    public static class UI
    {
        public static float screenWidth => 1920f;
        public static float screenHeight => 1080f;
    }

    public class Event
    {
        public static Event current => new Event();
        public EventType type => EventType.Repaint;
        public int button => 0;
        public Vector2 mousePosition => Vector2.zero;
        public void Use() { }
    }

    public enum EventType
    {
        Repaint,
        MouseDown
    }
}

namespace HarmonyLib
{
    public class Harmony
    {
        public Harmony(string id) { }
        public void PatchAll() { }
    }

    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
    public class HarmonyPatch : System.Attribute
    {
        public HarmonyPatch(System.Type type, string methodName) { }
    }
}

#endif

