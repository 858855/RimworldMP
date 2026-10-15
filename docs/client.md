# RimWorld-Client 0.1

**Ziel:** lokal installierter RimWorld-1.6-Build (Unity/Mono). `Version.txt` meldet `1.6.4871 rev598`, der Spielprozess `rev600`; die genaue Runtime-Kompatibilität ist offen. Der Adapter zielt auf `netstandard2.1`, weil die lokale `Assembly-CSharp.dll` und Unity-Module `netstandard` 2.1 referenzieren. Das wurde durch einen Probe-Build und den Release-Mod-Build geprüft. Andere Spielversionen sind nicht getestet.

## Bedienung

Im Arbeitsbranch `feat/main-menu-chat` ergänzt ein **Multiplayer**-Knopf direkt nach **Tutorial** in der nativen Hauptmenü-Spalte ein eigenes Verbindungsfenster. Ein kleiner Harmony-Prefix fügt die Option in RimWorlds vorhandene Liste ein; das Spiel zeichnet sie mit derselben Farbe und demselben Abstand wie die übrigen Knöpfe. Der Benutzer bestätigte Position und Fensteröffnung im Spiel; die übrige Bedienung ist noch zu prüfen. Über **Optionen → Mod-Einstellungen → Multiplayer Foundation → Multiplayer öffnen** bleibt ein Ausweichweg. Im Verbindungsfenster stehen Steam-Einladungen und Steam-Hosting zuerst, darunter IP/TCP-Verbindung und Direkt-Hosting. Serveradresse, Port, Bind-Adresse und Spielername bleiben als Mod-Einstellungen gespeichert.

Mit **Per IP verbinden** wird ein dedizierter Server oder ein direkt hostender Spieler erreicht. **Direkt hosten** startet den lokalen autoritativen TCP-Listener. Die Host-Bind-IP ist standardmäßig `::` (alle Schnittstellen, IPv4 und IPv6). Optional öffnet die Mod per UPnP den Router-Port und zeigt nach dem Hosten die Adressen für Mitspieler an (Internet-IPv4, LAN, VPN, globale IPv6) samt **Adresse kopieren**. Das Server-Feld akzeptiert IP, Hostname, `host:port` und `[IPv6]:port`. Ein Gast trägt die angezeigte Adresse ein und klickt nach dem Verbinden auf **Beitreten**; Details in ADR 0012 und `mod/INSTALLATION.md`. Beim Steam-Hosting entfällt das alles. Danach erscheinen Spielerliste, Host, Ready und Verlassen/Schließen. Rechts ist ein flüchtiger Lobby-Chat mit maximal 50 angezeigten Nachrichten. Beim **Trennen** des direkten Hosts stoppt auch sein Listener.

## Version 0.2 (Vorschau, Branch `feat/lobby-hardening`)

- **Mod-Prüfung:** Die Mod prüft beim Start im Hintergrund Spielversion, aktive Mods, Ladereihenfolge und Mod-Inhalte (Log: „Multiplayer Foundation: N Mods geprüft, Prüfsumme xxxx“). In der Lobby steht hinter jedem Spieler „Mods passen“, „Mods abweichend“ (rot) oder „Mod-Prüfung läuft …“. Bei Abweichung ist **Bereit** gesperrt und **Unterschiede anzeigen** öffnet einen Bericht: fehlende, zusätzliche und geänderte Mods, falsche Reihenfolge, Spielversion. Die Lobbyliste zeigt die Mod-Anzahl des Hosts und „passt“/„abweichend“; der Tooltip nennt Spielversionen und Prüfsummen.
- **Spielerschlüssel:** Die Mod meldet sich mit der SteamID an, ohne Steam mit einer einmal erzeugten, gespeicherten GUID.
- **Spiel starten:** Der Host sieht **Spiel starten**; daneben steht der erste fehlende Grund (zu wenige Spieler, Mods abweichend, nicht alle bereit, nicht im Hauptmenü). Nach Bestätigung ist die Lobby gesperrt. Die gemeinsamen Einrichtungsseiten folgen mit SETUP-001; bis dahin zeigt die Mod eine Einrichtungsansicht, in der der Host nur **Einrichtung abbrechen** kann. Es wird kein Spiel gestartet.
- Wire-Version 3: Client, Host und Dedicated Server brauchen denselben Stand.

`LobbyClient` liest Nachrichten im Hintergrund und beantwortet Server-Pings. Die UI verarbeitet empfangene Ereignisse nur auf dem Spielthread. Lobby-Snapshots mit älterer Revision werden ignoriert. Gameplay-Daten werden nicht gelesen oder synchronisiert.

## Steam-Freunde-Modus als lokale Vorschau

Der nach `main` gemergte Branch `feat/steam-friends-p2p` ergänzt Steam-Hosting und Einladungen. Der Host startet einen nur an Loopback gebundenen Foundation-Server, eröffnet eine Steam-Freundeslobby und lädt über das Steam-Overlay ein. Der Gast nimmt die Einladung im laufenden RimWorld an, öffnet in der neuen Vorschau **Multiplayer** und klickt **Einladung annehmen**. Danach stellt die Mod eine Steam-P2P-Verbindung zum Lobby-Besitzer her, führt den Foundation-Handshake aus und tritt der einzigen Foundation-Lobby automatisch bei. Beide Spieler können in einer neuen Sitzung die Host-Rolle übernehmen. Der Dedicated Server und die manuelle TCP-Verbindung bleiben verfügbar.

Der Mod verwendet die Steamworks.NET-Assembly der lokal installierten RimWorld-Version; das Paket kopiert sie nicht. Die Steam-Test-App-ID `480` wird nicht verwendet. Ein automatischer Beitritt bei Spielstart aus einer Einladung heraus ist noch nicht implementiert. Der Steam-Pfad hat einen fehlerfreien Release-Build und einen spielunabhängigen Stream-/Lobby-Test. Der Benutzer meldet eine erfolgreiche Steam-Verbindung; **Testablauf, Netzwerke und Lobby-Aktionen sind noch nicht dokumentiert**. Das reguläre historische ZIP enthält den Steam-Modus nicht; die Steam-Vorschau ist seit `dfe9d23` in `origin/main`. Die neue Menü-/Chat-Vorschau ist ein gesonderter, noch nicht gemergter Branch und verlangt auf Client und Server Wire-Version 2. Siehe [ADR 0010](decisions/0010-steam-home-host.md), [Prüfbericht](verification/2026-10-15-steam-transport.md) und [Nutzerbericht](verification/2026-10-15-steam-user-connection.md).

## Bauen und installieren

```bash
DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh
```

Das Skript erzeugt `dist/RimWorldMultiplayerFoundation/` mit `About/About.xml`, fünf eigenen DLLs und der gepinnten Harmony-Assembly samt Lizenz. Für andere Installationen `RIMWORLD_MANAGED` auf das lokale `RimWorldLinux_Data/Managed`-Verzeichnis setzen. Die neue [Vorschau](../releases/RimWorldMultiplayerFoundation-0.1.0-dev-menu-chat-preview-rw1.6.zip) direkt in RimWorld `Mods/` entpacken und im Mod-Menü aktivieren. Das ältere [Basis-ZIP](../releases/RimWorldMultiplayerFoundation-0.1.0-dev-rw1.6.zip) bleibt zur historischen Nachvollziehbarkeit erhalten, ist aber mit Wire-Version 2 nicht kompatibel. Der Archivordner enthält `About/`, `Assemblies/`, Lizenz und [Anleitung](../mod/INSTALLATION.md). Das Paket enthält keine proprietären Spiel-DLLs und keinen Server.

## Verifizierungsgrenze

Release-Build und Paketstruktur bestanden. Die spielunabhängige Client-Pumpe wurde gegen den echten TCP-Server getestet. Der integrierte Listen-Host wurde mit Gastbeitritt, Ready und Stopp des Hosts über echte TCP-Verbindungen geprüft. Der Spielstart gelang in dieser Ausführungsumgebung nicht bis zur UI: `-nographics` führte zu Atlasfehlern des Null-Grafikgeräts; grafischer Start blieb in der Unity-Initialisierung und wurde nach 60 s beendet. Eine visuelle und funktionale RimWorld-UI-Prüfung bleibt offen. Die normale Benutzer-Modliste wurde nicht verändert; der temporäre Test-Symlink wurde entfernt. Siehe `docs/verification/2026-10-15-client.md`.
