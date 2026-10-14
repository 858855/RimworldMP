# Gesamtbericht — RimWorld / Waylith / Modding / Multiplayer

**Stand:** 14.10.2026, basierend auf Web-Primärquellen und Stichproben in Source-Dateien; **keine lokale Game-/Multi-Client-Laufzeitprüfung.**

## A. Stand RimWorld

RimWorld 1.6 bleibt der Steam-Hauptzweig; RimWorld 1.7 wurde am **12. Oktober 2026** als **unstable Beta** angekündigt und bereitgestellt. Die präzise Hauptzweig-Unterpatchnummer der Nutzerinstallation wurde nicht erhoben. Offizielle Quelle: https://ludeon.com/blog/2026/10/announcing-the-waylith-engine-and-update-1-7/

## B. Engine-Entwicklung

**Nicht mehr Spekulation:** 1.7 migriert von Unity auf Ludeons neue **Waylith-Engine**, basierend auf nativen BRUTAL-Bindings mit Vulkan und **.NET 10/CoreCLR**. Ludeon behält Gamecode in `Assembly-CSharp.dll` weitgehend bei und bietet mit `UnityStubs.dll` Unity-Kompatibilität, während `Waylith.dll` Enginefunktion übernimmt. Der offizielle Primer nennt `Prepatcher` als explizit inkompatibel wegen fehlender Early-Hook-/Restart-Unterstützung. Für Modder entstehen `-Headless`, `-NoWindow`, Asset-Baking und Job-System. Die Herstellerleistungsangabe **2- bis 4-fach** ist nicht unabhängig gemessen. Quelle: https://docs.google.com/document/d/e/2PACX-1vSykw5_DYaaw42nEznI9zmBZVT_Vgg2bxYq_v2XWvAsHC3QSKFuDK2OyiQ5WC9jvVRUcrjpFt7xLLF5/pub

## C. Modding-Möglichkeiten

Defs/XML für Inhalte und Datenänderungen; C#-Components, Jobs, Incident-/StatWorkers für eigene Simulation und UI; Harmony für gezielte Modifikation bestehender Methoden. Für 1.6 typischer `net472` Build, für 1.7 offizielle `net10.0` Empfehlung. Große Spielcode-Kompatibilität **ohne** automatische Garantie für dynamische Loader, Assets, bestimmte Reflection oder Harmony3. Siehe `research/03-06` und `examples/`.

## D. Multiplayer-Architektur

`rwmt/Multiplayer` arbeitet grundlegend mit **deterministischem Lockstep** und zeitlich synchronisierten Commands. In `TickPatch.RunCmds` werden `ScheduledCommand`s anhand `ticks == Timer` verarbeitet, `TickableById(cmd.mapId)` bestimmt den World-/Map-Context und `ExecuteCmd` führt sie aus. `AsyncTimeComp.ExecuteCmd` stellt u. a. aktiven Map-/Faction-Kontext her; `UpdateManagers` behandelt ausdrücklich nichtdeterministische Update-Aufrufe. Serialization/SyncMethod und `MultiplayerGame`-Komponenten führen Daten-/Zeitmodelle zusammen. Detail: `research/07-multiplayer-architecture.md`.

## E. Multiplayer-kompatible eigene Mods

Die API unterscheidet `SyncMethod`, `SyncField+Watch`, `SyncWorker`. Der **ältere API-Wiki-Inhalt** belegt die Konzepte, während `RimWorld.MultiplayerAPI 0.6.0` auf NuGet `net472` als Paketstand ausweist. Der genaue aktuelle DLL-Signaturvergleich ist noch ausstehend. UI-Interaktionen dürfen nicht lokal replizierten State verändern; mutierende Logic gehört in synchronisierten Command-/Tick-Kontext. RNG, Sortierungsordnung, Maps, Saves, Cache, ModSettings und Harmony-Kompat müssen geprüft werden. Siehe `research/08-10`.

## F. Risiken

**Kritisch:** Multiplayer-Abhängigkeit von Prepatcher blockiert vorläufig 1.7-Waylith-MP. Weitere Hauptgefahren: extra RNG Calls, UI-Zustandsänderungen ohne Sync, `Dictionary`-Iteration, Thread-Asynchronität, SaveLoad-Verluste, statischer mutable State, unterschiedliche Modversionen, Harmony-/IL-Patches, Linux Case-Sensitivity, abweichende Runtime/Graphics.

## G. Codex-Eignung

Codex kann mit dieser Knowledge Base Mod-Strukturen anlegen, XML statisch prüfen, mit legal lokal verfügbaren Game-Referenzen C# bauen, API-Gebrauch und Harmony-Patches reviewen, deterministische Zustands-/Command-Schnittstellen entwerfen und Desync-Logs systematisch vergleichen. Codex **kann ohne installierte proprietäre DLLs** keine echten Game-Klassen kompilieren; ohne laufenden Client weder Save/Load noch UI-Gameplay validieren; ohne zwei passende MP-Instanzen keine multiplayerfähige Mod garantieren. 1.7-Headless erleichtert automatisierte Smoke-Tests, ersetzt nicht alle UI- oder Multiplayer-Tests.

## H. Empfehlungen

Für Multiplayerziel **zunächst RimWorld 1.6** wählen, exakten Build und MP-Version pinnen, Datenteil/Simulation/UI/Compat strikt trennen, XML/Vanilla-Komponenten vor Harmony. Zu jedem Feature einen deterministischen Command, Persistenz-/Tick-Plan und echte Tests planen. 1.7 als separate experimentelle Waylith-Branch ohne MP-Freigabe nutzen, bis Prepatcher-Blockade und `rwmt`-Support geklärt sind. Mit `AGENTS.md` und Codex-Workflows die Regeln zu festen Review-Gates machen.

## I. Offene Punkte

Kein verifizierter 1.7-Multiplayer-Mod, keine tiefe aktuelle Prüfung sämtlicher Network-Packet-/Rejoin-Verzweigungen; API-Wiki teilweise veraltet; konkrete `MP.IsExecutingSyncCommand`-Signatur nicht bestätigt; Harmony3 und Runtime/SDK-Buildvariante müssen mit lokalem Game gemessen werden; fehlende commitfeste Permalinks; keine Cross-OS-/2-Client-Tests. Dokumentiert in `references/open-questions.md`.

## Evidenz-/Qualitätsstand der gelieferten Dateien

- Dokumentation: offizielle Quellen, Entwickler-Repo-Code und Community-Quellen sauber getrennt.
- Codevorlagen: **keine erfolgte C#-Kompilation oder Gameplaytests**; unvollständige Beispiel-ThingDef/-Dialog-Features ausdrücklich markiert.
- XML: Struktur-/Syntaxprüfung mit beigefügtem Python-Skript möglich (Ergebnis siehe Auslieferungsprüfung).
- Netzwerkprotokoll: solide Hauptkonzepte aus direktem MP-Quellcode, keine vollständige forensische Netzwerkinterna-Abdeckung.
- Bewusste Grenze: Dieses Archiv ist eine ausführliche **Forschungs-Startbasis**, keine 100%-Abdeckung sämtlicher aktuell existierender Spiel- oder Mod-APIs.

## Quellen

Eine nach Thema gruppierte Liste der Primärquellen und Einschränkungen liegt in `references/sources.md`, komplette Quellpfade in `references/repositories.md`, verifizierte Symbole in `references/verified-apis.md`.
