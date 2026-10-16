using System;
using System.Linq;
using Multiplayer.Foundation.Protocol;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Opaque game commands for the coordinator. The first registered action is draft/undraft.</summary>
    internal static class GameCommandCodec
    {
        private const byte Version = 1;
        private const byte SetDraft = 1;

        [ThreadStatic] private static bool executing;
        internal static bool Executing => executing;

        internal static byte[] Draft(int mapId, int pawnId, bool drafted)
        {
            var writer = new PayloadWriter();
            writer.WriteByte(Version);
            writer.WriteByte(SetDraft);
            writer.WriteUInt32(unchecked((uint)mapId));
            writer.WriteUInt32(unchecked((uint)pawnId));
            writer.WriteBool(drafted);
            return writer.ToArray();
        }

        internal static (int MapId, int PawnId, bool Drafted) ReadDraft(byte[] data)
        {
            var reader = new PayloadReader(data);
            if (reader.ReadByte() != Version || reader.ReadByte() != SetDraft)
                throw new ProtocolException("Unbekannter Spielbefehl oder Codec-Version.");
            int mapId = unchecked((int)reader.ReadUInt32());
            int pawnId = unchecked((int)reader.ReadUInt32());
            bool drafted = reader.ReadBool();
            reader.EnsureComplete();
            return (mapId, pawnId, drafted);
        }

        internal static void Execute(ScheduledCommand command)
        {
            var (mapId, pawnId, drafted) = ReadDraft(command.Data);
            Map? map = Find.Maps.FirstOrDefault(m => m.uniqueID == mapId);
            Pawn? pawn = map?.listerThings.AllThings.OfType<Pawn>().FirstOrDefault(p => p.thingIDNumber == pawnId);
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
