# Review und Freigabe: Zwei Verbindungsarten in `main`

- **Aufgaben:** `TEST-002`, `SERVER-002`, `PKG-001`, `HOST-001`.
- **Quellbranch:** `release/mod-install-0.1-dev`, einschließlich der abhängigen Branches `test/multi-player-process` und `fix/server-heartbeat`.
- **Zielbranch:** `main`.
- **Menschliche Freigabe:** Der Benutzer schrieb am 2026-10-15 ausdrücklich „merge“ als Antwort auf die bereitgestellte Arbeit. Damit wurde die Integration des aktuellen Release-Branches in `main` freigegeben. Keine weiteren Bedingungen wurden genannt.
- **Integration:** Merge-Commit `e3905144a8fae86de82161226114f38990e62403` (`merge: integrate dual-mode lobby foundation`), anschließend erfolgreich `git push origin main` von `9d96665` auf `e390514`.
- **Änderung:** Mehrprozess-Nachweis, stabilere Heartbeats und SIGTERM-Behandlung, installierbares Mod-ZIP, Direkt-Hosting im Spiel zusätzlich zum Dedicated Server. Der vorhandene TCP-Lobby-Kern wird für beide Wege genutzt.
- **Relevante Commits:** `4a6b129`, `1192eb5`, `7e0707a`, `a424273`, `e16ac3b`, `a8aadae`, `2b78155` und Merge `e390514`.
- **Prüfung vor Integration:** Projektvalidator bestanden; vier Hosting-Testgruppen, 16 Server-Integrationstestgruppen, acht Mehrprozess-Schritte und RimWorld-1.6-Mod-Build mit null Warnungen und null Fehlern bestanden. Einzelheiten in `docs/verification/2026-10-15-dual-mode-merge.md`.
- **Risiken und manuelle Prüfung:** Mod-UI und Gastbeitritt im tatsächlich gestarteten Spiel noch nicht nachgewiesen. Installation, beide Verbindungswege, Host-Stopp und LAN-Erreichbarkeit mit zwei Spielclients prüfen. Ohne Authentifizierung/TLS nur für kontrolliertes LAN/Testnetz.
