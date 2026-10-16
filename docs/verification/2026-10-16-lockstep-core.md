# Verifikation – Lockstep-Kern: Sequencer, Tick-Horizont, Tick-Treiber (SYNC-001, Minimum DET-001)

- **Datum:** 2026-10-16
- **Branch:** `feat/sync-lockstep` (lokal, nicht gepusht), auf `feat/mod-check-setup-ui` @ `5c3f2b7`
- **Umgebung:** RimWorld `1.6.4871` rev591, alle DLCs; .NET SDK 8.0.403; Lib.Harmony 2.4.3.
- **Designnotiz:** ADR 0024 (vom Besitzer freigegeben am 2026-10-16), mit Nachtrag zu den Determinismus-Befunden.

## Anlass

Zwei Zwei-Spieler-Läufe vom 2026-10-16 landeten beide Spieler. Danach blieb das Spiel absichtlich pausiert (ADR 0023). Der Benutzer meldete: „Der Gleichlauf passiert auch nach 2 Minuten nicht.“ Dieser Schnitt lässt die Zeit im Gleichschritt laufen.

## Umsetzung (Kurzfassung)

- **Protokoll:** Wire-Version 4, Nachrichten 38–43. Siehe `docs/protocol.md`, Abschnitt Gleichlauf.
- **Neues Projekt `Multiplayer.Foundation.Session`** (netstandard2.0, ohne Verse):
  - `LockstepSequencer`: Tick-Vergabe, 50-ms-Takt, Flusskontrolle mit 2 s Vorsprung, Geschwindigkeitsregeln, Hash-Vergleich, Grenzen.
  - `LockstepSessions`: alle Spiele eines Servers.
  - Für den Client: `LockstepClientState`, `TickPacer`, `SessionSeed`, `StateHasher`.
- **Server:** `FoundationServer` hat einen eigenen Takt und ist der einzige Sender der Gleichlauf-Nachrichten. `LobbyRegistry` liefert die laufenden Spiele, mit neuer Spiel-ID je Spiel.
- **Spiel:**
  - `LockstepDriver`: führt Befehle bei t − 1 aus, läuft Ticks bis zum Horizont, mit Zufall je Tick und Befehl.
  - `GameStateHash`: Prüfsumme alle 60 Ticks.
  - Patches auf `TickManager`: Tick nur vom Treiber; Geschwindigkeit, Leertaste und Autopause werden Anfrage; kein Autosave.
  - Gegenmaßnahmen aus DET-001: `LockstepVisualRandPatch`, `LockstepTickRatePatch`, `LockstepPathRequestPatch` (siehe ADR-Nachtrag).
  - Anzeige im Feld oben rechts; Knopf „Testmarkierung“.
- API-Signaturen per Metadaten gegen `Assembly-CSharp.dll` 1.6.4871 geprüft (kein dekompilierter Code), darunter:
  - `TickManager.TickManagerUpdate`/`DoSingleTick`/`CurTimeSpeed`/`TogglePaused`/`Pause`/`TickRateMultiplier`/`slower`, `TimeSlower.ForcedNormalSpeed`
  - `Rand.PushState(int)`/`PopState`/`StateCompressed`, `MurmurHash.GetInt`
  - `Autosaver.AutosaverTick`
  - `PathFinder.PushRequest`/`FindPathNow`, `PathRequest.Resolve`/`ResultIsReady`
  - `FishShadowComponent.MapComponentTick`, `SoundStarter.*`, `SubSustainer.StartSample`
  - `Pawn.UpdateRateTicks`, `Thing.MinTickIntervalRate`/`MaxTickIntervalRate`
  - `Map.listerThings`, `Thing.thingIDNumber`, `Def.index`, `Need.CurLevel`

## Build

`RIMWORLD_MANAGED=E:/SteamLibrary/steamapps/common/RimWorld/RimWorldWin64_Data/Managed bash scripts/build-mod.sh`: 0 Warnungen, 0 Fehler. Neu im Paket: `Assemblies/Multiplayer.Foundation.Session.dll` (auch in `scripts/package-mod.py`).

## Automatisierte Tests (.NET-8-Harness)

Wie in `2026-10-16-guest-character-page.md`: `net8.0`-Wrapper im Sitzungs-Scratchpad um die unveränderten Repo-Testquellen. Neu ist ein Wrapper für `tests/Session.Tests`.

| Projekt | Ergebnis |
|---|---|
| Protocol.Tests | Exit 0, 16 Gruppen (neu: Gleichlauf-Nutzlasten hin und zurück, Grenzen; Wire-Version 4) |
| Session.Tests (neu) | Exit 0, 11 Gruppen: Seeds und Hash, Pacer, Client-Warteschlange, Start und Pause, Tick-Vergabe und Reihenfolge, 200 zufällige Abläufe (jeder Befehl vor dem Horizont, der ihn erreicht; alle bekommen dieselbe Folge), Flusskontrolle (auch in 16-ms-Schritten), Geschwindigkeitsregeln, Grenzen, Hash-Vergleich, Spiele je Server |
| Lobby.Tests | Exit 0 (neu: laufendes Spiel mit Mitgliedern und Spiel-ID, nach Abbruch keins) |
| Network.Tests | Exit 0 |
| Hosting.Tests | Exit 0 (neu, echter Server über Steam-ähnliche Verbindung): Start beim gemeldeten Tick, pausiert; Gast setzt fort, der Befehl kommt bei beiden mit Tick 1001 vor dem Horizont; der eingefrorene Gast hält den Horizont bei 1120 und wird genannt; Stufe nur vom Host (Code 3); Markierung mit gleichem Tick und `seq`; gleiche Hashes stehen im Horizont, abweichende melden den Gast; nach Abbruch Code 6 |
| Integration.Tests | Exit 0 |
| Client.Tests | Exit 0 |

## Selbsttest im Spiel

`RimWorldWin64.exe -mpWorldSelfTest -mpWorldSelfTestCoverage=0.3 [-mpSelfTestScreenshot] -mpQuitAfterSelfTest`. Der Gleichlauf-Teil (`LockstepSelfTest`) läuft nach dem Laden der Gastkopie. Der Benutzer hat die Installation in seinen Mod-Ordner erlaubt. Danach wurde der vorherige Ordner (Build `5c3f2b7`) aus der Sicherung zurückgelegt; er ist byte-gleich.

**Treiber gegen einen Sequencer im selben Prozess und einen Schein-Gast** (alle Läufe PASS, ab dem zweiten Lauf):
- `speedTable`: `TickRateMultiplier` Normal 1, Fast 3, Superfast 6. Das bestätigt 60/180/360 Ticks je Sekunde.
- `startsPaused`; `setterIsRequest` (vanillas Geschwindigkeitsknopf ändert nichts lokal, eine Anfrage geht raus).
- `speedAtItsTick` und `pauseAtItsTick`: Der Befehl lief bei Tick t − 1.
- `runsAtNormal`: 58–60 Ticks je Sekunde.
- `slowestGates`: 120 Ticks vor dem eingefrorenen Gast, der genannt wird. Im ersten Lauf FAIL: Der Gast wurde bei Schritten unter einem Tick nicht genannt. Behoben im Sequencer, mit eigenem Test.
- `resumes`, `pauseHolds`.
- `markerTick`: Host und Gast mit gleichem Tick und `seq`.
- `autosaveSkipped`, `ultrafastRefused`.
- `hashesMatched`; `desyncDetected` (eine falsche Gast-Prüfsumme wird gemeldet).
- `neverPastHorizon`, `noMissedCommands`.

**Determinismus:** fünf Läufe vom selben Spielstand.
- A und A2: mit Zufall je Tick.
- B: wie A, aber 3 Rand-Ziehungen zwischen den Ticks, wie Oberflächen-Code.
- C und D: ohne Zufall je Tick, als Gegenprobe.
- Weichen Läufe ab, folgen zwei aufgezeichnete Läufe (Dinge je Prüfsummen-Tick und Rand-Spur).

| Lauf | Konfiguration | Ergebnis |
|---|---|---|
| 1 | ohne Gegenmaßnahmen | A vs B weicht ab bei 3360 |
| 2–5 | Diagnose: erzwungene Pfad-Jobs, synchrone Pfade, Kartenupdates je Tick, fest 20 Ticks/Frame | weiter Abweichungen (2160–5880), immer zuerst wilde Tiere (Position oder Warte-Job) |
| 6–7 | Rand-Spur | Erste abweichende Ziehung schon in Tick 46/98: `FishShadowComponent.FindFishFleckLocation`/`SpawnFishFleck` (Ursache 1); 0 Ziehungen aus anderen Threads während der Ticks |
| 8–9 | Fisch isoliert | 6 000 Ticks: **PASS** |
| 10 | Matrix: ohne Isolierung / asynchrone Pfade / ohne Kartenupdates / 60 000 Ticks | FAIL (Fisch, wie erwartet) / FAIL (erste Ziehung: Geräusche, Ursache 2) / PASS / FAIL bei 8 820 (Geräusche) |
| 11 | Fisch + Geräusche isoliert: Standard / asynchrone Pfade / 60 000 | **PASS** / **PASS** / FAIL bei 26 040; Spur: zusätzliche `InspirationHandler.CheckStartRandomInspiration` in Tick 17 754 (Ursache 3) |
| 12 | + feste Tickrate: Standard / 60 000 | **PASS** (Raten vanilla 3..15, Minimum 1; 2,4 Mio. Abfragen angepasst) / A weicht von B und A2 bei 20 460 ab; die aufgezeichneten Läufe waren über alle 60 000 Ticks gleich |
| 13 | Endstand (Build wie im Commit) | **PASS**, alle Stufen; 54 von 54 Figuren bewegten sich, 722/722 Pfade; Gegenprobe weicht bei 120 ab; 0 Fehler im Log |

Logs liegen im Sitzungs-Scratchpad (`logs/run*-Player.log`), nicht im Repo.

## Ergebnis

- Der Gleichlauf-Kern arbeitet: Server-Tests, echter Server mit zwei Clients und Treiber im Spiel.
- Determinismus im selben Prozess: 5 × 6 000 Ticks gleich, auch mit Oberflächen-Zufall zwischen den Ticks. Ohne die Isolierung FAIL, mit ihr PASS.
- Über einen ganzen Spieltag bleibt ein seltener Rest: der erste Lauf nach dem Treiber-Teil ab Tick 20 460. Die Ursache ist offen. Im Zwei-Spieler-Lauf zeigt das Feld die Prüfsummen-Lage live.

## Nicht geprüft

- **Zwei echte Spieler** (`MP_ZWEI_CLIENTS_GETESTET`: nein). Anleitung: `docs/reviews/2026-10-16-sync-lockstep.md`.
- Determinismus zwischen zwei Prozessen oder PCs (TOOL-001, voller DET-001).
- Spieleraktionen (SYNC-002): Sie werden nicht geteilt und führen zu einer gemeldeten Abweichung.
- Lagen mit Kämpfen, Feuer, Wetterereignissen und vielen Effekten. Dort können weitere sicht- oder hörabhängige Ziehungen stecken.
- Rechenzeit großer Kolonien mit fester Tickrate und synchronen Pfaden.

## Einordnung nach AGENTS.md Regel 25

`BUILD_ERFOLGREICH` (0 Warnungen); Harness-Tests bestanden; `IN_GAME_GETESTET` (Selbsttest, ein Prozess, ohne Netz); `MP_ZWEI_CLIENTS_GETESTET`: nein.
