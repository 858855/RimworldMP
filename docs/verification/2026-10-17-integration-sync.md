# Verifikation – Integration `integration/sync-2026-10-17` (Phase 1)

- **Datum:** 2026-10-17
- **Branch:** `integration/sync-2026-10-17` (Worktree `E:\RimworldMP-integration`), von `origin/main` @ `85e02f9`; Kopf siehe `git log -1`. Code-Commits: `de63452`, `1de7bb3`, `f1ebe66` (Fast-Forward), `fd68525`, `1711a92`, `b0f5780`, `85a10d4`; danach der Doku-Commit dieser Integration.
- **Umgebung:** Windows 11 Pro 10.0.26200 (Svens Rechner, nicht der Linux-Rechner des Besitzers); RimWorld `1.6.4871` unter `E:/SteamLibrary/steamapps/common/RimWorld` (`Version.txt` rev590); .NET SDK 8.0.403 (kein .NET 10); Lib.Harmony 2.4.3 aus dem NuGet-Cache; Python 3.11 mit PyYAML für den Validator; Git Bash.
- **Aufgaben:** SYNC-009 (Integration), SYNC-010 (neu), SETUP-005 und SETUP-002 (Portierungen), DET-001 (Portierung).

## Abweichung von der dokumentierten Umgebung

Die Testprojekte und der Server zielen auf `net10.0`; mit SDK 8 schlägt der Build fehl (`error NETSDK1045`), auch mit `-p:TargetFramework=net8.0` und `--framework net8.0`. Die Tests liefen deshalb wie in `docs/verification/2026-10-15-lobby-hardening.md` in einem **lokalen, nicht eingecheckten .NET-8-Harness**: je Testprojekt eine `net8.0`-Konsole (`EnableDefaultCompileItems=false`, `Compile Include="<repo>/tests/<X>.Tests/**/*.cs"`), die genau die Repo-Testquellen kompiliert und dieselben Bibliotheksprojekte referenziert; der Server als `net8.0`-Konsole über `src/Multiplayer.Foundation.Server/**/*.cs` mit Assembly-Name `Multiplayer.Foundation.Server`, damit `Client.Tests`, `Integration.Tests` und `ProcessIntegration.Tests` ihn wie bisher finden. Unter .NET 10 sind diese Läufe nicht wiederholt. `scripts/build-server.sh` (net10.0-Publish) wurde nicht ausgeführt; stattdessen der Server-Harness-Build.

## Ausgeführte Prüfungen (Reihenfolge wie gelaufen, alle nach dem letzten Code-Commit `85a10d4`)

| Befehl (aus `E:\RimworldMP-integration`) | Ergebnis |
|---|---|
| `RIMWORLD_MANAGED=/e/SteamLibrary/steamapps/common/RimWorld/RimWorldWin64_Data/Managed DOTNET_BIN=dotnet bash scripts/build-mod.sh` | Exit 0, 3 s; `0 Warnung(en)`, `0 Fehler`; `Installable mod: /e/RimworldMP-integration/dist/RimWorldMultiplayerFoundation` mit `0Harmony.dll`, `Multiplayer.Foundation.{Protocol,Network,Lobby,Session,Hosting,RimWorld}.dll` |
| `dotnet run --project <harness>/Protocol/Protocol.csproj -c Release --nologo` | Exit 0, 2 s; 20 PASS-Zeilen, zuletzt `PASS: 19 protocol test groups` (neu: Code 104 im Frame, 102/105 abgelehnt) |
| `dotnet run --project <harness>/Network/Network.csproj -c Release --nologo` | Exit 0, 6 s; 12 PASS |
| `dotnet run --project <harness>/Lobby/Lobby.csproj -c Release --nologo` | Exit 0, 2 s; 12 PASS |
| `dotnet run --project <harness>/Session/Session.csproj -c Release --nologo` | Exit 0, 3 s; 16 PASS, neu: `PASS: pace notice payload and limits` und `PASS: pace control: down after 1.5 s of held horizon, floor 1, choice remembered, up after 10 s, never above the choice, off switch, same switch tick`; die bestehenden Simulationen (6 Läufe, Beitritt, Resync, Abstimmungen) unverändert bestanden |
| `dotnet run --project <harness>/Hosting/Hosting.csproj -c Release --nologo` (1. Lauf) | Exit 0, 48 s; 30 PASS |
| dieselbe Zeile (2. Lauf, zeitabhängige Gruppen) | Exit 0, 48 s; 30 PASS |
| `dotnet run --project <harness>/Client/Client.csproj -c Release --nologo` | Exit 0, 6 s; 3 PASS |
| `dotnet run --project <harness>/Integration/Integration.csproj -c Release --nologo` | Exit 0, 12 s; 18 PASS |
| `dotnet run --project <harness>/ProcessIntegration/ProcessIntegration.csproj -c Release --nologo` | Exit 0, 2 s; 7 PASS (eigenständiger Serverprozess aus dem net8-Harness, zwei Client-Prozesse) |
| `dotnet build <harness>/Server/Server.csproj -c Release --nologo` | Exit 0; `0 Fehler` |
| `C:/Users/svenp/AppData/Local/Programs/Python/Python311/python.exe -I scripts/validate_project_state.py` | Exit 0; `PASS: 22 required files, 69 tasks, statuses, dependencies and references.` (vor dem Anlegen dieser Datei: drei `missing evidence`-Fehler, danach keine) |
| `git diff --check origin/main...HEAD` | Exit 0, keine Ausgabe |

Zuvor, während der Entwicklung: Nach jeder Portierung ein Mod-Build (je 0 Warnungen); der erste Lauf des Pace-Tests schlug fehl (`held=5050 step=-1`), weil ein zweiter `Advance`-Aufruf im selben Millisekundentakt den gehaltenen Horizont als frei wertete; behoben (`held = target >= limit`) und zusammen mit dem Clock-Basis-Fehler in `85a10d4` enthalten.

## Was die neuen Tests belegen (SYNC-010)

- Nutzdaten 104 hin und zurück; abgelehnt: Tempo über der Wahl, Grund ohne Deckelung, Deckelung ohne Grund, Tempo 0, abgeschnittene Nutzdaten.
- Drei Spiele, Host wählt 3×: 3 s mit allen am Horizont ergeben keinen Befehl und keine Meldung.
- Ein Spiel bleibt stehen: Der Horizont wird 2 s vor ihm gehalten; nach 1,5 s (gemessen 1 450–1 600 ms) geht `ScheduledKind.Speed = 2` mit Absender `Guid.Empty` und Grenze = Horizont byte-gleich an alle drei Spiele, dazu `PaceNotice` (2× statt 3×, Grund langsamstes Spiel, dessen Session und Tick). Innerhalb weiterer 2 s die zweite Stufe auf 1×; danach 3 s lang nichts mehr (Untergrenze).
- Wahl des Hosts unter der Deckelung (2× bei 1×): nur gemerkt und gemeldet, kein Befehl.
- Alle holen auf: Rückkehr auf 2× genau 10 s nach dem letzten Stall (gemessen 10 000–10 100 ms), Grund entfällt; in 25 weiteren Sekunden kein Schritt auf 3× (nie über die Wahl). Danach Wahl 3× bei ruhiger Verbindung: sofort als Befehl des Hosts.
- `AutoPace = false`: Horizont wird gehalten (`stuck + 720`), Tempo bleibt 3×.
- Drei `LockstepClient` mit demselben Befehl: vor der Grenze alle 1×, nach `TakeCommandsAt(Grenze)` alle 2×.

## Nicht geprüft (Laufzeit)

- Kein RimWorld-Start (Spieltests vom Benutzer ausgesetzt): Zwei-Client-Gleichlauf, Hot-Rejoin, Steam-Werte, Fraktionen, Tempoabsenkung und Overlay-Anzeige im Spiel.
- Die drei portierten Determinismus-Patches auf `main`s Tick-Treiber (`SessionRandom.Active` statt `LockstepDriver.Ticking`); ihr Nachweis stammt von `feat/sync-lockstep` (Selbsttest 5 × 6 000 Ticks gleich).
- Der portierte Start- und Gast-Welt-Fix im Spiel auf dieser Linie; Nachweise der Herkunftslinie in `2026-10-16-start-keeps-setup.md` (inkl. Benutzer-Solo-Lauf) und `2026-10-16-guest-world-discard.md`.
- `scripts/build-server.sh` unter .NET 10.

## Einordnung nach AGENTS.md Regel 25

`BUILD_ERFOLGREICH` (Mod 0 Warnungen, Server-Harness 0 Fehler); .NET-Tests im .NET-8-Harness bestanden; `IN_GAME_GETESTET`: nein; `MP_ZWEI_CLIENTS_GETESTET`: nein. `LAUFZEIT NICHT VERIFIZIERT`.
