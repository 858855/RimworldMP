using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Hosting;
using Multiplayer.Foundation.Protocol;
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
        private Task<UpnpPortMapping>? mapping;
        private UpnpPortMapping? mapped;
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

        public FoundationMod(ModContentPack content) : base(content)
        { settings = GetSettings<FoundationSettings>(); }

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
                TooltipHandler.TipRegion(new Rect(x, y, width, 26f),
                    "IP-Adresse oder Name des Hosts, optional mit :Port, z. B. 203.0.113.5:28491 oder [2001:db8::5]:28491");
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
                settings.HostBindAddress = Widgets.TextField(new Rect(x + 110f, y, 200f, 26f), settings.HostBindAddress);
                Widgets.Label(new Rect(x + 320f, y, width - 320f, 26f), ":: = LAN und Internet, 127.0.0.1 = nur dieser PC");
                y += 31f;
                Widgets.Label(new Rect(x, y, 110f, 26f), "Lobbyname");
                lobbyName = Widgets.TextField(new Rect(x + 110f, y, width - 110f, 26f), lobbyName);
                y += 31f;
                Widgets.Label(new Rect(x, y, 110f, 26f), "Plätze");
                lobbyCapacity = Widgets.TextField(new Rect(x + 110f, y, width - 110f, 26f), lobbyCapacity);
                y += 31f;
                Widgets.CheckboxLabeled(new Rect(x, y, 420f, 26f), "Port am Router automatisch öffnen (UPnP)", ref settings.UseUpnp);
                y += 34f;
                if (connecting == null && hosting == null && action == null &&
                    Widgets.ButtonText(new Rect(x, y, 240f, 30f), "Lobby direkt hosten")) HostLobby();
            }
            else
            {
                if (Widgets.ButtonText(new Rect(x, y, 140f, 30f), "Trennen")) Disconnect();
                string? best = BestShareAddress();
                if (best != null && Widgets.ButtonText(new Rect(x + 150f, y, 180f, 30f), "Adresse kopieren"))
                { GUIUtility.systemCopyBuffer = best; status = "Kopiert: " + best; }
                y += 38f;
                if (hosted != null)
                {
                    string info = Reachability();
                    float height = Text.CalcHeight(info, width);
                    Widgets.Label(new Rect(x, y, width, height), info);
                    y += height + 8f;
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

        private void Disconnect()
        {
            if (client == null) return;
            LobbyClient old = client;
            client = null; lobby = null; available = Array.Empty<LobbySummary>();
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
                    status = "Lobby gehostet auf Port " + hosted.Port;
                    StartReachability(hosted);
                }
                catch (Exception error) { status = "Host fehlgeschlagen: " + error.Message; }
                hosting = null;
            }
            if (connecting != null && connecting.IsCompleted)
            {
                try
                {
                    client = connecting.GetAwaiter().GetResult();
                    status = "Verbunden";
                    action = client.SendAsync(MessageType.LobbyListRequest, requestId++, Array.Empty<byte>());
                }
                catch (Exception error) { status = "Verbindung fehlgeschlagen: " + error.Message; }
                connecting = null;
            }
            if (mapping != null && mapping.IsCompleted)
            {
                try
                {
                    mapped = mapping.GetAwaiter().GetResult();
                    upnpStatus = "UPnP: Router-Port " + mapped.Port + " ist an " + mapped.InternalAddress + " weitergeleitet.";
                }
                catch (Exception error)
                {
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
                            available = LobbyPayloads.ReadList(message.Payload); listPage = 0; break;
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
                    ReleaseMapping();
                }
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
            if (mapped?.ExternalAddress != null)
                lines.Add(mapped.ExternalAddressIsPublic
                    ? "  Internet (IPv4): " + NetworkAddresses.Format(mapped.ExternalAddress, port)
                    : "  Internet (IPv4) nicht möglich: Der Router hat keine öffentliche IPv4 (" + mapped.ExternalAddress +
                      ", CGNAT/DS-Lite). IPv6-Adresse oder ein VPN wie Tailscale/ZeroTier nutzen.");
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
            if (mapped?.ExternalAddress != null && mapped.ExternalAddressIsPublic)
                return NetworkAddresses.Format(mapped.ExternalAddress, hosted.Port);
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
            upnpStatus = "";
            shareable = Array.Empty<IPAddress>();
        }
    }
}
