# ADR 0034 – Abstimmung im Modus Coop-Demokratie (VOTE-001, Koordinator-Seite)

- **Status:** angenommen für `claude/vote`, auf `claude/resync` (ADR 0033). Designnotiz nach `AGENTS.md` Regel 4. Benutzerregeln aus ADR 0031; Aufgabe `VOTE-001` (Bahn Claude, C3).
- **Benutzerregeln (ADR 0031):**
  - Im Modus Coop-Demokratie wird abgestimmt, wenn ein Ereignis eine Entscheidung verlangt, z. B. ein Wanderer will beitreten oder eine Quest annehmen.
  - Keine Frist. Die Mehrheit gewinnt, bei Gleichstand entscheidet der Host.
  - Der Host kann vorzeitig beenden; wer nicht abgestimmt hat, gilt als enthalten.
  - Getrennte Spieler zählen nicht. Während der Abstimmung ist das Spiel für alle pausiert.

## Ablauf

1. **Öffnen:** Das Ereignis entsteht in allen Spielen gleich (Lockstep). Nur das Spiel des **Hosts** öffnet die Abstimmung mit `VoteOpen`. Mitgeschickt werden ein undurchsichtiger Ereignisschlüssel, mit dem jedes Spiel das Ereignis wiederfindet (bis 256 Byte), und die Anzahl der Optionen (2–10). Der Koordinator vergibt eine Abstimmungsnummer.
   - Nur im Modus Coop-Demokratie; sonst Code 6. Im Modus Coop-Host entscheidet der Host über einen normalen Spielbefehl (Rechte: `SYNC-005`).
2. **Pause:** Läuft die Zeit, pausiert der Koordinator an einer gemeinsamen Stelle und merkt sich, dass **er** pausiert hat.
3. **Abstimmen:** Jeder aktive Spieler gibt mit `VoteCast` eine Option ab und darf sie bis zum Ende ändern. Nach jeder Änderung erhalten alle `VoteState` mit den Stimmen je Spieler.
4. **Ende:**
   - automatisch, wenn **alle aktiven** Spieler abgestimmt haben (Beitretende und Neu-Ladende zählen nicht, getrennte fallen heraus), oder
   - vorzeitig durch den **Host** mit `VoteClose`; wer nicht abgestimmt hat, gilt als enthalten.
5. **Ergebnis:**
   - Die Option mit den meisten Stimmen gewinnt.
   - Bei Gleichstand gewinnt die Option des Hosts, wenn sie unter den gleichauf liegenden ist.
   - Hat der Host bei Gleichstand nicht für eine davon gestimmt, wird `VoteClose` abgelehnt („Bitte erst abstimmen“, Code 6). Das gilt auch, wenn noch niemand abgestimmt hat. Andernfalls gäbe es kein Ergebnis.
   - Das Ergebnis geht als geplanter Befehl der neuen Art `ScheduledKind.VoteResult` (4) an alle; Daten: Abstimmungsnummer, gewählte Option, Ereignisschlüssel. **Alle Spiele wenden die Entscheidung an derselben Stelle an.** Danach erhalten alle `VoteState` (geschlossen, mit Ergebnis).
6. **Fortsetzen:** Sind alle Abstimmungen beendet, kein Resync offen und hatte der Koordinator selbst pausiert, setzt er an einer gemeinsamen Stelle fort.

## Pause-Regeln (gemeinsam mit ADR 0033)

- Der Koordinator führt **eine** Merkmarke „vom System pausiert“ für Desync und Abstimmung. Fortgesetzt wird erst, wenn **beide** Gründe erledigt sind.
- Pausiert ein Spieler selbst, übernimmt er die Pause; der Koordinator setzt dann nicht mehr von sich aus fort.
- **Fortsetzen während einer offenen Abstimmung wird abgelehnt** (Code 6). Sonst ließe sich die gemeinsame Pause umgehen. Pausieren bleibt immer erlaubt.
- Mehrere Abstimmungen dürfen gleichzeitig offen sein, etwa bei zwei Ereignissen im selben Tick; jede endet für sich.

## Protokoll (letzte Codes der Claude-Bahn)

| Code | Typ | Richtung | Nutzdaten |
|---|---|---|---|
| 76 | VoteOpen | C→S, nur Host | Optionen `u8` (2–10), Ereignisschlüssel (`u32`-Länge + ≤ 256 Byte) |
| 77 | VoteCast | C→S | Abstimmungsnummer `u32`, Option `u8` |
| 78 | VoteClose | C→S, nur Host | Abstimmungsnummer `u32` |
| 79 | VoteState | S→C | Nummer, Ereignisschlüssel, Optionen, offen `bool`, Ergebnis `u8` (255 = keins), Stimmen (Anzahl, je Session + Option) |

- Neue geplante Art: `ScheduledKind.VoteResult` = 4, Daten: Nummer `u32`, Option `u8`, Ereignisschlüssel.
- Damit ist der Nummernbereich 71–79 der Claude-Bahn ausgeschöpft. Weitere Codes stimmt der Benutzer neu ab.

## Abgrenzung, Tests, Rollback

- **Spielseite (Codex):**
  - Ereignisse mit Wahl erkennen; der Host sendet `VoteOpen`.
  - Abstimmungsfenster mit Stimmen und Host-Knopf „Beenden“.
  - `VoteResult` anwenden.
  - Im Modus Coop-Host die Wahl nur dem Host erlauben.
  - Der Plan setzt `SYNC-004`/`SYNC-007` voraus.
- **Tests:**
  - Payloads.
  - Regeln: nur Demokratie, nur Host öffnet und beendet, Stimme ändern, automatisches Ende, Mehrheit, Gleichstand durch Host, Ablehnung bei Gleichstand ohne Host-Stimme, Enthaltung, getrennter Spieler, gleichzeitige Abstimmungen.
  - Pause-Zusammenspiel mit Spieler-Pause und Resync.
  - Simulation: Alle Spiele wenden das Ergebnis an derselben Stelle an.
  - Ende zu Ende über `ListenHost`.
- **Rollback:** Branch verwerfen; ohne ihn entscheidet in jedem Coop der Host.
