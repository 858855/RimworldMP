# 15 — Build-Prozess, reproduzierbare Tests und Qualitätssicherung

## Versionierter Buildprozess

1. RimWorld-Zweig **1.6 main** oder **1.7 unstable** feststellen. Spielstand-Backup und Modliste sichern. Versionsbezeichnung im Bericht mit Dateiversion/Binary-SHA/Branch.
2. Ziel-Framework: `net472` vs `net10.0`. Referenzen nur aus **genau dieser** legal installierten Zielversion. Quellabhängigkeiten per Paketversion fixieren.
3. Build aus projekt-/versionsgetrenntem Sourcefolder, Output in passende `Assemblies/` nach bestätigtem `LoadFolders.xml`-Layout. Keine Spiel-DLLs kopieren.
4. XML-Files prüfen, `About.xml`-Version und PackageID, Def-Parents, Patch-XPaths, lokalisierte Strings, Case-Sensitivity.
5. Build: `dotnet build path/to/Project.csproj -c Release /p:RimWorldManaged="..."`; Ausgabe prüfen. **Dieses Kommando ist Vorlage** und benötigt passende projektseitige Property/Targetingpacks.
6. Lokalen Mod installieren: per Symlink oder Kopie in den **realen** Steam-Mod-Ordner (nicht SteamWorkshop-Cache verwechseln).
7. Singleplayer: Start, initialer Log, Core/Defs, frische Map und Save, mod-spezifische Aktionen, Speichern, Beenden/Neustart, Save laden. Keine grünen Tests ohne Game-Lauf.
8. Multiplayer 1.6: beide Clients gleiche Builds und Modlisten, Host/Client-Klick, Save/Load, Speed/Pause, Reconnect, 30min Replay/Tick-Stresstest, Desync-/Error-Logs.
9. Rückwärtskompatibilität gegen alten Save nach angemessener Versionsmigration gesondert testen. 1.7 Waylith-Binärkompatibilität separat vom 1.6-MP-Test bewerten.

## Testmatrix

| Stufe | Test | Kann Codex ohne Spiel ausführen? | Beweist |
|---|---|---|---|
| S0 | XML-Wellformedness, `packageId`, lokaler Dateibaum | ja | nur Syntax und Schema-Grundcheck |
| S1 | .NET Build / AssemblyRefs / NuGet | wenn passende SDK/Legal DLLs vorhanden | nur Kompilation/Linking |
| S2 | 1.6 Game-Startup + Def-Patches | nein | Game-Load für Modliste |
| S3 | 1.6 Save/Load + Gameplay-UI | nein | SP-Verhalten, kein MP |
| S4 | zwei MP-Clients 1.6 Host/Client, Commands, RNG | nein | konkrete MP-Session-Funktion |
| S5 | Save/Load, Reconnect, Speed, multi-map | nein | robustere MP-Kompatibilität |
| S6 | 1.7 `-Headless -mod=...`, AssetBake | nur mit installiertem 1.7 Game | potenzielle Waylith-Build-/Def-Kompatibilität |
| S7 | Waylith UI/Shader-Vulkan + echte Interaktion | nein | 1.7 UI/Asset-Kompatibilität |
| S8 | MP unter 1.7 | **aktuell blockiert/ungeklärt** | keinerlei Zusage vor Prepatcher/MP-Port |

## Testprotokoll-Vorlage

```text
Test-ID: MP-CLIENT-TOGGLE-001
RimWorld: 1.6.<exakte Buildnummer>
Multiplayer: v0.11.5 oder gepinnter Commit
Prepatcher/Harmony/API: <Versionen und Hash>
Host: <OS>, Client: <OS>, gleiche Modliste SHA256: <hash>
Precondition: Save-ID, Seed, Thing-ID, Map uniqueID, Timer
Action: Client klickt Toggle bei Tick N einmal
Expected: Host und Client führen denselben Command genau einmal aus
Evidence: Game-Logs beider Peers, Video/Screenshot, Statevergleich
Observed: <tatsächlich>
Status: PASS/FAIL/NOT-RUN
```

## Release-Gates

- P0 `XML_OK` (automatisierbar).
- P1 `BUILD_OK` für **jede** unterstützte Runtime (RimWorld 1.6/1.7 getrennt).
- P2 `SP_BOOT_SAVE_LOAD_OK` nur nach echtem Spielstart.
- P3 `MP_TWO_CLIENTS_OK` nur mit unterstützter Version und zwei realen Instanzen.
- P4 `OS_CROSS_TESTED` nur nach Linux-Windows-Mixed-Test; `WAYLITH_1_7_TESTED` nur nach 1.7-Client-Test.

Fehlt ein Gate, Scope begrenzen und öffentlich ehrlich deklarieren, z. B. „1.6 Singleplayer getestet, Multiplayer nicht getestet“.

## Automation auf Linux

```bash
python3 scripts/validate_structure.py  # im beigefügten Wissensbasis-Repo enthalten
# Build-Beispiel erst nach Anbindung lokaler Game-Assemblies:
# dotnet build examples/minimal-mod/src/ExampleMod.csproj -c Release \
#  /p:RimWorldManaged="$RIMWORLD_MANAGED"
# 1.7: echtes Binary und -Headless / -mod mit lokalem Launcher prüfen.
```

**Begrenzung:** Eine Headless-Instanz mit regulärem UI-Input ist kein Multiplayer-Integrationstest; Headless-Ladeerfolg kann auch ohne grafische User-Interaktion bestehen. Alle tatsächlichen automatisierten Prüfungen dieser ausgelieferten Knowledge Base sind im README und finalem Bericht auszuweisen.
