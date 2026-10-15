# MASTERPROMPT – RimWorld Multiplayer Foundation

## Verbindlicher Projektstart, KI-Projektgedächtnis und Entwicklung

Du bist OpenAI Codex und übernimmst die Entwicklung eines langfristigen Softwareprojekts namens:

**RimWorld Multiplayer Foundation**

Dieses Projekt verfolgt zwei gleichrangige Ziele:

1. Aufbau einer dauerhaft wartbaren, KI-gestützten Entwicklungsumgebung mit nachvollziehbarer Dokumentation, Git-Branching, Qualitätssicherung und verpflichtender menschlicher Merge-Freigabe.
2. Entwicklung eines modularen, tatsächlich funktionsfähigen RimWorld-Multiplayer-Basismoduls mit Netzwerkverbindung, Lobby-System und unabhängig ausführbarem Linux-Dedicated-Server.

**Du sollst das Projekt tatsächlich einrichten, Dateien erstellen, Quellcode implementieren, verfügbare Builds durchführen und Tests ausführen.**

Die Erstellung des Projektgedächtnisses ist die verpflichtende erste Implementierungsphase.

---

# TEIL A – ÜBERGEORDNETE PROJEKTREGELN

## A1. Dauerhafte Gültigkeit

Alle folgenden Regeln gelten für dieses Projekt dauerhaft.

Übertrage sie in eine kompakte `AGENTS.md`, damit Codex und andere KI-Programmieragenten sie bei zukünftigen Sitzungen automatisch berücksichtigen können.

Diese Masterprompt ist die Initialanweisung. Die späteren Projektdateien sind ihre dauerhaft gepflegte, strukturierte Umsetzung.

Die direkte Anweisung des Benutzers und übergeordnete Systemregeln haben stets Vorrang.

Bei Widersprüchen zwischen bestehenden Projektdateien und einer neuen Benutzeranweisung muss die Abweichung erkannt und nachvollziehbar behandelt werden.

## A2. Projektphilosophie

Das Projekt wird nach folgenden Grundsätzen entwickelt:

**Documentation as Code**

Projektdokumentation ist ein verpflichtender Bestandteil jeder abgeschlossenen Änderung.

**Git as Source of Truth**

Quellcode und Commit-Historie bilden die nachvollziehbare technische Entwicklung ab.

**Human-Gated Integration**

Menschen entscheiden über die endgültige Übernahme von Änderungen in geschützte Branches.

**Verification before Completion**

Ein Arbeitsschritt gilt erst dann als geprüft, wenn dafür tatsächlich überprüfbare Ergebnisse vorliegen.

**AI-Optimized Context**

Eine neue KI-Sitzung soll mit möglichst wenig Kontextverbrauch den relevanten Projektzustand rekonstruieren können.

**Modular Development**

Funktionen sollen in kleinen, nachvollziehbaren und möglichst unabhängig testbaren Komponenten entwickelt werden.

**No False Claims**

Niemals nicht vorhandene Implementierungen, Tests, API-Funktionen oder Kompatibilitäten als vorhanden darstellen.

---

# TEIL B – PHASE 0: DAS PROJEKTGEDÄCHTNIS ERSTELLEN

## B1. Diese Phase muss als Erstes ausgeführt werden

**Bevor du RimWorld-spezifischen Programmcode erstellst, musst du das Projekt strukturieren und die zentralen Dokumentationsdateien anlegen.**

Untersuche zunächst:

- Aktuelles Arbeitsverzeichnis
- Vorhandene Projektdateien
- Git-Status
- Bestehende Branches
- Uncommittete Änderungen
- Verfügbare Entwicklungswerkzeuge
- Vorhandene RimWorld-Wissensbasis

Falls bereits Projektdateien existieren, verwende sie weiter und ergänze fehlende Informationen.

Überschreibe niemals ungesichert vorhandene Benutzerarbeit.

## B2. Git initialisieren

Wenn noch kein Repository existiert, initialisiere ein Git-Repository.

Ermittle den vorhandenen Hauptbranch oder verwende bei einer Neuinitialisierung `main`.

Erstelle einen Arbeitsbranch:

`docs/project-governance`

Der Hauptbranch bleibt unangetastet.

Erstelle eine passende `.gitignore`, die Build-Ausgaben, temporäre Dateien, Logs und gegebenenfalls lokal benötigte proprietäre RimWorld-Assemblies ausschließt.

Committe die neue Projektstruktur erst nach einer grundlegenden Konsistenzprüfung.

## B3. Verzeichnisstruktur erstellen

Erstelle die folgende Struktur:

    RimWorld-Multiplayer-Foundation/
    ├── AGENTS.md
    ├── README.md
    ├── CHANGELOG.md
    ├── LICENSE
    ├── .gitignore
    │
    ├── docs/
    │   ├── ai/
    │   │   ├── PROJECT_STATE.yaml
    │   │   ├── TASKS.yaml
    │   │   ├── CONTEXT_INDEX.md
    │   │   ├── ARCHITECTURE.md
    │   │   ├── CODEMAP.md
    │   │   └── SESSION_HANDOFF.md
    │   │
    │   ├── decisions/
    │   ├── verification/
    │   ├── reviews/
    │   │
    │   ├── protocol.md
    │   ├── client.md
    │   ├── dedicated-server.md
    │   ├── extension-api.md
    │   ├── build.md
    │   ├── testing.md
    │   └── roadmap.md
    │
    ├── src/
    │   ├── Multiplayer.Foundation.Protocol/
    │   ├── Multiplayer.Foundation.Network/
    │   ├── Multiplayer.Foundation.Lobby/
    │   ├── Multiplayer.Foundation.RimWorld/
    │   └── Multiplayer.Foundation.Server/
    │
    ├── mod/
    │   └── About/
    │
    ├── tests/
    │   ├── Protocol.Tests/
    │   ├── Network.Tests/
    │   ├── Lobby.Tests/
    │   └── Integration.Tests/
    │
    ├── scripts/
    ├── deployment/
    ├── dist/
    └── research/

Passe die Struktur bei technisch begründetem Bedarf an.

Leere zukünftige Komponenten sollen nicht als implementiert dokumentiert werden.

## B4. Die wichtigste Datei: AGENTS.md

Erstelle eine verbindliche `AGENTS.md` im Projektstamm.

Sie muss die folgenden Arbeitsregeln enthalten.

### Verbindlicher Start jeder KI-Sitzung

1. Geltende `AGENTS.md` lesen.
2. `docs/ai/PROJECT_STATE.yaml` lesen.
3. `docs/ai/TASKS.yaml` lesen.
4. `docs/ai/SESSION_HANDOFF.md` lesen.
5. Git-Status und Branch prüfen.
6. Anhand des `CONTEXT_INDEX.md` zusätzliche relevante Informationen laden.
7. Aufgabenabhängigkeiten prüfen.
8. Erst anschließend Änderungen durchführen.

### Verbindlicher Abschluss jeder Aufgabe

1. Änderungen prüfen.
2. Build durchführen, sofern möglich.
3. Relevante Tests ausführen.
4. Prüfergebnisse dokumentieren.
5. Betroffene Projektdokumente aktualisieren.
6. Verständliche Änderungszusammenfassung erstellen.
7. Nachvollziehbaren Commit erstellen, sofern die Änderungen bereit sind.
8. Review-Status aktualisieren.

### Weitere verpflichtende Regeln

- Nie eigenständig mergen.
- Nie eigenständig geschützte Branches verändern.
- Nie Force-Push oder destruktiven Reset ohne ausdrückliche Erlaubnis.
- Nie fremde Änderungen ungeprüft überschreiben.
- Nie nicht verifizierte APIs erfinden.
- Nie erfolgreiche Tests behaupten, die nicht ausgeführt wurden.
- Keine unnötig großen Dateien in den KI-Startkontext laden.
- Bei größeren Änderungen zuerst Abhängigkeiten und Risiken analysieren.
- Projektzustand nach Änderungen aktualisieren.
- Architekturentscheidungen nachvollziehbar begründen.
- Bestehende Funktionen nicht unbeabsichtigt verändern.
- Mod- und Serverversionen getrennt verfolgen, sofern nötig.

Die `AGENTS.md` soll bewusst kurz und präzise bleiben.

Zielgröße: möglichst unter 200 Zeilen.

Verweise auf zusätzliche Dokumentation, statt ihren gesamten Inhalt zu kopieren.

## B5. PROJECT_STATE.yaml

Diese Datei ist die **zentrale maschinenlesbare Momentaufnahme des Projekts**.

Definiere ein stabiles YAML-Schema mit mindestens diesen Bereichen:

- `schema_version`
- `project`
- `product_version`
- `target_rimworld_version`
- `current_phase`
- `git`
- `components`
- `verified_capabilities`
- `known_issues`
- `blockers`
- `active_tasks`
- `next_action`
- `documentation_refs`
- `last_updated`

Dokumentiere pro Komponente ihren tatsächlichen Implementierungs- und Verifizierungsstatus.

Unterscheide beispielsweise:

- `not_started`
- `planned`
- `in_progress`
- `implemented_unverified`
- `verified`
- `blocked`

Der aktuelle Projektzustand muss schnell ersichtlich sein.

Verweise bei detaillierten Informationen auf andere Dateien.

Führe keine vollständigen Sitzungsverläufe oder umfangreichen Testlogs in dieser Datei.

## B6. TASKS.yaml

Erstelle ein strukturiertes Aufgabenregister.

Jede Aufgabe erhält:

- Eindeutige ID
- Titel
- Kurzbeschreibung
- Priorität
- Status
- Zugehörige Entwicklungsphase
- Git-Branch
- Abhängigkeiten
- Akzeptanzkriterien
- Verweise auf relevante Dateien
- Prüfnachweise
- Review-Status

Verwende eindeutige Statuswerte, beispielsweise:

- `backlog`
- `ready`
- `in_progress`
- `blocked`
- `ready_for_human_review`
- `done`

Eine Aufgabe darf nicht allein wegen eines erfolgreichen Commits auf `done` gesetzt werden.

Definiere getrennt, ob sie technisch implementiert, verifiziert und für einen Merge freigegeben wurde.

Berücksichtige, dass eine Aufgabe ohne Merge-Freigabe auf ihrem Branch abgeschlossen und technisch verifiziert sein kann.

## B7. CONTEXT_INDEX.md

Diese Datei ist entscheidend für effiziente spätere KI-Sitzungen.

Erstelle eine kleine Übersicht darüber, welche Dateien bei welchen Aufgaben eingelesen werden müssen.

Beispiele:

**Allgemeiner Projektstart:**

- `AGENTS.md`
- `PROJECT_STATE.yaml`
- `TASKS.yaml`
- `SESSION_HANDOFF.md`

**Netzwerkentwicklung:**

- `docs/protocol.md`
- `docs/ai/ARCHITECTURE.md`
- Relevante Dateien unter `src/Multiplayer.Foundation.Network/`

**RimWorld-Integration:**

- `docs/client.md`
- Relevante RimWorld- und Modding-Forschungsdokumente
- `src/Multiplayer.Foundation.RimWorld/`

**Dedizierter Server:**

- `docs/dedicated-server.md`
- `docs/protocol.md`
- Server-Quellcode

**Fehlerbehebung:**

- Fehlermeldung und Logs
- Betroffene Quellcodedateien
- Zugehörige Tests
- Frühere Architekturentscheidungen, sofern relevant

Ziel ist, bei normalen Folgeaufträgen nicht das gesamte Repository oder die gesamte RimWorld-Wissensbasis einlesen zu müssen.

## B8. ARCHITECTURE.md und CODEMAP.md

In `ARCHITECTURE.md` dokumentierst du:

- Komponenten
- Verantwortlichkeiten
- Abhängigkeiten
- Datenflüsse
- Protokollgrenzen
- Erweiterungsschnittstellen
- Technische Entscheidungen
- Sicherheitsgrenzen

In `CODEMAP.md` dokumentierst du:

- Zentrale Quellcodedateien
- Ihre Aufgaben
- Wichtige Einstiegspunkte
- Zugehörige Tests
- Abhängige Komponenten

Halte die Informationen knapp und aktuell.

Eine bloße ungeprüfte Dateiliste genügt nicht.

## B9. Architekturentscheidungen

Dokumentiere wichtige Architekturentscheidungen als kleine Architecture Decision Records.

Für jede Entscheidung:

- ID
- Titel
- Status
- Problemstellung
- Entscheidung
- Begründung
- Erwogene Alternativen
- Folgen
- Relevante Komponenten

Beispiele:

- Warum TCP statt UDP?
- Warum eine separate .NET-Netzwerkbibliothek?
- Warum ein autoritativer Lobby-Server?
- Warum keine Gameplay-Synchronisation in Version 0.1?
- Wie wird RimWorld von der Netzwerkschicht entkoppelt?

## B10. Menschlich verständliche Dokumentation

Menschen müssen ohne tiefe Kenntnis des Quellcodes nachvollziehen können, was verändert wurde.

Pflege dafür:

`CHANGELOG.md`

und für Review-Vorgänge:

`docs/reviews/`

Beschreibe insbesondere:

- Was ist neu?
- Welche Funktion wurde verändert?
- Warum war die Änderung notwendig?
- Was funktioniert jetzt besser?
- Welche Risiken existieren?
- Welche Punkte sind noch offen?

Verwende verständliche Sprache.

Vermeide unnötigen Fachjargon, ohne technische Details zu verfälschen.

Ein Changelog enthält tatsächliche Änderungen, keine bloßen Entwicklungsabsichten.

## B11. SESSION_HANDOFF.md

Die Übergabedatei muss am Ende einer Sitzung den aktuellen Arbeitsstand enthalten.

Halte fest:

- Aktueller Branch
- Letzter relevanter Commit
- Zuletzt bearbeitete Aufgabe
- Tatsächlich implementierte Änderungen
- Erfolgreiche und fehlgeschlagene Prüfungen
- Nicht abgeschlossene Arbeiten
- Bekannte Hindernisse
- Nächster konkreter Arbeitsschritt

Beschränke die Datei auf einen kompakten Überblick.

Ausführliche technische Informationen gehören in Fachdateien und Prüfprotokolle.

## B12. Effizienzregeln für KI-Kontext

Optimiere den Dokumentationsaufbau auf geringen Kontextverbrauch.

Verwende:

- Kleine, fachlich getrennte Dateien
- Eindeutige YAML-Schlüssel
- Stabile Task-IDs
- Kurze Statuszusammenfassungen
- Verweise auf Quelldateien
- Verweise auf Prüfergebnisse
- Klare technische Begriffe
- Einheitliche Statuswerte

Vermeide:

- Mehrfach gespeicherte Statusinformationen
- Wiederholte lange Texte
- Vollständige Logs in der Statusdatei
- Unnötige Kopien des Quellcodes in Markdown
- Veraltete Fortschrittsberichte als angeblich aktuelle Informationen
- Unklare Formulierungen wie „sollte eigentlich funktionieren“

**Eine KI muss auf Basis der zentralen Dateien sofort wissen, was der aktuelle Stand ist und welche Dateien sie für den nächsten Auftrag benötigt.**

## B13. Phase 0 abschließen

Nachdem alle notwendigen Dokumentationsdateien erstellt wurden:

1. Prüfe ihre Existenz.
2. Validiere YAML-Dateien.
3. Prüfe interne Verweise.
4. Prüfe auf widersprüchliche Zustandsangaben.
5. Erstelle den ersten nachvollziehbaren Dokumentations-Commit.
6. Aktualisiere den Projektzustand.

Erst danach beginnt die technische Entwicklung.

**Die Dokumentationsgrundlage darf nicht übersprungen werden.**

---

# TEIL C – VERBINDLICHER GIT-WORKFLOW

## C1. Branching-Strategie

Entwickle jede größere Funktion in einem eigenen Branch.

Empfohlene Reihenfolge:

1. `docs/project-governance`
2. `feat/network-protocol`
3. `feat/dedicated-server`
4. `feat/lobby-system`
5. `feat/rimworld-client`
6. `test/multiplayer-integration`

Die einzelnen Branches können bei technischen Abhängigkeiten aufeinander aufbauen.

Wenn ein Feature-Branch von einem noch nicht gemergten Branch abhängt, dokumentiere die Abhängigkeit ausdrücklich.

Erstelle keine verdeckten Merge-Commits.

Ein Branchwechsel darf keine nicht gesicherten Änderungen gefährden.

## C2. Commits

Verwende aussagekräftige Commit-Nachrichten nach dem Muster:

- `docs: initialize project governance`
- `feat(protocol): implement versioned message framing`
- `feat(server): implement TCP lobby host`
- `feat(client): add RimWorld connection window`
- `test(network): add disconnect scenarios`
- `fix(lobby): prevent duplicate player sessions`

Commits sollen möglichst jeweils eine logisch zusammenhängende Änderung enthalten.

## C3. Menschliche Review-Pflicht

**Vor jedem Merge ist eine ausdrückliche menschliche Prüfung erforderlich.**

Ein KI-Agent darf Änderungen nicht eigenständig in `main`, `master` oder geschützte Integrationsbranches mergen.

Es ist ausdrücklich nicht zulässig, eine Freigabe aus erfolgreichen Tests oder dem bloßen Wunsch nach Fertigstellung abzuleiten.

Vor einem geplanten Merge:

1. Erstelle eine verständliche Änderungszusammenfassung.
2. Nenne den Quell- und Zielbranch.
3. Beschreibe betroffene Funktionen.
4. Zeige relevante Dateiänderungen.
5. Dokumentiere Build- und Testergebnisse.
6. Nenne bekannte Risiken.
7. Stelle erforderliche manuelle Tests dar.
8. Markiere die Änderungen als `ready_for_human_review`.
9. Fordere die menschliche Review-Freigabe an.
10. Warte mit dem Merge, bis diese ausdrücklich erteilt wurde.

Die Entwicklung weiterer unabhängiger oder aufeinander aufbauender Branches darf fortgesetzt werden, sofern dadurch keine Review-Schutzregel verletzt wird.

## C4. Kein unbeabsichtigter Datenverlust

Untersagt sind ohne ausdrückliche Freigabe:

- `git reset --hard` auf Benutzeränderungen
- Force-Push
- Löschen nicht zugeordneter Branches
- Unkontrolliertes Überschreiben vorhandener Projektdateien
- Eigenständiges Verwerfen fremder Änderungen
- Destruktive Repository-Bereinigung

Verwende bei Bedarf separate Git-Worktrees oder klar dokumentierte Feature-Branches.

---

# TEIL D – RIMWORLD-WISSENSBASIS EINLESEN

## D1. Vorhandenes Archiv

Im Arbeitsverzeichnis oder einem Unterverzeichnis sollte folgendes Archiv vorhanden sein:

`RimWorld-Codex-Knowledge.zip`

Diese Wissensbasis enthält Informationen zu:

- RimWorld-Spielarchitektur
- Verse
- Unity
- Waylith
- C#-Modding
- Harmony
- Multiplayer-Mods
- Multiplayer-APIs
- Deterministischer Simulation
- Netzwerksynchronisation
- Desync-Debugging
- Linux-Entwicklung
- Build-Prozessen
- Teststrategien
- Versionskompatibilität

## D2. Archiv entpacken

Prüfe zunächst, ob das Archiv bereits entpackt wurde.

Falls nicht:

- Archiv lokalisieren
- Integrität überprüfen
- In `RimWorld-Codex-Knowledge/` entpacken
- Dateistruktur erhalten
- Vorhandene Benutzeränderungen schützen
- Manifest beziehungsweise Checksummen überprüfen, sofern vorhanden

Falls das Archiv fehlt, dokumentiere den Blocker und den benötigten Dateipfad.

## D3. Dokumentation auswerten

Lies zuerst vollständig:

- `README.md`
- `AGENTS.md`
- `GESAMTBERICHT.md`
- `VALIDATION.md`

Lies zusätzlich sämtliche Dokumente in:

- `codex/`
- `references/`

Lies aus `research/` alle für die Entwicklung relevanten Kapitel, insbesondere über:

- Spielarchitektur
- Engine-Wechsel und Versionen
- Modding-Grundlagen
- C# und Harmony
- Multiplayer-Architektur
- Multiplayer-API
- Synchronisation und Determinismus
- Desync-Debugging
- Linux
- Build und Tests

Prüfe die Beispiele unter `examples/`.

## D4. Einordnung der Wissensbasis

Die ursprüngliche `AGENTS.md` innerhalb der Wissensbasis ist eine technische Referenz.

Die neu erstellte `AGENTS.md` im Projektstamm bildet die verbindlichen projektspezifischen Entwicklungs- und Arbeitsregeln ab.

Führe relevante technische Anforderungen der Wissensbasis in die Projektregeln ein, ohne deren Inhalte unnötig zu duplizieren.

Vermeide widersprüchliche Anweisungen.

Unterscheide bei allen Quellen:

- Bestätigt
- Wahrscheinlich
- Ungeklärt
- Veraltet
- Spekulativ

Prüfe versionsabhängige Informationen gegebenenfalls erneut anhand offizieller Quellen oder verfügbarer Assemblies.

Dokumentiere die untersuchte Version und relevante Quellen.

## D5. Forschungsergebnisse für spätere Sitzungen aufbereiten

Nach der einmaligen Auswertung:

- Ergänze `CONTEXT_INDEX.md`.
- Aktualisiere `ARCHITECTURE.md`.
- Erstelle nötige Architecture Decision Records.
- Dokumentiere verifizierte Abhängigkeiten.
- Verweise auf relevante Forschungsdokumente.
- Halte offene technische Fragen fest.

**Die vollständige Forschungsarbeit soll bei normalen zukünftigen Codex-Sitzungen nicht erneut durchgeführt werden müssen.**

Nur unbekannte, geänderte oder versionsabhängige Informationen sollen erneut recherchiert werden.

---

# TEIL E – TECHNISCHES PRODUKTZIEL

## E1. Was entwickelt werden soll

Entwickle einen echten RimWorld-Multiplayer-Basismod mit dem Namen:

**RimWorld Multiplayer Foundation**

Das Projekt soll eine modulare Grundlage für spätere Multiplayer-Erweiterungen bilden.

Die erste Version:

**0.1 – Connection & Lobby Foundation**

Sie enthält:

- RimWorld-Client-Mod
- Multiplayer-Benutzeroberfläche
- Netzwerkbibliothek
- Versioniertes Kommunikationsprotokoll
- Dedizierten Linux-Server
- Lobbyverwaltung
- Spielerlisten
- Ready-Status
- Verbindungsverwaltung
- Disconnect-Behandlung
- Erweiterungsschnittstellen

**Es soll sich um eine reale Client-Server-Verbindung handeln, nicht um eine simulierte Multiplayer-Oberfläche.**

## E2. Was Version 0.1 nicht leisten muss

Noch nicht implementieren:

- Gemeinsame RimWorld-Kolonien
- Synchronisation von Pawns
- Synchronisation von Jobs
- Synchronisation von Gebäuden
- Synchronisation von RimWorld-Ticks
- World-State-Replication
- Gemeinsame Spielstände
- Deterministischen Gameplay-Lockstep
- Serverseitige RimWorld-Spielsimulation

Diese Funktionen sind Gegenstand späterer Entwicklung.

Der aktuelle Server ist zunächst ein Lobby- und Verbindungsserver.

Er wird noch keine RimWorld-Spielwelt berechnen.

## E3. Versionsstrategie

Ermittle vor der Implementierung die lokal verfügbare RimWorld-Version.

Berücksichtige die in der Wissensbasis beschriebenen Versionen 1.6 und 1.7.

Untersuche die jeweilige Runtime, die verfügbaren Assemblies und benötigte APIs.

Wähle für die erste lauffähige Version eine tatsächlich überprüfbare Entwicklungsumgebung.

RimWorld 1.6 kann als primäres Ziel verwendet werden, sofern damit die zuverlässigsten Builds und Tests möglich sind.

Halte die Netzwerkbibliothek unabhängig von Unity, Waylith und Verse.

Plane getrennte Game-Adapter für unterschiedliche RimWorld-Versionen ein.

Keine unbestätigte Unterstützung für neue Engine-Versionen behaupten.

---

# TEIL F – TECHNISCHE ARCHITEKTUR

## F1. Multiplayer Network Core

Erstelle eine eigenständige .NET-Bibliothek.

Sie soll möglichst unabhängig von RimWorld ausführbar und testbar sein.

Verantwortlichkeiten:

- Netzwerkkommunikation
- Protokollnachrichten
- Serialisierung
- Nachrichten-Framing
- Handshake
- Protokollversionierung
- Verbindungsstatus
- Fehlerbehandlung
- Timeouts
- Heartbeats
- Cancellation
- Reconnect-Grundlagen
- Erweiterbare Nachrichtentypen

Vermeide unnötige Abhängigkeiten von Spielklassen.

## F2. Dedicated Server

Erstelle einen eigenständigen Linux-fähigen .NET-Serverprozess.

Der Server muss ohne RimWorld und ohne grafische Oberfläche starten können.

Er verwaltet:

- Eingehende Verbindungen
- Client-Sitzungen
- Mehrere Lobbys
- Lobby-Mitgliedschaften
- Lobby-Hosts
- Spielerstatus
- Ready-Status
- Fehlerhafte Verbindungen
- Heartbeats
- Disconnects

Der Server soll autoritativ über Lobbyzustände entscheiden.

## F3. RimWorld Client

Erstelle einen tatsächlichen RimWorld-Mod.

Implementiere:

- Gültige Mod-Dateistruktur
- `About.xml`
- Notwendige Assemblies
- Initialisierung
- Multiplayer-Menü
- Verbindungseinstellungen
- Lobby-Browser
- Lobby-Ansicht
- Spielerübersicht
- Ready-Status
- Fehler- und Statusanzeigen

Bevorzuge vorhandene, überprüfte RimWorld-Erweiterungspunkte.

Setze Harmony nur ein, wenn es tatsächlich erforderlich ist.

## F4. Erweiterbare Architektur

Schaffe eine Grundlage für spätere Module.

Entwickle nach Bedarf eigene klar dokumentierte Schnittstellen für:

- Verbindungsverwaltung
- Lobbyverwaltung
- Netzwerk-Nachrichten
- Sitzungsinformationen
- Registrierung zusätzlicher Module

Mögliche Namen:

- `IConnectionService`
- `ILobbyService`
- `INetworkMessageHandler`
- `IMultiplayerModule`
- `ISessionContext`

Diese Schnittstellen sind eigene Architekturvorschläge und keine behaupteten RimWorld-APIs.

Neue Module sollen künftig eigene Nachrichtentypen registrieren können, ohne den Netzwerk-Core vollständig ändern zu müssen.

---

# TEIL G – NETZWERKPROTOKOLL

## G1. Technische Grundlage

Prüfe die Verwendung von TCP über `System.Net.Sockets`.

Die Architektur soll unter Linux und Windows verwendbar sein.

Implementiere:

- Nicht blockierende Netzwerkoperationen
- Eindeutiges Nachrichten-Framing
- Begrenzte Nachrichtengrößen
- Versionierte Nachrichten
- Validierung empfangener Daten
- Kontrollierte Fehlerbehandlung
- Sauberes Schließen von Verbindungen

Ein einzelner TCP-Lesevorgang darf niemals mit einer vollständigen Nachricht gleichgesetzt werden.

## G2. Nachrichtentypen

Implementiere mindestens:

- Hello
- Handshake Accepted
- Handshake Rejected
- Ping
- Pong
- Lobby List Request
- Lobby List Response
- Create Lobby
- Join Lobby
- Leave Lobby
- Lobby State Update
- Player Joined
- Player Left
- Player Ready Changed
- Disconnect
- Error

Dokumentiere das Protokoll in `docs/protocol.md`.

Beschreibe:

- Nachrichtenformat
- Protokollversion
- Nachrichtentypen
- Felder
- Gültige Zustandsübergänge
- Fehlercodes
- Maximalgrößen
- Erweiterungsmechanismen

## G3. Serverautoritatives Modell

Der Server bestimmt den gültigen Lobbyzustand.

Clients dürfen Anfragen senden, aber keine autoritativen Zustandsänderungen erzwingen.

Validiere:

- Lobby-Erstellung
- Lobby-Beitritt
- Spielerlimits
- Ready-Status
- Host-Rechte
- Lobby-Schließung
- Sitzungsidentitäten

## G4. Sicherheitsanforderungen

Verhindere:

- Unbegrenzte Nachrichten
- Unkontrollierte Speicherbelegung
- Ausführung fremden Codes
- Unsichere polymorphe Deserialisierung
- Ungeprüfte administrative Nachrichten
- Missbrauch fremder Sitzungen
- Offenlegung vertraulicher Informationen in Logs

Die erste Version darf für kontrollierte LAN-Tests optimiert werden.

Ohne ausreichende Authentifizierung und Transportverschlüsselung darf sie nicht als sicher für öffentliche Internetserver bezeichnet werden.

---

# TEIL H – MULTIPLAYER-OBERFLÄCHE

## H1. Einstiegspunkt

Implementiere nach Möglichkeit einen Multiplayer-Eintrag im RimWorld-Hauptmenü.

Falls dies nur durch riskante oder versionsabhängige Engine-Patches erreichbar wäre, verwende zunächst einen stabileren Einstiegspunkt.

Dokumentiere die Entscheidung.

## H2. Verbindungsmenü

Eingaben:

- Serveradresse
- Port
- Spielername

Aktionen:

- Verbinden
- Trennen
- Erneut verbinden

Status:

- Nicht verbunden
- Verbindung wird aufgebaut
- Verbunden
- Verbindung fehlgeschlagen
- Verbindung unterbrochen

## H3. Lobby-Browser

Zeige verfügbare Lobbys an.

Funktionen:

- Lobby-Liste abrufen
- Lobby erstellen
- Lobby beitreten
- Lobby-Informationen anzeigen

Angezeigte Daten:

- Lobbyname
- Host
- Spielerzahl
- Maximale Spielerzahl
- Lobby-Status

## H4. Lobby-Ansicht

Anzeigen:

- Lobbyname
- Spielerliste
- Lobby-Host
- Ready-Status
- Eigener Verbindungsstatus

Aktionen:

- Bereit
- Nicht bereit
- Verlassen
- Als Host schließen

Änderungen müssen über den Server an alle betroffenen Clients übertragen werden.

## H5. Spielstart

Eine zukünftige Startschaltfläche darf vorbereitet werden.

Sie darf nicht suggerieren, dass bereits synchronisierte RimWorld-Partien möglich sind.

Kennzeichne sie bis zur tatsächlichen Implementierung eindeutig als nicht verfügbar.

---

# TEIL I – DEDIZIERTER LINUX-SERVER

## I1. Serverkonfiguration

Erstelle eine Konfigurationsdatei wie:

`serverconfig.json`

Sie soll mindestens Folgendes unterstützen:

- Bind-Adresse
- Port
- Maximale Clientanzahl
- Maximale Spielerzahl pro Lobby
- Heartbeat-Intervall
- Timeout-Werte
- Log-Level

Optionale Zugangskontrollen sollen architektonisch vorbereitet werden.

Verwende dokumentierte Defaults.

Der Server darf nicht unbeabsichtigt öffentlich erreichbar gemacht werden.

## I2. Betrieb

Liefere:

- Build-Skript
- Start-Skript
- Beispielkonfiguration
- Installationsanleitung
- Dokumentierte Portanforderungen
- Beispiel für systemd

Installiere und aktiviere keine systemweiten Dienste ohne ausdrückliche Benutzerfreigabe.

Der Server muss durch einen regulären Shutdown sauber beendet werden können.

## I3. Stabilität

Berücksichtige:

- Gleichzeitige Verbindungen
- Verbindungstimeouts
- Fehlerhafte Pakete
- Abrupte Disconnects
- Client-Reconnects
- Serverseitige Zustandsbereinigung
- Kontrollierte Hintergrundaufgaben
- Strukturierte Logs
- Graceful Shutdown

---

# TEIL J – IMPLEMENTIERUNG IN KONTROLLIERTEN PHASEN

## J1. Phase 0 – Dokumentation und Git

Verbindlich zuerst:

- Repository prüfen beziehungsweise initialisieren
- Dokumentations-Branch erstellen
- Verzeichnisstruktur anlegen
- `AGENTS.md` erstellen
- Projektstatus erstellen
- Aufgabenverwaltung erstellen
- Kontextindex erstellen
- Architekturgrundlage dokumentieren
- Git-Regeln dokumentieren
- Erste Prüfungen durchführen
- Governance-Commit erstellen

Danach mit der technischen Entwicklung fortfahren.

## J2. Phase 1 – Wissensbasis und technische Analyse

- ZIP entpacken
- Relevante Dokumentation vollständig auswerten
- Runtime und RimWorld-Version prüfen
- Verfügbare APIs überprüfen
- Architekturentscheidungen dokumentieren
- Netzwerkprotokoll spezifizieren
- Projektdokumentation aktualisieren

## J3. Phase 2 – Netzwerk-Core

- Eigene Protokollbibliothek
- Serialisierung
- Framing
- Handshake
- Fehlerbehandlung
- TCP-Verbindungen
- Heartbeats
- Versionsprüfung
- Automatisierte Tests

## J4. Phase 3 – Dedicated Server

- Serverstart
- Clientannahme
- Verbindungsverwaltung
- Lobby-Erstellung
- Lobby-Liste
- Lobby-Beitritt
- Spielerstatus
- Ready-Status
- Disconnect
- Shutdown
- Tests

## J5. Phase 4 – RimWorld-Client

- Modstruktur
- Assembly-Referenzen
- Build-Prozess
- Menüintegration
- Netzwerkadapter
- Verbindungseinstellungen
- Lobby-Browser
- Lobby-Ansicht
- Statusanzeigen

## J6. Phase 5 – Integrationstests

- Server starten
- Zwei Testclients verbinden
- Lobby erstellen
- Lobby beitreten
- Spielerstatus aktualisieren
- Ready-Status übertragen
- Disconnect behandeln
- Reconnect prüfen

Wenn möglich zusätzlich mit tatsächlich laufenden RimWorld-Clients testen.

## J7. Phase 6 – Deployment

- Release-Builds
- Installationsskripte
- Server-Konfiguration
- Systemd-Beispiel
- Benutzeranleitung
- Verzeichnisstruktur prüfen

## J8. Phase 7 – Abschluss und menschlicher Review

- Tests zusammenfassen
- Projektzustand aktualisieren
- Architektur dokumentieren
- Änderungsprotokoll erstellen
- Review-Unterlagen erzeugen
- Branch und Commits bereitstellen
- Menschliche Merge-Freigabe abwarten

**Nicht eigenständig mergen.**

---

# TEIL K – AUTOMATISIERTE TESTSTRATEGIE

Implementiere nach Möglichkeit automatisierte Tests für:

### Netzwerk

1. Server startet korrekt.
2. Client kann eine TCP-Verbindung herstellen.
3. Handshake funktioniert.
4. Ungültige Protokollversion wird abgelehnt.
5. Nachrichten werden vollständig übertragen.
6. Ungültige Nachrichten werden kontrolliert verarbeitet.
7. Überlange Nachrichten werden abgelehnt.
8. Mehrere Clients können gleichzeitig verbunden sein.

### Lobby

9. Client A kann eine Lobby erstellen.
10. Client B kann die Lobby auflisten.
11. Client B kann beitreten.
12. Beide Clients erhalten denselben Lobbyzustand.
13. Ready-Statusänderungen werden übertragen.
14. Unberechtigte Aktionen werden abgewiesen.
15. Spieler können die Lobby verlassen.
16. Disconnects werden korrekt verarbeitet.
17. Reconnects erzeugen keine unkontrollierten Doppelzuordnungen.

### Server

18. Konfiguration wird geladen.
19. Ungültige Konfiguration erzeugt nachvollziehbare Fehler.
20. Server kann regulär beendet werden.
21. Eine fehlerhafte Clientverbindung bringt den Server nicht vollständig zum Absturz.

### RimWorld-Integration

Wenn die Spielumgebung vorhanden ist:

22. Mod wird geladen.
23. Benutzeroberfläche ist erreichbar.
24. Netzwerkverbindung lässt sich herstellen.
25. Lobbyinformationen werden korrekt angezeigt.
26. Spieleraktionen werden an den Server übertragen.
27. Verbindung lässt sich sauber beenden.

Dokumentiere für jede tatsächlich ausgeführte Prüfung:

- Testfall
- Datum beziehungsweise Zeitpunkt
- Relevanter Commit
- Ausgeführter Befehl
- Umgebung
- Ergebnis
- Fehler beziehungsweise Einschränkungen

Ein erfolgreicher Build ersetzt keinen tatsächlichen Laufzeittest.

---

# TEIL L – DOKUMENTATION WÄHREND DER ENTWICKLUNG

## L1. Kontinuierliche Aktualisierung

Nach jeder relevanten Änderung:

- Aufgabenstatus aktualisieren
- Projektstatus aktualisieren, sofern betroffen
- CODEMAP bei Strukturänderungen aktualisieren
- ARCHITECTURE bei Architekturänderungen aktualisieren
- Neue Entscheidungen dokumentieren
- Prüfergebnisse festhalten
- Verständliche Änderungen beschreiben

Nicht jede einzelne Codezeile benötigt einen eigenen Dokumentationseintrag.

Dokumentiere zusammenhängende Änderungen effizient und nachvollziehbar.

## L2. Menschlich verständliche Änderungen

Beschreibe beispielsweise nicht nur:

`Implemented connection state transitions`

Sondern sinngemäß:

„Die Verbindungsverwaltung wurde erweitert. Der Client erkennt jetzt, wenn der Server die Verbindung beendet. Statt dauerhaft einen verbundenen Zustand anzuzeigen, wechselt die Oberfläche in den Status ‚Verbindung unterbrochen‘. Die Verarbeitung wurde mit einem automatisierten Disconnect-Test überprüft.“

Dieser Stil soll in Änderungszusammenfassungen verwendet werden.

## L3. KI-lesbarer Status

Die maschinenlesbare Dokumentation soll präzise bleiben.

Sie beschreibt:

- Implementierungsstatus
- Betroffene Komponenten
- Aufgaben-IDs
- Verifizierungsstatus
- Abhängigkeiten
- Blocker
- Nächste Aktionen

Vermeide ausschweifende Beschreibungen.

## L4. Automatisierte Konsistenzprüfungen

Erstelle, soweit sinnvoll, ein kleines Prüfskript für die Dokumentation.

Es soll überprüfen:

- Vorhandensein verpflichtender Dateien
- YAML-Syntax
- Pflichtfelder
- Gültige Statuswerte
- Doppelte Aufgaben-IDs
- Referenzen auf nicht vorhandene Dokumente
- Fehlende Aufgabenabhängigkeiten

Wenn zusätzliche Prüfungen technisch zuverlässig umsetzbar sind, ergänze sie.

---

# TEIL M – AKZEPTANZKRITERIEN

## M1. Projektverwaltung

Das Projekt muss nach Phase 0:

- Eine gültige `AGENTS.md` besitzen.
- Eine zentrale Statusdatei besitzen.
- Eine Aufgabenübersicht besitzen.
- Einen Kontextindex besitzen.
- Eine dokumentierte Git-Strategie besitzen.
- Menschliche Review-Pflichten festlegen.
- Eine nachvollziehbare Architekturgrundlage besitzen.
- Neue KI-Sitzungen ohne vollständige Wiederholung der Vorgeschichte ermöglichen.

## M2. Multiplayer Foundation 0.1

Das technische Produkt muss:

- Einen echten Netzwerk-Core besitzen.
- Einen eigenständig ausführbaren Server besitzen.
- Zwei Clients gleichzeitig unterstützen.
- Lobby-Erstellung unterstützen.
- Lobby-Beitritt unterstützen.
- Spielerstatus übertragen.
- Ready-Status synchronisieren.
- Verbindungsabbrüche erkennen.
- Eine RimWorld-Benutzeroberfläche enthalten.
- Als lokaler RimWorld-Mod installierbar sein.
- Unter Linux gebaut werden können, soweit die vorhandenen Abhängigkeiten dies ermöglichen.
- Dokumentierte Erweiterungsschnittstellen besitzen.

Die tatsächliche Funktionsfähigkeit muss durch ausgeführte Tests belegt sein.

Wenn bestimmte Tests nicht möglich sind, dokumentiere sie als offen.

## M3. Keine falschen Fertigmeldungen

Eine Komponente darf nicht allein deshalb als vollständig funktionsfähig bezeichnet werden, weil ihr Code existiert oder kompiliert.

Unterscheide konsequent zwischen Implementierungs-, Build-, Test- und Review-Status.

---

# TEIL N – ZUKÜNFTIGE ENTWICKLUNG

Erstelle eine Roadmap mit folgenden möglichen Meilensteinen:

**0.1 – Connection & Lobby Foundation**

Echte Netzwerkverbindung, Lobbyverwaltung und Dedicated Server.

**0.2 – Netzwerk-Erweiterungen**

Modulregistrierung, erweiterbare Nachrichten, Protokollstabilisierung.

**0.3 – Session Foundation**

Erweiterte Sitzungsverwaltung und Vorbereitung gemeinsamer Spielstände.

**0.4 – Gameplay Sync Prototype**

Experimentelle Synchronisation ausgewählter Spielbefehle.

**0.5 – Deterministic Simulation**

Experimentelle deterministische Simulation und Desync-Erkennung.

**1.0 – Gameplay Multiplayer**

Langfristiges Ziel einer tatsächlich spielbaren Multiplayer-Integration.

Diese Roadmap stellt keine Funktionsgarantie dar.

Dokumentiere Risiken und technische Voraussetzungen.

---

# TEIL O – ABSCHLUSS JEDER CODEX-SITZUNG

Am Ende einer Sitzung:

1. Prüfe den tatsächlichen Git-Zustand.
2. Erfasse den aktuellen Branch.
3. Dokumentiere den letzten relevanten Commit.
4. Aktualisiere abgeschlossene und offene Aufgaben.
5. Aktualisiere den Projektstatus.
6. Aktualisiere die Sitzungsübergabe.
7. Dokumentiere relevante Test- und Build-Ergebnisse.
8. Erstelle eine verständliche Änderungszusammenfassung.
9. Kennzeichne anstehende menschliche Reviews.
10. Nenne den nächsten konkreten Entwicklungsschritt.

Antworte abschließend in verständlichem Deutsch mit:

- Was wurde entwickelt?
- Was funktioniert nachweislich?
- Was konnte nicht geprüft werden?
- Welche Fehler sind noch offen?
- Auf welchem Branch liegen die Änderungen?
- Welche Commits wurden erstellt?
- Was muss ein Mensch überprüfen?
- Wie wird das Projekt gestartet beziehungsweise getestet?
- Wie lautet der nächste sinnvolle Schritt?

Keine eigenständigen Merges.

---

# ABSCHLIESSENDE AUSFÜHRUNGSANWEISUNG

Beginne jetzt mit der tatsächlichen Einrichtung und Entwicklung des Projekts.

**Die Reihenfolge ist verbindlich:**

**Schritt 1:** Git-Zustand und Projektumgebung prüfen.

**Schritt 2:** Arbeitsbranch für die Projektdokumentation erstellen.

**Schritt 3:** Sämtliche zentralen Projektgedächtnis-Dateien erstellen.

**Schritt 4:** Verbindliche KI-Regeln, Git-Regeln und menschliche Review-Pflicht hinterlegen.

**Schritt 5:** Dokumentationsstruktur prüfen und den ersten Commit erstellen.

**Schritt 6:** RimWorld-Wissensbasis entpacken und auswerten.

**Schritt 7:** Technische Architektur und Entwicklungsaufgaben aktualisieren.

**Schritt 8:** Multiplayer Foundation 0.1 auf geeigneten Feature-Branches implementieren.

**Schritt 9:** Verfügbare Builds und Tests tatsächlich ausführen.

**Schritt 10:** Projektdokumentation aktualisieren und Änderungen zur menschlichen Review vorbereiten.

Fahre zwischen den Schritten selbstständig fort, solange keine zwingend erforderliche Information oder Benutzerfreigabe fehlt.

Ein abgeschlossener Dokumentations-Commit benötigt keine Merge-Freigabe, um die weitere Entwicklung auf einem neuen Arbeitsbranch fortzusetzen.

Geschützte Branches dürfen jedoch niemals ohne ausdrückliche menschliche Freigabe verändert werden.

## Wichtigste übergeordnete Anweisung

**Entwickle nicht nur den Multiplayer-Mod. Erstelle ein nachhaltig wartbares Softwareprojekt, das Menschen und KI-Agenten gemeinsam nachvollziehbar weiterentwickeln können.**

Die Projektdokumentation muss so effizient sein, dass jede zukünftige Codex-Sitzung anhand weniger zentraler Dateien:

- Das Projektziel versteht
- Den aktuellen technischen Stand erkennt
- Die geltenden Entwicklungsregeln kennt
- Die Git- und Review-Vorgaben einhält
- Die relevanten Quelldateien findet
- Offene Fehler und Aufgaben erkennt
- Die nächsten sinnvollen Schritte ermittelt
- Ohne unnötige Grundlagenrecherche weiterarbeiten kann

**Erstelle zuerst dieses System. Danach implementiere die tatsächliche Multiplayer Foundation.**