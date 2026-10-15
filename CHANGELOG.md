# Änderungsprotokoll

## Unreleased

### Steam-Verbindung (lokal integrierte Vorschau)

- Der Mod kann eine Steam-Freundeslobby anlegen und Einladungen im laufenden Spiel entgegennehmen. Die Netzwerkdaten sollen über Steam P2P zum bisherigen autoritativen Lobby-Kern gelangen; dafür ist keine Portfreigabe am Heimrouter vorgesehen. Mod-Build sowie Handshake und Lobby-Beitritt über einen eingespeisten Stream sind getestet. Der Betrieb mit zwei Steam-Konten und das RimWorld-Fenster selbst sind noch nicht geprüft; das bestehende ZIP enthält diese Änderung noch nicht.

### KI-Projektregeln

- Ein Fortsetzungsprompt lädt die drei ursprünglichen Anweisungen und führt eine neue KI-Sitzung zum belegten Stand und offenen RimWorld-UI-Test.

- Die ursprüngliche Überprompt und beide Masterprompts sind jetzt wortgetreu mit Prüfsummen als historische Quellen dokumentiert. `AGENTS.md` und die Governance-2.0-Dateien bleiben die geltenden Arbeitsregeln.
- Ein erneuter grafischer RimWorld-Start mit isolierter Modliste wurde nach 180 Sekunden beendet. Das Mod-Laden und die UI bleiben ungeprüft; `Version.txt` und Runtime melden unterschiedliche Revisionen.

### Integrationstests

- Ein neuer Mehrprozess-Test startet den Server und zwei Clients als getrennte Programme. Er prüft Lobby-Liste, Beitritt, identische Zustände, Ready und Disconnect über TCP; alle sieben Testschritte bestanden.

- Reconnect ohne doppelte Lobby-Mitgliedschaft, Ablehnung falscher Protokollversion und Übertragung der Host-Rechte nach Disconnect mit echten TCP-Clients nachgewiesen. RimWorld-Spieltests bleiben offen.

### RimWorld-Client

- Die Lobby kann jetzt direkt aus der Mod gehostet werden, ohne ein separates Serverprogramm zu starten. Gäste verbinden sich mit der LAN-IP des Hosts. Der integrierte und der dedizierte Modus nutzen denselben autoritativen Lobby-Kern. Direkt-Hosting, Gastbeitritt, Ready und Host-Stopp wurden über TCP getestet; die Spieloberfläche bleibt ungeprüft. Das ZIP enthält dafür jetzt fünf eigene Assemblies.

- Ein installierbares Test-ZIP für RimWorld 1.6 mit Mod-Ordner, drei eigenen DLLs und deutscher Anleitung erstellt. Release-Build und Entpacken in einen `RimWorld/Mods/`-Testpfad geprüft; der Spiel-Laufzeittest ist weiterhin offen.

- Einen echten RimWorld-1.6-Mod-Adapter mit Verbindungs- und Lobby-Bedienung in den Mod-Einstellungen ergänzt. Die Assembly baut gegen die lokale Spielinstallation und das Paket ist installierbar; die UI im laufenden Spiel ist noch nicht geprüft.
- Eine spielunabhängige Client-Nachrichtenpumpe beantwortet Server-Heartbeats und stellt Lobby-Updates bereit. Drei TCP-Testgruppen bestanden.

### Dedicated Server

- Der Server akzeptiert jetzt eine verspätete Antwort auf einen bereits gesendeten Ping, solange sie noch nicht bestätigt wurde. Dadurch wird eine gültige Verbindung bei kurzem Netzwerkverzug nicht fälschlich getrennt; doppelte und ungesendete Antworten bleiben ungültig. Unter Linux beendet SIGTERM den Prozess geordnet und trennt verbundene Clients.

- Eigenständigen TCP-Lobby-Server mit konfigurierbarem Listener, Heartbeats und Shutdown ergänzt. 13 Release-Integrationstestgruppen mit zwei echten Clients bestanden; ein RimWorld-Spielclient fehlt noch.

### Lobby-Core

- Flüchtige, serverautoritative Lobbyzustände mit Kapazität, Host-Rechten, Ready-Status und Bereinigung implementiert; Regel- und Payload-Test bestanden. Die TCP-Anbindung verteilt Zustandsänderungen an die beteiligten Clients.

### Netzwerk

- TCP-Verbindung und Handshake mit Timeout ergänzt; Loopback-Tests decken echte Verbindung, gleichzeitige Sendungen und Ablehnung ab. Server-Heartbeats sind implementiert und im Integrationstest geprüft; eine Client-Nachrichtenpumpe ist ergänzt und mit drei TCP-Testgruppen geprüft.

### Protokoll

- Versioniertes, auf 64 KiB begrenztes TCP-Framing und strikte Payload-Kodierung implementiert; fünf automatisierte Testgruppen bestanden. Server und zwei Testclients verwenden das Format in echten TCP-Verbindungen.

### Governance 2.0

- Nach dem Abbruch den Git- und Dateistand erfasst und im Recovery-Bericht festgehalten.
- Projektziel 0.1, getrennte Statusdimensionen, automatische lokale Commits und verpflichtende menschliche Merge-Freigabe dokumentiert.
- Governance-, Spezifikations- und Prüfstruktur ergänzt; noch kein produktiver Multiplayer-Code.

### Dokumentation

- Vom Benutzer bereitgestellte RimWorld-Wissensbasis aus dem geprüften Archiv im Repository zugänglich gemacht.
- Projektgedächtnis, Aufgabenstatus, Kontextindex, Architektur- und Dateikarte, Sitzungsübergabe, Entscheidung, Prüfbericht und Review-Unterlage angelegt.
- Lokale RimWorld-Datei `Version.txt` (`1.6.4871 rev598`) und Steam-Build `23969874` erfasst. Ein späterer grafischer Start meldete `rev600`, erreichte aber keinen belegten Mod-Ladevorgang.
