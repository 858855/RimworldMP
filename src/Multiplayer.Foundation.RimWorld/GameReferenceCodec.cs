using System.Linq;
using Multiplayer.Foundation.Protocol;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Stable reference for a spawned Thing. World pawns and other reference kinds follow in SYNC-002.</summary>
    internal static class GameReferenceCodec
    {
        internal static void WriteThing(PayloadWriter writer, int mapId, int thingId)
        {
            writer.WriteUInt32(unchecked((uint)mapId));
            writer.WriteUInt32(unchecked((uint)thingId));
        }

        internal static (int MapId, int ThingId) ReadThing(PayloadReader reader) =>
            (unchecked((int)reader.ReadUInt32()), unchecked((int)reader.ReadUInt32()));

        internal static Thing? ResolveThing(int mapId, int thingId)
        {
            Map? map = Find.Maps.FirstOrDefault(m => m.uniqueID == mapId);
            return map?.listerThings.AllThings.FirstOrDefault(t => t.thingIDNumber == thingId);
        }
    }
}
