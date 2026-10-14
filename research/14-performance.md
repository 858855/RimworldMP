# 14 — Performance-Leitfaden mit deterministischen Grenzen

## Messansatz

Nur optimieren, wenn die bottleneck-Messung mit gleichem Seed/Save/Modsatz reproduzierbar ist. RimWorld ist oft ticklastig, insbesondere Pawn-AI, Pfadsuche, Bills/Jobs, große Maps und häufige Listenabfragen. Für 1.7 dokumentiert Ludeon **2–4-fache** Performanceverbesserungen aus eigenen Tests sowie einen **Frame Profiler**; diese Werte sind Hersteller-Benchmarks, keine universelle Hardwaregarantie.

## Konkrete Maßnahmen

1. **Tick-Frequenzen:** Teure reine Prüfungen seltener ausführen, aber Frequenz nur aus stabilen Spiel-Ticks/IDs ableiten; z. B. modulo auf Tickindex, nicht `Stopwatch`.
2. **Caching:** für häufige immutable Def-Lookups; bei World-/Map-Zustand explizite Invalidierung und Save/Load/Join-Reset. Cache darf keinen anderen Entscheidungszustand als uncached Variante erzeugen.
3. **Allokationen:** in hot paths keine unnötigen temporären LINQ-Listen, Regex, String-Formatierungen oder Reflection; GC/Perf mit Logs/Profiler messen.
4. **Map-Abfragen:** Region- und Thing-Iterationen begrenzen, gefilterte Caches pro Map (nie globale Tabelle ohne ID), Positionsergebnisse stabil sortieren, wenn die Reihenfolge Einfluss hat.
5. **Pathfinding/Jobs:** reinspielseitige Hooks nutzen, keine unendlichen Retry-/Reserve-Loops; jede WorkGiver-Logik deterministisch tie-breaken.
6. **Harmony:** keine schweren Reflective Lookups in Prefix pro Pawn-Tick; MethodInfo/FieldInfo beim Startup cachen, Patchliste nur bei Debug loggen.
7. **UI/Rendering:** UI-Cache darf clientlokal sein, solange er keine Weltdaten mutiert. Material/Textures auf Linux (1.7 Vulkan) separat profilieren.
8. **Multithreading:** 1.7 Waylith Jobs können CPU nutzen; Parallelität nur bei read-only Daten bzw. deterministisch sortiertem Commit. `WaylithJobRuntime.Schedule*`/`Complete` existiert laut Primer; **genaue API-Signatur im Game verifizieren**.

## Negative Beispiele

- Spawnjob benutzt Dictionary-FirstItem, um Zielpawn zu bestimmen: Cache spart Zeit, aber Ziel unterschiedlich.
- AutoCrafter führt `DateTime.Now`-gesteuerte Arbeit aus: Host und Client erzeugen unterschiedliche Materialien.
- Parallele Sortierung schreibt `ThingComp.progress` aus Worker-Thread: Race und nicht deterministische Reihenfolge.
- UI-Prefetch ruft globales `Verse.Rand` und verschiebt RNG für nächstes Incident.

## Messen und Abnehmen

Baseline aus Save mit 1000/10000/60000 Ticks; CPU, Allocations, TPS, Sim-Zeit und ggf. Frame-Zeit; 3 Läufe Median. Optimierung muss funktional identische Serienergebnisse liefern. Im Multiplayer zusätzlich beide Peers/Desyncs, save/load und Reconnect validieren. Bei fehlendem Game nur statisches Code-Review, **keine behauptete TPS-Steigerung**.

Quellen: https://ludeon.com/blog/2026/10/announcing-the-waylith-engine-and-update-1-7/ ; https://github.com/rwmt/Multiplayer/blob/master/Source/Client/AsyncTime/AsyncTimeComp.cs
