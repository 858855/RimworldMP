using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

if (args.Length != 1 || !File.Exists(Path.Combine(args[0], "Assembly-CSharp.dll")))
{
    Console.Error.WriteLine("Usage: dotnet run --project scripts/faction-setup-probe -- <RimWorld Managed directory>");
    return 2;
}

string managed = Path.GetFullPath(args[0]);
AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
{
    string candidate = Path.Combine(managed, new AssemblyName(request.Name).Name + ".dll");
    return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
};

Assembly game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
string[] types =
{
    "Verse.GameInitData", "Verse.Game", "RimWorld.PageUtility",
    "RimWorld.FactionGenerator", "RimWorld.FactionManager", "RimWorld.Faction",
    "RimWorld.Planet.WorldGenerator"
};
foreach (string name in types)
{
    Type type = game.GetType(name, throwOnError: true)!;
    Console.WriteLine("TYPE: " + type.FullName);
    foreach (FieldInfo field in type.GetFields(flags).Where(field => field.DeclaringType == type &&
        (field.Name.Contains("faction", StringComparison.OrdinalIgnoreCase) ||
         field.Name.Contains("startingTile", StringComparison.OrdinalIgnoreCase) ||
         field.Name.Contains("ideo", StringComparison.OrdinalIgnoreCase))).OrderBy(field => field.Name, StringComparer.Ordinal))
        Console.WriteLine("  FIELD: " + field);
    foreach (MethodBase method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags))
        .Where(method => method.DeclaringType == type &&
            (method.Name.Contains("Faction", StringComparison.OrdinalIgnoreCase) ||
             method.Name.Contains("Generate", StringComparison.OrdinalIgnoreCase) ||
             method.Name.Contains("InitGame", StringComparison.OrdinalIgnoreCase) ||
             method.Name.Contains("Start", StringComparison.OrdinalIgnoreCase) ||
             method.Name.Contains("Player", StringComparison.OrdinalIgnoreCase) ||
             method.Name.Contains("Add", StringComparison.OrdinalIgnoreCase) ||
             method.Name.Contains("Ideo", StringComparison.OrdinalIgnoreCase) ||
             method.Name == ".ctor"))
        .OrderBy(method => method.Name, StringComparer.Ordinal))
        Console.WriteLine("  METHOD: " + method);
}

foreach (var (typeName, methodName) in new[]
{
    ("RimWorld.PageUtility", "InitGameStart"),
    ("RimWorld.PageUtility", "<InitGameStart>g__PreLoadAct|1_0"),
    ("Verse.GameInitData", "PrepForMapGen"),
    ("RimWorld.Faction", "get_OfPlayer"),
    ("RimWorld.Faction", "get_OfPlayerSilentFail"),
    ("RimWorld.FactionManager", "get_OfPlayer"),
    ("RimWorld.FactionManager", "Add"),
    ("RimWorld.FactionGenerator", "InitializeFactions"),
    ("RimWorld.FactionGenerator", "CreateFactionAndAddToManager"),
    ("RimWorld.Planet.WorldGenerator", "GenerateWorld"),
    ("RimWorld.FactionGenerator", "NewGeneratedFaction")
})
{
    Type type = game.GetType(typeName, throwOnError: true)!;
    MethodInfo[] methods = type.GetMethods(flags).Where(method => method.DeclaringType == type && method.Name == methodName).ToArray();
    foreach (MethodInfo method in methods)
    {
        Console.WriteLine("CALLS: " + method);
        foreach (MemberInfo called in Calls(method).Where(member =>
            member.DeclaringType?.FullName is string owner &&
            (owner.Contains("Faction", StringComparison.OrdinalIgnoreCase) ||
             owner.Contains("Game", StringComparison.OrdinalIgnoreCase) ||
             owner.Contains("World", StringComparison.OrdinalIgnoreCase))).DistinctBy(member => member.ToString()))
            Console.WriteLine("  -> " + called.DeclaringType?.FullName + "." + called.Name);
    }
}

RequireCall("RimWorld.PageUtility", "<InitGameStart>g__PreLoadAct|1_0", "Verse.GameInitData", "PrepForMapGen");
RequireCall("Verse.GameInitData", "PrepForMapGen", "RimWorld.Faction", "get_OfPlayer");
RequireCall("RimWorld.Faction", "get_OfPlayerSilentFail", "RimWorld.FactionManager", "get_OfPlayer");
RequireCall("RimWorld.FactionGenerator", "CreateFactionAndAddToManager", "RimWorld.FactionManager", "Add");
RequireCall("RimWorld.FactionManager", "Add", "RimWorld.FactionManager", "RecacheFactions");
Console.WriteLine("PASS: vanilla start, singleton player faction lookup, faction registration and recache call paths confirmed.");

return 0;

void RequireCall(string ownerName, string methodName, string calleeOwnerName, string calleeName)
{
    Type owner = game.GetType(ownerName, throwOnError: true)!;
    MethodInfo[] methods = owner.GetMethods(flags)
        .Where(method => method.DeclaringType == owner && method.Name == methodName).ToArray();
    if (!methods.Any(method => Calls(method).Any(called =>
        called.DeclaringType?.FullName == calleeOwnerName && called.Name == calleeName)))
        throw new InvalidOperationException($"Expected call missing: {ownerName}.{methodName} -> {calleeOwnerName}.{calleeName}.");
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
