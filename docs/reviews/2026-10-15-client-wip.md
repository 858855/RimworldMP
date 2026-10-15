# Review-Zwischenstand: NET-001 / CLIENT-001

- **Branch/Ziel:** `feat/rimworld-client` (Basis `feat/dedicated-server`/`61ee7fb`) → später `main` nur nach menschlicher Freigabe.
- **Änderung:** Client-Nachrichtenpumpe mit Pong/Queue sowie 1.6-Mod mit Verbindungs-, Lobby- und Ready-Oberfläche in den Mod-Einstellungen. Installierbares Paket in `dist/` baubar.
- **Nachweis:** Release-Build, Paketprüfung und drei TCP-Client-Testgruppen bestanden; `docs/verification/2026-10-15-client.md`.
- **Risiko/offen:** Die UI konnte im Spiel nicht geladen oder bedient werden. Grafikloser Test erzeugt Unity-Atlasfehler; grafischer Prozess blieb beim Start hängen. Keine 1.7-Zusage.
- **Manuelle Prüfung:** Mod mit isolierter Modliste grafisch aktivieren, Optionen/Mod-Einstellungen öffnen, zwei Spielclients gegen Server verbinden, Lobby/Ready/Disconnect bedienen. Bis dahin `CLIENT-001` nicht integrationsbereit.
