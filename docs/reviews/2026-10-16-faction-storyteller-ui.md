# Review FACTION-003 – Storyteller-Auswahl in der Spieloberfläche

- **Quellbranch:** `codex/faction-ui`, Basis `main` @ `ea065aa`; Zielbranch für spätere Integration: `main`.
- **Änderung:** Codex' bisherige Save/Load-Grundlage wurde auf Claudes integrierten Lobbyvertrag gebracht. Der Host veröffentlicht die gemeinsame Schwierigkeit und kompatible Erzähler; jeder Spieler wählt seinen Erzähler und sieht Platz, Wahl und Fertig-Zustand. Die Server-Seite nimmt nur gültige Anfragen an.
- **Betroffen:** `FoundationMod`, `SetupMirror`, `MultiplayerSession`, ADR 0045 und zugehörige Status-/Benutzerdokumentation. Keine Änderungen an Claudes Protocol-, Lobby-, Hosting- oder Server-Dateien.
- **Commits:** `214b5ee`, `7bd721c`, `894816f`, `007b8bb`; zusätzlich dieser Merge-Prüfcommit (`git log -1` prüfen).
- **Prüfung:** Mod-Release-Build ohne Warnungen; alle acht .NET-Testprojekte und Projektvalidator bestanden. Befehle und Grenzen: `docs/verification/2026-10-16-faction-ui-merge-gate.md`.
- **Offen/Risiken:** Kein RimWorld-UI- oder Save/Load-Spieltest. Getrennte Spielerfraktionen und sichere Ereignisziele fehlen; der Fraktionsstart bleibt gesperrt. Weltweite Ereignisse nach ADR 0037 R4 brauchen eine ausdrückliche Regel.
- **Manuelle Folgeprüfung:** Ist die Host-Bedienung zur Veröffentlichung der Optionen verständlich? Stimmen die angezeigten Wahlen bei zwei Clients überein? Dieser Teilstand ist kein spielbarer Fraktionsmodus.
- **Menschliche Freigabe:** Benutzeranweisung vom 2026-10-16: „dann push und merge und mach weiter“. Sie gilt für diesen Quellbranch und das Ziel `main`. Die Integration bleibt ein Teilstand der FACTION-003-Aufgabe; sie schaltet den Fraktionsstart nicht frei.
- **Integrationsstatus:** `approved` vor dem Merge; tatsächlichen Merge-Commit danach auf `main` dokumentieren.
