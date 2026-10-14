# Nachweisbare Quellen — Abrufdatum 2026-10-14

| Projekt/URL | Typ / Stand | Version | Verlässlichkeit | Aussage und Grenze |
|---|---|---|---|---|
| [Ludeon Waylith Announcement](https://ludeon.com/blog/2026/10/announcing-the-waylith-engine-and-update-1-7/) | offiziell, 12.10.2026 | 1.6 / 1.7 unstable | hoch | Enginewechsel, Performance, Betazweig, allgemeine Save-/Mod-Kompatibilität; keine fremde MP-Garantie |
| [Ludeon Modder Primer 1.7](https://docs.google.com/document/d/e/2PACX-1vSykw5_DYaaw42nEznI9zmBZVT_Vgg2bxYq_v2XWvAsHC3QSKFuDK2OyiQ5WC9jvVRUcrjpFt7xLLF5/pub) | offiziell, WIP, 14.10.2026 abgerufen | 1.7 | hoch, **WIP** | Vulkan/.NET10/Waylith/UnityStubs, Prepatcher-Ausnahme, Headless/Arguments/Jobs |
| [rwmt/Multiplayer](https://github.com/rwmt/Multiplayer) | Open Source | 1.6 im About.xml | hoch zu Code | Multiplayerarchitektur; moving `master` statt commitfest |
| [MP About.xml](https://github.com/rwmt/Multiplayer/blob/master/About/About.xml) | Quellcode | 1.6, >=1.6.4491 | sehr hoch für deklarierte Dependency | explizit Prepatcher und keine 1.7-Deklaration |
| [MP Releases](https://github.com/rwmt/Multiplayer/releases) | Release-Metadaten | v0.11.5 / continuous | hoch | Releaseart/Server-Pakete; widersprüchliche Patchnummern in Detailtext prüfen |
| [MP TickPatch.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Patches/TickPatch.cs) | Source | master für 1.6 | hoch für eingesehene Datei | RunCmds/DoUpdate/DoTick und Command-Queue; nicht automatisch alle Branches |
| [MP AsyncTimeComp.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Client/AsyncTime/AsyncTimeComp.cs) | Source | master für 1.6 | hoch | map async tick, command context, UpdateManagers-Kommentar |
| [MP SyncSerialization.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Common/Syncing/SyncSerialization.cs) | Source | master | hoch für Mechanismus | Typ-/Objektserialisierung; vollständige API nicht exportiert |
| [MP MultiplayerGame.cs](https://github.com/rwmt/Multiplayer/blob/master/Source/Client/MultiplayerGame.cs) | Source | master | hoch | SyncCoordinator, World/Map-Komponenten |
| [Multiplayer API Repo](https://github.com/rwmt/MultiplayerAPI) | Open Source | development | hoch für Code | Public API-Interface, MIT |
| [MP API Wiki](https://github.com/rwmt/MultiplayerAPI/wiki) | Entwickler-Wiki, einige Inhalte alt (~2019) | ältere APIs | **mittel** | `[SyncMethod]`, `[SyncField]`, `Watch`, `SyncWorker`; Signaturen gegen aktuelle DLL prüfen |
| [MP API NuGet](https://www.nuget.org/packages/RimWorld.MultiplayerAPI/) | Paketmetadaten | **0.6.0** (net472) | hoch | Zielplattform/Version, kein Laufzeittest |
| [MP Compat](https://github.com/rwmt/Multiplayer-Compatibility) | Open Source | variabel | hoch für Repo | Patterns für Mod-Patches; keine Garantie, dass alle Mods kompatibel sind |
| [MP Compat Desynchronized](https://github.com/rwmt/Multiplayer-Compatibility/blob/master/Source/Mods/Desynchronized.cs) | Quellcode | variabel | hoch für Beispiel | `MpCompatFor`, RNG-Push/Pop im Compat-Projekt, kein offizielles MP-API-Attribut |
| [Harmony Docs](https://harmony.pardeike.net/articles/intro.html) | Primär-Doku | versionsabhängig | hoch | Prefix, Postfix, Transpiler, Finalizer, Priorität |
| [RimWorld Wiki 1.7 Updates](https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.7_Mod_Updates) | Community | 1.7 | mittel | Harmony 3/API-Portierungen, laufende Änderungen |
| [RimWorld ModFolderStructure](https://rimworldwiki.com/wiki/Modding_Tutorials/Mod_Folder_Structure) | Community | mehrere | mittel | About, Defs, Patches, Assets, LoadFolders; genaue Pfade live testen |
| [RimWorld XML Patches](https://rimworldwiki.com/wiki/Modding_Tutorials/PatchOperations) | Community | mehrere | mittel | XML-PatchOperation XPath |
| [MP Historical Handshake](https://github.com/rwmt/Multiplayer/wiki/Handshake-Process) | Entwickl.-Wiki 2019 | historisch | **niedrig für aktuelle Details** | Handshake-Modell, keine Paketversion als aktuelle Wahrheit |
| [Old DesyncCheck](https://github.com/Zetrith/Multiplayer/blob/master/Source/Client/DesyncCheck.cs) | Altcode | alte Version | **veraltet** | konzeptionelle SyncInfo-Analyse, nicht 2026 API |

**Lizenzgrenzen:** Jede Repository-Lizenz separat im jeweiligen Commit prüfen. Die Links dienen der Recherche, nicht als Einbettung fremden Quellcodes. `master`-Quelllinks bitte vor tatsächlicher Entwicklung als Commit-Permalink sichern.
