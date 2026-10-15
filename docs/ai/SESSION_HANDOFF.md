# Sitzungsübergabe

- **Bereich:** `TEST-002` Mehrprozess-Verbindung und `SERVER-002` dedizierter Server bei Heartbeat-Verzug und SIGTERM.
- **Branch:** `fix/server-heartbeat`, Basis `test/multi-player-process` bei `4a6b129`, dessen Basis `main` bei `9d96665` liegt. Aktuellen Commit per `git log -1 --oneline` prüfen; beide Arbeitsbranches sind lokal, kein Push oder Merge.
- **Arbeitsstand:** Zwei unabhängige Client-Prozesse und Serverprozess verbinden sich; Lobby, Ready und Disconnect geprüft. Verzögertes Pong wird angenommen, doppelte/ungesendete Werte abgewiesen. Linux-SIGTERM stoppt den Server trotz aktiver Verbindung; Client erkennt die Trennung.
- **Prüfung:** 16 TCP-Integrationstestgruppen und acht Mehrprozess-Schritte mit .NET 10 auf Linux bestanden; Details unter `docs/verification/2026-10-15-multi-process-lobby.md` und `docs/verification/2026-10-15-server-heartbeat-shutdown.md`.
- **Offen:** RimWorld-UI-Laufzeit und zwei echte Spielprozesse; 1.7; Authentifizierung/TLS. `Version.txt` rev598 vs. Runtime rev600; grafischer Start erreichte bisher keinen belegten Mod-Ladeeintrag.
- **Nächster Schritt:** Mit bereinigter, isolierter Modliste den grafischen RimWorld-Start bis zum Hauptmenü und die Foundation-Mod-Einstellungen prüfen; danach zwei Spielclients mit dediziertem Server verbinden. Vorige 180-s-Wiederholung nicht ohne neue Diagnose wiederholen.
- **Dateien/Befehle:** `src/Multiplayer.Foundation.Server/FoundationServer.cs`, `Program.cs`, `tests/Integration.Tests/Program.cs`, `tests/ProcessIntegration.Tests/Program.cs`; Testbefehle in den genannten Nachweisen.
