# Kontextindex

## Pflicht beim Sitzungsstart

1. `AGENTS.md` und eventuell weitere `AGENTS.md` im Arbeitsbereich lesen.
2. `docs/ai/PROJECT_STATE.yaml`, `TASKS.yaml` und `SESSION_HANDOFF.md` lesen.
3. Git-Branch, `git status` und `git log -1` prüfen.
4. Nur die für die Aufgabe benötigten Dateien aus der Tabelle nachladen.

| Aufgabe | Zusätzlich lesen |
|---|---|
| Projektumfang und Architektur | `README.md`, `docs/ai/ARCHITECTURE.md`, `codex/architecture-guidelines.md` |
| Code- oder Dateisuche | `docs/ai/CODEMAP.md`; danach konkrete Dateien |
| RimWorld-Version, Portierung | `references/version-matrix.md`, `research/02-engine-and-future-changes.md`, `research/16-version-compatibility.md` |
| XML/Defs/Patches | `research/03-modding-fundamentals.md`, `research/04-xml-modding.md`, `codex/mod-development-workflow.md` |
| C#/Harmony | `research/05-csharp-modding.md`, `research/06-harmony-patching.md`, `references/verified-apis.md` |
| Multiplayer/Determinismus | `research/07-multiplayer-architecture.md` bis `research/11-desync-debugging.md`, `codex/multiplayer-rules.md`, `references/verified-apis.md` |
| Build und Tests | `research/13-linux-development.md`, `research/15-build-and-testing.md`, `codex/testing-checklist.md` |
| Quellenlage und offene Fragen | `references/sources.md`, `references/open-questions.md`; bewegliche Links vor Nutzung erneut prüfen |
| Review und Nachweise | `docs/reviews/`, `docs/verification/`, `CHANGELOG.md` |

Die Forschungsdokumente stammen aus dem am 2026-10-14 bereitgestellten Archiv. Ihr Auslieferungsstatus steht in `VALIDATION.md`; ein erfolgreicher Strukturtest ist kein Laufzeittest.
