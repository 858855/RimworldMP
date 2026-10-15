using Multiplayer.Foundation.Lobby;
using Multiplayer.Foundation.Protocol;

var registry = new LobbyRegistry(8);
Guid alice = Guid.NewGuid(), bob = Guid.NewGuid(), carol = Guid.NewGuid();
registry.AddMember(alice, "Alice"); registry.AddMember(bob, "Bob"); registry.AddMember(carol, "Carol");
var created = registry.Create(alice, "Test Lobby", 2);
Assert(created.State!.Players.Count == 1 && created.State.HostId == alice, "created room");
Assert(registry.List().Count == 1, "list room");
var joined = registry.Join(bob, created.State.Id);
Assert(joined.State!.Players.Count == 2 && joined.State.Revision == 2, "join and revision");
Expect(5, () => registry.Join(carol, created.State.Id));
Expect(6, () => registry.Join(alice, created.State.Id));
Expect(3, () => registry.Close(bob));
var ready = registry.Ready(bob, true);
Assert(ready.State!.Players.Single(p => p.Id == bob).Ready, "ready");
var encoded = LobbyPayloads.State(ready.State);
var decoded = LobbyPayloads.ReadState(encoded);
Assert(decoded.Id == ready.State.Id && decoded.Revision == ready.State.Revision && decoded.Players.Count == 2, "state payload");
var list = LobbyPayloads.ReadList(LobbyPayloads.List(registry.List()));
Assert(list.Count == 1 && list[0].HostName == "Alice", "list payload");
var left = registry.Leave(alice);
Assert(left.State!.HostId == bob && left.State.Players.Count == 1, "host transfer");
registry.RemoveMember(bob);
Assert(registry.List().Count == 0, "disconnect clears empty lobby");
registry.Create(carol, "Second", 4);
Assert(registry.List().Count == 1, "new lobby after cleanup");
Expect(1, () => registry.AddMember(Guid.NewGuid(), "bad\nname"));
Console.WriteLine("PASS: lobby create/list/join/capacity/ready/host/leave/disconnect/payload");

// MOD-001: Ready is gated by the member's manifest compared with the host's.
var mods = new LobbyRegistry(8);
Guid host = Guid.NewGuid(), same = Guid.NewGuid(), other = Guid.NewGuid(), late = Guid.NewGuid();
foreach (var (id, name) in new[] { (host, "Host"), (same, "Same"), (other, "Other"), (late, "Late") }) mods.AddMember(id, name);
var hostMods = new ModManifestSummary(Enumerable.Repeat((byte)1, 32).ToArray(), "1.6.4871 rev590", 12);
var otherMods = new ModManifestSummary(Enumerable.Repeat((byte)2, 32).ToArray(), "1.6.4871 rev590", 13);
Guid room = mods.Create(host, "Mods", 4).State!.Id;
Assert(mods.SetManifest(host, hostMods)!.State!.Players.Single().ModStatus == ModStatus.Match, "host matches itself");
Assert(mods.List().Single().HostMods!.SameAs(hostMods), "lobby list shows host mods");
mods.SetManifest(same, hostMods); mods.SetManifest(other, otherMods);
mods.Join(same, room); mods.Join(other, room);
var status = mods.Join(late, room).State!.Players.ToDictionary(p => p.Id, p => p.ModStatus);
Assert(status[same] == ModStatus.Match && status[other] == ModStatus.Mismatch && status[late] == ModStatus.Checking, "member statuses");
Assert(mods.Ready(same, true).State!.Players.Single(p => p.Id == same).Ready, "matching member may be ready");
Expect(8, () => mods.Ready(other, true));
Expect(8, () => mods.Ready(late, true));
Assert(!mods.Ready(other, false).State!.Players.Single(p => p.Id == other).Ready, "un-ready always allowed");
var fixedUp = mods.SetManifest(late, hostMods)!.State!;
Assert(fixedUp.Players.Single(p => p.Id == late).ModStatus == ModStatus.Match, "late manifest matches");
var afterHostLeft = mods.Leave(host).State!;
Assert(afterHostLeft.HostId == same && afterHostLeft.Players.Single(p => p.Id == same).Ready,
    "new host keeps its own ready state");
Assert(afterHostLeft.Players.Single(p => p.Id == other).ModStatus == ModStatus.Mismatch, "status follows new host");
var plain = new LobbyRegistry(8);
Guid p1 = Guid.NewGuid(), p2 = Guid.NewGuid();
plain.AddMember(p1, "P1"); plain.AddMember(p2, "P2");
Guid plainRoom = plain.Create(p1, "Plain", 2).State!.Id;
plain.Join(p2, plainRoom);
Assert(plain.Ready(p2, true).State!.Players.All(p => p.ModStatus == ModStatus.Unchecked), "no manifests stay unchecked");
Console.WriteLine("PASS: mod manifest status, ready gate and host change");

static void Assert(bool condition, string name)
{ if (!condition) throw new Exception("FAIL: " + name); }
static void Expect(byte code, Action action)
{
    try { action(); }
    catch (LobbyRuleException ex) when (ex.Code == code) { return; }
    throw new Exception("Expected LobbyRuleException " + code);
}
