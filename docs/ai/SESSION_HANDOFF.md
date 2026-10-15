# Sitzungsübergabe

- **Bereich:** serverseitige Regeln der Spieleinrichtung (`START-001`, `SETUP-003`, `SETUP-004`) nach ADR 0015 und ADR 0017. Aufbauend auf `ID-001` (Spielerkennung) und `XFER-001` (Chunk-Kanal). Grundlage ist ADR 0013: Steam-Listen-Host, kein Dedicated Server.
- **Branch:** `feat/setup-rules` von `feat/player-key` @ `2221608`, abhängig und ungemergt. Kette: `docs/full-multiplayer-plan` → `feat/chunk-transfer` → `feat/player-key` → `feat/setup-rules`. Worktree `/mnt/Arbeit/Git Projekte/RimworldMP-claude`, weil Codex parallel arbeitet. `feat/chunk-transfer` und `feat/player-key` sind gepusht, `feat/setup-rules` ist lokal.
- **Stand:** `LobbyRegistry` führt die Phasen Lobby → Scenario → … → Characters → Generating und setzt durch:
  - Start ab 2 Spielern und nur, wenn alle bereit sind.
  - Nur der Host schaltet weiter, nur zur nächsten Phase.
  - Lobby gesperrt während der Einrichtung.
  - Fähnchen von allen, Startort nur durch den Host.
  - Charakterwahl erst weiter, wenn alle fertig sind.
  - Abbruch nur durch den Host; Host-Abgang beendet die Einrichtung.
  Felder sind `PlanetTile` (Feld + Ebene, per Reflection geprüft). Nachrichten 24–27.
- **Geprüft:** Mod- und Server-Build ohne Warnungen, alle sieben Testprojekte, Validator. Details: `docs/verification/2026-10-15-setup-rules.md`.
- **Offen:**
  - Spielseite: Start-Knopf, vanilla-Seiten, Weltkarte, Charakterfenster.
  - Bedingungen Mod-Abgleich (MOD-001) und Welt-Fingerabdruck (SETUP-002).
  - Spieltests sind laut Benutzer derzeit nicht möglich.
  - Annahme in ADR 0015: Ein abgemeldeter Gast blockiert die Charakterwahl nicht; nicht bestätigt.
- **Nächster Schritt:** Review. Ohne Spieltest weiter möglich: die Serverseite von MOD-001 (Manifest per Chunk-Kanal, Vergleich mit dem Host, Bereit sperren) oder die Fingerabdruck-Bedingung von SETUP-002.
- **Dateien/Befehle:** `src/Multiplayer.Foundation.Lobby/LobbyRegistry.cs`, `src/Multiplayer.Foundation.Protocol/SetupPayloads.cs`, `LobbyPayloads.cs`, `FoundationServer.cs`; `dotnet run --project tests/Lobby.Tests/Lobby.Tests.csproj -c Release --nologo`.
