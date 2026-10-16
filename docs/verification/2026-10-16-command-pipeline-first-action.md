# X4 / SYNC-002: erster Spielbefehl, Prüfung 2026-10-16

- **Branch/Basis:** `codex/command-pipeline` nach Rebase auf `main` @ `b4255c6`. Umgebung: Linux, RimWorld 1.6.4871 rev598, .NET SDK 10.0.402.
- **API-Prüfung:** Reflection gegen lokale `Assembly-CSharp.dll`: `RimWorld.Pawn_DraftController.Drafted` besitzt einen öffentlichen Setter; das `pawn`-Feld ist vorhanden. `Verse.AI.Pawn_JobTracker.TryTakeOrderedJob(Job, JobTag?, bool)` und `Verse.Designator.DesignateSingleCell(IntVec3)` sind vorhanden, aber in diesem Schritt nicht gepatcht. Die Harmony-Signatur des Draft-Setters wurde mit dem Mod-Build geprüft.

| Ausgeführt | Ergebnis |
|---|---|
| `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Erster Lauf Exit 1: `GUIUtility.guiDepth` fehlt in der lokalen Unity-Assembly. Auf `Event.current` als GUI-Kontextprüfung geändert; zweiter Lauf Exit 0, 0 Warnungen, 0 Fehler. |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project /tmp/rimworld-api-inspect/Inspect.csproj --no-restore` | Exit 0: 11-Byte-Draft-Payload mit Map-ID 17, Pawn-ID 12345 und `true` roundtrip-geprüft; abgeschnittene Daten, falsche Version, ungültiges Bool und Anhänge abgelehnt. Temporäres Prüfprogramm ist nicht versioniert. |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Session.Tests/Session.Tests.csproj -c Release --nologo` | Exit 0: Lockstep-/Save-/Join-/Resync-/Vote-Simulationen bestanden; testet nicht den neuen Verse-Handler. |
| `python3 scripts/validate_project_state.py` | Exit 0: 22 Pflichtdateien, 61 Aufgaben und Referenzen gültig. |
| `git diff --check` | Exit 0. |

**Grenze:** Kein laufender RimWorld-Client und keine zwei Spiele mit Draft-Toggle getestet. Der Setter-Prefix ist nur für GUI-Aktionen einer aktiven Sitzung vorgesehen; Harmony-Reihenfolge mit anderen Mods und tatsächliche Erfassung im Spiel sind offen. Weitere Aktionen und Referenztypen aus Plan §7.3 fehlen.

## Erweiterung: Forbid/Unforbid

- **Lokale API-Prüfung:** Reflection gegen `Assembly-CSharp.dll` bestätigt `ForbidUtility.SetForbidden(Thing t, bool value, bool warnOnFail)` als öffentliche Methode. Der Hook verwendet diese Signatur; ob alle UI-Wege sie benutzen, ist ohne Spielstart ungeklärt.
- **Build:** `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` nach der Erweiterung, Exit 0; 0 Warnungen, 0 Fehler.
- **Codec-Prüfung:** Temporäres, nicht versioniertes Reflection-Programm `/tmp/rimworld-api-inspect/Inspect.csproj` mit `dotnet run --project /tmp/rimworld-api-inspect/Inspect.csproj --no-restore`, Exit 0. 11-Byte-Payload mit Karten-ID 17, Thing-ID 12345 und `true` roundtrip-geprüft; abgeschnittenes Paket und unbekannter Opcode abgewiesen.
- **Grenze:** Kein RimWorld-Prozess gestartet. Kein tatsächlicher Forbid-Klick und keine Zwei-Client-Synchronisierung geprüft. SYNC-002 bleibt `in_progress`.

## Gemeinsame Referenzkodierung

- `GameReferenceCodec` kodiert für beide Befehle Karten- und Thing-ID und löst die Referenz erst bei Ausführung gegen die aktuell geladene Karte auf. Fehlende Ziele bleiben ein protokollierter No-op; Welt-Pawns und weitere Referenzarten sind noch nicht unterstützt.
- Nach der Umstellung erneut `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh`: Exit 0, 0 Warnungen, 0 Fehler.
- Temporäres Reflection-Programm erneut ausgeführt: Exit 0 für Draft- und Forbid-Roundtrip (je 11 Byte) sowie abgeschnittene/ungültige Forbid-Payloads. Dies prüft den Codec, nicht den Spielzustand.

## Einfacher Goto-Auftrag

- **Lokale API-Prüfung:** `Pawn_JobTracker.TryTakeOrderedJob(Job job, JobTag? tag, bool requestQueueing)` ist öffentlich; privates `pawn`-Feld per Reflection bestätigt. `JobDefOf.Goto`, `JobMaker.MakeJob(JobDef, LocalTargetInfo)`, `LocalTargetInfo(IntVec3)` und `IntVec3`-Koordinaten sind in der installierten 1.6-Assembly vorhanden.
- **Build:** Erster `build-mod.sh`-Lauf Exit 1, weil `pawn` privat ist und die Nullable-Harmony-Abfrage `accepted` nicht sicher initialisierte. Nach `AccessTools.FieldRefAccess` und expliziter Instanzprüfung zweiter Lauf Exit 0, 0 Warnungen, 0 Fehler.
- **Codec:** Temporäres Reflection-Programm `dotnet run --project /tmp/rimworld-api-inspect/Inspect.csproj --no-restore`: erster Lauf Exit 134 wegen `Enum.IsDefined` mit `int` statt dem lokalen `byte`-Basistyp von `JobTag`. Nach Korrektur Exit 0: Goto-Payload mit Zelle (3,0,4), `DraftedOrder` und Queue-Flag roundtrip-geprüft; abgeschnittene Daten und ungültiger Tag abgewiesen.
- **Grenze:** Keine RimWorld-Instanz gestartet. Ob die Benutzeroberfläche den Hook trifft, der Auftrag wirklich ausgeführt wird und die Pfadsuche auf zwei Clients gleich bleibt, ist offen. Andere geordnete UI-Jobs werden bis zu ihrer Kodierung abgewiesen.
