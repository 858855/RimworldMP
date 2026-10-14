# Versionsmatrix — Stand 2026-10-14

| Kategorie | 1.6 | 1.7 | Nachweisstatus |
|---|---|---|---|
| Freigabekanal | Hauptzweig | unstable Beta seit 12.10.2026 | offiziell bestätigt |
| Engine | Unity | Waylith | offiziell bestätigt |
| Runtime | Mono | .NET 10/CoreCLR | Ludeon Primer |
| Grafik | Unity-basiert | Vulkan 1.2, Fallbackversuch auf 1.0 | Ludeon Primer |
| neue Mod-Projekte | `net472` üblich | `net10.0` empfohlen | 1.6 Community, 1.7 offiziell |
| `Harmony` | 2.x, exakte DLL versionsabhängig | 3.x nach Migrationsguide | 1.7 Community, genaue Patches offen |
| MP | `rwmt/Multiplayer` nur 1.6 in About.xml | keine nachgewiesene Unterstützung | 1.6 bestätigt; 1.7 UNGEKLÄRT |
| MP-Mindest-RW-Build | `>=1.6.4491` lt. About.xml | n/a | XML bestätigt |
| MP 0.11.5 | Release 1.6 | keine Zusage | Release-Metadaten |
| Multiplayer API | NuGet 0.6.0 `net472` | keine bestätigte Portierung | NuGet bestätigt |
| Prepatcher | direkte MP-Dependency | kein funktionsfähiger Early-Hook laut Primer | offiziell zur 1.7-Inkompatibilität |
| `Assembly-CSharp.dll` | klassische Core-Spielassembly | weitgehend beibehalten | Ludeon Primer |
| Neuer Host/Engine | Unity-Laufzeit | `RimWorld.dll`, `Waylith.dll`, `UnityStubs.dll` | Ludeon Primer |

**Nichts an dieser Matrix ersetzt echte Multiplayer-Zweiclienttests.** Nicht verifizierte Patch-Zeilen, Releases und DLL-Build-Hashes dürfen nicht aufgefüllt oder geraten werden.
