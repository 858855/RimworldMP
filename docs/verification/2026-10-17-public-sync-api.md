# Verifikation – Öffentliche Sync-API v1 (API-001, ADR 0029)

- **Datum:** 2026-10-17
- **Branch:** `feat/mod-api` (Worktree `E:\RimworldMP-api`), von `main` @ `060d2be`; Code-Commit siehe `git log` (danach der Doku-Commit).
- **Umgebung:** Windows 11 Pro; RimWorld `1.6.4871` (`Version.txt` rev590, Prozess rev591), Core + Royalty, Ideology, Biotech, Anomaly, Odyssey + Foundation; .NET SDK 8.0.403; Lib.Harmony 2.4.3.
- **Designnotiz:** ADR 0029; Vertrag in `docs/extension-api.md`.

## Ausgeführte Prüfungen

| Befehl (aus `E:\RimworldMP-api`) | Ergebnis |
|---|---|
| `RIMWORLD_MANAGED=/e/SteamLibrary/steamapps/common/RimWorld/RimWorldWin64_Data/Managed DOTNET_BIN=dotnet bash scripts/build-mod.sh` | Exit 0; `0 Warnung(en)`, `0 Fehler`; `Multiplayer.Foundation.Api.dll` liegt in `dist/…/Assemblies` (acht DLLs) |
| `python -I scripts/package-mod.py RimWorldMultiplayerFoundation-mod-api-test.zip` | `PASS … 11 files` (Probearchiv danach gelöscht) |
| Installation: Sicherung `ModBackups/RimWorldMultiplayerFoundation-2026-10-17-before-api`, Ordner komplett ersetzt | acht DLLs, About, Lizenz |
| `python -I scripts/validate_project_state.py`; `git diff --check` | PASS (22 Pflichtdateien, 69 Aufgaben); leer |

Die acht .NET-Testprojekte wurden nicht erneut ausgeführt: geändert sind nur `src/Multiplayer.Foundation.RimWorld` und das neue `src/Multiplayer.Foundation.Api`; `Protocol`, `Lobby`, `Hosting`, `Session` entsprechen `main` @ `060d2be` (dort am 2026-10-17 bestanden). Für die API gibt es kein eigenes Testprojekt: Serialisierer und Registry brauchen `Verse`; sie werden im Spiel-Selbsttest geprüft.

## Selbsttest im Spiel

`RimWorldWin64.exe -mpWorldSelfTest -mpWorldSelfTestCoverage=0.3 -mpQuitAfterSelfTest -screen-fullscreen 0 -screen-width 1280 -screen-height 720`. Der neue Teil `ApiSelfTest` läuft im erzeugten Spiel (Stufe 2) ohne Netz: `MP.RegisterAll` auf dem eigenen Assembly (ein `[SyncWorker]`, ein `[SyncHash]`, eine `[SyncMethod]` mit 15 Parametern: `Pawn`, `ThingDef`, `IntVec3`, `string`, `int`, `float`, `bool`, `List<int>`, `Faction`, `Map`, `Area`, eigener Worker-Typ, `LocalTargetInfo`, `Rot4`, `Def` mit abweichendem Laufzeittyp `JobDef`), jeder Typ einzeln als Schreib/Lese-Roundtrip, direkter Aufruf außerhalb einer Partie, Kodieren wie der Prefix und Ausführen wie der Tick-Treiber (`ScheduledCommand` mit Ausstellerschlüssel), fehlende Referenz, unbekannte Kennung, 32-KiB-Grenze, Hash-Beitrag.

| Lauf | Stand | Ergebnis |
|---|---|---|
| 1 (14:15) | erste Fassung | `apiAvailable apiRegister apiLocalRuns apiEncode apiMissingNoop apiUnknownRejected apiSizeCap apiHash = ok`; `apiRoundtrip=FAIL apiExecuting=FAIL`: `NullReferenceException` in `SyncSerializer.Read`, Argument nicht benannt |
| 2 (14:19) | Roundtrip je Typ im Selbsttest, Argumentposition in der Fehlermeldung des Ausführers, Def-Typ über `GenDefDatabase.AllDefTypesWithDatabases` | alle `apiType.*=ok` außer `apiType.area=FAIL`; Fehlerzeile nennt „Argument 10“: der Zone-/Bereichs-Leser rief den `Map`-Leser auf, der ein Null-Bit erwartet, das der Schreiber dort nicht schreibt |
| 3 (14:20) | `ReadMap` ohne Null-Bit für Zone, Bereich und Karte | **`game self-test: PASS … apiAvailable=ok apiRegister=ok apiType.pawn=ok … apiType.area=ok … apiType.anyDef=ok apiLocalRuns=ok apiEncode=ok apiRoundtrip=ok apiExecuting=ok apiMissingNoop=ok apiUnknownRejected=ok apiSizeCap=ok apiHash=ok … fingerprint=ok … ticksHeldAfterLoad=ok`**; Befehl 148 Byte; Log `1080|Steel|(3, 0, 7)|Grüße ☃|-12345|0.75|True|1,2,3|20|0|0|1080:42|1080|1|JobDef:Goto`; `Multiplayer Foundation: … als No-op ausgeführt: Thing 1 auf Karte 2147483632 fehlt.` für die fehlende Referenz; keine Exception; Prozess nach 42 s beendet |

Der übrige Selbsttest (Welt, Startort-Seite, Figuren, Start, Save/Load Host und Gast, Fingerabdruck, Pause) bestand in allen drei Läufen unverändert; die Opcode-Tabelle ändert die Kodierung von Draft/Forbid/Goto nicht.

## Nicht geprüft

- Der Harmony-Prefix in einer echten Partie (Erfassung, Senden, Ausführung an der Tickgrenze auf zwei Clients); im Selbsttest läuft keine Sitzung, der Prefix lässt den Aufruf durch (`apiLocalRuns`).
- Ein echter Fremd-Mod gegen `Multiplayer.Foundation.Api.dll` (`examples/multiplayer/FoundationApiExample.cs` ist nicht gebaut), Instanzmethoden auf `ThingComp`, `Thing` in Behältern, Weltfiguren, `Zone`, `Bill`, Enums, Arrays, `Nullable<T>` (nur statisch im Code, kein Roundtrip im Selbsttest).
- Verhalten bei ungleichen Registrierungen zwischen Clients (soll die Mod-Prüfung ausschließen).

## Einordnung nach AGENTS.md Regel 25

`BUILD_ERFOLGREICH` (0 Warnungen); `IN_GAME_GETESTET` (Selbsttest, ein Prozess, ohne Netz: Registrierung, Serialisierung, Ausführung, Hash); `MP_ZWEI_CLIENTS_GETESTET`: nein.
