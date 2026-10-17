# Verifikation – Alpha 0.3.0-alpha.1 (Zusammenführung der Feature-Branches)

- **Datum:** 2026-10-17
- **Branch:** lokaler `main` in `E:\RimworldMP`: `060d2be` + Merges `2ab54e1` (`feat/permissions`), `6077751` (`feat/continue-save`), `8212fc4` (`feat/factions-generation`), `c28718c` (`feat/mod-api`) + Release-Commit (Version, Hash-Fix, Paket, Doku). Nicht gepusht.
- **Umgebung:** Windows 11 Pro; RimWorld `1.6.4871` (`Version.txt` rev590, Prozess rev591), Core + Royalty, Ideology, Biotech, Anomaly, Odyssey + Foundation; .NET SDK 8.0.403 (kein .NET 10, Tests im lokalen .NET-8-Harness wie in `docs/build.md` beschrieben); Lib.Harmony 2.4.3.
- **Review:** `docs/reviews/2026-10-17-alpha-merge.md`.

## Build und Tests

| Schritt | Ergebnis |
|---|---|
| `RIMWORLD_MANAGED=… DOTNET_BIN=dotnet bash scripts/build-mod.sh` (Merge-Stand `c28718c` und nach dem Hash-Fix) | Exit 0; `0 Warnung(en)`, `0 Fehler`; acht eigene DLLs in `dist/…/Assemblies` |
| Harness `Protocol` | exit 0, `PASS: 19 protocol test groups` |
| Harness `Network` | exit 0, 12 PASS |
| Harness `Lobby` | exit 0, 13 PASS (inkl. Fortsetzen eines Coop-Spielstands) |
| Harness `Session` | exit 0, 16 PASS (inkl. Tempoanpassung) |
| Harness `Hosting` (zweimal) | exit 0, 31 PASS je Lauf (inkl. Restore über 105 Ende zu Ende) |
| Harness `Client`, `Integration`, `ProcessIntegration` | exit 0; 3, 18, 7 PASS |
| Harness Server-Build (`Multiplayer.Foundation.Server`) | exit 0 |
| `python -I scripts/validate_project_state.py` | PASS (22 Pflichtdateien, 69 Aufgaben) |
| `git diff --check` über Merges und Release-Commit | leer |
| `python -I scripts/package-mod.py` | `PASS: releases\RimWorldMultiplayerFoundation-0.3.0-alpha.1-rw1.6.zip (1085418 bytes, 11 files)`, SHA-256 `bc44fee8699752037d86ed0b95ebcc0934250648d68364ffcd41da9adcde2db7` |

Die Harness-Läufe fanden auf `c28718c` statt; der anschließende Hash-Fix berührt nur `src/Multiplayer.Foundation.RimWorld`, das kein Testprojekt referenziert.

## Selbsttests im Spiel

Installation: Sicherung `ModBackups/RimWorldMultiplayerFoundation-2026-10-17-before-alpha`, Ordner komplett ersetzt (acht DLLs). Jeder Lauf mit `-mpWorldSelfTestCoverage=0.3 -mpQuitAfterSelfTest -screen-fullscreen 0 -screen-width 1280 -screen-height 720`; Logs liegen lokal in `dist/selftest-*.log` (nicht eingecheckt).

| Lauf | Schalter | Stand | Ergebnis |
|---|---|---|---|
| 1 (14:34) | `-mpWorldSelfTest` | `c28718c` | `game self-test: PASS` inkl. `controlDenied=ok controlAllowed=ok` (ADR 0026) und allen API-Prüfungen `apiAvailable … apiHash=ok` (ADR 0029); Welt-, Seiten-, Figuren-, Verwerfen-Tests PASS; `fingerprint be08a8 -> be08a8`; 0 Exceptions |
| 2 (14:36) | `-mpWorldSelfTest -mpContinueSelfTest` | `c28718c` | `game self-test: PASS` inkl. Rechte- und API-Prüfungen und `continueLobby … continueHeld=ok` (ADR 0027); 0 Exceptions |
| 3 (14:37) | `-mpWorldSelfTest -mpFactionSelfTest` | `c28718c` | Fraktionen, zwei Karten, Save/Load `factionSetup=ok hostFactions=ok guestFactions=ok fingerprint=ok`, aber `api=FAIL`: `NullReferenceException` in `GoodwillSituationManager.GetMaxGoodwill` aus `SessionStateHash.Compute` (`Faction.GoodwillWith` für die zweite Spielerfraktion). Latenter Fehler des Hashes im Fraktionsmodus, erst durch die API-Hash-Prüfung im Fraktionsspiel sichtbar |
| 3b (14:39) | `-mpWorldSelfTest -mpFactionSelfTest` | Hash-Fix (`RelationWith(other, true)?.baseGoodwill`) | **`game self-test: PASS`** inkl. `factionSetup=ok factionTick=skipped(Anomaly) … apiHash=ok … hostFactions=ok … guestFactions=ok fingerprint d6071c -> d6071c`; zwei Karten, Spielstand 14,6 MB roh; 0 Exceptions |
| 1b (14:40) | `-mpWorldSelfTest` | Hash-Fix | **`game self-test: PASS`** wie Lauf 1; 0 Exceptions |

Lauf 2 wurde nach dem Hash-Fix nicht wiederholt: Der Continue-Pfad führt keinen Tick und keine Hash-Prüfung aus, und sein Code ist vom Fix nicht berührt; die Rechte- und API-Prüfungen desselben Pfads sind mit Lauf 1b abgedeckt.

## Nicht geprüft

- **Zwei Clients**: keine Funktion dieser Linie wurde mit zwei Spielprozessen geprüft (Lockstep, Befehle, Rechte, Fortsetzen, Fraktionen, API-Prefix in einer echten Sitzung). Die Alpha ist dafür gedacht.
- Fraktions-Tick mit aktivem Anomaly (Erzähler-Regel ADR 0046), Lobby-Startpfad des Fraktionsmodus (Server-Sperre), bearbeitete Ideologien.
- Fremd-Mod gegen `Multiplayer.Foundation.Api.dll`.
- Steam-Transport und Internet-Hosting dieses Stands.

## Einordnung nach AGENTS.md Regel 25

`BUILD_ERFOLGREICH` (0 Warnungen, alle Testprojekte, Server-Build); `IN_GAME_GETESTET` (drei Selbsttest-Pfade, ein Prozess, ohne Netz); `MP_ZWEI_CLIENTS_GETESTET`: **nein**.
