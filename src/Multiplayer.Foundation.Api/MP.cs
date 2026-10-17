using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Multiplayer.Foundation.Api
{
    /// <summary>Writes or reads one value of <typeparamref name="T"/>; the same code runs on both sides (see <see cref="SyncWorker.IsWriting"/>).</summary>
    public delegate void SyncWorkerDelegate<T>(SyncWorker sync, ref T value);

    /// <summary>
    /// Public entry point of the RimWorld Multiplayer Foundation for other mods (API-001, ADR 0029).
    /// Register in a <c>[StaticConstructorOnStartup]</c> type or your <c>Mod</c> constructor and load after
    /// <c>rimworldmp.foundation</c>. Everything registered must be registered identically on every client; the lobby's mod
    /// check makes sure the same mods and versions are active.
    /// </summary>
    public static class MP
    {
        /// <summary>Semantic version of this contract. Additive changes raise the minor part; removals the major part.</summary>
        public const string ApiVersion = "1.0.0";

        private static ISyncProvider? provider;

        /// <summary>True once the Foundation mod has installed its implementation.</summary>
        public static bool IsAvailable => provider != null;

        /// <summary>True while the current game is a shared multiplayer game (from the shared start on).</summary>
        public static bool IsInSession => provider?.IsInSession ?? false;

        /// <summary>True when this client hosts the lobby of the current session.</summary>
        public static bool IsHost => provider?.IsHost ?? false;

        /// <summary>True while a scheduled command is being executed on this client; mutate game state only then or in a tick.</summary>
        public static bool IsExecutingCommand => provider?.IsExecutingCommand ?? false;

        /// <summary>The local player's persistent key, or null outside a lobby.</summary>
        public static string? LocalPlayerKey => provider?.LocalPlayerKey;

        /// <summary>While a command executes: the key of the player who issued it; null for coordinator commands or outside execution.</summary>
        public static string? ExecutingIssuer => provider?.ExecutingIssuer;

        /// <summary>Called once by the Foundation mod. Other callers get an exception.</summary>
        public static void Install(ISyncProvider implementation)
        {
            if (implementation == null) throw new ArgumentNullException(nameof(implementation));
            if (provider != null && !ReferenceEquals(provider, implementation))
                throw new InvalidOperationException("Multiplayer Foundation API: eine Implementierung ist bereits installiert.");
            provider = implementation;
        }

        /// <summary>Registers every <see cref="SyncWorkerAttribute"/>, <see cref="SyncHashAttribute"/> and <see cref="SyncMethodAttribute"/> of the calling assembly, workers first.</summary>
        public static void RegisterAll() => RegisterAll(Assembly.GetCallingAssembly());

        /// <summary>Registers every attributed member of <paramref name="assembly"/>: workers first, then hash contributors, then methods.</summary>
        public static void RegisterAll(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            ISyncProvider impl = Require();
            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            List<MethodInfo> methods = LoadableTypes(assembly).SelectMany(t => t.GetMethods(All)).ToList();
            foreach (MethodInfo method in methods.Where(m => m.IsDefined(typeof(SyncWorkerAttribute), false)))
                RegisterSyncWorker(method);
            foreach (MethodInfo method in methods.Where(m => m.IsDefined(typeof(SyncHashAttribute), false)))
            {
                if (!method.IsStatic || method.ReturnType != typeof(ulong) || method.GetParameters().Length != 0)
                    throw new ArgumentException("[SyncHash] erwartet 'static ulong Methode()': " + Describe(method));
                impl.RegisterHashContributor(method.DeclaringType!.FullName + "." + method.Name,
                    (Func<ulong>)method.CreateDelegate(typeof(Func<ulong>)));
            }
            foreach (MethodInfo method in methods.Where(m => m.IsDefined(typeof(SyncMethodAttribute), false)))
                impl.RegisterSyncMethod(method);
        }

        /// <summary>
        /// A player action. When a player calls the method in a shared game, the call is sent to the coordinator instead
        /// and executed at the same tick on every client, including the caller's; while a command executes, or inside a
        /// shared tick, the method runs directly. The instance and every argument must be of a supported type
        /// (see docs/extension-api.md) or have a registered worker. Return values are not transported.
        /// </summary>
        public static void RegisterSyncMethod(MethodInfo method) => Require().RegisterSyncMethod(method ?? throw new ArgumentNullException(nameof(method)));

        /// <summary>Convenience for <see cref="RegisterSyncMethod(MethodInfo)"/> by type and name; the name must be unambiguous.</summary>
        public static void RegisterSyncMethod(Type type, string methodName)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            MethodInfo? method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (method == null) throw new ArgumentException("Methode nicht gefunden oder mehrdeutig: " + type.FullName + "." + methodName);
            RegisterSyncMethod(method);
        }

        /// <summary>Serializer for your own type: the delegate writes on the sender and reads on every client.</summary>
        public static void RegisterSyncWorker<T>(SyncWorkerDelegate<T> worker) =>
            Require().RegisterSyncWorker(typeof(T), worker ?? throw new ArgumentNullException(nameof(worker)));

        /// <summary>Registers a <c>static void Work(SyncWorker sync, ref T value)</c> method as the worker for T.</summary>
        public static void RegisterSyncWorker(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            ParameterInfo[] parameters = method.GetParameters();
            if (!method.IsStatic || method.ReturnType != typeof(void) || parameters.Length != 2 ||
                parameters[0].ParameterType != typeof(SyncWorker) || !parameters[1].ParameterType.IsByRef)
                throw new ArgumentException("[SyncWorker] erwartet 'static void Methode(SyncWorker sync, ref T wert)': " + Describe(method));
            Type valueType = parameters[1].ParameterType.GetElementType()!;
            Delegate worker = method.CreateDelegate(typeof(SyncWorkerDelegate<>).MakeGenericType(valueType));
            Require().RegisterSyncWorker(valueType, worker);
        }

        /// <summary>
        /// Adds your mod's gameplay state to the 60-tick state hash so a divergence in it is detected. The id must be unique
        /// (use your package id) and the value must depend only on synchronized state, never on rendering or local settings.
        /// </summary>
        public static void RegisterHashContributor(string id, Func<ulong> contributor)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Eine Kennung ist erforderlich.", nameof(id));
            Require().RegisterHashContributor(id, contributor ?? throw new ArgumentNullException(nameof(contributor)));
        }

        private static ISyncProvider Require() => provider ??
            throw new InvalidOperationException("Multiplayer Foundation API: der Foundation-Mod ist nicht geladen (loadAfter rimworldmp.foundation setzen).");

        private static IEnumerable<Type> LoadableTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException error) { return error.Types.Where(t => t != null)!; }
        }

        private static string Describe(MethodInfo method) => method.DeclaringType?.FullName + "." + method.Name;
    }

    /// <summary>Implemented by the Foundation mod; not for other mods.</summary>
    public interface ISyncProvider
    {
        bool IsInSession { get; }
        bool IsHost { get; }
        bool IsExecutingCommand { get; }
        string? LocalPlayerKey { get; }
        string? ExecutingIssuer { get; }
        void RegisterSyncMethod(MethodInfo method);
        /// <param name="type">The value type T.</param>
        /// <param name="worker">A <see cref="SyncWorkerDelegate{T}"/> for that T.</param>
        void RegisterSyncWorker(Type type, Delegate worker);
        void RegisterHashContributor(string id, Func<ulong> contributor);
    }

    /// <summary>Marks a player action; see <see cref="MP.RegisterSyncMethod(MethodInfo)"/>.</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class SyncMethodAttribute : Attribute { }

    /// <summary>Marks <c>static void Work(SyncWorker sync, ref T value)</c>; see <see cref="MP.RegisterSyncWorker(MethodInfo)"/>.</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class SyncWorkerAttribute : Attribute { }

    /// <summary>Marks <c>static ulong Method()</c> whose value joins the state hash; see <see cref="MP.RegisterHashContributor"/>.</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class SyncHashAttribute : Attribute { }

    /// <summary>
    /// Writer on the issuing client, reader on every client. Supported types: primitives, strings, enums, Unity/Verse
    /// value types (IntVec3, IntVec2, Rot4, CellRect), Def subclasses, Map, Thing and Pawn (spawned or world pawns),
    /// ThingComp, Faction, Zone, Area, Bill, LocalTargetInfo, nullable and List/array of these, and types with a worker.
    /// </summary>
    public abstract class SyncWorker
    {
        public abstract bool IsWriting { get; }
        public abstract void Write<T>(T value);
        public abstract T Read<T>();

        /// <summary>Writes or reads <paramref name="value"/> depending on the side.</summary>
        public void Bind<T>(ref T value)
        {
            if (IsWriting) Write(value);
            else value = Read<T>();
        }
    }

    /// <summary>Thrown by the implementation when a referenced game object no longer exists; the whole command then becomes a no-op on every client.</summary>
    public sealed class MissingReferenceException : Exception
    {
        public MissingReferenceException(string message) : base(message) { }
    }
}
