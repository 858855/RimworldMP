# RimWorld Multiplayer Foundation

**Projektziel:** Version 0.1 soll einen echten RimWorld-1.6-Client mit Lobby-Oberfläche, eine getrennte TCP-Netzwerkbibliothek und einen Linux-Dedicated-Server verbinden. Gameplay-Synchronisation gehört zu späteren Versionen. **Aktueller Stand:** Wissensbasis und Governance sind vorhanden; produktiver Multiplayer-Code ist noch nicht implementiert. Maßgeblich ist `docs/ai/PROJECT_STATE.yaml`.

Einstieg: `docs/SPEC.md`, `docs/ai/CONTEXT_INDEX.md`, `docs/ai/TASKS.yaml`. Build- und Teststand: `docs/build.md`, `docs/testing.md`. Die folgende ursprüngliche Forschungs-README bleibt als Herkunft und technische Referenz erhalten.

---

# RimWorld Codex Knowledge Base — Forschungsstand 2026-10-14

**Zweck:** Nachprüfbare Arbeitsgrundlage für Codex zur späteren Entwicklung, Portierung, Optimierung und Multiplayer-Absicherung von RimWorld-Mods. **Dies ist noch kein veröffentlichungsfertiger Mod.**

## Zusammenfassung und Status

- **BESTÄTIGT** RimWorld **1.6** ist am Recherchedatum der Hauptzweig. RimWorld **1.7** läuft als **unstable Beta** auf der hauseigenen **Waylith-Engine** und nicht mehr auf Unity. Offizielle Ankündigung vom 12.10.2026: https://ludeon.com/blog/2026/10/announcing-the-waylith-engine-and-update-1-7/
- **BESTÄTIGT** Waylith nutzt **CoreCLR / .NET 10**, **Vulkan**, `Waylith.dll`, `UnityStubs.dll` als Unity-API-Emulation und einen weitgehend erhaltenen `Assembly-CSharp.dll`-Spielcode. Hersteller-Primärquelle: https://docs.google.com/document/d/e/2PACX-1vSykw5_DYaaw42nEznI9zmBZVT_Vgg2bxYq_v2XWvAsHC3QSKFuDK2OyiQ5WC9jvVRUcrjpFt7xLLF5/pub
- **BESTÄTIGT** `rwmt/Multiplayer` deklariert **nur 1.6** in seiner `About.xml`, verlangt **Prepatcher**, und gibt **keine dokumentierte 1.7-Unterstützung** an: https://github.com/rwmt/Multiplayer/blob/master/About/About.xml
- **BESTÄTIGT** Ludeons Modder Primer nennt **Prepatcher und seine abhängigen Mods** ausdrücklich als Ausnahme von der üblichen 1.6→1.7-Binärkompatibilität. **WICHTIG:** Eine funktionierende 1.7-Multiplayer-Portierung kann gegenwärtig nicht behauptet werden.
- **BESTÄTIGT** Für 1.6 gibt es `rwmt/MultiplayerAPI` und das NuGet-Paket `RimWorld.MultiplayerAPI` **0.6.0** mit `net472`-Ziel: https://www.nuget.org/packages/RimWorld.MultiplayerAPI/
- **BESTÄTIGT** Multiplayer nutzt grundsätzlich **deterministischen Lockstep**: Eingaben/Kommandoereignisse werden synchronisiert, die Simulation läuft lokal. Dateibasierte aktuelle Quellen: `Source/Client/Patches/TickPatch.cs`, `Source/Client/AsyncTime/AsyncTimeComp.cs` und `Source/Common/Syncing/SyncSerialization.cs` im `rwmt/Multiplayer`-Repo.

**WICHTIGE EINSCHRÄNKUNG:** Diese Recherche verifiziert online einsehbare Dokumente und ausgewählte Quellcodedateien, aber **keine lokale RimWorld-Installation und keine proprietären Game-Assemblies**. Die C#-Beispiele sind bewusst als **nicht kompiliert/nicht laufzeitgetestet** gekennzeichnet; einige APIs müssen mit der lokal installierten Zielversion gegengeprüft werden. GitHub-`master`-Links zeigen bewegliche Branches; Commit-Pinning steht unter `references/open-questions.md`.

## Navigationspfade

| Für Codex | Datei |
|---|---|
| Verbindliche Regeln | `AGENTS.md` |
| Projektentscheidungen | `codex/architecture-guidelines.md` |
| Engine und Migration | `research/02-engine-and-future-changes.md` |
| Mod-Struktur | `research/03-modding-fundamentals.md` |
| Lockstep-Analyse | `research/07-multiplayer-architecture.md` |
| Multiplayer-API | `research/08-multiplayer-api.md` |
| Determinismus | `research/09-determinism.md` |
| Desync-Diagnose | `research/11-desync-debugging.md` |
| Linux, Build, Tests | `research/13-linux-development.md`, `research/15-build-and-testing.md` |
| Quelle vs. Annahme | `references/verified-apis.md`, `references/open-questions.md` |
| Zusammenfassung | `GESAMTBERICHT.md` |

## Forschungsdisziplin

Jede technische Aussage fällt in **BESTÄTIGT, WAHRSCHEINLICH, UNGEKLÄRT, VERALTET** oder **SPEKULATIV**. BESTÄTIGT bedeutet: Der konkrete Beleg bestätigt **den genannten Geltungsbereich**, nicht automatisch alle RimWorld- und Multiplayer-Versionen. Widersprüchliche Versionsangaben werden notiert statt weginterpretiert.

## Priorität für neue Entwicklungen

1. Für **jetzt nutzbaren Multiplayer** zuerst **RimWorld 1.6** als Ziel wählen und exakte lokal installierte Revisionsnummer prüfen.
2. **1.7/Waylith** als separaten experimentellen Buildpfad gestalten; keinerlei MP-Zusage, bis sämtliche Abhängigkeiten und Multiplayer-Tests nachgewiesen sind.
3. Vanilla-Def/XML bevorzugen, dann `ThingComp`/`GameComponent` etc., Harmony nur als letzten Schritt.
4. Trennung zwischen **lokaler UI** und **deterministisch synchronisierter Weltmutation** ist Architekturvorgabe.
5. Jeder Merge braucht Nachweis des Builds **und** entsprechender Runtime-/Multiplayer-Tests. Ein Build ist keine Multiplayer-Verifikation.

## Aktualisierungsprotokoll

- **2026-10-14:** Erstfassung auf Basis offizieller Waylith-Ankündigung, Ludeons Modder Primer, `rwmt`-Repos, Multiplayer-API-Wiki, NuGet und Harmony-Dokumentation.
- Bei jedem Update diese URLs erneut prüfen und Versionen/Kompatibilitätsstatus explizit aktualisieren.
