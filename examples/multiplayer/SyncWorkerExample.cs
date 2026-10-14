// Historical Multiplayer.API Wiki pattern. NOT COMPILED; wrapper type is illustrative.
using Multiplayer.API;
using Verse;

namespace CodexResearch.MultiplayerExample
{
    public sealed class ExampleTarget
    {
        public Thing Thing;
        public ExampleTarget(Thing thing) { Thing = thing; }
    }

    public static class ExampleSyncWorkers
    {
        [SyncWorker]
        public static void Work(SyncWorker sync, ref ExampleTarget target)
        {
            if (sync.isWriting)
            {
                sync.Write(target.Thing);
            }
            else
            {
                target = new ExampleTarget(sync.Read<Thing>());
            }
        }
    }
}
