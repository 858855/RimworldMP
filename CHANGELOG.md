# Änderungsprotokoll

## Unreleased

### Erster Spielbefehl (Arbeitsbranch `codex/command-pipeline`, X4)

- Draft/Undraft aus dem Spiel-UI wird als geplanter Befehl mit Karten- und Pawn-ID gesendet und an der gemeinsamen Tickgrenze ausgeführt. Fehlt die Figur beim Ausführen, wird der Befehl protokolliert und ausgelassen. Codec-Roundtrip und Mod-Build bestanden; das Verhalten in zwei laufenden RimWorld-Clients ist noch nicht geprüft. Weitere Spielaktionen bleiben offen.
- Forbid/Unforbid verwendet nun denselben Befehlsweg mit Karten- und Thing-ID. Der Codec und die lokale 1.6-API wurden geprüft; die tatsächliche Erfassung im laufenden Spiel bleibt offen.
- Ein einfacher Goto-Auftrag wird mit Zielzelle, JobTag und Queue-Flag geordnet übertragen. Noch nicht kodierte Spieleraufträge werden in der gemeinsamen Sitzung abgewiesen, damit sie nicht nur lokal wirken. Bewegung und Pfadsuche sind im Spiel weiterhin ungeprüft.

### Zeitgrenze beim Laden (Arbeitsbranch `claude/snapshot-timeout`)

- Lädt ein beitretender oder zu reparierender Spieler den Spielstand des Hosts nicht binnen drei Minuten, wartet die Partie nicht mehr auf ihn. Er wird aus Spiel und Lobby genommen und kann sofort neu beitreten; die anderen spielen weiter.

### Sicherheit für IP-Lobbys (Arbeitsbranch `claude/ip-password`, SEC-001)

- Lobbys können ein Passwort haben; die Lobbyliste zeigt ein Schloss.
- Niemand kann sich über IP als ein anderer Spieler ausgeben. Jeder Spieler hat ein eigenes Schlüsselpaar und unterschreibt seine Anmeldung. Der Host merkt sich beim ersten Kontakt, welcher Schlüssel zu wem gehört, und weist danach jeden ab, der dieselbe Kennung mit einem anderen oder ohne Schlüssel behauptet. Über Steam gilt das ohnehin schon.
- Im Spiel noch nicht aktiv: Schlüssel erzeugen und speichern sowie das Passwortfeld gehören zur Spielseite.

### Abstimmung im Coop-Demokratie-Modus (Arbeitsbranch `claude/vote`, VOTE-001)

- Verlangt ein Ereignis eine Entscheidung, etwa ein Wanderer will beitreten, öffnet der Host eine Abstimmung. Die Zeit hält an, bis entschieden ist. Die Mehrheit gewinnt, bei Gleichstand der Host. Ist jemand nicht am Rechner, kann der Host vorzeitig beenden; wer nicht abgestimmt hat, gilt als enthalten.
- Alle Spiele wenden das Ergebnis an derselben Stelle an.
- Im Spiel noch nicht aktiv: Abstimmungsfenster und Erkennen der Ereignisse gehören zur Spielseite.

### Desync-Reparatur (Arbeitsbranch `claude/resync`, DESYNC-003)

- Ein Desync beendet die Partie nicht mehr. Das abweichende Spiel lädt den Stand des Hosts neu, holt auf, und die Zeit läuft von selbst weiter; alle sehen „Desync behoben“. In einer Simulation mit echter Abweichung blieben danach alle Spiele identisch.
- Weicht ein Spiel dreimal hintereinander ab, gibt der Koordinator auf und lässt die Zeit angehalten; der Host entscheidet.
- Im Spiel noch nicht aktiv: Neuladen und Anzeigen gehören zur Spielseite.

### Spielseite für Save/Join, C2-Reparatur und Zustands-Hash (lokaler Arbeitsbranch `codex/resync-game`)

- Nach dem Merge der Koordinatorarbeit auf `main` wurde die Spielseite darauf rebasiert. Das erste Hashfenster nach Beitritt oder Reparatur wird auch bei einer 60-Tick-Grenze ausgelassen. Speicherbefehle behalten ihre Position zwischen Spielbefehlen; nachfolgende Befehle warten, bis der Save abgeschlossen ist. Ein gecachter Gesundheitswert und Fraktionsbeziehungen zu sich selbst wurden aus dem Vergleichshash entfernt.
- Geplante Speicherbefehle unterbrechen den Gleichlauf nicht mehr. Alle Spieler speichern am selben Tick; der Host kann seinen Stand als Snapshot gezielt an einen neuen Spieler senden. Der neue Spieler lädt ihn, holt auf und zeigt seinen Fortschritt an. Nach einer Desync-Meldung bleibt ein Neuladen möglich.
- Die Spielzufallsfolge wird je gemeinsamem Tick neu gesetzt. Der Vergleichshash berücksichtigt nun die im Entwicklungsplan genannten Dinge, Pawns, Fraktionen und Quests sowie die Rand-Ziehungen. Vanilla-Autosaves außerhalb der Koordinator-Regel sind während der Sitzung abgeschaltet.
- Nach dem Laden bestätigt der Client den Snapshot; nur betroffene Spieler warten bei einer Desync-Meldung auf die Reparatur. Erfolg und Abbruch der Reparatur werden angezeigt.
- Der Mod-Build und spielunabhängige Koordinator-/Host-Tests bestanden. Save, Join, Wiederverbindung, C2-Reparatur und Hash-Gleichheit in zwei RimWorld-Instanzen sind noch nicht geprüft.

### Speichern und Beitritt im laufenden Spiel (Arbeitsbranch `claude/save-join`, SAVE-001/JOIN-001)

- Alle Spiele speichern an derselben Stelle: automatisch einmal pro Spieltag oder wenn der Host speichert.
- Spieler können einem laufenden Spiel beitreten oder nach einem Abbruch zurückkehren. Der Host speichert dafür an einer festen Stelle; der Neue lädt diesen Stand, holt auf und läuft danach im Gleichschritt mit. Die anderen spielen währenddessen weiter. In einer Simulation führte der Neue danach exakt dieselben Befehle aus wie alle anderen.
- Wer andere Mods hat, kann nicht beitreten.
- Im Spiel noch nicht aktiv: Speichern, Laden und Aufholen gehören zur Spielseite.

### Tick-Treiber (Arbeitsbranch `codex/tick-driver`, X2)

- Nach der gemeinsamen Landung übernimmt der Spielclient den Tick-Horizont des Hosts. Er verarbeitet Befehle auch während einer Pause und lässt einen Tick nur nach Freigabe durch `LockstepClient` zu. Bei fehlendem Ausführer für Spielbefehle stoppt er mit einer Fehlermeldung.
- Pause und Geschwindigkeit werden als Anfragen an den Koordinator gesendet. Das Spieloverlay zeigt Sitzungstick, Pause und den Spieler, auf dessen Fortschritt gewartet wird. Alle 60 Ticks wird ein vorläufiger Kernhash gemeldet.
- Der RimWorld-1.6-Mod baut ohne Warnungen; Koordinator- und Client-Tests bestanden. Die Spielbedienung und der Gleichlauf mit zwei RimWorld-Clients sind noch nicht geprüft.

### Spielmodus (Arbeitsbranch `feat/game-mode`, MODE-001)

- Der Host wählt beim Erstellen der Lobby einmalig den Modus: **Coop** (eine gemeinsame Kolonie; Ereignisse entscheidet der Host oder, als Demokratie, die Mehrheit mit dem Host bei Gleichstand) oder **Fraktionen** (jeder seine eigene Kolonie; Beziehung verbündet, neutral oder verfeindet). Danach lässt er sich nicht mehr ändern.
- Spieler sehen den Modus schon in der Lobbyliste. Der Fraktionsmodus ist wählbar, aber noch nicht startbar.
- Die Auswahl im Lobby-Fenster folgt auf der Spielseite.

### Lockstep-Koordinator (Arbeitsbranch `feat/lockstep-session`, SYNC-001/DESYNC-001)

- Der Kern der Synchronität: Der Host ordnet alle Spielerbefehle in eine feste Reihenfolge und gibt vor, bis zu welchem Tick jedes Spiel rechnen darf. Alle Spiele führen dieselben Befehle an derselben Stelle aus. In einer Simulation mit drei Spielen, eines davon fünfmal langsamer, ist das in sechs Läufen nachgewiesen.
- Der langsamste Spieler bestimmt das Tempo. Pausieren und Fortsetzen darf jeder, die Geschwindigkeit nur der Host. Befehle im Pausenmodus wirken sofort.
- Weichen die Spielstände auseinander, wird das erkannt und die Zeit für alle an derselben Stelle angehalten, statt still weiterzulaufen.
- Im Spiel noch nicht aktiv: Die Spielseite (Tick-Treiber, Befehlserfassung, Zustands-Hash) folgt.

### Mod-Prüfung, Start-Knopf und Seitenspiegel (Arbeitsbranch `feat/mod-check-setup-ui`, Basis `feat/setup-rules`)

- In der Lobby werden Mods, Ladereihenfolge und Mod-Inhalte mit dem Host verglichen; die Spielversion wird nur angezeigt. Abweichende Spieler sind rot markiert und können nicht bereit sein; ein Bericht zeigt die Unterschiede. Die Lobbyliste zeigt vorab, ob die Mods passen.
- Der Host hat **Spiel starten**; daneben steht, was noch fehlt. Danach öffnet sich beim Host die normale Spieleinrichtung, und die Mitspieler sehen seine Auswahl live. Der letzte Start geht erst, wenn alle fertig sind.
- Debug-Option zum Testen: Der Host kann die Einrichtung auch allein starten (Kontrollkästchen „Debug: Spiel allein starten erlauben“).
- In der Startortwahl sehen alle die Weltkarte und können je ein Fähnchen als Vorschlag setzen; nur der Host wählt den Startort.
- Gemeinsamer Spielstart: Der Host startet das Spiel, speichert es und sendet den Spielstand an alle; Host und Gäste laden ihn und prüfen per Fingerabdruck, dass Ort, Figuren und Besitzer gleich sind. Danach bleibt das Spiel für alle pausiert, bis der Gleichlauf kommt.
- In der Charakterwahl übernimmt jeder Spieler eine eigene Figur; er kann sie neu würfeln, mit einer zurückgelassenen tauschen und umbenennen und meldet sich fertig. Erst wenn alle fertig sind, geht es weiter. Die Startgruppe hat mindestens eine Figur je Spieler.
- Der Host sendet die erzeugte Welt an alle Mitspieler; jeder lädt sie und bestätigt mit einem Fingerabdruck, dass er dieselbe Welt hat. Erst dann geht es zur Startortwahl. Ein Selbsttest (`-mpWorldSelfTest`) prüft Speichern und Laden ohne Mitspieler.

### Regeln der Spieleinrichtung (Arbeitsbranch `feat/setup-rules`)

- Der Host-Server führt die Phasen der gemeinsamen Spieleinrichtung und setzt die Regeln selbst durch:
  - Start erst, wenn alle bereit sind.
  - Nur der Host schaltet weiter.
  - Während der Einrichtung kann niemand beitreten.
  - Jeder setzt ein Fähnchen, nur der Host wählt den Startort.
  - Die Charakterwahl geht erst weiter, wenn alle fertig sind; der Host kann nichts erzwingen.
- Ein manipulierter Gast kann keine dieser Regeln umgehen; das ist auch über das Netz getestet.
- Startorte werden mit Planetenebene übertragen, weil RimWorld 1.6 Felder so adressiert.
- Im Spiel ist das noch nicht sichtbar; Knöpfe, Weltkarte und Charakterfenster folgen.

### Spielerkennung (Arbeitsbranch `feat/player-key`, ID-001)

- Jeder Spieler hat jetzt eine feste Kennung: mit Steam die SteamID, ohne Steam eine einmal erzeugte und gespeicherte Kennung. Sie ist die Grundlage für Charakterwahl und Pawn-Besitz.
- Über Steam kann sich niemand als anderer Spieler ausgeben: Der Host prüft die Kennung gegen das, was Steam selbst bestätigt hat. Jeder Spieler kann nur einmal gleichzeitig verbunden sein.
- Behoben: Abgelehnte Steam-Gäste sahen statt des Grunds nur „Server closed during handshake“. Ebenfalls behoben: Nach dem Trennen dauerte es bis zu 5 Sekunden, bis die anderen „Spieler hat verlassen“ sahen.
- Beide Spieler brauchen denselben Build.
- Entscheidungen des Benutzers: Startort per Fähnchen, nur der Host wählt; Charakterwahl erst weiter, wenn alle fertig sind (ADR 0015).

### Chunk-Kanal (Arbeitsbranch `feat/chunk-transfer`, XFER-001)

- Große Daten wie Welt und Spielstand lassen sich jetzt zwischen Lobbymitgliedern übertragen: in Stücken, mit SHA-256-Prüfung und mit Gegendruck. Ein Steam-Gast kann dadurch nicht überlaufen. Das ist die Grundlage für Weltübertragung, Spielstart und späteren Beitritt; im Spiel wird es noch nicht genutzt.
- Gegenüber dem Gesamtplan ist das Fenster kleiner (6 statt 32 Stücke). Die Plan-Zahl hätte Steam-Verbindungen abbrechen lassen; eine Gegenprobe im Test bestätigt das. Begründung in ADR 0014.
- Entscheidung des Benutzers: Der vollständige Multiplayer baut auf dem Steam-Listen-Host mit Einladung auf, nicht auf dem Dedicated Server (ADR 0013).
- Wire-Version 3: Beide Spieler brauchen denselben Build.

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
