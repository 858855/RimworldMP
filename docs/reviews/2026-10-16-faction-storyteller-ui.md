# Review FACTION-003 – Storyteller-Auswahl in der Spieloberfläche

- **Quellbranch:** `codex/faction-ui`, Basis `main` @ `ea065aa`; Zielbranch für spätere Integration: `main`.
- **Änderung:** Codex' bisherige Save/Load-Grundlage wurde auf Claudes integrierten Lobbyvertrag gebracht. Der Host veröffentlicht die gemeinsame Schwierigkeit und kompatible Erzähler; jeder Spieler wählt seinen Erzähler und sieht Platz, Wahl und Fertig-Zustand. Die Server-Seite nimmt nur gültige Anfragen an.
- **Betroffen:** `FoundationMod`, `SetupMirror`, `MultiplayerSession`, ADR 0045 und zugehörige Status-/Benutzerdokumentation. Keine Änderungen an Claudes Protocol-, Lobby-, Hosting- oder Server-Dateien.
- **Commits:** `214b5ee`, `7bd721c` sowie der UI-Commit dieses Branches (`git log -1` prüfen).
- **Prüfung:** Mod-Release-Build ohne Warnungen; Protocol-, Lobby- und Hosting-Tests bestanden. Befehle und Grenzen: `docs/verification/2026-10-16-faction-storyteller-ui.md`.
- **Offen/Risiken:** Kein RimWorld-UI- oder Save/Load-Spieltest. Getrennte Spielerfraktionen und sichere Ereignisziele fehlen; der Fraktionsstart bleibt gesperrt. Weltweite Ereignisse nach ADR 0037 R4 brauchen eine ausdrückliche Regel.
- **Menschlich prüfen:** Ist die Host-Bedienung zur Veröffentlichung der Optionen verständlich? Stimmen die angezeigten Wahlen bei zwei Clients überein? Erst nach gesonderter Freigabe integrieren; dieser Teilstand ist kein spielbarer Fraktionsmodus.
- **Integrationsstatus:** `not_submitted`; kein Push und kein Merge ohne neue ausdrückliche Benutzerfreigabe.
