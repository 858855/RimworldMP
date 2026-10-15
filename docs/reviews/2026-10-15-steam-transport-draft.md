# Review-Entwurf: Steam-Freundeslobby und P2P-Transport

- **Aufgabe:** `STEAM-001`; Quellbranch `feat/steam-friends-p2p`, Zielbranch `main` nach späterer Freigabe.
- **Änderung:** Zusätzlicher Steam-Transport für den vorhandenen Foundation-Handshake und Lobby-Server; Einladungs- und Host-Bedienung in den Mod-Einstellungen. Der eingebettete TCP-Listener bindet im Steam-Modus nur an Loopback. Der dedizierte TCP-Server bleibt erhalten.
- **Betroffene Komponenten:** `Network`, `Hosting`, RimWorld-Adapter, Hosting-Tests und zugehörige Dokumentation.
- **Prüfung:** Release-Build null Warnungen; drei Netzwerk-, fünf Hosting- und 16 Serverintegrationsgruppen bestanden. Gesondertes Vorschau-ZIP mit sieben Einträgen und SHA-256 geprüft. Details im [Prüfbericht](../verification/2026-10-15-steam-transport.md).
- **Offen vor Integrationsfreigabe:** Zwei Steam-Konten und zwei Heimanschlüsse, RimWorld-UI und Overlay, Einladung, Verbindung, Lobby-Beitritt, Ready, Trennen und wechselnder Host. Der native Steam-Pfad wurde noch nicht zur Laufzeit geprüft.
- **Status:** Entwurf, `not_submitted`. Keine menschliche Freigabe und kein Merge. Das Vorschau-ZIP wurde nicht auf GitHub veröffentlicht.
