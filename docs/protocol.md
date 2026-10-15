# Protokoll 0.1 – Wire-Version 1

**Implementierungsstand:** Framing und Nutzdaten-Helfer auf `feat/network-protocol`; Server- und Lobby-Semantik folgen auf abhängigen Branches. Kein Gameplay-Sync.

## Frame

Alle Ganzzahlen sind Big Endian. Vier Bytes geben die Body-Länge an (`6..65536`). Der Body enthält `version:u8` (=1), `type:u8`, `requestId:u32` und danach `payload`. TCP-Lesevorgänge dürfen Frames beliebig fragmentieren oder zusammenfassen. Ein einzelner Request erhält eine vom Client gewählte ID; Push-Nachrichten verwenden ID 0. Unbekannte Versionen und Typen werden abgelehnt.

Strings sind `length:u16` plus strikt gültige UTF-8-Bytes. Allgemeine Strings: höchstens 256 Bytes. Namen werden zusätzlich fachlich auf 1–40 Zeichen und Steuerzeichen geprüft. GUIDs sind 16 Bytes in kanonischer Netzwerkreihenfolge. Counts sind `u16`; Payloads müssen vollständig gelesen werden, zusätzliche Bytes sind Fehler.

## Nachrichtentypen

| Code | Typ | Richtung | Nutzdaten (geplant) |
|---|---|---|---|
| 1 | Hello | C→S | Spielername |
| 2 | HandshakeAccepted | S→C | Session-ID, Heartbeat-Sekunden |
| 3 | HandshakeRejected | S→C | Fehlercode, Text |
| 4/5 | Ping/Pong | beide | Nonce `u64` |
| 6/7 | LobbyListRequest/Response | C→S/S→C | leer / Liste von Lobby-Kurzständen |
| 8 | CreateLobby | C→S | Name, Kapazität |
| 9 | JoinLobby | C→S | Lobby-ID |
| 10 | LeaveLobby | C→S | leer |
| 11 | LobbyStateUpdate | S→C | vollständiger Lobbyzustand |
| 12/13 | PlayerJoined/PlayerLeft | S→C | Spieler-ID |
| 14 | PlayerReadyChanged | C→S und S→C | Anfrage: bool; Meldung: Spieler-ID, bool |
| 15 | Disconnect | beide | Grund |
| 16 | Error | S→C | Fehlercode, Text |
| 17 | CloseLobby | C→S | leer; nur Host |

Die konkreten Felder für Listen und Zustandsupdates werden mit `LOBBY-001` stabilisiert; bis dahin wird keine vollständige Wire-Kompatibilität behauptet. Fehlercodes: `1=InvalidMessage`, `2=VersionMismatch`, `3=Unauthorized`, `4=NotFound`, `5=Capacity`, `6=InvalidState`, `7=ServerBusy`.

## Übergänge und Grenzen

`Connected -> Hello -> Accepted -> LobbyRequests -> Disconnect`. Vor `Accepted` sind ausschließlich `Hello` und serverseitige Ablehnung zulässig. Der Server prüft Identität, Kapazität und Host-Rechte; Clients senden nur Anfragen. Frame-Limit 64 KiB, Standard-Stringlimit 256 Bytes. Kein fremder Code und keine unsichere polymorphe Deserialisierung.

Ohne TLS und Authentifizierung ausschließlich kontrollierte LAN-/Testnetze. Siehe ADR 0003 und `docs/SPEC.md`.
