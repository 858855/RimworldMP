# Änderungsprotokoll

## Unreleased

### RimWorld-Client

- Einen echten RimWorld-1.6-Mod-Adapter mit Verbindungs- und Lobby-Bedienung in den Mod-Einstellungen ergänzt. Die Assembly baut gegen die lokale Spielinstallation und das Paket ist installierbar; die UI im laufenden Spiel ist noch nicht geprüft.
- Eine spielunabhängige Client-Nachrichtenpumpe beantwortet Server-Heartbeats und stellt Lobby-Updates bereit. Drei TCP-Testgruppen bestanden.

### Dedicated Server

- Eigenständigen TCP-Lobby-Server mit konfigurierbarem Listener, Heartbeats und Shutdown ergänzt. Zehn Release-Integrationstestgruppen mit zwei echten Clients bestanden; ein RimWorld-Spielclient fehlt noch.

### Lobby-Core

- Flüchtige, serverautoritative Lobbyzustände mit Kapazität, Host-Rechten, Ready-Status und Bereinigung implementiert; Regel- und Payload-Test bestanden. Die TCP-Anbindung verteilt Zustandsänderungen an die beteiligten Clients.

### Netzwerk

- TCP-Verbindung und Handshake mit Timeout ergänzt; Loopback-Tests decken echte Verbindung, gleichzeitige Sendungen und Ablehnung ab. Server-Heartbeats sind implementiert und im Integrationstest geprüft; die dauerhafte Client-Nachrichtenpumpe fehlt.

### Protokoll

- Versioniertes, auf 64 KiB begrenztes TCP-Framing und strikte Payload-Kodierung implementiert; fünf automatisierte Testgruppen bestanden. Server und zwei Testclients verwenden das Format in echten TCP-Verbindungen.

### Governance 2.0

- Nach dem Abbruch den Git- und Dateistand erfasst und im Recovery-Bericht festgehalten.
- Projektziel 0.1, getrennte Statusdimensionen, automatische lokale Commits und verpflichtende menschliche Merge-Freigabe dokumentiert.
- Governance-, Spezifikations- und Prüfstruktur ergänzt; noch kein produktiver Multiplayer-Code.

### Dokumentation

- Vom Benutzer bereitgestellte RimWorld-Wissensbasis aus dem geprüften Archiv im Repository zugänglich gemacht.
- Projektgedächtnis, Aufgabenstatus, Kontextindex, Architektur- und Dateikarte, Sitzungsübergabe, Entscheidung, Prüfbericht und Review-Unterlage angelegt.
- Lokale RimWorld-Installation `1.6.4871 rev598` und Steam-Build `23969874` erfasst; kein Spiel- oder Multiplayer-Test behauptet.
