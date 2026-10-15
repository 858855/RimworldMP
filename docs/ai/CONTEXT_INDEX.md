# Kontextindex – drei Stufen

**Stufe 1, jeder Sitzungsstart:** `AGENTS.md`, `docs/ai/PROJECT_STATE.yaml`, `TASKS.yaml`, `SESSION_HANDOFF.md`, `git status`/Branch. `PROJECT_STATE.yaml` ist die maßgebliche Ist-Zustandsquelle; `TASKS.yaml` die Aufgabenquelle.

**Stufe 2, aktuelle Aufgabe:**

| Aufgabe | Zusätzlich lesen |
|---|---|
| Governance/Review | `docs/governance/`, `docs/verification/RECOVERY_REPORT.md` |
| Protokoll/Netzwerk | `docs/protocol.md`, `docs/ai/ARCHITECTURE.md`, `src/Multiplayer.Foundation.Protocol/`, `src/Multiplayer.Foundation.Network/`, zugehörige Tests |
| Server/Lobby | `docs/dedicated-server.md`, `docs/protocol.md`, `src/Multiplayer.Foundation.Server/`, `src/Multiplayer.Foundation.Lobby/`, Integrationstests |
| RimWorld-Client | `docs/client.md`, `docs/build.md`, `references/verified-apis.md`, `src/Multiplayer.Foundation.RimWorld/`, `src/Multiplayer.Foundation.Network/LobbyClient.cs`, `tests/Client.Tests/`, Mod-XML, `docs/verification/2026-10-15-client.md` |
| Fehler | Fehlermeldung/Logs, betroffene Quelldateien und Tests, passende ADRs |
| Verifikation/Deployment | `docs/testing.md`, `docs/build.md`, `docs/verification/`, `deployment/` |

**Stufe 3, nur bei neuer/unbekannter API oder Versionsfrage:** `research/`, `codex/`, `references/` anhand des Fachthemas. Archivbeispiele unter `examples/` sind keine produktiven Komponenten. `VALIDATION.md` beschreibt ihre ursprüngliche Prüfung.
