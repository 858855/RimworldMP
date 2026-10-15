# Architektur – tatsächlicher Stand

`Protocol` (`netstandard2.0`) definiert binäre, versionierte Frames bis 64 KiB und typisierte Lobby-Payloads. `Network` (`netstandard2.0`) stellt eine TCP-Verbindung mit serialisiertem Senden, einem Reader und zeitbegrenztem Handshake bereit. `Lobby` (`netstandard2.0`) hält Sitzungen, Räume, Host-Rechte, Ready-Status und Revisionen unter einem Lock. `Server` (`net10.0`) bindet diese Teile in einem eigenständigen Prozess; er verwaltet Listener, Handshake, Verteilung, Heartbeats und Bereinigung. Alle Lobbyzustände sind flüchtig.

Datenfluss: `Client -> FrameCodec -> FoundationServer -> LobbyRegistry -> LobbyPayloads/FrameCodec -> betroffene Clients`. Der Server entscheidet über Mitgliedschaft und Rechte. Lobby-Snapshots besitzen monoton steigende Revisionen; der künftige Client soll ältere Revisionen verwerfen.

Der RimWorld-Adapter ist noch geplant. Netzwerkbibliotheken referenzieren weder Verse noch Unity/Waylith. Lokales Ziel: RimWorld `1.6.4871 rev598`. Die erste Version simuliert keine Spielwelt. Authentifizierung und TLS fehlen; Standardbindung ist Loopback, LAN nur in kontrollierter Umgebung. Siehe `docs/protocol.md`, `docs/dedicated-server.md` und ADR 0002–0005.
