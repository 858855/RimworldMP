# Dateikarte

| Pfad | Zweck und Einstieg | Schnittstellen / Tests | Stand |
|---|---|---|---|
| `AGENTS.md`, `docs/governance/` | Verbindliche Regeln und Detailworkflow | `scripts/validate_project_state.py` | vorhanden |
| `docs/ai/PROJECT_STATE.yaml` | Maßgeblicher technischer Ist-Zustand | YAML-Schema v2 | vorhanden |
| `docs/ai/TASKS.yaml` | Aufgaben und drei Statusdimensionen | Validator prüft IDs/Abhängigkeiten | vorhanden |
| `scripts/validate_structure.py` | Archiv-XML-Syntax prüfen | drei Beispiel-XML-Dateien | ausgeführt |
| `scripts/verify_knowledge_archive.py` | Geliefertes ZIP gegen Manifest prüfen | Archiv, 49 Einträge | ausgeführt |
| `examples/` | Illustrative RimWorld- und MP-Beispiele | keine produktive API | unkompiliert |
| `src/Multiplayer.Foundation.Protocol/` | Framing und Nachrichten | `tests/Protocol.Tests/` | geplant |
| `src/Multiplayer.Foundation.Network/` | TCP-Client und Verbindungszustand | `tests/Network.Tests/` | geplant |
| `src/Multiplayer.Foundation.Lobby/` | Lobbyzustand | `tests/Lobby.Tests/` | geplant |
| `src/Multiplayer.Foundation.Server/` | Headless-Host | `tests/Integration.Tests/` | geplant |
| `src/Multiplayer.Foundation.RimWorld/`, `mod/` | Game-Adapter und UI | Game-Start und Integration | geplant |
