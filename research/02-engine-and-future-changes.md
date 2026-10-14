# 02 — Waylith: bestätigter Enginewechsel, Migration und Risiken

**BESTÄTIGT:** Ludeon kündigte am **12.10.2026** RimWorld 1.7 auf **Waylith** an; Steam-Branch **unstable**, nicht die aktuelle Hauptversion. Verbindliche Primärquelle [Ankündigung](https://ludeon.com/blog/2026/10/announcing-the-waylith-engine-and-update-1-7/). Die frühere Annahme „RimWorld bleibt vollständig auf Unity“ ist überholt.

## Architekturgegenüberstellung

| Dimension | 1.6 Hauptzweig | 1.7 unstable |
|---|---|---|
| Engine | Unity | Ludeons hauseigene Waylith |
| Managed Runtime | Mono | CoreCLR/.NET 10 |
| Rendering | Unity-Pipeline | Vulkan 1.2, Versuch auf Vulkan 1.0 zurückzufallen |
| Game Code | `Assembly-CSharp.dll` | `Assembly-CSharp.dll` weitgehend unverändert |
| Engine API | UnityEngine | `Waylith.dll` + `UnityStubs.dll` Unity-API-Kompatibilität |
| Prozess-Host | Unity-Laufzeit | `RimWorld.dll` hostet Engine/Game |
| Kompilationsziel NEUER Mods | i. d. R. `net472` | `net10.0` laut Primer |
| Harmony | 2.x (Version exakt buildabhängig) | Harmony-3-Umstellung laut Community-Migrationsleitfaden; exakte Paketversion lokal prüfen |
| Prepatcher | MP-Abhängigkeit auf 1.6 | laut Ludeon *nicht lauffähig* aufgrund fehlenden Early-Hooks/Restarts |
| MP | `rwmt/Multiplayer` 1.6 verifiziert | **UNGEKLÄRT / nicht nachgewiesen** |

### Erhaltene Kompatibilität

Ludeon nennt eine **UnityStubs.dll** als Emulationsoberfläche früherer Unity APIs und eine Emulation einzelner Mono-Verhaltensweisen. Dies erklärt die breite 1.6-Binärmod-Kompatibilität. **Nicht daraus schließen**, dass neue Waylith-APIs ohne Anpassung zu 1.6 kompatibel sind. Die offizielle Aussage „Savegames in beide Richtungen“ ist keine Garantie für komplexe gemoddete Spielstände mit MP.

### Breaking Changes / konkrete Blocker

1. **Prepatcher**: engine-seitiger Hook und Neustart funktionieren nach Herstellerangaben nicht. Da `rwmt/Multiplayer/About/About.xml` `zetrith.prepatcher` als `modDependencies` nennt, existiert eine **harte Kompatibilitätslücke**. Keine 1.7-MP-Tauglichkeit behaupten.
2. **Asset Loading**: tiefgreifend geändert; Assets-/Texture-/Shader-Hooks und dynamisches Laden besonders riskant. Waylith bietet `-BakeModAssetPacks=` für `Assets.zip`.
3. **Managed Runtime**: neue `net10.0`-Builds benötigen eigene Referenzen und Testpfade; Reflection/IL/Methodensignaturen und Harmony-Patches können brechen.
4. **Vulkan**: custom Shader bzw. alte Unity-Assets müssen angepasst/validiert werden; UI-Kompatibilität darf nicht aus C#-Build abgeleitet werden.
5. **Jobs/Threads**: Waylith.Jobs erlaubt Worker-Threads, aber mutierende Arbeit kann Sync/Determinismus gefährden.

### Neue dokumentierte 1.7-Argumente

- `-Headless`: kein Grafik-Subsystem, kein Fenster, ideal für automatisierte Start- und Asset-Prüfungen auf Linux; **kein Multiplayer-Zwei-Client-Beleg**.
- `-NoWindow`: Grafikpipeline aktiv, unsichtbares Fenster; **gegenseitig ausschließend** mit `-Headless`.
- `-mod=<packageIdOne>[;<packageIdTwo>...]`: nur gewählte Mods + Core + installierte DLCs; umgeht ModsConfig.xml.
- `-BakeModAssetPacks=<modOne>[;<modTwo>...]`: erzeugt `Assets.zip` pro relevanten Load-Folder, beendet danach.
- `-ColdBoot`: Texturcache übergehen; `-Verbose`: detailliertere Engine-Logs; `-WatchShaders`: Shader neu kompilieren.

**Achtung Shell:** `;` in Arguments muss zitiert werden (z. B. `'-mod=example.one;example.two'`), sonst behandelt Bash es als Befehlstrenner.

### Migrationsentscheidung für Codex

- Bestehenden 1.6-C#-Mod **nicht reflexartig auf 1.7 umstellen**: zunächst tatsächliche Binärfunktion auf Waylith messen, danach getrennte Projektkonfiguration für `net10.0` erstellen.
- Ziel nur 1.6+Multiplayer: auf net472/1.6 bleiben, bis aktive MP-Entwickler vollständige 1.7-Unterstützung und Prepatcher-Ersatz bzw. -Migration bestätigen.
- Bewährte Def- und Vanilla-Erweiterungen priorisieren; tiefe Reflection, Harmony-Transpiler und Unity-/Asset-Integration als besonders risikoreich markieren.

**Primär:** https://docs.google.com/document/d/e/2PACX-1vSykw5_DYaaw42nEznI9zmBZVT_Vgg2bxYq_v2XWvAsHC3QSKFuDK2OyiQ5WC9jvVRUcrjpFt7xLLF5/pub

**Sekundär für Harmony-3-Migration:** https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.7_Mod_Updates
