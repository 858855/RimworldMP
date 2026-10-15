using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private Task? action;
        private IReadOnlyList<LobbySummary> available = Array.Empty<LobbySummary>();
        private LobbyView? lobby;
        private string status = "Nicht verbunden";
        private string lobbyName = "Colony";
        private string lobbyCapacity = "4";
        private int listPage;
        private uint requestId = 2;

        public FoundationMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<FoundationSettings>();
            TryInitializeSteam();
        }

        public override string SettingsCategory() => "Multiplayer Foundation";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            UpdateNetwork();
            float x = inRect.x + 6f, y = inRect.y + 6f, width = inRect.width - 12f;
            Widgets.Label(new Rect(x, y, width, 28f), "RimWorld Multiplayer Foundation 0.1 – Lobby, kein gemeinsames Spiel");
            y += 36f;
            if (client == null)
            {
                Widgets.Label(new Rect(x, y, 110f, 26f), "Server");
                settings.ServerAddress = Widgets.TextField(new Rect(x + 110f, y, width - 110f, 26f), settings.ServerAddress);
                y += 31f;
                Widgets.Label(new Rect(x, y, 110f, 26f), "Port");
                settings.Port = Widgets.TextField(new Rect(x + 110f, y, width - 110f, 26f), settings.Port);
                y += 31f;
                Widgets.Label(new Rect(x, y, 110f, 26f), "Spielername");
                settings.PlayerName = Widgets.TextField(new Rect(x + 110f, y, width - 110f, 26f), settings.PlayerName);
                y += 34f;
                if (connecting == null && hosting == null && action == null &&
                    Widgets.ButtonText(new Rect(x, y, 140f, 30f), "Verbinden")) Connect();
                y += 42f;
                Widgets.Label(new Rect(x, y, width, 28f), "Ohne separaten Server: Lobby auf diesem Rechner hosten");
                y += 31f;
                Widgets.Label(new Rect(x, y, 110f, 26f), "Host-IP");
                settings.HostBindAddress = Widgets.TextField(new Rect(x + 110f, y, width - 110f, 26f), settings.HostBindAddress);
                y += 31f;
                Widgets.Label(new Rect(x, y, 110f, 26f), "Lobbyname");
                lobbyName = Widgets.TextField(new Rect(x + 110f, y, width - 110f, 26f), lobbyName);
                y += 31f;
                Widgets.Label(new Rect(x, y, 110f, 26f), "Plätze");
                lobbyCapacity = Widgets.TextField(new Rect(x + 110f, y, width - 110f, 26f), lobbyCapacity);
                y += 34f;
                if (connecting == null && hosting == null && action == null &&
                    Widgets.ButtonText(new Rect(x, y, 240f, 30f), "Lobby direkt hosten")) HostLobby();
                y += 39f;
                if (steam == null && Widgets.ButtonText(new Rect(x, y, 240f, 30f), "Steam prüfen")) TryInitializeSteam();
                if (steam != null && connecting == null && hosting == null && action == null &&
                    Widgets.ButtonText(new Rect(x, y, 240f, 30f), "Über Steam hosten")) HostSteamLobby();
                y += 38f;
                if (steam != null && pendingInvite.IsValid() && connecting == null && hosting == null &&
                    Widgets.ButtonText(new Rect(x, y, 240f, 30f), "Steam-Einladung annehmen")) JoinSteamInvite();
            }
            else
            {
                if (Widgets.ButtonText(new Rect(x, y, 140f, 30f), "Trennen")) Disconnect();
                y += 38f;
                if (steam?.IsHosting == true)
                {
                    if (Widgets.ButtonText(new Rect(x, y, 220f, 30f), "Steam-Freunde einladen"))
                    {
                        try { steam.OpenInviteDialog(); }
                        catch (Exception error) { status = "Steam-Einladung fehlgeschlagen: " + error.Message; }
                    }
                    y += 37f;
                }
                if (lobby == null) DrawBrowser(x, ref y, width);
                else DrawLobby(x, ref y, width);
            }
            y += 39f;
            Widgets.Label(new Rect(x, y, width, 48f), "Status: " + status);
        }

        private void Connect()
        {
            if (!int.TryParse(settings.Port, out int port) || port < 1 || port > 65535)
            { status = "Ungültiger Port"; return; }
            status = "Verbindung wird aufgebaut";
            connecting = LobbyClient.ConnectAsync(settings.ServerAddress.Trim(), port,
                settings.PlayerName.Trim(), TimeSpan.FromSeconds(8));
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
            client = null; lobby = null; available = Array.Empty<LobbySummary>();
            steam?.Leave();
            autoJoinSteamLobby = false;
            if (hosted != null)
            {
                ListenHost running = hosted;
                hosted = null;
                action = running.StopAsync();
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
            if (Widgets.ButtonText(new Rect(x, y, 140f, 30f), myReady ? "Nicht bereit" : "Bereit"))
                Send(MessageType.PlayerReadyChanged, LobbyPayloads.Bool(!myReady));
            if (Widgets.ButtonText(new Rect(x + 150f, y, 140f, 30f), "Verlassen"))
                Send(MessageType.LeaveLobby, Array.Empty<byte>());
            if (lobby.HostId == client.SessionId &&
                Widgets.ButtonText(new Rect(x + 300f, y, 140f, 30f), "Lobby schließen"))
                Send(MessageType.CloseLobby, Array.Empty<byte>());
            y += 36f;
            Widgets.Label(new Rect(x, y, width, 25f), "Spielstart nicht verfügbar (Version 0.1)");
        }

        private void Send(MessageType type, byte[] payload)
        {
            if (client == null || action != null) return;
            try { action = client.SendAsync(type, requestId++, payload); }
            catch (Exception error) { status = "Senden fehlgeschlagen: " + error.Message; }
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
                        "Lobby gehostet auf " + hosted.BindAddress + ":" + hosted.Port;
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
                                lobby = received;
                            break;
                        case MessageType.LeaveLobby:
                            lobby = null; status = "Lobby verlassen";
                            Send(MessageType.LobbyListRequest, Array.Empty<byte>()); break;
                        case MessageType.Error:
                            var reader = new PayloadReader(message.Payload);
                            byte code = reader.ReadByte(); string reason = reader.ReadString(); reader.EnsureComplete();
                            status = "Serverfehler " + code + ": " + reason; break;
                    }
                }
            }
            catch (ProtocolException error)
            { status = "Ungültige Serverdaten: " + error.Message; Disconnect(); return; }
            if (!client.IsConnected)
            {
                status = "Verbindung unterbrochen: " + (client.LastError ?? "Unbekannt");
                client.Dispose(); client = null; lobby = null;
                if (hosted != null)
                {
                    ListenHost running = hosted;
                    hosted = null;
                    action = running.StopAsync();
                }
                steam?.Leave();
            }
        }
    }
}
