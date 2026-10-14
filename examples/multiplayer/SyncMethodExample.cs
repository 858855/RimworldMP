// RimWorld 1.6 + Multiplayer.API 0.6.0 MODEL ONLY, NOT COMPILED.
// IMPORTANT: verify installed Multiplayer.API assembly and every type/overload.
using Multiplayer.API;
using Verse;

namespace CodexResearch.MultiplayerExample
{
    [StaticConstructorOnStartup]
    public static class ExampleMpRegistration
    {
        static ExampleMpRegistration()
        {
            if (MP.enabled)
                MP.RegisterAll();
        }
    }

    public static class ExampleMpCommands
    {
        // In production the `Thing` must hold a verifiable ThingComp and persistent state.
        [SyncMethod]
        public static void SetMachineEnabled(Thing thing, bool enable)
        {
            if (thing == null || thing.Destroyed) return;
            // TODO (not a runnable mod): resolve CompResearch and set comp.enabled,
            // validating allowed actor/Map/Thing ownership before world mutation.
        }
    }
}
