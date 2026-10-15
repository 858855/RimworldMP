# Fortsetzungsprompt – RimWorld Multiplayer Foundation

Du bist OpenAI Codex und setzt das **bestehende** Projekt „RimWorld Multiplayer Foundation“ fort. Arbeite auf Deutsch. Das Repository ist `https://github.com/858855/RimworldMP`. Verwende einen vorhandenen lokalen Checkout, falls er existiert; auf dem bisherigen Rechner liegt er unter `/mnt/Arbeit/Git Projekte/RimworldMP`. Erstelle kein zweites Projekt und überschreibe keine Benutzerarbeit.

## 1. Initialanweisungen und aktuellen Stand laden

Lies zu Beginn **vollständig** und in dieser Reihenfolge:

1. `AGENTS.md` und gegebenenfalls weitere für deinen Arbeitsbereich geltende `AGENTS.md`.
2. `docs/governance/PROMPT_HISTORY.md`.
3. `docs/governance/history/UEBERPROMPT_V1.md` – ursprüngliche Überprompt.
4. `docs/governance/history/MASTERPROMPT_V1.md` – ursprüngliche Masterprompt.
5. `docs/governance/history/MASTERPROMPT_V2.md` – Masterprompt 2.0 mit Überprompt 2.0.
6. `docs/ai/PROJECT_STATE.yaml`, `docs/ai/TASKS.yaml`, `docs/ai/SESSION_HANDOFF.md` und `docs/ai/CONTEXT_INDEX.md`.

Die Volltexte aus Schritt 3–5 werden **für diese Wiederaufnahme ausdrücklich geladen**. Bei Widerspruch ersetzt Version 2.0 die älteren Projektregeln; aktuelle direkte Benutzeranweisungen und übergeordnete Regeln haben Vorrang. Für spätere einzelne Aufgaben reicht wieder der gezielte Kontext aus `CONTEXT_INDEX.md`. Die historischen Prompts beschreiben Ziele und Regeln, **nicht** den bereits nachgewiesenen Implementierungsstand. Dafür gelten Git, Quellcode, `PROJECT_STATE.yaml` und Prüfnachweise.

Prüfe danach `git status`, aktuellen Branch, lokale und Remote-Branches, `git log`, `git remote -v` und uncommittete Änderungen. Vergleiche mit `origin/main`; fetch nur ohne fremde Arbeit zu verändern. Lies zusätzlich die für die nächste Aufgabe genannten Dateien, besonders `docs/client.md`, `docs/testing.md`, `docs/build.md`, `docs/verification/2026-10-15-client.md` und `docs/verification/2026-10-15-rimworld-runtime-retry.md`. Prüfe die tatsächliche lokale RimWorld-Version und Assemblies erneut, bevor du Spielcode änderst.

## 2. Letzter belegter Stand am 15. Oktober 2026

Dies ist eine Einstiegshilfe; **verifiziere sie gegen die aktuellen Dateien**. `main` stand vor diesem Fortsetzungsprompt bei `bfc9fcd` und war mit `origin/main` synchron. Die Originalprompts und KI-Governance liegen bereits im Repository. Produktversion: `0.1.0-dev`. Versioniertes TCP-Protokoll, Netzwerk-Core, autoritativer Lobby-Core und Linux-Dedicated-Server sind implementiert. 13 automatische Integrationstestgruppen mit zwei echten TCP-Testclients bestanden; der RimWorld-1.6-Mod wurde gegen die lokalen Spiel-Assemblies gebaut, seine UI im laufenden Spiel aber **nicht** nachgewiesen. Keine Gameplay-Synchronisation, keine 1.7-Freigabe, keine Authentifizierung oder TLS.

**Aktueller Hängepunkt:** Ein isolierter grafischer RimWorld-Start mit Core, offiziellen DLCs und `rimworldmp.foundation` öffnete ein OpenGL-Fenster, erreichte innerhalb von 180 Sekunden aber keinen belegten Mod-Ladevorgang oder die bedienbare UI (Exit 124). Ein früherer `-nographics`-Versuch scheiterte an Unity-Atlasfehlern. `Version.txt` meldete `1.6.4871 rev598`, der Spielprozess `rev600`; diese Abweichung ist ungeklärt. Der temporäre Mod-Symlink wurde entfernt; die normale Benutzer-Modliste wurde nicht verändert. Lies den genannten Laufzeit-Prüfbericht, bevor du erneut startest. Behaupte keinen erfolgreichen RimWorld- oder Zweispieler-Test.

## 3. Konkrete Fortsetzung

1. Ermittele, ob ein normales RimWorld mit einer **isolierten** Konfiguration auf diesem Rechner bis zum Hauptmenü startet. Unterscheide einen Umgebungs-/Grafik-/Steam-Hänger von einem Fehler des Foundation-Mods. Wiederhole nicht einfach denselben 180-Sekunden-Lauf ohne neue Diagnose. Schütze Benutzer-Modliste und Spielstände; installiere keinen Systemdienst.
2. Baue den Mod gegen die tatsächlich vorhandenen 1.6-Assemblies, prüfe die Paketstruktur und aktiviere ihn nur in einer kontrollierten Testkonfiguration. Erfasse den Spiel-Log bis zum Mod-Laden. Prüfe dann, ob **Optionen → Mod-Einstellungen → Multiplayer Foundation** erreichbar ist. Falls die grafische Oberfläche nur durch Benutzerinteraktion zugänglich ist, gib einen präzisen manuellen Testschritt an und markiere das Ergebnis bis zur Rückmeldung als offen.
3. Starte den Linux-Dedicated-Server lokal. Prüfe aus der RimWorld-Oberfläche Verbindung, Lobby-Liste, Erstellen, Beitreten, Ready, Verlassen/Schließen und Disconnect. Danach zwei unabhängige RimWorld-Clients testen. Die bereits bestandenen TCP-Testclients ersetzen diesen Spieltest nicht.
4. Wenn die Laufzeitprüfung blockiert bleibt, dokumentiere die Ursache und arbeite an einer unabhängigen, priorisierten Aufgabe aus `TASKS.yaml` weiter. Erfinde keine RimWorld-APIs oder Testresultate.

Nützliche Befehle stehen verbindlich in `docs/build.md` und `docs/testing.md`. Auf dem bisherigen Rechner liegt das .NET-SDK unter `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet`; diese Pfadangabe ist keine portable Voraussetzung. Für Dokumentationsänderungen `python3 scripts/validate_project_state.py` ausführen; für Codeänderungen die betroffenen Builds und Tests. Protokolliere Befehl, Umgebung, Exit-Code und tatsächliche Beobachtung unter `docs/verification/`.

## 4. Git, Dokumentation und Übergabe

Arbeite auf einem passenden Arbeitsbranch; prüfe Branch-Abhängigkeiten. Nach jeder abgeschlossenen Arbeitseinheit: gezielt testen, `PROJECT_STATE.yaml`, `TASKS.yaml`, betroffene Fachdocs, `SESSION_HANDOFF.md`, Changelog und Review-Unterlagen aktualisieren, nur eigene Dateien stagen und einen aussagekräftigen Conventional Commit erstellen. Drei getrennte Statusdimensionen gelten: Implementierung, Verifikation, Integration. Ein Build ist kein Spieltest. Ein fehlgeschlagener Lauf bleibt fehlgeschlagen.

**Kein Merge ohne ausdrückliche menschliche Prüfung und Freigabe – für jedes Ziel und jede Merge-Art. Kein Push ohne ausdrückliche Freigabe, kein Force-Push, kein destruktiver Reset und kein Verwerfen fremder Änderungen.** Diese Fortsetzungsprompt erteilt selbst keine künftige Merge- oder Push-Freigabe. Bereite Änderungen und einen Review-Bericht vor; setze `ready_for_human_review`, wenn sie prüfbar sind.

Zum Sitzungsende nenne auf Deutsch: tatsächlich entwickelte Änderungen, erfolgreiche und fehlgeschlagene Builds/Tests, nicht ausgeführte Spieltests, bekannte Probleme, Branch/Commits, nötige menschliche Prüfung, Start-/Testbefehle und den nächsten konkreten Schritt. Halte die versionierte Projektübergabe so aktuell, dass eine weitere Sitzung ohne Chatverlauf fortfahren kann.
