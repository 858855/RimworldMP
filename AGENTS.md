# AGENTS.md — Bindende Regeln für OpenAI Codex

## Scope und Initialprüfung (vor jeder Änderung)

1. Dieses Repository ist **Recherche/Wissensbasis, kein lauffähiger RimWorld-Mod**. Keine vermeintlichen `examples/` als produktionsfertigen Code behandeln.
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
21. Builds strikt trennen: **1.6 `net472`**, **1.7 `net10.0`**. Keine 1.7-DLL in einen 1.6-Build einschleusen oder umgekehrt. Versionierte `Assemblies/` und `LoadFolders.xml` nur mit tatsächlichem Runtime-Nachweis verwenden.

## Nachweis und Diagnose

22. Pro Änderung XML parsen, fehlende Texturen/Dateireferenzen prüfen, C# bauen, danach RimWorld starten, Defs laden, Spielzustand ändern, save/load neu starten.
23. Für Multiplayer mit **zwei echten Clients** mindestens Join, gleiche Mod-Reihenfolge, Aktion, gleiche RNG-Sequenz, Pausieren/Speeds, Save/Load, Reconnect, 30min-Tick-Lauf, Host/Client-Zustandsvergleich testen. Nicht durch einen Singleplayer- oder Headless-Start ersetzen.
24. Bei Desync minimal reproduzierbare Repro isolieren und Logs beider Peers, Spiel-/Mod-Builds, Ticks, betroffene Map und den **ersten abweichenden Zustand** dokumentieren. Ursachen nach `codex/debugging-workflow.md` eingrenzen.
25. Tests ehrlich klassifizieren: `XML_VALIDIERT`, `BUILD_ERFOLGREICH`, `IN_GAME_GETESTET`, `MP_ZWEI_CLIENTS_GETESTET`, `NICHT_GETESTET`. Keine Upgrade-Kompatibilität ohne gezielte Tests bescheinigen.
26. Änderungen als atomare Git-Commits dokumentieren; bei Patch-Konflikten Rollback/Feature-Toggle anbieten; nie unbekannte Userdateien überschreiben.
27. 1.7 `-Headless` nur nach Verifizierung des lokalen Startprogramms und Prozessverhaltens; `-NoWindow` ist semantisch etwas anderes. Kein automatisch behaupteter erfolgreicher Lauf.

## Fertigstellungsbericht (immer)

Nenne verwendete RimWorld-/MP-/SDK-Builds, Dateien, APIs mit Referenzen, Build-Befehl/Ergebnis, tatsächlich ausgeführte Tests mit Logpfad, Desync-Befund, offene Risiken, erlaubte Zielversion(en), bekannte Kompatibilitätsgrenzen. Bei unzugänglicher Spielinstallation ausdrücklich `LAUFZEIT NICHT VERIFIZIERT`.

## Persistentes Projektgedächtnis und Git-Workflow

28. Zu Sitzungsbeginn `AGENTS.md`, `docs/ai/PROJECT_STATE.yaml`, `docs/ai/TASKS.yaml`, `docs/ai/SESSION_HANDOFF.md` lesen; `docs/ai/CONTEXT_INDEX.md` für weitere aufgabenspezifische Quellen verwenden. Git-Branch, Status und letzten Commit prüfen.
29. Dokumentationsstatus: `open`, `in_progress`, `blocked`, `ready_for_human_review`, `done`; Definitionen in `docs/ai/TASKS.yaml`. Forschungsstatus (`BESTÄTIGT` usw.) bleibt davon getrennt.
30. Feature- und größere Fix-Arbeit auf eigenem Branch erledigen. Keine eigenständigen Merges in `main`/`master` oder geschützte Integrationsbranches, keine Review-Freigabe fingieren, kein Force-Push und keine fremden Änderungen verwerfen. Bei Integrationsreife `ready_for_human_review` setzen.
31. Nach jeder relevanten Arbeit Projektstatus, Aufgaben, Architektur/Dateikarte bei Bedarf, Verifizierungsnachweis, Review-Zusammenfassung, `CHANGELOG.md` und `SESSION_HANDOFF.md` an den tatsächlichen Zustand anpassen. Fakten, Annahmen, Pläne und Tests getrennt halten.
32. Für Dokumentation in Git gilt `HEAD` als dynamische Commit-Referenz des dokumentierten Branches (`git rev-parse HEAD`). Ein eigener Commit-Hash kann nicht wahrheitsgemäß im selben Commit gespeichert werden.
33. Bei Abweichung zwischen Dokumentation und Repository zuerst Code, Git und Prüfnachweise untersuchen; anschließend die betroffenen Statusdateien korrigieren.
