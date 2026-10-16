# Kontextindex – drei Stufen

**Fortsetzung nach dem Prompt-Archiv:** `docs/ai/CONTINUATION_PROMPT.md` verlangt auf ausdrücklichen Benutzerwunsch einmalig das vollständige Laden aller drei Initialprompts. Danach gelten wieder die folgenden Kontextstufen.

**Stufe 1, jeder Sitzungsstart:** `AGENTS.md`, `docs/ai/PROJECT_STATE.yaml`, `TASKS.yaml`, `SESSION_HANDOFF.md`, `git status`/Branch. `PROJECT_STATE.yaml` ist die maßgebliche Ist-Zustandsquelle; `TASKS.yaml` die Aufgabenquelle.

**Stufe 2, aktuelle Aufgabe:**

| Aufgabe | Zusätzlich lesen |
|---|---|
| Governance/Review | `docs/governance/WORKFLOW.md`, `REVIEW_POLICY.md`, `DOCUMENTATION_POLICY.md`, bei Regelherkunft `PROMPT_HISTORY.md`; `docs/verification/RECOVERY_REPORT.md` |
| Protokoll/Netzwerk | `docs/protocol.md`, `docs/ai/ARCHITECTURE.md`, `src/Multiplayer.Foundation.Protocol/`, `src/Multiplayer.Foundation.Network/`, zugehörige Tests |
| Server/Lobby | `docs/dedicated-server.md`, `docs/protocol.md`, `src/Multiplayer.Foundation.Server/`, `src/Multiplayer.Foundation.Lobby/`, Integrationstests |
| RimWorld-Client | `docs/client.md`, `docs/build.md`, `references/verified-apis.md`, `src/Multiplayer.Foundation.RimWorld/`, `src/Multiplayer.Foundation.Network/LobbyClient.cs`, `tests/Client.Tests/`, Mod-XML, `docs/verification/2026-10-15-client.md` |
| Fraktionsstart (FACTION-005) | `docs/decisions/0031-game-modes.md`, `docs/decisions/0037-faction-storytellers-coordinator.md`, `docs/decisions/0046-faction-setup-preflight.md`, `docs/verification/2026-10-16-faction-setup-audit.md`, `scripts/faction-setup-probe/`, `FoundationMod.cs`, `MultiplayerSession.cs` |
| Fraktions-Laufzeitschnitt / Figurenwahl | `docs/decisions/0047-faction-runtime-test-slice.md`, `docs/decisions/0048-selected-start-pawns.md`, `docs/verification/2026-10-16-faction-runtime-and-pawns.md`, `FactionRuntimeTestSlice.cs`, `CharacterHost.cs`, `GameCodec.cs`, `WorldSelfTest.cs` |
| Steam-Hosting | `docs/decisions/0010-steam-home-host.md`, `docs/client.md`, `src/Multiplayer.Foundation.RimWorld/SteamLobbyBridge.cs`, `SteamPeerStream.cs`, `docs/verification/2026-10-15-steam-transport.md`, `docs/verification/2026-10-15-steam-user-connection.md` |
| Hauptmenü/Chat | `docs/decisions/0011-main-menu-and-lobby-chat.md`, `docs/client.md`, `docs/protocol.md`, `MainMenuOptionPatch.cs`, `FoundationWindow.cs`, `FoundationMod.cs`, `FoundationServer.cs`, zugehörige Tests |
| Planung / nächste Meilensteine | `docs/plans/rimworldmp-full-plan.md` (Gesamtplan bis 1.0, Englisch), Einträge `VER-001` … `REL-001` in `TASKS.yaml` |
| Fehler | Fehlermeldung/Logs, betroffene Quelldateien und Tests, passende ADRs |
| Verifikation/Deployment | `docs/testing.md`, `docs/build.md`, `docs/verification/`, `deployment/` |

**Stufe 3, nur bei neuer/unbekannter API oder Versionsfrage:** `research/`, `codex/`, `references/` anhand des Fachthemas. Archivbeispiele unter `examples/` sind keine produktiven Komponenten. `VALIDATION.md` beschreibt ihre ursprüngliche Prüfung.

Die vollständigen historischen Prompts unter `docs/governance/history/` nur für Regel-Audits laden. Für normale Folgeaufträge sind `AGENTS.md` und die aktuellen Governance-Dateien maßgeblich.
