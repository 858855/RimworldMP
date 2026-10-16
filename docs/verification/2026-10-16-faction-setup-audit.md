# FACTION-005 – Audit des Fraktions-Startpfads

- **Branch/Basis:** `codex/faction-setup` von `main` @ `9fc1519`.
- **Umgebung:** Linux, lokale RimWorld-Installation `1.6.4871 rev598` laut `Version.txt`, Unity/Mono; .NET SDK 10.0.402. Für diese Arbeit wird kein Spielprozess gestartet.
- **Ziel:** Signaturen und Aufrufpfade zu `GameInitData.playerFaction`, Fraktionsgenerator, Fraktionsmanager und Spielstart gegen die lokale Assembly belegen. Das ist eine Vorbereitung, kein Fraktionsstart.

## Ausgeführte Prüfung

`/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project scripts/faction-setup-probe/faction-setup-probe.csproj -c Release --no-launch-profile -- /mnt/Spiele_1/SteamLibrary/steamapps/common/RimWorld/RimWorldLinux_Data/Managed` — Exit 0. Das Skript prüft die erwarteten Call-Sites zusätzlich zur Ausgabe der Signaturen. Ein erster Lauf des neu geschriebenen Skripts scheiterte beim Kompilieren an falscher Tupel-Dekonstruktion und fehlendem Rückgabewert; nach gezielter Korrektur verlief der erneute Lauf erfolgreich.

| Befund aus Metadaten/IL | Bedeutung |
|---|---|
| `GameInitData.playerFaction: Faction`, `.startingTile: PlanetTile` | Vanilla hat für Startfraktion und Startort je einen Einzelwert. |
| `PageUtility.<InitGameStart>g__PreLoadAct|1_0 → GameInitData.PrepForMapGen → Faction.OfPlayer → FactionManager.OfPlayer` | Der Vanilla-Start bindet die vorbereiteten Pawns an die globale Spielerfraktion. Ein zusätzlicher `FactionDef.isPlayer`-Eintrag ersetzt diese Bindung nicht. |
| `FactionGenerator.NewGeneratedFaction(FactionGeneratorParms)` sowie `(PlanetLayer, FactionGeneratorParms)` | Ein öffentlicher Generator für eine weitere Fraktionsinstanz existiert. Der zweite Pfad vergibt `UniqueIDsManager.GetNextFactionID`, wählt/generiert Ideologie und Beziehungen und kann Weltobjekte erzeugen. Determinismus und gewünschte Ideologie sind damit noch nicht bewiesen. |
| `FactionGenerator.CreateFactionAndAddToManager(PlanetLayer, FactionDef) → NewGeneratedFaction → FactionManager.Add → RecacheFactions` | Der Vanilla-Registrierungsweg ist bekannt. Eine weitere Fraktion muss vor Spielstart und Save geprüft sowie explizit einem `FactionSlot` zugeordnet werden. |
| `WorldGenerator.GenerateWorld` führt `GameSetupStep.GenerateFresh` aus | Weltgeneration ist ein mehrstufiger Prozess. Der Audit beweist keine sichere Einfügestelle für mehrere Startkolonien. |

**Abgeleitet, noch nicht verifiziert:** Eine Spielseiten-Hook-Grenze liegt nach Abschluss der Host-Weltgenerierung, aber vor `GameInitData.PrepForMapGen` und der ersten Map-Generierung. Sie muss zuerst den gewählten Fraktionsplan aller Slots und ihre Startorte/Ideologien erhalten. Danach sind Pawns, Karten und alle `Faction.OfPlayer`-abhängigen Systeme zu prüfen. Ein Harmony-Patch oder weiterer Fraktionsaufruf wurde nicht eingebaut.

**Offen:** Der Koordinator speichert derzeit nur einen globalen Startort und keine eigene Ideologie/Startort-Wahl je Slot. Save/Load, mehrere Fraktionen, zwei Clients, Incident-Ziele und Spielstart bleiben ungetestet. Der produktive Fraktionsstart bleibt gesperrt.
