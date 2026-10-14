# Review: Projektgedächtnis und Wissensbasis

- **Aufgabe:** `DOC-001`
- **Branch:** `docs/project-governance` (kein Merge)
- **Zweck:** Den vom Benutzer bereitgestellten Forschungsstand zugänglich und den Projektzustand für spätere Sitzungen eindeutig machen.

## Änderungen und Wirkung

- Vorhandene `AGENTS.md` und `README.md` bleiben erhalten; `AGENTS.md` erhält nur den neuen Sitzungs- und Dokumentationsablauf.
- Der geprüfte Archivinhalt wird in seine dokumentierten Pfade extrahiert. Das ZIP bleibt als Quellartefakt erhalten.
- `docs/ai/` enthält Status, Aufgaben mit Abhängigkeiten, Kontextindex, Architektur, Dateikarte und Übergabe.
- `docs/decisions/`, `docs/verification/`, `docs/reviews/` und `CHANGELOG.md` vervollständigen Entscheidungen, Nachweise und Änderungshistorie.
- Es entsteht kein produktiver Mod-Code und keine behauptete Spiel- oder MP-Funktion.

## Prüfung vor Merge

1. Stimmen Projektziel und vorgeschlagener 1.6-Arbeitspfad mit der beabsichtigten Richtung überein?
2. Soll das bereitgestellte ZIP als reproduzierbares Quellartefakt im Repository bleiben?
3. Stimmen Aufgabenstatus, Prüfnachweise und Forschungsgrenzen mit den Dateien überein?
4. Nach Freigabe: Merge ausdrücklich durch einen Menschen veranlassen; diese Arbeit führt ihn nicht aus.

Prüfergebnisse und nicht ausgeführte Tests: `docs/verification/2026-10-14-project-baseline.md`.
