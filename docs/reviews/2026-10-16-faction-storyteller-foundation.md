# Review-Vorbereitung: Fraktions-Storyteller-Grundlage

- **Aufgabe/Branch:** FACTION-003, `codex/faction-storytellers` von `main` @ `d568766`.
- **Änderung:** ADR 0045 ersetzt im Fraktionsmodus die Host-Storyteller-Wahl durch eine geplante Wahl je Fraktion bei gemeinsamer Schwierigkeit. Der Spielstand kann Spieler, stabilen Fraktionsplatz, echte Spielerfraktion und Erzähler eindeutig verbinden und in Platzreihenfolge speichern. Ein Fingerabdruck prüft auch die Custom-Werte der Host-Schwierigkeit beim Laden; ungültige Daten verhindern den Tick-Start. Koop bleibt bei der bisherigen Host-Auswahl.
- **Betroffen:** RimWorld-Adapter (`MultiplayerSession`, `FoundationMod`), Architektur-, Aufgaben- und Benutzerdokumentation. Kein Koordinator-/Servercode.
- **Prüfung:** lokaler 1.6-Metadaten- und IL-Aufrufpfad-Abgleich, Mod-Release-Build ohne Warnungen und Projektvalidator; Einzelheiten in `docs/verification/2026-10-16-faction-storyteller-contract.md`.
- **Risiken/offen:** Noch keine Auswahloberfläche, Netzwerkübertragung, Erzeugung getrennter Spielerfraktionen oder separate Ereignisplanung; der Fraktionsstart bleibt gesperrt. Save/Load und zwei RimWorld-Clients wurden nicht getestet. `AllIncidentTargets` sammelt globale Ziele und `IncidentQueueTick` ruft den globalen `Find.Storyteller` auf; blindes Mehrfach-Ticken wäre falsch. R4 (Weltbedingungen) ist offen.
- **Status:** WIP, nicht bereit für Integration als funktionaler Fraktionsmodus. Kein Push und kein Merge ohne neue ausdrückliche Freigabe.
