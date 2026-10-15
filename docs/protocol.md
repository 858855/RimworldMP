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
| 20 | ModManifestSummary | C→S | Mod-Prüfsumme SHA-256 (32 Bytes), Spielversion (nur Hinweis), Mod-Anzahl `u16` |
| 21 | ModManifestRequest | C→S, S→C | Anfrage: Session-ID des Hosts; an den Host weitergeleitet: Session-ID des Anfragenden |
| 22 | TransferBegin | C→S→C | Transfer-ID, Art (`1` Manifest, `2` Welt, `3` Spielstand), Sender (vom Server gesetzt), Ziel (leer = alle anderen), Größe `u64`, SHA-256 |
| 23 | TransferChunk | C→S→C | Transfer-ID, Index `u32`, danach bis zu 60 KiB Rohdaten |
| 24 | TransferAck | C→S→C | Transfer-ID, Empfänger (vom Server gesetzt), Anzahl empfangener Blöcke `u32` |
| 25 | TransferAbort | beide | Transfer-ID, Grund |
| 26 | StartSetupRequest | C→S | leer; nur Host |
| 27 | AbortSetupRequest | C→S | leer; nur Host |

Listen- und Zustandsupdates sind in `LobbyPayloads.cs` kodiert: Liste mit `count:u8`, je Lobby GUID, Name, Hostname, Spielerzahl und Kapazität; Snapshot mit GUID, Name, Host-GUID, Revision `u64`, Kapazität, Einrichtungsphase `u8` und Spielerarray (GUID, Name, Ready, Mod-Status `u8`: `0` ungeprüft, `1` Prüfung läuft, `2` passt, `3` abweichend). Listeneinträge enthalten zusätzlich das Manifest des Hosts (optional) und die Phase. Der TCP-Server verwendet diese Payloads; zwei unabhängige TCP-Testclients bestanden die Integrationstests. RimWorld-Client-Kompatibilität ist noch offen. Fehlercodes: `1=InvalidMessage`, `2=VersionMismatch`, `3=Unauthorized`, `4=NotFound`, `5=Capacity`, `6=InvalidState`, `7=ServerBusy`, `8=ModMismatch`, `9=SetupInProgress`.

## Übergänge und Grenzen

`Connected -> Hello -> Accepted -> LobbyRequests -> Disconnect`. Vor `Accepted` sind ausschließlich `Hello` und serverseitige Ablehnung zulässig. Der Server prüft Identität, Kapazität und Host-Rechte; Clients senden nur Anfragen. Frame-Limit 64 KiB, Standard-Stringlimit 256 Bytes. Kein fremder Code und keine unsichere polymorphe Deserialisierung.

Der unabhängige `LobbyClient` beantwortet `Ping` automatisch mit derselben Nonce und reicht andere Nachrichten an die Spiel-UI weiter. Die UI verwirft ältere Lobby-Revisionen. Ohne TLS und Authentifizierung ausschließlich kontrollierte LAN-/Testnetze. Siehe ADR 0003 und `docs/SPEC.md`.

Chat-Anfragen sind nur für Mitglieder einer Lobby gültig. Der Server bestimmt Sender-ID und Namen aus der authentisierten Sitzung und sendet die Meldung ausschließlich an die Mitglieder dieser Lobby. Leere Texte, Steuerzeichen, ungültiges UTF-8 und mehr als 512 UTF-8-Bytes werden abgelehnt. Der Chat wird weder auf dem Server noch im Spielstand gespeichert; die UI hält nur die letzten 50 Meldungen. Steam-, Direkt-TCP- und Dedicated-Server-Modus verwenden dasselbe Format. Siehe ADR 0011.

Der Spielerschlüssel (ID-001) ist die dauerhafte Identität eines Spielers über Neustarts und Reconnects hinweg. Der Mod nutzt die SteamID, ohne Steam eine einmal erzeugte und in den Mod-Einstellungen gespeicherte GUID. Ein `Hello` ohne gültigen Schlüssel wird abgelehnt. Meldet sich derselbe Schlüssel erneut an, schließt der Server die ältere Sitzung. Der Schlüssel wird nicht an andere Spieler verteilt. Ohne Authentifizierung kann er bei IP-Lobbys gefälscht werden (siehe SEC-001); Steam-Lobbys nehmen nur SteamIDs aus der Steam-Lobby an.

## Mod-Manifest (MOD-001)

Jeder Client bildet beim Spielstart im Hintergrund ein Manifest: Spielversion mit Revision und alle aktiven Mods **in Ladereihenfolge** (Package-ID, Name, Version aus `About.xml`, Inhalts-SHA-256 über `About/About.xml` sowie `Assemblies/**/*.dll`, `Defs/**/*.xml` und `Patches/**/*.xml` der Ladeordner; Texturen und Sounds zählen nicht). Die **Prüfsumme** ist ein SHA-256 nur über die Mods in Ladereihenfolge (Package-ID, Version, Inhalts-Hash); Spielversion und Anzeigenamen zählen nicht (Entscheidung des Projektinhabers, 2026-10-15). Die Spielversion wird nur als Hinweis mitgeschickt und angezeigt. An den Server geht nur die Zusammenfassung (`ModManifestSummary`). Der Server vergleicht den Hash jedes Mitglieds mit dem des **aktuellen** Hosts, setzt den Mod-Status im Snapshot und lehnt `PlayerReadyChanged(true)` bei „Prüfung läuft“ oder „abweichend“ mit Code 8 ab. Wechselt die Host-Rolle, wird neu verglichen und die Bereitschaft abweichender Mitglieder zurückgesetzt. Für den Unterschiedsbericht fragt ein Mitglied mit `ModManifestRequest` die volle Liste des Hosts an; der Host antwortet mit einer Chunk-Übertragung der Art `1`. Der Server liest Mod-Listen nie selbst.

## Chunk-Übertragung (XFER-001)

Große Daten gehen in Blöcken zu 60 KiB über den Server: `TransferBegin`, dann `TransferChunk` in strikter Reihenfolge, Empfänger bestätigen mit `TransferAck` (mindestens alle 8 Blöcke und am Ende). Nach `TransferBegin` meldet der Server dem Sender jeden Empfänger mit einem Ack über 0 Blöcke. Ein Sender hält höchstens 32 Blöcke über dem langsamsten Empfänger offen; das hält die 1-MiB-Empfangsqueue von Steam ein. Grenzen: Manifest 4 MiB, Welt 64 MiB, Spielstand 256 MiB; ein Sender darf nur eine Übertragung gleichzeitig haben; Empfänger müssen in derselben Lobby sein. Der Server prüft Reihenfolge und Blocklänge, speichert keine Daten und bricht für alle ab (`TransferAbort`), wenn ein Teilnehmer trennt oder die Lobby verlässt. Empfänger prüfen am Ende den SHA-256.

## Spielstart (START-001)

`StartSetupRequest` ist nur für den Host gültig und nur, wenn mindestens zwei Spieler in der Lobby sind, alle den Mod-Status „passt“ haben und alle bereit sind. Danach steht die Lobby in Phase `1` (Szenario): Beitritte werden mit Code 9 abgelehnt, Bereitschaftsänderungen mit Code 6. `AbortSetupRequest` des Hosts oder das Verlassen des Hosts setzt die Phase auf `0` zurück und löscht alle Bereitschaften, bevor die Host-Rolle wechselt. Die Phasen `2` bis `11` sind für SETUP-001 ff. reserviert (siehe `docs/plans/rimworldmp-full-plan.md` §6.2).
