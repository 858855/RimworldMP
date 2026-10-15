# Dateikarte

| Pfad | Aufgabe / Einstieg | Zugehörige Prüfung | Stand |
|---|---|---|---|
| `src/Multiplayer.Foundation.Protocol/FrameCodec.cs` | Wire-Version 2, TCP-Frames lesen/schreiben, Limit 64 KiB | `tests/Protocol.Tests/Program.cs` | implementiert, getestet |
| `src/Multiplayer.Foundation.Protocol/PayloadCodec.cs` | Strikte binäre Feldkodierung, UTF-8 und GUID | `tests/Protocol.Tests/Program.cs` | implementiert, getestet |
| `src/Multiplayer.Foundation.Protocol/MessageType.cs`, `WireMessage.cs` | Nachrichtentypen und Envelope | `tests/Protocol.Tests/Program.cs` | implementiert, getestet |
| `src/Multiplayer.Foundation.Protocol/LobbyPayloads.cs` | Typisierte Lobby-Snapshots, Listen und begrenzte Chat-Payloads | `tests/Lobby.Tests/Program.cs`, `Protocol.Tests` | Roundtrip und Grenzen getestet |
| `src/Multiplayer.Foundation.Protocol/ProtocolException.cs` | Fehler für ungültige Frames und Payloads | Protokolltests | implementiert |
| `tests/Protocol.Tests/Program.cs` | Ausführbare, paketfreie Protokoll-Testgruppen | `dotnet run --project ...` | sechs Gruppen bestanden |
| `src/Multiplayer.Foundation.Network/NetworkConnection.cs`, `HandshakeClient.cs` | TCP (mit DNS-Auflösung und IPv4/IPv6-Fallback) oder Duplex-Stream, serialisiertes Senden, ein Reader und Handshake | `tests/Network.Tests/Program.cs`, `tests/Hosting.Tests/Program.cs` | Stream-Anschluss und TCP-Regression getestet |
| `src/Multiplayer.Foundation.Network/HandshakeClient.cs` | Hello/Accepted/Rejected und Deadline | `tests/Network.Tests/Program.cs` | implementiert, Teiltests bestanden |
| `AGENTS.md`, `docs/governance/` | Verbindliche Regeln und Detailworkflow | `scripts/validate_project_state.py` | vorhanden |
| `docs/ai/PROJECT_STATE.yaml`, `TASKS.yaml` | Ist-Zustand und Aufgaben | Dokumentationsvalidator | vorhanden |
| `scripts/validate_structure.py`, `verify_knowledge_archive.py` | Archiv-XML und ZIP prüfen | drei XML-Dateien, 49 Archiveinträge | ausgeführt |
| `examples/` | Illustrative RimWorld-/MP-Muster | kein produktiver API-Nachweis | unkompiliert |
| `src/Multiplayer.Foundation.Lobby/LobbyRegistry.cs` | Autoritativer In-Memory-Zustand, Rechte, Revisionen und Chat-Empfängerliste | `tests/Lobby.Tests/Program.cs`, `Integration.Tests` | Domänenkern und TCP-Anbindung getestet |
| `src/Multiplayer.Foundation.Hosting/FoundationServer.cs`, `HostConfig.cs` | Gemeinsamer Dual-Stack-TCP-Listener (`::`) sowie Aufnahme externer Streams; Sitzungen, Chat-Routing und Heartbeat | `tests/Integration.Tests/Program.cs`, `Hosting.Tests` | 18 Server- und acht Host-Gruppen bestanden |
| `src/Multiplayer.Foundation.Hosting/UpnpPortMapping.cs`, `NetworkAddresses.cs` | UPnP-Portfreigabe am Router (SSDP Multicast + Gateway-Unicast, SOAP), externe IP, CGNAT-Erkennung, teilbare LAN/VPN/IPv6-Adressen | `tests/Hosting.Tests/` mit `FakeGateway.cs` | Fake-Gateway-Test bestanden, SSDP gegen echte FRITZ!Box nur lesend |
| `src/Multiplayer.Foundation.Network/HostEndpoint.cs` | Server-Feld `host`, `host:port`, `[v6]:port` parsen | `tests/Network.Tests/Program.cs` | getestet |
| `src/Multiplayer.Foundation.Hosting/ListenHost.cs` | Integrierten Host starten, Host-Client verbinden und Lobby anlegen | `tests/Hosting.Tests/Program.cs` | vier Gruppen bestanden |
| `src/Multiplayer.Foundation.Server/ServerConfig.cs`, `Program.cs` | JSON-Konfiguration, Serverstart und SIGTERM/Strg+C-Shutdown | Integrationstests/Mehrprozess-Test | getestet |
| `tests/Integration.Tests/Program.cs` | Reale Zwei-Client-TCP-Lobby und Fehlerfälle | `dotnet run --project ...` | bestanden |
| `src/Multiplayer.Foundation.Network/LobbyClient.cs` | Ein Reader, UI-Nachrichtenqueue, Ping/Pong und Disconnect | `tests/Client.Tests/Program.cs` | TCP-Test bestanden |
| `src/Multiplayer.Foundation.RimWorld/FoundationMod.cs`, `FoundationSettings.cs` | Mod-Einstellungen, TCP-/Steam-Verbindung und Lobby-UI | Release-Build; Game-Test offen | gebaut, Laufzeit offen |
| `src/Multiplayer.Foundation.RimWorld/MainMenuOptionPatch.cs`, `FoundationWindow.cs`, `FoundationUpdatePump.cs` | Nativen Menüpunkt nach Tutorial einfügen, Verbindungsfenster öffnen, UI-Netzwerkqueue pumpen | Release-Build; grafischer Spieltest offen | gebaut, Laufzeit offen |
| `src/Multiplayer.Foundation.RimWorld/SteamLobbyBridge.cs`, `SteamPeerStream.cs` | Steam-Freundeslobby, P2P-Listener, Einladungen, Callback-Pump und begrenzte Frame-Queue | Release-Build; Zwei-Konten-Test offen | gebaut, Laufzeit offen |
| `mod/About/About.xml`, `scripts/build-mod.sh` | Mod-Metadaten und Paketbau | XML/Paketprüfung | geprüft |
| `tests/ProcessIntegration.Tests/Program.cs` | Server und zwei Client-Prozesse über echte TCP-Lobby steuern | `dotnet run --project ...` | acht Prozess-Testschritte bestanden |
| `scripts/package-mod.py`, `mod/INSTALLATION.md` | Geprüftes Mod-ZIP einschließlich Harmony-Lizenz und manuelle Installation | ZIP-Extraktionsprüfung | neun Einträge geprüft, Spiel-Laufzeit offen |
