# ADR 0003: Versioniertes binäres TCP-Framing

- **ID/Status:** 0003, implementiert auf `feat/network-protocol`, Review ausstehend.
- **Ziel:** Eine einfache, begrenzte und RimWorld-unabhängige Leitung für Lobby-Nachrichten.
- **Betroffene Klassen/Defs:** `FrameCodec`, `WireMessage`, `MessageType`, `PayloadWriter`, `PayloadReader`. Keine RimWorld-Defs oder Game-Klassen.
- **Zustand/Persistenz:** Frames transportieren nur explizite Sitzungsnachrichten; keine Game-Persistenz. Serverseitige Lobby bleibt später die Autorität.
- **UI/Sync:** UI sendet Anfragen und verarbeitet Antworten. Keine Gameplay-Synchronisation, kein Tick-Kontext.
- **Entscheidung:** TCP mit 4-Byte-Big-Endian-Längenfeld. Body: 1 Byte Version, 1 Byte Typ, 4 Byte Request-ID, danach typabhängige Nutzdaten. Version 1, maximal 65.536 Body-Bytes. UTF-8-Strings haben 2-Byte-Länge und werden strikt dekodiert.
- **Gründe:** TCP liefert einen Byte-Stream; begrenztes Framing verhindert unbegrenzte Belegung. Typen und Feldgrenzen sind ohne polymorphe Deserialisierung prüfbar. `netstandard2.0` hält die Bibliothek vom Spiel und Server-Runtime getrennt.
- **Alternativen:** JSON mit Paketabhängigkeit (leichter lesbar, größere Angriffs-/Versionsfläche); UDP (zusätzliche Zuverlässigkeit nötig); bestehende `rwmt`-Pakete (an anderes Gameplay-Produkt gekoppelt).
- **Konflikt-/Sicherheitsbereich:** Unbekannte Version, Nachrichtentypen, überlange Frames und ungültiges UTF-8 werden abgelehnt. LAN-Testprotokoll ohne Authentifizierung/TLS; keine öffentliche Internetfreigabe.
- **Performance:** Puffer pro Frame maximal 64 KiB; keine unkontrollierten dynamischen Typen. Große Listen müssen innerhalb des Limits bleiben.
- **Tests/Rollback:** Fragmentiertes Lesen, mehrere Frames, Länge/Version/Typ/UTF-8/EOF testen. Rücknahme als einzelner Feature-Commit möglich; spätere Versionen erhalten eigene Decoder.
