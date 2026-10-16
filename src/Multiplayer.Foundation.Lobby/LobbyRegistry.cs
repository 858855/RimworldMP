using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Lobby
{
    public sealed class LobbyRuleException : Exception
    {
        public LobbyRuleException(byte code, string message) : base(message) { Code = code; }
        public byte Code { get; }
    }

    public sealed class LobbyChange
    {
        public LobbyChange(LobbyView? state, IReadOnlyList<Guid> recipients, Guid actor)
        { State = state; Recipients = recipients; Actor = actor; }
        public LobbyView? State { get; }
        public IReadOnlyList<Guid> Recipients { get; }
        public Guid Actor { get; }
    }

    public sealed class LobbyRegistry
    {
        private readonly object gate = new object();
        private readonly Dictionary<Guid, Member> members = new Dictionary<Guid, Member>();
        private readonly Dictionary<Guid, Room> rooms = new Dictionary<Guid, Room>();
        private readonly byte maximumCapacity;
        private readonly byte minimumStartPlayers;
        private readonly bool factionsSetupEnabled;

        /// <param name="minimumStartPlayers">Players needed to start the shared setup: 2 normally, 1 only as a debug option.</param>
        /// <param name="factionsSetupEnabled">
        /// Tests only: lets a Factions lobby start its setup. Off everywhere else until the game side exists (ADR 0037).
        /// </param>
        public LobbyRegistry(byte maximumCapacity, byte minimumStartPlayers = 2, bool factionsSetupEnabled = false)
        {
            if (maximumCapacity < 2 || maximumCapacity > 64) throw new ArgumentOutOfRangeException(nameof(maximumCapacity));
            if (minimumStartPlayers < 1 || minimumStartPlayers > 2) throw new ArgumentOutOfRangeException(nameof(minimumStartPlayers));
            this.maximumCapacity = maximumCapacity; this.minimumStartPlayers = minimumStartPlayers;
            this.factionsSetupEnabled = factionsSetupEnabled;
        }

        public void AddMember(Guid id, string name, PlayerKey key)
        {
            ValidateName(name);
            if (!key.IsValid) throw new LobbyRuleException(1, "Invalid player key.");
            lock (gate)
            {
                if (id == Guid.Empty || members.ContainsKey(id)) throw new LobbyRuleException(6, "Invalid session.");
                // One connection per player: ownership and permissions hang on the key (ADR 0016).
                if (members.Values.Any(member => member.Key == key)) throw new LobbyRuleException(6, "This player is already connected.");
                members.Add(id, new Member(id, name, key));
            }
        }

        public IReadOnlyList<LobbySummary> List()
        {
            lock (gate)
                return rooms.Values.OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Id)
                    .Select(r => new LobbySummary(r.Id, r.Name, members[r.Host].Name, (byte)r.Members.Count, r.Capacity, r.Phase,
                        members[r.Host].Mods, r.Mode, r.Password != null)).ToArray();
        }

        /// <summary>MOD-001: stores a member's mod summary; returns the lobby change when the member is in a lobby.</summary>
        public LobbyChange? SetManifest(Guid actor, ModManifestSummary summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            lock (gate)
            {
                Member member = GetMember(actor);
                member.Mods = summary;
                if (member.Room == null) return null;
                Room room = GetRoom(member.Room.Value);
                if (room.Phase == SetupPhase.Lobby) ClearReadyWithoutMatch(room);
                room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>Session ID of the host of the actor's lobby, e.g. for the full mod list behind the diff report.</summary>
        public Guid HostOfCurrentLobby(Guid actor)
        { lock (gate) return GetCurrentRoom(GetMember(actor)).Host; }

        /// <summary>First reason why the host may not start yet, or null. The same rules the server enforces.</summary>
        public static string? StartBlockReason(LobbyView lobby, Guid actor, bool allowSoloStart = false)
        {
            if (lobby.HostId != actor) return "Nur der Host kann das Spiel starten.";
            if (lobby.Phase != SetupPhase.Lobby) return "Spieleinrichtung läuft bereits.";
            if (lobby.Players.Count < (allowSoloStart ? 1 : 2)) return "Mindestens 2 Spieler nötig.";
            if (lobby.Players.Any(p => p.ModStatus == ModStatus.Checking)) return "Mod-Prüfung läuft …";
            string[] mismatched = lobby.Players.Where(p => p.ModStatus == ModStatus.Mismatch).Select(p => p.Name).ToArray();
            if (mismatched.Length > 0) return "Mods stimmen nicht überein: " + string.Join(", ", mismatched);
            int ready = lobby.Players.Count(p => p.Ready);
            if (ready < lobby.Players.Count) return "Warte auf Bereitschaft: " + ready + "/" + lobby.Players.Count + " bereit";
            return null;
        }

        /// <summary>
        /// ADR 0037: first reason why the Factions storyteller step cannot continue yet, or null. Null as well outside that
        /// step and in Coop. The same rules the server enforces.
        /// </summary>
        public static string? StorytellerBlockReason(LobbyView lobby)
        {
            if (lobby.Mode.Mode != GameMode.Factions || lobby.Phase != SetupPhase.Storyteller) return null;
            if (lobby.FactionOptions == null) return "Der Host hat Schwierigkeit und Erzähler noch nicht festgelegt.";
            string[] missing = lobby.Players.Where(p => !lobby.FactionOptions.Offers(p.Storyteller)).Select(p => p.Name).ToArray();
            if (missing.Length > 0) return "Erzähler fehlt: " + string.Join(", ", missing);
            string[] waiting = lobby.Players.Where(p => !p.Ready).Select(p => p.Name).ToArray();
            if (waiting.Length > 0) return "Warte auf: " + string.Join(", ", waiting);
            return null;
        }

        /// <summary>
        /// ADR 0038: first reason why a Factions step cannot continue yet, or null: storytellers, own sites, own ideologies,
        /// and in a lobby that continues a saved game, members without a faction in that save. The same rules the server enforces.
        /// </summary>
        public static string? FactionSetupBlockReason(LobbyView lobby)
        {
            if (lobby.Mode.Mode != GameMode.Factions) return null;
            string[] waiting = lobby.Players.Where(p => !p.Ready).Select(p => p.Name).ToArray();
            string? Missing(string what, Func<PlayerView, bool> has)
            {
                string[] missing = lobby.Players.Where(p => !has(p)).Select(p => p.Name).ToArray();
                if (missing.Length > 0) return what + " fehlt: " + string.Join(", ", missing);
                return waiting.Length > 0 ? "Warte auf: " + string.Join(", ", waiting) : null;
            }
            switch (lobby.Phase)
            {
                case SetupPhase.Lobby:
                    if (!lobby.FactionsRestored) return null;
                    string[] strangers = lobby.Players.Where(p => p.FactionSlot == null).Select(p => p.Name).ToArray();
                    return strangers.Length > 0 ? "Kein Platz im Spielstand: " + string.Join(", ", strangers) : null;
                case SetupPhase.Storyteller: return StorytellerBlockReason(lobby);
                case SetupPhase.SiteSelection: return Missing("Startort", p => p.FactionSite.HasValue);
                case SetupPhase.Ideology: return Missing("Ideologie", p => p.Ideology != null);
                default: return null;
            }
        }

        public (Guid LobbyId, IReadOnlyList<Guid> Recipients) ChatRecipients(Guid actor)
        {
            lock (gate)
            {
                Room room = GetCurrentRoom(GetMember(actor));
                return (room.Id, room.Members.ToArray());
            }
        }

        /// <param name="mode">Fixed for the lobby's lifetime; there is no way to change it later (ADR 0031).</param>
        public LobbyChange Create(Guid actor, string name, byte capacity, LobbyMode? mode = null, string? password = null)
        {
            ValidateName(name);
            lock (gate)
            {
                Member member = GetMember(actor); RequireFree(member);
                if (capacity < 2 || capacity > maximumCapacity) throw new LobbyRuleException(6, "Invalid lobby capacity.");
                if (rooms.Count >= 128) throw new LobbyRuleException(7, "Lobby limit reached.");
                var room = new Room(Guid.NewGuid(), name, actor, capacity, mode ?? LobbyMode.CoopHost) { Password = password }; room.Members.Add(actor);
                rooms.Add(room.Id, room); member.Room = room.Id;
                return Change(room, actor);
            }
        }

        public LobbyChange Join(Guid actor, Guid lobbyId, string? password = null)
        {
            lock (gate)
            {
                Member member = GetMember(actor); RequireFree(member);
                Room room = GetRoom(lobbyId);
                // ADR 0035: checked first, so a wrong password reveals nothing about the lobby's state.
                if (room.Password != null && !SameText(room.Password, password)) throw new LobbyRuleException(3, "Wrong lobby password.");
                // JOIN-001: a running game accepts joiners; the setup phases in between stay locked (ADR 0032).
                if (room.Phase != SetupPhase.Lobby && room.Phase != SetupPhase.Playing) throw new LobbyRuleException(6, "Setup in progress.");
                if (room.Members.Count >= room.Capacity) throw new LobbyRuleException(5, "Lobby is full.");
                if (room.Phase == SetupPhase.Playing && StatusOf(room, member) is ModStatus.Checking or ModStatus.Mismatch)
                    throw new LobbyRuleException(8, "Mods do not match the host.");
                // ADR 0037: a running Factions game has exactly the factions it started with; a player returns to theirs.
                if (room.Phase == SetupPhase.Playing && room.Mode.Mode == GameMode.Factions && !room.FactionSlots.ContainsKey(member.Key))
                    throw new LobbyRuleException(6, "This game has no faction for this player.");
                // ADR 0038: a lobby that continues a saved Factions game admits exactly the players of that save.
                if (room.Phase == SetupPhase.Lobby && room.FactionsRestored && !room.FactionSlots.ContainsKey(member.Key))
                    throw new LobbyRuleException(6, "This saved game has no faction for this player.");
                room.Members.Add(actor); member.Room = room.Id; room.Revision++;
                return Change(room, actor);
            }
        }

        public LobbyChange Ready(Guid actor, bool ready)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                // Ready means "ready to start" in the lobby and "done" in character selection. In the Factions mode it is also
                // "done" with the own storyteller (ADR 0037), site and ideology (ADR 0038); nowhere else.
                bool factions = room.Mode.Mode == GameMode.Factions;
                bool factionStep = factions && room.Phase is SetupPhase.Storyteller or SetupPhase.SiteSelection or SetupPhase.Ideology;
                if (room.Phase != SetupPhase.Lobby && room.Phase != SetupPhase.Characters && !factionStep)
                    throw new LobbyRuleException(6, "Ready is not used in this setup step.");
                if (ready && factionStep && room.Phase == SetupPhase.Storyteller &&
                    (room.FactionOptions == null || !room.FactionOptions.Offers(StorytellerOf(room, member))))
                    throw new LobbyRuleException(6, "Choose a storyteller before you are done.");
                if (ready && factionStep && room.Phase == SetupPhase.SiteSelection && !room.FactionSites.ContainsKey(member.Key))
                    throw new LobbyRuleException(6, "Choose your starting site before you are done.");
                if (ready && factionStep && room.Phase == SetupPhase.Ideology && !room.Ideologies.ContainsKey(member.Key))
                    throw new LobbyRuleException(6, "Choose your ideology before you are done.");
                // SETUP-004: "done" needs a pawn of your own in the starting group (ADR 0015).
                if (ready && room.Phase == SetupPhase.Characters && (room.Roster == null || !room.Roster.OwnsSelectedPawn(member.Key)))
                    throw new LobbyRuleException(6, "Take a pawn before you are done.");
                if (ready && room.Phase == SetupPhase.Lobby)
                {
                    ModStatus status = StatusOf(room, member);
                    if (status == ModStatus.Checking) throw new LobbyRuleException(8, "Mod check is still running.");
                    if (status == ModStatus.Mismatch) throw new LobbyRuleException(8, "Mods do not match the host.");
                }
                member.Ready = ready; room.Revision++;
                return Change(room, actor);
            }
        }

        public LobbyChange Leave(Guid actor)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                member.Room = null; member.Ready = false; member.Flag = null; room.Members.Remove(actor); room.Revision++;
                // ADR 0037/0038: before a new game exists a departed player cannot come back, so no faction is made for them.
                // From Generating on, and in a lobby that continues a saved game, the faction exists and waits for its player.
                if (room.Phase < SetupPhase.Generating && !room.FactionsRestored) ForgetFaction(room, member.Key);
                if (room.Members.Count == 0) { rooms.Remove(room.Id); return new LobbyChange(null, Array.Empty<Guid>(), actor); }
                // Only the host holds the generated setup state, so it ends before the host role moves (plan §6.2).
                if (room.Host == actor) { ResetSetup(room); room.Host = room.Members[0]; ClearReadyWithoutMatch(room); }
                return Change(room, actor);
            }
        }

        public LobbyChange Close(Guid actor)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                if (room.Host != actor) throw new LobbyRuleException(3, "Only host may close lobby.");
                Guid[] recipients = room.Members.ToArray();
                foreach (Guid id in recipients) { members[id].Room = null; members[id].Ready = false; members[id].Flag = null; }
                rooms.Remove(room.Id);
                return new LobbyChange(null, recipients, actor);
            }
        }

        /// <summary>Host moves setup one phase on. The target must be the next phase, so a repeated click skips nothing.</summary>
        public LobbyChange AdvanceSetup(Guid actor, SetupPhase target)
        {
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase == SetupPhase.Playing) throw new LobbyRuleException(6, "Setup is complete; time runs with lockstep (SYNC-001).");
                // ADR 0038 (SAVE-002, Factions part): a continued game skips the new-game setup and goes to the game transfer.
                if (room.Phase == SetupPhase.Lobby && room.FactionsRestored)
                {
                    if (target != SetupPhase.Generating) throw new LobbyRuleException(6, "This lobby continues a saved game; start it with the game transfer.");
                    RequireStartConditions(room);
                    if (room.Members.Any(id => !room.FactionSlots.ContainsKey(members[id].Key)))
                        throw new LobbyRuleException(6, "A player has no faction in the saved game.");
                    EnterPhase(room, SetupPhase.Generating);
                    return Change(room, actor);
                }
                if (target != room.Phase + 1) throw new LobbyRuleException(6, "Unexpected setup step.");
                switch (room.Phase)
                {
                    case SetupPhase.Lobby:
                        RequireStartConditions(room);
                        break;
                    case SetupPhase.Storyteller when room.Mode.Mode == GameMode.Factions:
                        // ADR 0037: the host's shared difficulty, a storyteller of the list for everyone, everyone done.
                        if (room.FactionOptions == null)
                            throw new LobbyRuleException(6, "The host has not set the difficulty and storytellers yet.");
                        if (room.Members.Any(id => !room.FactionOptions.Offers(StorytellerOf(room, members[id]))))
                            throw new LobbyRuleException(6, "Not every player has chosen a storyteller.");
                        RequireEveryoneReady(room, "Not every player is done choosing a storyteller.");
                        break;
                    case SetupPhase.WorldTransfer:
                        // SETUP-002: every guest must have loaded the host's world with the same fingerprint.
                        if (room.HostWorld == null) throw new LobbyRuleException(6, "The world has not been sent yet.");
                        if (room.Members.Any(id => WorldStatusOf(room, members[id]) == WorldStatus.Mismatch))
                            throw new LobbyRuleException(8, "The world differs for at least one player.");
                        if (room.Members.Any(id => WorldStatusOf(room, members[id]) != WorldStatus.Match))
                            throw new LobbyRuleException(6, "Not every player has loaded the world yet.");
                        break;
                    case SetupPhase.SiteSelection when room.Mode.Mode == GameMode.Factions:
                        // ADR 0038: every faction has its own site, checked by the host's game before it continues.
                        if (room.Members.Any(id => !room.FactionSites.ContainsKey(members[id].Key)))
                            throw new LobbyRuleException(6, "Not every player has chosen a starting site.");
                        RequireEveryoneReady(room, "Not every player is done choosing a starting site.");
                        break;
                    case SetupPhase.SiteSelection:
                        throw new LobbyRuleException(6, "Choose the starting site to continue.");
                    case SetupPhase.Ideology when room.Mode.Mode == GameMode.Factions:
                        if (room.Members.Any(id => !room.Ideologies.ContainsKey(members[id].Key)))
                            throw new LobbyRuleException(6, "Not every player has chosen an ideology.");
                        RequireEveryoneReady(room, "Not every player is done choosing an ideology.");
                        break;
                    case SetupPhase.Characters:
                        // ADR 0015: everyone must be done; the host cannot force-start.
                        RequireEveryoneReady(room, "Not every player is done choosing characters.");
                        break;
                    case SetupPhase.Generating:
                        // SETUP-005: every guest must have loaded the host's starting game with the same fingerprint.
                        if (room.HostGame == null) throw new LobbyRuleException(6, "The game has not been sent yet.");
                        if (room.Members.Any(id => WorldStatusOf(room, members[id]) == WorldStatus.Mismatch))
                            throw new LobbyRuleException(8, "The game differs for at least one player.");
                        if (room.Members.Any(id => WorldStatusOf(room, members[id]) != WorldStatus.Match))
                            throw new LobbyRuleException(6, "Not every player has loaded the game yet.");
                        break;
                }
                if (target == SetupPhase.Scenario && room.Mode.Mode == GameMode.Factions) AssignFactionSlots(room);
                EnterPhase(room, target);
                return Change(room, actor);
            }
        }

        /// <summary>
        /// ADR 0037: the host sets the shared difficulty and the storytellers players may choose. A change clears everyone's
        /// "done" and drops choices no longer offered; sending the same options again changes nothing.
        /// </summary>
        public LobbyChange SetFactionOptions(Guid actor, FactionOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                RequireStorytellerStep(room);
                if (options.SameAs(room.FactionOptions)) return Change(room, actor);
                room.FactionOptions = options;
                foreach (PlayerKey key in room.Storytellers.Where(entry => !options.Offers(entry.Value)).Select(entry => entry.Key).ToArray())
                    room.Storytellers.Remove(key);
                foreach (Guid id in room.Members) members[id].Ready = false;
                room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>ADR 0037: any member, the host included, chooses the storyteller of their own faction from the host's list.</summary>
        public LobbyChange ChooseStoryteller(Guid actor, string storyteller)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                RequireStorytellerStep(room);
                if (room.FactionOptions == null) throw new LobbyRuleException(6, "Wait for the host's storytellers.");
                if (!room.FactionOptions.Offers(storyteller)) throw new LobbyRuleException(6, "This storyteller is not offered.");
                if (StorytellerOf(room, member) == storyteller) return Change(room, actor);
                room.Storytellers[member.Key] = storyteller;
                member.Ready = false; room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>
        /// ADR 0038: the starting site of the actor's own faction (null withdraws it). No two factions share a site; whether the
        /// tile can be settled is for the host's game to check (<see cref="ClearFactionChoice"/>).
        /// </summary>
        public LobbyChange ChooseFactionSite(Guid actor, SiteTile? tile)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                RequireFactionStep(room, SetupPhase.SiteSelection, "Sites are chosen during site selection.");
                RequireSlot(room, member);
                room.FactionSites.TryGetValue(member.Key, out SiteTile current);
                bool has = room.FactionSites.ContainsKey(member.Key);
                if (tile == null ? !has : has && current == tile.Value) return Change(room, actor);
                if (tile != null && room.FactionSites.Any(entry => entry.Key != member.Key && entry.Value == tile.Value))
                    throw new LobbyRuleException(6, "Another faction already starts on this site.");
                if (tile == null) room.FactionSites.Remove(member.Key); else room.FactionSites[member.Key] = tile.Value;
                room.Notes.Remove(member.Key); member.Ready = false; room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>
        /// ADR 0038: the ideology of the actor's own faction. The lobby keeps the label and the SHA-256; when the choice changed,
        /// the caller forwards the data to <c>Host</c> with the actor's slot and key (only the host generates the world).
        /// </summary>
        public (LobbyChange Change, bool Forward, Guid Host, byte Slot, PlayerKey Player) ChooseIdeology(Guid actor, string label, byte[] data)
        {
            if (!FactionPayloads.IsValidText(label, FactionPayloads.MaximumIdeologyLabelBytes) || data == null || data.Length == 0 ||
                data.Length > FactionPayloads.MaximumIdeologyBytes)
                throw new LobbyRuleException(1, "Invalid ideology choice.");
            IdeologySummary summary = IdeologySummary.Of(label, data);
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                RequireFactionStep(room, SetupPhase.Ideology, "Ideologies are chosen in the ideology step.");
                byte slot = RequireSlot(room, member);
                if (room.Ideologies.TryGetValue(member.Key, out IdeologySummary? current) && current.SameAs(summary))
                    return (Change(room, actor), false, room.Host, slot, member.Key);
                room.Ideologies[member.Key] = summary;
                room.Notes.Remove(member.Key); member.Ready = false; room.Revision++;
                return (Change(room, actor), true, room.Host, slot, member.Key);
            }
        }

        /// <summary>
        /// ADR 0038: the host's game rejects a faction's site or ideology it found invalid. The choice and that player's "done"
        /// are cleared, and the reason is shown with the player until their next choice.
        /// </summary>
        public LobbyChange ClearFactionChoice(Guid actor, byte slot, FactionChoiceKind kind, string reason)
        {
            // Checked before any change: an invalid note would make every later lobby state unencodable.
            if (!FactionPayloads.IsValidText(reason, FactionPayloads.MaximumNoteBytes)) throw new LobbyRuleException(1, "Invalid reason.");
            if (kind != FactionChoiceKind.Site && kind != FactionChoiceKind.Ideology) throw new LobbyRuleException(1, "Unknown faction choice.");
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                SetupPhase phase = kind == FactionChoiceKind.Site ? SetupPhase.SiteSelection : SetupPhase.Ideology;
                RequireFactionStep(room, phase, "This choice is not made in the current step.");
                PlayerKey? owner = room.FactionSlots.Where(entry => entry.Value == slot).Select(entry => (PlayerKey?)entry.Key).FirstOrDefault();
                if (owner == null) throw new LobbyRuleException(6, "No faction has this slot.");
                if (kind == FactionChoiceKind.Site) room.FactionSites.Remove(owner.Value); else room.Ideologies.Remove(owner.Value);
                room.Notes[owner.Value] = reason;
                foreach (Guid id in room.Members.Where(id => members[id].Key == owner.Value)) members[id].Ready = false;
                room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>
        /// ADR 0038 (SAVE-002, Factions part): the host's game restores the faction table of the saved game it loaded. The lobby
        /// then admits only those players and starts straight into the game transfer.
        /// </summary>
        public LobbyChange RestoreFactions(Guid actor, FactionRestore restore)
        {
            if (restore == null) throw new ArgumentNullException(nameof(restore));
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Mode.Mode != GameMode.Factions) throw new LobbyRuleException(6, "Only a Factions game restores faction slots.");
                if (room.Phase != SetupPhase.Lobby) throw new LobbyRuleException(6, "Factions are restored before the game starts.");
                if (!restore.Entries.Any(entry => entry.Player == members[actor].Key))
                    throw new LobbyRuleException(6, "The host has no faction in this saved game.");
                ClearFactions(room);
                foreach (FactionRestoreEntry entry in restore.Entries)
                { room.FactionSlots[entry.Player] = entry.Slot; room.Storytellers[entry.Player] = entry.Storyteller; }
                room.FactionsRestored = true;
                foreach (Guid id in room.Members) members[id].Ready = false;
                room.Revision++;
                return Change(room, actor);
            }
        }

        public LobbyChange AbortSetup(Guid actor)
        {
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase == SetupPhase.Lobby) throw new LobbyRuleException(6, "No setup is running.");
                ResetSetup(room); room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>Any member places, moves or (with null) removes their site suggestion.</summary>
        public LobbyChange SetSiteFlag(Guid actor, SiteTile? tile)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                if (room.Phase != SetupPhase.SiteSelection) throw new LobbyRuleException(6, "Flags are only placed during site selection.");
                // ADR 0038: in the Factions mode every player chooses their own site instead of suggesting one.
                if (room.Mode.Mode == GameMode.Factions) throw new LobbyRuleException(6, "In the Factions mode every player chooses their own site.");
                member.Flag = tile; room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>SETUP-002: the host announces the fingerprint of the world it is about to send.</summary>
        public LobbyChange SetHostWorld(Guid actor, byte[] fingerprint)
        {
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase != SetupPhase.WorldTransfer) throw new LobbyRuleException(6, "The world is sent during world transfer.");
                room.HostWorld = fingerprint;
                // A new world (host went back and generated again) invalidates every earlier report.
                foreach (Guid id in room.Members) members[id].World = null;
                room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>SETUP-002: a guest reports the fingerprint of the world it loaded.</summary>
        public LobbyChange ReportWorld(Guid actor, byte[] fingerprint)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                if (room.Phase != SetupPhase.WorldTransfer) throw new LobbyRuleException(6, "The world is reported during world transfer.");
                if (room.Host == actor) throw new LobbyRuleException(6, "The host announces its world with WorldReady.");
                member.World = fingerprint; room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>
        /// SETUP-005: the host announces the fingerprint of the starting game after loading its own save. Guests load the
        /// copy at the same time and may report first; their reports count once this arrives.
        /// </summary>
        public LobbyChange SetHostGame(Guid actor, byte[] fingerprint)
        {
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase != SetupPhase.Generating) throw new LobbyRuleException(6, "The game is sent while it is generated.");
                room.HostGame = fingerprint;
                room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>SETUP-005: a guest reports the fingerprint of the game it loaded.</summary>
        public LobbyChange ReportGame(Guid actor, byte[] fingerprint)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                if (room.Phase != SetupPhase.Generating) throw new LobbyRuleException(6, "The game is reported while it is generated.");
                if (room.Host == actor) throw new LobbyRuleException(6, "The host announces its game with GameReady.");
                member.Game = fingerprint; room.Revision++;
                return Change(room, actor);
            }
        }

        /// <summary>
        /// SETUP-004: the host publishes the starting pawns. A member whose own pawns changed loses "done" (ADR 0015).
        /// Returns the change and the members to forward the roster to.
        /// </summary>
        public (LobbyChange Change, IReadOnlyList<Guid> Viewers) SetRoster(Guid actor, PawnRoster roster)
        {
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase != SetupPhase.Characters) throw new LobbyRuleException(6, "Pawns are chosen during character selection.");
                room.Roster = roster;
                foreach (Guid id in room.Members)
                {
                    Member member = members[id];
                    byte[] hash = roster.OwnerHash(member.Key);
                    if (member.PawnHash != null && !member.PawnHash.SequenceEqual(hash)) member.Ready = false;
                    if (!roster.OwnsSelectedPawn(member.Key)) member.Ready = false;
                    member.PawnHash = hash;
                }
                room.Revision++;
                return (Change(room, actor), room.Members.Where(id => id != actor).ToArray());
            }
        }

        /// <summary>SETUP-004: a guest's pawn request goes to the host, stamped with the guest's player key.</summary>
        public (Guid Host, PlayerKey Requester) PawnRequestRoute(Guid actor)
        {
            lock (gate)
            {
                Member member = GetMember(actor); Room room = GetCurrentRoom(member);
                if (room.Phase != SetupPhase.Characters) throw new LobbyRuleException(6, "Pawns are chosen during character selection.");
                if (room.Host == actor) throw new LobbyRuleException(6, "The host changes pawns in its own game.");
                return (room.Host, member.Key);
            }
        }

        /// <summary>SETUP-001: the host's page mirror goes to every other member while setup runs.</summary>
        public IReadOnlyList<Guid> SetupSettingsRecipients(Guid actor)
        {
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase == SetupPhase.Lobby) throw new LobbyRuleException(6, "No setup is running.");
                return room.Members.Where(id => id != actor).ToArray();
            }
        }

        /// <summary>Only the host chooses the site, flagged or not (ADR 0015).</summary>
        public LobbyChange DecideSite(Guid actor, SiteTile tile)
        {
            lock (gate)
            {
                Room room = GetHostedRoom(actor);
                if (room.Phase != SetupPhase.SiteSelection) throw new LobbyRuleException(6, "The site is chosen during site selection.");
                if (room.Mode.Mode == GameMode.Factions) throw new LobbyRuleException(6, "In the Factions mode every player chooses their own site.");
                room.Site = tile;
                EnterPhase(room, SetupPhase.Ideology);
                return Change(room, actor);
            }
        }

        public LobbyChange? RemoveMember(Guid id)
        {
            lock (gate)
            {
                if (!members.TryGetValue(id, out Member? member)) return null;
                LobbyChange? change = null;
                if (member.Room != null) change = Leave(id);
                members.Remove(id); return change;
            }
        }

        private Member GetMember(Guid id)
        { if (!members.TryGetValue(id, out Member? member)) throw new LobbyRuleException(3, "Unknown session."); return member; }
        private Room GetRoom(Guid id)
        { if (!rooms.TryGetValue(id, out Room? room)) throw new LobbyRuleException(4, "Lobby not found."); return room; }
        private Room GetCurrentRoom(Member member)
        { if (member.Room == null) throw new LobbyRuleException(6, "Not in a lobby."); return GetRoom(member.Room.Value); }
        // Constant-time comparison: the time taken does not reveal how much of a guessed password was right.
        private static bool SameText(string expected, string? given)
        {
            if (given == null) return false;
            int difference = expected.Length ^ given.Length;
            for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ (i < given.Length ? given[i] : 0);
            return difference == 0;
        }
        private Room GetHostedRoom(Guid actor)
        {
            Room room = GetCurrentRoom(GetMember(actor));
            if (room.Host != actor) throw new LobbyRuleException(3, "Only the host can do this.");
            return room;
        }
        // MOD-001: compare each member's mod checksum with the current host's.
        private ModStatus StatusOf(Room room, Member member)
        {
            ModManifestSummary? host = members[room.Host].Mods;
            if (host == null) return member.Mods == null || member.Id == room.Host ? ModStatus.Unchecked : ModStatus.Checking;
            if (member.Mods == null) return ModStatus.Checking;
            return member.Mods.SameAs(host) ? ModStatus.Match : ModStatus.Mismatch;
        }

        private void ClearReadyWithoutMatch(Room room)
        {
            foreach (Guid id in room.Members)
                if (StatusOf(room, members[id]) is ModStatus.Checking or ModStatus.Mismatch) members[id].Ready = false;
        }

        private static void RequireStorytellerStep(Room room)
        {
            if (room.Mode.Mode != GameMode.Factions) throw new LobbyRuleException(6, "In Coop the host chooses the storyteller.");
            if (room.Phase != SetupPhase.Storyteller) throw new LobbyRuleException(6, "Storytellers are chosen in the storyteller step.");
        }
        private static void RequireFactionStep(Room room, SetupPhase phase, string wrongPhase)
        {
            if (room.Mode.Mode != GameMode.Factions) throw new LobbyRuleException(6, "Only the Factions mode has a choice per faction.");
            if (room.Phase != phase) throw new LobbyRuleException(6, wrongPhase);
        }
        private static byte RequireSlot(Room room, Member member)
        {
            if (!room.FactionSlots.TryGetValue(member.Key, out byte slot)) throw new LobbyRuleException(6, "You have no faction in this game.");
            return slot;
        }
        // ADR 0031/0037/0038: the checks of every start, new game or continued one.
        private void RequireStartConditions(Room room)
        {
            // Coop is built first; the Factions start stays refused until its game side exists.
            if (room.Mode.Mode == GameMode.Factions && !factionsSetupEnabled)
                throw new LobbyRuleException(6, "The Factions mode is not available yet.");
            if (room.Members.Count < minimumStartPlayers) throw new LobbyRuleException(6, "At least 2 players are needed.");
            // MOD-001: start again checks that every member's mods match the host's.
            if (room.Members.Any(id => StatusOf(room, members[id]) is ModStatus.Checking or ModStatus.Mismatch))
                throw new LobbyRuleException(8, "Mods do not match for every player.");
            RequireEveryoneReady(room, "Not every player is ready.");
        }
        private static void ForgetFaction(Room room, PlayerKey key)
        {
            room.FactionSlots.Remove(key); room.Storytellers.Remove(key); room.FactionSites.Remove(key);
            room.Ideologies.Remove(key); room.Notes.Remove(key);
        }
        private static void ClearFactions(Room room)
        {
            room.FactionSlots.Clear(); room.Storytellers.Clear(); room.FactionOptions = null; room.FactionSites.Clear();
            room.Ideologies.Clear(); room.Notes.Clear(); room.FactionsRestored = false;
        }
        private static string? StorytellerOf(Room room, Member member) =>
            room.Storytellers.TryGetValue(member.Key, out string? storyteller) ? storyteller : null;
        // ADR 0037: host first, then everyone else in joining order. Slots stay with the player key and are never renumbered.
        private void AssignFactionSlots(Room room)
        {
            ClearFactions(room);
            byte slot = 0;
            foreach (Guid id in new[] { room.Host }.Concat(room.Members.Where(id => id != room.Host)))
                room.FactionSlots[members[id].Key] = slot++;
        }
        private void RequireEveryoneReady(Room room, string message)
        { if (room.Members.Any(id => !members[id].Ready)) throw new LobbyRuleException(6, message); }
        // Ready and flags belong to one phase; a lobby "ready" must never count as "done" later.
        private void EnterPhase(Room room, SetupPhase phase)
        {
            room.Phase = phase; room.Revision++; room.Roster = null; room.Notes.Clear();
            foreach (Guid id in room.Members) { members[id].Ready = false; members[id].Flag = null; members[id].PawnHash = null; }
        }
        private void ResetSetup(Room room)
        {
            room.Phase = SetupPhase.Lobby; room.Site = null; room.HostWorld = null; room.Roster = null; room.HostGame = null;
            ClearFactions(room);
            foreach (Guid id in room.Members)
            {
                members[id].Ready = false; members[id].Flag = null; members[id].World = null; members[id].PawnHash = null;
                members[id].Game = null;
            }
        }
        // One status for the copy that matters now: the world in WorldTransfer, the starting game from Generating on.
        private WorldStatus WorldStatusOf(Room room, Member member)
        {
            byte[]? hostCopy, ownCopy;
            if (room.Phase == SetupPhase.WorldTransfer) { hostCopy = room.HostWorld; ownCopy = member.World; }
            else if (room.Phase >= SetupPhase.Generating) { hostCopy = room.HostGame; ownCopy = member.Game; }
            else return WorldStatus.None;
            if (hostCopy == null) return WorldStatus.Waiting;
            if (member.Id == room.Host) return WorldStatus.Match;
            if (ownCopy == null) return WorldStatus.Waiting;
            return ownCopy.SequenceEqual(hostCopy) ? WorldStatus.Match : WorldStatus.Mismatch;
        }
        private static void RequireFree(Member member)
        { if (member.Room != null) throw new LobbyRuleException(6, "Already in a lobby."); }
        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 40 || name.Any(char.IsControl))
                throw new LobbyRuleException(1, "Name must have 1-40 printable characters.");
        }
        private LobbyChange Change(Room room, Guid actor)
        {
            var players = room.Members.Select(id => new PlayerView(id, members[id].Name, members[id].Ready, members[id].Key,
                members[id].Flag, StatusOf(room, members[id]), WorldStatusOf(room, members[id]), SlotOf(room, members[id]),
                StorytellerOf(room, members[id]), SiteOf(room, members[id]), IdeologyOf(room, members[id]), NoteOf(room, members[id]))).ToArray();
            return new LobbyChange(new LobbyView(room.Id, room.Name, room.Host, room.Revision, room.Capacity, players,
                room.Phase, room.Site, room.Mode, room.FactionOptions, room.FactionsRestored && room.Phase == SetupPhase.Lobby),
                room.Members.ToArray(), actor);
        }
        private static SiteTile? SiteOf(Room room, Member member) =>
            room.FactionSites.TryGetValue(member.Key, out SiteTile site) ? site : (SiteTile?)null;
        private static IdeologySummary? IdeologyOf(Room room, Member member) =>
            room.Ideologies.TryGetValue(member.Key, out IdeologySummary? ideology) ? ideology : null;
        private static string? NoteOf(Room room, Member member) =>
            room.Notes.TryGetValue(member.Key, out string? note) ? note : null;
        private static byte? SlotOf(Room room, Member member) =>
            room.FactionSlots.TryGetValue(member.Key, out byte slot) ? slot : (byte?)null;
        private sealed class Member
        {
            public Member(Guid id, string name, PlayerKey key) { Id = id; Name = name; Key = key; }
            public Guid Id { get; }
            public string Name { get; }
            public PlayerKey Key { get; }
            public ModManifestSummary? Mods { get; set; }
            public byte[]? World { get; set; }
            public byte[]? PawnHash { get; set; }
            public byte[]? Game { get; set; }
            public Guid? Room { get; set; }
            public bool Ready { get; set; }
            public SiteTile? Flag { get; set; }
        }
        private sealed class Room
        {
            public Room(Guid id, string name, Guid host, byte capacity, LobbyMode mode) { Id = id; Name = name; Host = host; Capacity = capacity; Mode = mode; }
            public LobbyMode Mode { get; }
            public string? Password { get; set; }
            public Guid Id { get; }
            public string Name { get; }
            public Guid Host { get; set; }
            public byte Capacity { get; }
            public ulong Revision { get; set; } = 1;
            public SetupPhase Phase { get; set; } = SetupPhase.Lobby;
            public SiteTile? Site { get; set; }
            public byte[]? HostWorld { get; set; }
            public byte[]? HostGame { get; set; }
            public PawnRoster? Roster { get; set; }
            public List<Guid> Members { get; } = new List<Guid>();
            // ADR 0037, Factions mode only: faction slot and storyteller per player key; options from the host.
            public Dictionary<PlayerKey, byte> FactionSlots { get; } = new Dictionary<PlayerKey, byte>();
            public Dictionary<PlayerKey, string> Storytellers { get; } = new Dictionary<PlayerKey, string>();
            public FactionOptions? FactionOptions { get; set; }
            // ADR 0038: site, ideology and the host's rejection note per player key; whether the lobby continues a saved game.
            public Dictionary<PlayerKey, SiteTile> FactionSites { get; } = new Dictionary<PlayerKey, SiteTile>();
            public Dictionary<PlayerKey, IdeologySummary> Ideologies { get; } = new Dictionary<PlayerKey, IdeologySummary>();
            public Dictionary<PlayerKey, string> Notes { get; } = new Dictionary<PlayerKey, string>();
            public bool FactionsRestored { get; set; }
        }
    }
}
