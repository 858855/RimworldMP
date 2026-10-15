# Review: Server-Heartbeat und Shutdown

- Aufgabe: `SERVER-002`
- Quellbranch: `fix/server-heartbeat`, lokal; Basis `test/multi-player-process` bei `4a6b129`; Zielbranch: `main` erst nach ausdrücklicher menschlicher Freigabe und Berücksichtigung des Basisbranches.
- Commit: mit `git log -1 --oneline` nach Erstellung ermitteln.
- Änderung: Gültige verspätete Pong-Antworten erhalten die Sitzung; doppelte und nicht gesendete Nummern werden abgewiesen. Linux-SIGTERM beendet den dedizierten Server kontrolliert.
- Komponenten: `FoundationServer`, Server-`Program`, TCP-Integrations- und Mehrprozess-Tests.
- Entscheidung: ADR 0007; keine Protokollformat-Änderung.
- Nachweis: 16 Integrationstestgruppen und acht Mehrprozess-Schritte bestanden. Details in `docs/verification/2026-10-15-server-heartbeat-shutdown.md`.
- Risiken: Authentifizierung/TLS fehlen; RimWorld-Spiel-UI wurde nicht ausgeführt. Prozess-SIGTERM-Test ist auf Unix-Systeme begrenzt.
- Menschliche Prüfung: Heartbeat-Grenzen und Signalverhalten beurteilen; keine Freigabe oder Merge dokumentiert.
