using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;
using Multiplayer.Foundation.Session;

// ADR 0030: lockstep coordinator and its game-side counterpart, without RimWorld.
Guid host = Guid.NewGuid(), guest = Guid.NewGuid(), third = Guid.NewGuid();
PlayerKey hostKey = PlayerKey.Steam(1001), guestKey = PlayerKey.Steam(1002), thirdKey = PlayerKey.Steam(1003);
(Guid, PlayerKey)[] members = { (host, hostKey), (guest, guestKey), (third, thirdKey) };

// --- payloads ---
var scheduled = new ScheduledCommand(42, 7, guest, guestKey, ScheduledKind.Game, new byte[] { 1, 2, 3 });
ScheduledCommand back = LockstepPayloads.ReadScheduled(LockstepPayloads.Scheduled(scheduled));
Assert(back.Boundary == 42 && back.Sequence == 7 && back.IssuerSession == guest && back.IssuerKey == guestKey &&
    back.Kind == ScheduledKind.Game && back.Data.SequenceEqual(new byte[] { 1, 2, 3 }), "scheduled command round trip");
Assert(LockstepPayloads.ReadScheduled(LockstepPayloads.Scheduled(new ScheduledCommand(0, 1, Guid.Empty, null, ScheduledKind.Pause, new byte[] { 1 }))).IssuerKey == null,
    "coordinator-issued command has no player key");
TickHorizon horizonBack = LockstepPayloads.ReadHorizon(LockstepPayloads.Horizon(new TickHorizon(600, 2, false, third, 480)));
Assert(horizonBack.Horizon == 600 && horizonBack.Speed == 2 && !horizonBack.Paused && horizonBack.SlowestSession == third && horizonBack.SlowestTick == 480, "horizon round trip");
Expect<ProtocolException>(() => LockstepPayloads.Command(new byte[LockstepPayloads.MaximumCommandBytes + 1]));
Expect<ProtocolException>(() => LockstepPayloads.Command(Array.Empty<byte>()));
Expect<ProtocolException>(() => LockstepPayloads.Control(ScheduledKind.Speed, 4));
Expect<ProtocolException>(() => LockstepPayloads.Control(ScheduledKind.Game, 1));
Expect<ProtocolException>(() => LockstepPayloads.ReadTickPayload(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }));
Assert(LockstepPayloads.ReadDesync(LockstepPayloads.Desync(60, new[] { guest })).Sessions.Single() == guest, "desync round trip");
Console.WriteLine("PASS: lockstep payloads and limits");

// --- coordinator rules ---
var session = new LockstepSession(host, members, 0);
Assert(Horizons(session.Begin()).All(h => h.Horizon == 0 && h.Paused), "session begins paused at tick 0");
var first = Scheduled(session.Submit(guest, new byte[] { 9 }, 0));
Assert(first.Boundary == 0 && first.Sequence == 1 && first.IssuerKey == guestKey, "command in pause gets boundary 0 and the stamped issuer");
Assert(session.Advance(5000).Count == 0 && session.Horizon == 0, "paused: horizon does not move");
Expect<LockstepRuleException>(() => session.Control(guest, ScheduledKind.Speed, 2, 0), 3);
session.Control(guest, ScheduledKind.Pause, 0, 1000);                                  // anyone may resume
Assert(!session.Paused, "guest resumed");
session.Advance(2000);                                                                 // 1 s at 60 ticks/s
Assert(session.Horizon == 60, "horizon follows the clock: " + session.Horizon);
session.Advance(10_000);                                                               // nobody reported: lead limit
Assert(session.Horizon == 120, "slowest game limits the horizon to its tick + 2 s: " + session.Horizon);
Expect<LockstepRuleException>(() => session.ReportTick(guest, 121, 10_000), 1);        // beyond the horizon
foreach (Guid p in new[] { host, guest, third }) session.ReportTick(p, 120, 10_000);
session.Advance(10_500);
Assert(session.Horizon == 150, "clock rebased while limited: no burst after catching up: " + session.Horizon);
session.Control(host, ScheduledKind.Speed, 3, 10_500);
session.Advance(11_000);
Assert(session.Horizon == 330, "speed 3 runs at 360 ticks/s: " + session.Horizon);
var burst = new LockstepSession(host, members, 0);
for (int i = 0; i < 60; i++) burst.Submit(guest, new byte[] { 1 }, 0);
Expect<LockstepRuleException>(() => burst.Submit(guest, new byte[] { 1 }, 0), 7);
burst.Submit(guest, new byte[] { 1 }, 100);                                            // refilled 3 tokens after 100 ms
Console.WriteLine("PASS: coordinator: pause, resume, speed right, clock, flow control with rebase, rate limit");

var hashes = new LockstepSession(host, members, 0);
hashes.Control(host, ScheduledKind.Pause, 0, 0); hashes.Advance(2000);
Assert(hashes.ReportHash(host, 60, 5).Count == 0 && hashes.ReportHash(guest, 60, 5).Count == 0, "incomplete: nothing yet");
Assert(hashes.ReportHash(third, 60, 5).Count == 0, "all equal: no alarm");
Expect<LockstepRuleException>(() => hashes.ReportHash(host, 61, 5), 1);
hashes.Advance(3000);
hashes.ReportHash(host, 120, 7); hashes.ReportHash(third, 120, 7);
var alarm = hashes.ReportHash(guest, 120, 8);
var desync = alarm.Where(o => o.Message.Type == MessageType.DesyncDetected).Select(o => LockstepPayloads.ReadDesync(o.Message.Payload)).ToList();
Assert(desync.Count == 3 && desync.All(d => d.Tick == 120 && d.Sessions.Single() == guest), "desync reported to everyone, naming the guest");
Assert(alarm.Any(o => o.Message.Type == MessageType.ScheduledCommand &&
    LockstepPayloads.ReadScheduled(o.Message.Payload) is { Kind: ScheduledKind.Pause, IssuerSession: var s } && s == Guid.Empty) && hashes.Paused,
    "desync stops time for everyone at one shared point");
Assert(hashes.ReportHash(guest, 60, 99).Count == 0, "late report for a compared tick is ignored");
var leaving = new LockstepSession(host, members, 0);
leaving.Control(host, ScheduledKind.Pause, 0, 0); leaving.Advance(2000);
leaving.ReportHash(host, 60, 1); leaving.ReportHash(guest, 60, 1);
Assert(leaving.RemoveParticipant(third).Count == 0 && leaving.Participants.Count == 2, "a leaving game completes the comparison");
leaving.RemoveParticipant(host);
Assert(leaving.Ended, "host leaving ends the session");
Expect<LockstepRuleException>(() => leaving.Submit(guest, new byte[] { 1 }, 0), 6);
Console.WriteLine("PASS: desync detection against the host, shared pause, late reports, leaving players");

// --- game side guards ---
var client = new LockstepClient();
client.Receive(H(0, paused: true));
Assert(!client.MayRunTick(0), "nothing runs before the commands for tick 0 were taken");
client.TakeCommandsAt(0);
Assert(!client.MayRunTick(0), "horizon 0: tick 1 may not run");
client.Receive(S(0, 1, ScheduledKind.Pause, 0));
Assert(!client.MayRunTick(0), "a command arrived for this point: take it first");
Assert(client.TakeCommandsAt(0).Count == 0 && !client.Paused, "pause command applied, not returned");
client.Receive(H(5));
Assert(client.MayRunTick(0), "within the horizon and nothing pending: may run");
Expect<ProtocolException>(() => client.Receive(S(3, 3, ScheduledKind.Game, 1)));       // sequence gap
for (int tick = 1; tick <= 5; tick++) { client.TakeCommandsAt(tick - 1); client.TakeCommandsAt(tick); }
// Negative control: a command for tick 2 arriving after the game ran past it would desync; the client refuses it.
Expect<ProtocolException>(() => client.Receive(S(2, 2, ScheduledKind.Game, 1)));
Expect<ProtocolException>(() => client.Receive(H(4)));                                 // horizon moved back
Console.WriteLine("PASS: game-side guards: take before run, pause in place, sequence gap, passed boundary, shrinking horizon");

// --- the main claim: every game executes exactly the same commands at exactly the same ticks ---
SimResult clean = Simulate(seed: 1, corruptAt: -1);
Assert(clean.Logs.Values.All(log => log.Count > 50), "plenty of commands executed: " + string.Join(",", clean.Logs.Values.Select(l => l.Count)));
Assert(clean.Completed.Values.Min() >= 900, "games made progress: " + string.Join(",", clean.Completed.Values));
AssertSameOrder(clean);
Assert(clean.MaxSpread <= 720 + 60, "the slow game held the others back: spread " + clean.MaxSpread);
Assert(clean.Desyncs == 0, "no desync in a clean run");
for (int seed = 2; seed <= 6; seed++) AssertSameOrder(Simulate(seed, corruptAt: -1));
Console.WriteLine($"PASS: 6 simulated runs (3 games, one 5x slower, random latency, commands, pauses, speeds): identical execution order everywhere; e.g. {clean.Logs.Values.First().Count} commands up to tick {clean.Completed.Values.Min()}");

SimResult corrupted = Simulate(seed: 1, corruptAt: 600);
Assert(corrupted.Desyncs == 3, "every game learned about the desync");
Assert(corrupted.PausedAt.Values.Distinct().Count() == 1 && corrupted.PausedAt.Values.All(t => t >= 600),
    "all games stopped at the same tick: " + string.Join(",", corrupted.PausedAt.Values));
AssertSameOrder(corrupted);
Console.WriteLine($"PASS: corrupted state in one game detected at tick 600; all games stopped together at tick {corrupted.PausedAt.Values.First()}");

// --- SAVE-001 / JOIN-001 (ADR 0032) ---
var joinBack = LockstepPayloads.ReadJoin(LockstepPayloads.Join(new JoinSnapshot(120, 9, host, 150, 2, true)));
Assert(joinBack.Boundary == 120 && joinBack.Sequence == 9 && joinBack.Host == host && joinBack.Horizon == 150 && joinBack.Speed == 2 && joinBack.Paused,
    "join snapshot round trip");
Expect<ProtocolException>(() => LockstepPayloads.Control(ScheduledKind.Save, 1));
var saving = new LockstepSession(host, members, 0, new LockstepOptions { AutosaveIntervalTicks = 120 });
Expect<LockstepRuleException>(() => saving.Control(guest, ScheduledKind.Save, 0, 0), 3);
var manual = Scheduled(saving.Control(host, ScheduledKind.Save, 0, 0));
Assert(manual.Kind == ScheduledKind.Save && manual.Data.Length == 0 && manual.Boundary == 0, "host saves on demand at the current point");
saving.Control(host, ScheduledKind.Pause, 0, 0);
foreach (Guid p in new[] { host, guest, third }) saving.ReportTick(p, 0, 0);
var autosaves = new List<ScheduledCommand>();
for (long now = 50; now <= 6000; now += 50)
{
    var round = new List<(Guid To, WireMessage Message)>();
    foreach (Guid p in new[] { host, guest, third }) round.AddRange(saving.ReportTick(p, saving.Horizon, now));
    round.AddRange(saving.Advance(now));
    autosaves.AddRange(round.Where(o => o.To == host && o.Message.Type == MessageType.ScheduledCommand)
        .Select(o => LockstepPayloads.ReadScheduled(o.Message.Payload)).Where(c => c.Kind == ScheduledKind.Save));
}
Assert(autosaves.Count >= 2 && autosaves.All(c => c.IssuerSession == Guid.Empty && c.Data.Length == 0) &&
    autosaves.Zip(autosaves.Skip(1)).All(pair => pair.Second.Boundary - pair.First.Boundary >= 120),
    "autosave once per interval, issued by the coordinator: " + string.Join(",", autosaves.Select(c => c.Boundary)));
Console.WriteLine("PASS: coordinated save: host on demand, guests refused, autosave once per interval");

Guid late = Guid.NewGuid(); PlayerKey lateKey = PlayerKey.Steam(1004);
var joining = new LockstepSession(host, members, 0);
joining.Control(host, ScheduledKind.Pause, 0, 0);
joining.Advance(1000);
int before = joining.Horizon;
var joinOut = joining.AddJoiner(late, lateKey, 1000);
var snapshotCommand = joinOut.Where(o => o.Message.Type == MessageType.ScheduledCommand).Select(o => (o.To, Command: LockstepPayloads.ReadScheduled(o.Message.Payload))).ToList();
Assert(snapshotCommand.Count == 3 && snapshotCommand.All(c => c.To != late && c.Command.Kind == ScheduledKind.Save && new Guid(c.Command.Data) == late),
    "snapshot command goes to the existing games only and names the joiner");
JoinSnapshot start = LockstepPayloads.ReadJoin(joinOut.Single(o => o.To == late && o.Message.Type == MessageType.JoinSnapshot).Message.Payload);
Assert(start.Boundary == before && start.Sequence == snapshotCommand[0].Command.Sequence && start.Host == host, "joiner learns where the snapshot was taken");
Assert(joinOut.Any(o => o.To == late && o.Message.Type == MessageType.TickHorizon), "joiner gets the current horizon");
Assert(!joining.ActiveParticipants.Contains(late), "joiner does not slow the others while catching up");
var nextCommand = joining.Submit(guest, new byte[] { 5 }, 1000);
Assert(nextCommand.Any(o => o.To == late) && LockstepPayloads.ReadScheduled(nextCommand.First(o => o.To == late).Message.Payload).Sequence == start.Sequence + 1,
    "joiner receives every command from the next sequence on");
Expect<LockstepRuleException>(() => joining.AddJoiner(late, lateKey, 1000), 6);
foreach (Guid p in new[] { host, guest, third }) joining.ReportTick(p, before, 1000);
var synced = joining.ReportTick(late, before, 1100);
Assert(synced.Count(o => o.Message.Type == MessageType.PlayerSynced) == 4 && joining.ActiveParticipants.Contains(late), "caught-up joiner goes live, everyone is told");
var lateClient = LockstepClient.ForJoin(start);
Assert(!lateClient.MayRunTick(start.Boundary) || lateClient.Horizon == start.Boundary, "joiner starts at the snapshot point");
lateClient.Receive(nextCommand.First(o => o.To == late).Message);
Assert(lateClient.TakeCommandsAt(start.Boundary).Single().Data[0] == 5, "joiner runs the next command at the same point as everyone");
// Negative control: a joiner starting one sequence too early or too late is caught at the next command, not silently off.
foreach (ulong wrong in new[] { start.Sequence - 1, start.Sequence + 1 })
{
    var offByOne = LockstepClient.ForJoin(new JoinSnapshot(start.Boundary, wrong, host, start.Horizon, start.Speed, start.Paused));
    Expect<ProtocolException>(() => offByOne.Receive(nextCommand.First(o => o.To == late).Message));
}
Console.WriteLine("PASS: join mid-game: snapshot to existing games only, joiner continues at the next sequence, not counted until caught up; off-by-one start refused");

SimResult withJoin = SimulateJoin(seed: 11);
AssertSameOrder(withJoin);
Assert(withJoin.Desyncs == 0, "joiner's hashes match after catching up");
Assert(withJoin.Completed.Count == 4 && withJoin.Completed.Values.Min() >= 900, "all four games progressed: " + string.Join(",", withJoin.Completed.Values));
for (int seed = 12; seed <= 15; seed++) { var run = SimulateJoin(seed); AssertSameOrder(run); Assert(run.Desyncs == 0, "no desync with a joiner"); }
Console.WriteLine($"PASS: 5 simulated runs with a player joining mid-game: after loading the host's snapshot the joiner executes exactly the same order; {withJoin.Logs.Values.First().Count} commands, no desync");

// Like Simulate, but a fourth game joins at 6 s: the host saves at the snapshot point, the joiner loads that state,
// catches up twice as fast and must then match everyone.
static SimResult SimulateJoin(int seed)
{
    var random = new Random(seed);
    Guid[] ids = { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
    Guid joiner = ids[3];
    double[] ticksPerMs = { 1.0, 0.8, 0.5, 2.0 };
    var coordinator = new LockstepSession(ids[0], ids.Take(3).Select((id, i) => (id, PlayerKey.Steam((ulong)(i + 1)))), 0);
    var games = ids.ToDictionary(id => id, _ => new LockstepClient());
    var down = ids.ToDictionary(id => id, _ => new Link());
    var up = ids.ToDictionary(id => id, _ => new Link());
    var completed = ids.ToDictionary(id => id, _ => 0);
    var budget = ids.ToDictionary(id => id, _ => 0.0);
    var hashed = ids.ToDictionary(id => id, _ => -1);
    var state = ids.ToDictionary(id => id, _ => 14695981039346656037UL);
    var logs = ids.ToDictionary(id => id, _ => new List<(int Boundary, ulong Sequence)>());
    bool joined = false, loaded = false;
    JoinSnapshot? start = null;
    (long At, int Tick, ulong State, List<(int, ulong)> Log)? snapshot = null;
    int desyncs = 0;
    void Out(IEnumerable<(Guid To, WireMessage Message)> messages, long now)
    { foreach (var (to, message) in messages) down[to].Send(message, now + random.Next(5, 80)); }
    Out(coordinator.Begin(), 0);
    up[ids[1]].Send(LockstepClient.ControlMessage(ScheduledKind.Pause, 0), 20);
    for (long now = 0; now < 20_000; now += 5)
    {
        if (now == 6000) { Out(coordinator.AddJoiner(joiner, PlayerKey.Steam(4), now), now); joined = true; }
        foreach (Guid id in ids)
            while (up[id].TryReceive(now, out WireMessage? m))
            {
                try
                {
                    Out(m!.Type switch
                    {
                        MessageType.Command => coordinator.Submit(id, m.Payload, now),
                        MessageType.ClientTick => coordinator.ReportTick(id, LockstepPayloads.ReadTickPayload(m.Payload), now),
                        MessageType.StateHash => coordinator.ReportHash(id, LockstepPayloads.ReadHash(m.Payload).Tick, LockstepPayloads.ReadHash(m.Payload).Hash),
                        MessageType.SessionControl => coordinator.Control(id, LockstepPayloads.ReadControl(m.Payload).Kind, LockstepPayloads.ReadControl(m.Payload).Value, now),
                        _ => throw new Exception("unexpected")
                    }, now);
                }
                catch (LockstepRuleException error) when (error.Code is 3 or 7) { }
            }
        if (now % 50 == 0) Out(coordinator.Advance(now), now);
        foreach (Guid id in ids)
        {
            if (id == joiner)
            {
                if (!joined) continue;
                if (start == null)
                {
                    if (!down[id].TryReceive(now, out WireMessage? first)) continue;
                    if (first!.Type != MessageType.JoinSnapshot) throw new Exception("FAIL: joiner's first message is not JoinSnapshot");
                    start = LockstepPayloads.ReadJoin(first.Payload);
                }
                if (!loaded)
                {
                    if (snapshot == null || snapshot.Value.At > now) continue;          // chunk transfer still running
                    games[id] = LockstepClient.ForJoin(start);
                    completed[id] = snapshot.Value.Tick; state[id] = snapshot.Value.State;
                    logs[id] = new List<(int, ulong)>(snapshot.Value.Log);
                    hashed[id] = snapshot.Value.Tick;   // the snapshot is mid-tick: never report the hash of that tick
                    loaded = true;
                }
            }
            LockstepClient game = games[id];
            while (down[id].TryReceive(now, out WireMessage? m))
            {
                if (m!.Type == MessageType.PlayerSynced || m.Type == MessageType.JoinSnapshot) continue;
                game.Receive(m);
                if (m.Type == MessageType.DesyncDetected) desyncs++;
            }
            void Execute()
            {
                foreach (ScheduledCommand command in game.TakeCommandsAt(completed[id]))
                {
                    if (command.Kind == ScheduledKind.Save)
                    {
                        // The host's state right here is the joiner's snapshot (later commands of this tick excluded).
                        if (id == ids[0] && command.Data.Length == 16 && new Guid(command.Data) == joiner)
                            snapshot = (now + 400, completed[id], state[id], new List<(int, ulong)>(logs[id]));
                        continue;
                    }
                    logs[id].Add((command.Boundary, command.Sequence));
                    state[id] = (state[id] ^ command.Sequence ^ command.Data[0]) * 1099511628211UL;
                }
            }
            void Hash()
            {
                int t = completed[id];
                if (t % 60 != 0 || t <= hashed[id]) return;
                hashed[id] = t;
                up[id].Send(LockstepClient.HashMessage(t, state[id] ^ (ulong)t), now + random.Next(5, 80));
            }
            Hash(); Execute();
            budget[id] = Math.Min(budget[id] + ticksPerMs[Array.IndexOf(ids, id)] * 5, 2000);
            while (budget[id] >= 1 && game.MayRunTick(completed[id])) { completed[id]++; budget[id]--; Hash(); Execute(); }
            if (!game.MayRunTick(completed[id])) budget[id] = Math.Min(budget[id], 1);
            if (game.ProgressMessage(completed[id], now) is WireMessage progress) up[id].Send(progress, now + random.Next(5, 80));
            if (random.Next(100) < 3) up[id].Send(LockstepClient.CommandMessage(new[] { (byte)random.Next(256) }), now + random.Next(5, 80));
        }
    }
    if (!coordinator.ActiveParticipants.Contains(joiner)) throw new Exception("FAIL: joiner never went live");
    return new SimResult(logs, completed, new Dictionary<Guid, int>(), desyncs, 0);
}

static void AssertSameOrder(SimResult run)
{
    int common = run.Completed.Values.Min();
    var reference = run.Logs.Values.First().Where(e => e.Boundary < common).ToList();
    foreach (var log in run.Logs.Values)
        Assert(log.Where(e => e.Boundary < common).SequenceEqual(reference), "identical execution order up to tick " + common);
}

// Virtual-time simulation: coordinator + games with per-direction FIFO links of random latency.
static SimResult Simulate(int seed, int corruptAt)
{
    var random = new Random(seed);
    Guid[] ids = { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
    double[] ticksPerMs = { 1.0, 0.8, 0.2 };                                           // third game is 5x slower
    var coordinator = new LockstepSession(ids[0], ids.Select((id, i) => (id, PlayerKey.Steam((ulong)(i + 1)))), 0);
    var games = ids.ToDictionary(id => id, _ => new LockstepClient());
    var down = ids.ToDictionary(id => id, _ => new Link());
    var up = ids.ToDictionary(id => id, _ => new Link());
    var completed = ids.ToDictionary(id => id, _ => 0);
    var budget = ids.ToDictionary(id => id, _ => 0.0);
    var hashed = ids.ToDictionary(id => id, _ => -1);
    var state = ids.ToDictionary(id => id, _ => 14695981039346656037UL);               // FNV over executed commands
    var logs = ids.ToDictionary(id => id, _ => new List<(int Boundary, ulong Sequence)>());
    var pausedAt = new Dictionary<Guid, int>();
    int desyncs = 0, maxSpread = 0;
    void Out(IEnumerable<(Guid To, WireMessage Message)> messages, long now)
    { foreach (var (to, message) in messages) down[to].Send(message, now + random.Next(5, 80)); }
    Out(coordinator.Begin(), 0);
    for (long now = 0; now < 20_000; now += 5)
    {
        foreach (Guid id in ids)                                                       // coordinator side
            while (up[id].TryReceive(now, out WireMessage? m))
            {
                try
                {
                    Out(m!.Type switch
                    {
                        MessageType.Command => coordinator.Submit(id, m.Payload, now),
                        MessageType.ClientTick => coordinator.ReportTick(id, LockstepPayloads.ReadTickPayload(m.Payload), now),
                        MessageType.StateHash => coordinator.ReportHash(id, LockstepPayloads.ReadHash(m.Payload).Tick, LockstepPayloads.ReadHash(m.Payload).Hash),
                        MessageType.SessionControl => coordinator.Control(id, LockstepPayloads.ReadControl(m.Payload).Kind, LockstepPayloads.ReadControl(m.Payload).Value, now),
                        _ => throw new Exception("unexpected")
                    }, now);
                }
                catch (LockstepRuleException error) when (error.Code is 3 or 7) { }    // refused speed or rate: like a real error reply
            }
        if (now % 50 == 0) Out(coordinator.Advance(now), now);
        foreach (Guid id in ids)                                                       // game side
        {
            LockstepClient game = games[id];
            while (down[id].TryReceive(now, out WireMessage? m))
            {
                game.Receive(m!);
                if (m!.Type == MessageType.DesyncDetected) desyncs++;
            }
            void Execute()
            {
                foreach (ScheduledCommand command in game.TakeCommandsAt(completed[id]))
                {
                    logs[id].Add((command.Boundary, command.Sequence));
                    state[id] = (state[id] ^ command.Sequence ^ command.Data[0]) * 1099511628211UL;
                }
                if (game.Paused && !pausedAt.ContainsKey(id) && desyncs > 0) pausedAt[id] = completed[id];
            }
            void Hash()
            {
                int t = completed[id];
                if (t % 60 != 0 || t <= hashed[id]) return;
                hashed[id] = t;
                ulong value = state[id] ^ (ulong)t;
                if (t == corruptAt && id == ids[2]) value++;                           // one game's state diverges
                up[id].Send(LockstepClient.HashMessage(t, value), now + random.Next(5, 80));
            }
            Hash(); Execute();
            budget[id] = Math.Min(budget[id] + ticksPerMs[Array.IndexOf(ids, id)] * 5, 400);
            while (budget[id] >= 1 && game.MayRunTick(completed[id]))
            {
                completed[id]++; budget[id]--;
                Hash(); Execute();
            }
            if (!game.MayRunTick(completed[id])) budget[id] = Math.Min(budget[id], 1);
            if (game.ProgressMessage(completed[id], now) is WireMessage progress) up[id].Send(progress, now + random.Next(5, 80));
            if (random.Next(100) < 3) up[id].Send(LockstepClient.CommandMessage(new[] { (byte)random.Next(256) }), now + random.Next(5, 80));
        }
        // Players pause, resume and change speed while playing; the guest's speed attempts are refused.
        if (now == 300) up[ids[1]].Send(LockstepClient.ControlMessage(ScheduledKind.Pause, 0), now + 20);
        if (now == 4000) up[ids[2]].Send(LockstepClient.ControlMessage(ScheduledKind.Pause, 1), now + 20);
        if (now == 4600) up[ids[1]].Send(LockstepClient.ControlMessage(ScheduledKind.Pause, 0), now + 20);
        if (now == 7000) up[ids[0]].Send(LockstepClient.ControlMessage(ScheduledKind.Speed, 3), now + 20);
        if (now == 7500) up[ids[1]].Send(LockstepClient.ControlMessage(ScheduledKind.Speed, 1), now + 20);
        if (now == 12_000) up[ids[0]].Send(LockstepClient.ControlMessage(ScheduledKind.Speed, 2), now + 20);
        maxSpread = Math.Max(maxSpread, completed.Values.Max() - completed.Values.Min());
    }
    return new SimResult(logs, completed, pausedAt, desyncs, maxSpread);
}

static ScheduledCommand Scheduled(IReadOnlyList<(Guid To, WireMessage Message)> outgoing) =>
    LockstepPayloads.ReadScheduled(outgoing.First(o => o.Message.Type == MessageType.ScheduledCommand).Message.Payload);
static IEnumerable<TickHorizon> Horizons(IReadOnlyList<(Guid To, WireMessage Message)> outgoing) =>
    outgoing.Where(o => o.Message.Type == MessageType.TickHorizon).Select(o => LockstepPayloads.ReadHorizon(o.Message.Payload));
static WireMessage H(int horizon, bool paused = false) =>
    new(MessageType.TickHorizon, 0, LockstepPayloads.Horizon(new TickHorizon(horizon, 1, paused, Guid.Empty, 0)));
static WireMessage S(int boundary, ulong sequence, ScheduledKind kind, byte value) =>
    new(MessageType.ScheduledCommand, 0, LockstepPayloads.Scheduled(new ScheduledCommand(boundary, sequence, Guid.Empty, null, kind, new[] { value })));
static void Assert(bool condition, string label) { if (!condition) throw new Exception("FAIL: " + label); }
static void Expect<T>(Action action, byte? code = null) where T : Exception
{
    try { action(); }
    catch (T error)
    {
        if (code != null && error is LockstepRuleException rule && rule.Code != code) throw new Exception($"FAIL: expected code {code}, got {rule.Code}");
        return;
    }
    throw new Exception("FAIL: expected " + typeof(T).Name);
}

record SimResult(Dictionary<Guid, List<(int Boundary, ulong Sequence)>> Logs, Dictionary<Guid, int> Completed,
    Dictionary<Guid, int> PausedAt, int Desyncs, int MaxSpread);

// FIFO link with latency: a later message never overtakes an earlier one on the same link, like TCP or Steam reliable.
sealed class Link
{
    private readonly Queue<(long At, WireMessage Message)> queue = new();
    private long last;
    public void Send(WireMessage message, long at) { last = Math.Max(last, at); queue.Enqueue((last, message)); }
    public bool TryReceive(long now, out WireMessage? message)
    {
        if (queue.Count > 0 && queue.Peek().At <= now) { message = queue.Dequeue().Message; return true; }
        message = null; return false;
    }
}
