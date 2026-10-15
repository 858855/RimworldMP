# RimWorld-Client 0.1

**Ziel:** lokal installierter RimWorld-1.6-Build (Unity/Mono). `Version.txt` meldet `1.6.4871 rev598`, der Spielprozess `rev600`; die genaue Runtime-Kompatibilität ist offen. Der Adapter zielt auf `netstandard2.1`, weil die lokale `Assembly-CSharp.dll` und Unity-Module `netstandard` 2.1 referenzieren. Das wurde durch einen Probe-Build und den Release-Mod-Build geprüft. Andere Spielversionen sind nicht getestet.

## Bedienung

Nach Aktivierung des Mods in RimWorld unter **Optionen → Mod-Einstellungen → Multiplayer Foundation** öffnen. Das ist der geprüfte Kompilier-Einstieg `Verse.Mod`; ein Hauptmenü-Patch ist derzeit nicht nötig. Serveradresse, Port und Spielername sind editierbar und werden als Mod-Einstellungen gespeichert. Mit **Verbinden** wird eine bestehende Lobby-Instanz erreicht: entweder ein dedizierter Server oder ein Spieler, der direkt hostet. Mit **Lobby direkt hosten** startet der lokale Client selbst den autoritativen TCP-Listener und erstellt eine Lobby. Der Kollege verbindet sich mit der LAN-IP des Hosts und klickt **Beitreten**. Danach erscheinen Spielerliste, Host, Ready und Verlassen/Schließen. Beim **Trennen** des direkten Hosts stoppt auch sein Listener. **Spielstart nicht verfügbar** ist ausdrücklich angezeigt. Ein Button **Trennen** beendet die Sitzung; danach kann erneut verbunden werden.

`LobbyClient` liest Nachrichten im Hintergrund und beantwortet Server-Pings. Die UI verarbeitet empfangene Ereignisse nur auf dem Spielthread. Lobby-Snapshots mit älterer Revision werden ignoriert. Gameplay-Daten werden nicht gelesen oder synchronisiert.

## Steam-Freunde-Modus als lokale Vorschau

Der lokal nach `main` gemergte Branch `feat/steam-friends-p2p` ergänzt **Über Steam hosten** und **Steam-Freunde einladen**. Der Host startet einen nur an Loopback gebundenen Foundation-Server, eröffnet eine Steam-Freundeslobby und lädt über das Steam-Overlay ein. Der Gast nimmt die Einladung im laufenden RimWorld an, öffnet die Mod-Einstellungen und klickt **Steam-Einladung annehmen**. Danach stellt die Mod eine Steam-P2P-Verbindung zum Lobby-Besitzer her, führt den bestehenden Foundation-Handshake aus und tritt der einzigen Foundation-Lobby automatisch bei. Beide Spieler können in einer neuen Sitzung die Host-Rolle übernehmen. Der Dedicated Server und die manuelle TCP-Verbindung bleiben verfügbar.

Der Mod verwendet die Steamworks.NET-Assembly der lokal installierten RimWorld-Version; das Paket kopiert sie nicht. Die Steam-Test-App-ID `480` wird nicht verwendet. Ein automatischer Beitritt bei Spielstart aus einer Einladung heraus ist noch nicht implementiert. Der Steam-Pfad hat einen fehlerfreien Release-Build und einen spielunabhängigen Stream-/Lobby-Test. Der Benutzer meldet eine erfolgreiche Steam-Verbindung; **Testablauf, Netzwerke und Lobby-Aktionen sind noch nicht dokumentiert**. Das reguläre ZIP enthält den Steam-Modus nicht; das gesonderte Vorschau-ZIP liegt lokal im Repository und im Spielordner. GitHub wurde nach dem Merge nicht aktualisiert. Siehe [ADR 0010](decisions/0010-steam-home-host.md), [Prüfbericht](verification/2026-10-15-steam-transport.md) und [Nutzerbericht](verification/2026-10-15-steam-user-connection.md).

## Bauen und installieren

```bash
DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh
```

Das Skript erzeugt `dist/RimWorldMultiplayerFoundation/` mit `About/About.xml` und fünf eigenen DLLs in `Assemblies/` (Protocol, Network, Lobby, Hosting, RimWorld). Für andere Installationen `RIMWORLD_MANAGED` auf das lokale `RimWorldLinux_Data/Managed`-Verzeichnis setzen. Zum manuellen Installieren das [ZIP](../releases/RimWorldMultiplayerFoundation-0.1.0-dev-rw1.6.zip) direkt in RimWorld `Mods/` entpacken und im Mod-Menü aktivieren. Der Archivordner enthält `About/`, `Assemblies/` und eine [Anleitung](../mod/INSTALLATION.md). Das Paket enthält keine proprietären Spiel-DLLs und keinen Server.

## Verifizierungsgrenze

Release-Build und Paketstruktur bestanden. Die spielunabhängige Client-Pumpe wurde gegen den echten TCP-Server getestet. Der integrierte Listen-Host wurde mit Gastbeitritt, Ready und Stopp des Hosts über echte TCP-Verbindungen geprüft. Der Spielstart gelang in dieser Ausführungsumgebung nicht bis zur UI: `-nographics` führte zu Atlasfehlern des Null-Grafikgeräts; grafischer Start blieb in der Unity-Initialisierung und wurde nach 60 s beendet. Eine visuelle und funktionale RimWorld-UI-Prüfung bleibt offen. Die normale Benutzer-Modliste wurde nicht verändert; der temporäre Test-Symlink wurde entfernt. Siehe `docs/verification/2026-10-15-client.md`.
