# 10 — Multiplayer-Kompatibilität, Modtypen und Integrationsprojekte

**Kompatibilität ist ein getesteter Zustand je Versionskombination**, kein automatisch aus Def/XML oder einem „supports multiplayer“-Label ableitbares Qualitätsmerkmal. `rwmt/Multiplayer` unterstützt in seiner About.xml 1.6, aktuell nicht verifiziert 1.7. Siehe `references/version-matrix.md`.

## Typisierte Risikomatrix

| Modtyp | Hauptproblem | Syncbedarf | deterministischer Plan | verpflichtender Test |
|---|---|---|---|---|
| neue Items/Ressourcen (`ThingDef`) | unterschiedliche Defs/IDs | keine interaktive Spezial-Sync bei reinem Def | gleiche Deflisten/Reihenfolge | Laden, Craft, Spawn, Save/Join |
| Waffen/Projektile | Schadens-RNG/Burst/Verb-Patches | Specials als Commands | `Verse.Rand`-Kontext, keine UI-Shots | 1000 Schüsse, Hit/Ammo vergleichen |
| Bauwerke/Produktion | Toggles, Inventar, lokale Timer | SetEnabled/SetRecipe SyncMethod | `ThingComp` + persistierte Ticks | Host/Client-Klick, Pause/Save |
| Rezepte/Forschung | unterschiedliche Unlocks und Tick-Aufwand | abhängig von Interaktion | Defs konsistent, validierte Unlock-Changes | abschließen, Unlock, Load |
| Jobs/WorkGivers/Pawn AI | Suche, tie-breaks, reservations | UI-Prioritäten syncen | stabile Sort/Job Driver | viele Pawns, Overlap, Ticklast |
| Fraktionen/PawnKinds | Spawn-/World-RNG, Relation Changes | dynamische Diplomatieaktionen | gezielte Command-Verarbeitung | Worldgen, Join, Incidents |
| Hediffs/Gesundheit | random Stat/Health changes | gezielte UI-Eingriffe | serialisierte Hediff/Ticks | Heilen, Sterben, Load |
| Karten-/Weltmanager | multi-map contexts, statics | Commands für Einstellung | korrekte Map/World-Komponente | 2 Karten, getrennte Speeds |
| UI/Dialoge/Gizmos | onClick-only lokale Änderung | SyncMethod/Field+Watch | UI nur Anzeige, stabile referenzierte Objekte | Client-only geöffnetes Fenster |
| ModSettings | pro Client unterschiedlich | gameplay settings vor Join abgleichen | Sessionkonfiguration/Scribe | Konfig-Differenzen absichtlich erzeugen |
| Harmony tief | Core-MP-Patchkollision | abhängig von Target | MP-Patch-Reihenfolge respektieren | andere Mods im Stack |
| Performance-Mods | Reihenfolge/Cache/Async ändern | nicht automatisch syncbar | nur messbar deterministic optimieren | Langzeitvergleich, Cross-OS |
| Client-seitige Grafik/Tooltips | keine Weltmutation erlaubt | meist kein Sync | RNG strikt vom Gameplay trennen | UI unterschiedlich geöffnet |

## Bekannte Kompatibilitätsprojekte

**Projekt:** https://github.com/rwmt/Multiplayer-Compatibility. Enthält viele mod-spezifische Registrierungen/Patches. Das **Vorhandensein eines Patches** sagt nichts über den heutigen Status einer neueren fremden Modversion. Repository gibt selbst an, dass man passend gelöste Compat-Verfahren in eigene Mods übernehmen darf, **Lizenz und tatsächliche Änderungen prüfen**.

Beispiel im aktuellen Repo: [Source/Mods/Desynchronized.cs](https://github.com/rwmt/Multiplayer-Compatibility/blob/master/Source/Mods/Desynchronized.cs). Die Klasse nutzt das projektseitige Attribut `[MpCompatFor("Mlie.Desynchronized")]` und eine mod-spezifische RNG-Fixregistrierung (`PatchingUtilities.PatchPushPopRand(...)`). **Dieses Attribut gehört zum Kompatibilitätsprojekt, ist keine garantierte Vanilla- oder MP-API-Schnittstelle.** Die Funktion ist eine Referenz für Struktur/Erkennung, kein Copy/Paste-Rezept für andere Mods.

## Sichere Compat-Patch-Strategie

1. Exakte `packageId`, Zielversion und gefundene Assembly des fremden Mods dokumentieren.
2. Reproduktionsfall in einer Minimalmodliste nachweisen.
3. Für jede betroffene fremde UI-Weltänderung registrierte `SyncMethod` / `SyncField` ermitteln.
4. Wenn neue Objekte als Parameter gesendet werden: `SyncWorker` mit Identitätsmapping implementieren.
5. Zufallspfade und statischen State überprüfen; nur gezielt patchen.
6. Patches nur aktivieren, wenn Zielmod und API in bestätigter Version installiert sind; Versions-/Reflection-Guards für abwesende Methoden.
7. Host/Client-Funktion, Sync, Save/Load, Join und andere populäre Frameworks erneut testen.
8. Eigene Mod-Lizenz mit der des Compat-Codes abgleichen; bei fremden Harmony-Transpilern keine blinde Kopie.

## Singleplayer vs. Multiplayer

Ein Mod, der Singleplayer-Save/Load besteht, kann trotzdem sofort desyncen. Umgekehrt kann ein einwandfreier `.xml`-Mod von anderen aktiven Mods in Loadorder instabil gemacht werden. Aussage `MP-kompatibel` erfordert belegte **konkrete** RimWorld-, Multiplayer-, API- und Abhängigkeitsversionen mit Testlogs.
