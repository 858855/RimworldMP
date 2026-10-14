# Offene Forschungsfragen (nicht erfinden)

| Frage | Warum offen? | Bedeutung | Nächster Prüfschritt |
|---|---|---|---|
| Wie/ob funktioniert `Prepatcher` auf Waylith 1.7? | Ludeon meldet fehlende Hook-/Restart-Fähigkeit, kein akzeptierter Ersatz verifiziert | **harter MP-Blocker** | Prepatcher-Repo, MP-1.7-Forks, offizielles Discord + eigener Build/Test |
| Gibt es inzwischen gepflegten 1.7-MP-Fork? | geprüfte `rwmt`-About.xml nur 1.6; keine offizielle 1.7-Portierung belegt | Versionsunterstützung | Releases, Branches, Tags, Workshop und Issue Tracker erneut prüfen |
| Exakte 1.7 Harmony 3 Releasebuilds? | Community-Migration dokumentiert 3, Beta kann sich rasch ändern | Build & Patchkompatibilität | ausgelieferte Harmony Assembly/Version im installierten Game auslesen |
| Exakte NuGet 0.6.0-MP-API Signaturen? | Wiki teilweise 2019, Paket-Binärsignaturen nicht lokal reflektiert | `[SyncMethod]`, `Watch`, `SyncWorker` korrekt implementieren | NuGet herunterladen, `ilspycmd`/Reflection an festem Commit |
| Existiert `MP.IsExecutingSyncCommand` in 0.6.0? | Kandidat, kein belastbarer Signaturfund | Doppelaufrufe vermeiden | Gegen 0.6.0-DLL und Entwickler-Doku suchen; bis dahin **nicht verwenden** |
| Exakte Netzwerkpakete, sichere Host-Join-Strategie? | nur zentrale Tick-/Serialisierungs-/Game-Dateien inspiziert, Handshake-Wiki veraltet | Reconnect, Server security | Network-Pakete, Handshake, CommandQueue und Snapshot-Code unter Commit lesen |
| Wie heißt der aktuelle MP-Branch/Commit? | Browserlinks `master`, keine git clones/commit hash in Erhebung | Reproduzierbarkeit | `git clone`, `git rev-parse HEAD`, Permalinks aktualisieren |
| Gilt `net472` in jedem 1.6-Setup? | Tutorials/MP NuGet net472, nicht jede Zielassembly lokal gemessen | SDK-Konfiguration | im installierten Game / Projekt und NuGet-Targetlisten prüfen |
| Welche Unity 1.6-Patchversion exakt? | Quellen aus mehreren Spielbuilds; nicht alle DLLs geprüft | native API und IL-Patching | Unity/Mono-Metadaten der lokalen 1.6-Binary lesen |
| Welche Mods/Frameworks 1.7-kompatibel? | allgemeines Ludeon-Ziel, viele individuell zu testen | fremde Abhängigkeiten | konkrete Mods/Tags/Issues und Ingame-Probe; keine pauschalen Aussagen |
| Wo landen 1.7 Linux logs und headless exits? | Primärquelle dokumentiert Flags, nicht den lokalen Pfad/die ausführbare Datei | CI-Integration | installierte 1.7-Binary starten, `-Verbose`, Pfad/Exitcode beobachten |
| Sind Contentpacks und Vulkan Shader auf Linux für den Mod korrekt? | benötigt tatsächliche Assets, Treiber, 1.7-Laufzeit | 1.7 UI/Asset-Releases | `-BakeModAssetPacks`, Vulkan-GUI-Test |
| Wie sind `GameComponent`/`CompProperties`-APIs in 1.7 exakt? | keine proprietären Assemblies vorhanden | Beispielcode-Kompilation | Reflection der lokalen Game DLL und Build gegen `net10.0` |
| Sind Cross-OS 1.6-MP-Sitzungen deterministisch? | keine zwei laufenden RimWorld-Clients hier | Linux+Windows-Freigabe | fixer Seed, gleiche Mods, 30min Cross-OS-Clienttests |

Die Forschungsarbeit hier ist eine belastbare **Startbasis**, aber weder ein vollständiges Audit jedes Netzwerkmethodepfads noch ein Laufzeittest. Offene Fragen beeinflussen spätere Sicherheits-/Kompatibilitätsaussagen und dürfen nicht durch Vermutungen ersetzt werden.
