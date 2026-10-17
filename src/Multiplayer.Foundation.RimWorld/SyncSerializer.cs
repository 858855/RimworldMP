using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Api;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// Serializer registry for sync-method arguments (API-001, ADR 0029; plan §7.3). Every value is written by the issuing
    /// client and read by every client at the same tick, so references are resolved against identical state. A reference to
    /// something that no longer exists raises <see cref="MissingReferenceException"/>; the command then becomes a no-op
    /// everywhere. Other mods add their own types through <see cref="MP.RegisterSyncWorker{T}"/>.
    /// </summary>
    internal static class SyncSerializer
    {
        private const int MaxString = ushort.MaxValue;
        private const int MaxElements = 65535;
        private const byte ThingNone = 0, ThingSpawned = 1, ThingWorldPawn = 2, ThingHeld = 3;

        private static readonly Dictionary<Type, Delegate> workers = new Dictionary<Type, Delegate>();

        internal static int WorkerCount => workers.Count;

        internal static void RegisterWorker(Type type, Delegate worker)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (worker == null) throw new ArgumentNullException(nameof(worker));
            Type expected = typeof(SyncWorkerDelegate<>).MakeGenericType(type);
            if (!expected.IsInstanceOfType(worker))
                throw new ArgumentException("Der Worker für " + type.FullName + " muss ein SyncWorkerDelegate<" + type.Name + "> sein.");
            if (workers.TryGetValue(type, out Delegate? existing))
            {
                if (ReferenceEquals(existing, worker) || existing.Method == worker.Method) return;
                throw new InvalidOperationException("Für " + type.FullName + " ist bereits ein anderer Worker registriert.");
            }
            workers[type] = worker;
        }

        /// <summary>True when a value of this static type can be written and read.</summary>
        internal static bool Supports(Type type)
        {
            if (type.IsByRef || type.IsPointer) return false;
            if (WorkerFor(type) != null) return true;
            Type? nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null) return Supports(nullable);
            if (type.IsEnum) return true;
            if (type.IsArray) return type.GetArrayRank() == 1 && Supports(type.GetElementType()!);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return Supports(type.GetGenericArguments()[0]);
            if (type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
                type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
                type == typeof(float) || type == typeof(double) || type == typeof(string) || type == typeof(char) ||
                type == typeof(IntVec3) || type == typeof(IntVec2) || type == typeof(Rot4) || type == typeof(CellRect) ||
                type == typeof(LocalTargetInfo)) return true;
            return typeof(Def).IsAssignableFrom(type) || typeof(Thing).IsAssignableFrom(type) || typeof(ThingComp).IsAssignableFrom(type) ||
                type == typeof(Map) || type == typeof(Faction) || typeof(Zone).IsAssignableFrom(type) ||
                typeof(Area).IsAssignableFrom(type) || typeof(Bill).IsAssignableFrom(type);
        }

        internal static void Write(PayloadWriter writer, Type type, object? value)
        {
            Delegate? worker = WorkerFor(type);
            if (worker != null)
            {
                if (!type.IsValueType) { writer.WriteBool(value != null); if (value == null) return; }
                RunWorker(worker, new PayloadSyncWorker(writer), ref value);
                return;
            }
            Type? nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null)
            {
                writer.WriteBool(value != null);
                if (value != null) Write(writer, nullable, value);
                return;
            }
            if (type.IsEnum) { writer.WriteUInt64(unchecked((ulong)Convert.ToInt64(value))); return; }
            if (type == typeof(bool)) { writer.WriteBool((bool)value!); return; }
            if (type == typeof(byte)) { writer.WriteByte((byte)value!); return; }
            if (type == typeof(sbyte)) { writer.WriteByte(unchecked((byte)(sbyte)value!)); return; }
            if (type == typeof(short)) { writer.WriteUInt16(unchecked((ushort)(short)value!)); return; }
            if (type == typeof(ushort)) { writer.WriteUInt16((ushort)value!); return; }
            if (type == typeof(char)) { writer.WriteUInt16((char)value!); return; }
            if (type == typeof(int)) { WriteInt(writer, (int)value!); return; }
            if (type == typeof(uint)) { writer.WriteUInt32((uint)value!); return; }
            if (type == typeof(long)) { writer.WriteUInt64(unchecked((ulong)(long)value!)); return; }
            if (type == typeof(ulong)) { writer.WriteUInt64((ulong)value!); return; }
            if (type == typeof(float)) { WriteInt(writer, BitConverter.SingleToInt32Bits((float)value!)); return; }
            if (type == typeof(double)) { writer.WriteUInt64(unchecked((ulong)BitConverter.DoubleToInt64Bits((double)value!))); return; }
            if (type == typeof(string))
            {
                writer.WriteBool(value != null);
                if (value != null) writer.WriteString((string)value, MaxString);
                return;
            }
            if (type == typeof(IntVec3)) { var v = (IntVec3)value!; WriteInt(writer, v.x); WriteInt(writer, v.y); WriteInt(writer, v.z); return; }
            if (type == typeof(IntVec2)) { var v = (IntVec2)value!; WriteInt(writer, v.x); WriteInt(writer, v.z); return; }
            if (type == typeof(Rot4)) { writer.WriteByte(((Rot4)value!).AsByte); return; }
            if (type == typeof(CellRect))
            {
                var r = (CellRect)value!;
                WriteInt(writer, r.minX); WriteInt(writer, r.minZ); WriteInt(writer, r.Width); WriteInt(writer, r.Height);
                return;
            }
            if (type == typeof(LocalTargetInfo))
            {
                var target = (LocalTargetInfo)value!;
                writer.WriteByte(!target.IsValid ? (byte)0 : target.HasThing ? (byte)1 : (byte)2);
                if (!target.IsValid) return;
                if (target.HasThing) WriteThing(writer, target.Thing);
                else Write(writer, typeof(IntVec3), target.Cell);
                return;
            }
            if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type element = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
                writer.WriteBool(value != null);
                if (value == null) return;
                var items = ((IEnumerable)value).Cast<object?>().ToList();
                if (items.Count > MaxElements) throw new InvalidOperationException("Liste mit mehr als " + MaxElements + " Einträgen.");
                writer.WriteUInt32((uint)items.Count);
                foreach (object? item in items) Write(writer, element, item);
                return;
            }
            if (!type.IsValueType) { writer.WriteBool(value != null); if (value == null) return; }
            if (typeof(Def).IsAssignableFrom(type))
            {
                var def = (Def)value!;
                bool exact = def.GetType() == type;
                writer.WriteBool(exact);
                if (!exact) writer.WriteString(def.GetType().FullName ?? def.GetType().Name, 512);
                writer.WriteString(def.defName, 512);
                return;
            }
            if (typeof(Thing).IsAssignableFrom(type)) { WriteThing(writer, (Thing)value!); return; }
            if (typeof(ThingComp).IsAssignableFrom(type))
            {
                var comp = (ThingComp)value!;
                if (comp.parent == null) throw new InvalidOperationException("ThingComp ohne Träger.");
                WriteThing(writer, comp.parent);
                int index = comp.parent.AllComps.IndexOf(comp);
                if (index < 0) throw new InvalidOperationException("ThingComp gehört nicht zu seinem Träger.");
                writer.WriteUInt32((uint)index);
                return;
            }
            if (type == typeof(Map)) { WriteInt(writer, ((Map)value!).uniqueID); return; }
            if (type == typeof(Faction)) { WriteInt(writer, ((Faction)value!).loadID); return; }
            if (typeof(Zone).IsAssignableFrom(type))
            {
                var zone = (Zone)value!;
                if (zone.Map == null) throw new InvalidOperationException("Zone ohne Karte.");
                WriteInt(writer, zone.Map.uniqueID); WriteInt(writer, zone.ID);
                return;
            }
            if (typeof(Area).IsAssignableFrom(type))
            {
                var area = (Area)value!;
                if (area.Map == null) throw new InvalidOperationException("Bereich ohne Karte.");
                WriteInt(writer, area.Map.uniqueID); WriteInt(writer, area.ID);
                return;
            }
            if (typeof(Bill).IsAssignableFrom(type))
            {
                var bill = (Bill)value!;
                if (bill.billStack?.billGiver is not Thing giver) throw new InvalidOperationException("Auftrag ohne Werkbank.");
                WriteThing(writer, giver);
                writer.WriteString(bill.GetUniqueLoadID(), 256);
                return;
            }
            throw new InvalidOperationException("Kein Serialisierer für " + type.FullName + "; MP.RegisterSyncWorker verwenden.");
        }

        internal static object? Read(PayloadReader reader, Type type)
        {
            Delegate? worker = WorkerFor(type);
            if (worker != null)
            {
                if (!type.IsValueType && !reader.ReadBool()) return null;
                object? value = type.IsValueType ? Activator.CreateInstance(type) : null;
                RunWorker(worker, new PayloadSyncWorker(reader), ref value);
                return value;
            }
            Type? nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null) return reader.ReadBool() ? Read(reader, nullable) : null;
            if (type.IsEnum)
            {
                long raw = unchecked((long)reader.ReadUInt64());
                object boxed = Enum.ToObject(type, raw);
                if (!type.IsDefined(typeof(FlagsAttribute), false) && !Enum.IsDefined(type, boxed))
                    throw new ProtocolException("Ungültiger Wert für " + type.Name + ".");
                return boxed;
            }
            if (type == typeof(bool)) return reader.ReadBool();
            if (type == typeof(byte)) return reader.ReadByte();
            if (type == typeof(sbyte)) return unchecked((sbyte)reader.ReadByte());
            if (type == typeof(short)) return unchecked((short)reader.ReadUInt16());
            if (type == typeof(ushort)) return reader.ReadUInt16();
            if (type == typeof(char)) return (char)reader.ReadUInt16();
            if (type == typeof(int)) return ReadInt(reader);
            if (type == typeof(uint)) return reader.ReadUInt32();
            if (type == typeof(long)) return unchecked((long)reader.ReadUInt64());
            if (type == typeof(ulong)) return reader.ReadUInt64();
            if (type == typeof(float)) return BitConverter.Int32BitsToSingle(ReadInt(reader));
            if (type == typeof(double)) return BitConverter.Int64BitsToDouble(unchecked((long)reader.ReadUInt64()));
            if (type == typeof(string)) return reader.ReadBool() ? reader.ReadString(MaxString) : null;
            if (type == typeof(IntVec3)) return new IntVec3(ReadInt(reader), ReadInt(reader), ReadInt(reader));
            if (type == typeof(IntVec2)) return new IntVec2(ReadInt(reader), ReadInt(reader));
            if (type == typeof(Rot4))
            {
                byte raw = reader.ReadByte();
                if (raw > 3) throw new ProtocolException("Ungültige Rot4.");
                return new Rot4(raw);
            }
            if (type == typeof(CellRect)) return new CellRect(ReadInt(reader), ReadInt(reader), ReadInt(reader), ReadInt(reader));
            if (type == typeof(LocalTargetInfo))
            {
                byte kind = reader.ReadByte();
                if (kind == 0) return LocalTargetInfo.Invalid;
                if (kind == 1) return new LocalTargetInfo(ReadThing(reader));
                if (kind == 2) return new LocalTargetInfo((IntVec3)Read(reader, typeof(IntVec3))!);
                throw new ProtocolException("Ungültiges Ziel.");
            }
            if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type element = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
                if (!reader.ReadBool()) return null;
                uint count = reader.ReadUInt32();
                if (count > MaxElements) throw new ProtocolException("Liste zu lang.");
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;
                for (uint i = 0; i < count; i++) list.Add(Read(reader, element));
                if (!type.IsArray) return list;
                Array array = Array.CreateInstance(element, list.Count);
                list.CopyTo(array, 0);
                return array;
            }
            if (!type.IsValueType && !reader.ReadBool()) return null;
            if (typeof(Def).IsAssignableFrom(type))
            {
                Type defType = type;
                if (!reader.ReadBool())
                {
                    string typeName = reader.ReadString(512);
                    defType = GenDefDatabase.AllDefTypesWithDatabases().FirstOrDefault(t => t.FullName == typeName) ??
                        throw new ProtocolException("Unbekannter Def-Typ " + typeName + ".");
                    if (!type.IsAssignableFrom(defType)) throw new ProtocolException("Def-Typ " + typeName + " passt nicht zu " + type.Name + ".");
                }
                string defName = reader.ReadString(512);
                return GenDefDatabase.GetDefSilentFail(defType, defName, false) ??
                    throw new ProtocolException("Unbekannte Def " + defName + " (" + defType.Name + ").");
            }
            if (typeof(Thing).IsAssignableFrom(type))
            {
                Thing thing = ReadThing(reader);
                if (!type.IsInstanceOfType(thing)) throw new MissingReferenceException("Thing " + thing.thingIDNumber + " ist kein " + type.Name + ".");
                return thing;
            }
            if (typeof(ThingComp).IsAssignableFrom(type))
            {
                Thing parent = ReadThing(reader);
                uint index = reader.ReadUInt32();
                var comps = (parent as ThingWithComps)?.AllComps;
                if (comps == null || index >= comps.Count || !type.IsInstanceOfType(comps[(int)index]))
                    throw new MissingReferenceException("ThingComp " + index + " an " + parent.thingIDNumber + " fehlt.");
                return comps[(int)index];
            }
            if (type == typeof(Map)) return ReadMap(reader);
            if (type == typeof(Faction))
            {
                int loadId = ReadInt(reader);
                return Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.loadID == loadId) ??
                    throw new MissingReferenceException("Fraktion " + loadId + " fehlt.");
            }
            if (typeof(Zone).IsAssignableFrom(type))
            {
                Map map = ReadMap(reader);
                int id = ReadInt(reader);
                Zone? zone = map.zoneManager.AllZones.FirstOrDefault(z => z.ID == id);
                if (zone == null || !type.IsInstanceOfType(zone)) throw new MissingReferenceException("Zone " + id + " fehlt.");
                return zone;
            }
            if (typeof(Area).IsAssignableFrom(type))
            {
                Map map = ReadMap(reader);
                int id = ReadInt(reader);
                Area? area = map.areaManager.AllAreas.FirstOrDefault(a => a.ID == id);
                if (area == null || !type.IsInstanceOfType(area)) throw new MissingReferenceException("Bereich " + id + " fehlt.");
                return area;
            }
            if (typeof(Bill).IsAssignableFrom(type))
            {
                Thing giver = ReadThing(reader);
                string loadId = reader.ReadString(256);
                Bill? bill = (giver as IBillGiver)?.BillStack?.Bills.FirstOrDefault(b => b.GetUniqueLoadID() == loadId);
                if (bill == null || !type.IsInstanceOfType(bill)) throw new MissingReferenceException("Auftrag " + loadId + " fehlt.");
                return bill;
            }
            throw new ProtocolException("Kein Serialisierer für " + type.FullName + ".");
        }

        // The map id without a null bit; Zone and Area write it the same way.
        private static Map ReadMap(PayloadReader reader)
        {
            int mapId = ReadInt(reader);
            return Find.Maps.FirstOrDefault(m => m.uniqueID == mapId) ?? throw new MissingReferenceException("Karte " + mapId + " fehlt.");
        }

        private static void WriteThing(PayloadWriter writer, Thing thing)
        {
            if (thing.Spawned && thing.Map != null)
            {
                writer.WriteByte(ThingSpawned);
                WriteInt(writer, thing.Map.uniqueID); WriteInt(writer, thing.thingIDNumber);
                return;
            }
            if (thing is Pawn pawn && pawn.IsWorldPawn())
            {
                writer.WriteByte(ThingWorldPawn);
                WriteInt(writer, thing.thingIDNumber);
                return;
            }
            Thing holder = thing.SpawnedParentOrMe;
            if (holder != null && !ReferenceEquals(holder, thing) && holder.Spawned && holder.Map != null)
            {
                writer.WriteByte(ThingHeld);
                WriteInt(writer, holder.Map.uniqueID); WriteInt(writer, holder.thingIDNumber); WriteInt(writer, thing.thingIDNumber);
                return;
            }
            throw new InvalidOperationException("Thing " + thing.ThingID + " ist weder platziert, Weltfigur noch in einem platzierten Behälter.");
        }

        private static Thing ReadThing(PayloadReader reader)
        {
            byte kind = reader.ReadByte();
            if (kind == ThingSpawned)
            {
                int mapId = ReadInt(reader), thingId = ReadInt(reader);
                return GameReferenceCodec.ResolveThing(mapId, thingId) ??
                    throw new MissingReferenceException("Thing " + thingId + " auf Karte " + mapId + " fehlt.");
            }
            if (kind == ThingWorldPawn)
            {
                int thingId = ReadInt(reader);
                return Find.WorldPawns.AllPawnsAliveOrDead.FirstOrDefault(p => p.thingIDNumber == thingId) ??
                    throw new MissingReferenceException("Weltfigur " + thingId + " fehlt.");
            }
            if (kind == ThingHeld)
            {
                int mapId = ReadInt(reader), holderId = ReadInt(reader), thingId = ReadInt(reader);
                Thing? holder = GameReferenceCodec.ResolveThing(mapId, holderId);
                Thing? held = holder is IThingHolder owner
                    ? ThingOwnerUtility.GetAllThingsRecursively(owner, true).FirstOrDefault(t => t.thingIDNumber == thingId) : null;
                return held ?? throw new MissingReferenceException("Thing " + thingId + " in " + holderId + " fehlt.");
            }
            throw new ProtocolException("Ungültige Thing-Referenz.");
        }

        private static Delegate? WorkerFor(Type type)
        {
            for (Type? t = type; t != null; t = t.BaseType)
                if (workers.TryGetValue(t, out Delegate? worker)) return worker;
            return null;
        }

        private static void RunWorker(Delegate worker, SyncWorker sync, ref object? value)
        {
            object?[] arguments = { sync, value };
            try { worker.DynamicInvoke(arguments); }
            catch (System.Reflection.TargetInvocationException error) when (error.InnerException != null)
            { throw error.InnerException; }
            value = arguments[1];
        }

        private static void WriteInt(PayloadWriter writer, int value) => writer.WriteUInt32(unchecked((uint)value));
        private static int ReadInt(PayloadReader reader) => unchecked((int)reader.ReadUInt32());

        /// <summary>The <see cref="SyncWorker"/> handed to mod workers; one side per instance.</summary>
        private sealed class PayloadSyncWorker : SyncWorker
        {
            private readonly PayloadWriter? writer;
            private readonly PayloadReader? reader;
            internal PayloadSyncWorker(PayloadWriter writer) { this.writer = writer; }
            internal PayloadSyncWorker(PayloadReader reader) { this.reader = reader; }
            public override bool IsWriting => writer != null;
            public override void Write<T>(T value)
            {
                if (writer == null) throw new InvalidOperationException("Dieser SyncWorker liest.");
                SyncSerializer.Write(writer, typeof(T), value);
            }
            public override T Read<T>()
            {
                if (reader == null) throw new InvalidOperationException("Dieser SyncWorker schreibt.");
                return (T)SyncSerializer.Read(reader, typeof(T))!;
            }
        }
    }
}
