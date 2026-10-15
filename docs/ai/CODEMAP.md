# Dateikarte

| Pfad | Aufgabe / Einstieg | Zugehörige Prüfung | Stand |
|---|---|---|---|
| `src/Multiplayer.Foundation.Protocol/FrameCodec.cs` | Versionierte TCP-Frames lesen/schreiben, Limit 64 KiB | `tests/Protocol.Tests/Program.cs` | implementiert, getestet |
| `src/Multiplayer.Foundation.Protocol/PayloadCodec.cs` | Strikte binäre Feldkodierung, UTF-8 und GUID | `tests/Protocol.Tests/Program.cs` | implementiert, getestet |
| `src/Multiplayer.Foundation.Protocol/MessageType.cs`, `WireMessage.cs` | Nachrichtentypen und Envelope | `tests/Protocol.Tests/Program.cs` | implementiert, getestet |
| `src/Multiplayer.Foundation.Protocol/LobbyPayloads.cs` | Typisierte Lobby-Snapshots und Listen | `tests/Lobby.Tests/Program.cs` | Roundtrip getestet |
| `src/Multiplayer.Foundation.Protocol/ProtocolException.cs` | Fehler für ungültige Frames und Payloads | Protokolltests | implementiert |
| `tests/Protocol.Tests/Program.cs` | Ausführbare, paketfreie Protokoll-Testgruppen | `dotnet run --project ...` | fünf Gruppen bestanden |
| `src/Multiplayer.Foundation.Network/NetworkConnection.cs` | TCP-Verbindung, serialisiertes Senden, ein Reader und Close | `tests/Network.Tests/Program.cs` | implementiert, Teiltests bestanden |
| `src/Multiplayer.Foundation.Network/HandshakeClient.cs` | Hello/Accepted/Rejected und Deadline | `tests/Network.Tests/Program.cs` | implementiert, Teiltests bestanden |
| `AGENTS.md`, `docs/governance/` | Verbindliche Regeln und Detailworkflow | `scripts/validate_project_state.py` | vorhanden |
| `docs/ai/PROJECT_STATE.yaml`, `TASKS.yaml` | Ist-Zustand und Aufgaben | Dokumentationsvalidator | vorhanden |
| `scripts/validate_structure.py`, `verify_knowledge_archive.py` | Archiv-XML und ZIP prüfen | drei XML-Dateien, 49 Archiveinträge | ausgeführt |
| `examples/` | Illustrative RimWorld-/MP-Muster | kein produktiver API-Nachweis | unkompiliert |
| `src/Multiplayer.Foundation.Lobby/LobbyRegistry.cs` | Autoritativer In-Memory-Zustand, Rechte und Revisionen | `tests/Lobby.Tests/Program.cs` | Domänenkern getestet, TCP ausstehend |
| `src/Multiplayer.Foundation.Server/FoundationServer.cs` | TCP-Listener, Sitzungen, Nachrichtenrouting, Broadcast und Heartbeat | `tests/Integration.Tests/Program.cs` | zehn Gruppen bestanden |
| `src/Multiplayer.Foundation.Server/ServerConfig.cs`, `Program.cs` | Konfiguration und Serverstart | Integrationstests/Build | implementiert |
| `tests/Integration.Tests/Program.cs` | Reale Zwei-Client-TCP-Lobby und Fehlerfälle | `dotnet run --project ...` | bestanden |
| `src/Multiplayer.Foundation.RimWorld/`, `mod/` | Game-Adapter und UI | Game-Start | geplant |
