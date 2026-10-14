# Codex — Schnelle Mod-/Version-Kompatibilitätsmatrix

| Funktion | Vanilla 1.6 SP | 1.6 MP (rwmt) | 1.7 SP Waylith | 1.7 MP |
|---|---|---|---|---|
| XML ThingDef mit regulären Vanilla-Assets | grundsätzlich möglich, Test nötig | tendenziell möglich, Def-Sync-Test nötig | laut Ludeon in den meisten Fällen kompatibel, Test nötig | **UNVERIFIZIERT** |
| XML Patch/Recipe/Research | grundsätzlich möglich | gleiche Defliste/Settings nötig | Kompatibilitätsziel, Test nötig | **UNVERIFIZIERT** |
| C# `ThingComp` im Tick | möglich mit .NET472/Spielrefs | nur deterministisch + synchronisiert | meist binär kompatibel, neuer Target .NET10 optional | **UNVERIFIZIERT** |
| lokaler UI-Button verändert Welt | funktioniert oft | **Desync-Risiko** ohne Command-Sync | SP-UI-Test nötig | **UNVERIFIZIERT** |
| SyncMethod/SyncField/SyncWorker | nicht erforderlich | API 0.6.0 als 1.6-Referenz; testen | nicht erforderlich für SP | **UNVERIFIZIERT** |
| Harmony-Prefix/Transpiler | Target-abhängig | besondere MP-Patch-Konflikte | Harmony-3-/Runtime-/IL-Prüfung | **UNVERIFIZIERT** |
| Prepatcher-Pflichtmod | 1.6 grundsätzlich nutzbar | MP benötigt Prepatcher | laut Primer **inkompatibel** | **BLOCKIERT** |
| Unity-Asset-Loader-Interception | auf 1.6 möglich | sehr hohes Risiko | laut Primer signifikant geändert | **BLOCKIERT/UNVERIFIZIERT** |

**Legende:** Kein Feld stellt ohne dokumentierten realen Clienttest eine Feature-Freigabe dar. `1.7 MP` bleibt gesperrt, bis Abhängigkeiten und echte MP-Client-Tests existieren.

Siehe `../research/10-multiplayer-compatibility.md` für Modtyp-spezifische Risiken.
