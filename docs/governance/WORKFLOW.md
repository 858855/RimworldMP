# Arbeitsablauf

1. Pflichtkontext aus `docs/ai/CONTEXT_INDEX.md` lesen; Git-Status, Branch und Abhängigkeiten prüfen.
2. Für größere Aufgaben einen kurzen Vertrag in `TASKS.yaml` oder `docs/decisions/` mit Ziel, Umfang, Nicht-Zielen, Akzeptanz und Tests festhalten.
3. Auf passendem Arbeitsbranch entwickeln. Abhängige, noch nicht gemergte Branches in `TASKS.yaml` und Review-Bericht benennen.
4. Risikobasiert bauen und testen; Fehlschläge mit Hypothese korrigieren. Nicht ausführbare Prüfungen ausdrücklich protokollieren.
5. Status, Fachdocs, CODEMAP, Changelog, Review und Übergabe aktualisieren. Nur zugeordnete Dateien stagen.
6. Nach jeder abgeschlossenen Arbeitseinheit automatisch einen Conventional Commit erstellen. Ohne Freigabe weder mergen noch pushen.

Drei Statusdimensionen sind in `TASKS.yaml` definiert. `done` wird nicht aus einem Commit abgeleitet. Git-`HEAD` ist die dynamische Commit-Referenz in versionierten Übergabedateien.
