# Sitzungsübergabe

- **Bereich:** `HOST-001`, Direkt-Hosting aus der Mod zusätzlich zum bestehenden Dedicated Server.
- **Branch:** `release/mod-install-0.1-dev`, Basis `fix/server-heartbeat` bei `1192eb5`; dahinter `test/multi-player-process` bei `4a6b129`. Paket-Commit `a8aadae` auf `origin/release/mod-install-0.1-dev` gepusht; aktuellen Dokumentations-Commit und Remote per `git status --short --branch` prüfen. Kein Merge.
- **Stand:** `FoundationServer` in gemeinsamen `netstandard2.1`-Hosting-Kern ausgelagert. `ListenHost` erstellt Lobby im Spielprozess; Mod bietet Host-IP, Lobbyname, Kapazität und Direkt-Host-Knopf. Gast nutzt den vorhandenen Verbindungs-/Beitreten-Ablauf. Neues ZIP enthält fünf eigene DLLs.
- **Prüfungen:** Vier Direkt-Host-Gruppen, 16 Server-Gruppen, acht Mehrprozess-Schritte, drei Client-Gruppen, RimWorld-1.6-Release-Build ohne Warnungen und ZIP-Extraktion bestanden. Details in `docs/verification/2026-10-15-direct-host.md`.
- **Download:** `https://raw.githubusercontent.com/858855/RimworldMP/a8aadae428446f08648d957582deb1c41f8e244b/releases/RimWorldMultiplayerFoundation-0.1.0-dev-rw1.6.zip`; HTTP 200, 42.985 Bytes und identische SHA-256-Prüfsumme bestätigt. UI mit zwei tatsächlich laufenden RimWorld-Clients, LAN über zwei Rechner und 1.7 bleiben ungeprüft. Keine Authentifizierung/TLS.
- **Bekannte Probleme:** `Version.txt` rev598 vs. frühere Runtime rev600; grafischer Start erreichte bislang keinen belegten Mod-Ladeeintrag.
- **Nächster Schritt:** Aktualisiertes ZIP mit zwei Spielclients installieren; Direkt-Hosting und Dedicated Server im Spiel und LAN manuell prüfen.
- **Dateien/Befehle:** `src/Multiplayer.Foundation.Hosting/`, `src/Multiplayer.Foundation.RimWorld/FoundationMod.cs`, `scripts/build-mod.sh`, `scripts/package-mod.py`, `tests/Hosting.Tests/`; vollständige Befehle im Prüfnachweis.
