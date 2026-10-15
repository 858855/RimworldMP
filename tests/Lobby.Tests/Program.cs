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

// ADR 0015/0017: setup phases, flags, host-only site choice, everyone done before characters continue.
var setup = new LobbyRegistry(8);
Guid host = Guid.NewGuid(), g1 = Guid.NewGuid(), g2 = Guid.NewGuid(), late = Guid.NewGuid(), solo = Guid.NewGuid();
foreach (var (id, name) in new[] { (host, "Host"), (g1, "Guest1"), (g2, "Guest2"), (late, "Late"), (solo, "Solo") })
    setup.AddMember(id, name, PlayerKey.Local(Guid.NewGuid()));
Guid room = setup.Create(host, "Setup", 4).State!.Id;
setup.Join(g1, room); setup.Join(g2, room);
Guid soloRoom = setup.Create(solo, "Alone", 4).State!.Id;
setup.Ready(solo, true);
Expect(6, () => setup.AdvanceSetup(solo, SetupPhase.Scenario));                       // fewer than 2 players
Expect(3, () => setup.AdvanceSetup(g1, SetupPhase.Scenario));                         // not host
Expect(6, () => setup.AdvanceSetup(host, SetupPhase.Storyteller));                    // skips a phase
setup.Ready(host, true); setup.Ready(g1, true);
Expect(6, () => setup.AdvanceSetup(host, SetupPhase.Scenario));                       // g2 not ready
setup.Ready(g2, true);
LobbyView scenario = setup.AdvanceSetup(host, SetupPhase.Scenario).State!;
Assert(scenario.Phase == SetupPhase.Scenario && scenario.Players.All(p => !p.Ready), "start enters Scenario and clears ready");
Expect(6, () => setup.AdvanceSetup(host, SetupPhase.Scenario));                       // repeated click skips nothing
Expect(6, () => setup.Join(late, room));                                              // lobby locked
Assert(setup.List().Single(r => r.Id == room).Phase == SetupPhase.Scenario, "lobby list shows the setup phase");
Expect(6, () => setup.Ready(g1, true));                                               // ready not used here
Expect(6, () => setup.SetSiteFlag(g1, new SiteTile(1, 0)));                           // flags only in site selection
setup.AdvanceSetup(host, SetupPhase.Storyteller); setup.AdvanceSetup(host, SetupPhase.WorldParams);
setup.AdvanceSetup(host, SetupPhase.WorldTransfer); setup.AdvanceSetup(host, SetupPhase.SiteSelection);
Expect(6, () => setup.AdvanceSetup(host, SetupPhase.Ideology));                       // only by choosing a site

var a = new SiteTile(100, 0); var b = new SiteTile(200, 0); var c = new SiteTile(300, 1); var d = new SiteTile(400, 0);
setup.SetSiteFlag(g1, a); setup.SetSiteFlag(g2, b); setup.SetSiteFlag(host, c);
setup.SetSiteFlag(g1, d);                                                             // replaces a
LobbyView flagged = setup.SetSiteFlag(g2, null).State!;                               // removes b
Assert(flagged.Players.Single(p => p.Id == g1).Flag == d && flagged.Players.Single(p => p.Id == g2).Flag == null &&
    flagged.Players.Single(p => p.Id == host).Flag == c, "flags are per player, replaceable and removable");
Assert(LobbyPayloads.ReadState(LobbyPayloads.State(flagged)).Players.Single(p => p.Id == host).Flag == c,
    "flag with planet layer survives the state payload");
Expect(3, () => setup.DecideSite(g1, d));                                             // guests only suggest
var chosen = new SiteTile(555, 0);                                                    // host may pick an unflagged tile
LobbyView ideology = setup.DecideSite(host, chosen).State!;
Assert(ideology.Phase == SetupPhase.Ideology && ideology.Site == chosen && ideology.Players.All(p => p.Flag == null),
    "host choice ends site selection, stores the site and clears flags");
Expect(6, () => setup.SetSiteFlag(g1, a));
Expect(6, () => setup.DecideSite(host, a));

LobbyView characters = setup.AdvanceSetup(host, SetupPhase.Characters).State!;
Assert(characters.Players.All(p => !p.Ready), "lobby ready never counts as done");
setup.Ready(host, true); setup.Ready(g1, true);
Expect(6, () => setup.AdvanceSetup(host, SetupPhase.Generating));                     // g2 not done, no force-start
setup.Ready(g1, false);
setup.Ready(g2, true);
Expect(6, () => setup.AdvanceSetup(host, SetupPhase.Generating));                     // g1 took it back
setup.Ready(g1, true);
Assert(setup.AdvanceSetup(host, SetupPhase.Generating).State!.Phase == SetupPhase.Generating, "all done: characters continue");
Expect(6, () => setup.AdvanceSetup(host, (SetupPhase)9));                             // later phases not yet
Expect(3, () => setup.AbortSetup(g1));
LobbyView aborted = setup.AbortSetup(host).State!;
Assert(aborted.Phase == SetupPhase.Lobby && aborted.Site == null && aborted.Players.All(p => !p.Ready), "host abort resets to lobby");
Expect(6, () => setup.AbortSetup(host));
setup.Join(late, room);                                                               // unlocked again
Console.WriteLine("PASS: setup phases, lock, host-only steps, flags and host site choice, everyone done before characters continue");

// A guest who leaves stops counting; a host who leaves ends the setup before the role moves.
setup.Leave(late);
foreach (Guid id in new[] { host, g1, g2 }) setup.Ready(id, true);
setup.AdvanceSetup(host, SetupPhase.Scenario);
foreach (SetupPhase next in new[] { SetupPhase.Storyteller, SetupPhase.WorldParams, SetupPhase.WorldTransfer, SetupPhase.SiteSelection })
    setup.AdvanceSetup(host, next);
setup.DecideSite(host, chosen); setup.AdvanceSetup(host, SetupPhase.Characters);
setup.Ready(host, true); setup.Ready(g1, true);
setup.Leave(g2);
Assert(setup.AdvanceSetup(host, SetupPhase.Generating).State!.Phase == SetupPhase.Generating, "departed guest no longer blocks");
LobbyView afterHost = setup.Leave(host).State!;
Assert(afterHost.Phase == SetupPhase.Lobby && afterHost.Site == null && afterHost.HostId == g1, "host leaving ends setup, then role moves");
Console.WriteLine("PASS: leaving guest stops counting; leaving host ends setup before the host role moves");

static void Assert(bool condition, string name)
{ if (!condition) throw new Exception("FAIL: " + name); }
static void Expect(byte code, Action action)
{
    try { action(); }
    catch (LobbyRuleException ex) when (ex.Code == code) { return; }
    throw new Exception("Expected LobbyRuleException " + code);
}
