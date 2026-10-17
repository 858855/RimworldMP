using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Api;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// API-001 part of <c>-mpWorldSelfTest</c>: registers this assembly's attributed test members through the public API,
    /// encodes a call like the capture prefix does, executes it like the tick driver does and checks the values, the
    /// execution flags, the no-op for a missing reference, the rejection of an unknown method, the size cap and the hash
    /// contribution. Runs in the generated game (stage 2) without a network.
    /// </summary>
    internal static class ApiSelfTest
    {
        internal sealed class Target
        {
            public Pawn? Pawn;
            public int Count;
        }

        private static string? recorded;
        private static bool executingSeen;
        private static string? issuerSeen;
        private static ulong hashValue = 7;

        [SyncWorker]
        internal static void WorkTarget(SyncWorker sync, ref Target target)
        {
            if (sync.IsWriting)
            {
                sync.Write(target.Pawn);
                sync.Write(target.Count);
            }
            else target = new Target { Pawn = sync.Read<Pawn?>(), Count = sync.Read<int>() };
        }

        [SyncHash]
        internal static ulong Hash() => hashValue;

        [SyncMethod]
        internal static void Record(Pawn pawn, ThingDef def, IntVec3 cell, string text, int number, float ratio, bool flag,
            List<int> list, Faction faction, Map map, Area? area, Target target, LocalTargetInfo aim, Rot4 rotation, Def anyDef)
        {
            executingSeen = MP.IsExecutingCommand;
            issuerSeen = MP.ExecutingIssuer;
            recorded = Summary(pawn, def, cell, text, number, ratio, flag, list, faction, map, area, target, aim, rotation, anyDef);
        }

        private static string Summary(Pawn pawn, ThingDef def, IntVec3 cell, string text, int number, float ratio, bool flag,
            List<int> list, Faction faction, Map map, Area? area, Target target, LocalTargetInfo aim, Rot4 rotation, Def anyDef) =>
            pawn.thingIDNumber + "|" + def.defName + "|" + cell + "|" + text + "|" + number + "|" + ratio.ToString("R") + "|" + flag + "|" +
            string.Join(",", list) + "|" + faction.loadID + "|" + map.uniqueID + "|" + (area?.ID.ToString() ?? "-") + "|" +
            (target.Pawn?.thingIDNumber.ToString() ?? "-") + ":" + target.Count + "|" + (aim.HasThing ? aim.Thing.thingIDNumber.ToString() : aim.Cell.ToString()) +
            "|" + rotation.AsByte + "|" + anyDef.GetType().Name + ":" + anyDef.defName;

        internal static void Run(Action<string, bool> check)
        {
            check("apiAvailable", MP.IsAvailable);
            try { MP.RegisterAll(typeof(ApiSelfTest).Assembly); }
            catch (Exception error) { Log.Error("Multiplayer Foundation API self-test: register " + error); check("apiRegister", false); return; }
            var method = typeof(ApiSelfTest).GetMethod(nameof(Record), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            SyncMethodEntry? entry = SyncMethods.EntryOf(method);
            check("apiRegister", entry != null && SyncHashes.Count >= 1 && SyncSerializer.WorkerCount >= 1);
            if (entry == null) return;

            Map map = Find.Maps[0];
            Pawn pawn = GameFingerprint.Colonists()[0];
            Area area = map.areaManager.Home;
            var target = new Target { Pawn = pawn, Count = 42 };
            object?[] args =
            {
                pawn, ThingDefOf.Steel, new IntVec3(3, 0, 7), "Grüße ☃", -12345, 0.75f, true, new List<int> { 1, 2, 3 },
                Faction.OfPlayer, map, area, target, new LocalTargetInfo(pawn), Rot4.East, (Def)JobDefOf.Goto,
            };
            string expected = Summary(pawn, ThingDefOf.Steel, new IntVec3(3, 0, 7), "Grüße ☃", -12345, 0.75f, true, new List<int> { 1, 2, 3 },
                Faction.OfPlayer, map, area, target, new LocalTargetInfo(pawn), Rot4.East, JobDefOf.Goto);

            // Every argument type on its own, so a failing serializer names itself.
            string[] names = { "pawn", "thingDef", "cell", "text", "int", "float", "bool", "list", "faction", "map", "area", "worker", "target", "rot", "anyDef" };
            for (int i = 0; i < args.Length; i++)
            {
                Type type = entry.ParameterTypes[i];
                try
                {
                    var writer = new PayloadWriter();
                    SyncSerializer.Write(writer, type, args[i]);
                    var reader = new PayloadReader(writer.ToArray());
                    object? back = SyncSerializer.Read(reader, type);
                    reader.EnsureComplete();
                    bool same = args[i] is Target t ? back is Target b && b.Pawn == t.Pawn && b.Count == t.Count
                        : args[i] is List<int> l ? back is List<int> bl && bl.SequenceEqual(l)
                        : args[i] is LocalTargetInfo lt ? back is LocalTargetInfo blt && blt == lt
                        : Equals(args[i], back);
                    check("apiType." + names[i], same);
                    if (!same) Log.Error("Multiplayer Foundation API self-test: " + names[i] + " came back as " + (back ?? "null"));
                }
                catch (Exception error) { check("apiType." + names[i], false); Log.Error("Multiplayer Foundation API self-test: " + names[i] + " " + error); }
            }

            // Outside a session the patched method runs directly (prefix lets it through).
            recorded = null; executingSeen = true;
            Record(pawn, ThingDefOf.Steel, new IntVec3(3, 0, 7), "Grüße ☃", -12345, 0.75f, true, new List<int> { 1, 2, 3 },
                Faction.OfPlayer, map, area, target, new LocalTargetInfo(pawn), Rot4.East, JobDefOf.Goto);
            check("apiLocalRuns", recorded == expected && !executingSeen);

            // Encode like the capture, execute like the tick driver.
            PlayerKey issuer = PlayerKey.Local(Guid.NewGuid());
            byte[] data;
            try { data = SyncMethods.Encode(entry, null, args); }
            catch (Exception error) { Log.Error("Multiplayer Foundation API self-test: encode " + error); check("apiEncode", false); return; }
            check("apiEncode", data.Length > 10 && data[1] == GameCommandCodec.SyncMethod);
            recorded = null; executingSeen = false; issuerSeen = null;
            try { GameCommandCodec.Execute(new ScheduledCommand(0, 1, Guid.NewGuid(), issuer, ScheduledKind.Game, data), Guid.Empty); }
            catch (Exception error) { Log.Error("Multiplayer Foundation API self-test: execute " + error); }
            check("apiRoundtrip", recorded == expected);
            check("apiExecuting", executingSeen && issuerSeen == issuer.ToString() && !MP.IsExecutingCommand);
            Log.Message("Multiplayer Foundation API self-test: " + data.Length + " bytes; " + (recorded ?? "nothing recorded"));

            // A reference to a thing that does not exist: no-op without an exception, nothing invoked.
            PayloadWriter bogus = GameCommandCodec.Begin(GameCommandCodec.SyncMethod);
            bogus.WriteUInt64(entry.Id);
            bogus.WriteBool(true); bogus.WriteByte(1); bogus.WriteUInt32(0x7ffffff0); bogus.WriteUInt32(1);
            recorded = null;
            bool missingQuiet;
            try { GameCommandCodec.Execute(new ScheduledCommand(0, 2, Guid.NewGuid(), issuer, ScheduledKind.Game, bogus.ToArray()), Guid.Empty); missingQuiet = true; }
            catch (Exception) { missingQuiet = false; }
            check("apiMissingNoop", missingQuiet && recorded == null);

            // An unknown method id is a protocol error, like an unknown opcode.
            PayloadWriter unknown = GameCommandCodec.Begin(GameCommandCodec.SyncMethod);
            unknown.WriteUInt64(entry.Id ^ 0x5555);
            bool rejected;
            try { GameCommandCodec.Execute(new ScheduledCommand(0, 3, Guid.NewGuid(), issuer, ScheduledKind.Game, unknown.ToArray()), Guid.Empty); rejected = false; }
            catch (ProtocolException) { rejected = true; }
            check("apiUnknownRejected", rejected);

            // The 32 KiB command cap applies to mod commands too.
            object?[] big = (object?[])args.Clone();
            big[3] = new string('x', 40000);
            bool capped;
            try { SyncMethods.Encode(entry, null, big); capped = false; }
            catch (InvalidOperationException) { capped = true; }
            catch (ProtocolException) { capped = true; }
            check("apiSizeCap", capped);

            // The hash contributor changes the state hash.
            ulong before = SessionStateHash.Compute(Find.TickManager, 0, 0);
            hashValue++;
            ulong after = SessionStateHash.Compute(Find.TickManager, 0, 0);
            check("apiHash", before != after);
        }
    }
}
