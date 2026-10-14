# 09 — Determinismus: verbindliche technische Regeln für Lockstep

**Quellenanker:** [TickPatch.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Patches/TickPatch.cs), [AsyncTimeComp.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Client/AsyncTime/AsyncTimeComp.cs), [SyncSerialization.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Common/Syncing/SyncSerialization.cs). Die konkreten Fehlermuster sind eine **technische Risikoanalyse** auf Grundlage dieses Lockstep-Prinzips; nicht jeder Einzelpunkt wurde empirisch als Fehlverhalten jedes Builds nachgewiesen.

## Zentrale Invariante

`State(N+1) = ApplyTick(State(N), SameSortedCommands(N), SameRngStream(N))`. Das ist eine modellhafte Gleichung **kein API-Aufruf**. Ein Peer darf den replizierten World-/Map-Zustand nur in derselben Order und mit denselben Inputs wie andere Peers verändern.

| Risiko | Warum problematisch? | Verbindliche Gegenmaßnahme | Test |
|---|---|---|---|
| `new System.Random()` | Seed/Callorder divergiert | Spiel-RNG-Kontext (`Verse.Rand`) nur nach lokaler MP-API-Analyse | zwei Peers mit fixem Seed, Tracefolge |
| `UnityEngine.Random`/Waylith-Viz-RNG | nicht kontrollierter Simulationsstrom | visual only; kein Gameplay-RNG | randomisierte Spawn-/Damage-Repro |
| `DateTime.Now`, `Stopwatch.Elapsed` | Uhrzeit/Latenz sind peerabhängig | Tickindex/serialisierte Timervariable statt Wallclock | Speedwechsel/pausiertes Spiel |
| Frame-/UI-Callbacks | Renderzyklen unterscheiden sich | Weltmutationen nur Tick/Sync-Command | UI offen bei nur einem Peer |
| `Dictionary`, `HashSet` Reihenfolge | nicht als kanonische Ordnung garantiert | vor wertabhängiger Selektion nach stabiler ID sortieren | 100 Wiederholungen über OS |
| LINQ mit impliziter Reihenfolge | Quelleniteration evtl. ungeordnet | `OrderBy` mit stabiler eindeutiger Tie-Breaker-ID | Host-Linux/Client-Windows |
| Floating Point | Architektur/Runtime/JIT kann Unterschiede erzeugen | feste Algorithmen/Toleranzen, keine Float-Schwellen ohne Test | Cross-OS/langlaufende Saves |
| Threads/Tasks/Async | Scheduling nicht deterministisch | parallel ausschließlich read-only/reproduzierbar, Commit sortiert auf Tick | `-Headless` / UI und Cross-OS |
| `static` mutable state | nicht durch Scribe sauber pro Session reset | Zustand auf `GameComponent`/`MapComponent`; Reset/Load testen | mehrere Games in einem Prozess |
| Cache | stale Cache/Invalidierungsorder | Cache aus persisted Inputs ableiten und invalidieren | Save/Load/Replay/Rejoin |
| Externe HTTP/Dateiinputs | Peer-Daten unterschiedlich | Inputs zentral validieren und als Command/Ingame-Content bereitstellen | Client offline / anderer OS |
| Locale/TimeZone | Parse und Sort order unterschiedlich | `InvariantCulture`, canonical IDs | deutsche/englische OS |
| Hash / object identity | Referenzen/Speicheradressen divergieren | stable Pawn/Thing/Map IDs, kein process hash | separate Prozesse |
| ModSettings | clientlokale Differenz | gameplay-relevante Settings pro Session angleichen | Setting einseitig verändern |
| Harmony-Konflikte | Prefix überspringt/doppelt Core-Logik | Patchlisten auf allen Peers vergleichen | A/B Modliste und Loadorder |

## RNG richtig denken

Nur „alle nutzen `Verse.Rand`“ genügt nicht. Schon ein zusätzliches Rand-Call auf einem Peer verschiebt alle folgenden Werte in demselben Strom. **Keine Zufallszahlen für Tooltipfarben, Iconanimationen oder UI-Elementsortierung aus dem Gameplay-Strom entnehmen**. Wann `Rand.PushState/PopState` existiert und wie MP eigene Command-/Map-RNG-Stapel verwendet, gegen aktuelle installierte Assemblies/MP-Code verifizieren, bevor Beispielcode mit ihnen als garantiert ausgegeben wird.

## Deterministische Entscheidungsmethode (C#-Pseudoalgorithmus)

```text
eligible = things.Where(valid);
ordered = eligible.OrderBy(thing.thingIDNumber);  // property/signature im Zielbuild prüfen
for each candidate in ordered:
    score = pure_function(serialized_world_state, candidate);
select best, break ties by stable candidate ID;
execute mutation ONLY inside synchronized command or world tick;
```

Die konkreten Eigenschafts-/Methodennamen dieser **Pseudosequenz** sind nicht als API-Verifikation zu lesen.

## Warum UI von Simulation getrennt bleiben muss

Das Quellcode-Kommentar in `AsyncTimeComp.UpdateManagers` beschreibt Map-`Update()`-Methoden als nichtdeterministisch und verschiebt statusverändernde Arbeiten in `Tick()`/`ExecuteCmd()`. Test: Eine Produktionsmaschine im Host klicken, während Client in anderem Menü/Karte ist. Wenn allein bei offener UI des ersten Peers eine Anzahl verändert wird, besteht Desync-Gefahr.

## Float und Plattformkonstanz

Kein pauschales Verbot von Float: RimWorld nutzt Float intensiv. Risiko entsteht speziell bei divergierender Runtime/Architektur, non-associativen Summen, Threadreihenfolge, Schwellenwerten und fortlaufender Integration. Für neu erfundene Simulationen ggf. Integer-Tick-Punkte/Festkomma für kritische Entscheidungen verwenden und präzise dokumentieren. **1.6 ↔ 1.7 gemeinsam** in einer MP-Session ist nicht unterstützt oder verifiziert; Cross-OS 1.6-Tests sind ein eigener Testfall.

## Determinismus-Teststrategie

1. Def-/Settings-/RNG-Seeds und Mods vor Session vergleichen.
2. Save und Commandfolge aufnehmen (Klicks, Incidents, Crafting, Pawn movement).
3. Nach jedem synchronen Command hash-/objektbasiert **nur kanonische** World-Felder vergleichen; keine Render-/Cachefelder.
4. `Dictionary`-Iteration mit zufälligen Einfügereihenfolgen in isolierten Tests provozieren.
5. Reihenfolge von Thread-Ergebnissen unter Last kontrollieren; Frame-Rate bei nur einem Peer absenken.
6. Tick 0/100/1000/10000/50000 Save-/Load-Check, Join und Reconnect.
7. Desyncs mit Vorher-/Nachher-State, Commands, Kontext, RNG-Trace und reproduzierbarem minimalem Save dokumentieren.
