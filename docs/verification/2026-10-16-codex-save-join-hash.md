# Save/Join-Spielseite und X3 – Prüfung 2026-10-16

- **Branch:** `codex/resync-game`; Basis `origin/claude/resync` @ `bd4c98e` einschließlich C1 plus X2-/X3-Cherry-Picks. Zu prüfenden Commit nach Abschluss mit `git log -1 --oneline` ermitteln.
- **Umgebung:** Linux, .NET SDK 10.0.402, lokale RimWorld `1.6.4871 rev598` (`Assembly-CSharp.dll`, Unity/Mono). Kein RimWorld-Prozess für diese Änderungen gestartet.
- **API-Abgleich:** Reflection gegen die lokale Assembly: `Rand.PushState(int)`, `PopState()`, `Value`, `Int`; `Autosaver.AutosaverTick()`; `Map.listerThings.AllThings`; `Thing`-ID/Def/Position/HitPoints/Stack; Pawn-Job/Health/Needs; Faction-Goodwill; Quest-ID/-State. IL der Rand-Getter: `Value` und `Int` erhöhen `Rand.iterations`, `Bool`/`Range`/`Chance` rufen sie auf. Temporäres Prüfprojekt unter `/tmp/rimworld-api-inspect/` (nicht versioniert).

| Ausgeführt | Ergebnis |
|---|---|
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project /tmp/rimworld-api-inspect/Inspect.csproj --nologo` mit `PushState(1192)` zweimal | Exit 0: `Rand.Int`, `Value`, `Range` identisch. Das ist ein API-Probeprogramm, kein Spieltest. |
| `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Erster Lauf nach Autosave-Hook: Exit 1 wegen Namespace-Auflösung. Nach `global::RimWorld.Autosaver` und Nullable-Korrektur: Exit 0, 0 Warnungen, 0 Fehler. |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Session.Tests/Session.Tests.csproj -c Release --nologo` | Exit 0, neun Gruppen: einschließlich Save/Join-Simulationen von Claude. |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Hosting.Tests/Hosting.Tests.csproj -c Release --nologo` | Exit 0, einschließlich laufendem Join über Listen-Host und Chunk-Kanal. |
| `Session.Tests` und `Hosting.Tests` erneut auf C2-Basis | Beide Exit 0; C2-Tests zu Snapshot nach Desync, `SnapshotLoaded`, Wiederaktivierung und automatischem Fortsetzen bestanden. |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Client.Tests/Client.Tests.csproj -c Release --nologo` | Exit 0, drei Client-Pump-/Heartbeat-/Disconnect-Gruppen. |
| `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` nach C2-Anpassung | Exit 0, 0 Warnungen, 0 Fehler. `DesyncDetected` betrifft nur gelistete Session-IDs; `SnapshotLoadedMessage()` und Statusmeldungen kompilieren. |
| `python3 scripts/validate_project_state.py` und `git diff --check` | Beide Exit 0; 22 Pflichtdateien, 61 Aufgaben und Referenzen gültig. |

**Quellcodeprüfung:** Save-Kommandos werden an der Grenze akzeptiert, Game-Kommandos ohne `SYNC-002` bleiben gesperrt. Snapshot-IDs sind in 16-Byte-Einheiten begrenzt, Uploads serialisiert. `ForJoin` beginnt nach Vanilla-Load mit `TicksGame - Boundary`; ein partielles erstes Hashfenster wird verworfen. `SnapshotLoaded` folgt nach dem Laden. `DesyncDetected` hält nur betroffene Clients bis zum Snapshot an. Dies beweist keine Laufzeitfunktion der RimWorld-Adapter.

**Offen:** Echter Zwei-Client-Lauf für Tick-/Hash-Gleichheit, Save bei 60 000 Ticks, Join und Reconnect, Snapshot-Transfer im Spiel, Harmony-Rand-Scope, `PlayerSynced`, provozierter Desync und C2-Reparatur. `SYNC-002` und `DET-001` bleiben offen. Die Mod wurde nicht in den Benutzer-Modordner kopiert.
