# Review-Vorbereitung: Fraktions-Storyteller-Grundlage

- **Aufgabe/Branch:** FACTION-003, `codex/faction-storytellers` von `main` @ `d568766`.
- **Änderung:** ADR 0045 ersetzt im Fraktionsmodus die Host-Storyteller-Wahl durch eine geplante Wahl je Fraktion bei gemeinsamer Schwierigkeit. Der Spielstand kann die Zuordnung je dauerhaftem Spieler speichern und verweigert ungültige Fraktionsdaten vor dem Tick-Start. Koop bleibt bei der bisherigen Host-Auswahl.
- **Betroffen:** RimWorld-Adapter (`MultiplayerSession`, `FoundationMod`), Architektur-, Aufgaben- und Benutzerdokumentation. Kein Koordinator-/Servercode.
- **Prüfung:** lokaler 1.6-Metadatenabgleich und Mod-Release-Build nach gezielter Korrektur ohne Warnungen; Einzelheiten in `docs/verification/2026-10-16-faction-storyteller-foundation.md`.
- **Risiken/offen:** Noch keine Auswahloberfläche, Übertragung, Fraktions-IDs oder separate Ereignisplanung; der Fraktionsstart bleibt gesperrt. Save/Load und zwei RimWorld-Clients wurden nicht getestet. Die globale Vanilla-Storyteller-Instanz darf nicht pro Client ausgetauscht werden.
- **Status:** WIP, nicht bereit für Integration als funktionaler Fraktionsmodus. Kein Push und kein Merge ohne neue ausdrückliche Freigabe.
