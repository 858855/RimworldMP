# ADR 0033 – Desync-Reparatur per Resync (DESYNC-003, Koordinator-Seite)

- **Status:** angenommen für `claude/resync`, auf `claude/save-join` (ADR 0032). Designnotiz nach `AGENTS.md` Regel 4. Plan §7.9 „Recovery“; Aufgabe `DESYNC-003` (Bahn Claude, C2).
- **Ziel:** Ein erkannter Desync beendet die Partie nicht. Die abweichenden Spiele laden den Stand des Hosts neu, holen auf, und die Zeit läuft automatisch weiter. Das Ganze ohne Neustart (Plan §7.9).

## Ablauf

1. **Erkennen** (ADR 0030): Der Hash eines Spiels weicht vom Hash des Hosts ab. Alle erhalten `DesyncDetected`; hat der Koordinator dabei selbst pausiert, merkt er sich das.
2. **Snapshot:** Unter derselben Sperre plant der Koordinator einen Speicherbefehl (`Save`, ADR 0032). Seine Daten listen die Session-IDs **aller** abweichenden Spiele (n × 16 Byte). Der Host speichert dort und schickt den Stand über den Chunk-Kanal an jedes dieser Spiele.
3. **Neu laden:**
   - Jedes abweichende Spiel erhält direkt danach `JoinSnapshot` (Code 71) mit Grenze und Sequenz des Speicherbefehls.
   - Es verwirft seinen Stand und seinen `LockstepClient`, lädt den Stand des Hosts und startet mit `LockstepClient.ForJoin`. Danach bestätigt es mit `SnapshotLoaded` (Code 75).
   - Bis dahin ist es **inaktiv**: Es zählt nicht für Tempo und Hash-Vergleich.
   - Hash-Meldungen aus seinem alten Stand (Tick ≤ Snapshot-Grenze) werden verworfen, auch solche, die noch unterwegs sind.
4. **Wieder dabei:** Nach `SnapshotLoaded` und sobald sein Tick den langsamsten aktiven Spieler erreicht, wird es aktiv; alle erhalten `PlayerSynced` (72).
5. **Fortsetzen:** Sind alle Reparierten wieder aktiv und hatte der Koordinator wegen des Desyncs pausiert, plant er das Fortsetzen (`Pause` = 0, Absender Koordinator). Alle erhalten `DesyncResolved` (Code 73) mit dem Tick des Desyncs, damit die UI „Desync behoben (Tick X)“ zeigen kann. Hatten Spieler schon vorher pausiert, bleibt es pausiert.
6. **Aufgeben:** Weicht ein Spiel **dreimal in Folge** ab, ohne dass dazwischen ein Vergleich gepasst hat, repariert der Koordinator es nicht erneut. Alle erhalten `ResyncGaveUp` (Code 74) mit dem Spieler, und die Zeit bleibt pausiert; der Host entscheidet, etwa durch Entfernen des Spielers. Ein passender Vergleich setzt den Zähler zurück.

## Entscheidungen und Abweichungen vom Plan

- **Referenz ist immer der Host** (Plan P-2: der Host ist Snapshot-Lieferant). Plan §7.9 sagt „alle neu laden, wenn mehr als die Hälfte abweicht“. Mit dem Host als einziger Quelle lädt ohnehin jedes abweichende Spiel neu; passende Spiele neu zu laden, brächte nichts. Weichen viele vom Host ab, könnte der Host selbst falsch liegen. Eine Mehrheitsentscheidung mit anderem Lieferanten ist **offen** und nicht Teil dieser Aufgabe.
- **`SnapshotLoaded` ist Pflicht, auch für Beitretende aus ADR 0032.** Ohne die Bestätigung könnte eine noch unterwegs befindliche Fortschrittsmeldung aus dem alten Stand ein Spiel aktiv schalten, bevor es neu geladen hat. ADR 0032 war bei Einführung dieser Regel noch nicht gemergt; die Spielseite des Beitritts gab es noch nicht.

## Protokoll

| Code | Typ | Richtung | Nutzdaten |
|---|---|---|---|
| 73 | DesyncResolved | S→C | Tick des Desyncs `u32` |
| 74 | ResyncGaveUp | S→C | Anzahl `u8`, Session-IDs |
| 75 | SnapshotLoaded | C→S | leer; nach dem Laden eines Snapshots (Beitritt oder Resync) |

- Daten des Speicherbefehls (ADR 0032): leer = koordiniertes Speichern; n × 16 Byte = Snapshot für diese Spieler.

## Abgrenzung, Tests, Rollback

- **Spielseite (Codex):**
  - Auf `JoinSnapshot` als bestehender Teilnehmer: Stand verwerfen, Snapshot laden, `ForJoin`, `SnapshotLoaded` senden.
  - Speicherbefehl mit eigener ID nicht als Fehler behandeln.
  - Anzeigen für `DesyncResolved` und `ResyncGaveUp`.
  - Diagnosepaket (`DESYNC-002`).
- **Tests:**
  - Koordinator-Regeln: Snapshot für Abweichende, Inaktivität, verworfene alte Hashes, Aktivierung erst nach `SnapshotLoaded`, Fortsetzen nur bei eigener Pause, Aufgeben nach drei.
  - Simulation mit **echter** Zustandsabweichung eines Spiels: Erkennen, Neuladen, automatisches Weiterlaufen, danach keine Abweichung mehr und gleiche Ausführungsfolge.
  - Ende zu Ende über `ListenHost`.
- **Rollback:** Branch verwerfen; ohne ihn bleibt ein Desync ein endgültiger Halt.
