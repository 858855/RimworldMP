# X4 / SYNC-002: erster Spielbefehl, Prüfung 2026-10-16

- **Branch/Basis:** `codex/command-pipeline` von `codex/resync-game` @ `0bde512`; dieser Basisbranch ist noch nicht in `main`. Umgebung: Linux, RimWorld 1.6.4871 rev598, .NET SDK 10.0.402.
- **API-Prüfung:** Reflection gegen lokale `Assembly-CSharp.dll`: `RimWorld.Pawn_DraftController.Drafted` besitzt einen öffentlichen Setter; das `pawn`-Feld ist vorhanden. `Verse.AI.Pawn_JobTracker.TryTakeOrderedJob(Job, JobTag?, bool)` und `Verse.Designator.DesignateSingleCell(IntVec3)` sind vorhanden, aber in diesem Schritt nicht gepatcht. Die Harmony-Signatur des Draft-Setters wurde mit dem Mod-Build geprüft.

| Ausgeführt | Ergebnis |
|---|---|
| `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Erster Lauf Exit 1: `GUIUtility.guiDepth` fehlt in der lokalen Unity-Assembly. Auf `Event.current` als GUI-Kontextprüfung geändert; zweiter Lauf Exit 0, 0 Warnungen, 0 Fehler. |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project /tmp/rimworld-api-inspect/Inspect.csproj --no-restore` | Exit 0: 11-Byte-Draft-Payload mit Map-ID 17, Pawn-ID 12345 und `true` roundtrip-geprüft; abgeschnittene Daten, falsche Version, ungültiges Bool und Anhänge abgelehnt. Temporäres Prüfprogramm ist nicht versioniert. |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Session.Tests/Session.Tests.csproj -c Release --nologo` | Exit 0: Lockstep-/Save-/Join-/Resync-/Vote-Simulationen bestanden; testet nicht den neuen Verse-Handler. |
| `python3 scripts/validate_project_state.py` | Exit 0: 22 Pflichtdateien, 61 Aufgaben und Referenzen gültig. |
| `git diff --check` | Exit 0. |

**Grenze:** Kein laufender RimWorld-Client und keine zwei Spiele mit Draft-Toggle getestet. Der Setter-Prefix ist nur für GUI-Aktionen einer aktiven Sitzung vorgesehen; Harmony-Reihenfolge mit anderen Mods und tatsächliche Erfassung im Spiel sind offen. Weitere Aktionen und Referenztypen aus Plan §7.3 fehlen.
