# Protokoll – Wire-Version 3

**Implementierungsstand:** Framing und Nutzdaten-Helfer auf `feat/network-protocol`; Server- und Lobby-Semantik sind auf `feat/dedicated-server` implementiert und per TCP getestet. Kein Gameplay-Sync.

## Frame

Alle Ganzzahlen sind Big Endian. Vier Bytes geben die Body-Länge an (`6..65536`). Der Body enthält `version:u8` (=3), `type:u8`, `requestId:u32` und danach `payload`. TCP-Lesevorgänge dürfen Frames beliebig fragmentieren oder zusammenfassen. Ein einzelner Request erhält eine vom Client gewählte ID; Push-Nachrichten verwenden ID 0. Unbekannte Versionen und Typen werden abgelehnt. Version 2 brachte Chat-Nachrichten, Version 3 den Chunk-Kanal; Versionen sind untereinander nicht kombinierbar.

Strings sind `length:u16` plus strikt gültige UTF-8-Bytes. Allgemeine Strings: höchstens 256 Bytes. Namen werden zusätzlich fachlich auf 1–40 Zeichen und Steuerzeichen geprüft. GUIDs sind 16 Bytes in kanonischer Netzwerkreihenfolge. Listen- und Spieler-Counts sind `u8`; Payloads müssen vollständig gelesen werden, zusätzliche Bytes sind Fehler.

## Nachrichtentypen

| Code | Typ | Richtung | Nutzdaten |
|---|---|---|---|
| 1 | Hello | C→S | Spielername, PlayerKey (Art `u8`: 1 = Steam + `u64`, 2 = lokal + GUID) |
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
| 20 | TransferBegin | beide | Transfer-ID, Art `u8`, Gegenstelle, Länge `u64`, Chunkzahl `u32`, SHA-256 (32 Byte) |
| 21 | TransferChunk | beide | Transfer-ID, Index `u32`, Daten (`u32`-Länge, höchstens 61 440 Byte) |
| 22 | TransferAck | beide | Transfer-ID, Anzahl lückenlos verarbeiteter Chunks `u32` |
| 23 | TransferAbort | beide | Transfer-ID, Grund `u8`, Text |
| 24 | SetupAdvance | C→S | Zielphase `u8`; nur Host, nur die nächste Phase |
| 25 | SetupAbort | C→S | leer; nur Host |
| 26 | SiteFlag | C→S | `bool` + optional Feld (`tileId:u32`, `layerId:u32`); nur in der Startortwahl |
| 27 | SiteDecided | C→S | Feld; nur Host |
| 28 | ModManifestSummary | C→S | Mod-Prüfsumme SHA-256 (32 Byte), Spielversion (nur Hinweis), Mod-Anzahl `u16` |
| 29 | ModManifestRequest | C→S, S→C | Anfrage: Session-ID des Hosts; an den Host weitergeleitet: Session-ID des Anfragenden |
| 30 | SetupSettings | C→S→C | Seitenname, bis zu 24 Paare Bezeichnung/Wert; nur Host, nur während der Einrichtung |
| 31 | WorldReady | C→S | Welt-Fingerabdruck (32 Byte); nur Host, nur in `WorldTransfer` |
| 32 | WorldLoaded | C→S | Fingerabdruck der geladenen Welt (32 Byte); nur Gäste, nur in `WorldTransfer` |

Listen- und Zustandsupdates sind in `LobbyPayloads.cs` kodiert: Liste mit `count:u8`, je Lobby GUID, Name, Hostname, Spielerzahl und Kapazität; Snapshot mit GUID, Name, Host-GUID, Revision `u64`, Kapazität, Setup-Phase `u8`, optional gewähltes Feld und Spielerarray (Session-GUID, Name, Ready, PlayerKey, optional Fähnchen). Die Liste trägt je Lobby zusätzlich die Phase. Der TCP-Server verwendet diese Payloads; zwei unabhängige TCP-Testclients bestanden die Integrationstests. RimWorld-Client-Kompatibilität ist noch offen. Fehlercodes: `1=InvalidMessage`, `2=VersionMismatch`, `3=Unauthorized`, `4=NotFound`, `5=Capacity`, `6=InvalidState`, `7=ServerBusy`.

## Übergänge und Grenzen

`Connected -> Hello -> Accepted -> LobbyRequests -> Disconnect`. Vor `Accepted` sind ausschließlich `Hello` und serverseitige Ablehnung zulässig. Der Server prüft Identität, Kapazität und Host-Rechte; Clients senden nur Anfragen. Frame-Limit 64 KiB, Standard-Stringlimit 256 Bytes. Kein fremder Code und keine unsichere polymorphe Deserialisierung.

Der unabhängige `LobbyClient` beantwortet `Ping` automatisch mit derselben Nonce und reicht andere Nachrichten an die Spiel-UI weiter. Die UI verwirft ältere Lobby-Revisionen. Ohne TLS und Authentifizierung ausschließlich kontrollierte LAN-/Testnetze. Siehe ADR 0003 und `docs/SPEC.md`.

Chat-Anfragen sind nur für Mitglieder einer Lobby gültig. Der Server bestimmt Sender-ID und Namen aus der authentisierten Sitzung und sendet die Meldung ausschließlich an die Mitglieder dieser Lobby. Leere Texte, Steuerzeichen, ungültiges UTF-8 und mehr als 512 UTF-8-Bytes werden abgelehnt. Der Chat wird weder auf dem Server noch im Spielstand gespeichert; die UI hält nur die letzten 50 Meldungen. Steam-, Direkt-TCP- und Dedicated-Server-Modus verwenden dasselbe Format. Siehe ADR 0011.

## Chunk-Kanal (Wire-Version 3)

Große Daten (Mod-Manifest bis 4 MiB, Welt bis 64 MiB, Spielstand und Snapshot bis 256 MiB) gehen in Chunks zu 61 440 Byte; die Chunkzahl ist ⌈Länge / 61 440⌉. Ein Absender hat höchstens 6 unbestätigte Chunks unterwegs; der Server bricht ab Index „bestätigt + 8“ ab. In `TransferBegin` nennt der Absender den Empfänger (`Guid.Empty` = alle anderen Lobbymitglieder); der Server ersetzt das Feld beim Weiterleiten durch die serverseitige Session-ID des Absenders. Empfänger bestätigen erst nach Verarbeitung, die letzte Bestätigung erst nach erfolgreicher SHA-256-Prüfung. Der Server meldet dem Absender nach jeder Bestätigung das Minimum über alle Empfänger. Abbruchgründe: `1=Rejected`, `2=Cancelled`, `3=HashMismatch`, `4=Timeout`, `5=PeerLeft`, `6=InvalidData`. Verletzungen der Transfer-Regeln beenden nur die Übertragung, nicht die Verbindung. Siehe ADR 0014.

## Spielerkennung

Die Session-ID wechselt mit jeder Verbindung; der `PlayerKey` bleibt. Gespeichertes wie Pawn-Besitz nutzt nur den `PlayerKey`. Über Steam prüft der Server das Hello gegen die von Steam authentifizierte SteamID (Ablehnung Code 3). Über TCP ist die Kennung nicht prüfbar (SEC-001). Eine Kennung darf nur einmal gleichzeitig verbunden sein (Ablehnung Code 6). Siehe ADR 0016.

## Spieleinrichtung

Phasen: `0 Lobby`, `1 Scenario`, `2 Storyteller`, `3 WorldParams`, `4 WorldTransfer`, `5 SiteSelection`, `6 Ideology`, `7 Characters`, `8 Generating`. Der Server setzt die Regeln durch:
- Start ab 2 Spielern, wenn alle bereit sind.
- Nur der Host schaltet weiter, und nur zur nächsten Phase.
- Außerhalb der Lobby sind Beitritte gesperrt.
- Fähnchen setzen alle, den Startort wählt nur der Host.
- Die Charakterwahl endet erst, wenn alle fertig sind.

Bereit gilt in Lobby und Charakterwahl und wird bei jedem Phasenwechsel gelöscht. Felder sind `PlanetTile` aus RimWorld 1.6 mit Feld- und Ebenennummer. Antwort ist der vollständige Lobbyzustand; Verstöße kommen als `Error` mit Code 3 (nicht Host) oder 6 (Phase/Bedingung). Siehe ADR 0015 und 0017.

## Mod-Prüfung (MOD-001, ADR 0018)

Die Lobby-Prüfsumme ist ein SHA-256 nur über die Mods in Ladereihenfolge (Package-ID, Version, Inhalts-Hash aus `About.xml`, `Assemblies`, `Defs`, `Patches`); Spielversion und Anzeigenamen zählen nicht. Jeder Spieler im Lobbyzustand trägt zusätzlich `ModStatus:u8` (`0` ungeprüft, `1` Prüfung läuft, `2` passt, `3` abweichend), verglichen mit dem aktuellen Host. Lobbylisteneinträge tragen nach der Phase `bool` + optional die Zusammenfassung des Hosts. Code 8 = Mods abweichend (Bereit in der Lobby, Start). Die volle Liste für den Bericht geht als Chunk-Übertragung der Art `1` vom Host an den Anfragenden.

## Seitenspiegel (SETUP-001, ADR 0019)

`SetupSettings` ist reine Anzeige: was der Host auf seiner vanilla-Seite gerade sieht. Der Server prüft nur Absender (Host, sonst Code 3), Phase (nicht `Lobby`, sonst Code 6) und Format und leitet an die übrigen Mitglieder weiter.

Debug-Option: Ein Host-Server mit `AllowSoloStart` nimmt `SetupAdvance(Scenario)` schon mit einem Spieler an; Standard sind mindestens zwei (ADR 0018, Nachtrag).

## Weltübertragung (SETUP-002, ADR 0020)

In `WorldTransfer` trägt jeder Spieler im Lobbyzustand zusätzlich `WorldStatus:u8` (`0` außerhalb der Phase, `1` wartet, `2` passt, `3` weicht ab). Die Welt selbst geht als Chunk-Übertragung der Art `2`. `SetupAdvance(SiteSelection)` verlangt eine Host-Meldung und „passt“ bei allen Gästen; Abweichung ergibt Code 8, fehlende Meldungen Code 6. Ein neues `WorldReady` setzt alle Gastmeldungen zurück.
