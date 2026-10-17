# Sitzungsübergabe

Codex und Claude arbeiten parallel in getrennten Bahnen; siehe `docs/ai/ASSIGNMENTS.md`.

- **Claude** (Koordinator und Protokoll): `docs/ai/handoff/claude.md`
- **Codex** (Spielseite): `docs/ai/handoff/codex.md`

**Integration 2026-10-17 (Sven, Branch `integration/sync-2026-10-17`, Worktree `E:\RimworldMP-integration`):** `codex/continuous-sync` per Fast-Forward, drei Portierungen aus Svens Branches, Tempoanpassung des Koordinators (SYNC-010, Code 104). Branch-Matrix, Konflikte und Löschliste in `docs/reviews/2026-10-17-integration-sync.md`; Prüfnachweis in `docs/verification/2026-10-17-integration-sync.md`. Der Besitzer hat am 2026-10-17 Merge, Push, Archiv-Tags und Löschungen freigegeben; `main` steht seitdem auf dem Integrationsstand (Phase 2 im Review-Bericht).

Maßgeblicher gemeinsamer Stand ist `main` (Commit über `git log -1` prüfen). Die X1/X4/X5-Vorschau wurde mit `237ac93` integriert. Der Tick-Treiber ist enthalten; X4 bleibt eine Teilimplementierung. Der aktuelle technische Status steht in `PROJECT_STATE.yaml`, die nächsten Spieltests in `docs/ai/handoff/codex.md`.
