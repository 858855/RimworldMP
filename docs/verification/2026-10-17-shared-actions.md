# Verifikation – Geteilte Spieleraktionen, erster Schnitt (SYNC-002, Teil SYNC-004)

- **Datum:** 2026-10-17
- **Branch:** `feat/sync-commands` (lokal, nicht gepusht), auf `feat/sync-lockstep` @ `2a52807`
- **Umgebung:** RimWorld `1.6.4871` rev591, alle DLCs; .NET SDK 8.0.403; Lib.Harmony 2.4.3.
- **Designnotiz:** ADR 0025 (vom Besitzer freigegeben am 2026-10-17), mit Nachtrag.

## Anlass

Erster Zwei-Spieler-Lauf mit dem Gleichlauf (2026-10-16, drei Bildschirmfotos des Benutzers):
- Die Zeit lief bei beiden gleich; die Markierungen kamen bei beiden bei den Ticks 28, 2411, 2985 und 3060 an.
- Jede Aktion blieb aber lokal:
  - Bei Tick 420 hatte Tyrorift (Figur 205) beim Gast einen `Goto`-Job (Bewegungsbefehl), beim Host `GotoWander`. Daraus kam die gemeldete Abweichung bei Tick 420.
  - Der Host hatte Oak eingezogen; beim Gast nicht.
- Außerdem liefen verschiedene Spiel-Builds: Host rev591, Gast rev600.

## Umsetzung (Kurzfassung)

Siehe ADR 0025 mit Nachtrag.
- **Protokoll:** `SyncCodec` (Werte, Zellen als Reihen von Läufen).
- **Spiel:**
  - `SyncWorkers`: Verweise, Jobs mit Selbstprüfung, Designatoren.
  - `LockstepCommands`: Erfassen, Bündeln je Frame, Ausführen auf der Karte des Befehls, Auswahl für Zonen und Bereiche.
  - `LockstepCapturePatches`: Einstiegspunkte, alle Designator-Überschreibungen von RimWorld, lokale IDs der Spielerseite.
  - Erweiterte Prüfsumme; Anzeige.
- API-Signaturen per Metadaten gegen `Assembly-CSharp.dll` 1.6.4871 geprüft (kein dekompilierter Code), darunter:
  - `Pawn_JobTracker.TryTakeOrderedJob`, `Pawn_DraftController.Drafted`/`FireAtWill`
  - `Designator.DesignateSingleCell`/`DesignateMultiCell`/`DesignateThing`, `Designator_Build` (`entDef`, `stuffDef`, `styleDef`, `sourcePrecept`), `Designator_Place.placingRot`, `Designator_ZoneAdd`, `Designator_AreaAllowed.selectedArea`
  - `CompForbiddable.Forbidden`, `Pawn_WorkSettings.SetPriority`, `Pawn_TimetableTracker.SetAssignment`, `ResearchManager.SetCurrentProject`/`StopProject`, `Zone_Growing.SetPlantDefToGrow`
  - `UniqueIDsManager.GetNextID`, `Selector`, `Game.currentMapIndex`, `JobMaker.MakeJob`, `VerbTracker`/`CompEquippable.AllVerbs`

## Build

0 Warnungen, 0 Fehler. Ein erster Build ließ den C#-Compiler mit Stack Overflow abstürzen (Exit `-1073741571`). Die Halbierung fand einen Bedingungsausdruck mit LINQ-Lambda in `SyncWorkers.ReadJob`; er wurde durch Schleifen ersetzt. Danach musste der Build-Server neu starten (`dotnet build-server shutdown`).

## Automatisierte Tests (.NET-8-Harness)

| Projekt | Ergebnis |
|---|---|
| Protocol.Tests | Exit 0, 17 Gruppen (neu: Befehls-Nutzlast hin und zurück; ein 100 × 100-Zug sind 100 Läufe; fehlerhafte Läufe werden abgelehnt) |
| Session.Tests | Exit 0 |
| Lobby.Tests, Network.Tests, Hosting.Tests, Integration.Tests, Client.Tests | Exit 0 |

## Selbsttest im Spiel

`RimWorldWin64.exe -mpWorldSelfTest -mpWorldSelfTestCoverage=0.3 -mpQuitAfterSelfTest`. Die Installation lief mit Erlaubnis des Benutzers; danach wurde sein vorheriger Ordner (Build `sync-lockstep`) aus der Sicherung zurückgelegt.

**Befehls-Stufe**, pausiert, gegen den Sequencer im selben Prozess. Jede Aktion wird so aufgerufen wie von der Oberfläche:
- `cmdCapturedNotLocal`: Einziehen ändert lokal nichts, bis der Befehl zurückkommt.
- `cmdDraft`, `cmdOrder`.
- `localIds`: Der Job eines Rechtsklick-Menüs bekommt eine lokale ID; `sharedJobId`: Der ausgeführte bekommt eine gemeinsame ID.
- `designationsNotLocal`, `cmdMine`.
- `cmdBuild`: Bett aus Holz, nach Osten gedreht.
- `cmdZone`, `cmdZoneExpand` (über die Auswahl), `cmdGrowing`.
- `cmdForbid`, `cmdPriority`, `cmdSchedule`, `cmdResearch`, `cmdUndraft`.
- `cmdMissingNoop`: Ein Befehl für eine nicht vorhandene Figur ist ein No-op.
- `cmdSameForGuest`: Der Schein-Gast bekam dieselben Befehle.
- `cmdAllShared`: Nichts lief nur lokal.

**Determinismus:** Die fünf Läufe aus ADR 0024 spielen jetzt ein Skript von 10 Befehlen zu festen Ticks ab (Einziehen, Bewegen, Abbauen, Bett, Lager, Verbieten, Priorität, Zeitplan, Forschung, Entlassen).

| Lauf | Ergebnis |
|---|---|
| 1 | Alle Befehls-Prüfungen ok außer `sharedJobId`: Der beim Ausführen gelesene Job bekam eine lokale ID, weil das Lesen vor dem Ausführungs-Kennzeichen lag. Behoben: Das Lesen zählt mit. Determinismus mit Skript: gleich. |
| 2 | **PASS**, alle Stufen. 13 Aktionen erfasst, 11 Befehle gesendet (Bündeln), 13 Einträge ausgeführt, 1 fehlender Verweis. Determinismus 5 × 6 000 Ticks mit Skript gleich, Skript wirkt (Lager vorhanden). 0 Fehler im Log. |
| 3 | 60 000 Ticks mit Skript: Skript wirkt, 0 Fehler. Nur Lauf A (der erste nach der Befehls-Stufe) weicht ab Tick 13 380 von B und A2 ab; B und A2 sind gleich. Die beiden aufgezeichneten Läufe danach waren über alle 60 000 Ticks Ziehung für Ziehung gleich. Das ist dasselbe Muster wie im offenen Rest aus ADR 0024 (dort ab Tick 20 460, ohne Befehle). Die geteilten Aktionen selbst liefen in allen Läufen gleich. |

## Nicht geprüft

- **Zwei echte Spieler** (`MP_ZWEI_CLIENTS_GETESTET`: nein). Anleitung: `docs/reviews/2026-10-17-sync-commands.md`.
- Ein Klick von Hand mit Maus über die echte Oberfläche: Der Selbsttest ruft dieselben Einstiegspunkte im Code auf.
- Rechtsklick-Befehle mit Rechnung, Fähigkeit oder Lord: Sie bleiben lokal mit Hinweis.
- Designatoren anderer Mods, Malen, Pläne, Installieren.

## Einordnung nach AGENTS.md Regel 25

`BUILD_ERFOLGREICH` (0 Warnungen); Harness-Tests bestanden; `IN_GAME_GETESTET` (Selbsttest, ein Prozess, ohne Netz); `MP_ZWEI_CLIENTS_GETESTET`: nein.
