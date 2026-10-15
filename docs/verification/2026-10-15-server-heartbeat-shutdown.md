# Server-Heartbeat und Shutdown, 15.10.2026

- Branch: `fix/server-heartbeat`; Basis `test/multi-player-process` bei `4a6b129`. Änderungen lokal, kein Push/Merge. Commit-ID nach Erstellung via `git log -1 --oneline` ermitteln.
- Umgebung: Linux, .NET SDK 10.0.402; Loopback-TCP. Spiel: lokale RimWorld-Dateien 1.6.4871; Game-UI für diese Änderung nicht gestartet.
- Reproduktion vor der Korrektur: `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Integration.Tests/Integration.Tests.csproj -c Release --nologo` endete mit Exit 134 nach `Unexpected server error: 0` beim verspäteten Pong auf Ping 1 nach Empfang von Ping 2.
- Korrektur: Server bestätigt jede gesendete, noch nicht bestätigte Nonce. Er misst die Zeit seit der letzten gültigen Antwort mit monotonem `Stopwatch`; Duplikate, Nonce 0 und nicht gesendete Werte werden abgewiesen.
- Derselbe Integrationstest nach Korrektur: Exit 0, 16 PASS-Gruppen. Darunter verzögertes Pong angenommen, doppeltes und ungesendetes Pong abgewiesen, stummer Client nach Timeout entfernt.
- Mehrprozess-Test: `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/ProcessIntegration.Tests/ProcessIntegration.Tests.csproj -c Release --nologo`, Exit 0, acht PASS-Schritte. Server, Alice und Bob laufen als getrennte Prozesse; SIGTERM beendet den Server mit Exit 0 und Alice erkennt das Verbindungsende.
- Release-Build: `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet build src/Multiplayer.Foundation.Server/Multiplayer.Foundation.Server.csproj -c Release --nologo --no-restore`, Exit 0, 0 Warnungen, 0 Fehler.
- Dokumentationsprüfung: `python3 scripts/validate_project_state.py`, Exit 0, 22 Pflichtdateien und 13 Aufgaben gültig.
- Nicht geprüft: RimWorld-Spiel-UI, zwei Spielprozesse, anderes Betriebssystem als Linux und Verbindungen über Rechnergrenzen. Authentifizierung/TLS fehlen weiterhin.
