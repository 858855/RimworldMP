# Erweiterte Zwei-Client-Integration – 2026-10-15

- **Branch:** `test/lobby-integration`, Basis `feat/rimworld-client`/`d20a2e6`; Commit beim Test noch offen.
- **Umgebung:** Linux, .NET-SDK `10.0.402`, lokale TCP-Loopback-Verbindungen; kein RimWorld-Spielprozess.
- **Befehl:** `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Integration.Tests/Integration.Tests.csproj -c Release --nologo` → Exit 0.
- **Ergebnis:** 13 PASS-Gruppen: Konfiguration, Start/zwei Handshakes, Erstellen/Liste/Beitreten, Ready/Rechte, Verlassen/Disconnect, Reconnect ohne Doppelmitgliedschaft, fehlerhafter Frame, ungültige Wire-Version, Ping/Pong, Host-Schließen, Host-Übergabe nach Disconnect, Shutdown, Heartbeat-Timeout.
- **Grenze:** Reconnect ist eine neue Sitzung mit erneuter Lobby-Teilnahme; eine alte Sitzungsidentität wird nicht wiederhergestellt. RimWorld-UI und Spiel-Ladeprozess sind nicht verifiziert.
