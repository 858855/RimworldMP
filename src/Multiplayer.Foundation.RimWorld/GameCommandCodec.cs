using System;
using System.Collections.Generic;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using Verse;
using Verse.AI;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// Opaque game commands for the coordinator: one byte codec version, one opcode byte, then the opcode's payload.
    /// Only registered opcodes may execute (ADR 0029: the framework's own commands and the sync methods of other mods use
    /// the same table). Missing game objects make a command a deterministic no-op with a log line.
    /// </summary>
    internal static class GameCommandCodec
    {
        private const byte Version = 1;
        internal const byte SetDraft = 1;
        internal const byte SetForbidden = 2;
        internal const byte OrderedGoto = 3;
        /// <summary>A method registered through the public API (<see cref="SyncMethods"/>).</summary>
        internal const byte SyncMethod = 4;

        private static readonly Dictionary<byte, Action<ScheduledCommand>> executors = new Dictionary<byte, Action<ScheduledCommand>>
        {
            [SetDraft] = ExecuteDraft,
            [SetForbidden] = ExecuteForbid,
            [OrderedGoto] = ExecuteGoto,
            [SyncMethod] = SyncMethods.Execute,
        };

        [ThreadStatic] private static bool executing;
        [ThreadStatic] private static ScheduledCommand? current;
        internal static bool Executing => executing;
        /// <summary>The command being executed on this thread, or null.</summary>
        internal static ScheduledCommand? Current => executing ? current : null;

        internal static PayloadWriter Begin(byte opcode)
        {
            var writer = new PayloadWriter();
            writer.WriteByte(Version);
            writer.WriteByte(opcode);
            return writer;
        }

        private static PayloadReader Open(byte[] data, byte opcode)
        {
            var reader = new PayloadReader(data);
            if (reader.ReadByte() != Version || reader.ReadByte() != opcode)
                throw new ProtocolException("Unbekannter Spielbefehl oder Codec-Version.");
            return reader;
        }

        internal static byte[] Draft(int mapId, int pawnId, bool drafted)
        {
            PayloadWriter writer = Begin(SetDraft);
            GameReferenceCodec.WriteThing(writer, mapId, pawnId);
            writer.WriteBool(drafted);
            return writer.ToArray();
        }

        internal static (int MapId, int PawnId, bool Drafted) ReadDraft(byte[] data)
        {
            PayloadReader reader = Open(data, SetDraft);
            var (mapId, pawnId) = GameReferenceCodec.ReadThing(reader);
            bool drafted = reader.ReadBool();
            reader.EnsureComplete();
            return (mapId, pawnId, drafted);
        }

        internal static byte[] Forbid(int mapId, int thingId, bool forbidden)
        {
            PayloadWriter writer = Begin(SetForbidden);
            GameReferenceCodec.WriteThing(writer, mapId, thingId);
            writer.WriteBool(forbidden);
            return writer.ToArray();
        }

        internal static (int MapId, int ThingId, bool Forbidden) ReadForbid(byte[] data)
        {
            PayloadReader reader = Open(data, SetForbidden);
            var (mapId, thingId) = GameReferenceCodec.ReadThing(reader);
            bool forbidden = reader.ReadBool();
            reader.EnsureComplete();
            return (mapId, thingId, forbidden);
        }

        internal static byte[] Goto(int mapId, int pawnId, IntVec3 cell, JobTag? tag, bool queued)
        {
            PayloadWriter writer = Begin(OrderedGoto);
            GameReferenceCodec.WriteThing(writer, mapId, pawnId);
            writer.WriteUInt32(unchecked((uint)cell.x));
            writer.WriteUInt32(unchecked((uint)cell.y));
            writer.WriteUInt32(unchecked((uint)cell.z));
            writer.WriteBool(tag.HasValue);
            if (tag.HasValue) writer.WriteUInt32(unchecked((uint)tag.Value));
            writer.WriteBool(queued);
            return writer.ToArray();
        }

        internal static (int MapId, int PawnId, IntVec3 Cell, JobTag? Tag, bool Queued) ReadGoto(byte[] data)
        {
            PayloadReader reader = Open(data, OrderedGoto);
            var (mapId, pawnId) = GameReferenceCodec.ReadThing(reader);
            var cell = new IntVec3(unchecked((int)reader.ReadUInt32()), unchecked((int)reader.ReadUInt32()),
                unchecked((int)reader.ReadUInt32()));
            JobTag? tag = null;
            if (reader.ReadBool())
            {
                uint raw = reader.ReadUInt32();
                if (raw > byte.MaxValue || !Enum.IsDefined(typeof(JobTag), (byte)raw))
                    throw new ProtocolException("Ungültiger JobTag.");
                tag = (JobTag)raw;
            }
            bool queued = reader.ReadBool();
            reader.EnsureComplete();
            return (mapId, pawnId, cell, tag, queued);
        }

        /// <summary>Dispatches by opcode; an unknown opcode is a protocol error (the mod check should have prevented it).</summary>
        internal static void Execute(ScheduledCommand command)
        {
            if (command.Data.Length < 2 || command.Data[0] != Version)
                throw new ProtocolException("Unbekannter Spielbefehl oder Codec-Version.");
            if (!executors.TryGetValue(command.Data[1], out Action<ScheduledCommand>? executor))
                throw new ProtocolException("Unbekannter Spielbefehl " + command.Data[1] + ".");
            executor(command);
        }

        /// <summary>Runs <paramref name="action"/> with <see cref="Executing"/> set, so capture prefixes let the original through.</summary>
        internal static void Run(ScheduledCommand command, Action action)
        {
            executing = true; current = command;
            try { action(); }
            finally { executing = false; current = null; }
        }

        private static void ExecuteForbid(ScheduledCommand command)
        {
            var (mapId, thingId, forbidden) = ReadForbid(command.Data);
            Thing? thing = GameReferenceCodec.ResolveThing(mapId, thingId);
            if (thing == null)
            {
                Log.Warning("Multiplayer Foundation: Forbid-Befehl ohne vorhandenes Thing/Map als No-op ausgeführt.");
                return;
            }
            Run(command, () => ForbidUtility.SetForbidden(thing, forbidden, false));
        }

        private static void ExecuteGoto(ScheduledCommand command)
        {
            var (mapId, pawnId, cell, tag, queued) = ReadGoto(command.Data);
            Pawn? pawn = GameReferenceCodec.ResolveThing(mapId, pawnId) as Pawn;
            if (pawn?.jobs == null || pawn.Map == null || !cell.InBounds(pawn.Map))
            {
                Log.Warning("Multiplayer Foundation: Goto-Befehl ohne gültigen Pawn, Map oder Ziel als No-op ausgeführt.");
                return;
            }
            Job job = JobMaker.MakeJob(JobDefOf.Goto, new LocalTargetInfo(cell));
            Run(command, () => pawn.jobs.TryTakeOrderedJob(job, tag, queued));
        }

        private static void ExecuteDraft(ScheduledCommand command)
        {
            var (mapId, pawnId, drafted) = ReadDraft(command.Data);
            Pawn? pawn = GameReferenceCodec.ResolveThing(mapId, pawnId) as Pawn;
            if (pawn?.drafter == null)
            {
                Log.Warning("Multiplayer Foundation: Draft-Befehl ohne vorhandenen Pawn/Map als No-op ausgeführt.");
                return;
            }
            Run(command, () => pawn.drafter.Drafted = drafted);
        }
    }
}
