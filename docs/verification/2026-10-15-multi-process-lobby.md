# Mehrprozess-Lobby-Verbindung, 15.10.2026

- Branch: `test/multi-player-process`, Basis `main` bei `9d96665`; Commit vor Prüfung noch nicht erstellt.
- Umgebung: Linux, .NET SDK 10.0.402, TCP-Loopback `127.0.0.1`, RimWorld 1.6.4871 lokal vorhanden, aber für diesen Test nicht gestartet.
- Befehl: `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/ProcessIntegration.Tests/ProcessIntegration.Tests.csproj -c Release --nologo`
- Erstlauf: Exit 134. Das Testprogramm startete sich selbst als Server, weil `Environment.ProcessPath` auf seinen Apphost statt auf `dotnet` zeigte. Der Test wählt nun den Host aus dem tatsächlichen .NET-Runtime-Verzeichnis.
- Wiederholung: Exit 0. Sieben PASS-Ausgaben: eigenständiger Serverprozess; zwei getrennte Client-Prozesse mit verschiedenen Sitzungs-IDs; Lobby erstellen und auflisten; gleicher Snapshot nach Join; Ready an beide verteilt; Disconnect aus verbleibender Spielerliste entfernt; beide Clients getrennt.
- Testgrenze: Der Test benutzt `LobbyClient` in Konsolenprozessen. Er belegt keine RimWorld-Mod-UI, keine Spielclients und keine Verbindung über Rechnergrenzen. Der Server wird im Test-Cleanup beendet; regulärer Signal-Shutdown wird hier nicht geprüft.
