# 05 — C#-Mods: Projektlayout, Komponenten, Persistenz, UI

## Build-Modell

- 1.6: **`net472`** und Referenzen aus der tatsächlich installierten Unity-Spielversion bzw. geprüften NuGet-Ref-Packs.
- 1.7: laut Ludeon **`net10.0`**, Referenzen `Assembly-CSharp.dll`, `Waylith.dll`, `UnityStubs.dll`, ggf. `RimWorld.dll`. Keine ungeprüften 1.6-DLLs in 1.7-Projekt ziehen.
- Assemblies nicht einchecken; Projekte sollten `RimWorldManaged`/`RimWorldDir` als lokal übergebene Property nehmen.

## Architektur / Komponenten

`Verse.Mod` kann Einstieg und `ModSettings` verwalten. `GameComponent`, `MapComponent`, `WorldComponent` speichern zustandsgebundene Manager; `ThingComp` ist gekapselte Funktionalität eines `Thing`. `CompProperties` deklariert Konfiguration; Spiel-Klassen via `JobDriver`, `WorkGiver`, `StatWorker`, `IncidentWorker` nur in dem Ausmaß erweitern, das die Version tatsächlich erlaubt.

Entkopple **Daten** von **UI**: UI liest Modellzustand; jede Weltänderung ruft eine zugelassene Command-Methode auf (Multiplayer-API) oder wird im Singleplayer-Controller ausgeführt. Ein gemeinsamer Handler darf nicht direkt `Event.current`/Systemzeit lesen.

## Beispiel Design: Produktionskomponente

- `CompProperties_Production`: nur statische Konfiguration (Def-Parsing).
- `CompProduction`: persistente Instanzdaten, z. B. `progress`, `enabled`, `recipeDef`; seriell als Scribe-Daten.
- `ProductionTicker`: feste Tick-Schritte, keine Clock-Walltime, nur definierte RNG-Ströme.
- `ProductionCommands`: sync-bare öffentliche Methoden `SetEnabled`, `SetRecipe`.
- `ITab`/Gizmos: Anzeige/Buttons, die `ProductionCommands` aufrufen; bei MP Dialog-Sync mittels API korrekt registrieren.
- `ProductionCache`: abgeleiteter Cache, nach Load invalidieren, deterministisch auffüllen, nicht heimlich als einzige Quelle der Wahrheit nutzen.

## Lifecycles und Speichern

`ExposeData()`/`Scribe_Values.Look(ref field, "key", defaultValue)` für primitives; Referenzen auf Things/Pawns/Maps passend referenziell scripten, Ladephasen gegen 1.6/1.7 prüfen. Keine statischen Instanzregistries ohne Reset beim Wechsel von Karte/Spiel, Replay/MP-Reconnect. Nie eine sicherheitskritische Änderung bei `OnGUI` ohne Command-Barrier durchführen.

## Wartbarkeit

Ein Mod-Feature pro Namespace/Ordner, reduzierte öffentliche APIs, `internal` wo möglich, Null-/DLC-/Missing-Def-Guards, zentrierte Reflection, kein globaler catch, genau ein Besitzer für jedes persistente Feld. Testbare reine Funktionen von Unity/Verse abhängigkeitsarm halten. Logs mit Feature, DefName, PawnID/MapID/Tick anreichern (keine übermäßigen Tick-Logs).

## Versionsprüfung auf Linux

```bash
dotnet --info
# Liste des Game-Verzeichnisses ohne proprietäre DLLs in Repo zu kopieren:
find "$HOME/.steam/steam/steamapps/common/RimWorld" -maxdepth 5 \
  \( -name 'Assembly-CSharp.dll' -o -name 'Waylith.dll' -o -name 'UnityStubs.dll' \) -print
```

Pfad ist beispielhaft und hängt von Steam-Library/Installation ab. Der String `RimWorldManaged` aus der csproj-Vorlage im Sample muss entsprechend gesetzt werden.

Quellen: https://docs.google.com/document/d/e/2PACX-1vSykw5_DYaaw42nEznI9zmBZVT_Vgg2bxYq_v2XWvAsHC3QSKFuDK2OyiQ5WC9jvVRUcrjpFt7xLLF5/pub ; https://rimworldwiki.com/wiki/Modding_Tutorials/GameComponent (**historisch**)
