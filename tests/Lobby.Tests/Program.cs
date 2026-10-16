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
setup.AdvanceSetup(host, SetupPhase.WorldTransfer);
// SETUP-002: site selection waits until every guest loaded the host's world with the same fingerprint.
byte[] world = Enumerable.Repeat((byte)9, 32).ToArray(), otherWorld = Enumerable.Repeat((byte)8, 32).ToArray();
Expect(6, () => setup.AdvanceSetup(host, SetupPhase.SiteSelection));                   // world not sent yet
Expect(3, () => setup.SetHostWorld(g1, world));                                       // only the host sends the world
Expect(6, () => setup.ReportWorld(host, world));                                      // the host does not report
Assert(setup.SetHostWorld(host, world).State!.Players.Count(p => p.WorldStatus == WorldStatus.Waiting) == 2, "guests wait");
Expect(6, () => setup.AdvanceSetup(host, SetupPhase.SiteSelection));                   // guests have not loaded it
setup.ReportWorld(g1, world);
LobbyView worldState = setup.ReportWorld(g2, otherWorld).State!;
Assert(worldState.Players.Single(p => p.Id == host).WorldStatus == WorldStatus.Match &&
    worldState.Players.Single(p => p.Id == g1).WorldStatus == WorldStatus.Match &&
    worldState.Players.Single(p => p.Id == g2).WorldStatus == WorldStatus.Mismatch, "world statuses");
Expect(8, () => setup.AdvanceSetup(host, SetupPhase.SiteSelection));                   // fingerprint differs
Assert(setup.SetHostWorld(host, otherWorld).State!.Players.Single(p => p.Id == g1).WorldStatus == WorldStatus.Waiting,
    "a new host world resets earlier reports");
setup.SetHostWorld(host, world); setup.ReportWorld(g1, world); setup.ReportWorld(g2, world);
Assert(setup.AdvanceSetup(host, SetupPhase.SiteSelection).State!.Players.All(p => p.WorldStatus == WorldStatus.None),
    "world status only shows during world transfer");
Expect(6, () => setup.ReportWorld(g1, world));                                        // only during world transfer
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
Expect(6, () => setup.Ready(g1, true));                                               // SETUP-004: done needs a pawn
setup.SetRoster(host, RosterFor(characters.Players.Select(p => p.Key).ToArray()));
setup.Ready(host, true); setup.Ready(g1, true);
Expect(6, () => setup.AdvanceSetup(host, SetupPhase.Generating));                     // g2 not done, no force-start
setup.Ready(g1, false);
setup.Ready(g2, true);
Expect(6, () => setup.AdvanceSetup(host, SetupPhase.Generating));                     // g1 took it back
setup.Ready(g1, true);
Assert(setup.AdvanceSetup(host, SetupPhase.Generating).State!.Phase == SetupPhase.Generating, "all done: characters continue");
// SETUP-005: landing waits until every guest loaded the host's game with the same fingerprint.
byte[] game = Enumerable.Repeat((byte)7, 32).ToArray(), otherGame = Enumerable.Repeat((byte)6, 32).ToArray();
Expect(6, () => setup.AdvanceSetup(host, SetupPhase.Playing));                        // game not sent yet
Expect(3, () => setup.SetHostGame(g1, game));                                         // only the host sends the game
Expect(6, () => setup.ReportGame(host, game));                                        // the host does not report
Expect(6, () => setup.ReportWorld(g1, game));                                         // world reports are over
// Guests load while the host reloads its own save, so a guest may report first.
Assert(setup.ReportGame(g1, game).State!.Players.All(p => p.WorldStatus == WorldStatus.Waiting), "early report waits for the host");
Expect(6, () => setup.AdvanceSetup(host, SetupPhase.Playing));
LobbyView announced = setup.SetHostGame(host, game).State!;
Assert(announced.Players.Single(p => p.Id == g1).WorldStatus == WorldStatus.Match &&
    announced.Players.Single(p => p.Id == g2).WorldStatus == WorldStatus.Waiting, "early report counts once the host announces");
Assert(setup.ReportGame(g2, otherGame).State!.Players.Single(p => p.Id == g2).WorldStatus == WorldStatus.Mismatch, "a differing game shows");
Expect(8, () => setup.AdvanceSetup(host, SetupPhase.Playing));
setup.ReportGame(g2, game);
LobbyView landed = setup.AdvanceSetup(host, SetupPhase.Playing).State!;
Assert(landed.Phase == SetupPhase.Playing && landed.Players.All(p => p.WorldStatus == WorldStatus.Match), "all loaded: everyone landed");
Expect(6, () => setup.ReportGame(g1, game));                                          // only while the game starts
Expect(6, () => setup.AdvanceSetup(host, (SetupPhase)10));                            // nothing after landing yet
Expect(6, () => setup.Ready(g1, true));
// JOIN-001 (ADR 0032): a running game accepts joiners; before it, the lobby stayed locked during setup.
Assert(setup.Join(late, room).State!.Players.Any(p => p.Id == late), "joining a running game is allowed");
setup.Leave(late);
Expect(3, () => setup.AbortSetup(g1));
LobbyView aborted = setup.AbortSetup(host).State!;
Assert(aborted.Phase == SetupPhase.Lobby && aborted.Site == null && aborted.Players.All(p => !p.Ready && p.WorldStatus == WorldStatus.None),
    "host abort resets to lobby");
Expect(6, () => setup.AbortSetup(host));
setup.Join(late, room);                                                               // unlocked again
Console.WriteLine("PASS: setup phases, lock, host-only steps, flags and host site choice, everyone done before characters continue, landing after every guest loaded the game (SETUP-005)");

// A guest who leaves stops counting; a host who leaves ends the setup before the role moves.
setup.Leave(late);
foreach (Guid id in new[] { host, g1, g2 }) setup.Ready(id, true);
setup.AdvanceSetup(host, SetupPhase.Scenario);
foreach (SetupPhase next in new[] { SetupPhase.Storyteller, SetupPhase.WorldParams, SetupPhase.WorldTransfer })
    setup.AdvanceSetup(host, next);
setup.SetHostWorld(host, world); setup.ReportWorld(g1, world); setup.ReportWorld(g2, world);
setup.AdvanceSetup(host, SetupPhase.SiteSelection);
setup.DecideSite(host, chosen);
LobbyView againCharacters = setup.AdvanceSetup(host, SetupPhase.Characters).State!;
setup.SetRoster(host, RosterFor(againCharacters.Players.Select(p => p.Key).ToArray()));
setup.Ready(host, true); setup.Ready(g1, true);
setup.Leave(g2);
Assert(setup.AdvanceSetup(host, SetupPhase.Generating).State!.Phase == SetupPhase.Generating, "departed guest no longer blocks");
LobbyView afterHost = setup.Leave(host).State!;
Assert(afterHost.Phase == SetupPhase.Lobby && afterHost.Site == null && afterHost.HostId == g1, "host leaving ends setup, then role moves");
Console.WriteLine("PASS: leaving guest stops counting; leaving host ends setup before the host role moves");

// MOD-001: mod status per member, Ready gate in the lobby, start re-check, status follows the host.
var mods = new LobbyRegistry(8);
Guid mHost = Guid.NewGuid(), mSame = Guid.NewGuid(), mOther = Guid.NewGuid(), mLate = Guid.NewGuid();
foreach (var (id, name) in new[] { (mHost, "Host"), (mSame, "Same"), (mOther, "Other"), (mLate, "Late") })
    mods.AddMember(id, name, PlayerKey.Local(Guid.NewGuid()));
var hostMods = new ModManifestSummary(Enumerable.Repeat((byte)1, 32).ToArray(), "1.6.4871 rev590", 12);
var otherMods = new ModManifestSummary(Enumerable.Repeat((byte)2, 32).ToArray(), "1.6.4871 rev591", 13);
Guid mRoom = mods.Create(mHost, "Mods", 4).State!.Id;
Assert(mods.SetManifest(mHost, hostMods)!.State!.Players.Single().ModStatus == ModStatus.Match, "host matches itself");
Assert(mods.List().Single().HostMods!.SameAs(hostMods), "lobby list shows host mods");
mods.SetManifest(mSame, hostMods); mods.SetManifest(mOther, otherMods);
mods.Join(mSame, mRoom); mods.Join(mOther, mRoom);
var modStatus = mods.Join(mLate, mRoom).State!.Players.ToDictionary(p => p.Id, p => p.ModStatus);
Assert(modStatus[mSame] == ModStatus.Match && modStatus[mOther] == ModStatus.Mismatch && modStatus[mLate] == ModStatus.Checking,
    "member statuses");
mods.Ready(mHost, true);
Assert(mods.Ready(mSame, true).State!.Players.Single(p => p.Id == mSame).Ready, "matching member may be ready");
Expect(8, () => mods.Ready(mOther, true));
Expect(8, () => mods.Ready(mLate, true));
Assert(!mods.Ready(mOther, false).State!.Players.Single(p => p.Id == mOther).Ready, "un-ready always allowed");
Expect(8, () => mods.AdvanceSetup(mHost, SetupPhase.Scenario)); // start re-checks mods
Assert(LobbyRegistry.StartBlockReason(mods.SetManifest(mLate, hostMods)!.State!, mHost)!.StartsWith("Mods stimmen nicht"),
    "client-side reason names the mismatch");
mods.Leave(mOther);
mods.Ready(mLate, true);
Assert(LobbyRegistry.StartBlockReason(mods.Ready(mHost, true).State!, mHost) == null, "client-side reason agrees once all match");
Assert(mods.AdvanceSetup(mHost, SetupPhase.Scenario).State!.Phase == SetupPhase.Scenario, "start with matching mods");
mods.AbortSetup(mHost);
mods.Join(mOther, mRoom);
var afterHostLeft = mods.Leave(mHost).State!;
Assert(afterHostLeft.HostId == mSame && afterHostLeft.Players.Single(p => p.Id == mOther).ModStatus == ModStatus.Mismatch,
    "status follows the new host");
var plain = new LobbyRegistry(8);
Guid p1 = Guid.NewGuid(), p2 = Guid.NewGuid();
plain.AddMember(p1, "P1", PlayerKey.Local(Guid.NewGuid())); plain.AddMember(p2, "P2", PlayerKey.Local(Guid.NewGuid()));
plain.Join(p2, plain.Create(p1, "Plain", 2).State!.Id);
Assert(plain.Ready(p2, true).State!.Players.All(p => p.ModStatus == ModStatus.Unchecked), "without manifests nothing is checked");
Console.WriteLine("PASS: mod status, ready gate, start re-check and host change (MOD-001)");

// Debug option: one player may start only when the host's server allows it.
foreach (bool allowSolo in new[] { false, true })
{
    var soloRegistry = new LobbyRegistry(4, allowSolo ? (byte)1 : (byte)2);
    Guid alone = Guid.NewGuid();
    soloRegistry.AddMember(alone, "Alone", PlayerKey.Local(Guid.NewGuid()));
    soloRegistry.Create(alone, "Solo", 2);
    LobbyView soloReady = soloRegistry.Ready(alone, true).State!;
    Assert((LobbyRegistry.StartBlockReason(soloReady, alone, allowSolo) == null) == allowSolo, "client-side reason follows the option");
    if (allowSolo) Assert(soloRegistry.AdvanceSetup(alone, SetupPhase.Scenario).State!.Phase == SetupPhase.Scenario, "solo start allowed");
    else Expect(6, () => soloRegistry.AdvanceSetup(alone, SetupPhase.Scenario));
}
Console.WriteLine("PASS: solo start only with the debug option");

// SETUP-004: roster from the host only; done needs an own selected pawn; any change to your pawns resets done.
var pawns = new LobbyRegistry(4, 1);
Guid pHost = Guid.NewGuid(), pGuest = Guid.NewGuid();
PlayerKey pHostKey = PlayerKey.Local(Guid.NewGuid()), pGuestKey = PlayerKey.Local(Guid.NewGuid());
pawns.AddMember(pHost, "Host", pHostKey); pawns.AddMember(pGuest, "Guest", pGuestKey);
pawns.Join(pGuest, pawns.Create(pHost, "Pawns", 4).State!.Id);
pawns.Ready(pHost, true); pawns.Ready(pGuest, true);
pawns.AdvanceSetup(pHost, SetupPhase.Scenario);
foreach (SetupPhase next in new[] { SetupPhase.Storyteller, SetupPhase.WorldParams, SetupPhase.WorldTransfer })
    pawns.AdvanceSetup(pHost, next);
byte[] pWorld = Enumerable.Repeat((byte)4, 32).ToArray();
pawns.SetHostWorld(pHost, pWorld); pawns.ReportWorld(pGuest, pWorld);
pawns.AdvanceSetup(pHost, SetupPhase.SiteSelection); pawns.DecideSite(pHost, new SiteTile(9, 0));
Expect(6, () => pawns.SetRoster(pHost, RosterFor(pHostKey)));                         // not yet in character selection
pawns.AdvanceSetup(pHost, SetupPhase.Characters);
Expect(3, () => pawns.SetRoster(pGuest, RosterFor(pGuestKey)));                       // only the host publishes pawns
Expect(6, () => pawns.PawnRequestRoute(pHost));                                       // host changes pawns locally
Assert(pawns.PawnRequestRoute(pGuest) == (pHost, pGuestKey), "guest requests go to the host with the guest's key");
var onlyHost = pawns.SetRoster(pHost, RosterFor(pHostKey));
Assert(onlyHost.Viewers.Single() == pGuest, "roster goes to the guest");
Expect(6, () => pawns.Ready(pGuest, true));                                           // guest owns no pawn yet
pawns.SetRoster(pHost, RosterFor(pHostKey, pGuestKey));
pawns.Ready(pHost, true);
Assert(pawns.Ready(pGuest, true).State!.Players.All(p => p.Ready), "both done with own pawns");
Assert(pawns.SetRoster(pHost, RosterFor(pHostKey, pGuestKey)).Change.State!.Players.All(p => p.Ready), "unchanged roster keeps done");
var renamed = RosterFor(pHostKey, pGuestKey);
renamed = new PawnRoster(new[] { renamed.Cards[0], new PawnCard(101, true, pGuestKey, "Renamed", "R", "Age 30", "", "",
    Array.Empty<string>(), Array.Empty<(string, byte, byte)>(), Array.Empty<string>(), Array.Empty<string>()) });
LobbyView afterRename = pawns.SetRoster(pHost, renamed).Change.State!;
Assert(afterRename.Players.Single(p => p.Id == pHost).Ready && !afterRename.Players.Single(p => p.Id == pGuest).Ready,
    "a change to the guest's pawn resets only the guest");
pawns.Ready(pGuest, true);
var leftBehind = new PawnRoster(new[] { renamed.Cards[0], new PawnCard(101, false, pGuestKey, "Renamed", "R", "Age 30", "", "",
    Array.Empty<string>(), Array.Empty<(string, byte, byte)>(), Array.Empty<string>(), Array.Empty<string>()) });
Assert(!pawns.SetRoster(pHost, leftBehind).Change.State!.Players.Single(p => p.Id == pGuest).Ready,
    "a pawn moved out of the starting group no longer counts");
Expect(6, () => pawns.AdvanceSetup(pHost, SetupPhase.Generating));
Console.WriteLine("PASS: pawn roster from the host, done needs an own pawn, changes reset done (SETUP-004)");

// One selected pawn per given owner (SETUP-004 test helper).
// ADR 0031: the game mode is fixed at creation and visible to everyone; Factions cannot start yet.
var modes = new LobbyRegistry(8);
Guid fh = Guid.NewGuid(), fg = Guid.NewGuid(), ch = Guid.NewGuid(), cg = Guid.NewGuid();
foreach (Guid id in new[] { fh, fg, ch, cg }) modes.AddMember(id, "P" + id.ToString("N")[..4], PlayerKey.Local(Guid.NewGuid()));
Guid factionRoom = modes.Create(fh, "Factions", 4, LobbyMode.Factions(FactionRelation.Neutral)).State!.Id;
Guid coopRoom = modes.Create(ch, "Democracy", 4, LobbyMode.CoopDemocracy).State!.Id;
LobbyView joinedFactions = modes.Join(fg, factionRoom).State!;
Assert(joinedFactions.Mode == LobbyMode.Factions(FactionRelation.Neutral), "joiner sees the mode in the lobby state");
Assert(modes.List().Single(r => r.Id == factionRoom).Mode.Mode == GameMode.Factions, "lobby list shows the mode before joining");
modes.Ready(fh, true); modes.Ready(fg, true);
Expect(6, () => modes.AdvanceSetup(fh, SetupPhase.Scenario));
modes.Join(cg, coopRoom); modes.Ready(ch, true); modes.Ready(cg, true);
LobbyView democracy = modes.AdvanceSetup(ch, SetupPhase.Scenario).State!;
Assert(democracy.Phase == SetupPhase.Scenario && democracy.Mode == LobbyMode.CoopDemocracy, "Coop starts and keeps its mode");
Console.WriteLine("PASS: game mode fixed at creation, shown in state and list, Factions start refused for now");

static PawnRoster RosterFor(params PlayerKey[] owners) => new PawnRoster(owners.Select((owner, i) => new PawnCard(100 + i, true, owner,
    "Pawn " + i, "P" + i, "Age 30", "", "", Array.Empty<string>(), Array.Empty<(string, byte, byte)>(),
    Array.Empty<string>(), Array.Empty<string>())).ToArray());
static void Assert(bool condition, string name)
{ if (!condition) throw new Exception("FAIL: " + name); }
static void Expect(byte code, Action action)
{
    try { action(); }
    catch (LobbyRuleException ex) when (ex.Code == code) { return; }
    throw new Exception("Expected LobbyRuleException " + code);
}
