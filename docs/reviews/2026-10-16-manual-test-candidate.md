# Review: gemeinsamer manueller Testkandidat

- **Aufgabe:** PKG-002; enthält VER-001 (X1), SYNC-002-Teilstand (X4) und MODE-001 (X5).
- **Quellbranch:** `codex/manual-test-candidate`, entstanden aus `codex/command-pipeline` mit Cherry-Picks von `codex/ver-tickpath` und `codex/mode-ui`. Codebasis `f04cb14`.
- **Zielbranch:** `main`. Der Benutzer hat am 2026-10-16 mit „push und merge“ die Integration dieses gemeinsamen Vorschaukandidaten ausdrücklich freigegeben. Damit ist auch der dokumentierte WIP-Umfang von X4 im Merge enthalten.
- **Änderung:** Ein identisches, lokal installierbares RimWorld-1.6-ZIP bündelt die drei Arbeitsstände. Das Archiv wird nicht als funktionierender Gameplay-Multiplayer bezeichnet.
- **Betroffene Komponenten:** RimWorld-Adapter, Mod-Paket, Projekt- und Prüfungsdokumente; Server/Protokoll unverändert gegenüber `main`.
- **Nachweise:** Release-Build, Paketprüfung, SHA-256, alle acht .NET-Testprojekte und Validator: `docs/verification/2026-10-16-manual-test-candidate.md`, `docs/verification/2026-10-16-manual-test-candidate-merge.md`.
- **Risiken:** Kein Spieltest; Harmony-Hooks, RSA in RimWorld-Mono, Bewegung/Pfadsuche und Save/Load offen. X4 ist nicht vollständig. X1 nennt parallele Pfadsuche als konkretes Desync-Risiko.
- **Manuell zu prüfen:** Gleicher ZIP-Hash und Mod-Reihenfolge bei Fabio/Sven, Steam/IP-Verbindung, Modus/Passwort, Tick-/Hash-Gleichheit und erster abweichender Zustand. Ablauf in `docs/verification/TEST_PROMPTS_FABIO_SVEN.md`.
- **Status:** Nach ausdrücklicher Benutzerfreigabe auf `codex/manual-test-candidate` gepusht und mit Merge-Commit `237ac93` in `main` integriert. X4 bleibt technisch unvollständig; der Spieltest steht aus.
