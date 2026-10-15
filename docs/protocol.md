# Protokoll – Wire-Version 3

**Implementierungsstand:** Framing und Nutzdaten-Helfer auf `feat/network-protocol`; Server- und Lobby-Semantik sind auf `feat/dedicated-server` implementiert und per TCP getestet. Kein Gameplay-Sync.

## Frame

Alle Ganzzahlen sind Big Endian. Vier Bytes geben die Body-Länge an (`6..65536`). Der Body enthält `version:u8` (=3), `type:u8`, `requestId:u32` und danach `payload`. TCP-Lesevorgänge dürfen Frames beliebig fragmentieren oder zusammenfassen. Ein einzelner Request erhält eine vom Client gewählte ID; Push-Nachrichten verwenden ID 0. Unbekannte Versionen und Typen werden abgelehnt. Version 3 (Spielerschlüssel, Mod-Manifest, Spieleinrichtung, Chunk-Übertragung) ist nicht mit Version-2-Clients/Servern kombinierbar; Version 2 brachte den Chat.

Strings sind `length:u16` plus strikt gültige UTF-8-Bytes. Allgemeine Strings: höchstens 256 Bytes. Namen werden zusätzlich fachlich auf 1–40 Zeichen und Steuerzeichen geprüft. GUIDs sind 16 Bytes in kanonischer Netzwerkreihenfolge. Listen- und Spieler-Counts sind `u8`; Payloads müssen vollständig gelesen werden, zusätzliche Bytes sind Fehler.

## Nachrichtentypen

| Code | Typ | Richtung | Nutzdaten |
|---|---|---|---|
| 1 | Hello | C→S | Spielername, Spielerschlüssel (`steam:<SteamID64>` oder `local:<32 hex>`, höchstens 64 Bytes) |
| 2 | HandshakeAccepted | S→C | Session-ID, Heartbeat-Sekunden |
| 3 | HandshakeRejected | S→C | Fehlercode, Text |
| 4/5 | Ping/Pong | beide | Nonce `u64` |
| 6/7 | LobbyListRequest/Response | C→S/S→C | leer / Liste von Lobby-Kurzständen |
| 8 | CreateLobby | C→S | Name, Kapazität |
| 9 | JoinLobby | C→S | Lobby-ID |
| 10 | LeaveLobby | beide | Anfrage leer; Antwort leer, auch für Host-Schließen |
| 11 | LobbyStateUpdate | S→C | vollständiger Lobbyzustand |
| 12/13 | PlayerJoined/PlayerLeft | S→C | Spieler-ID |
| 14 | PlayerReadyChanged | C→S und S→C | Anfrage: bool; Meldung: Spieler-ID, bool |
| 15 | Disconnect | C→S | leer; die Verbindung wird geschlossen |
| 16 | Error | S→C | Fehlercode, Text |
| 17 | CloseLobby | C→S | leer; nur Host |
| 18 | LobbyChatRequest | C→S | Text, höchstens 512 UTF-8-Bytes |
| 19 | LobbyChatMessage | S→C | Lobby-ID, Sender-Session-ID, serverseitiger Spielername, Text |

Listen- und Zustandsupdates sind in `LobbyPayloads.cs` kodiert: Liste mit `count:u8`, je Lobby GUID, Name, Hostname, Spielerzahl und Kapazität; Snapshot mit GUID, Name, Host-GUID, Revision `u64`, Kapazität und Spielerarray (GUID, Name, Ready). Der TCP-Server verwendet diese Payloads; zwei unabhängige TCP-Testclients bestanden die Integrationstests. RimWorld-Client-Kompatibilität ist noch offen. Fehlercodes: `1=InvalidMessage`, `2=VersionMismatch`, `3=Unauthorized`, `4=NotFound`, `5=Capacity`, `6=InvalidState`, `7=ServerBusy`.

## Übergänge und Grenzen

`Connected -> Hello -> Accepted -> LobbyRequests -> Disconnect`. Vor `Accepted` sind ausschließlich `Hello` und serverseitige Ablehnung zulässig. Der Server prüft Identität, Kapazität und Host-Rechte; Clients senden nur Anfragen. Frame-Limit 64 KiB, Standard-Stringlimit 256 Bytes. Kein fremder Code und keine unsichere polymorphe Deserialisierung.

Der unabhängige `LobbyClient` beantwortet `Ping` automatisch mit derselben Nonce und reicht andere Nachrichten an die Spiel-UI weiter. Die UI verwirft ältere Lobby-Revisionen. Ohne TLS und Authentifizierung ausschließlich kontrollierte LAN-/Testnetze. Siehe ADR 0003 und `docs/SPEC.md`.

Chat-Anfragen sind nur für Mitglieder einer Lobby gültig. Der Server bestimmt Sender-ID und Namen aus der authentisierten Sitzung und sendet die Meldung ausschließlich an die Mitglieder dieser Lobby. Leere Texte, Steuerzeichen, ungültiges UTF-8 und mehr als 512 UTF-8-Bytes werden abgelehnt. Der Chat wird weder auf dem Server noch im Spielstand gespeichert; die UI hält nur die letzten 50 Meldungen. Steam-, Direkt-TCP- und Dedicated-Server-Modus verwenden dasselbe Format. Siehe ADR 0011.

Der Spielerschlüssel (ID-001) ist die dauerhafte Identität eines Spielers über Neustarts und Reconnects hinweg. Der Mod nutzt die SteamID, ohne Steam eine einmal erzeugte und in den Mod-Einstellungen gespeicherte GUID. Ein `Hello` ohne gültigen Schlüssel wird abgelehnt. Meldet sich derselbe Schlüssel erneut an, schließt der Server die ältere Sitzung. Der Schlüssel wird nicht an andere Spieler verteilt. Ohne Authentifizierung kann er bei IP-Lobbys gefälscht werden (siehe SEC-001); Steam-Lobbys nehmen nur SteamIDs aus der Steam-Lobby an.
