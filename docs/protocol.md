# Protokoll – Wire-Version 4

**Implementierungsstand:** Framing und Nutzdaten-Helfer auf `feat/network-protocol`; Server- und Lobby-Semantik sind auf `feat/dedicated-server` implementiert und per TCP getestet. Kein Gameplay-Sync.

## Frame

Alle Ganzzahlen sind Big Endian. Vier Bytes geben die Body-Länge an (`6..65536`). Der Body enthält `version:u8` (=4), `type:u8`, `requestId:u32` und danach `payload`. TCP-Lesevorgänge dürfen Frames beliebig fragmentieren oder zusammenfassen. Ein einzelner Request erhält eine vom Client gewählte ID; Push-Nachrichten verwenden ID 0. Unbekannte Versionen und Typen werden abgelehnt. Version 2 brachte Chat-Nachrichten, Version 3 den Chunk-Kanal, Version 4 den Gleichlauf (SYNC-001); Versionen sind untereinander nicht kombinierbar.

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
| 33 | PawnRoster | C→S→C | `count:u8` (≤ 24) Figurenkarten; nur Host, nur in `Characters` |
| 34 | PawnRequest | C→S→C | Art `u8`, Figur `u32`, andere Figur `u32`, Text (≤ 4096 Byte), optional `PlayerKey`; nur Gäste, nur in `Characters`, an den Host |
| 35 | GameReady | C→S | Fingerabdruck des geladenen Startspiels (32 Byte); nur Host, nur in `Generating` |
| 36 | GameLoaded | C→S | Fingerabdruck des geladenen Startspiels (32 Byte); nur Gäste, nur in `Generating` |
| 37 | PawnPortrait | C→S→C | Figur `u32`, PNG (`u32`-Länge, 1..49152 Byte); nur Host, nur in `Characters`, an alle anderen |
| 38 | LockstepCommand | C→S | Art `u16`, Nutzlast (`u32`-Länge, ≤ 32 768 Byte); nur in `Playing` |
| 39 | ScheduledCommand | S→C | Tick `u32`, `seq` `u32`, Aussteller `PlayerKey`, Art `u16`, Nutzlast; an alle |
| 40 | TickHorizon | S→C | Horizont `u32`, Geschwindigkeit `u8` (0–3), wartet auf (Session-ID, leer = niemand), dessen Tick `u32`, letzter Tick mit gleichen Prüfsummen `u32` (`0xFFFFFFFF` = keiner) |
| 41 | ClientTick | C→S | Tick `u32`, Flags `u8` (Bit 0: vanillas Gefahrenbremse aktiv); nur in `Playing` |
| 42 | StateHash | C→S | Tick `u32`, Hash `u64`; nur in `Playing` |
| 43 | DesyncDetected | S→C | Tick `u32`, Anzahl `u8` (1–64), Session-IDs der Spieler, die vom Host abweichen |

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

Phasen: `0 Lobby`, `1 Scenario`, `2 Storyteller`, `3 WorldParams`, `4 WorldTransfer`, `5 SiteSelection`, `6 Ideology`, `7 Characters`, `8 Generating`, `9 Playing`. Der Server setzt die Regeln durch:
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

## Figurenwahl (SETUP-004, ADR 0022)

Figuren gibt es nur im Spiel des Hosts. Eine Karte in `PawnRoster`: Thing-ID `u32`, Startgruppe `bool`, optional Besitzer (`PlayerKey`), fünf Texte (Name, Kurzname, Kurzbeschreibung, Kindheit, Erwachsenenleben, je ≤ 160 Byte), dann Listen mit `count:u8` (≤ 24): Merkmale, Fertigkeiten (Name, Stufe `u8` mit `255` = unfähig, Leidenschaft `u8`), Unfähigkeiten, Gesundheit. Danach `bool` und, wenn gesetzt, die Details für die Gastseite (2026-10-16):
- drei Namensteile (je ≤ 64 Byte), Geschlecht `u8`, biologisches und chronologisches Alter (je `u16`);
- Xenotyp (≤ 64 Byte) und Symbolschlüssel (`x:`+XenotypeDef oder `c:`+XenotypeIconDef, ≤ 96 Byte), Lieblingsfarbe RGBA `u32` (0 = keine);
- Listen (`count:u8` ≤ 24): Gesundheit (Körperteil, Text), Beziehungen (Art, Name, Meinung `i16`, Gegenmeinung `i16`), Besitz (Name, Anzahl `u32`);
- Altersstufe des Platzes `u8` (DevelopmentalStage), Tooltips für Kindheit und Erwachsenenleben (je ≤ 1024 Byte), Listen (`count:u8` ≤ 24): Merkmal-Tooltips (≤ 1024 Byte, Reihenfolge wie Merkmale), AbilityDef-Namen, ThingDef-Namen des Besitzes (je ≤ 64 Byte).

Passt eine Liste mit Tooltips nicht in einen Frame, sendet der Host sie ohne Tooltips.

`PawnPortrait` trägt das Bild einer Figur so, wie das Spiel des Hosts sie zeichnet: Figur `u32`, Ansicht `u8`, dann PNG.
- Ansicht `0` ist Kleidung ohne Kopfbedeckung (vanillas Voreinstellung); Bit `1` = mit Kopfbedeckung, Bit `2` = ohne Kleidung.
- Der Host sendet zuerst Ansicht 0 aller Figuren, dann die anderen, und jede wieder, wenn sich das Aussehen ändert. Der Server prüft die Nutzlast und leitet sie nur in `Characters` und nur vom Host weiter (sonst Code 3 bzw. 6). Die Codes 33–37 kamen ohne neue Wire-Version; alte Clients können neue Listen nicht lesen.

Der Server leitet die Liste an die Gäste weiter, bildet je Spieler einen SHA-256 über dessen Karten und löscht Fertig, wenn sich der Wert ändert oder der Spieler keine Figur der Startgruppe mehr hat. Fertig in `Characters` verlangt eine eigene Figur der Startgruppe (Code 6). `PawnRequest`-Arten:
- `1` übernehmen, `2` freigeben, `3` neu würfeln, `4` tauschen, `5` umbenennen (Spitzname).
- `6` Namen setzen: Text `Vorname␟Spitzname␟Nachname` (Trenner U+001F).
- `7` Xenotyp: Text `x:`+XenotypeDef, oder `c:`+Name␟`0|1` vererbbar␟XenotypeIconDef␟Gene durch Komma (≤ 256).
- `8` Altersstufe: Wert im Feld „andere Figur“.

Bei 7 und 8 ändert der Host die Generierungsanfrage des Platzes und erzeugt die Figur neu wie vanillas Knöpfe unter „Randomize“. Der Server ersetzt den mitgeschickten `PlayerKey` durch den des Absenders und leitet an den Host; ob eine Anfrage erlaubt ist, entscheidet der Host.

## Gemeinsamer Spielstart (SETUP-005, ADR 0023)

In `Generating` startet der Host das vanilla-Spiel, speichert es, sendet den Spielstand als Chunk-Übertragung der Art `3` und lädt ihn selbst neu. Ab `Generating` meldet `WorldStatus` je Spieler den Spielstand (`1` lädt, `2` passt, `3` weicht ab). `GameLoaded` eines Gastes darf vor `GameReady` kommen und zählt, sobald der Host meldet. `SetupAdvance(Playing)` verlangt `GameReady` und „passt“ bei allen; Abweichung Code 8, fehlende Meldung Code 6. In `Playing` gibt es keinen weiteren Setup-Schritt; dort läuft der Gleichlauf (unten). Bereit ist nicht gültig, Beitritte bleiben gesperrt, `SetupAbort` des Hosts beendet die Sitzung.

## Gleichlauf (SYNC-001, ADR 0024)

Nur in `Playing` (sonst Code 6). Jede Lobby in `Playing` hat einen Sequencer auf dem Server; ein neues Spiel bekommt einen neuen.
- **Start:** Erst wenn jeder Spieler einen `ClientTick` gemeldet hat. Der Horizont beginnt beim kleinsten gemeldeten Tick, die Geschwindigkeit ist Pause.
- **Takt:** etwa alle 50 ms. Wartende Befehle bekommen Tick = Horizont + 1 und fortlaufende `seq` und gehen an alle. Danach rückt der Horizont um die verstrichene Zeit × 60/180/360 Ticks je Sekunde vor, höchstens bis zum Tick des langsamsten Spielers + 2 s; gestaute Zeit wird nicht nachgeholt. Nur dieser Takt sendet `ScheduledCommand`, `TickHorizon` und `DesyncDetected`. So kommt jeder Befehl für Tick t vor jedem Horizont ≥ t an.
- **Ausführung beim Client:** Ein Befehl für Tick t läuft, sobald das Spiel bei t − 1 steht, vor dem Tick t; mehrere in `seq`-Reihenfolge. Ein Befehl für einen schon gelaufenen Tick wird nicht ausgeführt und als Abweichung gemeldet.
- **Arten:** `1` Geschwindigkeit (Nutzlast `u8` 0–3), `2` Testmarkierung (Text ≤ 64 Byte, ohne Spielwirkung). Ab `16` Spielbefehle, die der Server nicht liest (SYNC-002); bis dahin überall ein No-op.
- **Regeln für die Geschwindigkeit:** Pausieren und Fortsetzen darf jeder; die Stufe wählt der Host (sonst Code 3). Ein Gast setzt eine Pause mit der letzten Stufe fort. Ein Wunsch ohne Änderung wird verworfen. Meldet ein Spieler die Gefahrenbremse, läuft der Takt mit Normal.
- **Grenzen:** höchstens 30 Befehle je Sekunde und Spieler (Code 7), Befehl ≤ 32 KiB. Den Aussteller setzt der Server.
- **Prüfsummen:** beim Start und nach jedem Tick mit `Tick % 60 == 0`. Der Server vergleicht, sobald alle gemeldet haben, mit dem Hash des Hosts. Der letzte gleiche Tick steht im Horizont; die erste Abweichung meldet er einmal mit `DesyncDetected`. Höchstens 64 offene Ticks.
