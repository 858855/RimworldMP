using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using HarmonyLib;
using Multiplayer.Foundation.Network;
using RimWorld;
using RimWorld.Planet;
using Multiplayer.Foundation.Hosting;
using Multiplayer.Foundation.Lobby;
using Multiplayer.Foundation.Protocol;
using Steamworks;
using UnityEngine;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    public sealed class FoundationMod : Mod
    {
        private readonly FoundationSettings settings;
        private LobbyClient? client;
        private Task<LobbyClient>? connecting;
        private Task<ListenHost>? hosting;
        private ListenHost? hosted;
        private SteamLobbyBridge? steam;
        private CSteamID pendingInvite;
        private bool steamHostStarting;
        private bool autoJoinSteamLobby;
        private Task<UpnpPortMapping>? mapping;
        private UpnpPortMapping? mapped;
        private IPAddress? externalAddress;
        private string upnpStatus = "";
        private IReadOnlyList<IPAddress> shareable = Array.Empty<IPAddress>();
        private Task? action;
        private IReadOnlyList<LobbySummary> available = Array.Empty<LobbySummary>();
        private LobbyView? lobby;
        private string status = "Nicht verbunden";
        private string lobbyName = "Colony";
        private string lobbyCapacity = "4";
        private int listPage;
        private uint requestId = 2;
        private readonly List<LobbyChatView> chatMessages = new List<LobbyChatView>();
        private string chatInput = "";
        private Vector2 chatScroll;
        private Vector2 connectionScroll;
        private readonly GameObject updateObject;
        // MOD-001: own manifest, built once in the background, sent once per connection.
        private Task<ModManifest>? manifestTask;
        private ModManifest? manifest;
        private ModManifestSummary? manifestSummary;
        private LobbyClient? manifestSentTo;
        // XFER-001 state machines: the host answers mod-list requests one at a time; a guest downloads one list.
        private TransferSender? upload;
        private readonly Queue<Guid> manifestRequests = new Queue<Guid>();
        private TransferReceiver? download;
        private bool diffRequested;
        private readonly List<Task> backgroundSends = new List<Task>();
        // SETUP-001: the host's vanilla setup pages drive the server phases and are mirrored to the guests.
        private bool hostSetupActive;
        private SetupSettings? lastMirror;
        private SetupSettings? hostSettings;
        private float nextMirrorAt;
        private float? pageMissingSince;
        private bool advancePending;
        // SETUP-002: the host sends its generated world once per world object; guests load it and report.
        private World? sentWorld;
        private (byte[] Fingerprint, byte[] Data)? preparedWorld;
        private TransferSender? worldUpload;
        private TransferReceiver? worldDownload;
        private byte[]? pendingWorldReport;
        private bool guestWorldLoaded;
        private string worldText = "";
        private string? hostScenarioName;
        // SETUP-003: a guest's own starting-site page, where "Next" sets the guest's flag.
        private bool guestSiteMapActive;
        // SETUP-004: the host owns the pawns (CharacterHost); guests see the latest roster.
        private readonly CharacterHost characterHost = new CharacterHost();
        private byte[] lastRosterPayload = System.Array.Empty<byte>();
        private PawnRoster? pawnRoster;
        private int? chosenPawnId;
        private string renameText = "";
        private static readonly AccessTools.FieldRef<Page_ConfigureStartingPawns, int> CurPawnIndex =
            AccessTools.FieldRefAccess<Page_ConfigureStartingPawns, int>("curPawnIndex");
        // SETUP-005: the host starts the vanilla game once the server is in Generating, saves it and sends the save;
        // guests load it. Time stays paused for everyone until lockstep (SYNC-001).
        private bool startRequested, allowGameStart, resendGame;
        private Dictionary<int, PlayerKey>? startOwners;
        private List<(PlayerKey Key, string Name)>? startPlayers;
        private string? gameFileName;
        private DateTime saveStartedUtc;
        private byte[]? sentGame, gameFingerprint, hostSaveFile;
        private Game? reloadFrom;
        private TransferSender? gameUpload;
        private TransferReceiver? gameDownload;
        private byte[]? guestGameFile;
        private bool guestGameLoading;
        private float guestLoadStartedAt;
        private string gameText = "";
        private float nextPauseNoteAt;
        internal bool HostSetupActive => hostSetupActive;
        internal bool GuestSiteMapActive => guestSiteMapActive;

        /// <summary>
        /// SETUP-005: the shared game is open and lockstep does not exist yet, so time must not run. While the lobby is
        /// in Generating or Playing, the game in Play is the shared one: setup starts from the main menu only.
        /// </summary>
        internal bool SessionPaused => WorldSelfTest.HoldsPause ||
            (client != null && lobby != null && lobby.Phase >= SetupPhase.Generating && Current.ProgramState == ProgramState.Playing);

        /// <summary>
        /// The game in the main menu lives only while a window is open (see FoundationWindow): a guest's received world,
        /// and the host's set-up game while its start waits for the server (vanilla has closed the character page by then).
        /// </summary>
        internal bool KeepWindowOpen => lobby != null && client != null && Current.ProgramState == ProgramState.Entry &&
            (lobby.HostId == client.SessionId ? startRequested
                : guestWorldLoaded && (lobby.Phase == SetupPhase.WorldTransfer || lobby.Phase == SetupPhase.SiteSelection));

        internal void NotifySessionPaused()
        {
            if (Time.realtimeSinceStartup < nextPauseNoteAt) return;
            nextPauseNoteAt = Time.realtimeSinceStartup + 3f;
            Messages.Message("Die Zeit läuft erst mit dem Gleichlauf (SYNC-001). Bis dahin bleibt das Spiel für alle pausiert.",
                MessageTypeDefOf.RejectInput, false);
        }
        internal static FoundationMod? Instance { get; private set; }
        private static bool menuPatched;

        public FoundationMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<FoundationSettings>();
            Instance = this;
            try { manifestTask = ModManifestBuilder.StartAsync(); }
            catch (Exception error) { Log.Error("Multiplayer mod manifest failed: " + error); }
            TryInitializeSteam();
            updateObject = new GameObject("Multiplayer Foundation updates");
            UnityEngine.Object.DontDestroyOnLoad(updateObject);
            updateObject.AddComponent<FoundationUpdatePump>().Owner = this;
            if (!menuPatched)
            {
                try
                {
                    new Harmony("rimworldmp.foundation.mainmenu").PatchAll(typeof(MainMenuOptionPatch).Assembly);
                    menuPatched = true;
                }
                catch (Exception error) { Log.Error("Multiplayer main-menu entry failed: " + error); }
            }
        }

        public override string SettingsCategory() => "Multiplayer Foundation";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x + 8f, inRect.y + 8f, inRect.width - 16f, 54f),
                "Verbindung, Steam-Einladungen und Lobby befinden sich im Multiplayer-Fenster.");
            Widgets.CheckboxLabeled(new Rect(inRect.x + 8f, inRect.y + 112f, Mathf.Min(560f, inRect.width - 16f), 28f),
                "Debug: Spiel allein starten erlauben (nur zum Testen)", ref settings.AllowSoloStart);
            if (Widgets.ButtonText(new Rect(inRect.x + 8f, inRect.y + 66f, 250f, 36f),
                "Multiplayer öffnen")) OpenWindow();
        }

        internal void OpenWindow()
        {
            if (!Find.WindowStack.IsOpen<FoundationWindow>())
                Find.WindowStack.Add(new FoundationWindow(this));
        }

        internal void TickNetwork() => UpdateNetwork();

        internal void DrawWindow(Rect inRect)
        {
            UpdateNetwork();
            float x = inRect.x + 8f, y = inRect.y + 6f, width = inRect.width - 16f;
            Widgets.Label(new Rect(x, y, width, 30f), "Multiplayer Foundation 0.2 (Vorschau)");
            y += 32f;
            Widgets.Label(new Rect(x, y, width, 28f), "Lobby, gemeinsame Einrichtung und Spielstart · die Zeit läuft erst mit dem Gleichlauf (SYNC-001)");
            y += 34f;
            float statusY = inRect.yMax - 48f;
            if (client == null)
            {
                Rect viewport = new Rect(x, y, width, statusY - y - 8f);
                Widgets.BeginScrollView(viewport, ref connectionScroll, new Rect(0f, 0f, width - 18f, 600f));
                float contentY = 0f, contentWidth = width - 18f;
                Widgets.Label(new Rect(0f, contentY, 110f, 26f), "Spielername");
                settings.PlayerName = Widgets.TextField(new Rect(112f, contentY, contentWidth - 112f, 26f), settings.PlayerName);
                contentY += 32f;
                Rect soloRect = new Rect(0f, contentY, Mathf.Min(520f, contentWidth), 26f);
                Widgets.CheckboxLabeled(soloRect, "Debug: Spiel allein starten erlauben (nur zum Testen)", ref settings.AllowSoloStart);
                TooltipHandler.TipRegion(soloRect, "Gilt beim nächsten Hosten. Der Host darf dann ohne Mitspieler starten, " +
                    "um die Spieleinrichtung allein zu testen.");
                contentY += 40f;
                DrawSteamSection(0f, ref contentY, contentWidth);
                contentY += 12f;
                DrawIpSection(0f, ref contentY, contentWidth);
                Widgets.EndScrollView();
            }
            else
            {
                DrawConnected(x, ref y, width, statusY);
            }
            Widgets.DrawMenuSection(new Rect(x, statusY, width, 42f));
            Widgets.Label(new Rect(x + 10f, statusY + 7f, width - 20f, 30f), "Status: " + status);
        }

        private void DrawSteamSection(float x, ref float y, float width)
        {
            Widgets.DrawMenuSection(new Rect(x, y, width, 190f));
            float inner = x + 12f, innerWidth = width - 24f, line = y + 8f;
            Widgets.Label(new Rect(inner, line, innerWidth, 28f), "1 · Mit Steam-Freunden spielen");
            line += 29f;
            Widgets.Label(new Rect(inner, line, innerWidth, 29f),
                "Hoste eine Lobby und lade Freunde ein – ohne IP-Adresse oder Portfreigabe.");
            line += 33f;
            Widgets.Label(new Rect(inner, line, 100f, 26f), "Lobbyname");
            float nameWidth = Mathf.Max(150f, innerWidth - 250f);
            lobbyName = Widgets.TextField(new Rect(inner + 103f, line, nameWidth, 26f), lobbyName);
            Widgets.Label(new Rect(inner + 115f + nameWidth, line, 48f, 26f), "Plätze");
            lobbyCapacity = Widgets.TextField(new Rect(inner + 168f + nameWidth, line, 65f, 26f), lobbyCapacity);
            line += 34f;
            bool idle = connecting == null && hosting == null && action == null;
            if (steam == null)
            {
                Widgets.Label(new Rect(inner, line, innerWidth - 145f, 34f), "Steam gerade nicht verfügbar.");
                if (Widgets.ButtonText(new Rect(inner + innerWidth - 135f, line, 135f, 30f), "Steam prüfen")) TryInitializeSteam();
            }
            else
            {
                if (idle && Widgets.ButtonText(new Rect(inner, line, 220f, 32f), "Steam-Lobby hosten")) HostSteamLobby();
                if (pendingInvite.IsValid() && idle &&
                    Widgets.ButtonText(new Rect(inner + 230f, line, 235f, 32f), "Einladung annehmen")) JoinSteamInvite();
                else if (!pendingInvite.IsValid())
                    Widgets.Label(new Rect(inner + 230f, line + 4f, innerWidth - 230f, 30f), "Warte auf Einladung eines Freundes");
            }
            y += 190f;
        }

        private void DrawIpSection(float x, ref float y, float width)
        {
            Widgets.DrawMenuSection(new Rect(x, y, width, 260f));
            float inner = x + 12f, innerWidth = width - 24f, line = y + 8f;
            Widgets.Label(new Rect(inner, line, innerWidth, 28f), "2 · Per IP verbinden oder direkt hosten");
            line += 32f;
            Widgets.Label(new Rect(inner, line, 105f, 26f), "Server-IP");
            TooltipHandler.TipRegion(new Rect(inner, line, innerWidth, 26f),
                "IP-Adresse oder Name des Hosts, optional mit :Port, z. B. 203.0.113.5:28491 oder [2001:db8::5]:28491");
            float addressWidth = Mathf.Max(170f, innerWidth - 220f);
            settings.ServerAddress = Widgets.TextField(new Rect(inner + 105f, line, addressWidth, 26f), settings.ServerAddress);
            Widgets.Label(new Rect(inner + 115f + addressWidth, line, 38f, 26f), "Port");
            settings.Port = Widgets.TextField(new Rect(inner + 158f + addressWidth, line, 60f, 26f), settings.Port);
            line += 34f;
            Widgets.Label(new Rect(inner, line, 105f, 26f), "Host-Bind-IP");
            settings.HostBindAddress = Widgets.TextField(new Rect(inner + 105f, line, addressWidth, 26f), settings.HostBindAddress);
            line += 31f;
            Widgets.Label(new Rect(inner + 105f, line, innerWidth - 105f, 26f), ":: = LAN und Internet, 127.0.0.1 = nur dieser PC");
            line += 31f;
            Widgets.CheckboxLabeled(new Rect(inner, line, Mathf.Min(420f, innerWidth), 26f),
                "Port am Router automatisch öffnen (UPnP)", ref settings.UseUpnp);
            line += 37f;
            bool idle = connecting == null && hosting == null && action == null;
            if (idle && Widgets.ButtonText(new Rect(inner, line, 220f, 32f), "Per IP verbinden")) Connect();
            if (idle && Widgets.ButtonText(new Rect(inner + 230f, line, 220f, 32f), "Direkt hosten")) HostLobby();
            line += 40f;
            Widgets.Label(new Rect(inner, line, innerWidth, 43f),
                "Für Internet-Spieler muss TCP-Port " + settings.Port +
                " am Router weitergeleitet sein – per UPnP automatisch oder manuell. Steam benötigt das nicht.");
            y += 260f;
        }

        private void DrawConnected(float x, ref float y, float width, float statusY)
        {
            if (steam?.IsHosting == true && Widgets.ButtonText(new Rect(x, y, 225f, 32f), "Steam-Freunde einladen"))
            {
                try { steam.OpenInviteDialog(); }
                catch (Exception error) { status = "Steam-Einladung fehlgeschlagen: " + error.Message; }
            }
            string? best = steam?.IsHosting == true ? null : BestShareAddress();
            if (best != null && Widgets.ButtonText(new Rect(x, y, 180f, 32f), "Adresse kopieren"))
            { GUIUtility.systemCopyBuffer = best; status = "Kopiert: " + best; }
            if (Widgets.ButtonText(new Rect(x + width - 145f, y, 145f, 32f), "Trennen")) Disconnect();
            y += 44f;
            if (hosted != null && steam?.IsHosting != true)
            {
                string info = Reachability();
                float infoHeight = Text.CalcHeight(info, width);
                Widgets.Label(new Rect(x, y, width, infoHeight), info);
                y += infoHeight + 8f;
            }
            float leftWidth = Mathf.Max(290f, width * 0.58f);
            float rightX = x + leftWidth + 12f;
            float rightWidth = width - leftWidth - 12f;
            float panelHeight = Mathf.Max(180f, statusY - y - 8f);
            Widgets.DrawMenuSection(new Rect(x, y, leftWidth, panelHeight));
            float leftY = y + 12f;
            if (lobby == null) DrawBrowser(x + 12f, ref leftY, leftWidth - 24f);
            else DrawLobby(x + 12f, ref leftY, leftWidth - 24f);
            DrawChat(new Rect(rightX, y, rightWidth, panelHeight));
        }

        private void DrawChat(Rect panel)
        {
            Widgets.DrawMenuSection(panel);
            Widgets.Label(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, 30f), "Lobby-Chat");
            Rect viewport = new Rect(panel.x + 12f, panel.y + 46f, panel.width - 24f, panel.height - 105f);
            if (lobby == null)
            {
                Widgets.Label(viewport, "Tritt einer Lobby bei, um mit den Spielern zu schreiben.");
                return;
            }
            float textWidth = viewport.width - 22f;
            float contentHeight = 0f;
            foreach (LobbyChatView entry in chatMessages)
                contentHeight += Text.CalcHeight(entry.SenderName + ": " + entry.Text, textWidth) + 8f;
            Widgets.BeginScrollView(viewport, ref chatScroll,
                new Rect(0f, 0f, textWidth, Mathf.Max(viewport.height, contentHeight)));
            float line = 0f;
            foreach (LobbyChatView entry in chatMessages)
            {
                string label = entry.SenderName + ": " + entry.Text;
                float lineHeight = Text.CalcHeight(label, textWidth) + 8f;
                Widgets.Label(new Rect(0f, line, textWidth, lineHeight), label);
                line += lineHeight;
            }
            Widgets.EndScrollView();
            float inputY = panel.yMax - 47f;
            GUI.SetNextControlName("FoundationChatInput");
            chatInput = Widgets.TextField(new Rect(panel.x + 12f, inputY, panel.width - 110f, 29f), chatInput);
            bool sendByButton = Widgets.ButtonText(new Rect(panel.xMax - 90f, inputY, 78f, 29f), "Senden");
            bool sendByEnter = Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "FoundationChatInput";
            if (sendByButton || sendByEnter)
            {
                if (sendByEnter) Event.current.Use();
                try { if (Send(MessageType.LobbyChatRequest, LobbyPayloads.ChatRequest(chatInput.Trim()))) chatInput = ""; }
                catch (Exception error) { status = "Chat: " + error.Message; }
            }
        }

        private void Connect()
        {
            if (!int.TryParse(settings.Port, out int port) || port < 1 || port > 65535)
            { status = "Ungültiger Port"; return; }
            if (!HostEndpoint.TryParse(settings.ServerAddress, port, out string host, out port))
            { status = "Ungültige Serveradresse"; return; }
            status = "Verbindung wird aufgebaut";
            connecting = LobbyClient.ConnectAsync(host, port, settings.PlayerName.Trim(), TimeSpan.FromSeconds(10),
                System.Threading.CancellationToken.None, CurrentPlayerKey());
        }

        // SteamID when Steam runs, otherwise a key created once and stored in the mod settings (ADR 0016).
        private PlayerKey CurrentPlayerKey()
        {
            if (steam != null)
            {
                CSteamID own = SteamUser.GetSteamID();
                if (own.IsValid()) return PlayerKey.Steam((ulong)own);
            }
            if (settings.EnsureLocalPlayerKey()) WriteSettings();
            return settings.LocalKey;
        }

        private void HostLobby()
        {
            if (!int.TryParse(settings.Port, out int port) || port < 1 || port > 65535)
            { status = "Ungültiger Port"; return; }
            if (!byte.TryParse(lobbyCapacity, out byte capacity) || capacity < 2 || capacity > 64)
            { status = "Plätze müssen zwischen 2 und 64 liegen"; return; }
            string name = lobbyName.Trim();
            if (name.Length == 0 || name.Length > 40 || name.Any(char.IsControl))
            { status = "Lobbyname muss 1–40 druckbare Zeichen haben"; return; }
            status = "Lokale Lobby startet";
            hosting = ListenHost.StartAsync(settings.HostBindAddress.Trim(), port,
                settings.PlayerName.Trim(), name, capacity, CurrentPlayerKey(), settings.AllowSoloStart);
        }

        private void HostSteamLobby()
        {
            if (steam == null) { status = "Steam ist nicht verfügbar"; return; }
            if (!byte.TryParse(lobbyCapacity, out byte capacity) || capacity < 2 || capacity > 64)
            { status = "Plätze müssen zwischen 2 und 64 liegen"; return; }
            string name = lobbyName.Trim();
            if (name.Length == 0 || name.Length > 40 || name.Any(char.IsControl))
            { status = "Lobbyname muss 1–40 druckbare Zeichen haben"; return; }
            status = "Steam-Lobby startet";
            steamHostStarting = true;
            hosting = StartSteamHostAsync(steam, settings.PlayerName.Trim(), name, capacity, CurrentPlayerKey(), settings.AllowSoloStart);
        }

        private static async Task<ListenHost> StartSteamHostAsync(SteamLobbyBridge bridge,
            string playerName, string name, byte capacity, PlayerKey playerKey, bool allowSoloStart)
        {
            ListenHost host = await ListenHost.StartAsync("127.0.0.1", 0, playerName, name, capacity, playerKey, allowSoloStart);
            try { await bridge.StartHostingAsync(host, name, capacity); return host; }
            catch { await host.DisposeAsync(); throw; }
        }

        private void JoinSteamInvite()
        {
            if (steam == null || !pendingInvite.IsValid()) return;
            CSteamID invited = pendingInvite;
            pendingInvite = default;
            status = "Steam-Verbindung wird aufgebaut";
            autoJoinSteamLobby = true;
            connecting = steam.JoinAsync(invited, settings.PlayerName.Trim());
        }

        private void TryInitializeSteam()
        {
            if (steam != null) return;
            try { steam = new SteamLobbyBridge(id => { pendingInvite = id; status = "Steam-Einladung erhalten"; }); }
            catch (Exception error) { status = "Steam nicht verfügbar: " + error.Message; }
        }

        private void Disconnect()
        {
            if (client == null) return;
            LobbyClient old = client;
            client = null; lobby = null; available = Array.Empty<LobbySummary>(); chatMessages.Clear();
            ResetTransfers();
            steam?.Leave();
            autoJoinSteamLobby = false;
            if (hosted != null)
            {
                ListenHost running = hosted;
                hosted = null;
                action = running.StopAsync();
                ReleaseMapping();
            }
            else action = old.DisconnectAsync();
            status = "Nicht verbunden";
        }

        private void DrawBrowser(float x, ref float y, float width)
        {
            if (Widgets.ButtonText(new Rect(x, y, 140f, 30f), "Aktualisieren"))
                Send(MessageType.LobbyListRequest, Array.Empty<byte>());
            y += 38f;
            Widgets.Label(new Rect(x, y, 110f, 26f), "Lobbyname");
            lobbyName = Widgets.TextField(new Rect(x + 110f, y, width - 110f, 26f), lobbyName);
            y += 31f;
            Widgets.Label(new Rect(x, y, 110f, 26f), "Plätze");
            lobbyCapacity = Widgets.TextField(new Rect(x + 110f, y, width - 110f, 26f), lobbyCapacity);
            y += 34f;
            if (Widgets.ButtonText(new Rect(x, y, 140f, 30f), "Lobby erstellen"))
            {
                if (byte.TryParse(lobbyCapacity, out byte capacity) && capacity >= 2 && capacity <= 64)
                {
                    string name = lobbyName.Trim();
                    if (name.Length == 0 || name.Length > 40 || name.Any(char.IsControl))
                        status = "Lobbyname muss 1–40 druckbare Zeichen haben";
                    else Send(MessageType.CreateLobby, LobbyPayloads.Create(name, capacity));
                }
                else status = "Plätze müssen zwischen 2 und 64 liegen";
            }
            y += 42f;
            Widgets.Label(new Rect(x, y, width, 27f), "Verfügbare Lobbys: " + available.Count);
            y += 30f;
            int start = listPage * 5;
            foreach (LobbySummary room in available.Skip(start).Take(5))
            {
                Rect roomRect = new Rect(x, y, width - 110f, 28f);
                bool locked = room.Phase != SetupPhase.Lobby;
                Widgets.Label(roomRect, room.Name + " – " + room.HostName + " (" + room.PlayerCount + "/" + room.Capacity + ")" +
                    (!locked ? "" : room.Phase == SetupPhase.Playing ? " [Spiel läuft]" : " [Einrichtung]") + ModsHint(room.HostMods));
                if (room.HostMods != null)
                    TooltipHandler.TipRegion(roomRect, "Host: " + room.HostMods.GameVersion + ", " + room.HostMods.ModCount +
                        " Mods, Prüfsumme " + room.HostMods.ShortHash + "\nDu: " +
                        (manifestSummary == null ? "Mod-Prüfung läuft …" : manifestSummary.GameVersion + ", " +
                        manifestSummary.ModCount + " Mods, Prüfsumme " + manifestSummary.ShortHash));
                if (Widgets.ButtonText(new Rect(x + width - 105f, y, 100f, 28f), "Beitreten", true, true, !locked))
                    Send(MessageType.JoinLobby, LobbyPayloads.GuidValue(room.Id));
                y += 33f;
            }
            if (listPage > 0 && Widgets.ButtonText(new Rect(x, y, 100f, 28f), "Zurück")) listPage--;
            if (start + 5 < available.Count && Widgets.ButtonText(new Rect(x + 110f, y, 100f, 28f), "Weiter")) listPage++;
        }

        private void DrawLobby(float x, ref float y, float width)
        {
            if (lobby == null || client == null) return;
            Widgets.Label(new Rect(x, y, width, 28f), lobby.Name + " (" + lobby.Players.Count + "/" + lobby.Capacity + ")");
            y += 33f;
            if (lobby.Phase != SetupPhase.Lobby) { DrawSetup(x, ref y, width); return; }
            foreach (PlayerView player in lobby.Players)
            {
                string label = player.Name + (player.Id == lobby.HostId ? " [Host]" : "") +
                    (player.Ready ? " – bereit" : " – nicht bereit");
                string modText = StatusText(player.ModStatus, out Color color);
                GUI.color = color;
                Widgets.Label(new Rect(x, y, width, 27f), label + modText);
                GUI.color = Color.white;
                y += 29f;
            }
            y += 8f;
            PlayerView? me = lobby.Players.FirstOrDefault(p => p.Id == client.SessionId);
            bool myReady = me?.Ready ?? false;
            bool modsOk = me == null || me.ModStatus == ModStatus.Match || me.ModStatus == ModStatus.Unchecked;
            if (Widgets.ButtonText(new Rect(x, y, 125f, 30f), myReady ? "Nicht bereit" : "Bereit", true, true, modsOk || myReady))
                Send(MessageType.PlayerReadyChanged, LobbyPayloads.Bool(!myReady));
            if (Widgets.ButtonText(new Rect(x + 135f, y, 125f, 30f), "Verlassen"))
                Send(MessageType.LeaveLobby, Array.Empty<byte>());
            if (lobby.HostId == client.SessionId)
            {
                if (width < 410f) y += 36f;
                if (Widgets.ButtonText(new Rect(x + (width < 410f ? 0f : 270f), y, 135f, 30f), "Lobby schließen"))
                    Send(MessageType.CloseLobby, Array.Empty<byte>());
            }
            y += 36f;
            if (me?.ModStatus == ModStatus.Mismatch)
            {
                GUI.color = new Color(1f, 0.45f, 0.45f);
                Widgets.Label(new Rect(x, y, width, 25f), "Deine Mods stimmen nicht mit dem Host überein.");
                GUI.color = Color.white;
                y += 27f;
                bool busy = diffRequested || download != null;
                if (Widgets.ButtonText(new Rect(x, y, 220f, 30f), busy ? "Mod-Liste wird geladen …" : "Unterschiede anzeigen",
                    true, true, !busy && manifest != null))
                {
                    diffRequested = SendBackground(new WireMessage(MessageType.ModManifestRequest, 0, LobbyPayloads.GuidValue(lobby.HostId)));
                    if (diffRequested) status = "Mod-Liste des Hosts wird geladen";
                }
                y += 36f;
            }
            else if (me?.ModStatus == ModStatus.Checking)
            {
                Widgets.Label(new Rect(x, y, width, 25f), "Mod-Prüfung läuft …");
                y += 27f;
            }
            DrawStart(x, ref y, width);
        }

        // START-001: the host starts once at least two players are ready and all mods match (server enforces the same).
        private void DrawStart(float x, ref float y, float width)
        {
            if (lobby == null || client == null) return;
            if (lobby.HostId != client.SessionId)
            {
                bool startable = LobbyRegistry.StartBlockReason(lobby, lobby.HostId) == null;
                Widgets.Label(new Rect(x, y, width, 25f), startable ? "Alle bereit – der Host kann starten." : "Warte auf den Host …");
                return;
            }
            bool solo = hosted?.AllowSoloStart == true;
            string? reason = LobbyRegistry.StartBlockReason(lobby, client.SessionId, solo) ??
                (Current.ProgramState != ProgramState.Entry ? "Nur im Hauptmenü möglich." : null);
            if (Widgets.ButtonText(new Rect(x, y, 180f, 32f), "Spiel starten", true, true, reason == null))
            {
                int players = lobby.Players.Count;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Spiel mit " + players + " Spielern starten? Die Lobby wird gesperrt.",
                    () => Send(MessageType.SetupAdvance, SetupPayloads.Advance(SetupPhase.Scenario)), false, "Spiel starten", WindowLayer.Dialog));
            }
            if (reason != null) Widgets.Label(new Rect(x + 190f, y + 4f, width - 190f, 28f), reason);
            y += 38f;
            if (solo)
            {
                GUI.color = new Color(1f, 0.85f, 0.45f);
                Widgets.Label(new Rect(x, y, width, 25f), "Debug: Allein-Start erlaubt");
                GUI.color = Color.white;
                y += 27f;
            }
        }

        // Shown while the lobby is in setup. Until the shared setup pages exist nothing here may look like
        // a running game (docs/SPEC.md).
        private void DrawSetup(float x, ref float y, float width)
        {
            if (lobby == null || client == null) return;
            Widgets.Label(new Rect(x, y, width, 27f), (lobby.Phase >= SetupPhase.Generating ? "Gemeinsames Spiel" : "Spieleinrichtung läuft") +
                " – Lobby gesperrt (" + PhaseName(lobby.Phase) + ")");
            y += 31f;
            foreach (PlayerView player in lobby.Players)
            {
                Widgets.Label(new Rect(x, y, width, 27f), player.Name + (player.Id == lobby.HostId ? " [Host]" : ""));
                y += 27f;
            }
            y += 8f;
            DrawSetupDetails(x, ref y, width);
            if (lobby.Phase == SetupPhase.SiteSelection && lobby.HostId != client.SessionId && !guestSiteMapActive && guestWorldLoaded)
            {
                if (Widgets.ButtonText(new Rect(x, y, 200f, 30f), "Weltkarte öffnen")) OpenGuestSiteMap();
                y += 36f;
            }
            if (lobby.Phase == SetupPhase.Characters)
            {
                Widgets.Label(new Rect(x, y, width, 25f), DoneLine());
                y += 29f;
                if (lobby.HostId != client.SessionId && !Find.WindowStack.IsOpen<CharacterSelectionWindow>() &&
                    Widgets.ButtonText(new Rect(x, y, 200f, 30f), "Figurenwahl öffnen"))
                    Find.WindowStack.Add(new CharacterSelectionWindow(this));
                y += 36f;
            }
            bool inGame = lobby.Phase >= SetupPhase.Generating;
            if (lobby.HostId == client.SessionId)
            {
                if (Widgets.ButtonText(new Rect(x, y, 200f, 30f), inGame ? "Sitzung beenden" : "Einrichtung abbrechen"))
                {
                    if (inGame) ConfirmAbort();
                    else Send(MessageType.SetupAbort, Array.Empty<byte>());
                }
            }
            else if (Widgets.ButtonText(new Rect(x, y, inGame ? 170f : 125f, 30f), inGame ? "Sitzung verlassen" : "Verlassen"))
            {
                if (inGame) ConfirmLeave();
                else Send(MessageType.LeaveLobby, Array.Empty<byte>());
            }
            y += 36f;
        }

        private void DrawSetupDetails(float x, ref float y, float width)
        {
            if (lobby == null || client == null) return;
            // The host's setup pages only matter until the game starts; afterwards their last page would look current.
            bool started = lobby.Phase >= SetupPhase.Generating;
            SetupSettings? shown = started ? null : lobby.HostId == client.SessionId ? lastMirror : hostSettings;
            if (!started && shown == null)
            {
                Widgets.Label(new Rect(x, y, width, 25f), "Warte auf die Einstellungen des Hosts …");
                y += 29f;
            }
            else if (shown != null)
            {
                Widgets.Label(new Rect(x, y, width, 25f), "Seite des Hosts: " + shown.Page);
                y += 27f;
                foreach (var (label, value) in shown.Entries)
                {
                    string line = "  " + label + ": " + value;
                    float lineHeight = Text.CalcHeight(line, width);
                    Widgets.Label(new Rect(x, y, width, lineHeight), line);
                    y += lineHeight + 2f;
                }
                y += 6f;
            }
            if (lobby.Phase == SetupPhase.WorldTransfer || lobby.Phase >= SetupPhase.Generating) DrawWorldTransfer(x, ref y, width);
            if (lobby.Site != null)
            {
                Widgets.Label(new Rect(x, y, width, 25f), "Startort (vom Host gewählt): " + SiteFlags.Describe(SiteFlags.ToPlanetTile(lobby.Site.Value)));
                y += 29f;
            }
            const string info = "Nach dem Start lädt jeder den Spielstand des Hosts. Die Zeit läuft erst mit dem Gleichlauf (SYNC-001); " +
                "bis dahin bleibt das gemeinsame Spiel für alle pausiert.";
            float infoHeight = Text.CalcHeight(info, width);
            Widgets.Label(new Rect(x, y, width, infoHeight), info);
            y += infoHeight + 10f;
        }

        private void DrawWorldTransfer(float x, ref float y, float width)
        {
            if (lobby == null || client == null) return;
            bool game = lobby.Phase >= SetupPhase.Generating;
            string own = game ? gameText : worldText;
            TransferReceiver? receiving = game ? gameDownload : worldDownload;
            if (receiving != null && receiving.Begin.ChunkCount > 0)
                own = (game ? "Spielstand" : "Welt") + " wird empfangen: " + (100 * receiving.ReceivedChunks / receiving.Begin.ChunkCount) + " %";
            if (own.Length > 0) { Widgets.Label(new Rect(x, y, width, 25f), own); y += 27f; }
            foreach (PlayerView player in lobby.Players)
            {
                string label = "  " + player.Name + ": " + WorldStatusText(player.WorldStatus, lobby.Phase, out Color color);
                GUI.color = color;
                Widgets.Label(new Rect(x, y, width, 25f), label);
                GUI.color = Color.white;
                y += 25f;
            }
            if (lobby.Players.Any(p => p.WorldStatus == WorldStatus.Mismatch))
            {
                string hint = game ? "Das geladene Spiel weicht ab. Meist liegt das an Mods, die beim Laden Figuren, Karten oder IDs ändern."
                    : "Die geladene Welt weicht ab. Meist liegt das an Mods, die Biome, Fraktionen oder Weltobjekte ändern.";
                float hintHeight = Text.CalcHeight(hint, width);
                Widgets.Label(new Rect(x, y, width, hintHeight), hint);
                y += hintHeight;
            }
            y += 8f;
        }

        private static string WorldStatusText(WorldStatus worldStatus, SetupPhase phase, out Color color)
        {
            color = Color.white;
            bool game = phase >= SetupPhase.Generating;
            switch (worldStatus)
            {
                case WorldStatus.Match: return game ? "Spiel geladen, passt" : "Welt geladen, passt";
                case WorldStatus.Mismatch: color = new Color(1f, 0.45f, 0.45f); return game ? "Spiel weicht ab" : "Welt weicht ab";
                case WorldStatus.Waiting: color = new Color(1f, 0.85f, 0.45f); return game ? "lädt das Spiel …" : "wartet auf die Welt";
                default: return "";
            }
        }

        // Panel above the host's setup pages.
        internal void DrawSetupOverlay(Rect inRect)
        {
            if (lobby != null && lobby.Phase >= SetupPhase.Generating) { DrawGameOverlay(inRect); return; }
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 26f),
                "Multiplayer-Einrichtung · " + (lobby == null ? "getrennt" : PhaseName(lobby.Phase)));
            string second = (lobby?.Players.Count ?? 0) + " Spieler sehen deine Auswahl live.";
            if (lobby?.Phase == SetupPhase.WorldTransfer)
            {
                int loaded = lobby.Players.Count(p => p.WorldStatus == WorldStatus.Match);
                string[] differ = lobby.Players.Where(p => p.WorldStatus == WorldStatus.Mismatch).Select(p => p.Name).ToArray();
                second = differ.Length > 0 ? "Welt weicht ab bei: " + string.Join(", ", differ)
                    : worldUpload != null ? "Welt wird gesendet: " + (100 * worldUpload.AcknowledgedChunks / Math.Max(1u, worldUpload.ChunkCount)) + " %"
                    : "Welt geladen: " + loaded + "/" + lobby.Players.Count;
            }
            bool isHost = lobby != null && client != null && lobby.HostId == client.SessionId;
            if (lobby?.Phase == SetupPhase.SiteSelection)
                second = isHost ? "Du wählst den Startort. Fähnchen sind Vorschläge." : "Nur der Host wählt. „Weiter“ setzt dein Fähnchen.";
            Widgets.Label(new Rect(inRect.x, inRect.y + 26f, inRect.width, 26f), second);
            float buttonY = inRect.y + 58f;
            if (lobby?.Phase == SetupPhase.SiteSelection)
            {
                if (Widgets.ButtonText(new Rect(inRect.x, buttonY, 105f, 30f), "Fähnchen hier")) PlaceFlagAtSelection();
                if (Widgets.ButtonText(new Rect(inRect.x + 110f, buttonY, 95f, 30f), "Entfernen"))
                    SendBackground(new WireMessage(MessageType.SiteFlag, requestId++, SetupPayloads.Flag(null)));
                if (Widgets.ButtonText(new Rect(inRect.x + 210f, buttonY, 110f, 30f), "Chat / Lobby")) OpenWindow();
                float listY = buttonY + 36f;
                var flagged = lobby.Players.Select((p, i) => (Player: p, Index: i)).Where(e => e.Player.Flag != null).Take(4).ToList();
                if (flagged.Count == 0)
                    Widgets.Label(new Rect(inRect.x, listY, inRect.width, 24f), "Noch keine Fähnchen.");
                foreach (var (player, index) in flagged)
                {
                    PlanetTile tile = SiteFlags.ToPlanetTile(player.Flag!.Value);
                    GUI.color = SiteFlags.ColorFor(index);
                    Widgets.Label(new Rect(inRect.x, listY + 3f, inRect.width - 70f, 24f), player.Name + ": " + SiteFlags.Describe(tile));
                    GUI.color = Color.white;
                    if (Widgets.ButtonText(new Rect(inRect.x + inRect.width - 64f, listY, 64f, 26f), "Hin")) SiteFlags.JumpTo(tile);
                    listY += 28f;
                }
                if (isHost && Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 30f, 140f, 28f), "Abbrechen")) ConfirmAbort();
                return;
            }
            if (lobby?.Phase == SetupPhase.Characters && isHost)
            {
                DrawHostCharacterPanel(inRect, buttonY);
                return;
            }
            if (Widgets.ButtonText(new Rect(inRect.x, buttonY, 140f, 30f), "Chat / Lobby")) OpenWindow();
            if (isHost && Widgets.ButtonText(new Rect(inRect.x + 150f, buttonY, 140f, 30f), "Abbrechen")) ConfirmAbort();
        }

        // SETUP-005: panel in the shared game, for host and guests, until lockstep exists.
        private void DrawGameOverlay(Rect inRect)
        {
            if (lobby == null || client == null) return;
            bool isHost = lobby.HostId == client.SessionId;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 26f), "Multiplayer · " + PhaseName(lobby.Phase));
            string second = gameUpload != null
                ? "Spielstand wird gesendet: " + (100 * gameUpload.AcknowledgedChunks / Math.Max(1u, gameUpload.ChunkCount)) + " %"
                : lobby.Phase == SetupPhase.Playing ? "Alle sind gelandet."
                : "Spiel geladen: " + lobby.Players.Count(p => p.WorldStatus == WorldStatus.Match) + "/" + lobby.Players.Count;
            Widgets.Label(new Rect(inRect.x, inRect.y + 24f, inRect.width, 24f), second);
            GUI.color = new Color(1f, 0.85f, 0.45f);
            Widgets.Label(new Rect(inRect.x, inRect.y + 46f, inRect.width, 24f), "Pausiert bis zum Gleichlauf (SYNC-001)");
            GUI.color = Color.white;
            float y = inRect.y + 72f;
            MultiplayerSession? session = MultiplayerSession.ForCurrentGame;
            List<Pawn> colonists = session?.Active == true ? GameFingerprint.Colonists() : new List<Pawn>();
            foreach (PlayerView player in lobby.Players.Take(4))
            {
                string key = player.Key.ToString(), pawn = "";
                if (session != null)
                    foreach (var (pawnId, owner) in session.Owners)
                        if (owner == key) pawn = " – " + (colonists.FirstOrDefault(p => p.thingIDNumber == pawnId)?.Name?.ToStringShort ?? "?");
                string line = player.Name + pawn + ": " + WorldStatusText(player.WorldStatus, lobby.Phase, out Color color);
                GUI.color = color;
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 24f), line);
                GUI.color = Color.white;
                y += 24f;
            }
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 30f, 140f, 28f), "Chat / Lobby")) OpenWindow();
            if (Widgets.ButtonText(new Rect(inRect.x + 150f, inRect.yMax - 30f, 160f, 28f), isHost ? "Sitzung beenden" : "Sitzung verlassen"))
            {
                if (isHost) ConfirmAbort();
                else ConfirmLeave();
            }
        }

        // SETUP-004, host: claim the pawn shown on the vanilla page, mark yourself done, see who owns what.
        private void DrawHostCharacterPanel(Rect inRect, float buttonY)
        {
            if (lobby == null || client == null) return;
            PlayerView? me = lobby.Players.FirstOrDefault(p => p.Id == client.SessionId);
            Pawn? shown = null;
            if (SetupMirror.CurrentPage() is Page_ConfigureStartingPawns page && Find.GameInitData != null)
            {
                int index = CurPawnIndex(page);
                if (index >= 0 && index < Find.GameInitData.startingAndOptionalPawns.Count) shown = Find.GameInitData.startingAndOptionalPawns[index];
            }
            PlayerKey? shownOwner = shown == null ? null : characterHost.OwnerOf(shown);
            if (me != null && shown != null)
            {
                bool mine = shownOwner == me.Key;
                if (Widgets.ButtonText(new Rect(inRect.x, buttonY, 105f, 30f), mine ? "Freigeben" : "Übernehmen", true, true, mine || shownOwner == null))
                {
                    string? error = characterHost.Apply(new PawnRequest(mine ? PawnRequestKind.Release : PawnRequestKind.Claim,
                        shown.thingIDNumber, 0, "", me.Key), Find.GameInitData!);
                    if (error != null) Messages.Message(error, MessageTypeDefOf.RejectInput, false);
                    nextMirrorAt = 0f; // publish the roster right away
                }
            }
            bool myDone = me?.Ready ?? false;
            if (Widgets.ButtonText(new Rect(inRect.x + 110f, buttonY, 95f, 30f), myDone ? "Nicht fertig" : "Fertig"))
                Send(MessageType.PlayerReadyChanged, LobbyPayloads.Bool(!myDone));
            if (Widgets.ButtonText(new Rect(inRect.x + 210f, buttonY, 110f, 30f), "Chat / Lobby")) OpenWindow();
            float listY = buttonY + 36f;
            Widgets.Label(new Rect(inRect.x, listY, inRect.width, 24f), DoneLine());
            listY += 26f;
            if (Find.GameInitData != null)
                foreach (PlayerView player in lobby.Players.Take(4))
                {
                    Pawn? owned = Find.GameInitData.startingAndOptionalPawns.FirstOrDefault(p => characterHost.OwnerOf(p) == player.Key);
                    Widgets.Label(new Rect(inRect.x, listY, inRect.width, 24f), player.Name + ": " + (owned?.Name?.ToStringShort ?? "keine Figur") +
                        (player.Ready ? " ✓" : ""));
                    listY += 24f;
                }
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 30f, 140f, 28f), "Abbrechen")) ConfirmAbort();
        }

        private string DoneLine()
        {
            if (lobby == null) return "";
            string[] waiting = lobby.Players.Where(p => !p.Ready).Select(p => p.Name).ToArray();
            return "Fertig: " + lobby.Players.Count(p => p.Ready) + "/" + lobby.Players.Count +
                (waiting.Length > 0 ? " · Warte auf: " + string.Join(", ", waiting) : " · alle fertig");
        }

        /// <summary>Why the host's final start on the character page is blocked, or null when it may go on (SETUP-005).</summary>
        internal string? StartGuardText()
        {
            if (lobby == null) return "Keine Lobby.";
            if (startRequested || lobby.Phase != SetupPhase.Characters) return "Der gemeinsame Spielstart läuft schon.";
            return lobby.Players.All(p => p.Ready) ? null : "Noch nicht alle fertig – " + DoneLine() + ".";
        }

        /// <summary>SETUP-005, host: vanilla wants to start the game. Ask the server first; start once it is in Generating.</summary>
        internal bool TryBeginSharedStart()
        {
            if (allowGameStart) { allowGameStart = false; return true; }
            if (lobby == null || lobby.Phase != SetupPhase.Characters || !lobby.Players.All(p => p.Ready))
            {
                Messages.Message(StartGuardText() ?? "Der Spielstart ist gerade nicht möglich.", MessageTypeDefOf.RejectInput, false);
                return false;
            }
            if (!startRequested &&
                SendBackground(new WireMessage(MessageType.SetupAdvance, requestId++, SetupPayloads.Advance(SetupPhase.Generating))))
            {
                startRequested = true; advancePending = true;
                status = "Gemeinsamer Spielstart angefragt …";
                // Vanilla closes the character page right after this call. Without a window the main menu drops the
                // set-up game before the server answers, and the start then ran vanilla's random quick-test game (2026-10-16).
                OpenWindow();
            }
            return false;
        }

        // SETUP-005, host: the server is in Generating. Remember who owns which pawn, then let vanilla start the game.
        private void StartSharedGame()
        {
            if (lobby == null) return;
            startOwners = characterHost.SnapshotOwners();
            startPlayers = lobby.Players.Select(p => (p.Key, p.Name)).ToList();
            bool requested = startRequested;
            startRequested = false;
            if (!requested) return;
            if (Find.GameInitData == null)
            {
                // Without it vanilla starts a quick-test game (random world, site and pawns) and everyone would get that.
                Log.Error("Multiplayer game start: the set-up game is gone (no GameInitData); setup aborted.");
                Messages.Message("Spielstart abgebrochen: Die Einrichtung ging verloren.", MessageTypeDefOf.RejectInput, false);
                Send(MessageType.SetupAbort, Array.Empty<byte>());
                return;
            }
            gameText = "Spiel wird erzeugt …";
            allowGameStart = true;
            try { PageUtility.InitGameStart(); }
            catch (Exception error)
            {
                allowGameStart = false;
                Log.Error("Multiplayer game start failed: " + error);
                gameText = "Spielstart fehlgeschlagen: " + error.Message;
            }
        }

        // The server refused the start (someone took back "done" in the meantime): back to the character page.
        private void ReopenCharacterPage()
        {
            if (Current.ProgramState != ProgramState.Entry || Find.GameInitData == null || Find.WindowStack.IsOpen<Page_ConfigureStartingPawns>()) return;
            Find.WindowStack.Add(new Page_ConfigureStartingPawns { nextAct = PageUtility.InitGameStart });
            Find.WindowStack.TryRemove(typeof(FoundationWindow), false); // it only held the game while the start was asked for
        }

        private void ConfirmLeave() =>
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Multiplayer-Sitzung verlassen? Dein Spiel läuft dann nur bei dir weiter.",
                () => Send(MessageType.LeaveLobby, Array.Empty<byte>()), true, "Sitzung verlassen", WindowLayer.Super));

        // SETUP-004, guest window.
        internal void DrawCharacterSelection(Rect inRect)
        {
            if (lobby == null || client == null || lobby.Phase != SetupPhase.Characters)
            { Widgets.Label(inRect, "Keine Figurenwahl aktiv."); return; }
            PlayerView? me = lobby.Players.FirstOrDefault(p => p.Id == client.SessionId);
            PlayerKey? myKey = me?.Key;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 28f), "Figurenwahl – " + DoneLine());
            if (pawnRoster == null) { Widgets.Label(new Rect(inRect.x, inRect.y + 34f, inRect.width, 28f), "Warte auf die Figuren des Hosts …"); return; }
            PawnCard? myPawn = pawnRoster.Cards.FirstOrDefault(c => c.Selected && c.Owner == myKey);
            if (chosenPawnId == null || pawnRoster.Cards.All(c => c.PawnId != chosenPawnId))
                chosenPawnId = (myPawn ?? pawnRoster.Cards.FirstOrDefault())?.PawnId;
            float top = inRect.y + 34f, bottom = inRect.yMax - 44f;
            // Left: starting group and left-behind pawns.
            Rect list = new Rect(inRect.x, top, 290f, bottom - top);
            Widgets.DrawMenuSection(list);
            float y = list.y + 8f;
            foreach (bool selected in new[] { true, false })
            {
                Widgets.Label(new Rect(list.x + 8f, y, list.width - 16f, 24f), selected ? "Startgruppe" : "Zurückgelassen");
                y += 26f;
                foreach (PawnCard card in pawnRoster.Cards.Where(c => c.Selected == selected))
                {
                    string ownerName = card.Owner == null ? "frei" : lobby.Players.FirstOrDefault(p => p.Key == card.Owner)?.Name ?? "?";
                    if (card.PawnId == chosenPawnId) GUI.color = new Color(1f, 0.85f, 0.45f);
                    else if (card.Owner == myKey && myKey != null) GUI.color = new Color(0.55f, 0.9f, 0.55f);
                    if (Widgets.ButtonText(new Rect(list.x + 8f, y, list.width - 16f, 28f), card.ShortName + " – " + ownerName)) chosenPawnId = card.PawnId;
                    GUI.color = Color.white;
                    y += 31f;
                }
                y += 6f;
            }
            // Right: the chosen pawn's card and what this player may do with it.
            PawnCard? chosen = pawnRoster.Cards.FirstOrDefault(c => c.PawnId == chosenPawnId);
            Rect detail = new Rect(list.xMax + 10f, top, inRect.width - list.width - 10f, bottom - top);
            Widgets.DrawMenuSection(detail);
            if (chosen != null) DrawPawnCard(chosen, myKey, myPawn, detail.ContractedBy(10f));
            // Bottom: done and chat.
            bool myDone = me?.Ready ?? false;
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 36f, 160f, 32f), myDone ? "Nicht fertig" : "Fertig", true, true, myDone || myPawn != null))
                Send(MessageType.PlayerReadyChanged, LobbyPayloads.Bool(!myDone));
            if (myPawn == null) Widgets.Label(new Rect(inRect.x + 170f, inRect.yMax - 32f, 320f, 28f), "Erst eine Figur der Startgruppe übernehmen.");
            if (Widgets.ButtonText(new Rect(inRect.xMax - 150f, inRect.yMax - 36f, 150f, 32f), "Chat / Lobby")) OpenWindow();
        }

        private void DrawPawnCard(PawnCard card, PlayerKey? myKey, PawnCard? myPawn, Rect rect)
        {
            float y = rect.y;
            void Line(string text, float gap = 2f)
            {
                float h = Text.CalcHeight(text, rect.width);
                Widgets.Label(new Rect(rect.x, y, rect.width, h), text);
                y += h + gap;
            }
            Line(card.Name, 4f);
            Line(card.Summary);
            if (card.Childhood.Length > 0) Line("Kindheit: " + card.Childhood);
            if (card.Adulthood.Length > 0) Line("Erwachsen: " + card.Adulthood);
            if (card.Traits.Count > 0) Line("Merkmale: " + string.Join(", ", card.Traits));
            if (card.Incapable.Count > 0) Line("Unfähig zu: " + string.Join(", ", card.Incapable));
            if (card.Health.Count > 0) Line("Gesundheit: " + string.Join(", ", card.Health));
            y += 4f;
            float column = (rect.width - 10f) / 2f;
            for (int i = 0; i < card.Skills.Count; i++)
            {
                var (skill, level, passion) = card.Skills[i];
                string text = skill + ": " + (level == 255 ? "–" : level.ToString()) + (passion == 2 ? " ++" : passion == 1 ? " +" : "");
                Widgets.Label(new Rect(rect.x + (i % 2) * (column + 10f), y + (i / 2) * 22f, column, 22f), text);
            }
            y += ((card.Skills.Count + 1) / 2) * 22f + 10f;
            bool mine = card.Owner == myKey && myKey != null;
            float x = rect.x;
            void Button(string label, PawnRequest request)
            {
                if (Widgets.ButtonText(new Rect(x, y, 150f, 30f), label))
                    SendBackground(new WireMessage(MessageType.PawnRequest, requestId++, request.ToPayload()));
                x += 158f;
            }
            if (card.Selected && card.Owner == null && myPawn == null) Button("Übernehmen", new PawnRequest(PawnRequestKind.Claim, card.PawnId));
            if (mine)
            {
                Button("Neu würfeln", new PawnRequest(PawnRequestKind.Reroll, card.PawnId));
                Button("Freigeben", new PawnRequest(PawnRequestKind.Release, card.PawnId));
                y += 38f; x = rect.x;
                renameText = Widgets.TextField(new Rect(x, y, 200f, 30f), renameText);
                x += 208f;
                if (renameText.Trim().Length > 0) Button("Umbenennen", new PawnRequest(PawnRequestKind.Rename, card.PawnId, 0, renameText.Trim()));
            }
            if (!card.Selected && card.Owner == null && myPawn != null)
                Button("Mit meiner Figur tauschen", new PawnRequest(PawnRequestKind.Swap, myPawn.PawnId, card.PawnId));
        }

        private void ConfirmAbort()
        {
            bool inGame = lobby?.Phase >= SetupPhase.Generating;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                inGame ? "Multiplayer-Sitzung für alle beenden? Jeder behält seinen Spielstand." : "Gemeinsame Einrichtung für alle abbrechen?",
                () => Send(MessageType.SetupAbort, Array.Empty<byte>()), true, inGame ? "Sitzung beenden" : "Einrichtung abbrechen", WindowLayer.Super));
        }

        // SETUP-003, guest: the received world on the vanilla starting-site page, in suggestion-only mode.
        private void OpenGuestSiteMap()
        {
            if (Current.ProgramState != ProgramState.Entry || Find.World == null) return;
            guestSiteMapActive = true;
            Find.WindowStack.TryRemove(typeof(FoundationWindow), false);
            Find.WindowStack.Add(new Page_SelectStartingSite());
            Find.WindowStack.Add(new SetupOverlayWindow(this));
        }

        internal void CloseGuestSiteMap(bool reopenWindow)
        {
            if (!guestSiteMapActive) return;
            guestSiteMapActive = false;
            foreach (Page_SelectStartingSite page in Find.WindowStack.Windows.OfType<Page_SelectStartingSite>().ToList())
                Find.WindowStack.TryRemove(page, false);
            Find.WindowStack.TryRemove(typeof(SetupOverlayWindow), false);
            if (reopenWindow) OpenWindow();
        }

        /// <summary>Sets (or moves) this player's flag to the selected world tile, if a colony could start there.</summary>
        internal void PlaceFlagAtSelection()
        {
            if (lobby?.Phase != SetupPhase.SiteSelection || Find.World == null) return;
            PlanetTile tile = Find.WorldSelector.SelectedTile;
            if (!tile.Valid) { Messages.Message("Erst ein Feld auf der Weltkarte auswählen.", MessageTypeDefOf.RejectInput, false); return; }
            var reason = new System.Text.StringBuilder();
            if (!TileFinder.IsValidTileForNewSettlement(tile, reason, false))
            { Messages.Message("Hier kann keine Kolonie starten: " + reason, MessageTypeDefOf.RejectInput, false); return; }
            if (SendBackground(new WireMessage(MessageType.SiteFlag, requestId++, SetupPayloads.Flag(SiteFlags.ToSiteTile(tile)))))
                Messages.Message("Fähnchen gesetzt: " + SiteFlags.Describe(tile), MessageTypeDefOf.SilentInput, false);
        }

        // Flags shown on every player's map, from the latest lobby state.
        private void UpdateSiteFlags()
        {
            if (WorldSelfTest.ShowingSitePage) return;
            if (lobby?.Phase != SetupPhase.SiteSelection) { SiteFlags.Shown = Array.Empty<(string, PlanetTile, Color)>(); return; }
            SiteFlags.Shown = lobby.Players.Select((p, i) => (Player: p, Index: i)).Where(e => e.Player.Flag != null)
                .Select(e => (e.Player.Name, SiteFlags.ToPlanetTile(e.Player.Flag!.Value), SiteFlags.ColorFor(e.Index))).ToList();
        }

        private void OnPhaseChanged(SetupPhase before, SetupPhase now)
        {
            if (lobby == null || client == null) return;
            if (before == SetupPhase.Lobby && now != SetupPhase.Lobby && lobby.HostId == client.SessionId) BeginHostSetup();
            bool guest = lobby.HostId != client.SessionId;
            if (!guest && before == SetupPhase.Characters && now == SetupPhase.Generating) StartSharedGame();
            if (now != SetupPhase.Characters && before == SetupPhase.Characters) ResetCharacters();
            if (guest && now == SetupPhase.Generating && before != SetupPhase.Generating)
            {
                gameText = "Der Host erzeugt das Spiel …";
                if (Current.ProgramState == ProgramState.Entry) OpenWindow();
            }
            if (now == SetupPhase.Playing && before != SetupPhase.Playing)
                status = "Alle sind gelandet. Das Spiel bleibt pausiert, bis der Gleichlauf fertig ist (SYNC-001).";
            if (guest && now == SetupPhase.Characters && before != SetupPhase.Characters)
            {
                Find.WindowStack.TryRemove(typeof(FoundationWindow), false);
                Find.WindowStack.Add(new CharacterSelectionWindow(this));
            }
            if (guest && now == SetupPhase.SiteSelection && before != SetupPhase.SiteSelection && guestWorldLoaded) OpenGuestSiteMap();
            if (guest && before == SetupPhase.SiteSelection && now != SetupPhase.SiteSelection && guestSiteMapActive)
            {
                CloseGuestSiteMap(true);
                if (lobby.Site != null) status = "Der Host hat den Startort gewählt: " + SiteFlags.Describe(SiteFlags.ToPlanetTile(lobby.Site.Value));
            }
            if (now == SetupPhase.Lobby && before != SetupPhase.Lobby)
            {
                hostSettings = null; lastMirror = null;
                ResetWorld();
                ResetGame();
                if (hostSetupActive) { EndHostSetup(true); status = "Einrichtung abgebrochen"; }
                if (before >= SetupPhase.Generating && Current.ProgramState == ProgramState.Playing)
                {
                    status = "Multiplayer-Sitzung beendet – dieses Spiel läuft jetzt nur bei dir weiter.";
                    Messages.Message(status, MessageTypeDefOf.NeutralEvent, false);
                }
            }
        }

        private void BeginHostSetup()
        {
            if (Current.ProgramState != ProgramState.Entry)
            { Send(MessageType.SetupAbort, Array.Empty<byte>()); status = "Einrichtung nur aus dem Hauptmenü möglich"; return; }
            hostSetupActive = true; lastMirror = null; pageMissingSince = null; advancePending = false; nextMirrorAt = 0f;
            Find.WindowStack.TryRemove(typeof(FoundationWindow), false);
            Find.WindowStack.Add(new Page_SelectScenario());
            Find.WindowStack.Add(new SetupOverlayWindow(this));
            status = "Einrichtung läuft – deine Auswahl wird geteilt";
        }

        private void EndHostSetup(bool reopenWindow)
        {
            hostSetupActive = false; advancePending = false;
            if (Current.ProgramState == ProgramState.Entry)
                foreach (Page page in Find.WindowStack.Windows.OfType<Page>().ToList()) Find.WindowStack.TryRemove(page, false);
            Find.WindowStack.TryRemove(typeof(SetupOverlayWindow), false);
            if (reopenWindow) OpenWindow();
        }

        // Called every frame; reads the host's page twice per second.
        private void UpdateHostSetup()
        {
            if (!hostSetupActive) return;
            if (client == null || lobby == null || lobby.HostId != client.SessionId) { EndHostSetup(true); return; }
            if (Time.realtimeSinceStartup < nextMirrorAt) return;
            nextMirrorAt = Time.realtimeSinceStartup + 0.5f;
            if (lobby.Phase >= SetupPhase.Generating) { UpdateHostGame(); return; }
            Page? page = SetupMirror.CurrentPage();
            if (page == null)
            {
                // world generation runs without a page; vanilla may close the character page while the start is asked for
                if (LongEventHandler.AnyEventNowOrWaiting || startRequested) return;
                pageMissingSince ??= Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - pageMissingSince.Value < 1.5f) return;
                // The host left the setup pages (Back on the first page): end the shared setup for everyone.
                hostSetupActive = false;
                Find.WindowStack.TryRemove(typeof(SetupOverlayWindow), false);
                Send(MessageType.SetupAbort, Array.Empty<byte>());
                OpenWindow();
                return;
            }
            pageMissingSince = null;
            SetupSettings mirror = SetupMirror.Read(page);
            if (!mirror.SameAs(lastMirror) && SendBackground(new WireMessage(MessageType.SetupSettings, 0, mirror.ToPayload())))
                lastMirror = mirror;
            SetupPhase? wanted = SetupMirror.PhaseOf(page);
            if (lobby.Phase == SetupPhase.WorldTransfer && !HostWorldReady()) return;
            if (lobby.Phase == SetupPhase.Characters && page is Page_ConfigureStartingPawns && Find.GameInitData != null)
                PublishRoster(Find.GameInitData);
            if (wanted == null || advancePending || lobby.Phase >= wanted.Value) return;
            // The server only accepts the next phase, so walk forward one step per answer.
            if (lobby.Phase == SetupPhase.SiteSelection)
            {
                GameInitData? init = Find.GameInitData;
                if (init == null || !init.startingTile.Valid) return;
                var site = new SiteTile(init.startingTile.tileId, init.startingTile.Layer.LayerID);
                advancePending = SendBackground(new WireMessage(MessageType.SiteDecided, requestId++, SetupPayloads.Site(site)));
            }
            else
                advancePending = SendBackground(new WireMessage(MessageType.SetupAdvance, requestId++,
                    SetupPayloads.Advance((SetupPhase)((byte)lobby.Phase + 1))));
        }

        // SETUP-005, host: after vanilla's map generation, save once, announce the fingerprint and send the save;
        // land everyone as soon as every guest loaded the same game.
        private void UpdateHostGame()
        {
            if (lobby == null || Current.ProgramState != ProgramState.Playing || LongEventHandler.AnyEventNowOrWaiting || Find.Maps.Count == 0) return;
            EnsureSessionOverlay();
            MultiplayerSession? session = MultiplayerSession.ForCurrentGame;
            if (session == null || lobby.Phase != SetupPhase.Generating) return;
            if (gameFileName == null)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                session.Begin(lobby.Id, startPlayers ?? lobby.Players.Select(p => (p.Key, p.Name)).ToList(),
                    startOwners ?? new Dictionary<int, PlayerKey>(), GameFingerprint.Colonists().Select(p => p.thingIDNumber).ToList());
                gameFileName = GameCodec.FileNameFor(lobby.Name);
                saveStartedUtc = DateTime.UtcNow;
                gameText = "Spielstand wird gespeichert …";
                try { GameDataSaveLoader.SaveGame(gameFileName); }
                catch (Exception error)
                {
                    Log.Error("Multiplayer game save failed: " + error);
                    gameText = "Spielstand konnte nicht gespeichert werden: " + error.Message;
                }
                return;
            }
            if (hostSaveFile == null)
            {
                byte[]? raw = GameCodec.ReadFinishedSave(gameFileName, saveStartedUtc);
                if (raw == null) return;
                hostSaveFile = raw;
                sentGame = GameCodec.Gzip(raw);
                resendGame = lobby.Players.Count > 1;
                StartGameUpload(); // runs while the host reloads
                // Everyone plays the loaded save, the host too: lockstep needs the same state everywhere, and only the
                // loaded state is the same on every machine (the live game may hold more than the save, ADR 0023).
                reloadFrom = Current.Game;
                gameText = "Spielstand gesendet (" + (sentGame.Length / 1024) + " KiB); wird neu geladen …";
                GameDataSaveLoader.LoadGame(gameFileName);
                return;
            }
            StartGameUpload();
            if (gameFingerprint == null)
            {
                if (ReferenceEquals(Current.Game, reloadFrom)) return; // the reload has not happened yet
                if (!session.Active) { gameText = "Das neu geladene Spiel enthält keine Multiplayer-Sitzung."; return; }
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                var (fingerprint, lines) = GameFingerprint.Compute(hostSaveFile, session);
                gameFingerprint = fingerprint;
                SendBackground(new WireMessage(MessageType.GameReady, requestId++, SetupPayloads.Fingerprint(fingerprint)));
                gameText = "Spielstand gesendet (" + (sentGame!.Length / 1024) + " KiB, Prüfsumme " + GameFingerprint.Short(fingerprint) + ")";
                Log.Message("Multiplayer Foundation: " + gameText + "; " + GameFingerprint.LogSummary(lines));
                return;
            }
            if (!advancePending && gameUpload == null && lobby.Players.All(p => p.WorldStatus == WorldStatus.Match))
                advancePending = SendBackground(new WireMessage(MessageType.SetupAdvance, requestId++, SetupPayloads.Advance(SetupPhase.Playing)));
        }

        private void StartGameUpload()
        {
            if (!resendGame || gameUpload != null || sentGame == null) return;
            resendGame = false;
            gameUpload = new TransferSender(TransferKind.Game, Guid.Empty, sentGame);
            SendAllBackground(gameUpload.Start(NowMs()));
        }

        // SETUP-005, guest: writes the received save into the save folder and loads it like "Load game" does.
        private void LoadGuestGame(byte[] gzipped)
        {
            if (lobby == null) return;
            byte[] raw;
            try { raw = GameCodec.Gunzip(gzipped); }
            catch (Exception error) { gameText = "Spielstand unlesbar: " + error.Message; return; }
            // Sent again for another guest after a failed transfer: nothing to do if this one already runs here.
            if (guestGameFile != null && !guestGameLoading && Current.ProgramState == ProgramState.Playing && raw.SequenceEqual(guestGameFile)) return;
            gameFileName ??= GameCodec.FileNameFor(lobby.Name);
            try { System.IO.File.WriteAllBytes(GameCodec.PathFor(gameFileName), raw); }
            catch (Exception error) { gameText = "Spielstand konnte nicht geschrieben werden: " + error.Message; return; }
            guestGameFile = raw; guestGameLoading = true; guestLoadStartedAt = Time.realtimeSinceStartup;
            gameText = "Spielstand wird geladen …";
            Find.WindowStack.TryRemove(typeof(FoundationWindow), false);
            Find.WindowStack.TryRemove(typeof(CharacterSelectionWindow), false);
            CloseGuestSiteMap(false);
            if (guestWorldLoaded) { guestWorldLoaded = false; WorldCodec.Discard(); }
            GameDataSaveLoader.LoadGame(gameFileName);
        }

        // SETUP-005, guest: once the received game is loaded, report its fingerprint; it then stays paused.
        private void UpdateGuestGame()
        {
            if (lobby == null || client == null || lobby.HostId == client.SessionId || lobby.Phase < SetupPhase.Generating) return;
            if (Current.ProgramState == ProgramState.Playing && !LongEventHandler.AnyEventNowOrWaiting) EnsureSessionOverlay();
            if (!guestGameLoading || LongEventHandler.AnyEventNowOrWaiting) return;
            if (Current.ProgramState != ProgramState.Playing)
            {
                // vanilla goes back to the main menu when loading fails
                if (Current.ProgramState == ProgramState.Entry && Time.realtimeSinceStartup - guestLoadStartedAt > 3f)
                { guestGameLoading = false; gameText = "Spielstand konnte nicht geladen werden (siehe Log)."; OpenWindow(); }
                return;
            }
            guestGameLoading = false;
            MultiplayerSession? session = MultiplayerSession.ForCurrentGame;
            if (session == null || !session.Active || guestGameFile == null)
            { gameText = "Der geladene Spielstand enthält keine Multiplayer-Sitzung."; return; }
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            var (fingerprint, lines) = GameFingerprint.Compute(guestGameFile, session);
            SendBackground(new WireMessage(MessageType.GameLoaded, requestId++, SetupPayloads.Fingerprint(fingerprint)));
            gameText = "Spiel geladen (Prüfsumme " + GameFingerprint.Short(fingerprint) + ")";
            Log.Message("Multiplayer Foundation: " + gameText + "; " + GameFingerprint.LogSummary(lines));
        }

        private void EnsureSessionOverlay()
        {
            if (Current.ProgramState == ProgramState.Playing && !Find.WindowStack.IsOpen<SetupOverlayWindow>())
                Find.WindowStack.Add(new SetupOverlayWindow(this));
        }

        private void ResetGame()
        {
            startRequested = false; allowGameStart = false; resendGame = false; startOwners = null; startPlayers = null;
            gameFileName = null; sentGame = null; gameFingerprint = null; hostSaveFile = null; reloadFrom = null; gameUpload = null; gameDownload = null;
            guestGameFile = null; guestGameLoading = false; gameText = "";
            if (Current.ProgramState == ProgramState.Playing) Find.WindowStack.TryRemove(typeof(SetupOverlayWindow), false);
        }

        // SETUP-004, host: one pawn per player at least, then the roster whenever it changed.
        private void PublishRoster(GameInitData init)
        {
            if (lobby == null) return;
            characterHost.EnsurePawnCount(init, lobby.Players.Count);
            byte[] payload = characterHost.Build(init, lobby.Players.Select(p => p.Key).ToList()).ToPayload();
            if (payload.SequenceEqual(lastRosterPayload)) return;
            if (SendBackground(new WireMessage(MessageType.PawnRoster, requestId++, payload))) lastRosterPayload = payload;
        }

        // SETUP-002, host: prepares and sends the generated world; true once every guest loaded it with the same fingerprint.
        private bool HostWorldReady()
        {
            if (lobby == null || LongEventHandler.AnyEventNowOrWaiting) return false;
            World? world = Find.World;
            if (world == null) return false;
            if (!ReferenceEquals(world, sentWorld))
            {
                sentWorld = world;
                worldUpload = null;
                worldText = "Welt wird vorbereitet …";
                LongEventHandler.QueueLongEvent(() =>
                {
                    WorldCodec.Standardize(world);
                    preparedWorld = (WorldFingerprint.Compute(world), WorldCodec.Save(world));
                }, "LoadingLongEvent", false, error =>
                {
                    Log.Error("Multiplayer world save failed: " + error);
                    worldText = "Welt konnte nicht gespeichert werden: " + error.Message;
                });
                return false;
            }
            if (preparedWorld != null)
            {
                var (fingerprint, data) = preparedWorld.Value;
                preparedWorld = null;
                SendBackground(new WireMessage(MessageType.WorldReady, requestId++, SetupPayloads.Fingerprint(fingerprint)));
                if (lobby.Players.Count > 1)
                {
                    worldUpload = new TransferSender(TransferKind.World, Guid.Empty, data);
                    SendAllBackground(worldUpload.Start(NowMs()));
                }
                worldText = "Welt gesendet (" + (data.Length / 1024) + " KiB, Prüfsumme " + WorldFingerprint.Short(fingerprint) + ")";
                Log.Message("Multiplayer Foundation: " + worldText);
                return false;
            }
            return !advancePending && lobby.Players.All(p => p.WorldStatus == WorldStatus.Match);
        }

        // SETUP-002, guest: loads a received world in the main menu and reports its fingerprint.
        private void LoadGuestWorld(byte[] data)
        {
            if (Current.ProgramState != ProgramState.Entry) { worldText = "Welt nur im Hauptmenü ladbar"; return; }
            worldText = "Welt wird geladen …";
            LongEventHandler.QueueLongEvent(() =>
            {
                World world = WorldCodec.Load(data, hostScenarioName);
                guestWorldLoaded = true;
                pendingWorldReport = WorldFingerprint.Compute(world);
            }, "LoadingLongEvent", false, error =>
            {
                Log.Error("Multiplayer world load failed: " + error);
                worldText = "Welt konnte nicht geladen werden: " + error.Message;
            });
        }

        private void ResetCharacters()
        {
            characterHost.Clear(); lastRosterPayload = System.Array.Empty<byte>(); pawnRoster = null; chosenPawnId = null; renameText = "";
            Find.WindowStack.TryRemove(typeof(CharacterSelectionWindow), false);
        }

        private void ResetWorld()
        {
            ResetCharacters();
            CloseGuestSiteMap(false); // before the world goes away
            SiteFlags.Shown = Array.Empty<(string, PlanetTile, Color)>();
            sentWorld = null; preparedWorld = null; worldUpload = null; worldDownload = null; pendingWorldReport = null; worldText = "";
            if (guestWorldLoaded) { guestWorldLoaded = false; WorldCodec.Discard(); }
        }

        private static string PhaseName(SetupPhase phase)
        {
            switch (phase)
            {
                case SetupPhase.Scenario: return "Szenario";
                case SetupPhase.Storyteller: return "Erzähler";
                case SetupPhase.WorldParams: return "Welt erstellen";
                case SetupPhase.WorldTransfer: return "Weltübertragung";
                case SetupPhase.SiteSelection: return "Startort";
                case SetupPhase.Ideology: return "Ideoligion";
                case SetupPhase.Characters: return "Figuren";
                case SetupPhase.Generating: return "Spiel wird erzeugt";
                case SetupPhase.Playing: return "Gelandet (pausiert)";
                default: return "Lobby";
            }
        }

        private static string StatusText(ModStatus modStatus, out Color color)
        {
            color = Color.white;
            switch (modStatus)
            {
                case ModStatus.Match: return " – Mods passen";
                case ModStatus.Mismatch: color = new Color(1f, 0.45f, 0.45f); return " – Mods abweichend";
                case ModStatus.Checking: color = new Color(1f, 0.85f, 0.45f); return " – Mod-Prüfung läuft …";
                default: return "";
            }
        }

        private string ModsHint(ModManifestSummary? host)
        {
            if (host == null) return "";
            string hint = " · " + host.ModCount + " Mods";
            if (manifestSummary == null) return hint;
            return hint + (host.SameAs(manifestSummary) ? " · passt" : " · abweichend") +
                (host.GameVersion != manifestSummary.GameVersion ? " (andere Spielversion)" : "");
        }

        private static long NowMs() => System.Diagnostics.Stopwatch.GetTimestamp() * 1000 / System.Diagnostics.Stopwatch.Frequency;

        // Sends that must not wait for the single UI action slot (transfer messages, mod-list requests).
        private bool SendBackground(WireMessage message)
        {
            if (client == null) return false;
            try { backgroundSends.Add(client.SendAsync(message.Type, message.RequestId, message.Payload)); return true; }
            catch (Exception error) { status = "Senden fehlgeschlagen: " + error.Message; return false; }
        }

        private void SendAllBackground(IEnumerable<WireMessage> messages)
        {
            foreach (WireMessage message in messages) SendBackground(message);
        }

        private void UpdateManifestAndTransfers()
        {
            if (manifestTask != null && manifestTask.IsCompleted)
            {
                try
                {
                    manifest = manifestTask.GetAwaiter().GetResult();
                    manifestSummary = manifest.Summary();
                    Log.Message("Multiplayer Foundation: " + manifest.Mods.Count + " Mods geprüft, Prüfsumme " + manifestSummary.ShortHash);
                }
                catch (Exception error) { Log.Error("Multiplayer mod manifest failed: " + error); status = "Mod-Prüfung fehlgeschlagen: " + error.Message; }
                manifestTask = null;
            }
            if (client != null && manifestSummary != null && manifestSentTo != client &&
                SendBackground(new WireMessage(MessageType.ModManifestSummary, 0, manifestSummary.ToPayload())))
                manifestSentTo = client;
            for (int i = backgroundSends.Count - 1; i >= 0; i--)
            {
                if (!backgroundSends[i].IsCompleted) continue;
                if (backgroundSends[i].IsFaulted) status = "Senden fehlgeschlagen: " + backgroundSends[i].Exception?.GetBaseException().Message;
                backgroundSends.RemoveAt(i);
            }
            if (upload != null)
            {
                SendAllBackground(upload.Poll(NowMs()));
                if (upload.State == TransferState.Failed) Log.Warning("Multiplayer mod-list transfer failed: " + upload.Failure?.Reason);
                if (upload.State != TransferState.Active) upload = null;
            }
            if (worldUpload != null)
            {
                SendAllBackground(worldUpload.Poll(NowMs()));
                if (worldUpload.State == TransferState.Failed)
                {
                    // e.g. a guest left mid-transfer: prepare and send again to whoever is still there
                    Log.Warning("Multiplayer world transfer failed: " + worldUpload.Failure?.Reason);
                    sentWorld = null;
                }
                if (worldUpload.State != TransferState.Active) worldUpload = null;
            }
            if (gameUpload != null)
            {
                SendAllBackground(gameUpload.Poll(NowMs()));
                if (gameUpload.State == TransferState.Failed)
                {
                    // e.g. a guest left mid-transfer: send the same save again to whoever still waits
                    Log.Warning("Multiplayer game transfer failed: " + gameUpload.Failure?.Reason);
                    resendGame = lobby?.Players.Any(p => p.WorldStatus == WorldStatus.Waiting) == true;
                }
                if (gameUpload.State != TransferState.Active) gameUpload = null;
            }
            if (pendingWorldReport != null && client != null)
            {
                byte[] report = pendingWorldReport;
                pendingWorldReport = null;
                SendBackground(new WireMessage(MessageType.WorldLoaded, requestId++, SetupPayloads.Fingerprint(report)));
                worldText = "Welt geladen (Prüfsumme " + WorldFingerprint.Short(report) + ")";
            }
            if (upload == null && client != null && manifest != null && manifestRequests.Count > 0)
            {
                upload = new TransferSender(TransferKind.ModManifest, manifestRequests.Dequeue(), manifest.Serialize());
                SendAllBackground(upload.Start(NowMs()));
            }
        }

        private void HandleTransfer(WireMessage message)
        {
            if (message.Type == MessageType.ModManifestRequest)
            {
                Guid requester = LobbyPayloads.ReadGuid(message.Payload);
                if (!manifestRequests.Contains(requester)) manifestRequests.Enqueue(requester);
                return;
            }
            if (upload != null && upload.Owns(message))
            {
                SendAllBackground(upload.Handle(message, NowMs()));
                return;
            }
            if (worldUpload != null && worldUpload.Owns(message))
            {
                SendAllBackground(worldUpload.Handle(message, NowMs()));
                return;
            }
            if (gameUpload != null && gameUpload.Owns(message))
            {
                SendAllBackground(gameUpload.Handle(message, NowMs()));
                return;
            }
            if (gameDownload != null && gameDownload.Owns(message))
            {
                SendAllBackground(gameDownload.Handle(message));
                if (gameDownload.State == TransferState.Completed) { byte[] data = gameDownload.Data!; gameDownload = null; LoadGuestGame(data); }
                else if (gameDownload.State == TransferState.Failed)
                { gameText = "Spielstandübertragung abgebrochen: " + gameDownload.Failure?.Reason; gameDownload = null; }
                return;
            }
            if (worldDownload != null && worldDownload.Owns(message))
            {
                SendAllBackground(worldDownload.Handle(message));
                if (worldDownload.State == TransferState.Completed) { byte[] data = worldDownload.Data!; worldDownload = null; LoadGuestWorld(data); }
                else if (worldDownload.State == TransferState.Failed)
                { worldText = "Weltübertragung abgebrochen: " + worldDownload.Failure?.Reason; worldDownload = null; }
                return;
            }
            if (message.Type == MessageType.TransferBegin)
            {
                TransferBegin begin = TransferPayloads.ReadBegin(message.Payload);
                if (begin.Kind == TransferKind.ModManifest && diffRequested && download == null)
                {
                    try { download = new TransferReceiver(begin); }
                    catch (ProtocolException error) { diffRequested = false; status = "Mod-Liste des Hosts unlesbar: " + error.Message; }
                }
                else if (begin.Kind == TransferKind.World && lobby?.Phase == SetupPhase.WorldTransfer && lobby.HostId != client?.SessionId)
                {
                    // A newer world (host generated again) replaces an unfinished one.
                    try { worldDownload = new TransferReceiver(begin); worldText = "Welt wird empfangen …"; }
                    catch (ProtocolException error) { worldText = "Welt unlesbar: " + error.Message; }
                }
                else if (begin.Kind == TransferKind.Game && lobby?.Phase == SetupPhase.Generating && lobby.HostId != client?.SessionId)
                {
                    try { gameDownload = new TransferReceiver(begin); gameText = "Spielstand wird empfangen …"; }
                    catch (ProtocolException error) { gameText = "Spielstand unlesbar: " + error.Message; }
                }
                else
                    SendBackground(new WireMessage(MessageType.TransferAbort, 0, TransferPayloads.Abort(
                        new TransferAbort(begin.TransferId, TransferAbortCode.Rejected, "Übertragung wird hier nicht erwartet."))));
                return;
            }
            if (download == null || !download.Owns(message)) return;
            SendAllBackground(download.Handle(message));
            if (download.State == TransferState.Completed)
            {
                try { ShowModDiff(ModManifest.Deserialize(download.Data!)); }
                catch (ProtocolException error) { status = "Mod-Liste des Hosts unlesbar: " + error.Message; }
                download = null; diffRequested = false;
            }
            else if (download.State == TransferState.Failed)
            {
                status = "Übertragung abgebrochen: " + download.Failure?.Reason;
                download = null; diffRequested = false;
            }
        }

        private void ShowModDiff(ModManifest host)
        {
            if (manifest == null) return;
            string hostName = lobby?.Players.FirstOrDefault(p => p.Id == lobby.HostId)?.Name ?? "Host";
            Find.WindowStack.Add(new ModDiffWindow(ModManifestDiff.Compare(manifest, host), hostName));
            status = "Mod-Unterschiede angezeigt";
        }

        private void ResetTransfers()
        {
            upload = null; download = null; diffRequested = false; manifestRequests.Clear(); manifestSentTo = null;
            hostSettings = null; lastMirror = null;
            ResetWorld();
            ResetGame();
            if (hostSetupActive) EndHostSetup(true);
        }

        private bool Send(MessageType type, byte[] payload)
        {
            if (client == null || action != null) return false;
            try { action = client.SendAsync(type, requestId++, payload); return true; }
            catch (Exception error) { status = "Senden fehlgeschlagen: " + error.Message; return false; }
        }

        private void UpdateNetwork()
        {
            if (hosting != null && hosting.IsCompleted)
            {
                try
                {
                    hosted = hosting.GetAwaiter().GetResult();
                    client = hosted.Client;
                    status = steamHostStarting ? "Steam-Lobby bereit – Freunde einladen" :
                        "Lobby gehostet auf Port " + hosted.Port;
                    if (!steamHostStarting) StartReachability(hosted);
                }
                catch (Exception error) { status = "Host fehlgeschlagen: " + error.Message; }
                hosting = null;
                steamHostStarting = false;
            }
            if (connecting != null && connecting.IsCompleted)
            {
                try
                {
                    client = connecting.GetAwaiter().GetResult();
                    status = "Verbunden";
                    action = client.SendAsync(MessageType.LobbyListRequest, requestId++, Array.Empty<byte>());
                }
                catch (Exception error)
                { autoJoinSteamLobby = false; status = "Verbindung fehlgeschlagen: " + error.Message; }
                connecting = null;
            }
            if (mapping != null && mapping.IsCompleted)
            {
                try
                {
                    mapped = mapping.GetAwaiter().GetResult();
                    externalAddress = mapped.ExternalAddress;
                    upnpStatus = "UPnP: Router-Port " + mapped.Port + " ist an " + mapped.InternalAddress + " weitergeleitet.";
                }
                catch (Exception error)
                {
                    externalAddress = (error as PortMappingException)?.ExternalAddress;
                    upnpStatus = "UPnP fehlgeschlagen (" + error.Message + "). Für Internet-Spieler TCP-Port " +
                        (hosted?.Port ?? 0) + " im Router manuell an diesen PC weiterleiten." +
                        (error.Message.Contains("606") ? " FRITZ!Box: Internet → Freigaben → dieses Gerät → " +
                            "„Selbstständige Portfreigaben erlauben“ aktivieren." : "");
                }
                mapping = null;
            }
            if (action != null && action.IsCompleted)
            {
                if (action.IsFaulted) status = "Aktion fehlgeschlagen: " + action.Exception?.GetBaseException().Message;
                action = null;
            }
            WorldSelfTest.TryStart();
            UpdateManifestAndTransfers();
            UpdateHostSetup();
            UpdateGuestGame();
            UpdateSiteFlags();
            if (client == null) return;
            try
            {
                while (client.TryDequeue(out WireMessage? message) && message != null)
                {
                    switch (message.Type)
                    {
                        case MessageType.LobbyListResponse:
                            available = LobbyPayloads.ReadList(message.Payload); listPage = 0;
                            if (autoJoinSteamLobby)
                            {
                                autoJoinSteamLobby = false;
                                if (available.Count == 1)
                                    Send(MessageType.JoinLobby, LobbyPayloads.GuidValue(available[0].Id));
                            }
                            break;
                        case MessageType.LobbyStateUpdate:
                            LobbyView received = LobbyPayloads.ReadState(message.Payload);
                            if (lobby == null || lobby.Id != received.Id || received.Revision >= lobby.Revision)
                            {
                                if (lobby == null || lobby.Id != received.Id) chatMessages.Clear();
                                SetupPhase before = lobby?.Id == received.Id ? lobby.Phase : SetupPhase.Lobby;
                                lobby = received;
                                advancePending = false;
                                OnPhaseChanged(before, received.Phase);
                            }
                            break;
                        case MessageType.PawnRoster:
                            pawnRoster = PawnRoster.Read(message.Payload);
                            break;
                        case MessageType.PawnRequest:
                            PawnRequest pawnRequest = PawnRequest.Read(message.Payload);
                            if (hostSetupActive && lobby?.Phase == SetupPhase.Characters && Find.GameInitData != null)
                            {
                                string? refused = characterHost.Apply(pawnRequest, Find.GameInitData);
                                if (refused != null) Log.Message("Multiplayer Foundation: pawn request refused: " + refused);
                                nextMirrorAt = 0f; // publish the result right away
                            }
                            break;
                        case MessageType.SetupSettings:
                            hostSettings = SetupSettings.Read(message.Payload);
                            if (hostSettings.Page == "Szenario" && hostSettings.Entries.Count > 0 && hostSettings.Entries[0].Label == "Szenario")
                                hostScenarioName = hostSettings.Entries[0].Value;
                            break;
                        case MessageType.LeaveLobby:
                            lobby = null; chatMessages.Clear(); status = "Lobby verlassen";
                            hostSettings = null; lastMirror = null;
                            ResetGame();
                            if (hostSetupActive) EndHostSetup(true);
                            Send(MessageType.LobbyListRequest, Array.Empty<byte>()); break;
                        case MessageType.LobbyChatMessage:
                            LobbyChatView chat = LobbyPayloads.ReadChatMessage(message.Payload);
                            if (lobby != null && chat.LobbyId == lobby.Id)
                            {
                                chatMessages.Add(chat);
                                if (chatMessages.Count > 50) chatMessages.RemoveAt(0);
                                chatScroll.y = float.MaxValue;
                            }
                            break;
                        case MessageType.ModManifestRequest:
                        case MessageType.TransferBegin:
                        case MessageType.TransferChunk:
                        case MessageType.TransferAck:
                        case MessageType.TransferAbort:
                            HandleTransfer(message); break;
                        case MessageType.Error:
                            var reader = new PayloadReader(message.Payload);
                            byte code = reader.ReadByte(); string reason = reader.ReadString(); reader.EnsureComplete();
                            advancePending = false;
                            if (startRequested)
                            {
                                startRequested = false;
                                Messages.Message("Spielstart abgelehnt: " + reason, MessageTypeDefOf.RejectInput, false);
                                ReopenCharacterPage();
                            }
                            status = "Serverfehler " + code + ": " + reason; break;
                    }
                }
            }
            catch (ProtocolException error)
            {
                string failure = "Ungültige Serverdaten: " + error.Message;
                Disconnect(); status = failure; return;
            }
            if (!client.IsConnected)
            {
                status = "Verbindung unterbrochen: " + (client.LastError ?? "Unbekannt");
                client.Dispose(); client = null; lobby = null; chatMessages.Clear();
                ResetTransfers();
                if (hosted != null)
                {
                    ListenHost running = hosted;
                    hosted = null;
                    action = running.StopAsync();
                    ReleaseMapping();
                }
                steam?.Leave();
            }
        }

        private void StartReachability(ListenHost host)
        {
            bool loopbackOnly = IPAddress.TryParse(host.BindAddress, out IPAddress? bound) && IPAddress.IsLoopback(bound);
            bool everywhere = bound != null && (bound.Equals(IPAddress.Any) || bound.Equals(IPAddress.IPv6Any));
            shareable = loopbackOnly || bound == null ? Array.Empty<IPAddress>()
                : everywhere ? NetworkAddresses.GetShareableAddresses()
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork ? host.AcceptsIPv4 : host.AcceptsIPv6).ToList()
                : new[] { bound };
            if (loopbackOnly) upnpStatus = "Host-IP ist 127.0.0.1: nur auf diesem PC erreichbar. Für LAN/Internet :: eintragen.";
            else if (!settings.UseUpnp)
                upnpStatus = "UPnP aus: Für Internet-Spieler TCP-Port " + host.Port + " im Router an diesen PC weiterleiten.";
            else if (!host.AcceptsIPv4) upnpStatus = "";
            else
            {
                upnpStatus = "UPnP: Router-Port wird geöffnet …";
                mapping = UpnpPortMapping.CreateAsync(host.Port, TimeSpan.FromSeconds(4));
            }
        }

        private string Reachability()
        {
            if (hosted == null) return "";
            int port = hosted.Port;
            var lines = new List<string> { "Mitspieler tragen eine dieser Adressen bei Server ein:" };
            if (externalAddress != null)
                lines.Add(!NetworkAddresses.IsPublic(externalAddress)
                    ? "  Internet (IPv4) nicht möglich: Der Router hat keine öffentliche IPv4 (" + externalAddress +
                      ", CGNAT/DS-Lite). IPv6-Adresse oder ein VPN wie Tailscale/ZeroTier nutzen."
                    : "  Internet (IPv4): " + NetworkAddresses.Format(externalAddress, port) +
                      (mapped != null ? "" : mapping != null ? " (Portfreigabe läuft …)"
                          : " (erst erreichbar, wenn der Port im Router weitergeleitet ist)"));
            foreach (IPAddress address in shareable.Take(4))
                lines.Add((address.AddressFamily == AddressFamily.InterNetworkV6 ? "  Internet (IPv6): "
                    : NetworkAddresses.IsSharedAddressSpace(address) ? "  VPN: " : "  LAN: ") +
                    NetworkAddresses.Format(address, port));
            if (shareable.Any(a => a.AddressFamily == AddressFamily.InterNetworkV6))
                lines.Add("  IPv6 nur, wenn die Router-Firewall TCP-Port " + port + " für diesen PC freigibt.");
            if (upnpStatus.Length > 0) lines.Add(upnpStatus);
            lines.Add("Fragt Windows nach Netzwerkzugriff für RimWorld, mit „Zulassen“ bestätigen.");
            return string.Join("\n", lines);
        }

        private string? BestShareAddress()
        {
            if (hosted == null) return null;
            if (externalAddress != null && NetworkAddresses.IsPublic(externalAddress))
                return NetworkAddresses.Format(externalAddress, hosted.Port);
            IPAddress? address = shareable.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6)
                ?? shareable.FirstOrDefault();
            return address == null ? null : NetworkAddresses.Format(address, hosted.Port);
        }

        private void ReleaseMapping()
        {
            _ = mapped?.RemoveAsync();
            mapped = null;
            mapping?.ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion) _ = t.Result.RemoveAsync();
                else _ = t.Exception;
            }, TaskScheduler.Default);
            mapping = null;
            externalAddress = null;
            upnpStatus = "";
            shareable = Array.Empty<IPAddress>();
        }
    }
}
