# FACTION-003 – Integration der Storyteller-UI mit dem Lobbyvertrag

- **Branch/Basis:** `codex/faction-ui` von `main` @ `ea065aa`; vorherige Codex-Commits als `214b5ee` und `7bd721c` übernommen. Kein Push oder Merge durch Codex.
- **Umgebung:** Linux, lokale RimWorld-Installation `1.6.4871 rev598` laut `Version.txt`, Unity/Mono; .NET SDK 10.0.402 unter `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet`. Der zuvor beobachtete Spielprozess meldete rev600. Für diese Arbeit wurde RimWorld nicht gestartet.
- **Änderung:** Host liest die Schwierigkeit aus `Page_SelectStoryteller`, filtert lokal installierte, sichtbare Erzähler nach Kompatibilität und gültigem Def-Namen und veröffentlicht die Liste mit Code 96. Spieler wählen einen angebotenen lokalen Erzähler mit Code 97 und melden Fertig mit der vorhandenen Lobby-Nachricht. Die Einrichtung zeigt Fraktionsplatz, eigene Wahl und Fertig-Zustand. Lobby und Server bleiben autoritativ; der UI-Code ändert keinen Spielzustand direkt.
- **Vertrag:** `FactionOptions.Difficulty` ist nur ein `DifficultyDef.defName`; die Custom-Werte bleiben im Host-Spielstand. Bei geänderten Optionen setzt Claudes Lobby-Kern Fertig zurück. Der Fraktionsstart bleibt gesperrt.

## Ausgeführte Prüfungen

| Befehl | Ergebnis |
|---|---|
| `env DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen, 0 Fehler; Release-Build gegen lokale 1.6-Assemblies. |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Protocol.Tests/Protocol.Tests.csproj -c Release --nologo` | Exit 0; 18 PASS-Gruppen. |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Lobby.Tests/Lobby.Tests.csproj -c Release --nologo` | Exit 0; Fraktionsplatz, Storyteller-Wahl und Fertig-Regeln bestanden. |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Hosting.Tests/Hosting.Tests.csproj -c Release --nologo` | Exit 0; Nachrichten 96/97, Server-Routing, Ablehnungen und Reconnect bestanden. |
| `python3 scripts/validate_project_state.py` | Exit 0; 22 Pflichtdateien, 64 Aufgaben, Status, Abhängigkeiten und Referenzen geprüft. |
| `git diff --check` | Exit 0; keine Whitespace-Fehler. |

**Nicht geprüft:** Klickpfad in laufendem RimWorld, tatsächlicher Save/Load der neuen Fraktionsdaten, mehrere Storyteller im Tick, Ereignisziele und zwei Spielclients. Die Tests prüfen den Vertrag und Server, nicht das UI zur Laufzeit. Der produktive Fraktionsstart ist wegen FACTION-001 und Incident-Isolation noch nicht freigegeben.
