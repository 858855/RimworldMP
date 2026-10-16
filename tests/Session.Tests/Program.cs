using Multiplayer.Foundation.Protocol;
using Multiplayer.Foundation.Session;

// SYNC-001 (ADR 0024): sequencer, flow control, speed rules, hash comparison, client queue, pacer and seeds.

// Seeds and hashes depend only on their inputs.
ulong session = SessionSeed.FromSessionId("4f1c0c2e9a7b4d3e8f6a5b4c3d2e1f00");
Assert(session == SessionSeed.FromSessionId("4f1c0c2e9a7b4d3e8f6a5b4c3d2e1f00"), "session seed is stable");
Assert(session != SessionSeed.FromSessionId("4f1c0c2e9a7b4d3e8f6a5b4c3d2e1f01"), "session seed differs per session");
Assert(SessionSeed.ForTick(session, 1000) == SessionSeed.ForTick(session, 1000), "tick seed is stable");
Assert(Enumerable.Range(0, 1000).Select(t => SessionSeed.ForTick(session, t)).Distinct().Count() == 1000, "tick seeds differ per tick");
Assert(SessionSeed.ForCommand(session, 1000, 1) != SessionSeed.ForCommand(session, 1000, 2) &&
    SessionSeed.ForCommand(session, 1000, 1) != SessionSeed.ForTick(session, 1000), "command seeds differ per command");
StateHasher Hash(params object[] values)
{
    var hasher = new StateHasher();
    foreach (object value in values)
        switch (value) { case int i: hasher.Add(i); break; case float f: hasher.Add(f); break; case bool b: hasher.Add(b); break; }
    return hasher;
}
Assert(Hash(1, 2, 3.5f).Value == Hash(1, 2, 3.5f).Value, "hash is stable");
Assert(Hash(1, 2).Value != Hash(2, 1).Value, "hash depends on order");
Assert(Hash(0f).Value != Hash(-0f).Value && Hash(1f).Value != Hash(1.0000001f).Value, "floats count by their bits");
Assert(Hash(1).Value != Hash(1, 0).Value && Hash(true).Count == 1, "hash counts values");
Assert(SessionSpeeds.TicksPerSecond(SessionSpeed.Paused) == 0 && SessionSpeeds.TicksPerSecond(SessionSpeed.Normal) == 60 &&
    SessionSpeeds.TicksPerSecond(SessionSpeed.Fast) == 180 && SessionSpeeds.TicksPerSecond(SessionSpeed.Superfast) == 360, "speed table");
Console.WriteLine("PASS: seeds, state hash and speed table");

// Pacer: the session's pace, never past the horizon, faster when behind, no saved time.
{
    var pacer = new TickPacer();
    int ran = 0;
    for (int frame = 0; frame < 120; frame++) ran += pacer.Next(1 / 60.0, 6, 60);
    Assert(ran >= 118 && ran <= 121, "Normal at 60 fps runs about 60 ticks per second (" + ran + ")");
    pacer = new TickPacer(); ran = 0;
    for (int frame = 0; frame < 60; frame++) ran += pacer.Next(1 / 60.0, 1000, 360);
    Assert(ran >= 360, "Superfast runs at least 360 ticks per second");
    Assert(new TickPacer().Next(1, 5, 360) == 5, "never past the horizon");
    Assert(new TickPacer().Next(1 / 60.0, 0, 60) == 0, "nothing without a horizon");
    // behind a paused horizon (a slow player catching up) the game still runs, at least at the normal pace
    pacer = new TickPacer(); ran = 0;
    for (int frame = 0; frame < 30; frame++) ran += pacer.Next(1 / 60.0, 100 - ran, 0);
    Assert(ran >= 30, "catches up behind a paused horizon (" + ran + ")");
    // far behind: the extra lag shrinks quickly
    pacer = new TickPacer(); int behind = 3000;
    for (int frame = 0; frame < 120; frame++) behind -= pacer.Next(1 / 60.0, behind, 60) - 1; // the horizon moves on at 60/s
    Assert(behind < 300, "a lag of 3000 ticks shrinks within two seconds (" + behind + ")");
    // waiting at the horizon saves no time: the first frame after it moves runs about one frame's worth
    pacer = new TickPacer();
    for (int frame = 0; frame < 60; frame++) pacer.Next(1 / 60.0, 0, 60);
    Assert(pacer.Next(1 / 60.0, 6, 60) <= 2, "no burst after waiting");
    Assert(new TickPacer().Next(10, 1000, 60) == new TickPacer().Next(TickPacer.MaximumFrameSeconds, 1000, 60), "a long hitch counts as a quarter second");
}
Console.WriteLine("PASS: tick pacer");

// Client state: runs commands in order at tick − 1; late commands are counted, never run.
{
    PlayerKey key = PlayerKey.Local(Guid.NewGuid());
    LockstepCommand marker = new(LockstepCommandKind.Marker, LockstepCommandKind.MarkerText("m"));
    var state = new LockstepClientState();
    Assert(!state.Started && state.TicksAllowed(100) == 0, "no ticks before the session starts");
    state.OnScheduled(new ScheduledCommand(101, 1, key, marker));
    state.OnScheduled(new ScheduledCommand(101, 2, key, marker));
    state.OnScheduled(new ScheduledCommand(103, 3, key, marker));
    state.OnHorizon(new TickHorizon(100, SessionSpeed.Normal, Guid.Empty, 0, -1));
    Assert(state.TicksAllowed(100) == 0 && state.TicksAllowed(98) == 2, "ticks allowed up to the horizon");
    Assert(state.TakeDue(99) == null, "a command for 101 does not run at 99");
    Assert(state.TakeDue(100)?.Seq == 1 && state.TakeDue(100)?.Seq == 2 && state.TakeDue(100) == null, "both commands for 101 run at 100, in order");
    Assert(state.TakeDue(101) == null && state.TakeDue(102)?.Seq == 3, "the next one at 102");
    state.OnScheduled(new ScheduledCommand(104, 4, key, marker));
    Assert(state.TakeDue(105) == null && state.LateCommands == 1, "a command for a tick already run is late and dropped");
    Throws<ProtocolException>(() => state.OnScheduled(new ScheduledCommand(110, 4, key, marker)));
    Throws<ProtocolException>(() => state.OnScheduled(new ScheduledCommand(103, 9, key, marker)));
    Throws<ProtocolException>(() => state.OnHorizon(new TickHorizon(99, SessionSpeed.Normal, Guid.Empty, 0, -1)));
    state.OnHorizon(new TickHorizon(160, SessionSpeed.Fast, Guid.Empty, 0, 120));
    Assert(state.Pace == SessionSpeed.Fast && state.CheckedTick == 120, "horizon details");
}
Console.WriteLine("PASS: client state");

Guid host = Guid.NewGuid(), guest = Guid.NewGuid(), third = Guid.NewGuid();
PlayerKey hostKey = PlayerKey.Local(Guid.NewGuid()), guestKey = PlayerKey.Steam(76561198000000002), thirdKey = PlayerKey.Local(Guid.NewGuid());
LockstepCommand Speed(SessionSpeed speed) => new(LockstepCommandKind.SetSpeed, LockstepCommandKind.Speed(speed));
LockstepCommand Marker(string text) => new(LockstepCommandKind.Marker, LockstepCommandKind.MarkerText(text));
LockstepSequencer NewSequencer() => new(Guid.NewGuid(), host, new[] { (host, hostKey), (guest, guestKey) });

// Start: waits for every player's first tick, starts at the lowest, paused.
{
    var s = NewSequencer();
    s.ReportTick(host, new ClientTick(1000, 0));
    Assert(s.Step(0).Count == 0 && !s.Started, "waits for every player");
    s.ReportTick(guest, new ClientTick(1000, 0));
    var first = s.Step(50);
    Assert(s.Started && s.Horizon == 1000 && s.Speed == SessionSpeed.Paused, "starts at the reported tick, paused");
    Assert(first.Count == 2 && first.All(m => m.Message.Type == MessageType.TickHorizon) &&
        first.Select(m => m.To).OrderBy(g => g).SequenceEqual(new[] { host, guest }.OrderBy(g => g)), "first horizon to everyone");
    for (long t = 100; t <= 2000; t += 50) s.Step(t);
    Assert(s.Horizon == 1000, "a paused session does not move");
    Assert(s.Step(2050).Count == 2, "an unchanged horizon is repeated once a second");
    Assert(s.Step(2100).Count == 0, "and not more often");
}
Console.WriteLine("PASS: start and pause");

// Commands get the tick after the horizon and come before any horizon that covers them.
{
    var s = NewSequencer();
    s.ReportTick(host, new ClientTick(500, 0)); s.ReportTick(guest, new ClientTick(500, 0)); s.Step(0);
    s.Submit(guest, Speed(SessionSpeed.Normal), 0);
    s.Submit(host, Marker("a"), 0);
    var out1 = s.Step(50);
    var forGuest = out1.Where(m => m.To == guest).Select(m => m.Message).ToList();
    Assert(forGuest.Count == 3 && forGuest[0].Type == MessageType.ScheduledCommand && forGuest[1].Type == MessageType.ScheduledCommand
        && forGuest[2].Type == MessageType.TickHorizon, "commands, then the horizon");
    var c1 = ScheduledCommand.Read(forGuest[0].Payload); var c2 = ScheduledCommand.Read(forGuest[1].Payload);
    Assert(c1.Tick == 501 && c1.Seq == 1 && c1.Issuer == guestKey && LockstepCommandKind.ReadSpeed(c1.Payload) == SessionSpeed.Normal,
        "speed command at h + 1, stamped with the guest's key");
    Assert(c2.Tick == 501 && c2.Seq == 2 && c2.Issuer == hostKey, "second command: same tick, next seq");
    var h1 = TickHorizon.Read(forGuest[2].Payload);
    Assert(h1.Speed == SessionSpeed.Normal && h1.Horizon == 503, "the clock runs from the speed command on (50 ms = 3 ticks)");
    Assert(out1.Where(m => m.To == host).Select(m => m.Message.Type).SequenceEqual(forGuest.Select(m => m.Type)), "same order for the host");
}
Console.WriteLine("PASS: command ticks and order");

// Random interleavings: on every connection each command arrives before a horizon that reaches its tick; every
// player gets the same commands in the same order; the horizon never goes back.
{
    var random = new Random(4711);
    for (int run = 0; run < 200; run++)
    {
        var s = new LockstepSequencer(Guid.NewGuid(), host, new[] { (host, hostKey), (guest, guestKey), (third, thirdKey) });
        var seen = new Dictionary<Guid, (int Horizon, List<(int Tick, uint Seq)> Commands)>
        { [host] = (-1, new()), [guest] = (-1, new()), [third] = (-1, new()) };
        var ticks = new Dictionary<Guid, int> { [host] = 0, [guest] = 0, [third] = 0 };
        foreach (Guid id in ticks.Keys) s.ReportTick(id, new ClientTick(0, 0));
        long now = 0;
        for (int step = 0; step < 200; step++)
        {
            now += random.Next(0, 120);
            int action = random.Next(10);
            Guid who = new[] { host, guest, third }[random.Next(3)];
            if (action < 2) s.Submit(host, Speed((SessionSpeed)random.Next(4)), now); // levels are the host's
            else if (action < 5) s.Submit(who, Marker("x"), now);
            foreach (Guid id in ticks.Keys.ToList())
            {
                // each player runs toward its horizon at its own speed
                ticks[id] = Math.Min(seen[id].Horizon < 0 ? 0 : seen[id].Horizon, ticks[id] + random.Next(0, 40));
                if (random.Next(3) == 0) s.ReportTick(id, new ClientTick(ticks[id], 0));
            }
            foreach (var (to, message) in s.Step(now))
            {
                var view = seen[to];
                if (message.Type == MessageType.TickHorizon)
                {
                    int h = TickHorizon.Read(message.Payload).Horizon;
                    Assert(h >= view.Horizon, "horizon never goes back");
                    seen[to] = (h, view.Commands);
                }
                else if (message.Type == MessageType.ScheduledCommand)
                {
                    var c = ScheduledCommand.Read(message.Payload);
                    Assert(c.Tick > view.Horizon, "command arrives before a horizon that covers its tick");
                    Assert(c.Tick > ticks[to], "no player has run the command's tick yet");
                    view.Commands.Add((c.Tick, c.Seq));
                }
            }
        }
        Assert(seen[host].Commands.SequenceEqual(seen[guest].Commands) && seen[guest].Commands.SequenceEqual(seen[third].Commands),
            "everyone gets the same commands in the same order");
        Assert(seen[host].Commands.Select(c => c.Seq).SequenceEqual(Enumerable.Range(1, seen[host].Commands.Count).Select(i => (uint)i)),
            "sequence numbers count up without gaps");
    }
}
Console.WriteLine("PASS: ordering under random interleavings (200 runs)");

// Flow control: the slowest player sets the pace; a player who leaves no longer counts; held-back time is not made up.
{
    var s = NewSequencer();
    s.ReportTick(host, new ClientTick(0, 0)); s.ReportTick(guest, new ClientTick(0, 0)); s.Step(0);
    s.Submit(host, Speed(SessionSpeed.Normal), 0);
    long now = 0;
    for (int i = 0; i < 20; i++) { now += 50; s.Step(now); s.ReportTick(host, new ClientTick(s.Horizon, 0)); s.ReportTick(guest, new ClientTick(s.Horizon, 0)); }
    Assert(s.Horizon >= 57 && s.Horizon <= 60, "one second at Normal is about 60 ticks (" + s.Horizon + ")");
    int guestStuck = s.Horizon;
    TickHorizon? last = null;
    for (int i = 0; i < 100; i++)
    {
        now += 50;
        foreach (var (to, message) in s.Step(now))
            if (to == host && message.Type == MessageType.TickHorizon) last = TickHorizon.Read(message.Payload);
        s.ReportTick(host, new ClientTick(s.Horizon, 0));
    }
    Assert(s.Horizon == guestStuck + 120, "at most two seconds ahead of the slowest player (" + s.Horizon + ")");
    Assert(last != null && last.WaitingFor == guest && last.WaitingTick == guestStuck, "the horizon says who holds it back");
    s.ReportTick(guest, new ClientTick(s.Horizon, 0));
    int before = s.Horizon;
    now += 50; s.Step(now);
    Assert(s.Horizon - before <= 4 && s.WaitingFor == Guid.Empty, "no burst once the slow player caught up");
    // the guest stops again, then leaves: the host runs on
    for (int i = 0; i < 60; i++) { now += 50; s.Step(now); s.ReportTick(host, new ClientTick(s.Horizon, 0)); }
    int held = s.Horizon;
    s.SetMembers(host, new[] { (host, hostKey) });
    for (int i = 0; i < 20; i++) { now += 50; s.Step(now); s.ReportTick(host, new ClientTick(s.Horizon, 0)); }
    Assert(s.Horizon > held + 50, "a player who left no longer holds back the game");
    // the server never counts a player as ahead of the horizon
    s.ReportTick(host, new ClientTick(int.MaxValue, 0));
    for (int i = 0; i < 4; i++) { now += 50; s.Step(now); }
    Assert(s.Horizon < held + 500, "a report past the horizon is capped");
}
Console.WriteLine("PASS: flow control");

// Speed rules (plan §7.1): anyone pauses and continues, the host picks the level; vanilla's danger brake paces at Normal.
{
    var s = NewSequencer();
    s.ReportTick(host, new ClientTick(0, 0)); s.ReportTick(guest, new ClientTick(0, 0)); s.Step(0);
    List<SessionSpeed> Scheduled(long now) => s.Step(now).Where(m => m.To == host && m.Message.Type == MessageType.ScheduledCommand)
        .Select(m => LockstepCommandKind.ReadSpeed(ScheduledCommand.Read(m.Message.Payload).Payload)).ToList();
    s.Submit(guest, Speed(SessionSpeed.Superfast), 0);
    Assert(Scheduled(50).SequenceEqual(new[] { SessionSpeed.Normal }), "a guest continues at the host's last speed (Normal at first)");
    s.Submit(host, Speed(SessionSpeed.Fast), 100);
    Assert(Scheduled(100).SequenceEqual(new[] { SessionSpeed.Fast }), "the host sets Fast");
    ExpectSession(3, () => s.Submit(guest, Speed(SessionSpeed.Superfast), 150));
    s.Submit(guest, Speed(SessionSpeed.Fast), 150);
    Assert(Scheduled(150).Count == 0, "a request that changes nothing is dropped");
    s.Submit(guest, Speed(SessionSpeed.Paused), 200);
    s.Submit(guest, Speed(SessionSpeed.Normal), 200);
    Assert(Scheduled(200).SequenceEqual(new[] { SessionSpeed.Paused, SessionSpeed.Fast }), "a guest pauses and continues at Fast");
    Throws<ProtocolException>(() => s.Submit(host, new LockstepCommand(LockstepCommandKind.SetSpeed, new byte[] { 4 }), 250));
    s.ReportTick(guest, new ClientTick(s.Horizon, ClientTickFlags.ForcedNormalSpeed));
    s.ReportTick(host, new ClientTick(s.Horizon, 0));
    int before = s.Horizon;
    var paced = s.Step(1250).Where(m => m.To == guest && m.Message.Type == MessageType.TickHorizon).Select(m => TickHorizon.Read(m.Message.Payload)).Single();
    Assert(paced.Speed == SessionSpeed.Normal && s.Speed == SessionSpeed.Fast && s.Horizon - before <= 61,
        "the danger brake paces at Normal while the speed stays Fast (" + (s.Horizon - before) + ")");
}
Console.WriteLine("PASS: speed rules");

// Limits: 30 commands per second and player; unknown players are refused.
{
    var s = NewSequencer();
    for (int i = 0; i < LockstepSequencer.MaximumCommandsPerSecond; i++) s.Submit(guest, Marker("m" + i), 1000);
    ExpectSession(7, () => s.Submit(guest, Marker("too many"), 1500));
    s.Submit(host, Marker("host is not limited by the guest"), 1500);
    s.Submit(guest, Marker("next second"), 2000);
    ExpectSession(6, () => s.Submit(third, Marker("stranger"), 2000));
    ExpectSession(6, () => s.ReportTick(third, new ClientTick(0, 0)));
}
Console.WriteLine("PASS: command limits");

// Hashes: compared once everyone reported; the host is the reference; the first difference is reported once.
{
    var s = new LockstepSequencer(Guid.NewGuid(), host, new[] { (host, hostKey), (guest, guestKey), (third, thirdKey) });
    foreach (Guid id in new[] { host, guest, third }) s.ReportTick(id, new ClientTick(0, 0));
    s.Step(0);
    foreach (Guid id in new[] { host, guest, third }) s.ReportHash(id, new StateHash(0, 77));
    s.ReportHash(host, new StateHash(60, 1)); s.ReportHash(guest, new StateHash(60, 1));
    s.Step(50);
    Assert(s.CheckedTick == 0, "tick 0 matched; tick 60 waits for the third player");
    s.ReportHash(third, new StateHash(60, 1));
    var matched = s.Step(1100).Where(m => m.To == third && m.Message.Type == MessageType.TickHorizon).Select(m => TickHorizon.Read(m.Message.Payload)).Single();
    Assert(s.CheckedTick == 60 && matched.CheckedTick == 60, "the horizon carries the last matching tick");
    s.ReportHash(host, new StateHash(120, 5)); s.ReportHash(guest, new StateHash(120, 6)); s.ReportHash(third, new StateHash(120, 5));
    var desync = s.Step(1150).Where(m => m.Message.Type == MessageType.DesyncDetected).ToList();
    Assert(desync.Count == 3, "every player hears about the desync");
    var detected = DesyncDetected.Read(desync[0].Message.Payload);
    Assert(detected.Tick == 120 && detected.Players.SequenceEqual(new[] { guest }) && s.DesyncReported, "the player who differs from the host");
    s.ReportHash(host, new StateHash(180, 5)); s.ReportHash(guest, new StateHash(180, 6)); s.ReportHash(third, new StateHash(180, 7));
    Assert(!s.Step(1200).Any(m => m.Message.Type == MessageType.DesyncDetected), "reported only once");
    // a player who leaves makes a waiting tick complete
    var t = NewSequencer();
    t.ReportTick(host, new ClientTick(0, 0)); t.ReportTick(guest, new ClientTick(0, 0)); t.Step(0);
    t.ReportHash(host, new StateHash(60, 9));
    t.Step(50);
    Assert(t.CheckedTick == -1, "waits for the guest");
    t.SetMembers(host, new[] { (host, hostKey) });
    t.Step(100);
    Assert(t.CheckedTick == 60, "compared once the guest is gone");
    for (int tick = 61; tick < 61 + LockstepSequencer.MaximumPendingHashTicks + 10; tick++) t.ReportHash(host, new StateHash(tick, 1));
    t.SetMembers(host, new[] { (host, hostKey), (guest, guestKey) }); // a hypothetical late member: hashes wait, but stay bounded
}
Console.WriteLine("PASS: hash comparison");

// All sessions of a server: one per running game; a game that ends is dropped, a new game starts fresh.
{
    var sessions = new LockstepSessions();
    var roomA = new LockstepRoom(Guid.NewGuid(), host, new[] { (host, hostKey) });
    var roomB = new LockstepRoom(Guid.NewGuid(), guest, new[] { (guest, guestKey) });
    sessions.ReportTick(roomA, host, new ClientTick(10, 0));
    sessions.ReportTick(roomB, guest, new ClientTick(20, 0));
    var both = sessions.Step(new[] { roomA, roomB }, 0);
    Assert(sessions.Count == 2 && both.Count == 2 && TickHorizon.Read(both.Single(m => m.To == guest).Message.Payload).Horizon == 20,
        "one sequencer per game");
    sessions.Step(new[] { roomB }, 50);
    Assert(sessions.Count == 1, "a game that is no longer running ends");
    var again = new LockstepRoom(Guid.NewGuid(), host, new[] { (host, hostKey) });
    sessions.ReportTick(again, host, new ClientTick(5, 0));
    var fresh = sessions.Step(new[] { roomB, again }, 100).Where(m => m.To == host).Select(m => TickHorizon.Read(m.Message.Payload)).Single();
    Assert(fresh.Horizon == 5, "a new game in the same lobby starts a new sequencer");
    ExpectSession(6, () => sessions.Submit(roomB, host, Marker("not in this game"), 100));
}
Console.WriteLine("PASS: sessions per server");

static void Assert(bool condition, string name)
{ if (!condition) throw new Exception("FAIL: " + name); }
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("expected " + typeof(T).Name);
}
static void ExpectSession(byte code, Action action)
{
    try { action(); }
    catch (LockstepRuleException ex) when (ex.Code == code) { return; }
    throw new Exception("Expected LockstepRuleException " + code);
}
