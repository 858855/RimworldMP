# ADR 0004: TCP-Verbindung und Handshake-Lebensdauer

- **ID/Status:** 0004, implementiert auf `feat/network-protocol`, Review ausstehend.
- **Ziel/Klassen:** `NetworkConnection` kapselt einen TCP-Stream; `HandshakeClient` prüft Hello/Accepted vor Lobby-Anfragen. Keine RimWorld-Defs.
- **Zustand/Persistenz:** Verbindung ist flüchtig; Session-ID kommt vom Server. Kein lokaler Gameplay-Zustand wird gespeichert.
- **UI/Sync:** UI darf auf Verbindungsstatus reagieren, aber keine Spielwelt mutieren. Lobby-Events folgen später.
- **Entscheidung:** Genau ein lesender Aufrufer je Verbindung; Schreibaufrufe werden serialisiert. Connect und Handshake haben Timeouts/Cancellation. EOF und Protokollfehler schließen die Verbindung. Heartbeat wird im späteren Nachrichtenpumpen-/Server-Schritt koordiniert, damit kein zweiter Reader entsteht.
- **Alternativen:** Ein Task pro Nachricht ohne Schreibsperre (Frames könnten ineinanderlaufen); Heartbeat mit eigenem Reader (Antwort-Race); synchrone Sockets (UI-Blockade).
- **Risiken/Tests:** Verbindungsabbruch, Timeout, paralleles Senden und echte Loopback-Sockets testen. Keine Authentifizierung/TLS, daher nur kontrollierte LAN-Tests. Rücknahme per Feature-Commit.
