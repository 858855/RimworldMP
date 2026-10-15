# Chunk-Kanal (XFER-001) – Prüfung 2026-10-15

- **Branch/Basis:** `feat/chunk-transfer` von `docs/full-multiplayer-plan` @ `1e5a529`, separater Worktree, damit sich die Arbeit nicht mit der parallelen Codex-Sitzung überschneidet.
- **Umgebung:** Linux, .NET SDK 10.0.402 / Runtime 10.0.13 (`/home/fabiok/.local/share/dotnet-rimworldmp`), lokale RimWorld-Assemblies `1.6.4871` (`Version.txt` rev598, Unity/Mono).
- **Design:** ADR 0014. Grundlage ist ADR 0013, mit dem Steam-Listen-Host statt Dedicated Server.

## Tatsächlich ausgeführte Prüfungen

| Befehl/Prüfung | Ergebnis |
|---|---|
| `DOTNET_BIN=…/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen, 0 Fehler gegen lokale 1.6-Assemblies |
| `DOTNET_BIN=…/dotnet bash scripts/build-server.sh` | Exit 0 |
| `dotnet run --project tests/Protocol.Tests/… -c Release` | Exit 0; sieben Gruppen, neu: Transfer-Nutzdaten und Grenzen, Wire-Version 3 |
| `dotnet run --project tests/Network.Tests/… -c Release` | Exit 0; elf Gruppen, neu sechs Zustandsmaschinen-Gruppen |
| `dotnet run --project tests/Hosting.Tests/… -c Release` | Exit 0; 13 Gruppen, neu fünf Relay-Gruppen über `ListenHost` |
| `dotnet run --project tests/Integration.Tests/… -c Release` | Exit 0; 18 Gruppen, unverändert |
| `dotnet run --project tests/Lobby.Tests/…`, `Client.Tests`, `ProcessIntegration.Tests` | Exit 0; ein, drei, acht Schritte unverändert |
| `python3 scripts/validate_project_state.py` | Exit 0; 22 Pflichtdateien, 55 Aufgaben |

## Messwerte über den eingebetteten Listen-Host

| Szenario | Ergebnis |
|---|---|
| 50 MB Host → Gast über TCP | SHA-256 gleich, 0,4 s |
| 50 MB Host → Gast über Steam-ähnlichen Strom, Gast leert seine Queue einmal je 16-ms-Frame | SHA-256 gleich, 5,4 s ≈ 9,3 MB/s; Spitze Sendepuffer **360 von 512 KiB**, Spitze Empfangsqueue **360 von 1024 KiB** |
| 8 MB Host → TCP-Gast und Steam-ähnlicher Gast zugleich | beide Kopien identisch, Strom intakt |
| Empfänger verlässt die Lobby nach drei Chunks | Absender erhält `PeerLeft` |
| Empfänger oder Absender außerhalb der Lobby | `Rejected` |
| Absender schickt 9 Chunks ohne Bestätigung; Grenze ist 8 | `Rejected` („window“) |
| Zweite gleichzeitige Übertragung desselben Absenders | `Rejected` („still running“) |

Die Spitze von 360 KiB entspricht genau dem Fenster: 6 × 61 474 Byte je Chunk-Frame.

Zustandsmaschinen, simulierter Kanal mit virtueller Uhr:
- ungestörter Lauf mit gefülltem, nie überschrittenem Fenster
- sieben verlorene Bestätigungen in Folge, danach Wiederaufnahme nach dem Timeout
- verlorene letzte Bestätigung
- vertauschter Chunk
- einmal beschädigte Daten: Neustart mit neuer Transfer-ID und korrektem Ergebnis
- dauerhaft beschädigte Daten: `HashMismatch`
- stummer Empfänger: genau drei Wiederholungen, dann `Timeout`
- falsche Chunklänge: `InvalidData`
- Abbruch durch den Benutzer und Größengrenzen

## Gegenprobe zum Plan-Fenster

Mit vorübergehend gesetztem `WindowChunks = 32` (Plan §6.4) und `MaximumWindowChunks = 34` lief der TCP-Fall durch. Über den Steam-ähnlichen Strom brach die Verbindung ab: Der Sendepuffer lief über, und der Absender erhielt `PeerLeft`. Danach wurden die Konstanten auf 6/8 zurückgesetzt, und die Suite lief erneut grün. Der Test erkennt das Risiko also tatsächlich.

## Steam-Nachbildung

`tests/Hosting.Tests/SteamLikeLink.cs` bildet die Grenzen aus `SteamPeerStream` nach:
- eine Nachricht je Schreibvorgang
- höchstens 8 Nachrichten je Pump bei 16 ms Takt
- 1-MiB-Empfangsqueue, deren Überlauf die Verbindung beendet
- 512-KiB-Sendepuffer; Überlauf beendet wie `k_EResultLimitExceeded`

Die 512 KB sind der Standardwert von `k_ESteamNetworkingConfig_SendBufferSize` laut Steamworks-Dokumentation, Status **WAHRSCHEINLICH**, lokal nicht gemessen.

## Klassifizierung und Grenzen

`BUILD_ERFOLGREICH`, Bibliothek und Relay automatisiert getestet. **Echtes Steam P2P: `NICHT_GETESTET`**; dafür braucht es zwei Steam-Konten. Die Nutzung im Spiel ist nicht Teil von XFER-001 und kommt mit MOD-001, SETUP-002 und SETUP-005. `LAUFZEIT NICHT VERIFIZIERT`. Wire-Version 3 ist nicht mit Version 2 kombinierbar.
