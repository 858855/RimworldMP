# Governance-2.0-Prüfung — 2026-10-15

- **Branch:** `docs/project-governance`; maßgeblicher Commit nach Erstellung: `HEAD`.
- **Umgebung:** Python 3 und PyYAML 6.0.3 auf Linux. RimWorld lokal `1.6.4871 rev598`, Steam-Build `23969874`.

| Befehl | Tatsächliches Ergebnis | Grenze |
|---|---|---|
| `python3 scripts/verify_knowledge_archive.py` | PASS, 49 Einträge | Nur Archivintegrität |
| `python3 scripts/validate_structure.py` | PASS, drei XML-Beispiele | Kein RimWorld-Def- oder Laufzeittest |
| `python3 scripts/validate_project_state.py` | PASS nach Ergänzung des fehlenden Prüfberichts | YAML, Pflichtfelder, Statuswerte, Aufgaben-IDs, Abhängigkeiten und lokale Markdown-Verweise |
| `git diff --check` | Exit 0 | Whitespace-Prüfung, kein Build |

Der erste Lauf des neuen Validators meldete korrekt den noch fehlenden Nachweis `docs/verification/2026-10-15-governance-v2.md`. Nach Anlegen des Berichts wurde er erneut ausgeführt. C#-Build, Server, Game-Start, zwei Clients und Desync wurden nicht getestet. Das .NET-SDK fehlt im PATH.
