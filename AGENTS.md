# AGENTS.md — Bindende Regeln für OpenAI Codex

## Scope und Initialprüfung (vor jeder Änderung)

1. Die Wissensbasis und `examples/` sind Referenz, kein produktiver Mod-Code. Der tatsächliche Produktstand steht ausschließlich in `docs/ai/PROJECT_STATE.yaml`.
2. Vor dem Schreiben die konkrete **RimWorld-Version einschließlich Build** und den ausgewählten Zweig **1.6 (Unity/Mono)** oder **1.7 (Waylith/CoreCLR)** ermitteln; im Arbeitsbericht dokumentieren. Falls keine Installation zugänglich: **blockiert/nicht verifiziert** ausweisen.
3. Bei Multiplayer-Wunsch: `research/07-11-*.md`, `codex/multiplayer-rules.md` und `references/verified-apis.md` lesen. **1.7+MP nicht als unterstützt bezeichnen**, solange `Prepatcher`-Blocker und Tests bestehen.
4. Vor der Implementierung 1-seitige Designnotiz schreiben: Ziel, beteiligte Defs/Klassen, Zustandsmodell, Persistenz, UI-Aktionen, Netzwerk-Sync, Konfliktbereich, Performance-Risiken, Tests, Rollback.

## Wahrheit, Quellen, Sicherheit

5. Verfügbare APIs gegen **passende installierte Assemblies** oder getaggte Repo-Quellen **prüfen**, keine Methoden/Properties erfinden. Nur `BESTÄTIGT`-Einträge aus `references/verified-apis.md` ohne Vorbehalt verwenden, aber versionierte Signatur trotzdem verifizieren.
6. Ein GitHub-`master`-Link ist **nicht unveränderlich**. Vor produktivem Code Commit hash pinnen; in der PR nennen. Alte Wiki-Snippets als historische Illustration behandeln.
7. Keine kommerziellen RimWorld-DLLs, Assets oder aus Assemblies dekompilierten Quellcode ins Repository, zu Releases oder in Modell-Prompts verteilen. Lokale Referenzen außerhalb des Repos halten.
8. Externe Framework-Lizenzen prüfen; Code nicht ohne Lizenzprüfung kopieren. NuGet-Abhängigkeiten versionieren.
9. Research-Status `BESTÄTIGT|WAHRSCHEINLICH|UNGEKLÄRT|VERALTET|SPEKULATIV` auf neue strittige Aussagen anwenden.

## Implementierung

10. Für simple neue Dinge/Rezepte/Forschung XML `Defs/` benutzen. Für vorhandene Defs `Patches/` mit stabilen XPath-Prädikaten und Kompatibilitäts-Guards.
11. Für Verhalten zuerst `ThingComp`, `HediffComp`, `StatWorker`, `JobDriver`, `MapComponent`, `GameComponent` u. ä. ansehen. Harmony erst, wenn keine robuste öffentliche Erweiterung ausreicht.
12. Harmony Prefix darf Original nicht unabsichtlich überspringen; Transpiler müssen gegen exakten IL-Stand abgesichert werden. Vor-/Nach-Prioritäten mit tatsächlichen Mods prüfen; keine blanket `catch {}`-Unterdrückungen.
13. Simulationszustand gehört in game-/map-/thing-gebundene Strukturen und muss via `ExposeData/Scribe` persistierbar sein; statische mutable Weltvariablen vermeiden. Verknüpfungen nach Load wiederherstellen.
14. Keine Weltzustandsmutation in reinem UI-Code, GUI-Zeichenroutinen, `Update`/Frame-Callbacks, Eventhandlers oder asynchronen Hintergrundthreads ohne geprüfte Sync-Brücke.
15. Simulation nur vom autorisierten Tick-/synchronisierten Kommando-Kontext aus verändern. UI erzeugt deklarative Befehle; deterministischer Command-Handler verarbeitet sie.
16. Zufall nur mit dokumentiert kontrollierter `Verse.Rand`-Strategie in replizierten Aktionen. Kein unkoordinierter `System.Random`, `UnityEngine.Random`, Echtzeit-/Netzwerk-/Thread-Reihenfolge als Spielentscheidungsquelle.
17. Ungeordnete `Dictionary`/`HashSet`-Iteration nicht als kanonische Spiel-Entscheidungsreihenfolge behandeln; Schlüssel stabil sortieren. Keine unstabilen Hashcodes als persistente IDs.
18. Keine neuen Parallelisierungen im mutationsführenden Tick-Pfad ohne explizit nachgewiesenen deterministischen Scheduler/Reintegration auf Tick-Grenze.
19. `MP.enabled`/`MP.RegisterAll`, `[SyncMethod]`, `RegisterSyncMethod/Field`, `MP.WatchBegin/Watch/WatchEnd` nur nach gegen lokale MP-API-Assembly verifizierten Signaturen einsetzen. **`MP.IsExecutingSyncCommand` ist in dieser Wissensbasis nicht ausreichend verifiziert**.
20. Gameplay-Einstellungen, RNG-Seeds und modrelevante statische Zustände müssen für alle Peers identisch sein; UI-/Layout-Präferenzen können clientlokal bleiben.
21. Builds strikt nach lokal geprüfter Runtime trennen: Der installierte 1.6-Build referenziert `netstandard2.1`; der aktuelle Adapter baut damit gegen dessen Assemblies. 1.7/Waylith bleibt ein separater, unbestätigter `net10.0`-Pfad. Keine Assemblies zwischen Spielversionen mischen oder Laufzeitkompatibilität aus einem Build ableiten.

## Nachweis und Diagnose

22. Nach Änderungstyp risikobasiert prüfen: XML/Referenzen, Build, Tests und bei RimWorld-Code nach Möglichkeit Game-Start, UI und Save/Load. Nicht ausgeführte Prüfungen offen nennen.
23. Für Multiplayer mit **zwei echten Clients** mindestens Join, gleiche Mod-Reihenfolge, Aktion, gleiche RNG-Sequenz, Pausieren/Speeds, Save/Load, Reconnect, 30min-Tick-Lauf, Host/Client-Zustandsvergleich testen. Nicht durch einen Singleplayer- oder Headless-Start ersetzen.
24. Bei Desync minimal reproduzierbare Repro isolieren und Logs beider Peers, Spiel-/Mod-Builds, Ticks, betroffene Map und den **ersten abweichenden Zustand** dokumentieren. Ursachen nach `codex/debugging-workflow.md` eingrenzen.
25. Tests ehrlich klassifizieren: `XML_VALIDIERT`, `BUILD_ERFOLGREICH`, `IN_GAME_GETESTET`, `MP_ZWEI_CLIENTS_GETESTET`, `NICHT_GETESTET`. Keine Upgrade-Kompatibilität ohne gezielte Tests bescheinigen.
26. Änderungen als atomare Git-Commits dokumentieren; bei Patch-Konflikten Rollback/Feature-Toggle anbieten; nie unbekannte Userdateien überschreiben.
27. 1.7 `-Headless` nur nach Verifizierung des lokalen Startprogramms und Prozessverhaltens; `-NoWindow` ist semantisch etwas anderes. Kein automatisch behaupteter erfolgreicher Lauf.

## Fertigstellungsbericht (immer)

Nenne verwendete RimWorld-/MP-/SDK-Builds, Dateien, APIs mit Referenzen, Build-Befehl/Ergebnis, tatsächlich ausgeführte Tests mit Logpfad, Desync-Befund, offene Risiken, erlaubte Zielversion(en), bekannte Kompatibilitätsgrenzen. Bei unzugänglicher Spielinstallation ausdrücklich `LAUFZEIT NICHT VERIFIZIERT`.

## Projektworkflow 2.0

28. Sitzungsstart: diese Datei, `docs/ai/PROJECT_STATE.yaml`, `TASKS.yaml`, `SESSION_HANDOFF.md` und Git-Status lesen. Mehr Kontext gezielt über `CONTEXT_INDEX.md` laden; umfangreiche Forschung nur bei neuer API-/Versionsfrage.
29. `PROJECT_STATE.yaml` ist der technische Ist-Zustand, `TASKS.yaml` das Aufgabenregister. Implementierungs-, Verifikations- und Integrationsstatus sind getrennt; Werte und Nachweise dort pflegen.
30. Größere Aufgaben erhalten vor Code einen kurzen Vertrag mit Ziel, Umfang, Nicht-Zielen, Abhängigkeiten, Akzeptanz, Tests, Risiken und Branch. Technische Designnotiz nach Regel 4 ergänzen.
31. Nur auf Arbeitsbranches entwickeln. Abhängige, ungemergte Branches dokumentieren. Nach jeder abgeschlossenen Arbeitseinheit zugehörige Dateien prüfen, Dokumentation aktualisieren, ausschließlich eigene Änderungen stagen und automatisch einen Conventional Commit erstellen.
32. **Jeder Merge** benötigt ausdrückliche menschliche Review-Freigabe, unabhängig vom Zielbranch. Kein Push ohne ausdrückliche Freigabe. Keine eigenständigen Statuswechsel zu `approved`/`merged`, kein Force-Push, destruktiver Reset oder Verwerfen fremder Arbeit.
33. Tests nach Risiko auswählen und Befehle, Ergebnisse, Exit-Codes und Grenzen in `docs/verification/` belegen. Build bedeutet nicht Laufzeitfunktion; Fehler mit Hypothese, gezielter Korrektur und Wiederholung untersuchen.
34. Fachdocs, CODEMAP, Changelog, Review und Übergabe nach tatsächlichen Änderungen pflegen; Fakten, Pläne und Behauptungen trennen. Details: `docs/governance/WORKFLOW.md`, `REVIEW_POLICY.md`, `DOCUMENTATION_POLICY.md`.
35. `HEAD` ist die dynamische Referenz auf den aktuellen Branch-Commit (`git rev-parse HEAD`); der Commit kann seinen eigenen Hash nicht enthalten. Bei Widerspruch Git, Code und Prüfbelege vor Dokumentationskorrektur untersuchen.
