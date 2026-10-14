# 01 — RimWorld-Architektur und bedeutende Spielsysteme

**Stand:** 2026-10-14. **Evidenz:** Spielkonzepte durch langjährige dokumentierte APIs und konkrete Multiplayer-Quellen plausibel; **exakte Methodensignaturen der 1.7-Runtime hier nicht lokal geprüft**.

## Schichten

- `Verse`: Fundament für Dinge/Defs, Karten, Mods, Komponenten, Serialisierung (`Scribe`), Simulation (`TickManager`) und Zufall (`Rand`). `Verse` ist ein *Namespace von Ludeons Spielcode*, **nicht** die Unity- oder Waylith-Engine.
- `RimWorld`: spielregel- und inhaltsnahe Klassen wie Jobs/Workers, Forschen, Vorfälle, Statistiken. Je nach Typ können sich beide Namensräume ergänzen.
- Engine: 1.6 Unity/Mono; 1.7 Waylith/.NET 10/Vulkan mit Unity-Kompatibilität. `Assembly-CSharp.dll` wird laut Ludeon weitgehend weiterverwendet. Primärquelle: [Modder Primer](https://docs.google.com/document/d/e/2PACX-1vSykw5_DYaaw42nEznI9zmBZVT_Vgg2bxYq_v2XWvAsHC3QSKFuDK2OyiQ5WC9jvVRUcrjpFt7xLLF5/pub).

## Datenfluss

`Defs/XML` → `DefDatabase` (statische Objektdefinitionen) → `Game`/`World`/`Map` (laufende Spielinstanzen) → `Thing`/`Pawn` und Tracker/Comps (dynamische Simulationsdaten) → `TickManager`/Jobs/Incidents → `Scribe` Save/Load. Im Multiplayer werden spielverändernde Eingaben als zeitlich geplante Commands in dieselbe Simulation eingespeist; clientlokales UI darf nicht ohne Sync Weltmutationen vornehmen.

## API-Landkarte (keine Gewähr für jede Versionssignatur)

| Symbol/Typ | Aufgabe und üblicher Hook | Multiplayer-Relevanz | Evidenzstatus |
|---|---|---|---|
| `Verse.Thing` | Weltobjekt, Position, Map, Spawn/Despawn; `ThingComp` für Verhalten | persistenter Zustand und Tick-Reihenfolge | bestätigt als Typ, Signaturen je Build prüfen |
| `Verse.ThingDef` / `Def` | statische Definition einer Sache, XML-Def-System | identische Mod-/Def-Listen auf allen Peers | bestätigt |
| `Verse.Pawn` | spezielles `Thing`, Eigenschaften, Bedürfnisse/Tracker | deterministische Jobs/Angriffe/Stats | bestätigt |
| `Pawn_JobTracker`, `Job`, `JobDriver` | Jobverwaltung, Arbeitsschritte/Toils | Jobauswahl, RNG, Reservierungen synchron halten | Typen aus Community-Modding; Signaturen offen |
| `WorkGiver` | Jobs für Pawn finden und vergeben | Suchreihenfolge stabil gestalten | Typen aus Community-Modding; Signaturen offen |
| `Verse.Map`, `Verse.World`, `Verse.Game` | Karte, Welt und Game-Lebenszyklus | Spielzustand, Map-/World-Zeitkomponenten | belegt in MP-Quellcode |
| `GameComponent`, `MapComponent`, `WorldComponent` | zustandsgebundene Manager mit Save-/Tick-Hooks | Zustand je Game/Map, Scribe | moddingdokumentiert; Lifecycle-Version prüfen |
| `ThingComp`/`CompProperties` | Komposition für Things | deterministische Comp-Änderungen | moddingdokumentiert |
| `Hediff`/`HediffComp` | Gesundheitszustände | Tick-/RNG-Determinismus | moddingdokumentiert |
| `IncidentWorker` | Ereignisausführung | Ereignis-RNG, Zielzuordnung | moddingdokumentiert |
| `StatDef`/`StatWorker` | Statistikdefinition/Berechnung | Cache/Float/Mod-Reihenfolge | moddingdokumentiert |
| `Scribe` | Serialize/Deserialize/ExposeData | Savegame-Replay, Reconnect | moddingdokumentiert |
| `TickManager` | Spiel-Ticks/Speed | *kritischer* MP-Patch `TickPatch` | Quellcode geprüft |
| `Verse.Rand` | Spiel-RNG | RNG-Strom und Aufrufreihenfolge | Quellcode/Dokumentation |
| `Harmony` | Runtime-Patching von Methoden | Mod-Reihenfolge und deterministischer Patch | offizielle Harmony-Doku |

**Nicht verwechseln:** Eine `ThingDef` ist eine meist unveränderliche Vorlage; ein `Thing` ist eine laufende Instanz. Ein `GameComponent` gehört zur aktuellen `Game`-Instanz, nicht zu einer einzelnen Map. `WorldComponent` besitzt Weltkontext, `MapComponent` den Kartenkontext.

## Tick-Ablauf und Threads

RimWorld-Logik läuft auf Spiel-Ticks, nicht per gerendertem Frame. Bei höheren Geschwindigkeiten laufen entsprechend mehr Simulationsticks. Ein Multiplayer-Server/Host vergibt konsistente Tick-Kommandos; die `TickManager`-Patch-Schicht der MP-Mod ruft die Commands am vorgesehenen Simulationsschritt auf. **1.7 Waylith.Jobs** bietet parallele Berechnungen, aber dies ist **kein Beleg**, dass mutationsführende Mod-Aktionen automatisch thread-sicher oder synchronisiert wären. Referenz: [TickPatch.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Patches/TickPatch.cs).

## Speichern und Laden

Persistente mutable Daten gehören in Game/World/Map/Thing-Komponenten und werden in üblicher Weise über `ExposeData()`/`Scribe_Values.Look` usw. gespeichert. Beim Laden sind Pawn-/Thing-Referenzen nicht während jeder Scribe-Phase verfügbar; Lade-Phasen und Cross-Ref-Behandlung in Zielassembly prüfen. Multiplayer nutzt zusätzlich Snapshots/Replay für Join/Reconnect; nicht nur normale Einzelspieler-Saves.

## Anwendungsentscheidung

Def/XML → reine Datenänderungen. `ThingComp`/`JobDriver` → isolierte Gameplay-Logik. `GameComponent`/`MapComponent` → zeitlicher/gebundener Zustand. `Harmony` → gezielte tiefe Integration nur bei fehlendem Hook. UI-Daten nicht gleichzeitig als replizierte Welt-Wahrheit behandeln.

### Quellen
- https://github.com/rwmt/Multiplayer/blob/master/Source/Client/MultiplayerGame.cs
- https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Patches/TickPatch.cs
- https://rimworldwiki.com/wiki/Modding_Tutorials/Custom_Comp_Classes
- https://rimworldwiki.com/wiki/Modding_Tutorials/GameComponent (alte Anleitung, für exakte 1.6/1.7-Signaturen ungeeignet)
