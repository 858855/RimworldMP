# ADR 0014 – Chunk-Kanal für große Übertragungen (XFER-001)

- **Status:** angenommen für `feat/chunk-transfer`. Designnotiz nach `AGENTS.md` Regel 4. Plan §6.4, Aufgabe `XFER-001`.
- **Ziel:** Große Binärdaten (Mod-Manifest, Welt, Spielstand, Join-Snapshot) von einem Lobbymitglied an eines oder alle anderen übertragen: geordnet, SHA-256-geprüft und mit Gegendruck, der Steam P2P nie überlastet (ADR 0013).
- **Beteiligte Klassen:**
  - `Protocol/TransferPayloads.cs`: Konstanten, Nutzdaten, Prüfregeln.
  - `Network/TransferSender.cs`, `Network/TransferReceiver.cs`: reine Zustandsmaschinen ohne eigene Threads oder I/O. Sie nehmen Nachrichten und eine Zeitangabe entgegen und liefern die zu sendenden Nachrichten zurück. Damit passen sie zum Spielthread, der `LobbyClient` einmal pro Frame leert, und sind deterministisch testbar.
  - `Hosting/TransferRelay.cs`: Relay im eingebetteten `FoundationServer`. Es hält keine Nutzdaten, nur Metadaten und Bestätigungsstände.
- **Nachrichten (Wire-Version 3):**

  | Code | Typ | Nutzdaten |
  |---|---|---|
  | 20 | TransferBegin | Transfer-ID, Art, Gegenstelle, Gesamtlänge `u64`, Chunkzahl `u32`, SHA-256 (32 Byte) |
  | 21 | TransferChunk | Transfer-ID, Index `u32`, Daten (`u32`-Länge + Bytes) |
  | 22 | TransferAck | Transfer-ID, Anzahl lückenlos verarbeiteter Chunks `u32` |
  | 23 | TransferAbort | Transfer-ID, Grund `u8`, Text |

  Die Gegenstelle in `TransferBegin` ist beim Senden der Empfänger (`Guid.Empty` = alle anderen Lobbymitglieder). Der Server ersetzt sie beim Weiterleiten durch die **serverseitig bestimmte** Session-ID des Absenders, wie beim Chat. Alle Nachrichten nutzen Request-ID 0.
- **Zustandsmodell:**
  - Sender: `Sending → Completed | Failed`. Höchstens `WindowChunks` (6) unbestätigte Chunks. Ohne Bestätigungsfortschritt innerhalb von 10 s sendet er ab dem letzten bestätigten Index erneut (Go-Back-N); nach drei Wiederholungen ohne Fortschritt bricht er mit `Timeout` ab. Bei `HashMismatch` startet er einmal mit neuer Transfer-ID neu, wie Plan §6.4 verlangt.
  - Empfänger: `Receiving → Completed | Failed`. Er nimmt nur den nächsten erwarteten Index an. Ein Duplikat beantwortet er erneut mit dem aktuellen Stand; das stellt eine verlorene Bestätigung wieder her. Einen Chunk aus der Zukunft verwirft er; der Timeout des Senders behebt die Lücke. Die letzte Bestätigung (`Anzahl == Chunkzahl`) sendet er erst **nach** erfolgreicher SHA-256-Prüfung.
  - Relay: merkt sich je Empfänger den höchsten Stand und meldet dem Absender nach **jeder** Empfängerbestätigung das Minimum. Dadurch bestimmt der langsamste Empfänger das Tempo, und eine wiederholte Bestätigung erreicht den Absender auch dann, wenn sich das Minimum nicht geändert hat.
- **Gegendruck von Ende zu Ende:** Die Bestätigung entsteht erst, wenn der Empfänger den Chunk tatsächlich verarbeitet hat, nicht schon beim Lesen vom Socket. `LobbyClient` puffert eingehende Nachrichten unbegrenzt bis zum nächsten Frame des Spiels; nur so begrenzt das Fenster auch diesen Puffer. Unterwegs sind auf allen Strecken zusammen höchstens 6 × 61 474 Byte ≈ 360 KiB. Das liegt unter Steams Sendepuffer (512 KB, WAHRSCHEINLICH) und unter der 1-MiB-Empfangsqueue von `SteamPeerStream`.
- **Abweichungen von Plan §6.4 und Begründung:**

  | Plan | Hier | Grund |
  |---|---|---|
  | höchstens 32 unbestätigte Chunks | 6 | 32 × 60 KiB = 1 920 KiB übersteigt die 1-MiB-Queue und den Steam-Sendepuffer; die Verbindung würde abbrechen |
  | Bestätigung alle 16 Chunks | nach jedem Chunk | Bei 6er-Fenster muss früher bestätigt werden; eine Bestätigung kostet 30 Byte |
  | eine Übertragung je Lobby | eine **ausgehende** je Absender | Ein großes Welt-Paket soll kein kleines Mod-Manifest eines anderen Spielers blockieren |

- **Grenzen:** Chunkgröße 61 440 Byte (60 KiB); jeder Chunk außer dem letzten ist genau so groß, Chunkzahl = ⌈Länge / 61 440⌉. Höchstlänge je Art: Manifest 4 MiB, Welt 64 MiB, Spielstand und Snapshot 256 MiB. Leere Übertragungen sind unzulässig. Der Server prüft Mitgliedschaft, Empfänger, Fenster, Index, Chunklänge und eindeutige Transfer-ID. Regelverstöße beenden nur die betroffene Übertragung mit `TransferAbort`, nicht die Verbindung; fehlerhafte Nutzdaten bleiben ein Protokollfehler wie bisher.
- **Abbruch:** Verlässt ein Beteiligter die Lobby oder trennt die Verbindung, beendet das Relay die Übertragung für alle Beteiligten mit `PeerLeft`. Ein Abbruch durch Absender oder Empfänger geht an alle Beteiligten.
- **Persistenz:** keine. Übertragungen leben nur im Speicher. Der Empfänger reserviert die angekündigte Länge erst nach Prüfung der Obergrenze.
- **UI/Spiel:** XFER-001 liefert Bibliothek, Relay und Tests. Die Nutzung im Spiel (Fortschrittsanzeige, Welt- und Spielstandübertragung) kommt mit den nutzenden Meilensteinen `MOD-001`, `SETUP-002` und `SETUP-005`. Keine Weltzustandsmutation, keine Harmony-Patches.
- **Konfliktbereich:** Wire-Version 3 ist nicht mit Version 2 kombinierbar; beide Spieler brauchen denselben Build. Die Nachrichtencodes 20–23 können mit parallel entwickelten Meilensteinen kollidieren und sind beim Merge abzugleichen.
- **Performance:** Durchsatz ≈ Fenster / Laufzeit hin und zurück, also etwa 3,6 MB/s bei 100 ms. Ein 30-MB-Spielstand braucht damit unter 10 s; das Plan-Budget sind 60 s. SHA-256 wird einmal am Ende gerechnet.
- **Tests:**
  - Protokoll: Grenzen und Rundlauf der Nutzdaten.
  - Zustandsmaschinen: verlorene Bestätigung, Reihenfolgefehler, Hash-Fehler mit Neustart, Timeout, Fenster wird nie überschritten.
  - Relay über `ListenHost`: 50 MB über TCP, 50 MB über einen Steam-ähnlichen Strom, Verteilung an zwei Empfänger, Abbruch beim Verlassen, Ablehnung fremder Empfänger und Fensterverstoß.
  - Der Steam-ähnliche Strom bildet 1-MiB-Empfangsqueue, Sendepuffer-Grenze und die Pump mit 8 Nachrichten je Frame aus `SteamPeerStream` nach. Echtes Steam bleibt `NICHT_GETESTET`.
- **Rollback:** Branch verwerfen. Bestehende Nachrichten 1–19 und ihr Verhalten bleiben unverändert; nur die Wire-Version steigt.
