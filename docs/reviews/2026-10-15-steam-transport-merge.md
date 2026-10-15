# Review und Freigabe: Steam-Freundeslobby und P2P-Transport

- **Aufgabe:** `STEAM-001`; Quellbranch `feat/steam-friends-p2p`, Zielbranch `main`, Ausgang `5b533cd`.
- **Branch-Commits:** `706a10c` (Design), `e837870` (Steam-Vorschau), `897492b` (lokale Installation), `bc9431a` (Review/Validator).
- **Menschliche Freigabe:** Der Benutzer schrieb am 2026-10-15 ausdrücklich „merge das“ für den aktuellen Steam-Branch. Die noch offenen Steam-Laufzeittests waren in der vorherigen Antwort genannt. Die Freigabe gilt für den lokalen Merge nach `main`, nicht für einen Push.
- **Änderung:** Zusätzlicher Steam-Transport für den vorhandenen Foundation-Handshake und Lobby-Server; Einladungs- und Host-Bedienung in den Mod-Einstellungen. Der eingebettete TCP-Listener bindet im Steam-Modus nur an Loopback. Der dedizierte TCP-Server bleibt erhalten.
- **Betroffene Komponenten:** `Network`, `Hosting`, RimWorld-Adapter, Hosting-Tests und zugehörige Dokumentation.
- **Prüfung:** Release-Build null Warnungen; drei Netzwerk-, fünf Hosting- und 16 Serverintegrationsgruppen vor dem Merge erneut bestanden. Gesondertes Vorschau-ZIP mit sieben Einträgen und SHA-256 geprüft. YAML-Validator einschließlich doppelter Schlüssel geprüft. Details im [Prüfbericht](../verification/2026-10-15-steam-transport.md).
- **Weiter offen:** Zwei Steam-Konten und zwei Heimanschlüsse, RimWorld-UI und Overlay, Einladung, Verbindung, Lobby-Beitritt, Ready, Trennen und wechselnder Host. Der native Steam-Pfad wurde noch nicht zur Laufzeit geprüft. Das Vorschau-ZIP darf nicht als verifizierter Internetbetrieb dargestellt werden.
- **Manuelle Prüfung:** Beide Spieler nutzen dasselbe Vorschau-ZIP und RimWorld 1.6; Host und Gast testen in beide Richtungen Steam-Einladung, Beitritt, Ready und Disconnect. Fehlerstatus und Logs beider Spielclients erfassen.
- **Integration:** Lokaler Merge-Commit `7bf6b42` (`merge: integrate Steam P2P preview`) auf `main`, ohne Konflikte. `origin/main` blieb unverändert; kein Push freigegeben.
- **Status:** `merged` als Git-Integrationsstatus. `STEAM-001` bleibt technisch `in_progress` und nur `build_passed`, bis der Steam-Laufzeittest vorliegt.
