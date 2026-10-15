# Prüfstrategie und Ist-Stand

Nach Änderungstyp testen: Protocol mit Framing-/Grenzwertfällen, Network mit echten TCP-Verbindungen, Server mit Start/Shutdown und ungültigen Clients, Lobby mit zwei Zustandsvergleichen, RimWorld mit Build, Game-Start und UI. Nachweise unter `docs/verification/` nennen Befehl, Ergebnis und Grenzen.

**Ausgeführt:** Archivprüfung (49 Einträge), drei XML-Strukturprüfungen, Governance-Validator und fünf Protokoll- und drei Netzwerk-Loopback-Testgruppen. **Nicht ausgeführt:** Serverlauf, RimWorld-Client-Build/Game-Start, zwei RimWorld-Clients. `VALIDATION.md` ist ein historischer Archivbericht, kein Produktnachweis.
