using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Multiplayer.Foundation.Api;
using Multiplayer.Foundation.Protocol;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>One registered sync method (API-001, ADR 0029).</summary>
    internal sealed class SyncMethodEntry
    {
        internal SyncMethodEntry(ulong id, MethodInfo method, string name)
        {
            Id = id; Method = method; Name = name;
            ParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
        }
        internal ulong Id { get; }
        internal MethodInfo Method { get; }
        /// <summary>Stable text the id is derived from: declaring type, name and parameter types.</summary>
        internal string Name { get; }
        internal Type[] ParameterTypes { get; }
        internal bool IsStatic => Method.IsStatic;
    }

    /// <summary>
    /// Registry of sync methods from other mods. A registered method gets a Harmony prefix: outside a command and outside a
    /// shared tick the call is encoded as game command opcode 4 (<see cref="GameCommandCodec.SyncMethod"/>) and sent to the
    /// coordinator; the original is skipped. At the scheduled tick every client decodes and invokes the method. Ids are a
    /// 64-bit FNV-1a hash of the signature, so identical mods on every client produce identical ids; the lobby's mod check
    /// guarantees identical mods.
    /// </summary>
    internal static class SyncMethods
    {
        private static readonly Dictionary<ulong, SyncMethodEntry> byId = new Dictionary<ulong, SyncMethodEntry>();
        private static readonly Dictionary<MethodBase, SyncMethodEntry> byMethod = new Dictionary<MethodBase, SyncMethodEntry>();
        private static readonly Harmony harmony = new Harmony("rimworldmp.foundation.api");

        internal static int Count => byId.Count;

        internal static SyncMethodEntry? EntryOf(MethodInfo method) => byMethod.TryGetValue(method, out SyncMethodEntry? entry) ? entry : null;

        internal static void Register(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (byMethod.ContainsKey(method)) return;
            if (method.DeclaringType == null || method.IsGenericMethodDefinition || method.ContainsGenericParameters || method.IsAbstract)
                throw new ArgumentException("Sync-Methoden dürfen nicht generisch oder abstrakt sein: " + Describe(method));
            if (!method.IsStatic && method.DeclaringType.IsValueType)
                throw new ArgumentException("Instanzmethoden von Strukturen werden nicht unterstützt: " + Describe(method));
            if (!method.IsStatic && !SyncSerializer.Supports(method.DeclaringType))
                throw new ArgumentException("Kein Serialisierer für den Empfänger " + method.DeclaringType.FullName + " von " + Describe(method) +
                    "; zuerst MP.RegisterSyncWorker.");
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                if (parameter.ParameterType.IsByRef || parameter.IsOut)
                    throw new ArgumentException("ref/out-Parameter werden nicht unterstützt: " + Describe(method));
                if (!SyncSerializer.Supports(parameter.ParameterType))
                    throw new ArgumentException("Kein Serialisierer für Parameter '" + parameter.Name + "' (" + parameter.ParameterType.FullName +
                        ") von " + Describe(method) + "; zuerst MP.RegisterSyncWorker.");
            }
            string name = Describe(method);
            ulong id = Fnv1a64(name);
            if (byId.TryGetValue(id, out SyncMethodEntry? other))
                throw new InvalidOperationException("Kennungskollision zwischen " + other.Name + " und " + name + ".");
            var entry = new SyncMethodEntry(id, method, name);
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(SyncMethods), nameof(Prefix)) { priority = Priority.First });
            byId[id] = entry;
            byMethod[method] = entry;
        }

        // Harmony injects the original method, its instance and all arguments. Returning false skips the original.
        private static bool Prefix(MethodBase __originalMethod, object? __instance, object?[] __args)
        {
            if (!byMethod.TryGetValue(__originalMethod, out SyncMethodEntry? entry)) return true;
            FoundationMod? mod = FoundationMod.Instance;
            return mod == null || !mod.CaptureSyncMethod(entry, __instance, __args);
        }

        /// <summary>Encodes a call as a game command; throws when a value cannot be serialized or the command is too large.</summary>
        internal static byte[] Encode(SyncMethodEntry entry, object? instance, object?[] args)
        {
            if (args.Length != entry.ParameterTypes.Length) throw new ArgumentException("Falsche Argumentzahl für " + entry.Name + ".");
            PayloadWriter writer = GameCommandCodec.Begin(GameCommandCodec.SyncMethod);
            writer.WriteUInt64(entry.Id);
            if (!entry.IsStatic)
            {
                if (instance == null) throw new InvalidOperationException("Instanzmethode ohne Empfänger: " + entry.Name);
                SyncSerializer.Write(writer, entry.Method.DeclaringType!, instance);
            }
            for (int i = 0; i < args.Length; i++) SyncSerializer.Write(writer, entry.ParameterTypes[i], args[i]);
            byte[] data = writer.ToArray();
            if (data.Length > LockstepPayloads.MaximumCommandBytes)
                throw new InvalidOperationException("Befehl " + entry.Name + " ist mit " + data.Length + " Bytes größer als " +
                    LockstepPayloads.MaximumCommandBytes + ".");
            return data;
        }

        /// <summary>Executor for opcode 4: decode, resolve, invoke. Missing references make the command a no-op everywhere. Permissions (ADR 0026) are the mod's business via MP.ExecutingIssuer.</summary>
        internal static void Execute(ScheduledCommand command, Guid localSession)
        {
            var reader = new PayloadReader(command.Data);
            reader.ReadByte(); reader.ReadByte();
            ulong id = reader.ReadUInt64();
            if (!byId.TryGetValue(id, out SyncMethodEntry? entry))
                throw new ProtocolException("Unbekannte synchronisierte Methode " + id.ToString("X16") + ".");
            object? instance = null;
            var args = new object?[entry.ParameterTypes.Length];
            int position = -1;
            try
            {
                if (!entry.IsStatic) instance = SyncSerializer.Read(reader, entry.Method.DeclaringType!);
                for (position = 0; position < args.Length; position++) args[position] = SyncSerializer.Read(reader, entry.ParameterTypes[position]);
                reader.EnsureComplete();
            }
            catch (MissingReferenceException missing)
            {
                Log.Warning("Multiplayer Foundation: " + entry.Name + " als No-op ausgeführt: " + missing.Message);
                return;
            }
            catch (Exception error) when (error is not ProtocolException)
            {
                throw new ProtocolException("Argument " + position + " von " + entry.Name + " nicht lesbar: " + error.GetType().Name + ": " + error.Message);
            }
            GameCommandCodec.Run(command, () =>
            {
                try { entry.Method.Invoke(instance, args); }
                catch (TargetInvocationException error)
                {
                    // The mod's own exception; every client saw the same one, so the game goes on.
                    Log.Error("Multiplayer Foundation: synchronisierte Methode " + entry.Name + " warf: " + (error.InnerException ?? error));
                }
            });
        }

        internal static string Describe(MethodInfo method)
        {
            var text = new StringBuilder();
            text.Append(method.DeclaringType?.FullName).Append("::").Append(method.Name).Append('(');
            text.Append(string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName)));
            return text.Append(')').ToString();
        }

        internal static ulong Fnv1a64(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(text)) unchecked { hash = (hash ^ b) * 1099511628211UL; }
            return hash;
        }
    }

    /// <summary>Mod state that joins the 60-tick state hash (plan §7.9), in id order so every client adds it alike.</summary>
    internal static class SyncHashes
    {
        private static readonly SortedDictionary<string, Func<ulong>> contributors = new SortedDictionary<string, Func<ulong>>(StringComparer.Ordinal);
        private static readonly HashSet<string> reported = new HashSet<string>();

        internal static int Count => contributors.Count;

        internal static void Register(string id, Func<ulong> contributor)
        {
            if (contributors.TryGetValue(id, out Func<ulong>? existing))
            {
                if (existing == contributor || existing.Method == contributor.Method && ReferenceEquals(existing.Target, contributor.Target)) return;
                throw new InvalidOperationException("Hash-Beitrag '" + id + "' ist bereits mit einer anderen Methode registriert.");
            }
            contributors[id] = contributor;
        }

        internal static void Contribute(Action<ulong> add)
        {
            add((ulong)contributors.Count);
            foreach (KeyValuePair<string, Func<ulong>> entry in contributors)
            {
                add(SyncMethods.Fnv1a64(entry.Key));
                ulong value;
                try { value = entry.Value(); }
                catch (Exception error)
                {
                    value = 0;
                    if (reported.Add(entry.Key)) Log.Error("Multiplayer Foundation: Hash-Beitrag '" + entry.Key + "' warf: " + error);
                }
                add(value);
            }
        }
    }

    /// <summary>The Foundation's implementation behind <see cref="MP"/>.</summary>
    internal sealed class FoundationSyncProvider : ISyncProvider
    {
        public bool IsInSession => FoundationMod.Instance?.InSharedGame ?? false;
        public bool IsHost => FoundationMod.Instance?.IsLobbyHost ?? false;
        public bool IsExecutingCommand => GameCommandCodec.Executing;
        public string? LocalPlayerKey => FoundationMod.Instance?.LocalPlayerKeyText;
        public string? ExecutingIssuer => GameCommandCodec.Current?.IssuerKey?.ToString();
        public void RegisterSyncMethod(MethodInfo method) => SyncMethods.Register(method);
        public void RegisterSyncWorker(Type type, Delegate worker) => SyncSerializer.RegisterWorker(type, worker);
        public void RegisterHashContributor(string id, Func<ulong> contributor) => SyncHashes.Register(id, contributor);
    }
}
