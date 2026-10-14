// 1.6/net472 TEMPLATE ONLY. Game DLL signature and runtime NOT tested.
using Verse;

namespace CodexResearch.Example
{
    public sealed class ExampleMod : Mod
    {
        public readonly ExampleSettings Settings;

        public ExampleMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ExampleSettings>();
        }

        public override string SettingsCategory()
        {
            return "Codex Research Example";
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            // UI intentionally omitted: rendering/widget API requires local Game DLL check.
        }
    }

    public sealed class ExampleSettings : ModSettings
    {
        public bool showDebug = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showDebug, "showDebug", false);
        }
    }
}
