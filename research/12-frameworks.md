# 12 — Frameworks und Quellprojekte für RimWorld-Modding

**Stand 2026-10-14.** **Nicht** alle gelisteten Projekte wurden auf letzter Commit-/Release-Ebene auditiert. Kompatibilität je Modliste aktiv testen. Ein Framework ist keine pauschale Multiplayer-Freigabe.

| Projekt | Hauptzweck | Repo | MP-Nutzen/Risiko | Status/Belege |
|---|---|---|---|---|
| Harmony | Patchen existierender .NET-Methoden | https://github.com/pardeike/Harmony | Patchorder, Prefix, Transpiler deterministisch prüfen | Dokumentation hoch; 1.6 2.x / 1.7 3-Migration prüfen |
| Multiplayer (rwmt) | Lockstep-Netzwerk, Save/Rejoin | https://github.com/rwmt/Multiplayer | Ausgangspunkt für echten MP | About.xml nur 1.6, Prepatcher-Abhängigkeit; MIT |
| MultiplayerAPI | abstrahierte SyncMethod/Field/Worker | https://github.com/rwmt/MultiplayerAPI | Ziel-Schnittstelle eigener MP-kompatibler Mods | NuGet 0.6.0 `net472`, 1.7 nicht verifiziert; MIT |
| Multiplayer-Compatibility | Patchsammlung für Fremdmods | https://github.com/rwmt/Multiplayer-Compatibility | Referenz für modabhängige Sync- und RNG-Patches | mod-/versionsabhängig; MIT |
| Prepatcher | Frühe IL-/Code-Vorverarbeitung | https://github.com/Zetrith/Prepatcher | direkte Abhängigkeit des MP-Mods | 1.7 laut Ludeon inkompatibel |
| HugsLib | Modding Utilities, Settings/Helper | https://github.com/UnlimitedHugs/RimworldHugsLib | ggf. Startup-/Patchreihenfolge | installierte Version/Lizenz nachsehen |
| Vanilla Expanded Framework | gemeinsames Framework Vanilla Expanded | https://github.com/Vanilla-Expanded/VanillaExpandedFramework | häufig in großen Modlisten; zusätzl. MP-Patches | aktuelle 1.6/1.7-Supportangaben beim jeweiligen Release prüfen |
| Performance Fish (Fork/1.6) | Performance-Patches und Caches | https://github.com/DannyProtons/Performance-Fish-1.6 | Cache/RNG/Thread-Ausführung kann Sync tangieren | MPL-2.0 laut Repo, MP-Aussagen selbst berichtet, keine Freigabe |
| RimWorld Wiki Modding | Tutorials XML/Def/Comp | https://rimworldwiki.com/wiki/Modding_Tutorials | API-Konzepte und alte Beispiele | einige Seiten ausdrücklich veraltet |

## Wann ein Framework wählen?

- **Kein Framework**, wenn reine `Defs`/Vanilla-XML oder Basiskomponenten reichen.
- **Harmony**, wenn Verhalten nicht anders verfügbar ist. Zielmethode, Prefix-/Postfix-Semantik und Performance klar protokollieren.
- **MP API** nur bei tatsächlich vorhandener, kompatibler Multiplayerinstallation und bestätigtem Ziel `net472`/1.6; Attributmethoden reflektieren.
- **HugsLib**, wenn konkrete Features den Zusatznutzen rechtfertigen (nicht nur als generische Pflichtdependency).
- **VEF**, wenn der Mod dessen Features tatsächlich verwendet; `loadAfter` und DLCs prüfen.
- **Prepatcher**, solange 1.6-Ziel und konkreter Nutzen; 1.7 ist belegt problematisch.

## Code-Lesestrategie

Aktiv verwendetes Modul suchen → `About.xml`/Projektziel/Release → Commit pinnen → Feature-Implementation und Patches lesen → API/Abhängigkeiten extrahieren → Lizenz vor Nachbau prüfen → eigene Minimal-Repro erstellen. **Kein kopierter fremder Code ist automatisch versionsrichtig.**
