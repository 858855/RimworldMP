using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Game-thread bridge between the vanilla tick loop and ADR 0030's coordinator.</summary>
    internal sealed class SessionTickDriver
    {
        private readonly LockstepClient lockstep;
        private readonly Action<IReadOnlyList<ScheduledCommand>> saveAtBoundary;
        private readonly int landingTicksGame;
        private readonly SessionRandom random;
        private int lastHashTick;
        private readonly int partialHashBoundary;
        private bool savePending;
        private bool awaitingResync;
        private bool catchingUp;
        private bool tickRandomActive;
        private int currentTick;

        internal SessionTickDriver(Guid lobbyId, int landingTicksGame, Action<IReadOnlyList<ScheduledCommand>> saveAtBoundary,
            JoinSnapshot? join = null)
        {
            LobbyId = lobbyId; this.landingTicksGame = landingTicksGame; this.saveAtBoundary = saveAtBoundary;
            lockstep = join == null ? new LockstepClient() : LockstepClient.ForJoin(join);
            random = new SessionRandom(lobbyId);
            catchingUp = join != null;
            lastHashTick = join?.Boundary ?? 0;
            partialHashBoundary = join != null && join.Boundary % 60 != 0
                ? (join.Boundary / 60 + 1) * 60 : -1;
        }

        internal Guid LobbyId { get; }
        internal bool Faulted { get; private set; }
        internal string Failure { get; private set; } = "";
        internal bool Paused => lockstep.Paused || Faulted || awaitingResync || savePending;
        internal bool AwaitingResync => awaitingResync;
        internal bool CatchingUp => catchingUp;
        internal bool SavePending => savePending;
        internal void MarkSynced() => catchingUp = false;
        internal byte Speed => lockstep.Speed;
        internal int Horizon => lockstep.Horizon;
        internal Guid SlowestSession => lockstep.SlowestSession;
        internal int SlowestTick => lockstep.SlowestTick;

        internal int CompletedTick(TickManager manager) => manager.TicksGame - landingTicksGame;

        internal void Receive(WireMessage message)
        {
            if (Faulted) return;
            lockstep.Receive(message);
            if (message.Type == MessageType.DesyncDetected)
                awaitingResync = true;
        }

        internal void Stop(string reason) { Faulted = true; Failure = reason; }

        /// <summary>Called every frame, including while paused, and immediately before each possible tick.</summary>
        internal void TakeAtCurrentBoundary(TickManager manager)
        {
            if (Faulted || awaitingResync) return;
            int completed = CompletedTick(manager);
            if (completed < 0) { Stop("Spiel-Ticks liegen vor dem gemeinsamen Startwert."); return; }
            var commands = lockstep.TakeCommandsAt(completed);
            if (commands.Any(c => c.Kind == ScheduledKind.Game))
                Stop("Ein Spielbefehl ist eingetroffen, aber der Ausführer (SYNC-002) fehlt noch.");
            else if (commands.Any(c => c.Kind != ScheduledKind.Save))
                Stop("Unbekannte Art eines geplanten Befehls.");
            else if (commands.Count != 0)
            {
                savePending = true;
                saveAtBoundary(commands);
            }
        }

        internal void FinishSave() => savePending = false;

        internal bool MayRunTick(TickManager manager)
        {
            TakeAtCurrentBoundary(manager);
            int completed = CompletedTick(manager);
            if (Paused || completed < 0 || !lockstep.MayRunTick(completed)) return false;
            currentTick = completed;
            random.BeginTick(completed + 1);
            tickRandomActive = true;
            return true;
        }

        internal void EndTick(TickManager manager)
        {
            AbortTickRandom();
            if (CompletedTick(manager) != currentTick + 1)
                Stop("Der Vanilla-Tick wurde trotz Freigabe nicht ausgeführt.");
        }

        internal void AbortTickRandom()
        {
            if (!tickRandomActive) return;
            tickRandomActive = false;
            random.End();
        }

        // SYNC-002 will call these around each ordered gameplay command.
        internal void BeginCommandRandom(int boundary, ulong sequence) => random.BeginCommand(boundary, sequence);
        internal void EndCommandRandom() => random.End();

        internal WireMessage? Progress(TickManager manager, long nowMs)
        {
            if (Faulted) return null;
            return lockstep.ProgressMessage(CompletedTick(manager), nowMs);
        }

        internal WireMessage? HashAfterTick(TickManager manager)
        {
            if (Faulted) return null;
            int completed = CompletedTick(manager);
            if (completed <= 0 || completed % 60 != 0 || completed <= lastHashTick) return null;
            lastHashTick = completed;
            var (draws, valueSum) = random.TakeWindow();
            // A joiner did not execute the whole first 60-tick window. The coordinator must not compare it.
            if (completed == partialHashBoundary) return null;
            return LockstepClient.HashMessage(completed, SessionStateHash.Compute(manager, draws, valueSum));
        }
    }
}
