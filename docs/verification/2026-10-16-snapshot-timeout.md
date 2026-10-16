# Zeitgrenze für nie geladene Snapshots (ADR 0036) – Prüfung 2026-10-16

- **Branch/Basis:** `claude/snapshot-timeout` von `main` @ `ff2f178`. Bahn Claude, C5.
- **Umgebung:** Linux, .NET SDK 10.0.402 / Runtime 10.0.13, lokale RimWorld-Assemblies `1.6.4871` (rev598).

| Befehl/Prüfung | Ergebnis |
|---|---|
| `DOTNET_BIN=…/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen, 0 Fehler |
| `DOTNET_BIN=…/dotnet bash scripts/build-server.sh` | Exit 0 |
| `Session.Tests` | Exit 0; 14 Gruppen, neu siehe unten |
| `Hosting.Tests` | Exit 0; 27 Gruppen, neu siehe unten |
| übrige sechs Testprojekte | Exit 0; unverändert |
| `python3 scripts/validate_project_state.py` | Exit 0 |

Neu in `Session.Tests`, Regeln:
- Vor Ablauf passiert nichts.
- Bei Ablauf erfahren alle fünf Teilnehmer, wer aufgegeben ist; nur der Säumige wird entfernt und genau einmal gemeldet; ein rechtzeitig Ladender bleibt.
- Eine hängende Reparatur wartet bis zur Grenze. Danach ist der Säumige entfernt, die System-Pause aufgehoben, und `DesyncResolved` geht an alle.

Neu in `Hosting.Tests`, Ende zu Ende über `ListenHost` mit 5 s Frist:
- Ein Steam-ähnlicher Spieler tritt dem laufenden Spiel bei und bestätigt nie.
- Er erhält `ResyncGaveUp` mit sich selbst und `LeaveLobby`; der Host sieht die Lobby ohne ihn.
- Seine Verbindung bleibt bestehen, und ein erneuter Beitritt gelingt.

Klassifizierung: `BUILD_ERFOLGREICH`, Koordinator automatisiert getestet. Die Anzeige im Spiel fehlt (Codex). Im Spiel `NICHT_GETESTET`.
