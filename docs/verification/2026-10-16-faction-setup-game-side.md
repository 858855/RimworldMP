# FACTION-005 – Spielseite auf ADR 0038/0046

- **Branch:** `codex/faction-setup`, lokal auf `claude/faction-sites` @ `428a065` aufgebaut; kein Push oder Merge.
- **Umgebung:** Linux; RimWorld `1.6.4871 rev598` aus `Version.txt`; installierte 1.6-Assemblies unter `RimWorldLinux_Data/Managed`; .NET SDK 10.0.402.
- **Eingriff:** Nur Spieladapter, Codex-Probe und Codex-Dokumentation. Claudes Protocol/Network/Lobby/Session/Hosting/Server nicht verändert.

## Belegte API-Signaturen

`/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project scripts/faction-setup-probe -- /mnt/Spiele_1/SteamLibrary/steamapps/common/RimWorld/RimWorldLinux_Data/Managed` — Exit 0. Reflection/IL bestätigen:

- `WorldGrid.ApproxDistanceInTiles(PlanetTile, PlanetTile)` und `TileFinder.IsValidTileForNewSettlement(PlanetTile, StringBuilder, bool)` für lokale und hostseitige Standortprüfung.
- `ModsConfig.IdeologyActive`, `Ideo.ExposeData`, parameterlosen `Ideo`-Konstruktor, `Page_ConfigureIdeo.ideo` sowie den bereits genutzten `Scribe_Deep`-Pfad für eine bearbeitete Ideologie.
- `GameInitData.PrepForMapGen → Faction.OfPlayer → FactionManager.OfPlayer`: weiterhin nur eine globale Startfraktion. Die Probe bestätigt keine sichere zweite Karten-/Pawn-Generierung.

## Tatsächlich ausgeführte Prüfungen

| Prüfung | Befehl | Ergebnis |
|---|---|---|
| Mod-Release-Build | `env DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Exit 0, 0 Warnungen, 0 Fehler. |
| Projektvalidator | `python3 scripts/validate_project_state.py` | Exit 0, 22 Pflichtdateien und 66 Aufgaben geprüft. |
| Vorhandene .NET-Testprogramme | `for project in tests/*.Tests/*.csproj; do /home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project "$project" -c Release --no-restore || exit 1; done` | Exit 0; Client, Hosting, Integration, Lobby, Network, ProcessIntegration, Protocol und Session melden PASS. Das sind spielunabhängige Tests. |
| Git-Diff | `git diff --check` | Exit 0. |
| RimWorld-Spieltest | Nicht ausgeführt | `NICHT_GETESTET`: Benutzer hat keine Freigabe für Spielstart erteilt. |

`dotnet test` auf diesen Projekten führte keine Tests aus, da es ausführbare Testprogramme und keine Test-SDK-Projekte sind. Deshalb wurden sie mit `dotnet run` gestartet; nur deren PASS-Ausgaben zählen als Prüfnachweis.

## Stand und Grenzen

- Standort 98 und Ideologie 99 sind in der Spiel-UI angebunden. Der Host vergleicht vollständige Ideologie-Daten je Slot mit `PlayerView.Ideology` und schickt bei Mangel oder Fehler 100. `PlayerReadyChanged` 14 gilt auf beiden Seiten. Coop 26/27 bleibt im Coop-Pfad.
- Das Format für Vorlagen, bearbeitetes Scribe-XML und die feste Wahl ohne DLC steht in ADR 0046. Die Strukturprüfung beweist nicht, dass eine bearbeitete Ideologie als getrennte Instanz in einer neuen Fraktion funktioniert. Ein eigener Ideologie-Editor für Gäste fehlt; sie können Vorlagen wählen.
- `MultiplayerSession` speichert Standort mit Platz/Fraktion/Erzähler und verwirft beim Laden unvollständige Fraktionstabellen. Die Wiederherstellung sendet 101; der Host soll anschließend seinen geladenen Spielstand wie SETUP-005 übertragen. Dieser Pfad ist nur gebaut, nicht im Spiel geprüft.
- Der neue Fraktionsstart wird zusätzlich spielseitig vor Vanilla-`InitGameStart` gestoppt. Eine zweite Spielerfraktion und getrennte Karten/Pawns werden noch nicht erzeugt. Weltweite Ereignisse nach ADR 0037 R4 bleiben offen.
