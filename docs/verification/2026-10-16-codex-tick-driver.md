# X2 Tick-Treiber – Prüfung 2026-10-16

- **Branch/Basis:** `codex/tick-driver` von `main`/`e4501df`; Commit-ID vor Abschluss dynamisch mit `git log -1` lesen.
- **Umgebung:** Linux, .NET SDK 10.0.402 (`/home/fabiok/.local/share/dotnet-rimworldmp/dotnet`), lokale RimWorld-Assemblies 1.6.4871 rev598 unter `RimWorldLinux_Data/Managed`. RimWorld-Spielprozess für X2 nicht gestartet; frühere Runtime rev600 ist keine neue Prüfung.
- **Lokaler API-Abgleich:** Reflection der installierten `Assembly-CSharp.dll`: `Verse.TickManager.TickManagerUpdate()`, `DoSingleTick()`, `TogglePaused()`, `TicksGame`, `Paused`, `CurTimeSpeed`; `TimeSpeed` mit `Paused`, `Normal`, `Fast`, `Superfast`, `Ultrafast`; `Map.uniqueID`, `MapPawns.FreeColonists`. `Pawn.Position` wird aus `Thing` geerbt und kompiliert. Harmony-Hooks wurden nicht im laufenden Spiel ausgeführt.

| Tatsächlich ausgeführt | Ergebnis |
|---|---|
| `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Exit 0, RimWorld-1.6-Release-Build, 0 Warnungen, 0 Fehler; sechs eigene Assemblies in `dist/` einschließlich `Multiplayer.Foundation.Session.dll` |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Session.Tests/Session.Tests.csproj -c Release --nologo` | Exit 0; Payload/Limit, Koordinator-Pause/Tempo/Flusskontrolle, Desync, spielunabhängige Client-Guards, sechs Drei-Spiel-Simulationen und Abweichungs-Stopp bestanden |
| `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Client.Tests/Client.Tests.csproj -c Release --nologo` | Exit 0; Client-Pumpe, Heartbeat, Disconnect bestanden |
| `python3 scripts/package-mod.py RimWorldMultiplayerFoundation-x2-tick-driver-rw1.6.zip` | Exit 0; 10 Einträge, `Multiplayer.Foundation.Session.dll` enthalten, ZIP-Integrität geprüft. Das nur für diese Prüfung erzeugte ZIP wurde danach aus `releases/` entfernt. |

**Codeprüfung:** `SessionPauseTickPatch` ruft vor jedem Vanilla-Tick `TakeCommandsAt(c)` und `MayRunTick(c)` auf. Auch ohne Tick werden Befehle im Frame und nach Nachrichteneingang abgeholt. `SessionPauseSpeedPatch`/`TogglePaused` schicken `SessionControl`; der interne Setzpfad wird nicht erneut gesendet. Bei Horizontende wird die Vanilla-Zeit pausiert; der `DoSingleTick`-Hook bleibt als harte Sperre. Diese Aussagen beruhen auf Quellcode und Build, nicht auf einem Spieltest.

**Offen:** `IN_GAME_GETESTET` und `MP_ZWEI_CLIENTS_GETESTET` für X2. Zu prüfen sind Landung → Resume → Tick 60/120, Pausieren während laufender Simulation, 1×/2×/3× durch Host, abgewiesene Gast-Geschwindigkeit, langsamer Client, Disconnect, zwei gleiche Hashes und Spielbefehl ohne Ausführer. Der Kernhash vergleicht bislang nur Tick, Karten-IDs und Kolonisten-IDs/-Positionen; er ersetzt X3 nicht. `SYNC-002` fehlt, daher stoppen echte Spielbefehle derzeit bewusst. Kein Desync-Spielbefund möglich.
