# RimWorld Multiplayer Foundation 0.1.0-dev installieren

Dieses Testpaket ist für **RimWorld 1.6** gebaut. Die Mod-Oberfläche im laufenden Spiel ist noch nicht verifiziert. Es gibt in dieser Version Verbindung und Lobby, **kein gemeinsames Gameplay**.

1. RimWorld schließen.
2. Dieses ZIP direkt in den Ordner `RimWorld/Mods/` entpacken. Danach muss `RimWorld/Mods/RimWorldMultiplayerFoundation/About/About.xml` vorhanden sein. Keinen zusätzlichen Zwischenordner anlegen.
3. RimWorld starten und `RimWorld Multiplayer Foundation` im Mod-Menü aktivieren. Nach Aufforderung neu starten.
4. Unter `Optionen → Mod-Einstellungen → Multiplayer Foundation` sind zwei Wege vorgesehen:
   - **Ohne separaten Server:** Ein Spieler trägt bei **Host-IP** die eigene LAN-IP ein und klickt **Lobby direkt hosten**. Andere Spieler geben diese IP bei **Server** ein und klicken **Verbinden**, danach **Beitreten**. Standard-Host-IP `127.0.0.1` funktioniert nur auf demselben Rechner. Port ist standardmäßig TCP `28491`.
   - **Mit dediziertem Server:** Den Server separat starten. Alle Spieler geben seine Adresse und den Port ein, verbinden sich; einer erstellt eine Lobby, die anderen treten bei.
5. Zum Beenden einer selbst gehosteten Lobby **Trennen** wählen. Dadurch stoppt auch der integrierte Listener und alle Gäste verlieren die Verbindung.

## Steam-Vorschau (nur im entsprechend benannten Test-ZIP)

Die Vorschau `*-steam-preview-rw1.6.zip` ist ein noch nicht im Spiel verifizierter Arbeitsstand. Beide Spieler benötigen RimWorld 1.6 über Steam und die gleiche Vorschau-Modversion. Beide starten RimWorld, aktivieren die Mod und öffnen **Optionen → Mod-Einstellungen → Multiplayer Foundation**. Der Host klickt **Über Steam hosten** und danach **Steam-Freunde einladen**. Der Gast nimmt die Steam-Einladung bei laufendem Spiel an und klickt in den Mod-Einstellungen **Steam-Einladung annehmen**. Danach sollte der Beitritt zur Foundation-Lobby automatisch erfolgen. **Bereit** und **Trennen** können anschließend geprüft werden. Das Steam-Verfahren ist für getrennte Heimanschlüsse vorgesehen und braucht keinen eingehenden TCP-Port am Heimrouter. Ein Beitritt durch Kaltstart des Spiels via Einladung ist noch nicht implementiert.

Wenn die Steam-Schaltfläche nicht erscheint, **Steam prüfen** anklicken und den angezeigten Status notieren. Für einen Fehlerbericht sind RimWorld-Version, Mod-ZIP-Name, Statusanzeige und Logs beider Spieler hilfreich. Der Steam-Pfad ist bislang nur gebaut und mit einem ersetzten Duplex-Stream getestet; eine funktionierende Steam-Verbindung ist noch nicht nachgewiesen. Das reguläre `0.1.0-dev-rw1.6.zip` enthält den Steam-Pfad nicht.

Typische Mod-Pfade: unter Linux der `Mods`-Ordner der verwendeten Steam-RimWorld-Installation, unter Windows meist `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods`. Entscheidend ist der tatsächliche RimWorld-Installationsordner.

Der dedizierte Server ist **nicht** in diesem ZIP enthalten. Für LAN-Verbindungen muss TCP-Port `28491` auf dem Hostrechner erreichbar sein. Weder integrierter noch dedizierter Host bieten derzeit Anmeldung oder Transportverschlüsselung; nur in einem kontrollierten Testnetz verwenden. RimWorld 1.7 und andere Mods sind nicht als kompatibel geprüft.
