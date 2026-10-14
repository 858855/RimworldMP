# 16 — Version-Matrix, Upgrade-Check und Kompatibilitätskontrollen

## Nachweisbarer Stand 2026-10-14

| RimWorld | Distribution | Engine/Runtime | Build Target | Multiplayer | Prepatcher | Evidenz |
|---|---|---|---|---|---|---|
| 1.6 | Steam Hauptzweig | Unity/Mono | `net472` für typische neue Mods | `rwmt/Multiplayer` About.xml listet **1.6**, benötigter RW Build `>=1.6.4491` | MP-Dependency, für 1.6 angeboten | BESTÄTIGT zu About.xml; exact engine patch nicht erhoben |
| 1.7 | Steam `unstable`, seit 12.10.2026 | Waylith/.NET10/CoreCLR/Vulkan | `net10.0` für neu kompilierte Mods | **nicht nachgewiesen**, 1.6-MP-Mod hat harte inkompatible Prepatcher-Dependency | **nicht kompatibel laut Ludeon** | BESTÄTIGT zur Engine/Prepatcher, UNGEKLÄRT zur MP-Portierung |
| 1.6 ↔ 1.7 | Saves | Ludeon erklärt gegenseitige Save-Kompatibilität | verschiedene Buildtargets | **kein gemischtes MP** | nicht ableitbar | Herstellerangabe, nicht mit Modlisten validiert |

**Harmony:** Das 1.7 Community-Update-Dokument behandelt Harmony 3; genaue Harmony-Version in 1.6 ist je ausgelieferter Core/Framework-Version zu messen und im Build-Report einzutragen. `RimWorld.MultiplayerAPI 0.6.0` ist als `net472` auf NuGet aufgeführt, **keine 1.7 API-Version verifiziert**.

## 1.6-MP-Freeze für produktives Modding

1. `git rev-parse HEAD` von Multiplayer und API, Modlist/Configs und Spielbuild archivieren.
2. Unter gleicher Lizenz/Installation Build und Savegenerierung nachweisen.
3. Compat-Matrix erstellen: Vanilla, Harmony, MP, Prepatcher, Frameworks, DLCs, OS.
4. Neues Feature nur mit eigener Serien-Test-ID freigeben.

## 1.7-Preview-Protokoll

1. Unstable-Branch selektieren und lokalen 1.7-Build dokumentieren.
2. In eigener Variante `net10.0` mit Waylith/UnityStubs referenzieren.
3. Auf Prepatcher- oder AssetLoading-Abhängigkeiten prüfen; bei Prepatcher-harter Abhängigkeit `BLOCKED`.
4. Headless/Asset-Bake/GUI Smoke-Tests unterscheiden.
5. Erst wenn 1.7-MP-Mod mit offiziellen SupportedVersions, Dependency-Support und 2-Client-Tests verifizierbar ist, 1.7-MP-Zeile als `BESTÄTIGT` ergänzen.

## Links

- https://ludeon.com/blog/2026/10/announcing-the-waylith-engine-and-update-1-7/
- https://docs.google.com/document/d/e/2PACX-1vSykw5_DYaaw42nEznI9zmBZVT_Vgg2bxYq_v2XWvAsHC3QSKFuDK2OyiQ5WC9jvVRUcrjpFt7xLLF5/pub
- https://github.com/rwmt/Multiplayer/blob/master/About/About.xml
- https://www.nuget.org/packages/RimWorld.MultiplayerAPI/
- https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.7_Mod_Updates
