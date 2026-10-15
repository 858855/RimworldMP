# Migration der Projektregeln auf Version 2.0

## Vorher

`AGENTS.md` enthielt RimWorld-/MP-Regeln, Sitzungsstart, Branch-/Merge-Verbot und allgemeine Dokumentationspflege. `PROJECT_STATE.yaml` hatte ein einfaches Phasenfeld; `TASKS.yaml` ein einzelnes Statusfeld. Die Wissensbasis und drei XML-Beispiele waren geprüft. Kein produktiver Mod-Code war vorhanden.

## Änderungen

- Projektziel auf „RimWorld Multiplayer Foundation 0.1“ konkretisiert, ohne Forschungsbeispiele als Produktcode umzudeuten.
- Git-Regeln ergänzt: automatische lokale Commits, kein automatischer Push, menschliche Freigabe vor **jedem** Merge, Schutz fremder Änderungen, dokumentierte Branch-Abhängigkeiten.
- Status in Implementierung, Verifikation und Integration getrennt; `PROJECT_STATE.yaml` und `TASKS.yaml` auf Schema 2 migriert.
- Drei Kontextstufen und eine einzige maßgebliche Quelle je Information eingeführt.
- Recovery, Governance-Workflow, Review-/Dokumentationsregeln, Produkt-Spezifikation und Dokumentationsvalidator ergänzt.
- Fehlerbehandlung und risikobasierte Testauswahl als Arbeitsablauf festgelegt.

## Erhalten und offen

`research/`, `references/`, `codex/`, `examples/`, Archiv, historische Prüfberichte und ADR 0001 bleiben erhalten. Alte Statusdateien wurden aktualisiert, historische Nachweise nicht umgeschrieben. Lizenzentscheidung, .NET-Buildumgebung, RimWorld-API-/UI-Laufzeittest und 1.7-Kompatibilität sind offen. Kein Merge oder Push erfolgt.
