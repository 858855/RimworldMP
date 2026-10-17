# Verifikation – Steuerungsrechte für eigene Figuren (SYNC-005 erster Schnitt, OWN-001 Anzeige)

- **Datum:** 2026-10-17
- **Branch:** `feat/permissions` (Worktree `E:\RimworldMP-permissions`), von `main` @ `060d2be`; Kopf siehe `git log -1`.
- **Umgebung:** Windows 11 Pro; RimWorld `1.6.4871` (`Version.txt` rev590) unter `E:/SteamLibrary/steamapps/common/RimWorld`; .NET SDK 8.0.403; Lib.Harmony 2.4.3; Python 3.11 mit PyYAML.
- **Designnotiz:** ADR 0026.

## Ausgeführte Prüfungen

| Befehl (aus `E:\RimworldMP-permissions`) | Ergebnis |
|---|---|
| `RIMWORLD_MANAGED=/e/SteamLibrary/steamapps/common/RimWorld/RimWorldWin64_Data/Managed DOTNET_BIN=dotnet bash scripts/build-mod.sh` | Exit 0; `0 Warnung(en)`, `0 Fehler`; `Installable mod: /e/RimworldMP-permissions/dist/RimWorldMultiplayerFoundation` |
| `C:/Users/svenp/AppData/Local/Programs/Python/Python311/python.exe -I scripts/validate_project_state.py` | siehe Ergebniszeile im Commit-Text des Doku-Commits (PASS erwartet: 22 Pflichtdateien, 69 Aufgaben) |
| `git diff --check main...HEAD` | leer |
| Installation: Sicherung nach `RimWorld/ModBackups/RimWorldMultiplayerFoundation-2026-10-17-before-permissions`, Mod-Ordner komplett durch `dist/RimWorldMultiplayerFoundation` ersetzt (RimWorld.dll byte-gleich mit dist) | sieben DLLs, About, Lizenz |
| `RimWorldWin64.exe -mpWorldSelfTest -mpWorldSelfTestCoverage=0.3 -mpQuitAfterSelfTest -screen-fullscreen 0 -screen-width 1280 -screen-height 720` (2026-10-17 04:18, Prozess rev591, Core + 5 DLCs + Foundation) | Prozess nach 42 s beendet. `Player.log`: `world self-test: PASS … fingerprint 9ab6ad -> 9ab6ad`, `site page PASS`, `pawns PASS`, `world discard PASS`; **`game self-test: PASS startKept=ok colonists=ok landedChosen=ok site=ok component=ok ownersLanded=ok controlDenied=ok controlAllowed=ok … sessionLoaded=ok fingerprint=ok tick=ok … ticksHeldAfterLoad=ok`** (players 2, owners 2). Dazu die erwartete Zeile `Zeichnen-Befehl von local:… für Mikey abgelehnt: Mikey gehört Test-Host.` Keine Exception, keine `Root level exception`, kein Steam-Fehler beim Beenden, keine `TypeLoadException` (Ordner komplett ersetzt). |

Die acht .NET-Testprojekte wurden nicht erneut ausgeführt: Geändert ist nur `src/Multiplayer.Foundation.RimWorld` (kein Testprojekt bindet es ein); `Protocol`, `Session`, `Hosting` sind unverändert gegenüber `main` @ `060d2be`, wo sie am 2026-10-17 im .NET-8-Harness bestanden (`2026-10-17-integration-sync.md`).

## Geänderte Pfade (statisch nachvollzogen)

- `GameCommandCodec.Execute(command, localSession)`: Draft und Goto prüfen `PawnControl.Denial(pawn, command.IssuerKey, session)` nach dem Auflösen der Figur; Ablehnung = No-op mit `Log.Message`, Hinweis nur bei `IssuerSession == localSession`. Forbid unverändert.
- `FoundationMod.CaptureDraft`/`CaptureOrderedJob`: dieselbe Regel mit dem eigenen Schlüssel aus `lobby.Players`; abgelehnte Aktion läuft nicht lokal und wird nicht gesendet.
- `FoundationMod.ExecuteGameCommand` übergibt `client.SessionId`.
- `MultiplayerSession.OwnerKeyOf`, `PlayerNameOf`, `FactionOwnerNameOf`; `PawnOwnerInspectPatch` (Anzeige).
- `WorldSelfTest`: nach `ownersLanded` die Prüfungen `controlDenied` und `controlAllowed` (Zeichnen-Befehl mit fremdem bzw. eigenem Schlüssel über `GameCommandCodec.Execute`), Zustand danach zurückgesetzt.

## Nicht geprüft

- Zwei Clients: Gast zeichnet die Figur des Hosts (Hinweis, keine Wirkung bei beiden), eigene Figur (Wirkung bei beiden, gleiche Hashes), Fraktionsmodus (fremde Fraktion abgelehnt).
- Inspektionszeile im Spiel.

## Einordnung nach AGENTS.md Regel 25

`BUILD_ERFOLGREICH` (0 Warnungen); `IN_GAME_GETESTET` (Selbsttest, ein Prozess, ohne Netz: fremder Schlüssel abgelehnt, eigener angewandt); `MP_ZWEI_CLIENTS_GETESTET`: nein.
