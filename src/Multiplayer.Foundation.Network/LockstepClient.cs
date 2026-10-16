using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Network
{
    /// <summary>
    /// The game side of the lockstep rule (ADR 0030), without Verse, for the tick driver:
    /// before running tick c+1, take the commands for boundary c; run c+1 only while <see cref="MayRunTick"/> says so.
    /// While paused at c, take the commands for c every frame, so orders given in pause act at once.
    /// Anything that would break the shared order (sequence gap, passed boundary, shrinking horizon) throws.
    /// </summary>
    public sealed class LockstepClient
    {
        private readonly SortedDictionary<int, List<ScheduledCommand>> pending = new SortedDictionary<int, List<ScheduledCommand>>();
        private readonly List<(int Tick, IReadOnlyList<Guid> Sessions)> desyncs = new List<(int, IReadOnlyList<Guid>)>();
        private ulong lastSequence;
        private int lastTaken = -1;
        private int lastReported = -1;
        private long? lastReportMs;

        public int Horizon { get; private set; }
        /// <summary>Changes only when the pause command is executed, at the same point in every game.</summary>
        public bool Paused { get; private set; } = true;
        public byte Speed { get; private set; } = 1;
        public Guid SlowestSession { get; private set; }
        public int SlowestTick { get; private set; }
        public IReadOnlyList<(int Tick, IReadOnlyList<Guid> Sessions)> Desyncs => desyncs;

        public static bool Handles(WireMessage message) =>
            message.Type == MessageType.ScheduledCommand || message.Type == MessageType.TickHorizon ||
            message.Type == MessageType.DesyncDetected;

        public void Receive(WireMessage message)
        {
            switch (message.Type)
            {
                case MessageType.ScheduledCommand:
                    ScheduledCommand command = LockstepPayloads.ReadScheduled(message.Payload);
                    if (command.Sequence != lastSequence + 1) throw new ProtocolException("Lockstep sequence gap.");
                    if (command.Boundary < lastTaken) throw new ProtocolException("Command for a boundary already passed.");
                    if (command.Boundary > Horizon) throw new ProtocolException("Command beyond the horizon.");
                    lastSequence = command.Sequence;
                    if (!pending.TryGetValue(command.Boundary, out var list)) pending[command.Boundary] = list = new List<ScheduledCommand>();
                    list.Add(command);
                    break;
                case MessageType.TickHorizon:
                    TickHorizon horizon = LockstepPayloads.ReadHorizon(message.Payload);
                    if (horizon.Horizon < Horizon) throw new ProtocolException("Horizon moved back.");
                    Horizon = horizon.Horizon; SlowestSession = horizon.SlowestSession; SlowestTick = horizon.SlowestTick;
                    break;
                case MessageType.DesyncDetected:
                    desyncs.Add(LockstepPayloads.ReadDesync(message.Payload));
                    break;
                default:
                    throw new ArgumentException("Not a lockstep message.", nameof(message));
            }
        }

        /// <summary>
        /// Game commands to run now, after completed tick <paramref name="completedTick"/>, in shared order.
        /// Pause and speed commands are applied here and not returned.
        /// </summary>
        public IReadOnlyList<ScheduledCommand> TakeCommandsAt(int completedTick)
        {
            if (completedTick < lastTaken) throw new InvalidOperationException("Ticks must not go back.");
            if (pending.Count > 0 && pending.Keys.First() < completedTick)
                throw new InvalidOperationException("A tick ran before its commands were executed.");
            lastTaken = completedTick;
            if (!pending.TryGetValue(completedTick, out var list)) return Array.Empty<ScheduledCommand>();
            pending.Remove(completedTick);
            var game = new List<ScheduledCommand>(list.Count);
            foreach (ScheduledCommand command in list)
            {
                if (command.Kind == ScheduledKind.Pause) Paused = command.Data[0] == 1;
                else if (command.Kind == ScheduledKind.Speed) Speed = command.Data[0];
                else game.Add(command);
            }
            return game;
        }

        /// <summary>
        /// True when tick completedTick+1 may run: within the horizon, the commands for this point were taken with
        /// <see cref="TakeCommandsAt"/>, and none arrived since. A forgotten take therefore stops the game instead of
        /// letting it run past commands.
        /// </summary>
        public bool MayRunTick(int completedTick) =>
            completedTick < Horizon && completedTick == lastTaken && (pending.Count == 0 || pending.Keys.First() > completedTick);

        /// <summary>ClientTick for the coordinator, at most every 100 ms and only when progress was made.</summary>
        public WireMessage? ProgressMessage(int completedTick, long nowMs)
        {
            // Nullable on purpose: "now - long.MinValue" overflows to a negative number and would block every report.
            if (completedTick <= lastReported || (lastReportMs != null && nowMs - lastReportMs.Value < 100)) return null;
            lastReported = completedTick; lastReportMs = nowMs;
            return new WireMessage(MessageType.ClientTick, 0, LockstepPayloads.Tick(completedTick));
        }

        public static WireMessage HashMessage(int tick, ulong stateHash) =>
            new WireMessage(MessageType.StateHash, 0, LockstepPayloads.Hash(tick, stateHash));

        public static WireMessage CommandMessage(byte[] data) =>
            new WireMessage(MessageType.Command, 0, LockstepPayloads.Command(data));

        public static WireMessage ControlMessage(ScheduledKind kind, byte value) =>
            new WireMessage(MessageType.SessionControl, 0, LockstepPayloads.Control(kind, value));
    }
}
