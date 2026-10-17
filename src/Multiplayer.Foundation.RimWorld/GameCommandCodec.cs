using System;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using Verse;
using Verse.AI;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Opaque game commands for the coordinator. Only explicitly recognized actions may execute.</summary>
    internal static class GameCommandCodec
    {
        private const byte Version = 1;
        private const byte SetDraft = 1;
        private const byte SetForbidden = 2;
        private const byte OrderedGoto = 3;

        [ThreadStatic] private static bool executing;
        internal static bool Executing => executing;

        internal static byte[] Draft(int mapId, int pawnId, bool drafted)
        {
            var writer = new PayloadWriter();
            writer.WriteByte(Version);
            writer.WriteByte(SetDraft);
            GameReferenceCodec.WriteThing(writer, mapId, pawnId);
            writer.WriteBool(drafted);
            return writer.ToArray();
        }

        internal static (int MapId, int PawnId, bool Drafted) ReadDraft(byte[] data)
        {
            var reader = new PayloadReader(data);
            if (reader.ReadByte() != Version || reader.ReadByte() != SetDraft)
                throw new ProtocolException("Unbekannter Spielbefehl oder Codec-Version.");
            var (mapId, pawnId) = GameReferenceCodec.ReadThing(reader);
            bool drafted = reader.ReadBool();
            reader.EnsureComplete();
            return (mapId, pawnId, drafted);
        }

        internal static byte[] Forbid(int mapId, int thingId, bool forbidden)
        {
            var writer = new PayloadWriter();
            writer.WriteByte(Version);
            writer.WriteByte(SetForbidden);
            GameReferenceCodec.WriteThing(writer, mapId, thingId);
            writer.WriteBool(forbidden);
            return writer.ToArray();
        }

        internal static (int MapId, int ThingId, bool Forbidden) ReadForbid(byte[] data)
        {
            var reader = new PayloadReader(data);
            if (reader.ReadByte() != Version || reader.ReadByte() != SetForbidden)
                throw new ProtocolException("Unbekannter Spielbefehl oder Codec-Version.");
            var (mapId, thingId) = GameReferenceCodec.ReadThing(reader);
            bool forbidden = reader.ReadBool();
            reader.EnsureComplete();
            return (mapId, thingId, forbidden);
        }

        internal static byte[] Goto(int mapId, int pawnId, IntVec3 cell, JobTag? tag, bool queued)
        {
            var writer = new PayloadWriter();
            writer.WriteByte(Version);
            writer.WriteByte(OrderedGoto);
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
            var reader = new PayloadReader(data);
            if (reader.ReadByte() != Version || reader.ReadByte() != OrderedGoto)
                throw new ProtocolException("Unbekannter Spielbefehl oder Codec-Version.");
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

        /// <param name="localSession">This game's session; only the issuer's own game shows why a command was refused.</param>
        internal static void Execute(ScheduledCommand command, Guid localSession)
        {
            if (command.Data.Length < 2 || command.Data[0] != Version)
                throw new ProtocolException("Unbekannter Spielbefehl oder Codec-Version.");
            if (command.Data[1] == SetForbidden)
            {
                var (forbidMapId, thingId, forbidden) = ReadForbid(command.Data);
                Thing? thing = GameReferenceCodec.ResolveThing(forbidMapId, thingId);
                if (thing == null)
                {
                    Log.Warning("Multiplayer Foundation: Forbid-Befehl ohne vorhandenes Thing/Map als No-op ausgeführt.");
                    return;
                }
                executing = true;
                try { ForbidUtility.SetForbidden(thing, forbidden, false); }
                finally { executing = false; }
                return;
            }
            if (command.Data[1] == OrderedGoto)
            {
                var (gotoMapId, gotoPawnId, cell, tag, queued) = ReadGoto(command.Data);
                Pawn? orderedPawn = GameReferenceCodec.ResolveThing(gotoMapId, gotoPawnId) as Pawn;
                if (orderedPawn?.jobs == null || orderedPawn.Map == null || !cell.InBounds(orderedPawn.Map))
                {
                    Log.Warning("Multiplayer Foundation: Goto-Befehl ohne gültigen Pawn, Map oder Ziel als No-op ausgeführt.");
                    return;
                }
                if (Refused(command, localSession, orderedPawn, "Spielauftrag")) return;
                Job job = JobMaker.MakeJob(JobDefOf.Goto, new LocalTargetInfo(cell));
                executing = true;
                try { orderedPawn.jobs.TryTakeOrderedJob(job, tag, queued); }
                finally { executing = false; }
                return;
            }
            var (mapId, pawnId, drafted) = ReadDraft(command.Data);
            Pawn? pawn = GameReferenceCodec.ResolveThing(mapId, pawnId) as Pawn;
            if (pawn?.drafter == null)
            {
                Log.Warning("Multiplayer Foundation: Draft-Befehl ohne vorhandenen Pawn/Map als No-op ausgeführt.");
                return;
            }
            if (Refused(command, localSession, pawn, "Zeichnen-Befehl")) return;
            executing = true;
            try { pawn.drafter.Drafted = drafted; }
            finally { executing = false; }
        }

        // SYNC-005 (plan §7.7): a command for a pawn the issuer may not control is a no-op in every game; the issuer's own
        // game says why. Forbid is a colony action and stays open to everyone. Uses the key the coordinator stamped.
        private static bool Refused(ScheduledCommand command, Guid localSession, Pawn pawn, string action)
        {
            string? denial = PawnControl.Denial(pawn, command.IssuerKey, MultiplayerSession.ForCurrentGame);
            if (denial == null) return false;
            Log.Message("Multiplayer Foundation: " + action + " von " + (command.IssuerKey?.ToString() ?? "?") + " für " +
                pawn.LabelShort + " abgelehnt: " + denial);
            if (command.IssuerSession == localSession)
                Messages.Message("Keine Berechtigung: " + denial, MessageTypeDefOf.RejectInput, false);
            return true;
        }
    }
}
