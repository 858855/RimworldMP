# Verifikation – Lobby 0.2-Vorschau (VER-001, ID-001, XFER-001, MOD-001, START-001)

- **Datum:** 2026-10-15
- **Branch:** `feat/lobby-hardening` (lokal, nicht gepusht), Basis `docs/full-multiplayer-plan` (ungemergt) auf `main` @ `cb63a22`
- **Umgebung:** Windows 11 Pro 10.0.26200; RimWorld `1.6.4871` (`Version.txt` rev590, Spielprozess rev591) unter `E:/SteamLibrary/steamapps/common/RimWorld`; .NET SDK 8.0.403 (kein .NET 10 installiert); Lib.Harmony 2.4.3 aus nuget.org; Python 3.14.3.

## Abweichung von der dokumentierten Umgebung

Die Testprojekte zielen auf `net10.0`; auf diesem Rechner ist nur .NET 8 installiert, und der Benutzer wollte kein .NET 10 installieren und keinen Dedicated Server bauen. Die Tests liefen deshalb in einem **lokalen, nicht eingecheckten .NET-8-Harness**: je Testprojekt eine `net8.0`-Konsole, die genau die Quelldateien des Repo-Testprojekts kompiliert und dieselben Bibliotheksprojekte referenziert. Integration.Tests und Client.Tests kompilieren statt des Serverprojekts nur `src/Multiplayer.Foundation.Server/ServerConfig.cs`. `ProcessIntegration.Tests` (startet den Serverprozess) lief **nicht**. Unter .NET 10 sind die Tests nicht erneut ausgeführt.

## Ausgeführte Prüfungen

| Prüfung | Befehl / Vorgehen | Ergebnis |
|---|---|---|
| API-Metadaten (VER-001) | `System.Reflection.Metadata` gegen `Assembly-CSharp.dll`, 143 Abfragen | alle benötigten Symbole vorhanden, siehe `references/verified-apis.md` |
| Protocol.Tests | .NET-8-Harness | Exit 0, 9 Gruppen (neu: Spielerschlüssel/Hello, Chunk-Übertragung, Mod-Manifest) |
| Lobby.Tests | .NET-8-Harness | Exit 0, 3 Gruppen (neu: Mod-Status/Ready-Sperre, Start/Sperre/Abbruch) |
| Network.Tests | .NET-8-Harness | Exit 0, 5 Gruppen |
| Hosting.Tests | .NET-8-Harness | Exit 0, 8 Gruppen |
| Integration.Tests | .NET-8-Harness, echte TCP-Verbindungen | Exit 0, 24 Gruppen (neu: Schlüssel ersetzt alte Sitzung, 50-MB-Übertragung in ~150 ms, Ziel außerhalb der Lobby abgelehnt, Abbruch bei Empfänger-Trennung, Mod-Status/Ready-Sperre/Mod-Listen-Anfrage, Start/Sperre/Abbruch) |
| Client.Tests | .NET-8-Harness | Exit 0, 3 Gruppen |
| Mod-Build | `RIMWORLD_MANAGED=E:/SteamLibrary/steamapps/common/RimWorld/RimWorldWin64_Data/Managed DOTNET_BIN=dotnet bash scripts/build-mod.sh` | 0 Warnungen, 0 Fehler; `dist/RimWorldMultiplayerFoundation/` mit About, 5 eigenen DLLs, `0Harmony.dll`, Lizenz |
| Projektdateien | `python scripts/validate_project_state.py` (PyYAML in venv) | PASS |
| Installation | Alte Installation nach `RimWorld/ModBackups/RimWorldMultiplayerFoundation-2026-10-15-before-0.2-preview` kopiert, neue nach `RimWorld/Mods/RimWorldMultiplayerFoundation` | Dateien und SHA-256 geprüft |
| Spielstart (Rauchtest) | `RimWorldWin64.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720`, aktive Mods: Core, 5 DLCs, Foundation | Hauptmenü geladen; `Player.log`: „Multiplayer Foundation: 7 Mods geprüft, Prüfsumme 3df9“, keine Fehler oder Exceptions; Spiel danach regulär geschlossen |

## Nachtrag: Prüfsumme ohne Spielversion

Auf Wunsch des Benutzers hängt die Lobby-Prüfsumme nur noch von den Mods ab (Package-ID, Version, Inhalts-Hash in Ladereihenfolge), nicht von Spielversion oder Anzeigenamen. Protocol.Tests prüft das neu: anderer Spielbuild und andere Anzeigenamen ergeben dieselbe Prüfsumme, der Bericht meldet die Spielversion nur als Hinweis. Alle sechs Testprojekte im .NET-8-Harness erneut bestanden; Mod neu gebaut (0 Warnungen) und über die installierte Vorschau kopiert.

## Einordnung nach AGENTS.md Regel 25

- `BUILD_ERFOLGREICH` für den Mod gegen die lokale 1.6-Installation.
- Automatisierte Tests bestanden (.NET-8-Harness, siehe oben).
- `IN_GAME_GETESTET` **nur** für: Mod lädt, Manifest wird im Spiel gebaut und geloggt. Lobby-UI, Mod-Status-Anzeige, Unterschiede-Fenster, Start-Knopf und Einrichtungsansicht wurden im Spiel **nicht** bedient.
- `MP_ZWEI_CLIENTS_GETESTET`: **nein**. Offen: zwei echte RimWorld-Clients (gleiche und absichtlich abweichende Modliste), Steam-P2P-Lobby, Start und Abbruch.
