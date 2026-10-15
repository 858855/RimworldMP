# Review: Manuell installierbares Mod-ZIP

- Aufgabe: `PKG-001`
- Quellbranch: `release/mod-install-0.1-dev`; Basis `fix/server-heartbeat` bei `1192eb5`, dessen Basis `test/multi-player-process` bei `4a6b129` ist. Zielbranch bei möglicher Integration: `main`, nur mit ausdrücklicher menschlicher Merge-Freigabe.
- Paket-Commit: `7e0707a3e5521c5351ce6f80a6b2f18f085d517e`; auf GitHub gepusht, nicht gemergt.
- Änderung: Paketwerkzeug und 1.6-ZIP mit `About.xml`, drei eigenen Assemblies und deutscher Anleitung. Kein Server im ZIP.
- Build und Prüfung: Release-Build Exit 0 ohne Warnungen; ZIP-Prüfung und Entpacken in temporären `RimWorld/Mods/`-Pfad bestanden. Prüfsumme und Details: `docs/verification/2026-10-15-installable-mod-zip.md`.
- Download: `https://raw.githubusercontent.com/858855/RimworldMP/7e0707a3e5521c5351ce6f80a6b2f18f085d517e/releases/RimWorldMultiplayerFoundation-0.1.0-dev-rw1.6.zip`; HTTP 200 und SHA-256 gegen lokale Datei bestätigt.
- Risiko: Spiel-UI und zwei tatsächliche RimWorld-Clients bleiben ungeprüft; nur kontrolliertes LAN/Testnetz, keine 1.7-Zusage.
- Menschliche Prüfung: Download, Entpacken und Aktivieren in RimWorld 1.6; keine Freigabe oder Merge dokumentiert.

**Nachtrag 2026-10-15:** Die spätere ausdrückliche Merge-Freigabe und die Integration in `main` sind in `docs/reviews/2026-10-15-dual-mode-merge.md` dokumentiert. Die obigen Angaben beschreiben den vorherigen Review-Stand.
