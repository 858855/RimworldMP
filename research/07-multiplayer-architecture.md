# 07 — RimWorld Multiplayer: geprüfte Lockstep-Architektur

**Zielversion:** `rwmt/Multiplayer` **RimWorld 1.6**, nicht 1.7. **Repository:** https://github.com/rwmt/Multiplayer. **Wichtig:** Die aktuell unter `master` einsehbaren Dateien sind dynamische Links, keine Commit-Snapshots; vor jeder Programmänderung taggen/pinnen.

## Support- und Architekturstatus

Die tatsächliche `About.xml` nennt `supportedVersions: 1.6`, `Requires RimWorld >= 1.6.4491` und harte Dependency `zetrith.prepatcher`: https://github.com/rwmt/Multiplayer/blob/master/About/About.xml. Seit Ludeons 1.7-Ankündigung am 12.10.2026 ist Prepatcher ausdrücklich ausgenommen vom Waylith-Kompatibilitätsversprechen. **Keine 1.7-MP-Fähigkeit bestätigt.** Releaseseite https://github.com/rwmt/Multiplayer/releases führt 0.11.5 als regulären 1.6-Stand; Dokumentation/Builds der `continuous`-Variante sind experimentell. Release-Beschreibung und Titel enthalten teils abweichende genaue 1.6-Patchnummern → Ziel-Build im Repo/Spiel prüfen.

## Wesentliches Prinzip: deterministischer Lockstep

1. **Verbindungsphase:** Peers tauschen Protokoll-/Mod-/Spiel-/Def-Zustände aus, um inkompatible Clients abzuweisen. Die historische Beschreibung steht in [Handshake-Process](https://github.com/rwmt/Multiplayer/wiki/Handshake-Process) (2019, deshalb keine aktuellen Paket-IDs oder exakten Reihenfolgen ableiten).
2. **Ausgangszustand:** Host/Server teilt eine gemeinsame Weltsnapshot-/Save-Basis; Joiner übernimmt den Zustand und simuliert nötigenfalls Ticks nach. Einzelheiten des heutigen Rejoinprotokolls nicht vollständig verifiziert.
3. **Eingabe:** Ein Client löst über die Multiplayer-Syncschicht ein *Command* aus, welches zur passenden Simulationszeit einem Context (World/Map) zugeordnet wird.
4. **Verteilung:** Server/Host koordiniert die relevanten Commands und Ticks; **nicht** alle Pawn-Positionen/komplette World-Objekte werden permanent klassisch gestreamt.
5. **Ausführung:** Jeder Peer simuliert die gleichen Spielregeln und RNG-Reihenfolgen auf demselben Zustand. Abweichungen werden als Desync sichtbar.
6. **Auswirkungen:** Die geringere zu übertragende Datenmenge wird durch hohe Anforderungen an *Determinismus* erkauft. Schon eine UI-Mutation außerhalb des Command-Pfads kann den Spielstand auseinanderlaufen lassen.

## Quellcode-Landkarte (tatsächlich eingesehene Ordner/Dateien)

| Komponente | Referenz | Begründeter Befund |
|---|---|---|
| Einstieg/Zustände | [Source/Client/Multiplayer.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Multiplayer.cs) | MP-Client-/Session-/Server-Referenzen, globale Zustandsübergänge, Multiplayer-Ordner für Logs/Desyncs/Replays |
| Session/Game | [Source/Client/MultiplayerGame.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Client/MultiplayerGame.cs) | `MultiplayerGame`, `SyncCoordinator`, `AsyncWorldTimeComp`, `MultiplayerWorldComp`, pro-Map-Komponenten |
| Tick-/Command-Hook | [Source/Client/Patches/TickPatch.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Patches/TickPatch.cs) | patched `TickManager.TickManagerUpdate`, `RunCmds`, `DoUpdate`, `DoTick`, `AllTickables`; **mit aktueller Codezeile `RunCmds` belegt** |
| Getrennte Map-Zeit | [Source/Client/AsyncTime/AsyncTimeComp.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Client/AsyncTime/AsyncTimeComp.cs) | Map-`ITickable`, eigene Timer, `ExecuteCmd`, `UpdateManagers` |
| Serialisierung | [Source/Common/Syncing/SyncSerialization.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Common/Syncing/SyncSerialization.cs) | Typ-/Objekt-Synchronisierung, `SyncWorker` und Reader/Writer |
| Sync-Kommando-Handling | [Source/Client/Syncing/Sync.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Syncing/Sync.cs) | clientseitige Sync-Funktionalität |
| SyncMethod-Handler | [Source/Client/Syncing/Handler/SyncMethod.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Syncing/Handler/SyncMethod.cs) | methodenbasierte Synchronisation |

### Tiefer: TickPatch

Im geprüften `TickPatch.cs` ruft die Hauptschleife zuerst `RunCmds()` und danach `DoUpdate()` auf. `RunCmds` iteriert `ITickable`s, prüft `Cmds.Peek().ticks == Timer`, entnimmt `ScheduledCommand`, bestimmt `TickableById(cmd.mapId)` und ruft `ExecuteCmd(cmd)` auf. Ein Kommentar erklärt, dass das Command-Handling bewusst gegen **out-of-order execution (#733)** in eine globale Queue gewandert ist. Anschließend ruft `DoTick` für jeden berechtigten Tick-Kontext `TickTickable` und aktualisiert den Timer. **Praktische Konsequenz:** niemals einen UI-Button lokal direkt einen produzierenden `ThingComp` mutieren lassen; das zu synchronisierende `SetEnabled` muss als Command für denselben Simulationsschritt behandelt werden.

### Tiefer: AsyncTimeComp

`AsyncTimeComp` trennt Karten-Ticks, Kontext (`executingCmdMap`), World-Faction-/Map-Ausführung. `ExecuteCmd(ScheduledCommand)` liest `cmd.data`, setzt die aktive Karte und den Befehls-Kontext, bevor es die Command-Routine aufruft. Die Datei kommentiert ausdrücklich, dass `MapUpdate()`-artige Methoden **nicht deterministisch** sein können und die für Game-State relevanten Mutationen daher in `Tick()`/`ExecuteCmd()` überführt werden. Dies ist eine besonders starke **Primärcode-Bestätigung** für die UI-/Command-Trennung.

## Host, Client, dedizierter Server und Mehrkartenmodus

- Der Mod unterstützt Client-Verbindungen und einen lokalen/dedizierten Serverpfad; die Releases bieten für Linux einen Server-Launcher `Server/Linux/Server.sh`. Die konkrete Netzwerktopologie (NAT/Steam/P2P/Portforward) hängt von Konfiguration/Build ab: **keine feste Portnummer hier als gesichert**.
- In Game-/World-/Map-Contexts können Ticks und Kommandos unterschieden werden, einschließlich `AsyncWorldTime`. Mehrere Kolonien/Karten und unterschiedliche Zeitgeschwindigkeiten sind mehr als ein einfacher globaler Unity-Tick.
- Render-/Frame-Funktionen der Mods dürfen keinen replizierten Zustand eigenständig fortschreiben. Eine Clientpause ist nicht automatisch dasselbe wie die replizierte Weltpause.

## RNG und Divergenz

Übereinstimmender Seed reicht nicht, wenn ein Peer zusätzliche Zufallszahlen zieht. Separate Map-/World-/Command-RNG-Kontexte und Eingabe-Sync erfordern nachprüfbare Aufrufreihenfolgen. Siehe `09-determinism.md`.

## Beitritt, Speichern, Wiederverbinden

Historische Multiplayer-Dokumente beschreiben die Übertragung eines World-Snapshots und anschließendes Aufholen, aber die genauen **aktuellen** `Protocol`-/`Join`-/`Replay`-Schritte sind in dieser Recherche nicht so weit belegt, dass eine Paket- oder Speicherformatreferenz stabil erklärt werden dürfte. Für konkrete Rejoin-Implementierung in den `rwmt`-Source-Bäumen nach Commits pinnen und lesen. `Client/Multiplayer.cs` verweist auf Desync-/Replay-/Log-Verzeichnisse; diese Artefakte sind für die Diagnose zentral.

## Invarianten für eigene Mods

- Gleiche Mods/DLCs/Defs/Settings auf allen Peers; sensible Config zur Session speichern bzw. abgleichen.
- Nur replizierte Commands oder deterministischer Tick ändern Weltdaten. GUI-Aufbau/Frame-Update/Tooltip dürfen es nicht.
- Gleiches `Thing` anhand stabiler Referenzen serialisieren, nicht mittels Host-lokaler Speicheradresse oder UI-Selektion referenzieren.
- Alle für Entscheidungen relevanten Iterationen sortieren, RNG-Kontext kontrollieren, keine Wallclock/System.Random/Thread-Race.
- Simulationskontext (World vs Map) im SyncHandler korrekt herstellen und nach Ausführung zurücksetzen.

## Prüfliste für vertiefte Quellcodeprüfung

Bei der tatsächlichen Implementierung müssen unter festem Commit zusätzlich Network-Packet-Definitionen, LiteNetLib-Setup, HostAuthority-Verhalten, Serializer-Fallbacks, Handshake-Verfahrensweise und Save-/Replay-Format gelesen werden. Sie sind **nicht** vollständig aus den wenigen untersuchten Files ableitbar. Diese offen gelassenen Punkte nicht als vollständig analysiert ausgeben.
