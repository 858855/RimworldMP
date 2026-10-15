# Änderungsprotokoll

## Unreleased

### Lobby 0.2-Vorschau (lokaler Arbeitsbranch `feat/lobby-hardening`)

- In der Lobby werden Spielversion, Mods, Ladereihenfolge und Mod-Inhalte mit dem Host verglichen. Abweichende Spieler werden rot markiert und können nicht bereit sein; ein Bericht zeigt, was fehlt, zusätzlich ist, sich unterscheidet oder falsch sortiert ist. Die Lobbyliste zeigt vorab, ob die Mods zum Host passen.
- Der Host hat einen **Spiel starten**-Knopf, der erst bei mindestens zwei bereiten Spielern mit passenden Mods aktiv wird. Danach ist die Lobby gesperrt; die gemeinsamen Einrichtungsseiten folgen später, bis dahin kann nur abgebrochen werden.
- Spieler melden sich mit einem dauerhaften Schlüssel an (SteamID oder gespeicherte GUID). Eine erneute Anmeldung mit demselben Schlüssel ersetzt die alte Sitzung.
- Große Daten (zuerst die Mod-Liste, später Welt und Spielstand) können blockweise mit Prüfsumme zwischen Lobbymitgliedern übertragen werden.
- Wire-Version 3: nicht mit älteren Ständen kombinierbar.

### Gesamtplan (Arbeitsbranch `docs/full-multiplayer-plan`)

- Neuer Planentwurf `docs/plans/rimworldmp-full-plan.md`: Mod- und Ladereihenfolgeprüfung in der Lobby, Start-Knopf, gemeinsame Spieleinrichtung mit Weltübertragung, Startort-Abstimmung und Charakterwahl, deterministische Lockstep-Gameplay-Synchronisation, Speichern/Beitritt/Reconnect, Desync-Erkennung und -Reparatur, öffentliche Sync-API sowie Meilensteine bis 1.0. Keine Funktionsänderung; die 35 Meilensteine in `TASKS.yaml` stehen auf `planned`, `PLAN-001` selbst wartet auf Review.

### Hauptmenü und Lobby-Chat (Arbeitsbranch `feat/main-menu-chat`)

- Der Multiplayer-Einstieg wird in RimWorlds eigene Hauptmenü-Liste direkt nach „Tutorial“ eingefügt. Der Benutzer bestätigte Position und Fensteröffnung im Spiel. Ein eigenes Verbindungsfenster zeigt Steam-Einladungen zuerst und darunter die manuelle IP-Verbindung; dessen übrige Bedienung ist noch ungeprüft.
- In der Lobby ist rechts ein flüchtiger Chat vorgesehen. Der Server weist Namen und Sitzung zu und verteilt Nachrichten ausschließlich an Mitglieder derselben Lobby. Zwei TCP-Clients, Nichtmitgliedschaft, Lobby-Isolation und ungültiger Text wurden automatisiert geprüft. Die Chat-Bedienung in RimWorld ist noch offen.
- Die Wire-Version wurde für den Chat auf 2 erhöht. Beide Spieler und ein optionaler Dedicated Server brauchen denselben neuen Build. Die manuell installierbare Vorschau enthält den gepinnten Harmony-Baustein samt MIT-Lizenz.

### Steam-Verbindung (lokal integrierte Vorschau)

- Der Mod kann eine Steam-Freundeslobby anlegen und Einladungen im laufenden Spiel entgegennehmen. Die Netzwerkdaten sollen über Steam P2P zum bisherigen autoritativen Lobby-Kern gelangen; dafür ist keine Portfreigabe am Heimrouter vorgesehen. Mod-Build sowie Handshake und Lobby-Beitritt über einen eingespeisten Stream sind getestet. Der Benutzer meldet eine erfolgreiche Steam-Verbindung; Umfang und Umgebung des Tests sind noch nicht dokumentiert. Das reguläre ZIP enthält diese Änderung nicht, das gesonderte Vorschau-ZIP schon.

### Internet-Verbindung

- Verbindungen über das Internet funktionieren jetzt. Bisher lauschten Direkt-Host und Server nur auf `127.0.0.1` und waren damit nur vom eigenen Rechner aus erreichbar. Standard ist jetzt `::` (IPv4 und IPv6 auf allen Schnittstellen); gespeicherte alte Host-IP-Einstellungen werden dafür einmalig ersetzt.
- Der Client löst Hostnamen auf, verbindet sich über IPv4 oder IPv6 und versucht bei mehreren Adressen die nächste. Das Server-Feld akzeptiert `host:port` und `[IPv6]:port`.
- Beim Direkt-Hosten öffnet die Mod optional per UPnP den Router-Port, zeigt die Adressen für Mitspieler an (Internet, LAN, VPN, IPv6), erkennt fehlende öffentliche IPv4 (CGNAT/DS-Lite) und entfernt die Freigabe beim Trennen. Alle sieben Testprojekte bestanden; eine echte Verbindung aus dem Internet ist noch nicht geprüft.
- Im ersten Spieltest scheiterte UPnP an einer FRITZ!Box mit „"utf-8" is not a supported encoding name“ (Mono liest `charset="utf-8"` mit Anführungszeichen nicht), und „Adresse kopieren“ lieferte dadurch die LAN-Adresse. Antworten werden jetzt als Bytes gelesen; die öffentliche IP wird vor der Portfreigabe abgefragt und auch bei Fehlschlag angezeigt und kopiert. Virtuelle Adapter (VMware/VirtualBox) erscheinen nicht mehr in der Adressliste.

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
