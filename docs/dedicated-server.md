# Dedicated Server 0.1

Der eigenständige .NET-10-Prozess nutzt denselben `netstandard2.1`-Hosting-Kern wie der direkte Host in der Mod und bindet standardmäßig dual-stack an `::` Port 28491 (IPv4 und IPv6 auf allen Schnittstellen; ohne IPv6 Rückfall auf `0.0.0.0`). Er akzeptiert mehrere TCP-Clients, prüft den Handshake, verwaltet Sitzungen und autoritative Lobbys, verteilt Snapshots, sendet Heartbeats und entfernt getrennte Clients. Details zum Nachrichtenformat: `docs/protocol.md`.

## Lokal bauen und starten

```bash
DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-server.sh
DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/start-server.sh
```

`build-server.sh` veröffentlicht nach `dist/server/` und kopiert `deployment/serverconfig.example.json` als lokale `dist/server/serverconfig.json`. `start-server.sh` liest diese lokale Datei; ein alternativer Konfigurationspfad ist sein erstes Argument. Strg+C oder unter Linux SIGTERM beendet Listener und Sitzungen. Ein verbundener Client erkennt dabei das Verbindungsende. Der Standardport ist TCP 28491. `BindAddress` `127.0.0.1` beschränkt den Server auf den eigenen Rechner; eine konkrete Adresse bindet nur diese Schnittstelle. Für Internet-Spieler muss TCP 28491 in Router und Firewall freigegeben sein (der dedizierte Server macht kein UPnP). Der Server bietet noch keine Authentifizierung oder TLS: Wer Adresse und Port kennt, kann sich verbinden.

`deployment/rimworldmp.service.example` ist ein manuell anzupassendes systemd-Beispiel; es wird nicht installiert oder aktiviert. Für diese Datei müssen Binary und Konfiguration nach `/opt/rimworldmp` kopiert und ein eingeschränkter Benutzer angelegt werden.

## Konfiguration

`BindAddress`, `Port`, `MaxClients`, `MaxPlayersPerLobby`, `HeartbeatIntervalSeconds`, `ClientTimeoutSeconds` und `LogLevel` werden beim Start validiert. `LogLevel` wird aktuell nur validiert; strukturierte Logfilterung ist noch offen. Räume und Sitzungen liegen nur im Speicher. Ein Serverneustart verwirft Lobbys.

## Nachweis und Grenzen

`tests/Integration.Tests/Program.cs` prüft zwei echte TCP-Clients, Lobbyaktionen, Host-Rechte, Disconnect, Heartbeat mit verzögerter Antwort, fehlerhaftes Framing und Shutdown. `tests/ProcessIntegration.Tests/Program.cs` startet Server und zwei Clients als eigene Prozesse und prüft auch SIGTERM mit noch verbundenem Client. Ergebnisse: `docs/verification/2026-10-15-dedicated-server.md` und `docs/verification/2026-10-15-server-heartbeat-shutdown.md`. Ein RimWorld-Spielclient wurde noch nicht verbunden.
