using Multiplayer.Foundation.Lobby;
using Multiplayer.Foundation.Protocol;

var registry = new LobbyRegistry(8);
Guid alice = Guid.NewGuid(), bob = Guid.NewGuid(), carol = Guid.NewGuid();
registry.AddMember(alice, "Alice", PlayerKey.Local(Guid.NewGuid())); registry.AddMember(bob, "Bob", PlayerKey.Local(Guid.NewGuid())); registry.AddMember(carol, "Carol", PlayerKey.Local(Guid.NewGuid()));
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
Expect(1, () => registry.AddMember(Guid.NewGuid(), "bad\nname", PlayerKey.Local(Guid.NewGuid())));
Console.WriteLine("PASS: lobby create/list/join/capacity/ready/host/leave/disconnect/payload");

var keyed = new LobbyRegistry(8);
PlayerKey steamKey = PlayerKey.Steam(76561198000000001UL);
Guid first = Guid.NewGuid(), second = Guid.NewGuid();
keyed.AddMember(first, "First", steamKey);
Expect(6, () => keyed.AddMember(second, "Second", steamKey));
Expect(1, () => keyed.AddMember(second, "Second", default));
var keyedRoom = keyed.Create(first, "Keyed", 4);
Assert(keyedRoom.State!.Players.Single().Key == steamKey, "lobby view carries the player key");
var keyedRoundTrip = LobbyPayloads.ReadState(LobbyPayloads.State(keyedRoom.State));
Assert(keyedRoundTrip.Players.Single().Key == steamKey, "player key survives the state payload");
keyed.RemoveMember(first);
keyed.AddMember(second, "Second", steamKey);
Console.WriteLine("PASS: player key unique per connected player, carried in lobby state, freed on disconnect");

static void Assert(bool condition, string name)
{ if (!condition) throw new Exception("FAIL: " + name); }
static void Expect(byte code, Action action)
{
    try { action(); }
    catch (LobbyRuleException ex) when (ex.Code == code) { return; }
    throw new Exception("Expected LobbyRuleException " + code);
}
