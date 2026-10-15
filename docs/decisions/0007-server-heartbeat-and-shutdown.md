# ADR 0007: Verspätete Heartbeats und Server-Stopp

- Status: angenommen für Serverkorrektur
- Ziel: Laufende Verbindungen bei verzögerter Pong-Antwort erhalten und den Linux-Server per SIGTERM geordnet stoppen.
- Betroffene Klassen: `FoundationServer.Peer`, Heartbeat- und Empfangsschleife, Server-`Program`; Spielklassen/Defs keine.
- Zustandsmodell: Pro Sitzung höchste gesendete und höchste bestätigte Ping-Nonce; ein Pong gilt nur für eine gesendete, noch nicht bestätigte Nonce. Jede gültige Antwort erneuert den Timeout-Zeitpunkt.
- Persistenz: keine; Sitzungen und Nonces sind flüchtig.
- UI-Aktionen: keine.
- Netzwerk-Sync: Client sendet dieselbe Nonce zurück; der Server kann ältere noch ausstehende Nonces akzeptieren.
- Konfliktbereich: paralleler Heartbeat-Sender und einzelner Nachrichten-Reader; gemeinsame Zähler/Zeitstempel werden atomar gelesen und geschrieben.
- Performance-Risiko: keine zusätzlichen Netzwerkpakete oder Listen; konstante Arbeit pro Pong.
- Tests: Pong nach zwei Pings, doppelte/ungesendete Antwort, bestehende Timeouts; SIGTERM an eigenen Serverprozess.
- Rollback: Serveränderung auf dem Arbeitsbranch zurücknehmen; keine Protokolländerung.
- Begründung: Das alte Gleichheitskriterium verglich nur mit dem letzten Ping und verwarf eine regulär verspätete Antwort.
- Alternativen: Ein einzelner ausstehender Ping; vermeidet Verwechslung, würde bei Netzverzögerung keine neuen Pings senden und erfordert anderes Timeout-Modell. Gewählter Zähler erhält das bestehende Sendeschema.
