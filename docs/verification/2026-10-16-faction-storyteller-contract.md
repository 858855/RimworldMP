# FACTION-003 – Fraktionsvertrag und Vanilla-Aufrufpfade

- **Branch/Basis:** `codex/faction-storytellers` von `main` @ `d568766`; neuer Commit wird nach dieser Prüfung erstellt.
- **Umgebung:** Linux, RimWorld `1.6.4871 rev598` laut `Version.txt`, Unity/Mono, lokale `RimWorldLinux_Data/Managed/Assembly-CSharp.dll`; .NET SDK 10.0.402 unter `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet`. Ein früher gestarteter Spielprozess meldete rev600; für diesen Stand wurde kein Spielprozess gestartet.
- **Änderung:** `MultiplayerSession.Begin` nimmt im Fraktionsmodus nur vollständige, eindeutige Zuweisungen von `PlayerKey`, `FactionSlot` 0–63, einer in der Welt vorhandenen Spielerfraktion (`Faction.loadID`) und einem sichtbaren, mit der Schwierigkeit kompatiblen `StorytellerDef` an. Die Listen werden nach Platz gespeichert. Der Coop-Pfad benötigt weiterhin keine Fraktionsdaten. Beim Laden sperren fehlende IDs/Defs, doppelte/ungeordnete Plätze und abweichende Host-Schwierigkeitswerte den Tick-Start.
- **Schwierigkeit:** Ein SHA-256-Fingerabdruck der 51 öffentlichen Instanz-Datenfelder von `RimWorld.Difficulty` bindet die Zuordnung an die Werte des Host-Spielstands. Feldnamen werden ordinal sortiert; `float` wird als IEEE-754-Bitmuster und `Def` als defName kodiert. Ein unbekannter Feldtyp führt zum Fehler statt zu einem stillen Vergleich. Der Fingerabdruck ersetzt weder den Host-Spielstand noch beweist er gleiches Laufzeitverhalten zwischen Clients.
- **Katalog:** `FactionStorytellerCatalog.AvailableFor` filtert geladene, sichtbare, nicht für Tutorials bestimmte Storyteller und solche mit erzwungener anderer Schwierigkeit. Die Auswahl ist nach `listOrder`, dann defName sortiert und auf die 32 vom Claude-Protokoll vorgesehenen Einträge begrenzt. UI und Nachrichtenaustausch sind auf diesem Branch noch nicht verdrahtet.

## Assembly-Prüfung

Ein .NET-Reflexionsprogramm las `Assembly-CSharp.dll` aus dem lokalen Managed-Verzeichnis und untersuchte Typen, Signaturen sowie `call`/`callvirt`/Feldreferenzen der Methodenkörper. Es wurde kein Spielcode ausgeführt und kein dekompilierter Quelltext gespeichert. Die erste breite Sichtung nutzte einen temporären Prober unter `/tmp/rimworld-faction-inspect/` (Exit 0); die für den Entwurf entscheidenden Aufrufpfade und der Fingerabdruck werden dauerhaft durch `scripts/faction-storyteller-probe/` geprüft.

| Pfad in der lokalen Assembly | Befund |
|---|---|
| `TickManager.DoSingleTick` | Ruft `Find.Storyteller` und dessen `StorytellerTick` genau einmal im Vanilla-Tickpfad auf. |
| `Storyteller.StorytellerTick` | Tickert die eigene `incidentQueue`, erzeugt Intervall-Incidents und ruft `TryFire` auf. |
| `Storyteller.AllIncidentTargets` | Sammelt aus `Find.Maps`, spielergesteuerten Karawanen und `Find.World`; kein Fraktionsfilter in diesem Getter. |
| `IncidentQueue.IncidentQueueTick` | Ruft beim Abfeuern `Find.Storyteller.TryFire` statt den besitzenden Storyteller auf. Eine Queue je Instanz genügt deshalb allein nicht. |
| `Storyteller.ExposeData`, `Game.ExposeSmallComponents` | Vanilla speichert den globalen Storyteller einschließlich Def, Schwierigkeit, Komponenten und Queue. Für weitere Instanzen braucht es explizite Persistenz und geprüfte Lade-Reihenfolge. |
| `Difficulty` | 51 öffentliche Instanzfelder; die Typen sind `bool`, `float`, `int`, nullable `float` oder `Def`-Unterklassen. Alle werden vom Fingerabdruck kodiert. |

**Folge:** Zusätzliche `StorytellerTick`-Aufrufe würden ohne gezielte Ziel- und Queue-Isolation Ereignisse anderer Fraktionen zulassen oder über den globalen Storyteller abfeuern. Es wurde daher kein Mehrfach-Tick-Hook eingebaut. ADR 0045 verwendet nun ADR 0037 R6 als geplantes Modell: alle Peers ticken künftig in Platzreihenfolge unter kontrolliertem Rand. Weltweite Ereignisse (R4), Quests, Gravship, Raid-Vermögen und modded Incidents benötigen gesonderte Zielprüfung. Der produktive Fraktionsstart bleibt gesperrt.

## Ausgeführte Prüfungen

| Befehl | Ergebnis |
|---|---|
| `env DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen, 0 Fehler. Build gegen lokale 1.6-Assemblies. |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project scripts/faction-storyteller-probe/faction-storyteller-probe.csproj -c Release --no-launch-profile -- /mnt/Spiele_1/SteamLibrary/steamapps/common/RimWorld/RimWorldLinux_Data/Managed src/Multiplayer.Foundation.RimWorld/bin/Release/netstandard2.1/Multiplayer.Foundation.RimWorld.dll` | Exit 0; 51 Datenfelder kodierbar, gleicher Wert liefert denselben 64-stelligen SHA-256-Wert, Änderung von `threatScale` ändert ihn. Vanilla-Tick, globale Incident-Ziele und globaler Queue-Eigentümer-Pfad bestätigt. Das ist kein Save/Load- oder RimWorld-Laufzeittest. |
| `python3 scripts/validate_project_state.py` | Zuerst Exit 1, weil dieser referenzierte Nachweis noch nicht angelegt war. Nach Anlage Exit 0: 22 Pflichtdateien, 63 Aufgaben samt Status, Abhängigkeiten und Verweisen geprüft. |
| `git diff --check` | Exit 0. |

**Nicht ausgeführt:** RimWorld-Start, Save/Load des neuen Fraktionsmodells, Storyteller-UI, zwei RimWorld-Clients und Incident-Ausführung. Der Server lässt eine Fraktionspartie weiterhin nicht starten. Der Build ist kein Laufzeitnachweis.
