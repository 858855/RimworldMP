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
