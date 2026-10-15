# Vorabprüfung: Hauptmenü-Einstieg und Lobby-Chat

- **Aufgaben:** `UI-002`, `CHAT-001`; Quellbranch `feat/main-menu-chat`, Zielbranch `main`, Ausgangscommit `dfe9d23`. Der Quellbranch ist noch lokal; kein Merge/Push für diese Änderung freigegeben.
- **Commits:** nach dem Feature-Commit anhand `git log main..feat/main-menu-chat --oneline` eintragen.
- **Änderung:** RimWorld übernimmt einen „Multiplayer“-Eintrag direkt nach „Tutorial“ in seine native Optionsliste. Das größere Fenster stellt Steam-Einladungen vor IP/TCP und zeigt den Lobby-Chat rechts. Der Dedicated Server verteilt kurze Meldungen nur innerhalb der jeweiligen Lobby; Wire-Version 2 schützt vor gemischten Alt-/Neuständen.
- **Betroffene Komponenten:** Protocol, Lobby, Hosting/Dedicated Server, RimWorld-Adapter, Mod-Paket, Protokoll-/Client-Dokumentation und Tests. Harmony 2.4.3 ist als zusätzliche Runtime-Abhängigkeit mit MIT-Lizenz im ZIP enthalten.
- **Technische Entscheidung:** Ein schmaler Harmony-Prefix ergänzt die native `ListableOption`-Liste, ohne die Hauptmenü-Methode zu ersetzen. Grund und Alternativen: `docs/decisions/0011-main-menu-and-lobby-chat.md`.
- **Prüfung:** Release-Mod-Build null Warnungen, Server-Build, sechs Protokoll-, 18 Serverintegrations-, fünf Hosting-, drei Client-, drei Netzwerk- und acht Mehrprozess-Schritte bestanden. ZIP und installierter Mod sind bytegleich. Details: `docs/verification/2026-10-15-menu-chat.md`.
- **Bekannte Risiken:** Menüeintrag direkt unter Tutorial und Fensteröffnung wurden vom Benutzer im Spiel bestätigt. Fensterlayout, Steam-/IP-Bedienung und Chat mit zwei Spielclients sind noch ungeprüft. Gemischte Wire-Versionen funktionieren nicht. Direkt-TCP bleibt ohne Anmeldung/TLS.
- **Manuell prüfen:** Steam oben und IP darunter, zwei gleiche Mod-Builds verbinden, Lobby erstellen/beitreten, Chat beidseitig sehen, Ready/Disconnect prüfen. Bei Steam zusätzlich Einladung über zwei Konten und getrennte Heimanschlüsse prüfen. Screenshots und `Player.log` festhalten.
- **Status:** `not_submitted`; weitere grafische Funktionsprüfung offen. Keine menschliche Merge-Freigabe für diesen Branch dokumentiert.
