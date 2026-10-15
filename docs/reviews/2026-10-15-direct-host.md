# Review: Direkt-Hosting zusätzlich zum Dedicated Server

- Aufgabe: `HOST-001`; Quellbranch `release/mod-install-0.1-dev`; Zielbranch `main` nur nach ausdrücklicher menschlicher Freigabe. Branch baut auf `fix/server-heartbeat` und `test/multi-player-process` auf.
- Änderung: Gemeinsamer Hosting-Kern unter `netstandard2.1`, `ListenHost` im Spielprozess, Host-IP-/Lobby-Felder und Direkt-Host-Knopf in der Mod. ZIP enthält jetzt fünf eigene DLLs und eine Anleitung für beide Wege. Der dedizierte Server verwendet denselben Kern weiter.
- Commits: `e16ac3b` (Serverkern verschoben), `a8aadae` (Listen-Host, UI, Tests, ZIP); zusätzlicher Dokumentations-Commit per `git log -1` ermitteln. ZIP auf GitHub gepusht, kein Merge.
- Entscheidung: ADR 0009; bestehendes Wire-Protokoll unverändert.
- Nachweis: vier Direkt-Host-Gruppen, 16 Server-Gruppen, acht Mehrprozess-Schritte, drei Client-Gruppen, Mod-Release-Build und ZIP-Extraktion bestanden. Details: `docs/verification/2026-10-15-direct-host.md`.
- Risiken: Bedienung im RimWorld-Spiel noch nicht nachgewiesen; LAN-Reichweite hängt von Bind-IP und Firewall ab. Keine Authentifizierung/TLS oder Gameplay-Synchronisation.
- Menschliche Prüfung: Beide Knöpfe im Spiel, Gastbeitritt über LAN, Trennung beim Host-Stopp und Paketinstallation. Keine Merge-Freigabe dokumentiert.
