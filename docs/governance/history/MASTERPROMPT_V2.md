# RIMWORLD MULTIPLAYER FOUNDATION
# MASTERPROMPT 2.0 – PROJEKT-MIGRATION, GOVERNANCE UND ENTWICKLUNGSFORTSETZUNG

**Dokumentversion:** 2.0  
**Zielsystem:** OpenAI Codex  
**Projekt:** RimWorld Multiplayer Foundation  
**Auftragstyp:** Bestehendes Projekt analysieren, Projektregeln modernisieren und unterbrochene Entwicklung fortsetzen  
**Arbeitssprache:** Deutsch  
**Programmiersprache:** C# / .NET  
**Zielplattformen:** Linux und Windows, abhängig von den verifizierten Runtime-Anforderungen

---

# 0. WICHTIGE AUSGANGSSITUATION

## 0.1 Bestehendes Projekt – KEIN Neustart

Dieses Projekt wurde bereits mit zwei älteren Prompts bearbeitet:

1. Einer ursprünglichen Überprompt mit Regeln für KI-Dokumentation, Git, Branching, Qualitätssicherung und menschliche Reviews.
2. Einer ursprünglichen Masterprompt zur Entwicklung der RimWorld Multiplayer Foundation mit Lobby, Netzwerk-Core und dediziertem Server.

**Beide Prompts wurden bereits zumindest teilweise ausgeführt.**

Die ursprüngliche Masterprompt wurde während der Bearbeitung manuell abgebrochen.

Daher ist ausdrücklich davon auszugehen, dass bereits Folgendes existieren könnte:

- Projektverzeichnisse
- Quellcode
- Teilweise implementierte Funktionen
- Git-Repositories
- Feature-Branches
- Commits
- Uncommittete Änderungen
- Projektdateien
- Dokumentation
- AGENTS.md
- README.md
- TASKS.yaml
- PROJECT_STATE.yaml
- Build-Artefakte
- Testprojekte
- Server- oder Client-Komponenten

Der tatsächliche Zustand ist unbekannt und muss von dir ermittelt werden.

## 0.2 Dein neuer Auftrag

**Du sollst das bestehende Projekt NICHT neu erstellen.**

Deine Aufgaben sind:

1. Tatsächlichen Projektzustand feststellen.
2. Bisherige Arbeit sichern und erhalten.
3. Bereits implementierte Funktionen identifizieren.
4. Vorhandene Git-Strukturen untersuchen.
5. Alte Überprompt-Regeln auf Version 2.0 migrieren.
6. Vorhandene Projektdokumentation konsolidieren.
7. Fehlende Governance-Dateien ergänzen.
8. Tatsächliche Fortschritte und offene Aufgaben erfassen.
9. Unvollständige Arbeit der bisherigen Masterprompt rekonstruieren.
10. Die Multiplayer Foundation auf dieser Grundlage weiterentwickeln.
11. Automatisch dokumentieren, prüfen und committen.
12. Änderungen für eine menschliche Review vorbereiten.

**Du sollst den bisherigen Entwicklungsstand erhalten und weiterverwenden.**

## 0.3 Vorrang der neuen Regeln

Diese Masterprompt ersetzt die bisherigen projektinternen Entwicklungsanweisungen, soweit sie widersprüchlich sind.

Die bisherige Überprompt wird durch die in diesem Dokument enthaltene **Überprompt 2.0** ersetzt.

Neue Anweisungen sind nicht automatisch ein Grund, bereits funktionierende Implementierungen umzuschreiben.

Behandle frühere Entscheidungen und Implementierungen als Bestand, den du zunächst bewertest.

Übergeordnete Systemanweisungen, Sicherheitsvorgaben und ausdrückliche aktuelle Benutzeranweisungen haben stets Vorrang.

---

# TEIL I – BESTANDSAUFNAHME UND WIEDERAUFNAHME

## 1. Phase Recovery – Projekt analysieren

**Dies ist die erste auszuführende Phase.**

Bevor du bestehenden Code veränderst, untersuche die Projektumgebung.

### 1.1 Arbeitsverzeichnis ermitteln

Ermittle:

- Aktuelles Arbeitsverzeichnis
- Projektstammverzeichnis
- Git-Repository
- Vorhandene Projektstruktur
- Bereits erstellte Dokumentationsdateien
- Existierende Quellcodeprojekte
- Lokale RimWorld-Installation, sofern vorhanden
- Bereits entpackte RimWorld-Wissensbasis

Suche insbesondere nach:

`RimWorld-Codex-Knowledge.zip`

sowie nach einem bereits entpackten Verzeichnis:

`RimWorld-Codex-Knowledge/`

Wenn das Projektverzeichnis einen anderen Namen besitzt, verwende das bestehende Verzeichnis.

Erstelle kein zweites paralleles Projekt mit identischem Zweck.

### 1.2 Git-Zustand vollständig prüfen

Untersuche:

- `git status`
- Aktuellen Branch
- Vorhandene lokale Branches
- Remote-Branches, soweit verfügbar
- Letzte Commits
- Uncommittete Änderungen
- Neue, noch nicht versionierte Dateien
- Aktuelle Branch-Abhängigkeiten
- Bestehende Merge- oder Rebase-Zustände

Ermittle, ob die alte Codex-Sitzung während einer Implementierung, eines Builds, einer Dokumentationsänderung oder eines Git-Vorgangs abgebrochen wurde.

**Wichtig:**

Verwende keinen destruktiven Reset, um einen vermeintlich sauberen Ausgangszustand herzustellen.

Unbekannte Änderungen dürfen weder gelöscht noch blind in einen Commit aufgenommen werden.

Wenn ein sicherer Branchwechsel nicht möglich ist, verwende gegebenenfalls einen separaten Git-Worktree oder dokumentiere den Blocker.

### 1.3 Bisherigen Entwicklungsfortschritt rekonstruieren

Prüfe nicht nur die Dokumentation.

Untersuche auch den tatsächlichen Quellcode.

Stelle fest:

- Welche Komponenten existieren?
- Welche Komponenten sind nur vorbereitet?
- Welche Funktionen wurden implementiert?
- Welche Funktionen sind tatsächlich getestet?
- Welche Builds wurden nachweislich ausgeführt?
- Welche Tests existieren?
- Welche Fehler sind erkennbar?
- Welche Aufgaben wurden angefangen, aber nicht abgeschlossen?
- Welche Aufgaben fehlen vollständig?
- Welche Aussagen in der Dokumentation sind möglicherweise veraltet?

Bewerte jede relevante Komponente anhand des tatsächlichen Zustands.

Ein Dateiname oder eine vorhandene Klasse bedeutet nicht automatisch, dass die Funktion implementiert wurde.

Ein erfolgreicher Build bedeutet nicht automatisch, dass die Funktion zur Laufzeit funktioniert.

### 1.4 Recovery-Bericht erstellen

Erstelle:

`docs/verification/RECOVERY_REPORT.md`

Der Bericht muss enthalten:

- Tatsächlich gefundenes Projektverzeichnis
- Aktueller Git-Status
- Bereits bestehende Branches
- Letzte relevante Commits
- Bereits implementierte Komponenten
- Vorhandene Dokumentation
- Vorhandene Tests
- Unvollständige Arbeiten
- Mögliche Widersprüche zwischen Dokumentation und Code
- Erkannte Risiken
- Empfohlene Fortsetzung

Kennzeichne Erkenntnisse als:

- Verifiziert
- Aus Quellcode abgeleitet
- Aus Dokumentation übernommen
- Noch ungeklärt

**Erst nach dieser Bestandsaufnahme darf die Governance-Migration beginnen.**

---

# TEIL II – ÜBERPROMPT 2.0

## 2. Zweck der Überprompt

Die Überprompt definiert die dauerhaft geltenden Entwicklungs- und Verhaltensregeln für sämtliche KI-Agenten innerhalb dieses Projekts.

Sie soll sicherstellen, dass:

- Der Projektzustand jederzeit nachvollziehbar bleibt.
- Entwicklungsfortschritte nicht von einer bestimmten Chat-Sitzung abhängen.
- Andere KI-Agenten das Projekt übernehmen können.
- Änderungen automatisch versioniert werden.
- Tests systematisch ausgeführt werden.
- Menschliche Reviews verpflichtend bleiben.
- Keine unerlaubten Merges stattfinden.
- Die Dokumentation aktuell ist.
- Der Kontextverbrauch effizient bleibt.
- Fehler reproduzierbar untersucht werden.
- Bereits bestehende Arbeit geschützt wird.

Diese Regeln gelten für sämtliche zukünftigen Entwicklungsaufgaben.

Überführe die folgenden Regeln in eine kompakte, dauerhaft gültige `AGENTS.md`.

---

# 3. Projektgedächtnis – Single Source of Truth

## 3.1 Grundprinzip

Das Projekt benötigt ein persistentes, KI-lesbares Gedächtnis.

**Der Chatverlauf ist nicht die maßgebliche Quelle des Entwicklungsstandes.**

Stattdessen muss der relevante Projektzustand aus versionierten Dateien rekonstruierbar sein.

Dabei gilt:

**Jede Information besitzt möglichst genau eine maßgebliche Quelle.**

Vermeide doppelte Statusinformationen in unterschiedlichen Dateien.

Verwende Verweise statt umfangreicher Kopien.

## 3.2 Verbindliche Dokumentationsstruktur

Erstelle fehlende Dateien und aktualisiere bestehende Dateien.

Verwende nach Möglichkeit folgende Struktur:

    docs/
    ├── ai/
    │   ├── PROJECT_STATE.yaml
    │   ├── TASKS.yaml
    │   ├── CONTEXT_INDEX.md
    │   ├── ARCHITECTURE.md
    │   ├── CODEMAP.md
    │   └── SESSION_HANDOFF.md
    │
    ├── governance/
    │   ├── WORKFLOW.md
    │   ├── REVIEW_POLICY.md
    │   ├── DOCUMENTATION_POLICY.md
    │   └── MIGRATION_V2.md
    │
    ├── decisions/
    ├── verification/
    ├── reviews/
    │
    ├── SPEC.md
    ├── protocol.md
    ├── client.md
    ├── dedicated-server.md
    ├── extension-api.md
    ├── build.md
    ├── testing.md
    └── roadmap.md

Im Projektstamm:

    AGENTS.md
    README.md
    CHANGELOG.md

Passe vorhandene Strukturen nur an, wenn dies technisch oder organisatorisch sinnvoll ist.

Vermeide unnötige Dateiverschiebungen.

Bei Umbenennungen müssen alle relevanten Verweise aktualisiert werden.

## 3.3 Verantwortlichkeiten der Dokumente

### AGENTS.md

Die zentrale Einstiegspunktdatei für KI-Agenten.

Sie enthält:

- Verbindliche Entwicklungsregeln
- Projektstartprozedur
- Git-Vorgaben
- Commit-Regeln
- Merge-Verbot ohne menschliche Freigabe
- Testpflichten
- Dokumentationsregeln
- Verweise auf wichtige Projektdateien

Die Datei soll möglichst kompakt bleiben.

Ziel: maximal ungefähr 150–200 Zeilen.

Detaillierte Anleitungen gehören in die Governance-Dokumentation.

### PROJECT_STATE.yaml

Enthält den aktuellen technischen Projektzustand.

Beispielsweise:

- Projektname
- Produktversion
- Zielversion von RimWorld
- Aktuelle Entwicklungsphase
- Implementierte Komponenten
- Verifizierte Funktionen
- Bekannte Probleme
- Blockierende Abhängigkeiten
- Aktive Aufgaben
- Nächster technischer Schritt
- Verweise auf relevante Dokumentation

Diese Datei ist die maßgebliche Quelle für den technischen Ist-Zustand.

### TASKS.yaml

Enthält die Aufgabenverwaltung.

Jede Aufgabe erhält eine eindeutige ID.

Beispiele:

- `GOV-001`
- `NET-001`
- `SERVER-001`
- `LOBBY-001`
- `CLIENT-001`
- `TEST-001`

Jede Aufgabe enthält:

- Titel
- Ziel
- Beschreibung
- Priorität
- Betroffene Komponenten
- Abhängigkeiten
- Akzeptanzkriterien
- Implementierungsstatus
- Verifizierungsstatus
- Integrationsstatus
- Zugeordneter Branch
- Verweise auf Prüfprotokolle

### CONTEXT_INDEX.md

Dient dazu, unnötigen Kontextverbrauch zu reduzieren.

Beschreibe, welche Dokumente für welche Entwicklungsaufgaben erforderlich sind.

Eine KI soll nicht bei jeder Sitzung sämtliche Forschungsdateien einlesen müssen.

### ARCHITECTURE.md

Enthält den tatsächlich implementierten technischen Aufbau.

Unterscheide:

- Vorhandene Komponenten
- Geplante Komponenten
- Experimentelle Komponenten

Dokumentiere Abhängigkeiten, Datenflüsse und Schnittstellen.

### CODEMAP.md

Enthält eine kompakte Übersicht der wichtigsten Quellcodedateien.

Dokumentiere:

- Dateipfad
- Zweck
- Wichtige Klassen
- Öffentliche Schnittstellen
- Zugehörige Tests

Aktualisiere diese Datei bei relevanten Strukturänderungen.

### SESSION_HANDOFF.md

Ist die kompakte Übergabe an die nächste KI-Sitzung.

Sie soll nach Möglichkeit aus den zentralen Statusdaten und tatsächlichen Git-Informationen generiert werden.

Keine eigenständige zweite Aufgabenverwaltung aufbauen.

### SPEC.md

Enthält die fachlichen und technischen Produktanforderungen.

Unterscheide klar zwischen:

- Verbindlichen Anforderungen
- Optionalen Erweiterungen
- Ausdrücklich ausgeschlossenen Funktionen
- Akzeptanzkriterien

### CHANGELOG.md

Beschreibt tatsächlich implementierte Änderungen in verständlicher Sprache.

Das Changelog ist keine Aufgabenliste und keine Sammlung geplanter Funktionen.

---

# 4. Effizientes KI-Kontextmanagement

## 4.1 Drei Kontextstufen

Implementiere ein dreistufiges Kontextmodell.

### Stufe 1 – Pflichtkontext

Zu Beginn jeder Sitzung lesen:

- Geltende `AGENTS.md`
- `PROJECT_STATE.yaml`
- `TASKS.yaml`
- `SESSION_HANDOFF.md`

Prüfe außerdem den tatsächlichen Git-Status.

Diese Dateien sollen so kompakt sein, dass der Projektzustand schnell erkennbar wird.

### Stufe 2 – Aufgabenbezogener Kontext

Lade nur die Dokumentation, die für die aktuelle Aufgabe erforderlich ist.

Beispiele:

Bei Netzwerkentwicklung:

- Protokolldokumentation
- Netzwerkarchitektur
- Relevante Netzwerkklassen
- Netzwerk-Tests

Bei Serverentwicklung:

- Serverarchitektur
- Konfiguration
- Server-Quellcode
- Integrationstests

Bei RimWorld-Integration:

- Client-Dokumentation
- RimWorld-API-Referenzen
- Client-Quellcode
- Relevante Spielversionsinformationen

### Stufe 3 – Vollständige Wissensbasis

Lade die umfangreiche RimWorld-Recherche nur dann erneut, wenn:

- Unbekannte APIs verwendet werden müssen.
- Versionsabhängige Fragen entstehen.
- Architekturentscheidungen überprüft werden müssen.
- Ungeklärte Multiplayer-Probleme auftreten.
- Neue RimWorld-Versionen berücksichtigt werden sollen.

**Vermeide das wiederholte vollständige Einlesen bereits ausgewerteter Informationen.**

## 4.2 Kontextkomprimierung

Zusammenfassungen müssen faktenbasiert bleiben.

Reduziere:

- Unnötige Wiederholungen
- Lange Verlaufsbeschreibungen
- Doppelte Statusangaben
- Redundante Codebeispiele

Behalte:

- Wichtige Entscheidungen
- Tatsächliche Implementierungen
- Schnittstellen
- Versionsangaben
- Bekannte Fehler
- Offene Aufgaben
- Verifizierungsnachweise
- Nächste Schritte

Dokumentiere bei technischen Informationen den Pfad zur maßgeblichen Quelle.

---

# 5. Git-Strategie 2.0

## 5.1 Grundregeln

Alle relevanten Projektänderungen werden mit Git versioniert.

Entwicklung erfolgt grundsätzlich auf Arbeitsbranches.

Geschützte Haupt- und Integrationsbranches dürfen nicht eigenständig verändert werden.

Verwende vorhandene Branches weiter, sofern sie zur aktuellen Aufgabe passen.

Erstelle keine unnötigen neuen Branches.

## 5.2 Branch-Namen

Verwende nachvollziehbare Namen wie:

- `docs/governance-v2`
- `feat/network-protocol`
- `feat/dedicated-server`
- `feat/lobby-management`
- `feat/rimworld-client`
- `fix/connection-handling`
- `refactor/network-core`
- `test/lobby-integration`

Bei bestehenden Feature-Branches dürfen die vorhandenen Namen erhalten bleiben.

## 5.3 Abhängige Branches

Untersuche vor Branch-Erstellung die technische Abhängigkeit zur vorherigen Arbeit.

Wenn eine Funktion auf noch nicht gemergtem Code basiert, darf ein darauf aufbauender Feature-Branch erstellt werden.

Dokumentiere:

- Ausgangsbranch
- Abhängige Branches
- Relevante Commits
- Ausstehende Reviews

Verwende keine verdeckten Merges zur Übernahme noch nicht freigegebener Änderungen.

Bei Bedarf sind Git-Worktrees zulässig.

## 5.4 Verbot eigenständiger Merges

**Vor JEDEM Merge muss ein Mensch die Änderungen geprüft und ausdrücklich freigegeben haben.**

Dies gilt unabhängig vom Zielbranch.

Die Regel umfasst insbesondere:

- Merge-Commits
- Fast-Forward-Merges
- Squash-Merges
- Pull-Request-Merges
- Andere Integrationsaktionen, die geschützte Zielbranches verändern

Eine erfolgreiche Testausführung ist keine Merge-Freigabe.

Eine frühere allgemeine Zustimmung zur Entwicklung ist ebenfalls keine Merge-Freigabe.

Ohne ausdrückliche Freigabe verbleiben die Änderungen auf ihren Arbeitsbranches.

## 5.5 Kein automatischer Push

Git-Commits dürfen automatisch erstellt werden.

Remote-Pushes erfolgen nur, wenn der Benutzer sie ausdrücklich erlaubt oder hierfür eine eindeutige dauerhafte Projektfreigabe besteht.

Ohne solche Freigabe bleiben Commits lokal.

## 5.6 Schutz vorhandener Änderungen

Untersagt sind ohne ausdrückliche Zustimmung:

- Destruktiver Reset
- Force-Push
- Löschen fremder Änderungen
- Löschen bestehender Branches mit relevanter Arbeit
- Überschreiben nicht gesicherter Dateien
- Unkontrollierte History-Rewrites
- Eigenständige Merges

Prüfe vor Git-Operationen, ob vorhandene Benutzeränderungen betroffen sind.

---

# 6. Automatische Git-Commits

## 6.1 Verbindliche Automatisierung

**Codex muss nach jeder abgeschlossenen, logisch zusammenhängenden Arbeitseinheit automatisch committen.**

Dafür ist keine menschliche Bestätigung erforderlich.

Beispiele:

- Neue Netzwerkkomponente implementiert
- Neue Serverfunktion erstellt
- Fehler behoben
- Tests ergänzt
- Dokumentationsmigration abgeschlossen
- Architekturentscheidung dokumentiert

## 6.2 Regeln für Commits

Vor jedem Commit:

1. Betroffene Dateien prüfen.
2. Zugehörige Tests ausführen, soweit verfügbar.
3. Relevante Dokumentation aktualisieren.
4. Nur zur Aufgabe gehörende Dateien stagen.
5. Aussagekräftige Commit-Message formulieren.
6. Commit erstellen.
7. Erfolgreichen Commit überprüfen.

Verwende Conventional Commits.

Beispiele:

`feat(server): add lobby connection handling`

`fix(network): prevent duplicate session registration`

`docs(governance): migrate project rules to version 2`

`test(protocol): verify invalid message rejection`

## 6.3 Fehlgeschlagene Tests

Wenn eine Aufgabe teilweise implementiert wurde, aber noch Fehler enthält:

- Analysiere zunächst den Fehler.
- Versuche eine nachvollziehbare Korrektur.
- Führe relevante Tests erneut aus.
- Dokumentiere weiterhin bestehende Fehler.

Ein unvollständiger Zwischenstand darf auf dem Arbeitsbranch committed werden.

Kennzeichne ihn eindeutig als WIP beziehungsweise unvollständig.

Ein solcher Commit darf nicht als erfolgreich verifizierte Funktion dokumentiert werden.

## 6.4 Unbekannte Benutzeränderungen

Committe niemals automatisch unbekannte oder nicht zur aktuellen Aufgabe gehörende Benutzeränderungen.

Trenne eigene Änderungen von bereits vorhandenen Änderungen.

Wenn dies nicht zuverlässig möglich ist, sichere den Zustand und dokumentiere den Blocker.

---

# 7. Verbindliche Statusmodelle

Verwende drei voneinander unabhängige Statusdimensionen.

## 7.1 Implementierungsstatus

Zulässige Werte:

- `not_started`
- `planned`
- `in_progress`
- `implemented`
- `blocked`

## 7.2 Verifizierungsstatus

Zulässige Werte:

- `not_tested`
- `build_passed`
- `tests_passed`
- `tests_failed`
- `runtime_verified`
- `blocked`

Ein Status darf nur gesetzt werden, wenn ein entsprechender Nachweis vorliegt.

Wenn mehrere Prüfungen relevant sind, speichere die einzelnen Ergebnisse getrennt, statt ein umfassenderes Ergebnis vorzutäuschen.

## 7.3 Integrationsstatus

Zulässige Werte:

- `not_submitted`
- `ready_for_human_review`
- `changes_requested`
- `approved`
- `merged`

`approved` darf nur durch eine dokumentierte ausdrückliche menschliche Freigabe entstehen.

`merged` darf nur gesetzt werden, wenn die Integration tatsächlich stattgefunden hat.

Die KI darf eine fehlende Freigabe nicht selbst ersetzen.

---

# 8. Aufgabenvertrag vor Implementierung

Für jede neue größere Aufgabe muss ein kompakter Aufgabenvertrag vorliegen.

Er enthält:

**Aufgaben-ID:** Eindeutiger Bezeichner.

**Ziel:** Welches Verhalten soll entstehen?

**Ausgangssituation:** Was existiert bereits?

**Umfang:** Welche Komponenten dürfen oder müssen verändert werden?

**Nicht-Ziele:** Was gehört ausdrücklich nicht zur Aufgabe?

**Abhängigkeiten:** Welche Schnittstellen oder Aufgaben werden benötigt?

**Akzeptanzkriterien:** Wann ist die Aufgabe technisch abgeschlossen?

**Prüfstrategie:** Wie wird das Ergebnis nachgewiesen?

**Risiken:** Welche bekannten Probleme müssen berücksichtigt werden?

**Branch:** Wo erfolgt die Entwicklung?

Der Aufgabenvertrag soll effizient und kurz sein.

Für kleine, eindeutig abgegrenzte Änderungen genügt ein entsprechend kurzer Eintrag in `TASKS.yaml`.

---

# 9. Automatisierte Qualitätssicherung

## 9.1 Grundprinzip

Codex muss Änderungen nach Möglichkeit selbstständig überprüfen.

Dabei soll nicht nach jeder Kleinigkeit das komplette Projekt neu getestet werden.

Verwende eine risikobasierte Teststrategie.

## 9.2 Prüfung nach Änderungstyp

### Dokumentationsänderungen

- Markdown prüfen
- YAML validieren
- Pflichtdateien überprüfen
- Interne Verweise überprüfen
- Dokumentationsstatus aktualisieren

### Netzwerkänderungen

- Build
- Protokolltests
- Serialisierungstests
- Verbindungs- und Fehlerbehandlungstests

### Serveränderungen

- Build
- Unit-Tests
- Serverintegrationstests
- Verbindungs- und Disconnect-Tests

### RimWorld-Client-Änderungen

- Build
- Assembly-Referenzen prüfen
- API-Kompatibilität prüfen
- Verfügbare Laufzeittests durchführen

### Architekturänderungen

- Betroffene Komponenten identifizieren
- Abhängige Builds ausführen
- Relevante Regressionstests durchführen
- Architektur dokumentieren

### Vor menschlichem Merge

- Git-Diff prüfen
- Relevante automatisierte Tests ausführen
- Bekannte Fehler dokumentieren
- Review-Bericht erstellen

## 9.3 Prüfnachweise

Speichere ausführliche Verifizierungsnachweise unter:

`docs/verification/`

Dokumentiere:

- Testfall
- Ausgeführten Befehl
- Tatsächliches Ergebnis
- Exit-Code, soweit verfügbar
- Umgebung
- Relevanten Branch beziehungsweise Commit
- Bekannte Einschränkungen

Wenn ein Test nicht ausführbar ist, dokumentiere den Grund.

**Niemals einen nicht ausgeführten Test als bestanden markieren.**

---

# 10. Systematische Fehlerbehandlung

## 10.1 Selbstständige Fehleranalyse

Wenn ein Build, Test oder eine Funktion fehlschlägt:

1. Fehlermeldung vollständig analysieren.
2. Betroffene Komponente ermitteln.
3. Relevanten Quellcode untersuchen.
4. Wahrscheinliche Ursache identifizieren.
5. Reproduzierbaren Testfall erstellen, soweit möglich.
6. Eine gezielte Korrektur implementieren.
7. Test erneut ausführen.
8. Ergebnis dokumentieren.

Vermeide unkontrollierte Änderungen an zahlreichen Dateien ohne nachvollziehbare Fehlerhypothese.

## 10.2 Keine endlosen Wiederholungen

Wenn eine Korrektur denselben Fehler erneut verursacht:

- Analysiere die neue Evidenz.
- Verändere die Hypothese, falls notwendig.
- Untersuche Abhängigkeiten.
- Prüfe Versions- und API-Konflikte.

Wiederhole nicht beliebig oft denselben erfolglosen Versuch.

Wenn die Lösung nicht zuverlässig möglich ist, dokumentiere den Fehler und fahre mit unabhängigen Aufgaben fort, sofern sinnvoll.

## 10.3 Selbstständige Entscheidungen

Triff gewöhnliche technische Entscheidungen selbstständig.

Bevorzuge:

- Kleine Änderungen
- Bewährte Schnittstellen
- Geringe Komplexität
- Reproduzierbare Lösungen
- Gute Testbarkeit
- Langfristige Wartbarkeit

Frage nur nach, wenn eine unverzichtbare Information fehlt oder eine ausdrückliche Freigabe erforderlich ist.

---

# 11. Menschlich verständliche Änderungen

Jede abgeschlossene Arbeitseinheit muss in verständlichem Deutsch zusammengefasst werden.

Beschreibe:

- Was wurde geändert?
- Warum?
- Was funktioniert dadurch?
- Welche Auswirkungen entstehen?
- Welche Tests wurden ausgeführt?
- Was ist noch offen?

Vermeide reine technische Commit-Meldungen als einzige Änderungsdokumentation.

Beispiel:

Nicht nur:

`Implemented heartbeat handling`

Sondern:

„Die Netzwerkverbindung wurde um eine automatische Erkennung unterbrochener Verbindungen erweitert. Wenn ein Spieler nicht mehr erreichbar ist, erkennt der Server dies nach Ablauf der konfigurierten Frist und aktualisiert die Lobby. Die Heartbeat-Verarbeitung wurde automatisiert getestet.“

Eine solche Aussage darf nur erfolgen, wenn die beschriebene Funktion und der Test tatsächlich existieren.

---

# 12. Menschliches Review-System

## 12.1 Review-Berichte

Erstelle für Änderungen, die bereit zur Integration sind, einen Bericht unter:

`docs/reviews/`

Der Bericht muss enthalten:

- Aufgaben-ID
- Branch
- Zielbranch
- Beschreibung der Änderung
- Betroffene Komponenten
- Relevante Commits
- Wichtigste technische Entscheidungen
- Build- und Testergebnisse
- Bekannte Risiken
- Manuell zu überprüfende Punkte

## 12.2 Review-Abschluss

Sobald eine Änderung bereit ist:

Setze den Integrationsstatus auf:

`ready_for_human_review`

Verändere den Status nicht eigenständig zu `approved`.

Führe keinen Merge aus.

Weitere unabhängige Arbeit darf fortgesetzt werden.

## 12.3 Review-Freigaben dokumentieren

Eine tatsächliche menschliche Freigabe soll im Review-Bericht erfasst werden.

Dokumentiere:

- Betroffene Änderung
- Freigabeentscheidung
- Zeitpunkt, soweit zuverlässig bekannt
- Freigegebenen Zielbranch
- Eventuelle Bedingungen

Keine Freigabe erfinden.

---

# 13. Dokumentationsautomatisierung

## 13.1 Automatische Konsistenzprüfung

Erstelle nach Möglichkeit ein kleines Skript:

`scripts/validate-project-state.sh`

oder eine plattformübergreifende Alternative.

Dieses soll prüfen:

- Pflichtdateien vorhanden
- YAML gültig
- Statuswerte gültig
- Aufgaben-IDs eindeutig
- Referenzen vorhanden
- Aufgabenabhängigkeiten konsistent
- Erforderliche Felder vorhanden

## 13.2 Automatische Übergabe

Erstelle, soweit sinnvoll:

`scripts/update-handoff.sh`

oder eine äquivalente Lösung.

Dieses Werkzeug kann aus vorhandenen Informationen eine kompakte `SESSION_HANDOFF.md` erzeugen.

Die Git-Historie selbst bleibt die maßgebliche Quelle für Commit-IDs und Branch-Zustände.

Vermeide zyklische Probleme, bei denen ein Commit seine eigene noch unbekannte Commit-ID in einer bereits versionierten Datei enthalten müsste.

## 13.3 Keine übermäßige Automatisierung

Erstelle keine komplexe Dokumentationsplattform, wenn wenige einfache Dateien und Skripte ausreichen.

Dokumentationsautomatisierung soll den Entwicklungsprozess erleichtern, nicht selbst zum Hauptprojekt werden.

---

# TEIL III – MIGRATION DER ALTEN ÜBERPROMPT

# 14. Governance-Migration auf Version 2.0

## 14.1 Bestehende Regeln untersuchen

Lies die bereits vorhandene `AGENTS.md` sowie alle bestehenden Governance-Dokumente.

Prüfe, welche Vorgaben aus der ursprünglichen Überprompt bereits umgesetzt wurden.

Identifiziere:

- Vorhandene Git-Regeln
- Commit-Regeln
- Branch-Regeln
- Review-Regeln
- Dokumentationsregeln
- Testregeln
- Projektstatusdateien
- Bereits bestehende Automatisierung

## 14.2 Regeln aktualisieren

Ersetze widersprüchliche ältere Projektregeln durch die Vorgaben der Überprompt 2.0.

Insbesondere müssen jetzt eindeutig geregelt sein:

1. Automatische Commits ohne Rückfrage.
2. Verbot sämtlicher eigenständiger Merges.
3. Keine automatischen Pushes ohne Freigabe.
4. Drei getrennte Statusdimensionen.
5. Drei Kontextstufen für effiziente KI-Sitzungen.
6. Zentrale Quellen für Projektinformationen.
7. Automatische beziehungsweise konsistente Übergabedokumentation.
8. Nachvollziehbare Fehlerbehandlung.
9. Risikobasierte Testauswahl.
10. Dokumentierte Branch-Abhängigkeiten.
11. Menschlich verständliche Änderungsbeschreibungen.
12. Schutz bestehender Benutzeränderungen.

## 14.3 Bestehende Dateien erhalten

Vorhandene Dokumentation darf überarbeitet und konsolidiert werden.

Verliere dabei keine relevanten historischen Informationen.

Wenn Informationen veraltet sind, kennzeichne sie oder verschiebe sie in geeignete historische Dokumentation.

Vermeide unnötige vollständige Dateikopien.

## 14.4 Migrationsbericht

Erstelle:

`docs/governance/MIGRATION_V2.md`

Dokumentiere:

- Welche Regeln vorher existierten
- Welche Regeln geändert wurden
- Welche Regeln neu hinzugekommen sind
- Welche Dateien angepasst wurden
- Welche Dateien neu erstellt wurden
- Welche Regeln jetzt verbindlich gelten
- Welche Migrationsteile nicht abgeschlossen werden konnten

## 14.5 Governance-Commit

Führe verfügbare Dokumentationsprüfungen aus.

Erstelle anschließend automatisch einen Commit mit einer Nachricht wie:

`docs(governance): migrate project workflow to v2`

Nutze einen geeigneten Arbeitsbranch.

**Kein automatischer Merge.**

Danach fahre mit der Wiederaufnahme der eigentlichen Multiplayer-Entwicklung fort.

---

# TEIL IV – RIMWORLD-WISSENSBASIS

# 15. Vorhandene Recherche verwenden

## 15.1 Archiv

Das Projekt besitzt möglicherweise bereits eine technische Wissensbasis:

`RimWorld-Codex-Knowledge.zip`

Prüfe, ob sie bereits entpackt und ausgewertet wurde.

Wenn ja, verwende die vorhandene Struktur und bisher dokumentierte Erkenntnisse.

Wenn nein, entpacke das Archiv nach einer Integritätsprüfung.

## 15.2 Relevante Dokumentation

Berücksichtige insbesondere:

- README.md
- AGENTS.md innerhalb der Wissensbasis
- Gesamtauswertung
- Validierungsbericht
- RimWorld-Architektur
- Waylith-Engine
- C#-Modding
- Harmony
- Multiplayer-Architektur
- Multiplayer-APIs
- Deterministische Simulation
- Desync-Debugging
- Linux-Entwicklung
- Build- und Testverfahren

Die Wissensbasis ist eine technische Referenz, kein bereits implementierter Mod.

## 15.3 Versionsprüfung

Verlasse dich bei versionsabhängigen APIs nicht ausschließlich auf ältere Forschungsinformationen.

Prüfe, soweit möglich:

- Lokal installierte RimWorld-Version
- Assembly-Versionen
- Target Framework
- Verfügbare Modding-APIs
- Harmony-Version
- Relevante Engine-Abhängigkeiten

Unterscheide RimWorld 1.6 und 1.7.

Behaupte keine Kompatibilität, die nicht nachgewiesen wurde.

## 15.4 Bereits erfolgte Recherche nutzen

Wiederhole nicht unnötig die gesamte Recherche der vorherigen Codex-Sitzung.

Verwende bereits dokumentierte, überprüfbare Ergebnisse.

Recherchiere erneut, wenn:

- Ein API-Aufruf ungeklärt ist.
- Eine Versionsabweichung erkannt wird.
- Ein Build an einer veralteten Referenz scheitert.
- Eine technische Annahme überprüft werden muss.

Ergänze `CONTEXT_INDEX.md`, damit spätere KI-Agenten die relevanten Informationen gezielt finden.

---

# TEIL V – MULTIPLAYER FOUNDATION 0.1

# 16. Produktziel

Entwickle die bestehende **RimWorld Multiplayer Foundation** weiter.

Das Ziel ist ein modularer Multiplayer-Basismod mit:

- Echten Client-Server-Verbindungen
- Einer RimWorld-Multiplayer-Oberfläche
- Lobby-Browser
- Lobby-Erstellung
- Lobby-Beitritt
- Spielerliste
- Ready-Status
- Netzwerkprotokoll
- Verbindungsverwaltung
- Dediziertem Linux-Server
- Erweiterbarer Architektur

Die erste Version ist:

**0.1 – Connection & Lobby Foundation**

Sie soll eine funktionierende Grundlage für spätere Multiplayer-Module schaffen.

## 16.1 Kein vollständiger Gameplay-Multiplayer

Version 0.1 muss noch nicht:

- Pawns synchronisieren
- Spielticks synchronisieren
- Gebäude synchronisieren
- Gemeinsame Spielstände laden
- Spielwelten replizieren
- Gemeinsame Kolonien simulieren
- Deterministischen Gameplay-Lockstep implementieren

Diese Funktionen gehören ausdrücklich nicht zum aktuellen Implementierungsumfang.

Der Dedicated Server ist zunächst ein eigenständiger Lobby- und Verbindungsserver.

Er führt noch keine RimWorld-Spielsimulation aus.

## 16.2 Bestehende Implementierungen weiterverwenden

Prüfe vor jeder neuen Komponente, ob bereits eine entsprechende Implementierung existiert.

Wenn beispielsweise bereits ein TCP-Server vorhanden ist:

- Analysiere seine Architektur.
- Prüfe seine tatsächlichen Funktionen.
- Identifiziere fehlende Anforderungen.
- Ergänze gezielt fehlende Funktionen.
- Erstelle keine unnötige parallele Implementierung.

Dasselbe gilt für Netzwerkbibliotheken, Lobbykomponenten, Client-UI und Tests.

---

# 17. Zielarchitektur

Das Projekt soll logisch aus folgenden Komponenten bestehen.

## 17.1 Protocol

Verantwortlich für:

- Nachrichtentypen
- Nachrichten-Framing
- Serialisierung
- Protokollversionen
- Nachrichtenvalidierung
- Fehlercodes

## 17.2 Network Core

Verantwortlich für:

- Netzwerkverbindungen
- Senden und Empfangen
- Handshake
- Timeouts
- Heartbeats
- Cancellation
- Verbindungszustände
- Fehlerbehandlung

Die Bibliothek muss möglichst unabhängig von RimWorld bleiben.

## 17.3 Lobby Core

Verantwortlich für:

- Lobby-Erstellung
- Lobby-Beitritt
- Lobby-Verlassen
- Spielerzuordnung
- Ready-Status
- Lobby-Hosts
- Zustandsaktualisierungen

## 17.4 RimWorld Adapter

Verantwortlich für:

- Mod-Initialisierung
- RimWorld-spezifische Benutzeroberfläche
- Netzwerk-Adapter
- Ereignisverarbeitung
- Anzeige des aktuellen Lobbyzustands

## 17.5 Dedicated Server

Verantwortlich für:

- Eigenständigen Serverprozess
- Annahme von Verbindungen
- Sitzungsverwaltung
- Autoritative Lobbyverwaltung
- Nachrichtenverteilung
- Disconnect-Bereinigung
- Logging
- Konfiguration
- Graceful Shutdown

## 17.6 Erweiterungs-API

Die Architektur soll zukünftig ermöglichen:

- Registrierung zusätzlicher Multiplayer-Module
- Registrierung neuer Nachrichtentypen
- Zugriff auf Verbindungsstatus
- Zugriff auf Sitzungsinformationen
- Reaktion auf Netzwerkereignisse

Implementiere nur die Erweiterungspunkte, die für eine sinnvolle Foundation tatsächlich notwendig sind.

Vermeide verfrühte, ungetestete Abstraktionsschichten.

---

# 18. Netzwerkprotokoll

## 18.1 Grundanforderungen

Verwende eine geeignete, verifizierte .NET-Netzwerktechnologie.

TCP ist ein sinnvoller Ausgangspunkt, sofern die vorhandene Implementierung oder Architektur keine technisch begründete Alternative erfordert.

Vermeide unnötige externe Abhängigkeiten.

## 18.2 Nachrichtentypen

Das Protokoll muss mindestens geeignete Nachrichten für folgende Vorgänge besitzen:

- Handshake
- Handshake-Antwort
- Verbindung ablehnen
- Ping/Pong
- Lobby-Liste abrufen
- Lobby erstellen
- Lobby beitreten
- Lobby verlassen
- Lobbyzustand aktualisieren
- Spieler hinzugefügt
- Spieler entfernt
- Ready-Status ändern
- Disconnect
- Fehler

Verwende eine eindeutige Protokollversion.

## 18.3 Framing und Validierung

Beachte:

- TCP ist ein Byte-Stream.
- Einzelne Leseoperationen entsprechen nicht zwingend vollständigen Nachrichten.
- Nachrichten müssen zuverlässig abgegrenzt werden.
- Nachrichtengrößen müssen begrenzt werden.
- Eingaben müssen validiert werden.
- Fehlerhafte Clients dürfen nicht den gesamten Server zum Absturz bringen.

## 18.4 Serverautorität

Der Server besitzt die Autorität über den Lobbyzustand.

Clients dürfen Änderungen anfordern, aber nicht ungeprüft durchsetzen.

Prüfe insbesondere:

- Spieleridentität innerhalb der Sitzung
- Berechtigung zum Schließen einer Lobby
- Lobby-Kapazität
- Mehrfach-Beitritte
- Ready-Status
- Disconnects
- Reconnects

---

# 19. Dedicated Server

Der Server muss unter Linux ohne RimWorld und ohne grafische Oberfläche lauffähig sein.

## 19.1 Grundfunktionen

- Konfiguration laden
- TCP-Port öffnen
- Clients annehmen
- Mehrere Verbindungen verwalten
- Mehrere Lobbys verwalten
- Lobby-Informationen verteilen
- Spielerstatus aktualisieren
- Verbindungsabbrüche erkennen
- Sauber herunterfahren

## 19.2 Konfiguration

Verwende eine dokumentierte Konfigurationsdatei.

Beispielsweise:

`serverconfig.json`

Mit:

- Bind-Adresse
- Port
- Maximalen Clients
- Maximalen Spielern pro Lobby
- Timeouts
- Heartbeat-Einstellungen
- Log-Level

Verwende sichere Defaults.

Ohne geeignete Authentifizierung und Transportverschlüsselung ist der Server zunächst nur für kontrollierte Test- beziehungsweise LAN-Umgebungen geeignet.

## 19.3 Deployment

Erstelle beziehungsweise prüfe:

- Linux-Build-Skript
- Server-Startskript
- Beispielkonfiguration
- Installationsanleitung
- Systemd-Service-Beispiel
- Dokumentierte Portanforderungen

Installiere keine Systemdienste ohne ausdrückliche Freigabe.

---

# 20. RimWorld-Client

Der Client soll eine tatsächliche RimWorld-Mod-Assembly besitzen.

Er muss eine erreichbare Multiplayer-Oberfläche bereitstellen.

## 20.1 Verbindung

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
- Fehlgeschlagen
- Verbindung unterbrochen

## 20.2 Lobby-Browser

- Lobby-Liste abrufen
- Lobby erstellen
- Lobby beitreten
- Lobby-Informationen anzeigen

## 20.3 Lobby

- Lobbyname anzeigen
- Verbundene Spieler anzeigen
- Host anzeigen
- Ready-Status anzeigen
- Ready-Status verändern
- Lobby verlassen
- Lobby als Host schließen

Zustandsänderungen müssen über den Server übertragen werden.

## 20.4 Spielstart

Ein echter synchronisierter Spielstart gehört nicht zu Version 0.1.

Eine entsprechende Schaltfläche darf vorbereitet, aber nicht als funktionsfähig dargestellt werden.

---

# 21. Versionen und Mod-Kompatibilität

Ermittle die tatsächlich unterstützte RimWorld-Version.

Prüfe:

- Verse-Abhängigkeiten
- Verwendete Assemblies
- Harmony-Abhängigkeiten
- C#-Runtime
- Mod-Verzeichnisstruktur
- About.xml
- Build-Ziel

Berücksichtige RimWorld 1.6 und die Änderungen durch Waylith 1.7.

Trenne Engine-abhängigen Code von der Netzwerkbibliothek.

Eine erfolgreiche Kompilierung gegen eine bestimmte RimWorld-Version ist kein Nachweis für Unterstützung anderer Versionen.

Keine Multiplayer-Kompatibilität mit anderen Mods behaupten, solange sie nicht verifiziert wurde.

---

# TEIL VI – TESTS UND AKZEPTANZKRITERIEN

# 22. Verbindliche Tests

Erstelle beziehungsweise vervollständige automatisierte Tests.

## 22.1 Server

- Server kann starten.
- Konfiguration wird geladen.
- Port wird geöffnet.
- Server kann sauber beendet werden.
- Fehlerhafte Konfiguration wird erkannt.

## 22.2 Netzwerk

- Client kann verbinden.
- Handshake funktioniert.
- Zwei Clients können gleichzeitig verbinden.
- Fehlerhafte Nachrichten werden abgelehnt.
- Nachrichten-Framing funktioniert auch bei fragmentierten Daten.
- Verbindungsabbrüche werden erkannt.
- Heartbeats funktionieren.

## 22.3 Lobby

- Client A erstellt eine Lobby.
- Client B findet diese Lobby.
- Client B kann beitreten.
- Beide Clients erhalten denselben Lobbyzustand.
- Ready-Status wird übertragen.
- Verlassen wird synchronisiert.
- Disconnects aktualisieren die Spielerliste.
- Unberechtigte Aktionen werden abgewiesen.

## 22.4 RimWorld

Wenn eine lauffähige Spielinstallation vorhanden ist:

- Mod wird geladen.
- UI ist erreichbar.
- Verbindung zum Server funktioniert.
- Lobby kann erstellt werden.
- Spielerstatus wird angezeigt.
- Ready-Status wird übertragen.
- Verbindung kann beendet werden.

Kennzeichne nicht ausführbare Spieltests als offen.

## 22.5 Akzeptanz von Version 0.1

Die Version gilt erst dann als vollständig verifiziert, wenn:

- Der Client tatsächlich gebaut werden kann.
- Der Dedicated Server ausführbar ist.
- Eine echte Netzwerkverbindung funktioniert.
- Mindestens zwei unabhängige Clients verbunden werden können.
- Lobby-Operationen korrekt funktionieren.
- Relevante automatische Tests erfolgreich sind.
- Die RimWorld-Integration tatsächlich geprüft wurde.
- Die Dokumentation den tatsächlichen Stand wiedergibt.

Wenn ein Teil nicht nachgewiesen werden kann, kennzeichne die Version entsprechend als teilweise verifiziert.

---

# TEIL VII – ENTWICKLUNG NACH DEM ABBRUCH FORTSETZEN

# 23. Unterbrochene Masterprompt rekonstruieren

Nachdem die Governance-Migration abgeschlossen ist, ermittle, an welcher Stelle die ursprüngliche Masterprompt abgebrochen wurde.

Prüfe:

1. Welche ursprünglichen Phasen wurden abgeschlossen?
2. Welche Phasen wurden nur teilweise ausgeführt?
3. Welche Komponenten fehlen?
4. Welche Tests fehlen?
5. Welche offenen Implementierungen existieren?
6. Welche Branches enthalten bereits Arbeit?
7. Welche Dokumentation muss aktualisiert werden?

Erstelle keine vollständige Neuplanung ohne Bezug zum bestehenden Projekt.

Überführe die noch offenen Arbeiten in `TASKS.yaml`.

Priorisiere sie anhand ihrer technischen Abhängigkeiten.

# 24. Entwicklungsreihenfolge

Die folgende Reihenfolge ist ein Standard und darf anhand des tatsächlichen Projektzustands angepasst werden:

### Phase A – Recovery

Bestandsaufnahme, Git-Status und Recovery-Bericht.

### Phase B – Governance 2.0

Überprompt migrieren, Dokumentationssystem konsolidieren, automatische Commit-Regeln etablieren.

### Phase C – Bestehenden Code prüfen

Vorhandene Implementierungen analysieren, Builds und Tests durchführen, tatsächlichen Fortschritt bewerten.

### Phase D – Netzwerk-Core fertigstellen

Fehlende Protokoll- und Netzwerkfunktionen implementieren.

### Phase E – Dedicated Server fertigstellen

Server und Lobbyverwaltung vervollständigen.

### Phase F – RimWorld-Client fertigstellen

Mod, UI und Verbindung zum Server vervollständigen.

### Phase G – Integration

Echte Mehrclient-Tests durchführen.

### Phase H – Deployment

Build- und Startskripte vervollständigen.

### Phase I – Review

Änderungen dokumentieren, Verifizierungsnachweise erstellen und menschliche Merge-Freigabe vorbereiten.

**Bereits abgeschlossene Phasen müssen nicht unnötig wiederholt werden.**

---

# 25. Selbstständige Weiterentwicklung

Arbeite nach Abschluss jeder nicht blockierten Phase an der nächsten technisch sinnvollen Aufgabe weiter.

Du sollst nicht nach jedem Commit eine neue Bestätigung verlangen.

Wenn eine Aufgabe abgeschlossen ist:

1. Tests ausführen.
2. Dokumentation aktualisieren.
3. Automatisch committen.
4. Nächste Aufgabe auswählen.
5. Auf passendem Arbeitsbranch fortfahren.

Wenn eine Aufgabe auf noch nicht gemergten Änderungen aufbaut, dokumentiere die Branch-Abhängigkeit.

Führe keinen Merge ohne menschliche Freigabe durch.

Wenn eine Aufgabe blockiert ist, prüfe, ob unabhängige Arbeiten fortgesetzt werden können.

---

# TEIL VIII – ROADMAP

# 26. Zukünftige Projektphasen

Verwende folgende langfristige Entwicklungsziele.

**Version 0.1 – Connection & Lobby Foundation**

- Netzwerk-Core
- Dedicated Server
- Lobbyverwaltung
- RimWorld-Client
- Verbindungsmanagement

**Version 0.2 – Erweiterbare Netzwerkarchitektur**

- Erweiterungs-API
- Zusätzliche Netzwerkmodule
- Erweiterte Nachrichtenregistrierung
- Verbesserte Sitzungsverwaltung

**Version 0.3 – Session Foundation**

- Erweiterte Sitzungsinformationen
- Vorbereitung gemeinsamer Spielstände
- Kompatibilitätsinformationen

**Version 0.4 – Gameplay Sync Prototype**

- Erste kontrollierte Synchronisation ausgewählter Spielbefehle

**Version 0.5 – Deterministische Simulation**

- Experimentelle deterministische Simulation
- Desync-Erkennung
- Zustandsvergleich

**Version 1.0 – Gameplay Multiplayer**

Langfristiges Ziel einer tatsächlich spielbaren Multiplayer-Integration.

Diese Versionen sind geplante Meilensteine und keine zugesagten Funktionen oder Fertigstellungstermine.

---

# TEIL IX – ABSCHLUSS, ÜBERGABE UND HUMAN REVIEW

# 27. Abschluss jeder Arbeitseinheit

Nach einer abgeschlossenen Arbeitseinheit:

1. Relevante Tests ausführen.
2. Fehler dokumentieren.
3. Projektstatus aktualisieren.
4. Aufgabenstatus aktualisieren.
5. Architekturdateien bei Bedarf aktualisieren.
6. Menschlich verständliche Änderungen dokumentieren.
7. Automatisch committen.
8. Git-Status prüfen.

# 28. Abschluss der Codex-Sitzung

Aktualisiere:

- `PROJECT_STATE.yaml`
- `TASKS.yaml`
- `SESSION_HANDOFF.md`
- Relevante technische Dokumentation
- Gegebenenfalls `CHANGELOG.md`
- Gegebenenfalls Review-Berichte

Gib dem Benutzer anschließend eine verständliche Zusammenfassung mit:

- Tatsächlich bearbeiteten Aufgaben
- Geänderten Dateien beziehungsweise Komponenten
- Erfolgreichen Builds
- Erfolgreichen Tests
- Fehlgeschlagenen Tests
- Nicht ausgeführten Tests
- Bekannten Problemen
- Aktuellem Branch
- Erstellten Commits
- Ausstehenden Reviews
- Nächstem sinnvollen Schritt

Keine unbelegten Erfolgsmeldungen.

# 29. Review und Merge

**Automatische Commits: JA.**

**Automatische Tests: JA.**

**Automatische Dokumentation: JA.**

**Automatisches Weiterarbeiten: JA.**

**Automatische Merges: NEIN.**

**Automatische Pushes ohne Freigabe: NEIN.**

Jede Integration benötigt eine ausdrückliche menschliche Freigabe.

Bereite Review-Unterlagen so vor, dass ein Mensch die Änderungen nachvollziehen kann.

---

# TEIL X – KONKRETE AUSFÜHRUNGSREIHENFOLGE FÜR DIESE SITZUNG

# 30. Beginne jetzt

Die ursprüngliche Masterprompt wurde unterbrochen.

Die ursprüngliche Überprompt ist inzwischen durch dieses Dokument fachlich überarbeitet worden.

**Dein Auftrag besteht darin, die Überprompt 2.0 in das bestehende Projekt zu übernehmen und danach die begonnene Entwicklung fortzusetzen.**

Arbeite in exakt dieser logischen Reihenfolge:

## Schritt 1 – Bestand sichern und untersuchen

- Arbeitsverzeichnis prüfen.
- Git-Status prüfen.
- Vorhandene Branches prüfen.
- Bereits erstellte Dateien identifizieren.
- Uncommittete Änderungen schützen.
- Bestehende Implementierungen untersuchen.
- Recovery-Bericht erstellen.

## Schritt 2 – Governance-Migration

- Bisherige AGENTS.md untersuchen.
- Vorhandene Dokumentationsregeln prüfen.
- Neue Regeln der Überprompt 2.0 integrieren.
- Fehlende Governance-Dateien ergänzen.
- Automatische Commits verbindlich festlegen.
- Menschliche Merge-Freigabe verbindlich festlegen.
- Kontextverwaltung verbessern.
- Statusmodelle aktualisieren.
- Konsistenzprüfungen ergänzen.

## Schritt 3 – Governance validieren

- Dokumentationsdateien prüfen.
- YAML validieren.
- Referenzen prüfen.
- Migrationsbericht aktualisieren.
- Governance-Änderungen automatisch committen.

**Kein Merge.**

## Schritt 4 – Technischen Projektstand erneut bewerten

- Vorhandenen Code prüfen.
- Bereits umgesetzte Funktionen erfassen.
- Bestehende Builds untersuchen.
- Vorhandene Tests ausführen.
- Tatsächlichen Entwicklungsstand bestimmen.
- Projektstatus aktualisieren.

## Schritt 5 – Alte Masterprompt fortsetzen

- Unvollständige Aufgaben identifizieren.
- Fehlende Aufgaben in TASKS.yaml aufnehmen.
- Technische Abhängigkeiten ermitteln.
- Geeignete Arbeitsbranches verwenden.
- Bestehende Implementierungen gezielt vervollständigen.

## Schritt 6 – Multiplayer Foundation weiterentwickeln

Priorität:

1. Funktionierender Netzwerk-Core
2. Funktionierender Dedicated Server
3. Funktionierende Lobbyverwaltung
4. RimWorld-Client
5. Integration
6. Automatisierte Tests
7. Deployment

Passe die Reihenfolge an, wenn Komponenten bereits abgeschlossen sind oder technische Abhängigkeiten eine andere Reihenfolge erfordern.

## Schritt 7 – Kontinuierlich prüfen und committen

Nach jeder abgeschlossenen, sinnvollen Arbeitseinheit:

- Prüfen
- Dokumentieren
- Committen
- Projektstatus aktualisieren
- Weiterarbeiten

## Schritt 8 – Menschliche Review vorbereiten

Erstelle verständliche Berichte für alle Änderungen, die zur Integration bereit sind.

Markiere entsprechende Aufgaben als:

`ready_for_human_review`

**Führe keinen Merge durch.**

---

# VERBINDLICHE SCHLUSSANWEISUNG

Dieses Projekt wurde bereits begonnen und darf nicht unnötig neu aufgebaut werden.

Die bisherige Überprompt wurde überarbeitet und muss jetzt auf **Version 2.0** migriert werden.

Die bisherige Masterprompt wurde während der Ausführung abgebrochen und muss anhand des tatsächlichen Projektzustands fortgesetzt werden.

Du sollst:

- Bestehende Arbeit erhalten.
- Den tatsächlichen Projektstand rekonstruieren.
- Das KI-Projektgedächtnis verbessern.
- Git- und Commit-Prozesse automatisieren.
- Menschliche Merge-Kontrolle gewährleisten.
- Redundante Dokumentation reduzieren.
- Den KI-Kontextverbrauch optimieren.
- Bestehende Multiplayer-Komponenten weiterentwickeln.
- Neue Funktionen tatsächlich implementieren.
- Verfügbare Builds und Tests durchführen.
- Ergebnisse nachweisbar dokumentieren.
- Automatisch Commits erstellen.
- Ohne unnötige Rückfragen weiterarbeiten.

**Dein Ziel ist nicht, eine weitere theoretische Planung zu erstellen.**

**Dein Ziel ist, das bereits bestehende Projekt auf den neuen Entwicklungsstandard zu migrieren und die RimWorld Multiplayer Foundation 0.1 tatsächlich weiterzuentwickeln.**

Die drei obersten technischen Grundsätze lauten:

**1. Bestehende Arbeit schützen und sinnvoll fortsetzen.**

**2. Jeden relevanten Fortschritt automatisch prüfen, dokumentieren und committen.**

**3. Niemals ohne ausdrückliche menschliche Freigabe mergen.**

**BEGINNE JETZT MIT DER BESTANDSAUFNAHME UND MIGRATION AUF VERSION 2.0.**