# Architektur – Stand und Grenzen

## Implementiert

`Multiplayer.Foundation.Protocol` ist eine `netstandard2.0`-Bibliothek ohne Verse-/Unity-/Waylith-Referenz. `FrameCodec` liest TCP-Byte-Streams bis zu einem kompletten, auf 64 KiB begrenzten Frame und schreibt Version, Typ, Request-ID und Payload. `PayloadWriter`/`PayloadReader` kodieren begrenzte Strings und primitive Werte. Fünf ausführbare Testgruppen prüfen Format und Fehlerfälle. Nachrichtenspezifische Lobby-Payloads fehlen noch. `Multiplayer.Foundation.Network` ergänzt eine echte TCP-Verbindung mit serialisierten Sendungen und einem Handshake-Client. Eine dauerhafte Nachrichtenpumpe und Heartbeats fehlen noch. `LobbyRegistry` verwaltet flüchtige Räume mit Host-Rechten, geordneter Mitgliedschaft und Revisionen unter einem Lock; seine TCP-Anbindung fehlt noch.

## Ziel für 0.1 (geplant)

`RimWorld UI -> Network Core -> Protocol/TCP -> Dedicated Server -> Lobby Core`. Der Server ist autoritativ für Sitzungen und Lobbys; er simuliert keine Spielwelt. Network, Lobby und Server bleiben von der Game-Runtime getrennt. Ziel-Adapter: lokal installiertes RimWorld `1.6.4871 rev598`.

**Sicherheitsgrenze:** Framing und UTF-8 werden begrenzt. Authentifizierung und TLS fehlen; vorerst nur kontrollierte LAN-/Testnetze. Details: `docs/protocol.md`, ADR 0002 und 0003.
