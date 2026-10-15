# Sitzungsübergabe

- **Branch/Commit:** `feat/rimworld-client`, Basis `feat/dedicated-server` bei `61ee7fb`; dieser basiert auf `feat/network-protocol` und `docs/project-governance`. Aktuellen Commit mit `git rev-parse HEAD` lesen. Kein Merge/Push.
- **Letzte Arbeit:** `NET-001` und `CLIENT-001`: Client-Nachrichtenpumpe, Mod-Einstellungen/UI, 1.6-Mod-Paket.
- **Belegt:** Client-TCP-Test (Snapshot, Ping/Pong, Disconnect), 1.6-Release-Build ohne Warnungen und XML/Paketprüfung. Details: `docs/verification/2026-10-15-client.md`. Servertests: `docs/verification/2026-10-15-dedicated-server.md`.
- **Offen:** RimWorld-Laufzeit- und UI-Test; im isolierten Versuch scheiterte `-nographics` an Unity-Atlanten, grafischer Start blieb bei Unity-Initialisierung. Zwei RimWorld-Clients fehlen. Kein TLS/Auth; Lizenz `LIC-001` ungeklärt.
- **Nächster Schritt:** In geeigneter grafischer Umgebung mit isolierter Modliste nur Core/DLC/`rimworldmp.foundation` aktivieren, UI unter Mod-Einstellungen prüfen und Lobby gegen `scripts/start-server.sh` testen. Relevante Dateien: `docs/client.md`, `docs/protocol.md`, `tests/Client.Tests/Program.cs`.
