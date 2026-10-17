using System;
using System.Linq;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SAVE-002 (ADR 0027) runtime check without a second player, appended to the game self-test with
    /// <c>-mpContinueSelfTest</c>: back in the main menu the mod hosts a local lobby (debug solo start), loads the game
    /// self-test's own save while the lobby connection stays, the loaded game names the save's players to the server
    /// (code 105), the host presses "fortsetzen" and the real host path (rebind, save, reload, fingerprint, Playing) runs.
    /// One process, loopback only; the guest side is covered by the hosting tests.
    /// </summary>
    internal static class ContinueSelfTest
    {
        internal static bool Requested => Environment.GetCommandLineArgs().Any(a => a == "-mpContinueSelfTest");

        private static int stage;
        private static float stageAt;
        private static int savedTick;
        private static Guid lobbyId;
        private static DateTime startedUtc;

        internal static void Begin(int tick)
        {
            stage = 1; stageAt = UnityEngine.Time.realtimeSinceStartup; savedTick = tick; startedUtc = DateTime.UtcNow;
        }

        /// <summary>One step per frame; true when the test is over (results went through <paramref name="check"/>).</summary>
        internal static bool Step(Action<string, bool> check, string hostSave)
        {
            FoundationMod? mod = FoundationMod.Instance;
            if (mod == null) { check("continueMod", false); return true; }
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now - stageAt > 120f)
            {
                Log.Error("Multiplayer Foundation continue self-test: stage " + stage + " timed out; status=" + mod.SelfTestStatus + " game=" + mod.SelfTestGameText +
                    " phase=" + (mod.SelfTestLobby?.Phase.ToString() ?? "-") + " restored=" + (mod.SelfTestLobby?.Restored.ToString() ?? "-"));
                check("continueTimeout" + stage, false);
                Cleanup(mod);
                return true;
            }
            switch (stage)
            {
                case 1:
                    // Like a host who starts the game and opens the multiplayer window: back to the main menu first.
                    if (Current.ProgramState != ProgramState.Playing || LongEventHandler.AnyEventNowOrWaiting) return false;
                    GenScene.GoToMainMenu();
                    stage = 2; stageAt = now;
                    return false;
                case 2:
                    if (Current.ProgramState != ProgramState.Entry || LongEventHandler.AnyEventNowOrWaiting) return false;
                    mod.SelfTestHost();
                    stage = 3; stageAt = now;
                    return false;
                case 3:
                    // The local lobby exists; pick the save like "Spielstand fortsetzen …" does.
                    LobbyView? lobby = mod.SelfTestLobby;
                    if (lobby == null || lobby.Phase != SetupPhase.Lobby) return false;
                    lobbyId = lobby.Id;
                    check("continueLobby", true);
                    mod.SelfTestLoadSave(hostSave);
                    stage = 4; stageAt = now;
                    return false;
                case 4:
                    // The save is loaded, the lobby survived the scene change and now knows the save's players (105).
                    if (Current.ProgramState != ProgramState.Playing || LongEventHandler.AnyEventNowOrWaiting) return false;
                    LobbyView? kept = mod.SelfTestLobby;
                    if (kept == null) { check("continueLobbyKept", false); Cleanup(mod); return true; }
                    if (!kept.Restored) return false;
                    check("continueLobbyKept", kept.Id == lobbyId);
                    check("continueRestored", true);
                    mod.SelfTestReady();
                    stage = 5; stageAt = now;
                    return false;
                case 5:
                    if (mod.SelfTestLobby?.Players.All(p => p.Ready) != true) return false;
                    mod.SelfTestAdvance(SetupPhase.Generating);
                    stage = 6; stageAt = now;
                    return false;
                case 6:
                    // Host path: rebind the saved session, save, reload, fingerprint, then the server lands everyone.
                    LobbyView? playing = mod.SelfTestLobby;
                    if (playing == null) { check("continuePlaying", false); Cleanup(mod); return true; }
                    if (playing.Phase != SetupPhase.Playing || LongEventHandler.AnyEventNowOrWaiting || Current.ProgramState != ProgramState.Playing) return false;
                    MultiplayerSession? session = MultiplayerSession.ForCurrentGame;
                    check("continuePlaying", true);
                    check("continueSession", session?.Active == true && session.SessionId == playing.Id.ToString("N"));
                    check("continueOwners", session?.Owners.Count() == 2);
                    check("continueTick", Find.TickManager.TicksGame == savedTick);
                    stage = 7; stageAt = now;
                    return false;
                case 7:
                    // The tick driver is up and the coordinator starts paused: no tick runs on its own.
                    if (now - stageAt < 2f) return false;
                    check("continueDriver", mod.SelfTestDriverRunning);
                    check("continueHeld", Find.TickManager.TicksGame == savedTick);
                    Cleanup(mod);
                    return true;
                default:
                    return true;
            }
        }

        private static void Cleanup(FoundationMod mod)
        {
            mod.SelfTestDisconnect();
            // The host path wrote "Multiplayer - Selbsttest Fortsetzen - <date>"; only files of this run are removed.
            foreach (System.IO.FileInfo file in GenFilePaths.AllSavedGameFiles.ToList())
            {
                if (!file.Name.StartsWith("Multiplayer - Selbsttest Fortsetzen", StringComparison.Ordinal) || file.LastWriteTimeUtc < startedUtc.AddMinutes(-1)) continue;
                try { file.Delete(); }
                catch (Exception error) { Log.Warning("Multiplayer Foundation continue self-test: could not remove " + file.Name + ": " + error.Message); }
            }
        }
    }
}
