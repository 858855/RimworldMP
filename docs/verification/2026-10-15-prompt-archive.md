# Archivierung der Benutzer-Prompts – 2026-10-15

- **Aufgabe/Branch:** `GOV-003`, `docs/prompt-archive`, aufgebaut auf `test/rimworld-runtime`/`4eb647e` und damit auf dem hochgeladenen `main`/`f86ab83`.
- **Quelle:** Drei als Benutzerbeiträge gekennzeichnete Nachrichten aus der lokalen Codex-Sitzungsaufzeichnung vom 14./15. Oktober 2026. Nur deren Text wurde übernommen; Umgebungsnachrichten und Antworten wurden ausgeschlossen.
- **Dateien:** `docs/governance/history/UEBERPROMPT_V1.md` (8 806 Zeichen), `MASTERPROMPT_V1.md` (37 116 Zeichen), `MASTERPROMPT_V2.md` (46 916 Zeichen). Exakte SHA-256-Werte stehen in `docs/governance/PROMPT_HISTORY.md`.
- **Prüfung:** Auswahl anhand eindeutiger Textanfänge und eindeutiger Trefferzahl (`assert`), UTF-8-SHA-256 bei der Extraktion berechnet. Ein einfacher Scan auf typische Zugangsdaten-Präfixe/Private-Key-Marker war negativ; er ersetzt keine umfassende Geheimnisprüfung.
- **Konsistenz:** `python3 scripts/validate_project_state.py` → Exit 0, Pflichtdateien/Status/Referenzen gültig; `git diff --check` → Exit 0. Hashes, Zeichenzahlen und vollständige Textgleichheit wurden vor Commit unabhängig erneut geprüft. `git -c core.whitespace=-blank-at-eol diff --cached --check` → Exit 0; die originale Markdown-Formatierung mit absichtlichen Zeilenend-Leerzeichen bleibt erhalten.
- **Geltung:** Die historischen Texte werden für Audit und Nachvollziehbarkeit veröffentlicht. Aktuelle Arbeitsregeln sind `AGENTS.md` und die Governance-2.0-Dateien; dies wird im Index und Kontextindex erklärt.
