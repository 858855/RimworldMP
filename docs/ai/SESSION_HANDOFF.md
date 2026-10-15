# Sitzungsübergabe

- **Bereich:** `PKG-001`, manuell installierbares Mod-ZIP für RimWorld 1.6.
- **Branch:** `release/mod-install-0.1-dev`, Basis `fix/server-heartbeat` bei `1192eb5`; davor `test/multi-player-process` bei `4a6b129`. Paket-Commit `7e0707a` zu GitHub gepusht; aktuellen Dokumentations-Commit mit `git log -1 --oneline` prüfen. Kein Merge.
- **Stand:** Frischer 1.6-Release-Build und ZIP mit `About.xml`, drei eigenen Assemblies und Anleitung erstellt. Archivstruktur und Entpacken in `RimWorld/Mods/` geprüft; SHA-256 unter `docs/verification/2026-10-15-installable-mod-zip.md`.
- **Download:** GitHub-URL und SHA-256 unter `docs/verification/2026-10-15-installable-mod-zip.md`; HTTP 200, 24.182 Bytes, Prüfsummen identisch. RimWorld-UI im Spiel und zwei tatsächliche Spielclients bleiben ungeprüft. 1.7 und Authentifizierung/TLS offen.
- **Bekanntes Problem:** `Version.txt` rev598, frühere Runtime rev600; grafischer Start erreichte bislang keinen belegten Mod-Ladeeintrag.
- **Nächster Schritt:** ZIP aus GitHub manuell in `RimWorld/Mods/` entpacken, im Spiel aktivieren, UI bis zur Lobby prüfen; danach zwei Spielclients gegen den dedizierten Server testen.
- **Dateien/Befehle:** `scripts/build-mod.sh`, `scripts/package-mod.py`, `mod/INSTALLATION.md`, `releases/RimWorldMultiplayerFoundation-0.1.0-dev-rw1.6.zip`; Build-/Prüfbefehle im Verifizierungsbericht.
