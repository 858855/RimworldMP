# Verifikation – Mod-Prüfung, Start-Knopf, Seitenspiegel (MOD-001, START-001, SETUP-001)

- **Datum:** 2026-10-15
- **Branch:** `feat/mod-check-setup-ui` (lokal), Basis `origin/feat/setup-rules` @ `5798f95`
- **Umgebung:** Windows 11 Pro; RimWorld `1.6.4871` (Spielprozess rev591) unter `E:/SteamLibrary/steamapps/common/RimWorld`; .NET SDK 8.0.403; Lib.Harmony 2.4.3.

## Abweichung von der dokumentierten Umgebung

Wie in `docs/verification/2026-10-15-lobby-hardening.md`: Die Testprojekte zielen auf `net10.0`, hier ist nur .NET 8 installiert; Tests liefen im lokalen, nicht eingecheckten .NET-8-Harness, der genau die Repo-Testquellen kompiliert. `ProcessIntegration.Tests` und der Serverbuild liefen nicht (Benutzerwunsch: kein Dedicated Server).

## Ausgeführte Prüfungen

| Prüfung | Ergebnis |
|---|---|
| Basis `feat/setup-rules` vor Änderungen, sechs Testprojekte | alle bestanden |
| Protocol.Tests | Exit 0, 12 Gruppen (neu: Mod-Manifest/Bericht/Inhalts-Hash, Mod-Status und Host-Mods in Nutzdaten, Seitenspiegel-Nutzdaten) |
| Lobby.Tests | Exit 0 (neu: Mod-Status, Bereit-Sperre, Start-Prüfung, Host-Wechsel) |
| Hosting.Tests | Exit 0 (neu: Mod-Prüfung über Steam-ähnlichen Strom inkl. Listenübertragung und Bericht; Seitenspiegel nur in der Einrichtung, nur vom Host) |
| Network.Tests, Integration.Tests, Client.Tests | Exit 0 |
| Mod-Build `scripts/build-mod.sh` gegen lokale 1.6-Assemblies | 0 Warnungen, 0 Fehler |
| Installation | Build über die installierte Vorschau kopiert (Sicherung der 0.1-Version in `ModBackups` aus der vorigen Sitzung) |
| Rauchtest im Spiel | Hauptmenü nach 14 s, `Player.log`: „Multiplayer Foundation: 7 Mods geprüft, Prüfsumme 69d3“, keine Fehler; Harmony-Patches ohne Fehler geladen; Spiel regulär geschlossen |
| Projektdateien | `scripts/validate_project_state.py`: PASS |

## Einordnung nach AGENTS.md Regel 25

- `BUILD_ERFOLGREICH`; automatisierte Tests bestanden.
- `IN_GAME_GETESTET` nur: Mod lädt, Manifest im Spiel gebaut, Patches geladen.
- **Nicht** im Spiel bedient: Start-Knopf, Einrichtungsseiten des Hosts mit Spiegel, Feld über den Seiten, Sperre des letzten Starts, Abbruch über „Zurück“. Die Mod-Prüfung und der Start-Knopf wurden in einer früheren Fassung (`feat/lobby-hardening`) vom Benutzer mit zwei Spielern als funktionsfähig gemeldet; diese übertragene Fassung ist noch nicht mit zwei Spielern geprüft.
