using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using HarmonyLib;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Hosting;
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
        internal static FoundationMod? Instance { get; private set; }
        private static bool menuPatched;

        public FoundationMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<FoundationSettings>();
            Instance = this;
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
            Widgets.Label(new Rect(x, y, width, 30f), "Multiplayer Foundation 0.1");
            y += 32f;
            Widgets.Label(new Rect(x, y, width, 28f), "Verbinden und gemeinsam eine Lobby nutzen · gemeinsames Spiel folgt später");
            y += 34f;
            float statusY = inRect.yMax - 48f;
            if (client == null)
            {
                Rect viewport = new Rect(x, y, width, statusY - y - 8f);
                Widgets.BeginScrollView(viewport, ref connectionScroll, new Rect(0f, 0f, width - 18f, 560f));
                float contentY = 0f, contentWidth = width - 18f;
                Widgets.Label(new Rect(0f, contentY, 110f, 26f), "Spielername");
                settings.PlayerName = Widgets.TextField(new Rect(112f, contentY, contentWidth - 112f, 26f), settings.PlayerName);
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
            connecting = LobbyClient.ConnectAsync(host, port, settings.PlayerName.Trim(), TimeSpan.FromSeconds(10));
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
                settings.PlayerName.Trim(), name, capacity);
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
            hosting = StartSteamHostAsync(steam, settings.PlayerName.Trim(), name, capacity);
        }

        private static async Task<ListenHost> StartSteamHostAsync(SteamLobbyBridge bridge,
            string playerName, string name, byte capacity)
        {
            ListenHost host = await ListenHost.StartAsync("127.0.0.1", 0, playerName, name, capacity);
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
                Widgets.Label(new Rect(x, y, width - 110f, 28f),
                    room.Name + " – " + room.HostName + " (" + room.PlayerCount + "/" + room.Capacity + ")");
                if (Widgets.ButtonText(new Rect(x + width - 105f, y, 100f, 28f), "Beitreten"))
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
            foreach (PlayerView player in lobby.Players)
            {
                string label = player.Name + (player.Id == lobby.HostId ? " [Host]" : "") +
                    (player.Ready ? " – bereit" : " – nicht bereit");
                Widgets.Label(new Rect(x, y, width, 27f), label);
                y += 29f;
            }
            y += 8f;
            bool myReady = lobby.Players.FirstOrDefault(p => p.Id == client.SessionId)?.Ready ?? false;
            if (Widgets.ButtonText(new Rect(x, y, 125f, 30f), myReady ? "Nicht bereit" : "Bereit"))
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
            Widgets.Label(new Rect(x, y, width, 25f), "Spielstart nicht verfügbar (Version 0.1)");
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
                                lobby = received;
                            }
                            break;
                        case MessageType.LeaveLobby:
                            lobby = null; chatMessages.Clear(); status = "Lobby verlassen";
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
                        case MessageType.Error:
                            var reader = new PayloadReader(message.Payload);
                            byte code = reader.ReadByte(); string reason = reader.ReadString(); reader.EnsureComplete();
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
