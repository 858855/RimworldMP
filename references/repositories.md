# Repositories und Dateien zum direkten Quellstudium

| Name | Repository / relevante Pfade | Zweck und Vorsicht |
|---|---|---|
| rwmt Multiplayer | https://github.com/rwmt/Multiplayer | 1.6 Lockstep-Client, Server und Patchintegration. `About/About.xml` legt 1.6+Prepatcher fest. |
| MP TickPatch | https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Patches/TickPatch.cs | `RunCmds()` und `DoTick()`: genaue Command-/Tick-Order. |
| MP async maps | https://github.com/rwmt/Multiplayer/blob/master/Source/Client/AsyncTime/AsyncTimeComp.cs | `ExecuteCmd`, `UpdateManagers`, `Map`/Command Context. |
| MP game | https://github.com/rwmt/Multiplayer/blob/master/Source/Client/MultiplayerGame.cs | SyncCoordinator, World/Map Components. |
| MP Serializer | https://github.com/rwmt/Multiplayer/blob/master/Source/Common/Syncing/SyncSerialization.cs | Lesen/Schreiben von Sync-Objekten. |
| MP Sync | https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Syncing/Sync.cs | Client-Sync-Mechanismen. |
| MP SyncMethod | https://github.com/rwmt/Multiplayer/blob/master/Source/Client/Syncing/Handler/SyncMethod.cs | Command-Handler. |
| MP public API | https://github.com/rwmt/MultiplayerAPI | Extern verwendbares API; Wiki alt, Paketversion 0.6.0. |
| MP Compat | https://github.com/rwmt/Multiplayer-Compatibility | Mod-spezifische Adapter, z. B. `Source/Mods/Desynchronized.cs`. |
| Prepatcher | https://github.com/Zetrith/Prepatcher | 1.6 Dependency; 1.7 Waylith kein kompatibler Early-Hook. |
| Harmony | https://github.com/pardeike/Harmony | offizielle .NET-Patchbibliothek, Ausführungsordnung. |
| HugsLib | https://github.com/UnlimitedHugs/RimworldHugsLib | Helper-Komponenten (optional). |
| Vanilla Expanded Framework | https://github.com/Vanilla-Expanded/VanillaExpandedFramework | Beispiel komplexer Def/C#-Framework-Pfade. |
| Performance Fish 1.6 | https://github.com/DannyProtons/Performance-Fish-1.6 | Performance-/Cache-Design, **keine automatische MP-Freigabe**. |

**Commit-Pinning:** In dieser Erhebung konnte kein vollständiger git-Clone ausgeführt werden. Die untersuchten URLs referenzieren deshalb `master` und sind **nicht unveränderlich**. Codex muss für eine spätere konkrete Implementation `git rev-parse HEAD` / commit-URL erzeugen und Versionskompatibilität neu prüfen.
