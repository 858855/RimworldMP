// Schematic, historic API-Wiki semantics. NOT COMPILED.
using Multiplayer.API;

namespace CodexResearch.MultiplayerExample
{
    public sealed class ResearchUiState
    {
        [SyncField]
        public float capacity;
    }

    public static class ResearchUi
    {
        public static void DrawSyncUi(ResearchUiState state)
        {
            if (state == null) return;
            bool multiplayer = MP.enabled && MP.IsInMultiplayer;
            if (multiplayer) MP.WatchBegin();
            try
            {
                if (multiplayer) MP.Watch(state, nameof(ResearchUiState.capacity));
                // Insert your real UI slider here; no direct world mutation.
            }
            finally
            {
                if (multiplayer) MP.WatchEnd();
            }
        }
    }
}
