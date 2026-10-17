# RimWorld Multiplayer Foundation 0.3.0-alpha.1 installieren

Diese Alpha ist für **RimWorld 1.6** gebaut (geprüft gegen 1.6.4871). Sie enthält Lobby (Steam, IP, dedizierter Server) mit Mod-Prüfung und Chat, den gemeinsamen Spielstart mit Weltübertragung, Startort und Figurenwahl, Lockstep-Zeit mit Koordinator (gemeinsame Pause, Tempo, Tempoanpassung bei langsamer Verbindung, Hash-Prüfung alle 60 Ticks, Speichern an der Tickgrenze, Beitritt und Resync), die gemeinsamen Befehle Zeichnen/Verbieten/Gehen, Steuerungsrechte je übernommener Figur, das Fortsetzen eines Spielstands aus der Lobby (Coop) sowie die Sync-API `Multiplayer.Foundation.Api` für andere Mods. Der Fraktionsmodus (eigene Kolonie je Spieler) ist enthalten, aber serverseitig gesperrt.

**Alpha-Stand:** Jede Funktion ist gebaut und im Selbsttest eines einzelnen Spielprozesses geprüft (`docs/verification/2026-10-17-alpha-release.md`); **kein Zwei-Client-Spieltest** dieses Stands. Alle anderen Spieleraktionen (Bauen, Zonen, Aufträge, Forschung, Handel …) laufen noch lokal und werden nicht übertragen; sie können Abweichungen auslösen, die die Hash-Prüfung meldet. Spielstände regelmäßig sichern.

1. RimWorld schließen. Beide Spieler benötigen **dasselbe Vorschau-ZIP**; der dedizierte Server muss aus demselben Branch gebaut sein. Die Chat-Vorschau verwendet Wire-Version 2 und ist nicht mit älteren Version-1-Mods/Servern kombinierbar.
2. ZIP direkt in `RimWorld/Mods/` entpacken. Danach muss `RimWorld/Mods/RimWorldMultiplayerFoundation/About/About.xml` vorhanden sein. Einen alten `RimWorldMultiplayerFoundation`-Ordner vorher sichern oder entfernen, damit keine alten DLLs verbleiben.
3. RimWorld starten und den Mod im Mod-Menü aktivieren; nach Aufforderung neu starten.
4. Im Hauptmenü **Multiplayer** wählen. Falls der neue Knopf im Spiel nicht erscheint, **Optionen → Mod-Einstellungen → Multiplayer Foundation → Multiplayer öffnen** verwenden und den Fehler melden.

## Spielen über das Internet

- **Router:** Mit aktivierter Option **Port am Router automatisch öffnen (UPnP)** gibt die Mod TCP-Port `28491` selbst frei. Bei einer FRITZ!Box muss dafür unter *Internet → Freigaben → Gerät bearbeiten* „Selbstständige Portfreigaben erlauben“ für den Host-PC aktiviert sein. Ohne UPnP den TCP-Port im Router manuell an die LAN-IP des Host-PCs weiterleiten.
- **Windows-Firewall:** Beim ersten Hosten fragt Windows nach Netzwerkzugriff für RimWorld. Mit **Zulassen** bestätigen, sonst bleibt der Port blockiert.
- **Keine öffentliche IPv4 (CGNAT/DS-Lite):** Meldet die Mod „Internet (IPv4) nicht möglich“, kann kein Router-Port helfen. Dann über die angezeigte IPv6-Adresse verbinden (Router-Firewall muss den Port für IPv6 freigeben) oder ein VPN wie Tailscale oder ZeroTier nutzen und die angezeigte VPN-Adresse weitergeben.
- Wird eine Lobby nur durch Abstürzen beendet, bleibt die UPnP-Freigabe im Router bestehen und kann dort gelöscht werden.

## Mit Steam-Freunden

Der Host gibt seinen Namen, Lobbyname und Platzzahl ein und wählt **Steam-Lobby hosten**. In der Lobby klickt er **Steam-Freunde einladen**. Der Gast nimmt die Einladung im laufenden RimWorld an, öffnet **Multiplayer** und wählt **Einladung annehmen**. Danach sollte er automatisch in der Foundation-Lobby erscheinen. Im Lobbyfenster stehen Spieler links und Chat rechts. Steam benötigt keine Router-Portfreigabe. Der Benutzer hat eine Steam-Verbindung mit der vorherigen Vorschau sowie Menüposition und Fensteröffnung mit der aktuellen Vorschau gemeldet; Chat benötigt noch einen echten Spieltest.

## Per IP oder dediziertem Server

Gäste geben unter **Per IP verbinden** die erreichbare IP/Hostadresse und den Port ein. Ein Spieler kann mit **Direkt hosten** den integrierten Lobby-Server starten; alternativ läuft der dedizierte Server separat. Standardport ist TCP `28491`. Die Host-Bind-IP `::` lässt LAN und Internet zu, `127.0.0.1` nur denselben Rechner. Nach dem Hosten zeigt die Mod die Adressen für Mitspieler an; **Adresse kopieren** legt die passende in die Zwischenablage. Für Spieler außerhalb muss der Port am Router des Hosts freigegeben sein – automatisch per UPnP oder manuell, siehe „Spielen über das Internet“. Beim **Trennen** des direkten Hosts stoppen der integrierte Server und die UPnP-Freigabe.

Der dedizierte Server ist nicht im Mod-ZIP enthalten. `scripts/build-server.sh` und `docs/dedicated-server.md` beschreiben seinen Start. Direkter TCP-Betrieb bietet keine Anmeldung oder Transportverschlüsselung: Wer Adresse und Port kennt, kann sich verbinden. Adresse nur an Mitspieler weitergeben. RimWorld 1.7 und Kompatibilität mit anderen Mods sind nicht geprüft.

Bei Fehlern sind RimWorld-Version, ZIP-Name, Statusanzeige und Logs beider Spieler hilfreich.
