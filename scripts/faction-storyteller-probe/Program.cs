using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

if (args.Length != 2 || !File.Exists(Path.Combine(args[0], "Assembly-CSharp.dll")) || !File.Exists(args[1]))
{
    Console.Error.WriteLine("Usage: dotnet run --project scripts/faction-storyteller-probe -- <RimWorld Managed directory> <built Multiplayer.Foundation.RimWorld.dll>");
    return 2;
}

string managed = Path.GetFullPath(args[0]);
string mod = Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
{
    string name = new AssemblyName(request.Name).Name + ".dll";
    foreach (string directory in new[] { mod, managed })
    {
        string candidate = Path.Combine(directory, name);
        if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
    }
    return null;
};

Assembly game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
Assembly adapter = Assembly.LoadFrom(Path.GetFullPath(args[1]));
Type difficulty = game.GetType("RimWorld.Difficulty", throwOnError: true)!;
Type session = adapter.GetType("Multiplayer.Foundation.RimWorld.MultiplayerSession", throwOnError: true)!;
MethodInfo fingerprint = session.GetMethod("DifficultyFingerprint", BindingFlags.NonPublic | BindingFlags.Static)!;
FieldInfo threat = difficulty.GetField("threatScale", BindingFlags.Public | BindingFlags.Instance)!;
int fields = difficulty.GetFields(BindingFlags.Public | BindingFlags.Instance).Length;
if (fields != 51) throw new InvalidOperationException($"Difficulty has {fields} public fields; recheck the fingerprint for this RimWorld build.");

object values = RuntimeHelpers.GetUninitializedObject(difficulty);
threat.SetValue(values, 1f);
string first = (string)fingerprint.Invoke(null, new[] { values })!;
string repeated = (string)fingerprint.Invoke(null, new[] { values })!;
threat.SetValue(values, 1.5f);
string changed = (string)fingerprint.Invoke(null, new[] { values })!;
if (first.Length != 64 || first != repeated || first == changed)
    throw new InvalidOperationException("Difficulty fingerprint was unstable or missed a changed gameplay value.");

Console.WriteLine("PASS: 51 Difficulty fields encoded; fingerprint stable and changes with threatScale.");
CheckCall("Verse.TickManager", "DoSingleTick", "RimWorld.Storyteller", "StorytellerTick");
CheckCall("RimWorld.Storyteller", "StorytellerTick", "RimWorld.IncidentQueue", "IncidentQueueTick");
CheckCall("RimWorld.Storyteller", "get_AllIncidentTargets", "Verse.Find", "get_Maps");
CheckCall("RimWorld.Storyteller", "get_AllIncidentTargets", "Verse.Find", "get_WorldObjects");
CheckCall("RimWorld.IncidentQueue", "IncidentQueueTick", "Verse.Find", "get_Storyteller");
CheckCall("RimWorld.Storyteller", "StorytellerTick", "RimWorld.Storyteller", "MakeIncidentsForInterval");
Console.WriteLine("PASS: vanilla tick, global incident targets and global queued-incident owner paths confirmed.");
PrintSurface("RimWorld.Storyteller", "StorytellerTick", "TryFire", "get_AllIncidentTargets", "ExposeData");
PrintSurface("RimWorld.IncidentQueue", "IncidentQueueTick", "ExposeData");
PrintSurface("Verse.Find", "get_Storyteller");
Type firing = game.GetType("RimWorld.FiringIncident", throwOnError: true)!;
if (firing.GetField("parms", BindingFlags.Public | BindingFlags.Instance) == null)
    throw new InvalidOperationException("FiringIncident.parms missing.");
Type parms = game.GetType("RimWorld.IncidentParms", throwOnError: true)!;
if (parms.GetField("target", BindingFlags.Public | BindingFlags.Instance) == null)
    throw new InvalidOperationException("IncidentParms.target missing.");
Type storyteller = game.GetType("RimWorld.Storyteller", throwOnError: true)!;
if (!storyteller.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
    .Any(method => method.Name == "TryFire" && method.GetParameters().Length == 2 &&
        method.GetParameters()[0].ParameterType == firing && method.GetParameters()[1].ParameterType == typeof(bool)))
    throw new InvalidOperationException("Storyteller.TryFire(FiringIncident, bool) missing.");
Console.WriteLine("PASS: scoped incident owner, target and fire guards have matching signatures.");
return 0;

void PrintSurface(string typeName, params string[] methodNames)
{
    Type type = game.GetType(typeName, throwOnError: true)!;
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    foreach (string name in methodNames)
    {
        MethodInfo[] matches = type.GetMethods(flags)
            .Where(candidate => candidate.DeclaringType == type && candidate.Name == name).ToArray();
        if (matches.Length == 0) throw new InvalidOperationException($"Missing method {typeName}.{name}.");
        foreach (MethodInfo method in matches)
            Console.WriteLine("SURFACE: " + method);
    }
    if (typeName == "RimWorld.Storyteller")
    {
        foreach (ConstructorInfo constructor in type.GetConstructors(flags))
            Console.WriteLine("SURFACE: " + constructor);
    }
}

void CheckCall(string typeName, string methodName, string calleeType, string calleeName)
{
    Type type = game.GetType(typeName, throwOnError: true)!;
    MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
        .Single(candidate => candidate.DeclaringType == type && candidate.Name == methodName);
    if (!Calls(method).Any(member => member.DeclaringType?.FullName == calleeType && member.Name == calleeName))
        throw new InvalidOperationException($"Expected call missing: {typeName}.{methodName} -> {calleeType}.{calleeName}");
}

static System.Collections.Generic.IEnumerable<MemberInfo> Calls(MethodInfo method)
{
    byte[] il = method.GetMethodBody()?.GetILAsByteArray() ?? Array.Empty<byte>();
    var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => unchecked((ushort)opcode.Value));
    for (int offset = 0; offset < il.Length;)
    {
        ushort raw = il[offset++];
        if (raw == 0xfe) raw = (ushort)(0xfe00 | il[offset++]);
        if (!opcodes.TryGetValue(raw, out OpCode opcode))
            throw new InvalidOperationException($"Unknown IL opcode {raw:X4} in {method}.");
        int operand = opcode.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineField or
                OperandType.InlineMethod or OperandType.InlineString or OperandType.InlineSig or
                OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
            _ => throw new InvalidOperationException($"Unsupported IL operand {opcode.OperandType}.")
        };
        if (opcode == OpCodes.Call || opcode == OpCodes.Callvirt)
            yield return method.Module.ResolveMember(BitConverter.ToInt32(il, offset),
                method.DeclaringType?.GetGenericArguments(), method.GetGenericArguments())!;
        offset += operand;
        if (offset > il.Length) throw new InvalidOperationException($"Malformed IL in {method}.");
    }
}
