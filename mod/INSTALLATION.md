# RimWorld Multiplayer Foundation 0.1.0-dev installieren

Diese Vorschau ist für **RimWorld 1.6** gebaut. Sie bietet Verbindung, Lobby und einen flüchtigen Lobby-Chat, aber **kein gemeinsames Gameplay**. Der Benutzer bestätigte den Hauptmenü-Knopf und die Fensteröffnung im Spiel; die Chat-Bedienung steht noch aus.

1. RimWorld schließen. Beide Spieler benötigen **dasselbe Vorschau-ZIP**; der dedizierte Server muss aus demselben Branch gebaut sein. Die Chat-Vorschau verwendet Wire-Version 2 und ist nicht mit älteren Version-1-Mods/Servern kombinierbar.
2. ZIP direkt in `RimWorld/Mods/` entpacken. Danach muss `RimWorld/Mods/RimWorldMultiplayerFoundation/About/About.xml` vorhanden sein. Einen alten `RimWorldMultiplayerFoundation`-Ordner vorher sichern oder entfernen, damit keine alten DLLs verbleiben.
3. RimWorld starten und den Mod im Mod-Menü aktivieren; nach Aufforderung neu starten.
4. Im Hauptmenü **Multiplayer** wählen. Falls der neue Knopf im Spiel nicht erscheint, **Optionen → Mod-Einstellungen → Multiplayer Foundation → Multiplayer öffnen** verwenden und den Fehler melden.

## Mit Steam-Freunden

Der Host gibt seinen Namen, Lobbyname und Platzzahl ein und wählt **Steam-Lobby hosten**. In der Lobby klickt er **Steam-Freunde einladen**. Der Gast nimmt die Einladung im laufenden RimWorld an, öffnet **Multiplayer** und wählt **Einladung annehmen**. Danach sollte er automatisch in der Foundation-Lobby erscheinen. Im Lobbyfenster stehen Spieler links und Chat rechts. Steam benötigt keine Router-Portfreigabe. Der Benutzer hat eine Steam-Verbindung mit der vorherigen Vorschau sowie Menüposition und Fensteröffnung mit der aktuellen Vorschau gemeldet; Chat benötigt noch einen echten Spieltest.

## Per IP oder dediziertem Server

Gäste geben unter **Per IP verbinden** die erreichbare IP/Hostadresse und den Port ein. Ein Spieler kann mit **Direkt hosten** den integrierten Lobby-Server starten; alternativ läuft der dedizierte Server separat. Standardport ist TCP `28491`. Verbindungen zwischen Heimanschlüssen brauchen bei direktem TCP-Hosting in der Regel eine Portweiterleitung am Router des Hosts. Beim **Trennen** des direkten Hosts stoppt der integrierte Server.

Der dedizierte Server ist nicht im Mod-ZIP enthalten. `scripts/build-server.sh` und `docs/dedicated-server.md` beschreiben seinen Start. Direkter TCP-Betrieb bietet keine Anmeldung oder Transportverschlüsselung; nur in kontrollierten Testnetzen verwenden. RimWorld 1.7 und Kompatibilität mit anderen Mods sind nicht geprüft.

Bei Fehlern sind RimWorld-Version, ZIP-Name, Statusanzeige und Logs beider Spieler hilfreich.
