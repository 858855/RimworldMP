using System;
using System.Collections.Generic;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Session
{
    /// <summary>
    /// SYNC-001 (ADR 0024): what one player's game knows about the lockstep session: the horizon it may simulate to and
    /// the commands still to run, in the server's order. The game runs a command for tick t when it is at t − 1.
    /// </summary>
    public sealed class LockstepClientState
    {
        private readonly Queue<ScheduledCommand> pending = new Queue<ScheduledCommand>();
        private uint lastSeq;
        private int lastTick;

        /// <summary>−1 until the server has started the session.</summary>
        public int Horizon { get; private set; } = -1;
        public bool Started => Horizon >= 0;
        public SessionSpeed Pace { get; private set; }
        public Guid WaitingFor { get; private set; }
        public int WaitingTick { get; private set; }
        public int CheckedTick { get; private set; } = -1;
        public int PendingCount => pending.Count;
        /// <summary>Commands that arrived for a tick this game had already run: it differs from the others from then on.</summary>
        public int LateCommands { get; private set; }

        public void OnHorizon(TickHorizon horizon)
        {
            if (horizon == null) throw new ArgumentNullException(nameof(horizon));
            if (horizon.Horizon < Horizon) throw new ProtocolException("The tick horizon went back.");
            Horizon = horizon.Horizon; Pace = horizon.Speed; WaitingFor = horizon.WaitingFor; WaitingTick = horizon.WaitingTick;
            CheckedTick = Math.Max(CheckedTick, horizon.CheckedTick);
        }

        public void OnScheduled(ScheduledCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (command.Seq <= lastSeq || command.Tick < lastTick) throw new ProtocolException("Lockstep commands out of order.");
            lastSeq = command.Seq; lastTick = command.Tick;
            pending.Enqueue(command);
        }

        /// <summary>The next command to run before tick <paramref name="localTick"/> + 1, or null.</summary>
        public ScheduledCommand? TakeDue(int localTick)
        {
            while (pending.Count > 0 && pending.Peek().Tick <= localTick) { pending.Dequeue(); LateCommands++; }
            return pending.Count > 0 && pending.Peek().Tick == localTick + 1 ? pending.Dequeue() : null;
        }

        /// <summary>Ticks the game may run now. A pending command for the next tick must run first (<see cref="TakeDue"/>).</summary>
        public int TicksAllowed(int localTick) => Started ? Math.Max(0, Horizon - localTick) : 0;
    }
}
