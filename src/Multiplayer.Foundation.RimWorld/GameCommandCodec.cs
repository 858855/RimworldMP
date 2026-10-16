using System;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Opaque game commands for the coordinator. Only explicitly recognized actions may execute.</summary>
    internal static class GameCommandCodec
    {
        private const byte Version = 1;
        private const byte SetDraft = 1;
        private const byte SetForbidden = 2;

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

        internal static void Execute(ScheduledCommand command)
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
            var (mapId, pawnId, drafted) = ReadDraft(command.Data);
            Pawn? pawn = GameReferenceCodec.ResolveThing(mapId, pawnId) as Pawn;
            if (pawn?.drafter == null)
            {
                Log.Warning("Multiplayer Foundation: Draft-Befehl ohne vorhandenen Pawn/Map als No-op ausgeführt.");
                return;
            }
            executing = true;
            try { pawn.drafter.Drafted = drafted; }
            finally { executing = false; }
        }
    }
}
