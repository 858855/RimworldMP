using System;
using System.Linq;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Game-thread bridge between the vanilla tick loop and ADR 0030's coordinator.</summary>
    internal sealed class SessionTickDriver
    {
        private readonly LockstepClient lockstep = new LockstepClient();
        private readonly int landingTicksGame;
        private int lastHashTick;

        internal SessionTickDriver(Guid lobbyId, int landingTicksGame)
        { LobbyId = lobbyId; this.landingTicksGame = landingTicksGame; }

        internal Guid LobbyId { get; }
        internal bool Faulted { get; private set; }
        internal string Failure { get; private set; } = "";
        internal bool Paused => lockstep.Paused || Faulted;
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
                Stop("Abweichender Spielzustand gemeldet; die Sitzung wurde angehalten.");
        }

        internal void Stop(string reason) { Faulted = true; Failure = reason; }

        /// <summary>Called every frame, including while paused, and immediately before each possible tick.</summary>
        internal void TakeAtCurrentBoundary(TickManager manager)
        {
            if (Faulted) return;
            int completed = CompletedTick(manager);
            if (completed < 0) { Stop("Spiel-Ticks liegen vor dem gemeinsamen Startwert."); return; }
            var commands = lockstep.TakeCommandsAt(completed);
            if (commands.Count != 0)
                Stop("Ein Spielbefehl ist eingetroffen, aber der Ausführer (SYNC-002) fehlt noch.");
        }

        internal bool MayRunTick(TickManager manager)
        {
            TakeAtCurrentBoundary(manager);
            int completed = CompletedTick(manager);
            return !Paused && completed >= 0 && lockstep.MayRunTick(completed);
        }

        internal WireMessage? Progress(TickManager manager, long nowMs)
        {
            if (Faulted) return null;
            return lockstep.ProgressMessage(CompletedTick(manager), nowMs);
        }

        internal WireMessage? HashAfterTick(TickManager manager)
        {
            if (Faulted) return null;
            int completed = CompletedTick(manager);
            if (completed <= 0 || completed % 60 != 0 || completed == lastHashTick) return null;
            lastHashTick = completed;
            return LockstepClient.HashMessage(completed, CoreHash(manager));
        }

        /// <summary>Provisional core hash. X3 will expand this to all gameplay state and controlled Rand.</summary>
        private static ulong CoreHash(TickManager manager)
        {
            const ulong offset = 14695981039346656037UL, prime = 1099511628211UL;
            ulong hash = offset;
            void Add(int value) { unchecked { hash = (hash ^ (uint)value) * prime; } }
            Add(manager.TicksGame);
            foreach (Map map in Find.Maps.OrderBy(m => m.uniqueID))
            {
                Add(map.uniqueID);
                foreach (Pawn pawn in map.mapPawns.FreeColonists.OrderBy(p => p.thingIDNumber))
                {
                    Add(pawn.thingIDNumber);
                    Add(pawn.Position.x); Add(pawn.Position.y); Add(pawn.Position.z);
                }
            }
            return hash;
        }
    }
}
