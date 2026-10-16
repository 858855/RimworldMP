# Übergabe Claude (Koordinator und Protokoll)

- **Bahn:** siehe `docs/ai/ASSIGNMENTS.md`. Branch-Präfix `claude/`, ADR 0032–0039, Nachrichtencodes 71–79. Worktree `/mnt/Arbeit/Git Projekte/RimworldMP-claude`.
- **Stand auf `main` (`85af3a9`):**
  - Chunk-Kanal (ADR 0014), PlayerKey (ADR 0016), Setup-Regeln (ADR 0017).
  - Lockstep-Koordinator (ADR 0030): `LockstepSession`, `LockstepClient`, `LockstepHost`, `OrderedOutbox`; Codes 64–70.
  - Spielmodus (ADR 0031, Koordinator-Seite): `LobbyMode` in `CreateLobby`, Lobbyzustand und -liste; Fraktionsstart abgelehnt bis `FACTION-001`.
- **Nächste Aufgabe:** C1 `SAVE-001`/`JOIN-001` (Koordinator) auf `claude/save-join`.
- **Wünsche an Codex:**
  - Tick-Treiber nach ADR 0030:
    - Tick c+1 nur, wenn `LockstepClient.MayRunTick(c)` wahr ist; vorher und in jedem Frame während der Pause `TakeCommandsAt(c)`.
    - Sitzungstick = `TicksGame − TicksGame` bei der Landung.
    - Etwa 10/s `ProgressMessage`, alle 60 Ticks `HashMessage`.
    - Pause und Geschwindigkeit nur als `ControlMessage`.
  - Paketlisten in `scripts/build-mod.sh` und `scripts/package-mod.py` müssen jede neue Bibliothek enthalten, sonst stürzt der Host beim Laden ab.
