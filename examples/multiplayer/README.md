# Multiplayer 1.6: API-Illustrationen (UNGETESTET)

`SyncMethodExample.cs`: `[StaticConstructorOnStartup]`, `MP.enabled`, `MP.RegisterAll`, `[SyncMethod]`, `Thing`-Argument. **Die Command-Methode enthält absichtlich keinen produktiven Gameplay-Eingriff** und beweist daher keine tatsächliche Synchronisation; erst nach Implementierung und Zwei-Client-Test verwenden.

`SyncFieldExample.cs`: `[SyncField]`/`MP.WatchBegin`/`MP.Watch`/`MP.WatchEnd` als historisch belegtes Scope-Muster, Feldinstanz muss in Spiel-/Dialogkontext zuverlässig referenziert werden. `ResearchUiState` darf nicht unüberprüft nur lokal erzeugt sein. UI-Slider ist ausdrücklich Platzhalter.

`SyncWorkerExample.cs`: `[SyncWorker]` mit `sync.Write(Thing)` und `sync.Read<Thing>()`. Zugriff/Null/Destroyed/MapContext müssen bei realer Modentwicklung geprüft werden.

**Keine 1.7-Unterstützung**; `rwmt/MultiplayerAPI/wiki` (teils 2019) kann von NuGet 0.6.0 abweichen. Exakte Signaturen über `ilspycmd`/Reflection gegen Paket-DLL prüfen. Quellen: https://github.com/rwmt/MultiplayerAPI/wiki ; https://www.nuget.org/packages/RimWorld.MultiplayerAPI/ .
