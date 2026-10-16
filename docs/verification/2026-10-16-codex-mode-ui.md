# Prüfung X5: Modusauswahl und IP-Identität

- **Datum/Umgebung:** 2026-10-16, Linux; lokale RimWorld-Installation `1.6.4871 rev598`, Unity/Mono; .NET SDK unter `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet`.
- **Branch/Basis:** `codex/mode-ui` von `origin/main` @ `b4255c6`. Commit: dynamisch `git rev-parse HEAD`.
- **Codeumfang:** `FoundationMod`, `FoundationSettings`, `MultiplayerSession`, `WorldSelfTest`; ADR 0044. Protokoll und Hosting stammen von `main`.

| Prüfung | Befehl | Ergebnis |
|---|---|---|
| Mod-Build | `dotnet build src/Multiplayer.Foundation.RimWorld/Multiplayer.Foundation.RimWorld.csproj -p:RimWorldManaged=/mnt/Spiele_1/SteamLibrary/steamapps/common/RimWorld/RimWorldLinux_Data/Managed --no-restore` | Exit 0; 0 Warnungen, 0 Fehler. Erstlauf Exit 1 durch neuen `Begin`-Parameter und Namenskollision `FactionRelation`; beide korrigiert und Build wiederholt. |
| Release-Mod | `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen, 0 Fehler; Ausgabe unter `dist/RimWorldMultiplayerFoundation/`. Kein Spielstart. |
| Protokoll | `dotnet run --project tests/Protocol.Tests/Protocol.Tests.csproj --no-restore` | Exit 0; 16 Testgruppen PASS, darunter Game-Mode und Key-Proof/Passwort. |
| Hosting | `dotnet run --project tests/Hosting.Tests/Hosting.Tests.csproj --no-restore` | Exit 0; ausgegebene Szenarien PASS, darunter Modus, IP-Passwort, Key-Pinning, gefälschte Kennung, Reconnect und Snapshot-Timeout. |
| Dokumentation | `python3 scripts/validate_project_state.py` | Exit 0; 22 Pflichtdateien, 61 Aufgaben, Statuswerte, Abhängigkeiten und Verweise geprüft. |
| Diff | `git diff --check` | Exit 0; keine Leerzeichenfehler. |

`dotnet test` auf diesen zwei Projekten gab Exit 0 ohne Testausgabe, weil sie Konsolenprogramme sind; der Nachweis stammt ausschließlich aus `dotnet run`.

**Nicht ausgeführt:** RimWorld-Start, UI-Bedienung, Mono-RSA zur Laufzeit, Save/Load, zwei Spielinstanzen, zwei echte Rechner, Steam und Windows. Der Benutzer will diese Spieltests erst später mit Sven durchführen. Der Build beweist keine Spielfunktion.

**Risiko:** IP-Passwort und Spielzustand laufen weiterhin ohne TLS über TCP. Die RSA-API ist in den lokalen Assemblies vorhanden und kompiliert, aber der RimWorld-Prozess wurde hier nicht gestartet.
