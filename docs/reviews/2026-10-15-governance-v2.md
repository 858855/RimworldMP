# Review – Governance 2.0

- **Aufgabe/Branch/Ziel:** `GOV-002`, `docs/project-governance` → `main`; Branch enthält bereits `DOC-001` aus `f3c795c`.
- **Änderung:** Recovery nach Abbruch dokumentiert; alte Regeln auf automatische lokale Commits, drei Statusdimensionen, Push-/Merge-Schutz und gestuften Kontextzugriff migriert. Produktziel 0.1 als Planung festgehalten. Kein produktiver Netzwerk- oder Mod-Code in diesem Commit.
- **Entscheidungen:** ADR 0002 trennt Lobby-Transport und RimWorld-Adapter. Alte Forschung und Beispiele bleiben erhalten.
- **Prüfung:** Siehe `docs/verification/2026-10-15-governance-v2.md`; XML-/Archivprüfungen separat. C#-Build und Game-/MP-Tests nicht ausgeführt.
- **Risiken:** SDK fehlt; Lizenz offen; RimWorld-API und 1.7-MP nicht verifiziert.
- **Menschlich prüfen:** Produktgrenze 0.1, Lizenz, Branch-Abhängigkeiten und Review-Regeln. Kein Merge und kein Push durch Codex.
