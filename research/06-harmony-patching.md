# 06 — Harmony in RimWorld, Patches und Konflikte

**Primär:** https://harmony.pardeike.net/articles/intro.html und https://harmony.pardeike.net/articles/patching.html ; https://harmony.pardeike.net/articles/patching-prefix.html ; https://harmony.pardeike.net/articles/patching-postfix.html ; https://harmony.pardeike.net/articles/patching-transpiler.html

## Begriffe und semantische Wirkung

| Typ | Wirkung | Risiko |
|---|---|---|
| Prefix | vor Original; kann bei bool `false` Original überspringen | hohe Konfliktgefahr bei mehreren Prefixes/MP-Patches |
| Postfix | nach Original, kann Rückgabe ändern | leicht unbemerkte Gameplay-Mutation |
| Transpiler | schreibt IL beim Patchen um | sehr fragil gegenüber C#-Compiler-/Runtime-/Game-Updates |
| Finalizer | ermöglicht Exception-Behandlung/-Ersetzung | kann echte Fehler verbergen, unbedingt loggen |
| `HarmonyPriority` | ordnet Patches grob | Versions-/Mod-Konflikte, feste Reihenfolge nicht immer garantiert |
| `HarmonyBefore`/`HarmonyAfter` | explizite relative Ordnung nach Patch-ID | nur wirksam, wenn Patch-Person und ID stimmen |

`Prefix` darf `return false` nur gezielt verwenden. `HarmonyPatch`/`Harmony.PatchAll` zur Initialisierung braucht eindeutige Patch-ID (eigener `packageId`), geeigneten Mod-Startup-Zeitpunkt und korrekte Target-Methode; nicht pauschal jede Tick-Funktion patchen. Harmony-Ausführungsregeln sind versioniert; 1.6 häufig Harmony 2.x, für 1.7 Harmony 3 laut Migrationsleitfaden und genaues Produkt prüfen.

## Konflikt- und Performance-Regeln

- Harmony-Patch-Liste der Zielmethode vor Änderungen auslesen (jeweilige API nach Versionsprüfung); Entwickler-Namen/IDs/BeforeAfter notieren.
- Patches an `TickManager`, `Verse.Rand`, `Scribe` oder `_JobTracker` sind **multiplayerkritisch**; aktuelle `rwmt`-Patches analysieren, um keine Command-/RNG-Tick-Reihenfolge zu verschieben.
- Ein normaler Postfix auf teure Pfade verursacht eventuell tausende Callbacks pro Tick. Caching an deterministische Invalidierung koppeln und Kosten am laufenden Spiel messen.
- Bei 1.7 Waylith trotz großer Binärkompatibilität IL-Targets, Reflection-Typen und `__instance`/`__result`-Injektion **erneut prüfen**.

## Veto für Harmony

Wenn eine XML-Def oder `ThingComp` dasselbe Verhalten erreicht, muss Codex das vorziehen. Bei schon von MP veränderten Methoden zuerst die Multiplayer-spezifischen Patches und Sync-Mechanismen lesen. Harmony als Konfliktlösung nur dokumentiert, versionsabhängig, regressionsgetestet.

## Beobachtungsbeispiel

`examples/harmony/` enthält einen **nur protokollierenden** Harmony-Patch; der Code ist **nicht kompiliert oder ausgeführt**. Keine gameplay-mutierenden Prefixes für unerprobte Targets aus dem Beispiel übernehmen.

Projektquellen: https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Patches/TickPatch.cs ; https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.7_Mod_Updates
