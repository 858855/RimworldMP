# FACTION-003/005 – Getrennte Erzähler im Fraktionszweig

- **Branch:** `codex/faction-setup` (lokal auf `claude/faction-sites` @ `428a065`; zum Prüfzeitpunkt nicht auf `origin/main`). Relevanter Commit: den Commit dieser Arbeitseinheit mit `git log -1` ermitteln.
- **Umgebung:** Linux, RimWorld `1.6.4871 rev598` (`Version.txt`), Managed-Assemblies der lokalen Installation, .NET SDK 10.0.402.
- **Änderung:** `MultiplayerSession` legt je Platz eine eigene `Storyteller`-Instanz an und persistiert sie als Deep-Scribe-Liste. Der Hook ersetzt für vollständige Fraktionssitzungen den Vanilla-Tick durch Platzreihenfolge. `Find.Storyteller` wird nur innerhalb dieses Tick-Kontexts auf die aktive Instanz umgebogen. Zielmengen enthalten nur Karten und Weltobjekte der zugehörigen Fraktion. `TryFire` unterdrückt nicht zuordenbare Weltziele; ein konkretes Ziel einer anderen Fraktion wirft einen Fehler. Der 1.6-Iterator liest zudem globale Quest-Teile; diese Quelle ist im Fraktionserzähler-Kontext deaktiviert. Bei aktivem Anomaly-DLC wird der Fraktions-Tick verweigert. Andere Spielmodi durchlaufen Vanilla.

## Ausgeführte Prüfungen

| Prüfung | Befehl | Tatsächliches Ergebnis |
|---|---|---|
| Lokale 1.6-API und Vanilla-Aufrufe | `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project scripts/faction-storyteller-probe -- /mnt/Spiele_1/SteamLibrary/steamapps/common/RimWorld/RimWorldLinux_Data/Managed src/Multiplayer.Foundation.RimWorld/bin/Release/netstandard2.1/Multiplayer.Foundation.RimWorld.dll` | Exit 0. `StorytellerTick`, `AllIncidentTargets`, `Find.Storyteller`, Queue-Tick, `TryFire(FiringIncident,bool)`, Deep-Scribe-Voraussetzungen sowie `Map`-/`Caravan`-Besitzoberflächen bestätigt. Der Iterator enthält globale Quest-Teile und Anomaly-Abfragen. Eine erste Probe mit nur einem `TryFire`-Parameter schlug fehl (Exit 134); Hook und Probe wurden auf die tatsächlichen zwei Parameter korrigiert. |
| Vanilla-Startpfad und weitere Generierungssignaturen | `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project scripts/faction-setup-probe -- /mnt/Spiele_1/SteamLibrary/steamapps/common/RimWorld/RimWorldLinux_Data/Managed` | Exit 0. Ausgabe lokal unter `/tmp/rimworld-faction-slice-api.txt`; bestätigt weiterhin den Einzel-Fraktionspfad über `PrepForMapGen`. |
| Mod-Build | `env DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Exit 0, 0 Warnungen, 0 Fehler. |
| Acht .NET-Testprogramme | `for project in tests/*.Tests/*.csproj; do dotnet run -c Release --project "$project" --no-restore; done` mit dem lokalen .NET-Binary | Client, Hosting, Integration, Lobby, Network, ProcessIntegration, Protocol und Session jeweils Exit 0; Rohlogs `/tmp/rimworldmp-*-20261016.log`. Diese Programme starten RimWorld nicht. |
| Dokumentation | `python3 scripts/validate_project_state.py` | Exit 0, 22 Pflichtdateien und 66 Aufgaben geprüft. |

## Grenzen und offene Risiken

- **`NICHT_GETESTET` im Spiel:** Harmony-Anwendung, Erzähler-Tick, Incident-Ziele, Deep-Scribe-Roundtrip, Save/Load und zwei echte Clients. Zum Zeitpunkt dieses Berichts lag keine ausdrückliche Freigabe zum Start von RimWorld-Instanzen vor.
- Ein zusätzlicher Versuch, ein uninitialisiertes `WorldObject` im .NET-Probeprozess mit Fraktionen zu belegen, schlug mit `UnityEngine.MaterialPropertyBlock.CreateImpl`/`ECall methods must be packaged into a system module` fehl. Die Probe führt deshalb nur Reflection-/IL-Prüfungen aus. Zieltrennung benötigt einen echten RimWorld-Prozess; dieser Versuch ist **kein** bestandener Zieltest.
- Die Fraktionsgenerierung erstellt noch keine zweite Spielerfraktion, Karte, Pawn-Gruppe oder Ideologie. Der Start ist in der Spielseite und im Koordinator gesperrt. Damit lässt sich der neue Erzählerpfad noch nicht über die normale Lobby ausführen.
- Direkte `Game.storyteller`-Zugriffe fremder Mods und Incident-Worker mit indirekten globalen Folgen sind nicht vollständig untersucht. ADR 0037 R4 lässt Weltwirkungen offen; sie dürfen bis zur Entscheidung nicht als korrekt isoliert gelten.
- Quest-basierte Storyteller-Incidents fehlen im Fraktionstestschnitt; Anomaly muss für diesen Modus deaktiviert sein. Diese Grenzen sind nur statisch abgesichert und bedürfen eines Spieltests.
- `Storyteller`-Objekte werden per Scribe getrennt gespeichert; ob RimWorld ihre Queue und Komponenten nach dem Laden korrekt wiederherstellt, muss im Spiel geprüft werden. Ein Build beweist das nicht.

**Nächster Nachweis:** Erst Mehrfraktions-Generierung und die Freigabe des Koordinator-Testpfads nach gemeinsamem Vertrag; dann zwei 1.6-Clients mit verschiedenen Erzählern, getrennten Maps/Pawns, Ereignisprotokollen, Save/Load und gleichen Lockstep-Hashes testen.
