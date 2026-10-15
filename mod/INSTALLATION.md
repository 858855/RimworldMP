# RimWorld Multiplayer Foundation 0.1.0-dev installieren

Dieses Testpaket ist für **RimWorld 1.6** gebaut. Die Mod-Oberfläche im laufenden Spiel ist noch nicht verifiziert. Es gibt in dieser Version nur Verbindung und Lobby, **kein gemeinsames Gameplay**.

1. RimWorld schließen.
2. Dieses ZIP direkt in den Ordner `RimWorld/Mods/` entpacken. Danach muss `RimWorld/Mods/RimWorldMultiplayerFoundation/About/About.xml` vorhanden sein. Keinen zusätzlichen Zwischenordner anlegen.
3. RimWorld starten und `RimWorld Multiplayer Foundation` im Mod-Menü aktivieren. Nach Aufforderung neu starten.
4. Unter `Optionen → Mod-Einstellungen → Multiplayer Foundation` Serveradresse, Port und Spielernamen eingeben. Standard: `127.0.0.1:28491` für einen Server auf demselben Rechner.
5. Der dedizierte Server ist **nicht** in diesem ZIP enthalten. Für Spieler auf anderen Rechnern muss der Server separat gestartet und auf eine passende LAN-Adresse konfiguriert werden. Die Mod-Einstellungen verwenden dann die LAN-Adresse dieses Servers.

Typische Mod-Pfade: unter Linux der `Mods`-Ordner der verwendeten Steam-RimWorld-Installation, unter Windows meist `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods`. Entscheidend ist der tatsächliche RimWorld-Installationsordner.

Der Server besitzt derzeit weder Anmeldung noch Transportverschlüsselung. Nur in einem kontrollierten Testnetz verwenden. RimWorld 1.7 und andere Mods sind nicht als kompatibel geprüft.
