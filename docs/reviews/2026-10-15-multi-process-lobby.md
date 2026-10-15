# Review: Mehrprozess-Lobby-Verbindung

- Aufgabe: `TEST-002`
- Quellbranch: `test/multi-player-process` (lokal); Zielbranch: `main` nach ausdrücklicher menschlicher Freigabe.
- Commit: nach Commit-Erstellung per `git log -1 --oneline` ermitteln.
- Änderung: Ein Test startet den vorhandenen Server und zwei unabhängige Clients als eigene Prozesse und prüft Lobby-Liste, Beitritt, gleiche Snapshots, Ready und Disconnect. Produktcode wird in dieser Einheit nicht verändert.
- Grund: Bisherige TCP-Integration lief mit beiden Clients in einem einzigen Testprozess.
- Entscheidung: ADR 0006 beschreibt Prozessgrenze und Testumfang.
- Prüfung: Release-Lauf des Testprojekts, sieben PASS-Schritte, Exit 0; siehe `docs/verification/2026-10-15-multi-process-lobby.md`.
- Risiko: Kurzfristige Portbelegung zwischen freier-Port-Ermittlung und Serverstart; Test verwendet nur Loopback. RimWorld-UI bleibt ungeprüft.
- Menschliche Prüfung: Testgrenze und Prozessbereinigung ansehen; keine Freigabe oder Merge dokumentiert.

**Nachtrag 2026-10-15:** Die spätere ausdrückliche Merge-Freigabe und die Integration in `main` sind in `docs/reviews/2026-10-15-dual-mode-merge.md` dokumentiert. Die obigen Angaben beschreiben den vorherigen Review-Stand.
