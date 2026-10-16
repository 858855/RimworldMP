# Save/Join-Spielseite und X3 – Prüfung 2026-10-16

- **Branch:** `codex/state-hash`; Basis `origin/claude/save-join` @ `2e6b77d` plus X2-Cherry-Picks. Zu prüfenden Commit nach Abschluss mit `git log -1 --oneline` ermitteln.
- **Umgebung:** Linux, .NET SDK 10.0.402, lokale RimWorld `1.6.4871 rev598` (`Assembly-CSharp.dll`, Unity/Mono). Kein RimWorld-Prozess für diese Änderungen gestartet.
- **API-Abgleich:** Reflection gegen die lokale Assembly: `Rand.PushState(int)`, `PopState()`, `Value`, `Int`; `Autosaver.AutosaverTick()`; `Map.listerThings.AllThings`; `Thing`-ID/Def/Position/HitPoints/Stack; Pawn-Job/Health/Needs; Faction-Goodwill; Quest-ID/-State. IL der Rand-Getter: `Value` und `Int` erhöhen `Rand.iterations`, `Bool`/`Range`/`Chance` rufen sie auf. Temporäres Prüfprojekt unter `/tmp/rimworld-api-inspect/` (nicht versioniert).

| Ausgeführt | Ergebnis |
|---|---|
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project /tmp/rimworld-api-inspect/Inspect.csproj --nologo` mit `PushState(1192)` zweimal | Exit 0: `Rand.Int`, `Value`, `Range` identisch. Das ist ein API-Probeprogramm, kein Spieltest. |
| `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Erster Lauf nach Autosave-Hook: Exit 1 wegen Namespace-Auflösung. Nach `global::RimWorld.Autosaver` und Nullable-Korrektur: Exit 0, 0 Warnungen, 0 Fehler. |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Session.Tests/Session.Tests.csproj -c Release --nologo` | Exit 0, neun Gruppen: einschließlich Save/Join-Simulationen von Claude. |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Hosting.Tests/Hosting.Tests.csproj -c Release --nologo` | Exit 0, einschließlich laufendem Join über Listen-Host und Chunk-Kanal. |

**Quellcodeprüfung:** Save-Kommandos werden an der Grenze akzeptiert, Game-Kommandos ohne `SYNC-002` bleiben gesperrt. Snapshot-IDs sind in 16-Byte-Einheiten begrenzt, Uploads serialisiert. `ForJoin` beginnt nach Vanilla-Load mit `TicksGame - Boundary`; ein partielles erstes Hashfenster wird verworfen. `DesyncDetected` hält nur vorläufig an. Dies beweist keine Laufzeitfunktion der RimWorld-Adapter.

**Offen:** Echter Zwei-Client-Lauf für Tick-/Hash-Gleichheit, Save bei 60 000 Ticks, Join und Reconnect, Snapshot-Transfer im Spiel, Harmony-Rand-Scope, `PlayerSynced`, provozierter Desync und C2-Reparatur. `SYNC-002` und `DET-001` bleiben offen. Die Mod wurde nicht in den Benutzer-Modordner kopiert.
