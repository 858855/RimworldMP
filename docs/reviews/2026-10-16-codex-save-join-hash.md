# Review-Vorbereitung: Save/Join-Spielseite und X3

- **Aufgaben:** `SAVE-003`, `JOIN-002`, `DESYNC-001` (Spielseite).
- **Branch/Ziel:** `codex/resync-game` → später `main`; auf `origin/main` @ `8f7c32f` rebasiert. Kein Merge ohne neue Freigabe. Aktuelle Commits mit `git log origin/main..codex/resync-game --oneline` prüfen.
- **Änderung:** Gemeinsame Speicherbefehle führen nun zu echten RimWorld-Spielständen; der Host kann sie auch im Overlay anfordern. Der Treiber hält weitere Befehle während jedes Saves an ihrer Sequenzposition zurück. Er kann einen gespeicherten Stand gezielt als Snapshot versenden; neue oder reparierte Spieler laden ihn, bestätigen das Laden und holen nach der gemeinsamen Tickbasis auf. Das erste Hashfenster nach jedem Einstieg wird ausgelassen. Der Vergleichshash umfasst zusätzliche Simulationsfelder ohne gecachten Health-Wert oder Fraktions-Eigenbeziehung; `Verse.Rand` wird im Tickpfad isoliert.
- **Dateien:** `SessionTickDriver`, `SessionPausePatch`, `FoundationMod`, `SessionRandom`, `SessionStateHash`; ADR 0041/0042, Clientdokumentation und eigene Übergabe.
- **Prüfstand:** Lokaler RimWorld-1.6-Mod-Build ohne Warnungen; spielunabhängige Session-/Hosting-/Client-Tests bestanden. Details in `docs/verification/2026-10-16-codex-save-join-hash.md` und `docs/verification/2026-10-16-codex-rebase-review-fixes.md`.
- **Risiken/manuell prüfen:** Zwei echte RimWorld-Clients, Save/Join/Resync, Performance des 60-Tick-Hashs und Harmony-Reihenfolge. `SYNC-002` und `DET-001` sind offen. `SnapshotLoaded` wird nach dem Laden gesendet; sein Laufzeitverhalten ist offen.
- **Review-Status:** in Arbeit; keine menschliche Freigabe und kein Merge.
