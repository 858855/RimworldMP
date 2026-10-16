# Review-Vorbereitung: Save/Join-Spielseite und X3

- **Aufgaben:** `SAVE-003`, `JOIN-002`, `DESYNC-001` (Spielseite).
- **Branch/Ziel:** `codex/state-hash` → später `main`; abhängig von `claude/save-join` @ `2e6b77d` und X2. Kein Merge ohne neue Freigabe. Commit mit `git log -1 --oneline` ermitteln.
- **Änderung:** Gemeinsame Speicherbefehle führen nun zu echten RimWorld-Spielständen. Der Host kann einen gespeicherten Stand gezielt als Snapshot versenden; neue oder reparierte Spieler laden ihn und holen nach der gemeinsamen Tickbasis auf. Der Vergleichshash umfasst zusätzliche Simulationsfelder, und `Verse.Rand` wird im Tickpfad isoliert.
- **Dateien:** `SessionTickDriver`, `SessionPausePatch`, `FoundationMod`, `SessionRandom`, `SessionStateHash`; ADR 0041/0042, Clientdokumentation und eigene Übergabe.
- **Prüfstand:** Lokaler RimWorld-1.6-Mod-Build ohne Warnungen; spielunabhängige Session-/Hosting-Tests bestanden. Details in `docs/verification/2026-10-16-codex-save-join-hash.md`.
- **Risiken/manuell prüfen:** Zwei echte RimWorld-Clients, Save/Join/Resync, Performance des 60-Tick-Hashs und Harmony-Reihenfolge. `SYNC-002` und `DET-001` sind offen. Die zwischenzeitlich erschienene C2-Koordinator-API erfordert noch `SnapshotLoaded` vor Integration.
- **Review-Status:** in Arbeit; keine menschliche Freigabe und kein Merge.
