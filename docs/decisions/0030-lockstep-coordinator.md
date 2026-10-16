# ADR 0030 – Lockstep-Koordinator (SYNC-001 und DESYNC-001, Koordinator-Seite)

- **Status:** angenommen für `feat/lockstep-session`, auf `feat/mod-check-setup-ui` @ `08e7f9b`. Designnotiz nach `AGENTS.md` Regel 4. Plan §7.1, §7.9, §7.12; Grundlage ADR 0013 (Listen-Host).
- **Aufteilung (Benutzer, 2026-10-16):**
  - Hier entsteht die Koordinator-Seite, die im eingebetteten `FoundationServer` des Hosts läuft. Dazu kommt die spielunabhängige Client-Logik, die der Tick-Treiber nutzt.
  - Die Spielseite (Tick-Treiber an `TickManager`, Befehlserfassung, Determinismus-Patches, Zustands-Hash) baut der zweite Beitragende.
  - Nummern bewusst abseits seiner fortlaufenden Folge: Nachrichtencodes 64–70, ADR 0030.
- **Ziel:** Alle Spiele führen dieselben Spielerbefehle an derselben Stelle in derselben Reihenfolge aus und rechnen keinen Tick, bevor sie alle Befehle bis dahin kennen. Abweichungen werden erkannt und stoppen die Zeit, statt unbemerkt weiterzulaufen.
- **Komponenten:**
  - `Multiplayer.Foundation.Session/LockstepSession.cs`: reine Zustandsmaschine (`netstandard2.0`, ohne Verse), Uhr als Parameter, liefert die zu sendenden Nachrichten.
  - `Multiplayer.Foundation.Network/LockstepClient.cs`: Gegenstück für den Tick-Treiber im Spiel.
  - `Protocol/LockstepPayloads.cs`.
  - Anbindung im `FoundationServer` mit geordneter Warteschlange je Spieler.
- **Tickzählung:** Sitzungsticks zählen ab der Landung (Phase `Playing`): Sitzungstick = `TicksGame − TicksGame bei der Landung`. Alle haben denselben Spielstand geladen, also denselben Startwert; der Host muss nichts mitteilen.

## Ausführungsregel (Kern der Synchronität)

- Der Koordinator führt einen **Horizont h**: Kein Spiel darf Tick h+1 rechnen, bevor es `TickHorizon ≥ h+1` erhalten hat.
- Ein Befehl, der eintrifft, während der Horizont h ist, bekommt die **Grenze h**. Er wird *nach* Tick h und *vor* Tick h+1 ausgeführt; innerhalb einer Grenze nach der fortlaufenden Sequenznummer.
- Der Koordinator schickt jedem Spieler alle Befehle mit Grenze h, **bevor** er ihm einen Horizont größer als h schickt. Wer Tick h fertig hat und `TickHorizon ≥ h+1` sieht, kennt also alle Befehle für h.
- Ein Spiel, das bei h steht, führt eintreffende Befehle für h **sofort** aus, auch pausiert. Ein Spiel, das zurückliegt, führt sie aus, sobald es h erreicht. In beiden Fällen ist die Stelle dieselbe: nach Tick h, in Sequenzreihenfolge.
- Gegenüber Plan §7.1 („Tick h+1“) hat das zwei Vorteile: Befehle im Pausenmodus wirken sofort, wie in RimWorld gewohnt, und die Eingabeverzögerung ist geringer.
- **Reihenfolge je Empfänger ist Pflicht.** Bisher sendet der Server aus mehreren gleichzeitigen Aufgaben; ein Horizont aus dem Takt-Thread könnte einen Befehl aus dem Befehls-Thread überholen. Alle Lockstep-Nachrichten laufen deshalb durch **eine geordnete Warteschlange je Spieler**. Gefüllt wird sie unter der Sperre der Sitzung, geleert von genau einem Sender je Spieler.

## Takt und Flusssteuerung

- **Zieltick:** Basis plus vergangene Zeit mal Tickrate der Geschwindigkeit. Standard 1× = 60, 2× = 180, 3× = 360 Ticks/s laut Plan; Status **WAHRSCHEINLICH** (Plan [U]), in `LockstepOptions` einstellbar. Die Tickrate bestimmt nur das Tempo, nicht die Synchronität.
- **Grenze:** Horizont ≤ kleinster gemeldeter `ClientTick` + Vorsprung. Der Vorsprung beträgt 2 s bei aktueller Rate, mindestens 60 Ticks. Damit bestimmt **der langsamste Spieler das Tempo**.
- Bremst die Grenze, setzt der Koordinator die Uhrbasis auf die Grenze zurück. So entsteht keine aufgestaute Zeit, die später als Sprung nachgeholt wird.
- Jeder `TickHorizon` nennt den langsamsten Spieler und seinen Tick, damit die UI „Warte auf …“ zeigen kann.
- Spiele melden `ClientTick` etwa zehnmal pro Sekunde. Eine Meldung über dem Horizont ist ein Protokollfehler: Das Spiel hätte gar nicht so weit rechnen dürfen.

## Pause und Geschwindigkeit

- Pause und Geschwindigkeit sind **geplante Befehle** (Art `Pause`/`Speed`) mit derselben Grenze-Regel. Alle Spiele wechseln an derselben Stelle. Der Koordinator ändert seine Uhr ab dem aktuellen Horizont.
- Pause stoppt den Horizont.
- Standardregel (Plan §7.1): Pausieren und Fortsetzen darf jeder, die Geschwindigkeit nur der Host. Andere Regeln kommen mit `SYNC-003`.
- Die Sitzung beginnt **pausiert** bei Tick 0.

## Desync-Erkennung (DESYNC-001, Koordinator-Seite)

- Spiele melden alle 60 Ticks (`HashInterval`) `StateHash(tick, u64)`.
- Haben alle aktuellen Teilnehmer für einen Tick gemeldet, vergleicht der Koordinator mit dem Wert des **Hosts**; er ist der Snapshot-Lieferant.
- Bei Abweichung sendet er `DesyncDetected(tick, betroffene Spieler)` an alle und plant sofort eine **Pause** (Absender: Koordinator).
- Absolut synchron heißt: nie still weiterlaufen. Reparatur ist `DESYNC-003`.
- Späte Meldungen für bereits verglichene Ticks werden ignoriert. Offene Vergleiche sind durch den Vorsprung begrenzt.

## Schutz (Plan §7.12)

- Der Absender eines Befehls wird **vom Koordinator gestempelt**: Session-ID und `PlayerKey`, nie vom Client übernommen.
- Befehl höchstens 32 KiB.
- Je Spieler höchstens 30 Befehle/s, Spitze 60 (Token-Bucket).
- Verstöße werden abgelehnt, nie ausgeführt.

## Teilnehmer

- Teilnehmer sind die Lobbymitglieder beim Eintritt in `Playing`.
- Wer geht, fällt aus Flusssteuerung und Hash-Vergleich heraus; offene Vergleiche werden neu bewertet.
- Geht der Host, endet die Sitzung. Im Listen-Host-Betrieb endet ohnehin der Koordinator.
- Beitritt mitten im Spiel ist `JOIN-001`.

## Protokoll

| Code | Typ | Richtung | Nutzdaten |
|---|---|---|---|
| 64 | Command | C→S | Befehlsdaten (für den Koordinator undurchsichtig, ≤ 32 KiB) |
| 65 | ScheduledCommand | S→C | Grenze `u32`, Sequenz `u64`, Absender-Session, Absender-`PlayerKey`, Art `u8` (0 Spiel, 1 Pause, 2 Geschwindigkeit), Daten |
| 66 | TickHorizon | S→C | Horizont `u32`, Geschwindigkeit `u8`, pausiert `bool`, langsamster Spieler, dessen Tick `u32` |
| 67 | ClientTick | C→S | zuletzt fertig gerechneter Sitzungstick `u32` |
| 68 | StateHash | C→S | Tick `u32`, Hash `u64` |
| 69 | DesyncDetected | S→C | Tick `u32`, Anzahl, Session-IDs |
| 70 | SessionControl | C→S | Art `u8` (1 Pause, 2 Geschwindigkeit), Wert `u8` |

## Spielseitige Nutzung (`LockstepClient`)

- Der Tick-Treiber fragt `MayRunTick(fertigerTick)`; das ist nur wahr, wenn `fertigerTick < Horizont`.
- Vor jedem Tick und in jedem Frame holt er `TakeCommandsAt(fertigerTick)`. Er bekommt die Spielbefehle für diese Grenze in Sequenzreihenfolge; Pause und Geschwindigkeit verarbeitet `LockstepClient` selbst.
- `ProgressMessage` und `HashMessage` erzeugen die Meldungen an den Koordinator.
- Der Client erkennt Protokollfehler selbst: eine Grenze, die er schon hinter sich hat, oder eine Sequenzlücke.

## Abgrenzung, Tests, Rollback

- **Nicht hier:**
  - Tick-Treiber, Befehlserfassung und Serialisierung (`SYNC-002`)
  - Zustands-Hash im Spiel
  - Rand-Neusaat (Plan §7.4)
  - Reparatur (`DESYNC-003`)
  - Speichern und Beitritt
- **Persistenz:** keine. Die Sitzung lebt im Speicher des Hosts.
- **Tests:**
  - Payload-Grenzen.
  - Zustandsmaschine mit virtueller Uhr: Grenzen, Reihenfolge, Pause, Geschwindigkeitsrecht, Flusssteuerung, Uhr-Rücksetzung, Ratenbegrenzung, Hash-Vergleich, Abgang.
  - Simulation mehrerer Spiele mit unterschiedlicher Verzögerung: Alle müssen exakt dieselbe Ausführungsfolge (Tick, Sequenz) protokollieren.
  - Ende zu Ende über `ListenHost` mit Steam-ähnlichem Gast.
  - Im Spiel: `NICHT_GETESTET`.
- **Rollback:** Branch verwerfen; ohne Phase `Playing` bleibt alles wie bisher.
