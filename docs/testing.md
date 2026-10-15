# Prüfstrategie

Risikobasiert prüfen: Governance per YAML-/Verweisvalidator; Protokoll per Framing-/Grenzwerttests; Server per Start/Shutdown- und echten TCP-Integrationstests; Lobby per Zwei-Client-Zustandsvergleich; RimWorld per Build, Game-Start und UI-Aktionen. Jeder Nachweis nennt Befehl, Umgebung, Exit-Code und Grenzen unter `docs/verification/`.

**Bisher:** Archivprüfung (49 Einträge) und drei XML-Strukturprüfungen bestanden. Kein C#-Build, kein Serverlauf, kein Game-Start und kein Zwei-Client-Test. `VALIDATION.md` ist ein historischer Archivbericht, kein Produktnachweis.
