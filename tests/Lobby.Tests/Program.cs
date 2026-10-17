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
// ADR 0035: lobby password, checked before anything else; the list shows that one is needed.
var locked = new LobbyRegistry(8);
Guid lh = Guid.NewGuid(), lg = Guid.NewGuid();
locked.AddMember(lh, "Host", PlayerKey.Local(Guid.NewGuid())); locked.AddMember(lg, "Guest", PlayerKey.Local(Guid.NewGuid()));
Guid lockedRoom = locked.Create(lh, "Locked", 4, null, "geheim").State!.Id;
Assert(locked.List().Single().PasswordRequired, "list shows the password requirement");
Expect(3, () => locked.Join(lg, lockedRoom));
Expect(3, () => locked.Join(lg, lockedRoom, "falsch"));
Expect(3, () => locked.Join(lg, lockedRoom, "geheim2"));
Assert(locked.Join(lg, lockedRoom, "geheim").State!.Players.Count == 2, "right password joins");
Console.WriteLine("PASS: lobby password: missing and wrong refused, right one joins, list flags it");

// ADR 0037: Factions mode – a faction slot per player key, a storyteller per faction, one shared difficulty from the host.
var fx = new LobbyRegistry(8, 2, factionsSetupEnabled: true);
Guid fxHost = Guid.NewGuid(), fxG1 = Guid.NewGuid(), fxG2 = Guid.NewGuid(), fxLate = Guid.NewGuid();
PlayerKey fxHostKey = PlayerKey.Local(Guid.NewGuid()), fxG1Key = PlayerKey.Local(Guid.NewGuid()), fxG2Key = PlayerKey.Local(Guid.NewGuid());
fx.AddMember(fxHost, "Host", fxHostKey); fx.AddMember(fxG1, "Guest1", fxG1Key); fx.AddMember(fxG2, "Guest2", fxG2Key);
fx.AddMember(fxLate, "Late", PlayerKey.Local(Guid.NewGuid()));
Guid fxRoom = fx.Create(fxHost, "Fraktionen", 4, LobbyMode.Factions(FactionRelation.Allied)).State!.Id;
fx.Join(fxG1, fxRoom);
LobbyView fxLobby = fx.Join(fxG2, fxRoom).State!;
Assert(fxLobby.Players.All(p => p.FactionSlot == null && p.Storyteller == null) && fxLobby.FactionOptions == null, "no factions before setup");
foreach (Guid id in new[] { fxHost, fxG1, fxG2 }) fx.Ready(id, true);
LobbyView fxScenario = fx.AdvanceSetup(fxHost, SetupPhase.Scenario).State!;
byte? SlotIn(LobbyView view, Guid id) => view.Players.Single(p => p.Id == id).FactionSlot;
string? TellerIn(LobbyView view, Guid id) => view.Players.Single(p => p.Id == id).Storyteller;
bool ReadyIn(LobbyView view, Guid id) => view.Players.Single(p => p.Id == id).Ready;
Assert(SlotIn(fxScenario, fxHost) == 0 && SlotIn(fxScenario, fxG1) == 1 && SlotIn(fxScenario, fxG2) == 2, "slots: host first, then joining order");
var tellers = new FactionOptions("Rough", new[] { "Cassandra", "Phoebe", "Randy" });
Expect(6, () => fx.SetFactionOptions(fxHost, tellers));                                // not the storyteller step yet
Expect(6, () => fx.ChooseStoryteller(fxG1, "Randy"));
LobbyView fxTellerStep = fx.AdvanceSetup(fxHost, SetupPhase.Storyteller).State!;
Assert(LobbyRegistry.StorytellerBlockReason(fxTellerStep)!.Contains("noch nicht festgelegt"), "reason: host has not set options");
Expect(6, () => fx.ChooseStoryteller(fxG1, "Randy"));                                  // no list from the host yet
Expect(6, () => fx.Ready(fxG1, true));                                                 // done needs a choice
Expect(6, () => fx.AdvanceSetup(fxHost, SetupPhase.WorldParams));
Expect(3, () => fx.SetFactionOptions(fxG1, new FactionOptions("Peaceful", new[] { "Randy" })));  // a guest cannot set the difficulty
LobbyView offered = fx.SetFactionOptions(fxHost, tellers).State!;
Assert(offered.FactionOptions!.SameAs(tellers), "everyone sees the shared difficulty and the list");
Expect(6, () => fx.ChooseStoryteller(fxG1, "Tutor"));                                  // not offered
fx.ChooseStoryteller(fxHost, "Cassandra"); fx.Ready(fxHost, true);
fx.ChooseStoryteller(fxG1, "Randy"); fx.Ready(fxG1, true);
Expect(6, () => fx.AdvanceSetup(fxHost, SetupPhase.WorldParams));                      // Guest2 has no storyteller
LobbyView missing = fx.ChooseStoryteller(fxHost, "Cassandra").State!;
Assert(LobbyRegistry.StorytellerBlockReason(missing) == "Erzähler fehlt: Guest2", "reason names who has no storyteller");
LobbyView allChosen = fx.ChooseStoryteller(fxG2, "Phoebe").State!;
Assert(LobbyRegistry.StorytellerBlockReason(allChosen) == "Warte auf: Guest2", "reason names who is not done");
Expect(6, () => fx.AdvanceSetup(fxHost, SetupPhase.WorldParams));                      // no force-start
LobbyView switched = fx.ChooseStoryteller(fxG1, "Phoebe").State!;
Assert(!ReadyIn(switched, fxG1) && ReadyIn(switched, fxHost) && TellerIn(switched, fxG1) == "Phoebe", "a new choice clears only the own done");
fx.Ready(fxG1, true);
Assert(ReadyIn(fx.ChooseStoryteller(fxG1, "Phoebe").State!, fxG1), "the same choice again keeps done");
fx.Ready(fxG2, true);
LobbyView harder = fx.SetFactionOptions(fxHost, new FactionOptions("Brutal", tellers.Storytellers)).State!;
Assert(harder.Players.All(p => !p.Ready) && TellerIn(harder, fxG1) == "Phoebe", "a new difficulty clears everyone's done, choices stay");
var narrow = new FactionOptions("Brutal", new[] { "Cassandra", "Randy" });
LobbyView narrowed = fx.SetFactionOptions(fxHost, narrow).State!;
Assert(TellerIn(narrowed, fxG1) == null && TellerIn(narrowed, fxG2) == null && TellerIn(narrowed, fxHost) == "Cassandra",
    "choices the host no longer offers are dropped");
Assert(fx.SetFactionOptions(fxHost, new FactionOptions("Brutal", new[] { "Cassandra", "Randy" })).State!.Revision == narrowed.Revision,
    "the same options again change nothing");
fx.ChooseStoryteller(fxG1, "Randy"); fx.ChooseStoryteller(fxG2, "Randy");
LobbyView afterLeave = fx.Leave(fxG2).State!;
Assert(afterLeave.Players.Count == 2 && SlotIn(afterLeave, fxHost) == 0 && SlotIn(afterLeave, fxG1) == 1, "a departed guest's slot is gone, others keep theirs");
foreach (Guid id in new[] { fxHost, fxG1 }) fx.Ready(id, true);
LobbyView fxWorldParams = fx.AdvanceSetup(fxHost, SetupPhase.WorldParams).State!;
Assert(fxWorldParams.Phase == SetupPhase.WorldParams && LobbyRegistry.StorytellerBlockReason(fxWorldParams) == null &&
    TellerIn(fxWorldParams, fxG1) == "Randy" && fxWorldParams.FactionOptions!.Difficulty == "Brutal", "all chosen and done: setup continues, choices stay");
Expect(6, () => fx.ChooseStoryteller(fxG1, "Cassandra"));                              // only in the storyteller step
Expect(6, () => fx.Ready(fxG1, true));                                                 // done is not used here
LobbyView fxRead = LobbyPayloads.ReadState(LobbyPayloads.State(fxWorldParams));
Assert(SlotIn(fxRead, fxG1) == 1 && TellerIn(fxRead, fxHost) == "Cassandra" && fxRead.FactionOptions!.SameAs(narrow), "factions survive the state payload");
// Through the rest of the setup into the running game.
fx.AdvanceSetup(fxHost, SetupPhase.WorldTransfer);
byte[] fxWorld = Enumerable.Repeat((byte)7, 32).ToArray();
fx.SetHostWorld(fxHost, fxWorld); fx.ReportWorld(fxG1, fxWorld);
fx.AdvanceSetup(fxHost, SetupPhase.SiteSelection);
ChooseOwnSitesAndIdeologies(fx, fxHost, fxG1);
fx.SetRoster(fxHost, RosterFor(fxHostKey, fxG1Key)); fx.Ready(fxHost, true); fx.Ready(fxG1, true);
fx.AdvanceSetup(fxHost, SetupPhase.Generating);
fx.SetHostGame(fxHost, fxWorld); fx.ReportGame(fxG1, fxWorld);
LobbyView fxPlaying = fx.AdvanceSetup(fxHost, SetupPhase.Playing).State!;
Assert(fxPlaying.Phase == SetupPhase.Playing && SlotIn(fxPlaying, fxG1) == 1, "the running game keeps the slots");
LobbyView gone = fx.Leave(fxG1).State!;
Assert(gone.Players.Count == 1, "guest left the running game");
try { fx.Join(fxLate, fxRoom); throw new Exception("FAIL: a new player joined a running Factions game"); }
catch (LobbyRuleException ex) { Assert(ex.Code == 6 && ex.Message.Contains("no faction"), "a new player has no faction in a running game"); }
fx.RemoveMember(fxG1); fx.AddMember(fxG1, "Guest1", fxG1Key);                          // reconnect: new session, same key
LobbyView back = fx.Join(fxG1, fxRoom).State!;
Assert(SlotIn(back, fxG1) == 1 && TellerIn(back, fxG1) == "Randy", "a returning player finds their faction and storyteller");
// Coop keeps the host's choice; abort clears every faction; without the test switch Factions still cannot start.
var coopTellers = new LobbyRegistry(8);
Guid ctHost = Guid.NewGuid(), ctGuest = Guid.NewGuid();
coopTellers.AddMember(ctHost, "Host", PlayerKey.Local(Guid.NewGuid())); coopTellers.AddMember(ctGuest, "Guest", PlayerKey.Local(Guid.NewGuid()));
coopTellers.Join(ctGuest, coopTellers.Create(ctHost, "Coop", 4, LobbyMode.CoopDemocracy).State!.Id);
coopTellers.Ready(ctHost, true); coopTellers.Ready(ctGuest, true);
coopTellers.AdvanceSetup(ctHost, SetupPhase.Scenario);
LobbyView coopStep = coopTellers.AdvanceSetup(ctHost, SetupPhase.Storyteller).State!;
Assert(coopStep.Players.All(p => p.FactionSlot == null) && LobbyRegistry.StorytellerBlockReason(coopStep) == null, "Coop has no faction slots");
Expect(6, () => coopTellers.SetFactionOptions(ctHost, tellers));
Expect(6, () => coopTellers.ChooseStoryteller(ctGuest, "Randy"));
Expect(6, () => coopTellers.Ready(ctGuest, true));
Assert(coopTellers.AdvanceSetup(ctHost, SetupPhase.WorldParams).State!.Phase == SetupPhase.WorldParams, "Coop storyteller step needs no choices");
var abortRegistry = new LobbyRegistry(8, 2, factionsSetupEnabled: true);
Guid abHost = Guid.NewGuid(), abGuest = Guid.NewGuid();
abortRegistry.AddMember(abHost, "Host", PlayerKey.Local(Guid.NewGuid())); abortRegistry.AddMember(abGuest, "Guest", PlayerKey.Local(Guid.NewGuid()));
abortRegistry.Join(abGuest, abortRegistry.Create(abHost, "F", 4, LobbyMode.Factions(FactionRelation.Hostile)).State!.Id);
abortRegistry.Ready(abHost, true); abortRegistry.Ready(abGuest, true);
abortRegistry.AdvanceSetup(abHost, SetupPhase.Scenario); abortRegistry.AdvanceSetup(abHost, SetupPhase.Storyteller);
abortRegistry.SetFactionOptions(abHost, tellers); abortRegistry.ChooseStoryteller(abGuest, "Randy");
LobbyView reset = abortRegistry.AbortSetup(abHost).State!;
Assert(reset.Phase == SetupPhase.Lobby && reset.FactionOptions == null && reset.Players.All(p => p.FactionSlot == null && p.Storyteller == null),
    "abort clears slots, choices and options");
var locked37 = new LobbyRegistry(8);
Guid lkHost = Guid.NewGuid(), lkGuest = Guid.NewGuid();
locked37.AddMember(lkHost, "Host", PlayerKey.Local(Guid.NewGuid())); locked37.AddMember(lkGuest, "Guest", PlayerKey.Local(Guid.NewGuid()));
locked37.Join(lkGuest, locked37.Create(lkHost, "F", 4, LobbyMode.Factions(FactionRelation.Neutral)).State!.Id);
locked37.Ready(lkHost, true); locked37.Ready(lkGuest, true);
Expect(6, () => locked37.AdvanceSetup(lkHost, SetupPhase.Scenario));
Console.WriteLine("PASS: factions: slot per player key, storyteller per faction, shared difficulty from the host, done rules, Factions start still refused (ADR 0037)");

// ADR 0038: own site and ideology per faction slot; no foreign or duplicate sites; the host's game rejects with a reason;
// slots stay stable through choices, departures and reconnects.
var fs = new LobbyRegistry(8, 2, factionsSetupEnabled: true);
Guid sHost = Guid.NewGuid(), sG1 = Guid.NewGuid(), sG2 = Guid.NewGuid();
PlayerKey sHostKey = PlayerKey.Local(Guid.NewGuid()), sG1Key = PlayerKey.Local(Guid.NewGuid()), sG2Key = PlayerKey.Local(Guid.NewGuid());
fs.AddMember(sHost, "Host", sHostKey); fs.AddMember(sG1, "Guest1", sG1Key); fs.AddMember(sG2, "Guest2", sG2Key);
Guid sRoom = fs.Create(sHost, "Orte", 4, LobbyMode.Factions(FactionRelation.Neutral)).State!.Id;
fs.Join(sG1, sRoom); fs.Join(sG2, sRoom);
Guid[] sAll = { sHost, sG1, sG2 };
foreach (Guid id in sAll) fs.Ready(id, true);
fs.AdvanceSetup(sHost, SetupPhase.Scenario); fs.AdvanceSetup(sHost, SetupPhase.Storyteller);
fs.SetFactionOptions(sHost, new FactionOptions("Rough", new[] { "Randy", "Phoebe" }));
foreach (Guid id in sAll) { fs.ChooseStoryteller(id, "Randy"); fs.Ready(id, true); }
fs.AdvanceSetup(sHost, SetupPhase.WorldParams); fs.AdvanceSetup(sHost, SetupPhase.WorldTransfer);
byte[] sWorld = Enumerable.Repeat((byte)9, 32).ToArray();
fs.SetHostWorld(sHost, sWorld); fs.ReportWorld(sG1, sWorld); fs.ReportWorld(sG2, sWorld);
LobbyView sSites = fs.AdvanceSetup(sHost, SetupPhase.SiteSelection).State!;
SiteTile? SiteIn(LobbyView view, Guid id) => view.Players.Single(p => p.Id == id).FactionSite;
string? NoteIn(LobbyView view, Guid id) => view.Players.Single(p => p.Id == id).FactionNote;
bool SlotsStable(LobbyView view) => view.Players.All(p => p.FactionSlot == (p.Key == sHostKey ? 0 : p.Key == sG1Key ? 1 : 2));
Assert(SlotsStable(sSites) && sSites.Site == null, "slots 0/1/2 and no global site in the Factions mode");
Expect(6, () => fs.SetSiteFlag(sG1, new SiteTile(1, 0)));                              // Coop flags are not used here
Expect(6, () => fs.DecideSite(sHost, new SiteTile(1, 0)));                             // the host does not choose for everyone
Expect(6, () => fs.ChooseIdeology(sG1, "Ideo", new byte[] { 1 }));                     // not the ideology step
Expect(6, () => fs.Ready(sG1, true));                                                  // done needs an own site
Expect(6, () => fs.AdvanceSetup(sHost, SetupPhase.Ideology));
var tileA = new SiteTile(40, 0); var tileAOrbit = new SiteTile(40, 1); var tileB = new SiteTile(55, 0);
LobbyView own = fs.ChooseFactionSite(sG1, tileA).State!;
Assert(SiteIn(own, sG1) == tileA && SiteIn(own, sHost) == null && SiteIn(own, sG2) == null && SlotsStable(own),
    "a choice only ever changes the sender's own faction");
try { fs.ChooseFactionSite(sG2, tileA); throw new Exception("FAIL: two factions on one site"); }
catch (LobbyRuleException ex) { Assert(ex.Code == 6 && ex.Message.Contains("already starts"), "a site another faction holds is refused"); }
Assert(SiteIn(fs.ChooseFactionSite(sG2, tileAOrbit).State!, sG2) == tileAOrbit, "the same tile id on another planet layer is another site");
fs.Ready(sG1, true);
LobbyView again = fs.ChooseFactionSite(sG1, tileA).State!;
Assert(again.Players.Single(p => p.Id == sG1).Ready && again.Revision == own.Revision + 2, "the same site again keeps done and changes nothing");
Assert(!fs.ChooseFactionSite(sG1, tileB).State!.Players.Single(p => p.Id == sG1).Ready, "a new site clears the own done");
Assert(SiteIn(fs.ChooseFactionSite(sG1, null).State!, sG1) == null, "a site can be withdrawn");
fs.ChooseFactionSite(sG1, tileA);
Assert(SiteIn(fs.ChooseFactionSite(sG2, tileB).State!, sG2) == tileB, "a withdrawn or replaced site is free again");
fs.ChooseFactionSite(sHost, new SiteTile(70, 0));
foreach (Guid id in sAll) fs.Ready(id, true);
Expect(3, () => fs.ClearFactionChoice(sG1, 2, FactionChoiceKind.Site, "nein"));        // only the host's game rejects
Expect(6, () => fs.ClearFactionChoice(sHost, 2, FactionChoiceKind.Ideology, "x"));     // not the ideology step
Expect(6, () => fs.ClearFactionChoice(sHost, 9, FactionChoiceKind.Site, "x"));         // no such slot
Expect(1, () => fs.ClearFactionChoice(sHost, 2, FactionChoiceKind.Site, new string('x', 129)));  // checked before any change
Expect(1, () => fs.ChooseIdeology(sG2, " ", new byte[1]));                            // an invalid label is refused before anything else

LobbyView rejected = fs.ClearFactionChoice(sHost, 2, FactionChoiceKind.Site, "Zu nah an Guest1").State!;
Assert(SiteIn(rejected, sG2) == null && !rejected.Players.Single(p => p.Id == sG2).Ready && NoteIn(rejected, sG2) == "Zu nah an Guest1" &&
    rejected.Players.Single(p => p.Id == sG1).Ready && NoteIn(rejected, sG1) == null && SlotsStable(rejected),
    "the host's rejection clears only that faction's site and done and tells why");
Assert(LobbyRegistry.FactionSetupBlockReason(rejected) == "Startort fehlt: Guest2", "the reason names who has no site");
Expect(6, () => fs.AdvanceSetup(sHost, SetupPhase.Ideology));
Assert(NoteIn(fs.ChooseFactionSite(sG2, new SiteTile(80, 0)).State!, sG2) == null, "the next own choice clears the note");
Assert(LobbyRegistry.FactionSetupBlockReason(fs.Ready(sHost, true).State!) == "Warte auf: Guest2", "then it waits for done");
fs.Ready(sG2, true);
LobbyView sIdeo = fs.AdvanceSetup(sHost, SetupPhase.Ideology).State!;
Assert(sIdeo.Phase == SetupPhase.Ideology && SiteIn(sIdeo, sG1) == tileA && SlotsStable(sIdeo), "sites stay after the step");
Expect(6, () => fs.ChooseFactionSite(sG1, tileB));                                     // sites only during site selection
Expect(6, () => fs.Ready(sG1, true));                                                  // done needs an own ideology
byte[] ideoA = { 1, 2, 3 }, ideoB = { 4, 5, 6 };
var firstIdeo = fs.ChooseIdeology(sG1, "Ahnen", ideoA);
Assert(firstIdeo.Forward && firstIdeo.Host == sHost && firstIdeo.Slot == 1 && firstIdeo.Player == sG1Key, "the data goes to the host with the chooser's slot and key");
Assert(firstIdeo.Change.State!.Players.Single(p => p.Id == sG1).Ideology!.SameAs(IdeologySummary.Of("Ahnen", ideoA)), "everyone sees label and hash");
fs.Ready(sG1, true);
var repeat = fs.ChooseIdeology(sG1, "Ahnen", ideoA);
Assert(!repeat.Forward && repeat.Change.State!.Players.Single(p => p.Id == sG1).Ready, "the same ideology again is not forwarded and keeps done");
Assert(fs.ChooseIdeology(sG2, "Ahnen", ideoA).Forward, "two factions may choose the same ideology");
var changed = fs.ChooseIdeology(sG1, "Ahnen", ideoB);
Assert(changed.Forward && !changed.Change.State!.Players.Single(p => p.Id == sG1).Ready, "new data is forwarded and clears done");
fs.ChooseIdeology(sHost, "Host-Ideo", ideoB);
foreach (Guid id in sAll) fs.Ready(id, true);
LobbyView ideoRejected = fs.ClearFactionChoice(sHost, 1, FactionChoiceKind.Ideology, "Daten fehlen beim Host").State!;
Assert(ideoRejected.Players.Single(p => p.Id == sG1).Ideology == null && NoteIn(ideoRejected, sG1) == "Daten fehlen beim Host" &&
    LobbyRegistry.FactionSetupBlockReason(ideoRejected) == "Ideologie fehlt: Guest1", "the host's game rejects an ideology it has no data for");
fs.ChooseIdeology(sG1, "Ahnen", ideoB); fs.Ready(sG1, true);
LobbyView afterLeave38 = fs.Leave(sG2).State!;
Assert(afterLeave38.Players.Count == 2 && afterLeave38.Players.All(p => p.Key != sG2Key) && SlotsStable(afterLeave38),
    "a departed player's faction is gone before the game exists; the others keep their slots");
LobbyView sChars = fs.AdvanceSetup(sHost, SetupPhase.Characters).State!;
Assert(sChars.Phase == SetupPhase.Characters && sChars.Players.All(p => p.Ideology != null && p.FactionSite != null && p.FactionNote == null),
    "with the departed player gone everyone has site and ideology");
fs.SetRoster(sHost, RosterFor(sHostKey, sG1Key)); fs.Ready(sHost, true); fs.Ready(sG1, true);
fs.AdvanceSetup(sHost, SetupPhase.Generating); fs.SetHostGame(sHost, sWorld); fs.ReportGame(sG1, sWorld);
fs.AdvanceSetup(sHost, SetupPhase.Playing);
fs.Leave(sG1); fs.RemoveMember(sG1); fs.AddMember(sG1, "Guest1", sG1Key);
LobbyView sBack = fs.Join(sG1, sRoom).State!;
Assert(sBack.Players.Single(p => p.Id == sG1).FactionSlot == 1 && SiteIn(sBack, sG1) == tileA &&
    sBack.Players.Single(p => p.Id == sG1).Ideology!.SameAs(IdeologySummary.Of("Ahnen", ideoB)), "reconnect: same slot, site and ideology");
LobbyView sRead = LobbyPayloads.ReadState(LobbyPayloads.State(sBack));
Assert(SiteIn(sRead, sG1) == tileA && sRead.Players.Single(p => p.Id == sG1).Ideology!.Label == "Ahnen", "site and ideology survive the state payload");
// Coop keeps its flags and the host's site; the faction choices do not exist there.
var coop38 = new LobbyRegistry(8);
Guid cHost38 = Guid.NewGuid(), cGuest38 = Guid.NewGuid();
coop38.AddMember(cHost38, "Host", PlayerKey.Local(Guid.NewGuid())); coop38.AddMember(cGuest38, "Guest", PlayerKey.Local(Guid.NewGuid()));
coop38.Join(cGuest38, coop38.Create(cHost38, "Coop", 4).State!.Id);
coop38.Ready(cHost38, true); coop38.Ready(cGuest38, true);
foreach (SetupPhase next in new[] { SetupPhase.Scenario, SetupPhase.Storyteller, SetupPhase.WorldParams, SetupPhase.WorldTransfer })
    coop38.AdvanceSetup(cHost38, next);
coop38.SetHostWorld(cHost38, sWorld); coop38.ReportWorld(cGuest38, sWorld); coop38.AdvanceSetup(cHost38, SetupPhase.SiteSelection);
Expect(6, () => coop38.ChooseFactionSite(cGuest38, new SiteTile(1, 0)));
Expect(6, () => coop38.ClearFactionChoice(cHost38, 0, FactionChoiceKind.Site, "x"));
Expect(6, () => coop38.RestoreFactions(cHost38, new FactionRestore(new[] { new FactionRestoreEntry(PlayerKey.Local(Guid.NewGuid()), 0, "Randy") })));
Assert(coop38.SetSiteFlag(cGuest38, new SiteTile(3, 0)).State!.Players.Single(p => p.Id == cGuest38).Flag == new SiteTile(3, 0), "Coop flags still work");
Assert(coop38.DecideSite(cHost38, new SiteTile(3, 0)).State!.Site == new SiteTile(3, 0), "Coop: the host still decides the site");
Expect(6, () => coop38.ChooseIdeology(cGuest38, "Ideo", new byte[] { 1 }));
Console.WriteLine("PASS: faction sites and ideologies: own faction only, no shared site, host's rejection with reason, done rules, stable slots, reconnect, Coop unchanged (ADR 0038)");

// ADR 0038 (SAVE-002, Factions part): continue a saved Factions game.
foreach (bool unlocked in new[] { true, false })
{
    var cont = new LobbyRegistry(8, 2, factionsSetupEnabled: unlocked);
    Guid kHost = Guid.NewGuid(), kGuest = Guid.NewGuid(), kStranger = Guid.NewGuid(), kAbsent = Guid.NewGuid(), kEarly = Guid.NewGuid();
    PlayerKey kHostKey = PlayerKey.Local(Guid.NewGuid()), kGuestKey = PlayerKey.Local(Guid.NewGuid()), kAbsentKey = PlayerKey.Local(Guid.NewGuid());
    cont.AddMember(kHost, "Host", kHostKey); cont.AddMember(kGuest, "Guest", kGuestKey); cont.AddMember(kStranger, "Fremd", PlayerKey.Local(Guid.NewGuid()));
    cont.AddMember(kAbsent, "Später", kAbsentKey); cont.AddMember(kEarly, "Früh", PlayerKey.Local(Guid.NewGuid()));
    Guid kRoom = cont.Create(kHost, "Fortsetzen", 6, LobbyMode.Factions(FactionRelation.Allied)).State!.Id;
    cont.Join(kEarly, kRoom);                                                          // joined before the save was restored
    var table = new FactionRestore(new[] { new FactionRestoreEntry(kHostKey, 0, "Randy"), new FactionRestoreEntry(kGuestKey, 3, "Phoebe"),
        new FactionRestoreEntry(kAbsentKey, 5, "Cassandra") });
    Expect(3, () => cont.RestoreFactions(kEarly, table));                              // only the host restores
    Expect(6, () => cont.RestoreFactions(kHost, new FactionRestore(new[] { new FactionRestoreEntry(kGuestKey, 3, "Phoebe") })));  // host missing
    LobbyView restored = cont.RestoreFactions(kHost, table).State!;
    Assert(restored.Restored && restored.Players.Single(p => p.Id == kHost).FactionSlot == 0 &&
        restored.Players.Single(p => p.Id == kHost).Storyteller == "Randy", "the save's table is in the lobby");
    Assert(LobbyRegistry.FactionSetupBlockReason(restored) == "Kein Platz im Spielstand: Früh", "the reason names who has no faction in the save");
    Expect(6, () => cont.Join(kStranger, kRoom));                                      // not a player of this save
    LobbyView contJoined = cont.Join(kGuest, kRoom).State!;
    Assert(contJoined.Players.Single(p => p.Id == kGuest).FactionSlot == 3 && contJoined.Players.Single(p => p.Id == kGuest).Storyteller == "Phoebe",
        "a player of the save gets their own slot back, gaps and all");
    cont.Leave(kGuest);
    Assert(cont.Join(kGuest, kRoom).State!.Players.Single(p => p.Id == kGuest).FactionSlot == 3, "leaving before the start keeps a saved faction");
    foreach (Guid id in new[] { kHost, kGuest, kEarly }) cont.Ready(id, true);
    Expect(6, () => cont.AdvanceSetup(kHost, SetupPhase.Generating));                  // "Früh" has no faction
    cont.Leave(kEarly);
    Expect(6, () => cont.AdvanceSetup(kHost, SetupPhase.Scenario));                    // a continued game skips the new-game setup
    LobbyView restoredRead = LobbyPayloads.ReadState(LobbyPayloads.State(cont.Ready(kHost, true).State!));
    Assert(restoredRead.Restored && restoredRead.Players.Single(p => p.Key == kGuestKey).FactionSlot == 3, "the restored lobby survives the state payload");
    if (!unlocked) { Expect(6, () => cont.AdvanceSetup(kHost, SetupPhase.Generating)); continue; }   // the Factions lock covers continuing
    LobbyView generating = cont.AdvanceSetup(kHost, SetupPhase.Generating).State!;
    Assert(generating.Phase == SetupPhase.Generating && !generating.Restored && generating.Players.Single(p => p.Id == kGuest).FactionSlot == 3,
        "a continued game goes straight to the game transfer and keeps its slots");
    Expect(6, () => cont.RestoreFactions(kHost, table));                               // only before the start
    cont.SetHostGame(kHost, sWorld); cont.ReportGame(kGuest, sWorld);
    cont.AdvanceSetup(kHost, SetupPhase.Playing);
    Assert(cont.Join(kAbsent, kRoom).State!.Players.Single(p => p.Id == kAbsent).FactionSlot == 5, "an absent player of the save joins the running game later");
    Expect(6, () => cont.Join(kStranger, kRoom));
}
Console.WriteLine("PASS: continue a saved Factions game: host restores the table, only its players join, slots stay, start goes to the game transfer, locked without the switch (ADR 0038)");

// ADR 0027 (SAVE-002, Coop part): continue a saved Coop game; the save's player list admits players, ownership stays in the save.
{
    var cont = new LobbyRegistry(8);
    Guid cHost = Guid.NewGuid(), cGuest = Guid.NewGuid(), cStranger = Guid.NewGuid(), cAbsent = Guid.NewGuid(), cEarly = Guid.NewGuid();
    PlayerKey cHostKey = PlayerKey.Local(Guid.NewGuid()), cGuestKey = PlayerKey.Local(Guid.NewGuid()), cAbsentKey = PlayerKey.Local(Guid.NewGuid());
    cont.AddMember(cHost, "Host", cHostKey); cont.AddMember(cGuest, "Guest", cGuestKey); cont.AddMember(cStranger, "Fremd", PlayerKey.Local(Guid.NewGuid()));
    cont.AddMember(cAbsent, "Später", cAbsentKey); cont.AddMember(cEarly, "Früh", PlayerKey.Local(Guid.NewGuid()));
    Guid cRoom = cont.Create(cHost, "Koop fortsetzen", 6).State!.Id;
    cont.Join(cEarly, cRoom); cont.Ready(cEarly, true);                                   // joined and ready before the save was restored
    var saved = new SessionRestore(new[] { cHostKey, cGuestKey, cAbsentKey });
    Expect(3, () => cont.RestoreSession(cEarly, saved));                                    // only the host restores
    Expect(6, () => cont.RestoreSession(cHost, new SessionRestore(new[] { cGuestKey })));    // host missing
    LobbyView restored = cont.RestoreSession(cHost, saved).State!;
    Assert(restored.Restored && restored.Players.All(p => !p.Ready) && restored.Players.All(p => p.FactionSlot == null), "the lobby continues the save; ready is cleared, no faction data");
    Expect(6, () => cont.Join(cStranger, cRoom));                                           // not a player of this save
    Assert(cont.Join(cGuest, cRoom).State!.Restored, "a player of the save joins");
    cont.Leave(cGuest);
    Assert(cont.Join(cGuest, cRoom).State!.Players.Any(p => p.Id == cGuest), "leaving before the start keeps the seat");
    foreach (Guid id in new[] { cHost, cGuest, cEarly }) cont.Ready(id, true);
    Expect(6, () => cont.AdvanceSetup(cHost, SetupPhase.Generating));                       // "Früh" is not in the save
    cont.Leave(cEarly);
    Expect(6, () => cont.AdvanceSetup(cHost, SetupPhase.Scenario));                         // a continued game skips the new-game setup
    LobbyView restoredRead = LobbyPayloads.ReadState(LobbyPayloads.State(cont.Ready(cHost, true).State!));
    Assert(restoredRead.Restored && restoredRead.Mode.Mode == GameMode.Coop, "the restored Coop lobby survives the state payload");
    LobbyView generating = cont.AdvanceSetup(cHost, SetupPhase.Generating).State!;
    Assert(generating.Phase == SetupPhase.Generating && !generating.Restored, "a continued game goes straight to the game transfer");
    Expect(6, () => cont.RestoreSession(cHost, saved));                                     // only before the start
    cont.SetHostGame(cHost, sWorld); cont.ReportGame(cGuest, sWorld);
    cont.AdvanceSetup(cHost, SetupPhase.Playing);
    Assert(cont.Join(cAbsent, cRoom).State!.Players.Any(p => p.Id == cAbsent), "an absent player of the save joins the running game later");
    // A Factions lobby refuses the Coop list; an aborted continue returns to a normal lobby.
    var fac = new LobbyRegistry(8);
    Guid fHost = Guid.NewGuid(); PlayerKey fHostKey = PlayerKey.Local(Guid.NewGuid());
    fac.AddMember(fHost, "Host", fHostKey);
    fac.Create(fHost, "Fraktionen", 4, LobbyMode.Factions(FactionRelation.Neutral));
    Expect(6, () => fac.RestoreSession(fHost, new SessionRestore(new[] { fHostKey })));
    var ccAgain = new LobbyRegistry(8);
    Guid aHost = Guid.NewGuid(), aGuest = Guid.NewGuid(); PlayerKey aHostKey = PlayerKey.Local(Guid.NewGuid());
    ccAgain.AddMember(aHost, "Host", aHostKey); ccAgain.AddMember(aGuest, "Guest", PlayerKey.Local(Guid.NewGuid()));
    Guid aRoom = ccAgain.Create(aHost, "Nochmal", 4).State!.Id;
    ccAgain.RestoreSession(aHost, new SessionRestore(new[] { aHostKey }));
    Expect(6, () => ccAgain.Join(aGuest, aRoom));
    Assert(!ccAgain.AbortSetup(aHost).State!.Restored && ccAgain.Join(aGuest, aRoom).State!.Players.Count == 2, "abort forgets the save; anyone may join ccAgain");
}
Console.WriteLine("PASS: continue a saved Coop game: host restores the player list, only its players join, start goes to the game transfer, Factions refuse the list, abort forgets it (ADR 0027)");


// ADR 0038: from SiteSelection in the Factions mode, every member chooses an own site and ideology, then the host goes on
// to Characters.
static void ChooseOwnSitesAndIdeologies(LobbyRegistry registry, Guid host, params Guid[] guests)
{
    Guid[] everyone = new[] { host }.Concat(guests).ToArray();
    for (int i = 0; i < everyone.Length; i++) { registry.ChooseFactionSite(everyone[i], new SiteTile(100 + i * 10, 0)); registry.Ready(everyone[i], true); }
    registry.AdvanceSetup(host, SetupPhase.Ideology);
    foreach (Guid id in everyone) { registry.ChooseIdeology(id, "Ideo", new byte[] { 1, 2, 3 }); registry.Ready(id, true); }
    registry.AdvanceSetup(host, SetupPhase.Characters);
}
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
