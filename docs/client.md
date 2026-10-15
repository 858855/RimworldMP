# RimWorld-Client 0.1

**Ziel:** lokal installierter RimWorld-1.6-Build (Unity/Mono). `Version.txt` meldet `1.6.4871 rev598`, der Spielprozess `rev600`; die genaue Runtime-Kompatibilität ist offen. Der Adapter zielt auf `netstandard2.1`, weil die lokale `Assembly-CSharp.dll` und Unity-Module `netstandard` 2.1 referenzieren. Das wurde durch einen Probe-Build und den Release-Mod-Build geprüft. Andere Spielversionen sind nicht getestet.

## Bedienung

Nach Aktivierung des Mods in RimWorld unter **Optionen → Mod-Einstellungen → Multiplayer Foundation** öffnen. Das ist der geprüfte Kompilier-Einstieg `Verse.Mod`; ein Hauptmenü-Patch ist derzeit nicht nötig. Serveradresse, Port und Spielername sind editierbar und werden als Mod-Einstellungen gespeichert. Mit **Verbinden** wird eine bestehende Lobby-Instanz erreicht: entweder ein dedizierter Server oder ein Spieler, der direkt hostet. Mit **Lobby direkt hosten** startet der lokale Client selbst den autoritativen TCP-Listener und erstellt eine Lobby. Der Kollege verbindet sich mit der LAN-IP des Hosts und klickt **Beitreten**. Danach erscheinen Spielerliste, Host, Ready und Verlassen/Schließen. Beim **Trennen** des direkten Hosts stoppt auch sein Listener. **Spielstart nicht verfügbar** ist ausdrücklich angezeigt. Ein Button **Trennen** beendet die Sitzung; danach kann erneut verbunden werden.

`LobbyClient` liest Nachrichten im Hintergrund und beantwortet Server-Pings. Die UI verarbeitet empfangene Ereignisse nur auf dem Spielthread. Lobby-Snapshots mit älterer Revision werden ignoriert. Gameplay-Daten werden nicht gelesen oder synchronisiert.

## Bauen und installieren

```bash
DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh
```

Das Skript erzeugt `dist/RimWorldMultiplayerFoundation/` mit `About/About.xml` und fünf eigenen DLLs in `Assemblies/` (Protocol, Network, Lobby, Hosting, RimWorld). Für andere Installationen `RIMWORLD_MANAGED` auf das lokale `RimWorldLinux_Data/Managed`-Verzeichnis setzen. Zum manuellen Installieren das [ZIP](../releases/RimWorldMultiplayerFoundation-0.1.0-dev-rw1.6.zip) direkt in RimWorld `Mods/` entpacken und im Mod-Menü aktivieren. Der Archivordner enthält `About/`, `Assemblies/` und eine [Anleitung](../mod/INSTALLATION.md). Das Paket enthält keine proprietären Spiel-DLLs und keinen Server.

## Verifizierungsgrenze

Release-Build und Paketstruktur bestanden. Die spielunabhängige Client-Pumpe wurde gegen den echten TCP-Server getestet. Der integrierte Listen-Host wurde mit Gastbeitritt, Ready und Stopp des Hosts über echte TCP-Verbindungen geprüft. Der Spielstart gelang in dieser Ausführungsumgebung nicht bis zur UI: `-nographics` führte zu Atlasfehlern des Null-Grafikgeräts; grafischer Start blieb in der Unity-Initialisierung und wurde nach 60 s beendet. Eine visuelle und funktionale RimWorld-UI-Prüfung bleibt offen. Die normale Benutzer-Modliste wurde nicht verändert; der temporäre Test-Symlink wurde entfernt. Siehe `docs/verification/2026-10-15-client.md`.
