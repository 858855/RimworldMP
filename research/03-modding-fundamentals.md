# 03 — Modding-Grundlagen, Werkzeuge und Verzeichnisstruktur

## Entwicklungswerkzeuge

| Werkzeug | Zweck | Status/Limit |
|---|---|---|
| Steam/RimWorld nativer Client | echter Def-/Render-/Save-/MP-Test | proprietäre Installation nötig |
| .NET SDK + MSBuild | C# kompilieren; 1.6 `net472`, 1.7 `net10.0` | nicht jede SDK-Version kann Mono-Framework-Referenzen ohne extra Refpacks kompilieren |
| Rider, VS Code, Visual Studio (Windows) | IntelliSense, Debugging, Source Browsing | keine Game-Assembly enthalten |
| ILSpy/ILSpyCmd, dnSpyEx | **lokales** Assembly-Studium, Signaturen überprüfen | nur bei vorhandenen legalen Spiel-DLLs; dekompilierten Spielcode nicht redistributieren |
| Git/GitHub | Diff/Branches/Commit-Pinning | `master`-Links sind veränderlich |
| Harmony | methodenbasierte Runtime-Patches | Konflikte/Versionsbrüche; zuerst APIs bevorzugen |
| XML-Parser (Python, xmllint) | XML-Syntax validieren | nicht dasselbe wie Def-/XPath-Gültigkeit im Spiel |
| Steam Workshop | Distributionskanal | Veröffentlichung nur nach Tests und Lizenzprüfung |

## Gewöhnlicher Mod-Aufbau

```text
Example.Mod/
├── About/
│   ├── About.xml
│   └── Preview.png        # optional
├── Defs/
│   ├── ThingDefs/
│   ├── RecipeDefs/
│   ├── HediffDefs/
│   └── ResearchProjectDefs/
├── Patches/
│   └── Compatibility.xml
├── Assemblies/             # bei einfachem Build oder Vanilla-Verzeichnislayout
│   └── Example.Mod.dll
├── 1.6/
│   └── Assemblies/         # nur wenn LoadFolders.xml/Versionpfad korrekt bestätigt
├── 1.7/
│   └── Assemblies/         # getrennt gebautes net10.0
├── Languages/
│   ├── English/Keyed/
│   └── German/Keyed/
├── Textures/
├── Sounds/
└── LoadFolders.xml         # optionale explizite Versions-Ladeordner
```

**Wichtig:** Versionsordner sind schematische Alternativen. Ob `LoadFolders.xml` und darin aufgeführte Pfade richtig laden, **vor Veröffentlichung im jeweiligen Client testen**. Nicht gleichzeitig generische und versionspezifische Assemblies doppelt laden.

`About/About.xml`: `ModMetaData`, `name`, `author`, `packageId` (stabil, typischerweise `entwickler.projekt`), `supportedVersions/li`, Beschreibung. `modDependencies` = harte Abhängigkeit; `loadAfter/loadBefore` = Reihenfolge, **keine automatische API-/Version-Kompatibilität**. Steam-Workshop nutzt eigenes Publikationsverfahren und ist nicht automatisch durch bloßen Ordner gegeben.

## Entscheidung XML vs. C#

- Neues Item, Rezept, Forschung, Faktor oder statische Fraktion: in der Regel **XML**.
- Zusätzliche Felder / mehrfach nutzbares Thing-Verhalten: `ThingComp`, passende `CompProperties`.
- Gleitender Weltzustand, Zeitserien, Kartencache: `GameComponent`/`MapComponent`/`WorldComponent`.
- Neuer Workgiver/Job, KI-/Stat-Worker: C# mit Vanilla-Erweiterungspunkt.
- Mutation einer fremden privaten Methode ohne Hook: punktueller Harmony-Patch mit dokumentiertem Versions-/Konfliktrisiko.

## Mod-Kompatibilität und Abhängigkeiten

Installierte Mods **müssen auf Host und Clients in kompatibler Reihenfolge/Version** laufen. Name/PackageId, aktive DLCs, Def-Konflikte, Harmony-Patch-Order, alte Assets, Settings und C#-DLL-Zielversion sind zu kontrollieren. Das Multiplayer-Handshake enthält historische Checks für Protokoll/Defs/Mods; die exakte aktuelle Handshake-Implementierung im Quellcode nachprüfen.

Quellen: https://rimworldwiki.com/wiki/Modding_Tutorials/Mod_Folder_Structure ; https://github.com/rwmt/Multiplayer/blob/master/About/About.xml
