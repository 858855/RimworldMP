// Pedagogical 1.6 component patterns. NOT COMPILED; method signatures require game assembly verification.
using Verse;

namespace CodexResearch.Components
{
    public class ExampleGameState : GameComponent
    {
        private int totalActions;
        public ExampleGameState(Game game) : base() { }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref totalActions, "totalActions", 0);
        }
    }

    public class ExampleMapState : MapComponent
    {
        private int mapCounter;
        public ExampleMapState(Map map) : base(map) { }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref mapCounter, "mapCounter", 0);
        }
    }

    public class CompProperties_Research : CompProperties
    {
        public int workTicks = 60;
        public CompProperties_Research()
        {
            compClass = typeof(CompResearch);
        }
    }

    public class CompResearch : ThingComp
    {
        public bool enabled = true;
        private int progress;

        public override void CompTick()
        {
            base.CompTick();
            // Intentionally no automatic world mutations in this sample.
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref progress, "progress", 0);
        }
    }
}
