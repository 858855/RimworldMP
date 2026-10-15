# Dedicated Server und echte TCP-Integration – 2026-10-15

- **Branch:** `feat/dedicated-server`, Basis `feat/network-protocol`/`f14142c`; Commit beim Test noch offen (anschließend lokal committen).
- **Umgebung:** Linux, .NET-SDK `10.0.402`, Loopback `127.0.0.1`, dynamischer Testport. Kein RimWorld-Prozess.
- **Build:** `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet build src/Multiplayer.Foundation.Server/Multiplayer.Foundation.Server.csproj --nologo` → Exit 0, 0 Warnungen, 0 Fehler.
- **Integration:** `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Integration.Tests/Integration.Tests.csproj -c Release --nologo` → Exit 0. Zehn PASS-Gruppen: Konfiguration, Start und zwei Handshakes, Erstellen/Liste/Beitreten, Ready/Rechte, Verlassen/Neu-Beitreten/Disconnect, fehlerhafter Frame, Ping/Pong, Host-Schließen, Shutdown, stiller Client-Timeout.
- **Deployment:** `env DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-server.sh` → Exit 0; `dist/server/` erstellt. `scripts/start-server.sh` mit derselben DOTNET_BIN-Variable startete auf `127.0.0.1:28491` und beendete sich nach Strg+C mit Exit 0.
- **Grenze:** Kein RimWorld-Client angeschlossen; keine öffentlichen Netzwerk- oder Lasttests.
