# Dokumentationsregeln

- `PROJECT_STATE.yaml` ist die maßgebliche technische Momentaufnahme; `TASKS.yaml` ist das Aufgabenregister. Die Übergabe wiederholt nur das Nötigste und verweist auf beide.
- Implementiert, gebaut, automatisiert getestet, im Spiel getestet und für Merge freigegeben sind verschiedene Aussagen. Jede Verifikation verweist auf einen tatsächlich ausgeführten Befehl und dessen Ergebnis.
- Fachdetails stehen in `docs/*.md`, Entscheidungen in `docs/decisions/`, Testnachweise in `docs/verification/`, Review-Unterlagen in `docs/reviews/`.
- `CHANGELOG.md` enthält nur tatsächlich abgeschlossene Änderungen. Forschungsbehauptungen bleiben mit Geltungsbereich und Quellenstatus gekennzeichnet.
- Bei Widerspruch den Code und die reproduzierbare Prüfung untersuchen, dann Statusdateien korrigieren. Keine vollen Logs im zentralen YAML.
- `HEAD` statt eigener Commit-ID im selben Commit; exakten Hash bei Bedarf per `git rev-parse HEAD` lesen.
