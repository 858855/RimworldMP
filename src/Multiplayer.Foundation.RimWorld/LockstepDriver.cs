using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Multiplayer.Foundation.Protocol;
using Multiplayer.Foundation.Session;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SYNC-001 (ADR 0024): runs the shared game's ticks in lockstep. Called once per frame from the TickManagerUpdate
    /// prefix; vanilla's own loop cannot tick while it exists (<see cref="SessionPauseTickPatch"/>). Each frame it runs
    /// the commands for the next tick, then as many ticks as <see cref="TickPacer"/> allows, never past the server's
    /// horizon. Every tick runs with the session's random seed for that tick. Speed changes from vanilla's controls become
    /// requests to the server; the speed only changes when the server's command runs.
    /// </summary>
    internal sealed class LockstepDriver
    {
        internal const int HashInterval = 60;
        private const long FrameBudgetMilliseconds = 25;
        private const float ReportSeconds = 0.1f;
        private const int KeptSummaries = 8;

        /// <summary>Set only around the driver's own calls; the TickManager patches let exactly these through.</summary>
        internal static bool Ticking, ApplyingSpeed;

        private readonly ulong sessionSeed;
        private readonly Action<MessageType, byte[]> send;
        private readonly Func<PlayerKey, string> nameOf;
        private readonly TickPacer pacer = new TickPacer();
        private readonly Stopwatch frameWatch = new Stopwatch();
        private readonly List<(int Tick, string Summary)> summaries = new List<(int, string)>();
        private bool started, previousRunInBackground, startHashSent;
        private int reportedTick = -1;
        private ClientTickFlags reportedFlags;
        private float nextReportAt, lastRequestAt = -10f;
        private SessionSpeed lastRequested;

        internal LockstepDriver(Game game, ulong sessionSeed, bool isHost, Action<MessageType, byte[]> send, Func<PlayerKey, string> nameOf)
        {
            Game = game; this.sessionSeed = sessionSeed; IsHost = isHost; this.send = send; this.nameOf = nameOf;
        }

        internal Game Game { get; }
        internal bool IsHost { get; }
        internal LockstepClientState State { get; } = new LockstepClientState();
        /// <summary>The speed in effect at the game's current tick (from the last speed command that ran).</summary>
        internal SessionSpeed Speed { get; private set; } = SessionSpeed.Paused;
        internal SessionSpeed LastRunning { get; private set; } = SessionSpeed.Normal;
        internal int TicksRun { get; private set; }
        internal int CommandsRun { get; private set; }
        internal int LastHashTick { get; private set; } = -1;
        internal string LastMarker { get; private set; } = "";
        internal int? DesyncTick { get; private set; }
        internal int MissedCommands => State.LateCommands;

        // Self-test hooks: run without the per-tick seed (the negative control), random draws between ticks like UI code,
        // and a report of each command with the tick it ran at.
        internal bool ReseedRand = true;
        // Self-test diagnosis (-mpLockstepTicksPerFrame=N): a fixed number of ticks per frame instead of the pacer, to tell
        // frame-dependent drift from other drift.
        internal int FixedTicksPerFrame;
        internal Action? BetweenTicks;
        /// <summary>Self-test: called right after each tick, still under the tick's random seed (its stream position counts the draws).</summary>
        internal Action<int>? AfterTick;
        internal Action<ScheduledCommand, int>? CommandRan;

        internal void Start()
        {
            if (started) return;
            started = true;
            // An unfocused game would stop updating and hold everyone back (the slowest sets the pace).
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            ApplySpeed(Game.tickManager);
            Report(Game.tickManager, true);
        }

        internal void Stop()
        {
            if (!started) return;
            started = false;
            Application.runInBackground = previousRunInBackground;
        }

        internal void Update(TickManager tickManager)
        {
            if (!started || !ReferenceEquals(Current.Game, Game) || !ReferenceEquals(tickManager, Game.tickManager)) return;
            if (!startHashSent) { SendHash(tickManager.TicksGame); startHashSent = true; }
            ApplySpeed(tickManager);
            ExecuteDue(tickManager);
            int allowed = State.TicksAllowed(tickManager.TicksGame);
            int planned = FixedTicksPerFrame > 0 ? Math.Min(FixedTicksPerFrame, allowed)
                : pacer.Next(Time.unscaledDeltaTime, allowed, SessionSpeeds.TicksPerSecond(State.Pace));
            frameWatch.Restart();
            for (int i = 0; i < planned; i++)
            {
                if (LongEventHandler.AnyEventNowOrWaiting || !ReferenceEquals(Current.Game, Game)) break;
                RunTick(tickManager);
                if (tickManager.TicksGame % HashInterval == 0) SendHash(tickManager.TicksGame);
                ExecuteDue(tickManager);
                BetweenTicks?.Invoke();
                if (FixedTicksPerFrame == 0 && frameWatch.ElapsedMilliseconds >= FrameBudgetMilliseconds) break;
            }
            Report(tickManager, false);
        }

        /// <summary>Vanilla's speed buttons and keys, and its auto-pause, land here instead of changing the speed.</summary>
        internal void RequestSpeed(TimeSpeed wanted)
        {
            SessionSpeed? speed = FromTimeSpeed(wanted);
            if (speed == null) { Messages.Message("Ultraschnell gibt es im Mehrspieler nicht.", MessageTypeDefOf.RejectInput, false); return; }
            if (!IsHost && speed.Value != SessionSpeed.Paused && Speed != SessionSpeed.Paused && speed.Value != Speed)
            { Messages.Message("Nur der Host ändert die Geschwindigkeit. Pausieren und Fortsetzen darf jeder.", MessageTypeDefOf.RejectInput, false); return; }
            // one click or key press, one request
            if (speed.Value == lastRequested && Time.realtimeSinceStartup - lastRequestAt < 0.25f) return;
            lastRequested = speed.Value; lastRequestAt = Time.realtimeSinceStartup;
            send(MessageType.LockstepCommand, new LockstepCommand(LockstepCommandKind.SetSpeed, LockstepCommandKind.Speed(speed.Value)).ToPayload());
        }

        internal void RequestTogglePause() => RequestSpeed(Speed == SessionSpeed.Paused ? ToTimeSpeed(LastRunning) : TimeSpeed.Paused);

        internal void SendMarker(string text) =>
            send(MessageType.LockstepCommand, new LockstepCommand(LockstepCommandKind.Marker, LockstepCommandKind.MarkerText(text)).ToPayload());

        internal void OnDesync(DesyncDetected desync, string who)
        {
            if (DesyncTick != null) return;
            DesyncTick = desync.Tick;
            string own = summaries.FirstOrDefault(s => s.Tick == desync.Tick).Summary ?? "(not kept)";
            Log.Warning("Multiplayer Foundation: lockstep desync at tick " + desync.Tick + " (" + desync.Players.Count +
                " player(s) differ from the host); own state: " + own);
            Messages.Message("Abweichung bei Tick " + desync.Tick + ": " + who + ". Spieleraktionen werden noch nicht geteilt (SYNC-002).",
                MessageTypeDefOf.NegativeEvent, false);
        }

        private void RunTick(TickManager tickManager)
        {
            bool pushed = false;
            Ticking = true;
            try
            {
                if (ReseedRand) { Rand.PushState(SessionSeed.ForTick(sessionSeed, tickManager.TicksGame + 1)); pushed = true; }
                tickManager.DoSingleTick();
                AfterTick?.Invoke(tickManager.TicksGame);
                TicksRun++;
            }
            finally
            {
                Ticking = false;
                if (pushed) Rand.PopState();
            }
        }

        private void ExecuteDue(TickManager tickManager)
        {
            int missed = State.LateCommands;
            ScheduledCommand? command;
            while ((command = State.TakeDue(tickManager.TicksGame)) != null) Execute(command, tickManager);
            if (State.LateCommands != missed)
                Log.Error("Multiplayer Foundation: " + (State.LateCommands - missed) + " lockstep command(s) arrived after their tick; " +
                    "this game differs from the others from tick " + tickManager.TicksGame + " on.");
        }

        private void Execute(ScheduledCommand command, TickManager tickManager)
        {
            Rand.PushState(SessionSeed.ForCommand(sessionSeed, command.Tick, command.Seq));
            try
            {
                switch (command.Kind)
                {
                    case LockstepCommandKind.SetSpeed:
                        Speed = LockstepCommandKind.ReadSpeed(command.Payload);
                        if (Speed != SessionSpeed.Paused) LastRunning = Speed;
                        ApplySpeed(tickManager);
                        break;
                    case LockstepCommandKind.Marker:
                        LastMarker = "Markierung #" + command.Seq + " von " + nameOf(command.Issuer) + " bei Tick " + command.Tick +
                            ": " + LockstepCommandKind.ReadMarker(command.Payload);
                        Messages.Message(LastMarker, MessageTypeDefOf.SilentInput, false);
                        Log.Message("Multiplayer Foundation: lockstep marker #" + command.Seq + " ran before tick " + command.Tick);
                        break;
                    default:
                        // game commands come with SYNC-002; until then a known no-op on every player
                        Log.Message("Multiplayer Foundation: lockstep command kind " + command.Kind + " is not supported yet; ignored before tick " + command.Tick);
                        break;
                }
            }
            catch (ProtocolException error)
            {
                Log.Error("Multiplayer Foundation: invalid lockstep command #" + command.Seq + ": " + error.Message);
            }
            finally { Rand.PopState(); }
            CommandsRun++;
            CommandRan?.Invoke(command, tickManager.TicksGame);
        }

        private void ApplySpeed(TickManager tickManager)
        {
            TimeSpeed wanted = ToTimeSpeed(Speed);
            if (tickManager.CurTimeSpeed == wanted) return;
            ApplyingSpeed = true;
            try { tickManager.CurTimeSpeed = wanted; }
            finally { ApplyingSpeed = false; }
        }

        private void SendHash(int tick)
        {
            var (hash, summary) = GameStateHash.Compute(Game);
            summaries.Add((tick, summary));
            if (summaries.Count > KeptSummaries) summaries.RemoveAt(0);
            LastHashTick = tick;
            send(MessageType.StateHash, new StateHash(tick, hash).ToPayload());
        }

        // About 10 times a second while ticks run, and once a second while nothing changes.
        private void Report(TickManager tickManager, bool force)
        {
            int tick = tickManager.TicksGame;
            ClientTickFlags flags = tickManager.slower.ForcedNormalSpeed ? ClientTickFlags.ForcedNormalSpeed : ClientTickFlags.None;
            float now = Time.realtimeSinceStartup;
            bool changed = tick != reportedTick || flags != reportedFlags;
            if (!force && !(changed && now >= nextReportAt) && now < nextReportAt + 0.9f) return;
            reportedTick = tick; reportedFlags = flags; nextReportAt = now + ReportSeconds;
            send(MessageType.ClientTick, new ClientTick(tick, flags).ToPayload());
        }

        internal static TimeSpeed ToTimeSpeed(SessionSpeed speed) => speed switch
        {
            SessionSpeed.Normal => TimeSpeed.Normal,
            SessionSpeed.Fast => TimeSpeed.Fast,
            SessionSpeed.Superfast => TimeSpeed.Superfast,
            _ => TimeSpeed.Paused
        };

        internal static SessionSpeed? FromTimeSpeed(TimeSpeed speed) => speed switch
        {
            TimeSpeed.Paused => SessionSpeed.Paused,
            TimeSpeed.Normal => SessionSpeed.Normal,
            TimeSpeed.Fast => SessionSpeed.Fast,
            TimeSpeed.Superfast => SessionSpeed.Superfast,
            _ => null
        };
    }
}
