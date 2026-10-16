# Merge-Prüfung `codex/faction-ui` → `main`

- **Datum:** 2026-10-16, Linux, lokales .NET SDK 10.0.402.
- **Basis:** `origin/main` @ `ea065aa`; `git merge-base codex/faction-ui origin/main` ergab `ea065aa`. Branch vor der Prüfung sauber. Benutzer hat Push und Merge in dieser Sitzung ausdrücklich freigegeben.
- **Spielbuild:** lokale RimWorld-Installation `1.6.4871 rev598` laut `Version.txt`; Unity/Mono. Der früher beobachtete Spielprozess meldete rev600. Für diese Prüfung wurde kein Spielprozess gestartet.

| Prüfung | Tatsächliches Ergebnis |
|---|---|
| `env DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen und 0 Fehler. |
| `dotnet run --project tests/Client.Tests/Client.Tests.csproj -c Release --nologo` | Exit 0; Client-Pump, Heartbeat, Disconnect bestanden. |
| `dotnet run --project tests/Hosting.Tests/Hosting.Tests.csproj -c Release --nologo` | Exit 0; Hosting-Szenarien einschließlich Fraktions-Storyteller 96/97 bestanden. |
| `dotnet run --project tests/Integration.Tests/Integration.Tests.csproj -c Release --nologo` | Exit 0; echte TCP-Serverintegration bestanden. |
| `dotnet run --project tests/Lobby.Tests/Lobby.Tests.csproj -c Release --nologo` | Exit 0; Lobby- und Fraktionsregeln bestanden. |
| `dotnet run --project tests/Network.Tests/Network.Tests.csproj -c Release --nologo` | Exit 0; TCP, Transfer und Identität bestanden. |
| `dotnet run --project tests/ProcessIntegration.Tests/ProcessIntegration.Tests.csproj -c Release --nologo` | Exit 0; eigenständiger Server und zwei Clientprozesse bestanden. |
| `dotnet run --project tests/Protocol.Tests/Protocol.Tests.csproj -c Release --nologo` | Exit 0; 18 Gruppen einschließlich Fraktionsnachrichten bestanden. |
| `dotnet run --project tests/Session.Tests/Session.Tests.csproj -c Release --nologo` | Exit 0; Lockstep- und simulierte Join-/Resync-Läufe bestanden. |
| `python3 scripts/validate_project_state.py` | Exit 0; Pflichtdateien, Aufgaben, Status, Abhängigkeiten und Referenzen gültig. |
| `git diff --check origin/main...HEAD` | Exit 0; keine Whitespace-Fehler. |

Die `dotnet`-Zeilen liefen mit `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet`; das Tabellenkürzel zeigt nur die Argumente. **Nicht ausgeführt:** RimWorld-UI, Save/Load im Spiel, Mehrfach-Storyteller-Ticks, echte Zwei-Spieler-Tests. Der Fraktionsstart bleibt gesperrt. Die Merge-Freigabe bestätigt die Integration dieses Teilstands, keine Laufzeitfunktion des Fraktionsmodus.
