# Übergabe Claude (Koordinator und Protokoll)

- **Bahn:** siehe `docs/ai/ASSIGNMENTS.md`. Branch-Präfix `claude/`, ADR 0032–0039, Nachrichtencodes 71–79. Worktree `/mnt/Arbeit/Git Projekte/RimworldMP-claude`.
- **Stand auf `main` (`85af3a9`):**
  - Chunk-Kanal (ADR 0014), PlayerKey (ADR 0016), Setup-Regeln (ADR 0017).
  - Lockstep-Koordinator (ADR 0030): `LockstepSession`, `LockstepClient`, `LockstepHost`, `OrderedOutbox`; Codes 64–70.
  - Spielmodus (ADR 0031, Koordinator-Seite): `LobbyMode` in `CreateLobby`, Lobbyzustand und -liste; Fraktionsstart abgelehnt bis `FACTION-001`.
- **C1 erledigt (Koordinator-Seite), Branch `claude/save-join`, ADR 0032:** Speichern als geplanter Befehl (`ScheduledKind.Save`), Autosave je 60 000 Ticks, Host-Speichern; Beitritt während `Playing` mit passenden Mods; Snapshot-Befehl, `JoinSnapshot` (71), Aufholen ohne Flusssteuerung, `PlayerSynced` (72). Simulation: Der Neue führt danach exakt dieselbe Folge aus.
- **Nächste Aufgabe:** C2 `DESYNC-003` (Koordinator) auf `claude/resync`.
- **Wünsche an Codex:**
  - Tick-Treiber nach ADR 0030:
    - Tick c+1 nur, wenn `LockstepClient.MayRunTick(c)` wahr ist; vorher und in jedem Frame während der Pause `TakeCommandsAt(c)`.
    - Sitzungstick = `TicksGame − TicksGame` bei der Landung.
    - Etwa 10/s `ProgressMessage`, alle 60 Ticks `HashMessage`.
    - Pause und Geschwindigkeit nur als `ControlMessage`.
  - Paketlisten in `scripts/build-mod.sh` und `scripts/package-mod.py` müssen jede neue Bibliothek enthalten, sonst stürzt der Host beim Laden ab.
  - **Speichern und Beitritt (ADR 0032)**, alles Spielseite:
    - `TakeCommandsAt` liefert jetzt auch `ScheduledKind.Save`.
    - Leere Daten: koordiniert speichern; maßgeblich ist der Stand des Hosts, die anderen behalten eine lokale Sicherung.
    - 16 Byte Daten (Session-ID eines Neuen): Der **Host** speichert genau dort und schickt den Spielstand über den Chunk-Kanal (`TransferKind.Snapshot`) an diesen Spieler.
    - Der **Neue** wartet auf `JoinSnapshot` (71) und den Spielstand, lädt ihn und erzeugt `LockstepClient.ForJoin(snapshot)`. Er rechnet mit voller Geschwindigkeit bis zum Horizont und meldet dabei `ProgressMessage`.
    - **Wichtig:** Der Snapshot entsteht mitten in den Befehlen von Tick T. Für Tick T selbst darf der Neue **keinen** Zustands-Hash melden; erst ab dem nächsten Hash-Tick.
    - Besitz und Rechte über den `PlayerKey` wiederherstellen (Reconnect).
    - Anzeige „holt auf“ nach `PlayerSynced` (72) beenden.
    - Option „pausieren beim Beitritt“ (Plan §7.8) in der UI.

