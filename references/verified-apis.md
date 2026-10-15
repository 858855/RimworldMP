# API-Evidenzregister (2026-10-14)

**Evidenzregel:** Für ein konkretes Symbol „BESTÄTIGT“ bedeutet: in einer zugänglichen Quelle im angegebenen Kontext gesehen. **Nicht**: mit dem lokalen 1.6/1.7-Build kompiliert oder einem laufenden MP-Client getestet.

| Symbol | Namespace / Kontext | Geltungsbereich | Status | Nachweis |
|---|---|---|---|---|
| `TickManager.TickManagerUpdate` | `Verse.TickManager` | MP 1.6 | BESTÄTIGT im MP-Patch | https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Patches/TickPatch.cs |
| `TickPatch.RunCmds` | interner MP-Client | MP source master 2026 | BESTÄTIGT | ebd. `ScheduledCommand.ticks`/`ExecuteCmd` |
| `AsyncTimeComp.ExecuteCmd(ScheduledCommand)` | interner MP-Client | MP source master 2026 | BESTÄTIGT | https://github.com/rwmt/Multiplayer/blob/master/Source/Client/AsyncTime/AsyncTimeComp.cs |
| `AsyncTimeComp.UpdateManagers` | interner MP-Client | MP source master 2026 | BESTÄTIGT | ebd., Kommentar Update non-deterministic |
| `UnityStubs.dll`, `Waylith.dll` | Engine 1.7 | 1.7 | BESTÄTIGT | Ludeon Primer |
| `net10.0` | SDK Ziel | 1.7 | BESTÄTIGT | Ludeon Primer |
| `-Headless`, `-NoWindow`, `-mod=`, `-BakeModAssetPacks=` | RimWorld-CLI 1.7 | 1.7 | BESTÄTIGT | Ludeon Primer |
| `WaylithJobRuntime` | Engine Jobs | 1.7 | BESTÄTIGT als API-Familie | Ludeon Primer, **Overloads nicht geprüft** |
| `Multiplayer.API.MP.enabled` | Public MP API | Wiki, 1.6-API | BESTÄTIGT HISTORISCH | https://github.com/rwmt/MultiplayerAPI/wiki |
| `MP.IsInMultiplayer`, `MP.IsHosting` | Public MP API | Wiki | BESTÄTIGT HISTORISCH | ebd. |
| `MP.RegisterAll`, `[SyncMethod]` | Public MP API | Wiki | BESTÄTIGT HISTORISCH | ebd.; Signaturen aktuell prüfen |
| `MP.RegisterSyncMethod`, `MP.RegisterSyncField` | Public MP API | Wiki | BESTÄTIGT HISTORISCH | ebd., diverse Overloads |
| `[SyncField]`, `MP.WatchBegin/Watch/WatchEnd` | Public MP API | Wiki | BESTÄTIGT HISTORISCH | ebd. |
| `[SyncWorker]`, `SyncWorker.Write/Read` | Public MP API | Wiki | BESTÄTIGT HISTORISCH | ebd. |
| `MP.IsExecutingSyncCommand` | API-Kandidat | unbekannt | **UNGEKLÄRT** | kein hinreichender 0.6.0-Signaturbeleg |
| `Scribe_Values.Look`, `GameComponent`, `MapComponent`, `ThingComp` | Verse | typische 1.6/ältere Dokumentation | BESTÄTIGT als Konzept, **lokale Version-Signatur OFFEN** | RimWorld Wiki Modding Tutorials |
| `HarmonyPrefix/Postfix/Transpiler/Finalizer` | Harmony | versionsabhängig | BESTÄTIGT für Harmony-Grundprinzip | https://harmony.pardeike.net/articles/intro.html |

Wichtiger Unterschied: `Multiplayer.Client.Multiplayer` ist eine interne Clientklasse und **nicht** `Multiplayer.API.MP` des integrierbaren öffentlichen Pakets.

## VER-001: Metadatenprüfung gegen lokales RimWorld 1.6 (2026-10-15)

**Quelle:** `E:/SteamLibrary/steamapps/common/RimWorld/RimWorldWin64_Data/Managed/Assembly-CSharp.dll`, `Version.txt` = `1.6.4871 rev590` (Windows). Geprüft mit `System.Reflection.Metadata` (nur Metadaten gelesen, kein Spielcode ausgeführt, kein Quelltext übernommen). **Status `BESTÄTIGT (Metadaten)`** heißt: Name, Sichtbarkeit und Signatur existieren in genau diesem Build. Laufzeitverhalten ist damit **nicht** belegt.

| Symbol (vollständig) | Signatur in 1.6.4871 | Plan | Status |
|---|---|---|---|
| `Verse.LoadedModManager.RunningModsListForReading` | `static List<ModContentPack>` (Property) | §5.1 | BESTÄTIGT (Metadaten) |
| `Verse.ModContentPack.PackageId`, `.PackageIdPlayerFacing`, `.Name`, `.RootDir`, `.ModMetaData`, `.IsCoreMod`, `.IsOfficialMod` | Properties (`string`/`ModMetaData`/`bool`) | §5.1 | BESTÄTIGT (Metadaten) |
| `Verse.ModContentPack.foldersToLoadDescendingOrder` | `public List<string>` (Feld) | §5.1 | BESTÄTIGT (Metadaten) |
| `Verse.ModMetaData.ModVersion`, `.PackageId`, `.Name`, `.Official`, `GetPublishedFileId()` | `string`/`bool`, `PublishedFileId_t` | §5.1 | BESTÄTIGT (Metadaten) |
| `Verse.ModsConfig.ActiveModsInLoadOrder` | `static IEnumerable<ModMetaData>` | §5.1 | BESTÄTIGT (Metadaten) |
| `RimWorld.VersionControl.CurrentVersionStringWithRev`, `.CurrentVersionString`, `.CurrentBuild` | `static string`/`int` (**Namespace `RimWorld`**, nicht `Verse`) | §5.1 | BESTÄTIGT (Metadaten) |
| `Verse.Current.ProgramState`, `Verse.ProgramState` | `static ProgramState` (Property, Enum) | §5.2 | BESTÄTIGT (Metadaten) |
| `Verse.Dialog_MessageBox.CreateConfirmation` | `(TaggedString, Action, bool destructive, string title, WindowLayer)` und Variante mit `cancelAct` | §5.2 | BESTÄTIGT (Metadaten) |
| `Verse.WindowStack.Add(Window)`, `IsOpen<T>()`, `IsOpen(Type)` | public | §5.2 | BESTÄTIGT (Metadaten) |
| `RimWorld.Page_SelectScenario()` | public ctor | §5.2 | BESTÄTIGT (Metadaten) |
| `RimWorld.MainMenuDrawer.DoMainMenuControls(Rect, bool anyMapFiles)` | public static | §5.2 | BESTÄTIGT (Metadaten) |
| `RimWorld.Page` (Basisklasse, **Namespace `RimWorld`**): `DoNext()`, `CanDoNext()`, `DoBack()` | protected/nonpublic virtual | §6.3 | BESTÄTIGT (Metadaten) |
| `RimWorld.Page.next`, `.prev`, `.nextAct` | public Felder `Page`/`Action` | §6.3 | BESTÄTIGT (Metadaten) |
| `RimWorld.PageUtility.StitchedPages(IEnumerable<Page>)` | public static | §6.3 | BESTÄTIGT (Metadaten) |
| `RimWorld.Page_SelectStoryteller`, `Page_SelectStorytellerInGame`, `Page_ChooseIdeoPreset` (`DoNext` nonpublic), `Page_ConfigureIdeo`, `Page_ConfigureFluidIdeo`, `Page_ScenarioEditor` | Typen vorhanden | §6.1 | BESTÄTIGT (Metadaten) |
| `RimWorld.Page_CreateWorldParams`: `seedString`, `planetCoverage`, `rainfall`, `temperature`, `population`, `landmarkDensity`, `factions` (nonpublic), `pollution` (public) | Felder; **kein eigenes `DoNext`**, Generierung über `CanDoNext` | §6.3 | BESTÄTIGT (Metadaten) |
| `RimWorld.Page_SelectStartingSite.DoNext()`, `.CanDoNext()` | nonpublic | §6.3 | BESTÄTIGT (Metadaten) |
| `RimWorld.Page_ConfigureStartingPawns.DoNext()`, `.curPawnIndex`, `SelectPawn` | nonpublic | §6.3 | BESTÄTIGT (Metadaten) |
| `Verse.GameInitData` (**Namespace `Verse`**): `startingAndOptionalPawns`, `startingPawnCount`, `startingTile` (`PlanetTile`), `playerFaction`, `permadeath`, `gameToLoad` | public Felder | §6.3 | BESTÄTIGT (Metadaten) |
| `Verse.StartingPawnUtility.RandomizePawn(int)`, `.NewGeneratedStartingPawn(int)` | public static | §6.3 | BESTÄTIGT (Metadaten) |
| `RimWorld.Planet.WorldGenerator.GenerateWorld` | `(float planetCoverage, string seedString, OverallRainfall, OverallTemperature, OverallPopulation, LandmarkDensity, List<FactionDef>, float pollution)` | §6.3 | BESTÄTIGT (Metadaten) |
| `RimWorld.Planet.TileFinder.IsValidTileForNewSettlement` | `(PlanetTile tile, StringBuilder reason, bool forGravship)` | §6.3 | BESTÄTIGT (Metadaten) |
| `Verse.Find.GameInitData`, `.World`, `.Scenario`, `.Storyteller`; `Verse.Current.Game`; `Verse.Game.InitData`, `.World`, `.ExposeData()`; `RimWorld.Planet.World.ExposeData()`, `.grid` | public | §6.3 | BESTÄTIGT (Metadaten) |
| `Verse.GameDataSaveLoader.SaveGame(string)`, `.LoadGame(string)`, `.LoadGame(FileInfo)`; `Verse.SavedGameLoaderNow.LoadGameFromSaveFileNow(string)` | public static | §6.3, §7.8 | BESTÄTIGT (Metadaten) |
| `Verse.Scribe.saver`, `.loader`; `ScribeSaver.InitSaving(string, string)`; `ScribeLoader.InitLoading(string)`; `Scribe_Deep.Look<T>(ref T, string, params object[])` | public | §6.3 | BESTÄTIGT (Metadaten) |
| `Verse.GenFilePaths.SaveDataFolderPath`; `Verse.GenCommandLine.TryGetCommandLineArg(string, ref string)` | public static | §9 | BESTÄTIGT (Metadaten) |
| `Verse.LongEventHandler.QueueLongEvent` (drei Overloads) | public static | §6.3 | BESTÄTIGT (Metadaten) |
| `RimWorld.Storyteller.difficulty`, `.difficultyDef`; `RimWorld.Difficulty.threatScale`; `StorytellerDef`, `DifficultyDef` | public | §6.3 | BESTÄTIGT (Metadaten) |
| `Verse.Rand.PushState()`, `PushState(int replacementSeed)`, `PopState()`, `Seed` (set) | public static | §7.4 | BESTÄTIGT (Metadaten) |
| `Verse.TickManager.TickManagerUpdate()`, `DoSingleTick()`, `TicksGame`, `CurTimeSpeed`, `Paused` | public | §7.1 | BESTÄTIGT (Metadaten) |
| `Verse.AI.Pawn_JobTracker.TryTakeOrderedJob(Job, JobTag?, bool requestQueueing)` (**Namespace `Verse.AI`**) | public | §7.2 | BESTÄTIGT (Metadaten) |
| `Verse.Designator.DesignateSingleCell(IntVec3)`, `DesignateMultiCell(IEnumerable<IntVec3>)`, `DesignateThing(Thing)` | public virtual | §7.2 | BESTÄTIGT (Metadaten) |
| `Verse.Thing.thingIDNumber`, `Verse.Map.uniqueID`, `Verse.Def.index`, `Verse.Def.shortHash` | public Felder (`int`/`int`/`ushort`/`ushort`) | §7.3 | BESTÄTIGT (Metadaten) |
| `Verse.Root_Play.Update()`, `Verse.Map.MapUpdate()`, `RimWorld.Autosaver.AutosaverTick()`, `Verse.GameComponent.ExposeData()` | public | §7.4, §7.8 | BESTÄTIGT (Metadaten) |

**Noch offen in VER-001 (braucht laufendes Spiel):** Welt ohne Karte speichern/laden und auf der Startortseite anzeigen; Größen von Welt- und Spielstand; Threads/Jobs im 1.6-Tickpfad (`Unity.Burst`, `Unity.Collections` liegen im `Managed`-Ordner, ihre Nutzung im Tickpfad ist ungeprüft).

## SETUP-001: Felder der Einrichtungsseiten (2026-10-15, gleiche Quelle)

| Symbol | Signatur in 1.6.4871 | Status |
|---|---|---|
| `RimWorld.Page_SelectScenario.curScen` | nonpublic Feld `Scenario` | BESTÄTIGT (Metadaten) |
| `RimWorld.Page_SelectStoryteller.storyteller`, `.difficulty`, `.difficultyValues` | nonpublic Felder `StorytellerDef`, `DifficultyDef`, `Difficulty` | BESTÄTIGT (Metadaten) |
| `RimWorld.Page_ChooseIdeoPreset.selectedIdeo`, `.selectedStructure`, `.presetSelection` | nonpublic Felder `IdeoPresetDef`, `MemeDef`, `PresetSelection` | BESTÄTIGT (Metadaten) |
| `RimWorld.Page_ConfigureIdeo.ideo`; `Page_ConfigureFluidIdeo` erbt von `Page_ConfigureIdeo` | public Feld `Ideo` | BESTÄTIGT (Metadaten, Vererbung durch Compiler-Fehler CS8120 bestätigt) |
| `RimWorld.Scenario.name`, `.summary`; `RimWorld.Ideo.name`; `Verse.Def.LabelCap` | public | BESTÄTIGT (Metadaten) |
| `Verse.WindowStack.Windows`, `TryRemove(Type, bool)`, `TryRemove(Window, bool)`; `Verse.LongEventHandler.AnyEventNowOrWaiting` | public | BESTÄTIGT (Metadaten) |
| `Verse.WindowLayer` (`GameUI`, `Dialog`, `SubSuper`, `Super`); `Verse.Window.layer`, `preventCameraMotion`, `closeOnCancel`, `focusWhenOpened`, `windowRect`, `PostOpen()` | public | BESTÄTIGT (Metadaten) |
| `Verse.Messages.Message(string, MessageTypeDef, bool)`; `RimWorld.MessageTypeDefOf.RejectInput` | public static | BESTÄTIGT (Metadaten) |
| `Verse.Find.WorldSelector` → `RimWorld.Planet.WorldSelector.SelectedTile` (`PlanetTile`); `Verse.Find.WorldGrid[PlanetTile]` → `Tile.PrimaryBiome` | public | BESTÄTIGT (Metadaten) |
| `RimWorld.Planet.PlanetTile.tileId`, `.Layer` → `PlanetLayer.LayerID`, `.Valid`; `Verse.GameInitData.permadeathChosen` | public | BESTÄTIGT (Metadaten) |
| `RimWorld.Page_ConfigureStartingPawns.DoNext()` | nonpublic, eigene Überschreibung (Harmony-Prefix) | BESTÄTIGT (Metadaten); Patch im Spiel ohne Fehler geladen |
