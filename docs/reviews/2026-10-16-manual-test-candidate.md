# Review: gemeinsamer manueller Testkandidat

- **Aufgabe:** PKG-002; enthält VER-001 (X1), SYNC-002-Teilstand (X4) und MODE-001 (X5).
- **Quellbranch:** `codex/manual-test-candidate`, entstanden aus `codex/command-pipeline` mit Cherry-Picks von `codex/ver-tickpath` und `codex/mode-ui`. Codebasis `f04cb14`.
- **Zielbranch:** `main` erst nach ausdrücklicher menschlicher Review-Freigabe und nach Entscheidung über den WIP-Umfang von X4.
- **Änderung:** Ein identisches, lokal installierbares RimWorld-1.6-ZIP bündelt die drei Arbeitsstände. Das Archiv wird nicht als funktionierender Gameplay-Multiplayer bezeichnet.
- **Betroffene Komponenten:** RimWorld-Adapter, Mod-Paket, Projekt- und Prüfungsdokumente; Server/Protokoll unverändert gegenüber `main`.
- **Nachweise:** Release-Build, Paketprüfung, SHA-256, Session-/Client-Konsolenprogramme und Validator: `docs/verification/2026-10-16-manual-test-candidate.md`.
- **Risiken:** Kein Spieltest; Harmony-Hooks, RSA in RimWorld-Mono, Bewegung/Pfadsuche und Save/Load offen. X4 ist nicht vollständig. X1 nennt parallele Pfadsuche als konkretes Desync-Risiko.
- **Manuell zu prüfen:** Gleicher ZIP-Hash und Mod-Reihenfolge bei Fabio/Sven, Steam/IP-Verbindung, Modus/Passwort, Tick-/Hash-Gleichheit und erster abweichender Zustand. Ablauf in `docs/verification/TEST_PROMPTS_FABIO_SVEN.md`.
- **Status:** Review vorbereitet, keine Freigabe und kein Merge/Push.
