# Merge-Prüfung für X1/X4/X5-Vorschaukandidat

- **Datum/Umgebung:** 2026-10-16, Linux, .NET SDK unter `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet`, lokale RimWorld-1.6-Assemblies `1.6.4871 rev598`.
- **Quellbranch:** `codex/manual-test-candidate` @ `22f8e83` vor diesem Prüfnachweis; `origin/main` @ `b4255c6` nach `git fetch origin`. Quellbranch ist Nachfahre von `origin/main`; `git diff --check origin/main...HEAD` war leer.
- **Freigabe:** Benutzeranweisung „push und merge“ vom 2026-10-16 für diesen Kandidaten. Kein Spiel-Laufzeittest in dieser Prüfung.

| Prüfung | Befehl | Tatsächliches Ergebnis |
|---|---|---|
| Mod-Build | `env DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Exit 0, 0 Warnungen, 0 Fehler. |
| Protocol | `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Protocol.Tests/Protocol.Tests.csproj --no-restore` | Exit 0, 16 Gruppen PASS. |
| Network | Gleicher `dotnet run`-Aufruf für `tests/Network.Tests/Network.Tests.csproj` | Exit 0, alle ausgegebenen Szenarien PASS. |
| Lobby | Gleicher Aufruf für `tests/Lobby.Tests/Lobby.Tests.csproj` | Exit 0, alle Szenarien PASS. |
| Hosting | Gleicher Aufruf für `tests/Hosting.Tests/Hosting.Tests.csproj` | Exit 0, alle Szenarien PASS. |
| Client | Gleicher Aufruf für `tests/Client.Tests/Client.Tests.csproj` | Exit 0, 3 Szenarien PASS. |
| Session | Gleicher Aufruf für `tests/Session.Tests/Session.Tests.csproj` | Exit 0, alle Simulationen PASS. |
| Integration | Gleicher Aufruf für `tests/Integration.Tests/Integration.Tests.csproj` | Exit 0, alle Szenarien PASS. |
| ProcessIntegration | Gleicher Aufruf für `tests/ProcessIntegration.Tests/ProcessIntegration.Tests.csproj` | Exit 0, eigenständiger Server und zwei Clientprozesse PASS. |
| Projektdokumentation | `python3 scripts/validate_project_state.py` | Exit 0, 22 Pflichtdateien, 62 Aufgaben, Status/Abhängigkeiten/Referenzen gültig. |
| ZIP-Prüfsumme | `sha256sum releases/RimWorldMultiplayerFoundation-0.2.0-preview-f04cb14-rw1.6.zip` | Exit 0, SHA-256 `34d73bc0b24718b5658e750115f82cc2206aa286e48254108beebaa9933e290c`. |

**Grenze:** Kein laufendes RimWorld gestartet. Die .NET-Tests prüfen den Netzwerk-/Koordinator-Kern, nicht den tatsächlichen Gameplay-Gleichlauf. X4 bleibt eine Teilimplementierung; Pfadsuche, Harmony-Hooks und Save/Load im Spiel sind offen. Testanweisungen für Fabio und Sven: `docs/verification/TEST_PROMPTS_FABIO_SVEN.md`.
