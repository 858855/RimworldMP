# Verifikation – Hostseitige Fraktionsgenerierung (FACTION-005, ADR 0028)

- **Datum:** 2026-10-17
- **Branch:** `feat/factions-generation` (Worktree `E:\RimworldMP-factions`), von `main` @ `060d2be`; Code-Commit siehe `git log` (danach der Doku-Commit).
- **Umgebung:** Windows 11 Pro; RimWorld `1.6.4871` (`Version.txt` rev590, Prozess rev591), Core + Royalty, Ideology, Biotech, Anomaly, Odyssey + Foundation; .NET SDK 8.0.403; Lib.Harmony 2.4.3.
- **Designnotiz:** ADR 0028.

## Ausgeführte Prüfungen

| Befehl (aus `E:\RimworldMP-factions`) | Ergebnis |
|---|---|
| `RIMWORLD_MANAGED=/e/SteamLibrary/steamapps/common/RimWorld/RimWorldWin64_Data/Managed DOTNET_BIN=dotnet bash scripts/build-mod.sh` | Exit 0; `0 Warnung(en)`, `0 Fehler` (Zwischenstände: `IdeoPresetDef.structureMeme` existiert in 1.6 nicht, entfernt; eine Nullable-Warnung als Fehler, behoben) |
| Installation: Sicherung `ModBackups/RimWorldMultiplayerFoundation-2026-10-17-before-factions`, Ordner komplett ersetzt | sieben DLLs, About, Lizenz |
| `python -I scripts/validate_project_state.py`; `git diff --check main...HEAD` | PASS (22 Pflichtdateien, 69 Aufgaben); leer |

Die acht .NET-Testprojekte wurden nicht erneut ausgeführt: nur `src/Multiplayer.Foundation.RimWorld` ist geändert; `Protocol`, `Lobby`, `Hosting`, `Session` entsprechen `main` @ `060d2be` (dort am 2026-10-17 bestanden).

## Selbsttest im Spiel

`RimWorldWin64.exe -mpWorldSelfTest -mpFactionSelfTest -mpWorldSelfTestCoverage=0.3 -mpQuitAfterSelfTest -screen-fullscreen 0 -screen-width 1280 -screen-height 720`. Der Fraktionsteil benutzt den Produktionscode `FactionGeneration` mit einem Zwei-Platz-Plan: Host (Platz 0) am gewählten Startort mit dem Vanilla-Erzähler; zweiter Spieler (Platz 1) mindestens 20 Felder entfernt, anderer Erzähler, erste installierte Ideologie-Vorlage. Nach der Landung Siedlung und Karte für Platz 1, Figur umgesetzt, `MultiplayerSession.Begin`, Speichern, Neuladen als Host, Laden als Gast, Prüfungen (`FactionRuntimeTestSlice.Diagnostic`).

| Lauf | Stand | Ergebnis |
|---|---|---|
| 1 (13:41) | Generierung ohne eigene Host-Ideologie | Fraktion und Karte erzeugt, Save/Load ok, aber `factionSetup=FAIL hostFactions=FAIL guestFactions=FAIL` mit `shared ideology`; der Host hatte vor der Landung keine Ideologie, Vanilla nahm bei `PrepForMapGen` die von Platz 1 |
| 2 (13:44) | Build fehlgeschlagen (Nullable), alter Stand lief erneut | wie Lauf 1; zusätzlich einmal „Object with load ID Thing_Human… is referenced (otherPawn) but is not deep-saved“ und ein Unity „Native Crash Reporting“-Block beim Start, Prozess lief normal weiter (in Lauf 3 und 4 nicht wieder aufgetreten) |
| 3 (13:46) | Host erhält vor der Landung eine eigene Ideologie | Vor der Landung getrennt (`Haxor-Archism` / `Free Creed`), nach der Landung wieder `shared ideology`: Vanillas `RecalculateIdeosBasedOnPlayerPawns` zählt die Figuren aller `isPlayer`-Fraktionen |
| 4 (13:48) | zusätzlich `FactionIdeosRecalculatePatch` | **`game self-test: PASS startKept=ok colonists=ok landedChosen=ok site=ok component=ok factionSetup=ok factionTick=skipped(Anomaly) ownersLanded=ok … hostFactions=ok … sessionLoaded=ok guestFactions=ok fingerprint=ok tick=ok … ticksHeldAfterLoad=ok`**; Log: `Fraktionen vor der Landung erzeugt: Host New Arrivals (20, Ideologie Haxor-Archism); Platz 1 Kolonie Test-Gast (21, Ideologie Liberal Creed)`, `Fraktionskolonien angelegt: Platz 0 … Feld 0 Erzähler Cassandra, Platz 1 Kolonie Test-Gast Feld 2 Erzähler Phoebe`, `before save error=none`, `host load error=none`, `guest load error=none`; Spielstand 14,5 MB roh mit zwei Karten; Prozess nach 52 s beendet; keine Exception |

Vanilla-Warnung in jedem Lauf: `Faction Kolonie Test-Gast of def PlayerColony has no PawnGroupMakers.` (zweimal beim Anlegen der zweiten Siedlung; die Spieler-Def hat keine Pawn-Gruppen für Überfälle/Besuche).

## Nicht geprüft

- `factionTick`: Mit aktivem Anomaly verweigert die Erzähler-Regel (ADR 0046) jeden Fraktions-Tick; die Prüfung wurde als übersprungen ausgewiesen, nicht als bestanden. Auf einem Profil ohne Anomaly erneut prüfen.
- Der echte Lobby-Startpfad mit zwei Clients (Server-Sperre, `-mpFactionStart`), bearbeitete Ideologien (Art 2) und die feste Wahl ohne DLC, Beziehung feindlich/verbündet im Spiel, Ereignisse über laufende Ticks, getrennte Zonen/Forschung/Arbeitsplan (FACTION-002).

## Einordnung nach AGENTS.md Regel 25

`BUILD_ERFOLGREICH` (0 Warnungen); `IN_GAME_GETESTET` (Selbsttest, ein Prozess, ohne Netz; Generierung, Save/Load, Fingerabdruck); `MP_ZWEI_CLIENTS_GETESTET`: nein.
