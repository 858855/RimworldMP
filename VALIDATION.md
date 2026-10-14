# Auslieferungsprüfung — 2026-10-14

- `scripts/validate_structure.py` durchgeführt: **PASS**, 3 XML-Beispiele syntaktisch gültig.
- Angeforderte 16 Forschungsdateien, 7 Codex-Workflows, 5 Referenzdateien, `README.md`, `AGENTS.md`, `GESAMTBERICHT.md`: **vorhanden**.
- 1.6/1.7 werden strikt getrennt; `rwmt/Multiplayer` wird **nicht** als 1.7-kompatibel ausgegeben.
- GitHub-Dateilinks basieren auf `master`-Ansicht (bewegliche Quellen) und müssen für konkrete Entwicklung per Commit gepinnt werden.
- **NICHT AUSGEFÜHRT:** Builds gegen proprietäre Spiel-Assemblies, API-Binärprüfung, Singleplayer-Test, zwei Multiplayer-Clients, Cross-OS-Tests.
- Alle C#-Beispiele sind Demonstrations-/Startvorlagen und haben daher **keinen** Build-/Laufzeitnachweis.
