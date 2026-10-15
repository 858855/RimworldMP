# RimWorld Multiplayer Foundation 0.1.0-dev installieren

Dieses Testpaket ist für **RimWorld 1.6** gebaut. Die Mod-Oberfläche im laufenden Spiel ist noch nicht verifiziert. Es gibt in dieser Version Verbindung und Lobby, **kein gemeinsames Gameplay**.

1. RimWorld schließen.
2. Dieses ZIP direkt in den Ordner `RimWorld/Mods/` entpacken. Danach muss `RimWorld/Mods/RimWorldMultiplayerFoundation/About/About.xml` vorhanden sein. Keinen zusätzlichen Zwischenordner anlegen. Eine ältere Version vorher löschen.
3. RimWorld starten und `RimWorld Multiplayer Foundation` im Mod-Menü aktivieren. Nach Aufforderung neu starten.
4. Unter `Optionen → Mod-Einstellungen → Multiplayer Foundation` sind zwei Wege vorgesehen:
   - **Ohne separaten Server:** Ein Spieler lässt **Host-IP** auf `::` (alle Netzwerkschnittstellen, IPv4 und IPv6) und klickt **Lobby direkt hosten**. Die Mod zeigt danach die Adressen an, die Mitspieler eintragen: **Internet (IPv4)** für Spieler außerhalb, **LAN** im selben Netz, **VPN** bei Tailscale/ZeroTier. **Adresse kopieren** legt die passende Adresse in die Zwischenablage. Mitspieler tragen sie bei **Server** ein (auch `ip:port`, `[IPv6]:port` oder ein Hostname), klicken **Verbinden** und danach **Beitreten**. `127.0.0.1` als Host-IP funktioniert nur auf demselben Rechner.
   - **Mit dediziertem Server:** Den Server separat starten. Alle Spieler geben seine Adresse und den Port ein, verbinden sich; einer erstellt eine Lobby, die anderen treten bei.
5. Zum Beenden einer selbst gehosteten Lobby **Trennen** wählen. Dadurch stoppt auch der integrierte Listener, die UPnP-Portfreigabe wird entfernt und alle Gäste verlieren die Verbindung.

## Spielen über das Internet

- **Router:** Mit aktivierter Option **Port am Router automatisch öffnen (UPnP)** gibt die Mod TCP-Port `28491` selbst frei. Bei einer FRITZ!Box muss dafür unter *Internet → Freigaben → Gerät bearbeiten* „Selbstständige Portfreigaben erlauben“ für den Host-PC aktiviert sein. Ohne UPnP den TCP-Port im Router manuell an die LAN-IP des Host-PCs weiterleiten.
- **Windows-Firewall:** Beim ersten Hosten fragt Windows nach Netzwerkzugriff für RimWorld. Mit **Zulassen** bestätigen, sonst bleibt der Port blockiert.
- **Keine öffentliche IPv4 (CGNAT/DS-Lite):** Meldet die Mod „Internet (IPv4) nicht möglich“, kann kein Router-Port helfen. Dann über die angezeigte IPv6-Adresse verbinden (Router-Firewall muss den Port für IPv6 freigeben) oder ein VPN wie Tailscale oder ZeroTier nutzen und die angezeigte VPN-Adresse weitergeben.
- Wird eine Lobby nur durch Abstürzen beendet, bleibt die UPnP-Freigabe im Router bestehen und kann dort gelöscht werden.

Typische Mod-Pfade: unter Linux der `Mods`-Ordner der verwendeten Steam-RimWorld-Installation, unter Windows meist `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods`. Entscheidend ist der tatsächliche RimWorld-Installationsordner.

Der dedizierte Server ist **nicht** in diesem ZIP enthalten. Weder integrierter noch dedizierter Host bieten derzeit Anmeldung oder Transportverschlüsselung: Wer Adresse und Port kennt, kann sich verbinden. Adresse nur an Mitspieler weitergeben. RimWorld 1.7 und andere Mods sind nicht als kompatibel geprüft.
