# Sitzungsübergabe

- **Bereich:** `HOST-001`, Direkt-Hosting aus der Mod zusätzlich zum bestehenden Dedicated Server.
- **Branch:** `release/mod-install-0.1-dev`, Basis `fix/server-heartbeat` bei `1192eb5`; dahinter `test/multi-player-process` bei `4a6b129`. Aktuellen Commit und Remote per `git status --short --branch` prüfen; kein Merge.
- **Stand:** `FoundationServer` in gemeinsamen `netstandard2.1`-Hosting-Kern ausgelagert. `ListenHost` erstellt Lobby im Spielprozess; Mod bietet Host-IP, Lobbyname, Kapazität und Direkt-Host-Knopf. Gast nutzt den vorhandenen Verbindungs-/Beitreten-Ablauf. Neues ZIP enthält fünf eigene DLLs.
- **Prüfungen:** Vier Direkt-Host-Gruppen, 16 Server-Gruppen, acht Mehrprozess-Schritte, drei Client-Gruppen, RimWorld-1.6-Release-Build ohne Warnungen und ZIP-Extraktion bestanden. Details in `docs/verification/2026-10-15-direct-host.md`.
- **Offen:** Aktualisiertes ZIP auf GitHub pushen und Download-Prüfsumme nachweisen. UI mit zwei tatsächlich laufenden RimWorld-Clients, LAN über zwei Rechner und 1.7 bleiben ungeprüft. Keine Authentifizierung/TLS.
- **Bekannte Probleme:** `Version.txt` rev598 vs. frühere Runtime rev600; grafischer Start erreichte bislang keinen belegten Mod-Ladeeintrag.
- **Nächster Schritt:** Paket-Commit, Push auf Arbeitsbranch, GitHub-Download prüfen; anschließend beide Verbindungswege manuell im Spiel verifizieren.
- **Dateien/Befehle:** `src/Multiplayer.Foundation.Hosting/`, `src/Multiplayer.Foundation.RimWorld/FoundationMod.cs`, `scripts/build-mod.sh`, `scripts/package-mod.py`, `tests/Hosting.Tests/`; vollständige Befehle im Prüfnachweis.
