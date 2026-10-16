# ADR 0032 – Speichern und Beitritt im laufenden Spiel (Koordinator-Seite)

- **Status:** angenommen für `claude/save-join`. Designnotiz nach `AGENTS.md` Regel 4. Plan §7.8; Aufgaben `SAVE-001`, `JOIN-001` (Bahn Claude, `docs/ai/ASSIGNMENTS.md`). Baut auf ADR 0030 (Lockstep), ADR 0014 (Chunk-Kanal), ADR 0016 (PlayerKey) und ADR 0018 (Mod-Prüfung) auf.
- **Ziel:**
  - Alle Spiele speichern an **derselben Stelle**: automatisch je Spieltag oder auf Wunsch des Hosts.
  - Ein Spieler kann **mitten im Spiel** beitreten oder nach einem Verbindungsabbruch zurückkehren. Er lädt den Stand des Hosts, holt auf und läuft danach im Gleichschritt mit, ohne dass die anderen dafür anhalten müssen.

## Speichern (SAVE-001)

- Neue Art `ScheduledKind.Save` (3) für geplante Befehle. Wie jeder geplante Befehl gilt sie nach Tick h in Sequenzreihenfolge, also in jedem Spiel an derselben Stelle. Was dort gespeichert wird, ist überall gleich.
- **Automatisch:** Sobald der Horizont das nächste Vielfache von `AutosaveIntervalTicks` erreicht, plant der Koordinator einen Speicherbefehl. Standard 60 000 Ticks = ein Spieltag (`RimWorld.GenDate.TicksPerDay`, per Reflection gegen die lokale 1.6-`Assembly-CSharp.dll` geprüft).
- **Auf Wunsch:** `SessionControl` mit Art `Save`, nur vom Host (Code 3 sonst).
- **Daten des Speicherbefehls:**
  - leer: Koordiniertes Speichern. Der Spielstand des Hosts ist maßgeblich, die anderen behalten eine lokale Sicherung (Plan §7.8).
  - 16 Byte: Session-ID eines Beitretenden. Dann ist der Spielstand des Hosts der **Beitritts-Snapshot**; der Host schickt ihn über den Chunk-Kanal (Art `Snapshot`) an diesen Spieler.

## Beitritt und Reconnect (JOIN-001)

1. **Lobby:** Während `Playing` ist der Beitritt erlaubt, wenn Platz ist und – bei vorhandener Host-Referenz – die Mods passen (`Match`). Sonst Ablehnung (Code 5 bzw. 8). In allen anderen Einrichtungsphasen bleibt die Lobby gesperrt.
2. **Snapshot planen:** Der Koordinator plant `Save` mit der Session-ID des Neuen; dieser Befehl hat Grenze T und Sequenz S. **Unter derselben Sperre** nimmt er den Neuen in den Verteiler auf. Der Neue erhält also genau die Nachrichten ab S+1 und nie den Befehl S selbst.
3. **Start-Nachricht** `JoinSnapshot` (Code 71) an den Neuen: Grenze T, Sequenz S, Host, aktueller Horizont, Geschwindigkeit, Pause. Danach folgt wie für alle der aktuelle `TickHorizon`.
4. **Laden und Aufholen:** Der Neue lädt den Snapshot, erzeugt seinen `LockstepClient` mit `LockstepClient.ForJoin(T, S, …)` und führt die gepufferten Befehle in derselben Reihenfolge aus wie alle anderen. Er rechnet mit voller Geschwindigkeit bis zum Horizont.
5. **Live:** Der Neue zählt **nicht** zur Flusssteuerung, solange er aufholt; die anderen spielen weiter (Plan §7.8). Erreicht sein gemeldeter Tick den langsamsten aktiven Spieler, wird er aktiv: Ab dann bestimmt er das Tempo mit, und seine Hashes werden verglichen. Alle erhalten `PlayerSynced` (Code 72).
6. **Reconnect** ist derselbe Ablauf mit neuer Session-ID; Besitz und Rechte hängen am `PlayerKey` (Spielseite, `OWN-001`).

- **Hash-Vergleich:** Er wartet nur auf aktive Teilnehmer. Meldet ein Aufholender Hashes für noch offene Ticks, werden sie mitverglichen; ein Fehler beim Aufholen fällt damit früh auf.
- **Abbruch:** Trennt sich der Neue vor dem Live-Schalten, wird er entfernt. Nach Plan §7.8 kann der Host wählen, ob beim Beitritt pausiert wird; diese Option folgt mit der Spielseite. Standard: Die anderen spielen weiter.

## Protokoll

| Code | Typ | Richtung | Nutzdaten |
|---|---|---|---|
| 71 | JoinSnapshot | S→C | Grenze `u32`, Sequenz `u64`, Host-Session, Horizont `u32`, Geschwindigkeit `u8`, pausiert `bool` |
| 72 | PlayerSynced | S→C | Session-ID des nun aktiven Spielers |

- Neue Art `ScheduledKind.Save` = 3. Neue Steuerart `Save` für `SessionControl`, Wert 0, nur der Host.
- `LockstepClient.TakeCommandsAt` liefert neben Spielbefehlen auch Speicherbefehle; die Spielseite unterscheidet nach `Kind`.

## Abgrenzung, Tests, Rollback

- **Spielseite (Bahn Codex):**
  - Speichern beim Speicherbefehl, Senden des Snapshots über den Chunk-Kanal.
  - Laden beim Beitretenden und Aufholen mit `ForJoin`.
  - Besitz über `PlayerKey`, Option „pausieren beim Beitritt“, Anzeige „X holt auf“.
- **Persistenz:** Der Koordinator speichert nichts; er plant nur.
- **Tests:**
  - Payload-Grenzen.
  - Koordinator-Regeln: Autosave-Intervall, Host-Speichern, Beitritt, Aktivierung, Flusssteuerung ohne Aufholende, Abbruch beim Beitritt.
  - Simulation: Ein Spieler tritt mitten im Lauf bei, lädt den Stand anderer Spiele an der Snapshot-Stelle, holt auf und muss ab dort **dieselbe Ausführungsfolge** haben.
  - Ende zu Ende über `ListenHost` mit Steam-ähnlichem Spät-Beitretenden.
- **Rollback:** Branch verwerfen; ohne Beitritt in `Playing` bleibt alles wie bisher.
