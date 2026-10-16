# Aufgabenteilung Claude / Codex

- **Stand:** 2026-10-16, auf Anweisung des Benutzers: Codex und Claude arbeiten parallel, in getrennten Branches, an Aufgaben, die möglichst wenig aufeinander aufbauen.
- **Verbindlich** nach `AGENTS.md` Regel 37. Bei Widerspruch gilt die neueste Anweisung des Benutzers.
- **Ausgangspunkt:** `main` @ `85af3a9`. Enthält Lobby, Steam, Mod-Prüfung, gemeinsame Einrichtung bis zur Landung, Lockstep-Koordinator und Spielmodus.

## Bahnen

| | **Claude** | **Codex** |
|---|---|---|
| Bereich | Koordinator und Protokoll, ohne RimWorld | Spielseite mit Verse und Harmony |
| Projekte | `Protocol`, `Network`, `Lobby`, `Session`, `Hosting`, `Server` und ihre `tests/*` | `src/Multiplayer.Foundation.RimWorld/`, `mod/`, Paketskripte |
| Prüfung | .NET-Testprojekte, Simulation | Mod-Build gegen lokale 1.6-Assemblies, API-Prüfung per Reflection (Regel 5); Spieltests derzeit nicht möglich |
| Branch-Präfix | `claude/` | `codex/` |
| Arbeitsverzeichnis | `/mnt/Arbeit/Git Projekte/RimworldMP-claude` (Worktree) | `/mnt/Arbeit/Git Projekte/RimworldMP` |
| ADR-Nummern | 0032–0039 | 0040–0049 |
| Nachrichtencodes | 71–79 | 80–95 |

Svens Bereich bleibt, falls er weiterarbeitet: Branches `feat/lobby-hardening` und `feat/mod-check-setup-ui` bleiben bestehen. Seine fortlaufenden Nummern sind ADR 0024–0029 und Nachrichtencodes 37–63.

## Aufgaben

Je Aufgabe ein eigener Branch von `main`. Die Reihenfolge innerhalb einer Bahn ist eine Empfehlung.

**Claude**

| # | Aufgabe | Branch | Inhalt | braucht von Codex |
|---|---|---|---|---|
| C1 | `SAVE-001`, `JOIN-001` (Koordinator) | `claude/save-join` | Speichern als geplanter Befehl am gemeinsamen Tick; beim Beitritt mitten im Spiel: Snapshot, Befehle puffern, Aufholen; Reconnect über den `PlayerKey` | nichts |
| C2 | `DESYNC-003` (Koordinator) | `claude/resync` | Ablauf der Reparatur: Pause, Snapshot des Hosts über den Chunk-Kanal an die Abweichenden, Neuladen bestätigen, fortsetzen | nichts |
| C3 | `VOTE-001` (Koordinator) | `claude/vote` | Abstimmung nach ADR 0031: Pause während der Abstimmung, keine Frist, Host beendet vorzeitig, Nichtabstimmende enthalten, Gleichstand entscheidet der Host; Ergebnis als geplanter Befehl | nichts |
| C4 | `SEC-001` | `claude/ip-password` | Lobby-Passwort für IP-Lobbys | nichts |

**Codex**

| # | Aufgabe | Branch | Inhalt | braucht von Claude |
|---|---|---|---|---|
| X1 | `VER-001` (Rest) | `codex/ver-tickpath` | Liste der Mehrfach-Thread-Systeme im 1.6-Tickpfad, geprüft an den lokalen Assemblies | nichts |
| X2 | `SYNC-001`, `SYNC-003` (Spielseite) | `codex/tick-driver` | Tick-Treiber statt `SessionPauseTickPatch` nach `LockstepClient`; Pause und Geschwindigkeit als `ControlMessage`; „Warte auf …“-Anzeige | nichts; `LockstepClient` ist auf `main` |
| X3 | `DESYNC-001` (Spielseite) und Rand-Neusaat | `codex/state-hash` | Zustands-Hash nach Plan §7.9 alle 60 Ticks; `Rand`-Zustand je Tick und je Befehl nach Plan §7.4 | nichts |
| X4 | `SYNC-002` | `codex/command-pipeline` | Befehlserfassung und Serializer-Registry; die Befehlsdaten sind für den Koordinator undurchsichtig | nichts |
| X5 | `MODE-001` (Spielseite) | `codex/mode-ui` | Modus-Auswahl im Lobby-Fenster, Anzeige in der Liste, Modus in `MultiplayerSession` speichern und beim Laden übernehmen | nichts |
| X6 | `OWN-001` | `codex/ownership` | Besitz-Komponente und Besitzeranzeige | nichts |

Danach folgen auf beiden Seiten die abhängigen Aufgaben: `SYNC-004`, `SYNC-005`, `SYNC-007`, die Spielseite von `VOTE-001` und `FACTION-*`. Sie werden nach Abschluss der obigen neu verteilt.

**Nicht verteilt:** `TOOL-001`, `DET-001`, `TEST-*`. Sie brauchen laufende RimWorld-Instanzen und Spieltests, die laut Benutzer derzeit nicht möglich sind.

## Regeln für die Zusammenarbeit

1. **Nur eigene Bahn ändern.**
   - Braucht Claude etwas auf der Spielseite oder Codex etwas im Koordinator, kommt ein Eintrag unter „Wünsche an die andere Bahn“ in die eigene Übergabedatei, keine Änderung im fremden Bereich.
   - Ausnahme: neue Nachrichtencodes und Nutzdaten darf Codex im eigenen Nummernbereich in `Protocol` ergänzen, wenn die Spielseite sie braucht.
2. **Eigene Nummernbereiche** für ADRs und Nachrichtencodes (siehe Tabelle). Die Wire-Version bleibt bis zum ersten Release 3.
3. **Gemeinsame Dateien konfliktarm pflegen:**
   - `docs/ai/TASKS.yaml`: nur Einträge mit eigenem `owner` ändern.
   - Übergabe in der eigenen Datei: `docs/ai/handoff/claude.md` bzw. `docs/ai/handoff/codex.md`. `docs/ai/SESSION_HANDOFF.md` ist nur noch der Verweis darauf.
   - `docs/ai/PROJECT_STATE.yaml` wird erst beim Merge nach `main` angepasst.
   - `CHANGELOG.md`: eigener Abschnitt je Branch.
4. **Vor jedem Merge nach `main`:**
   - auf den aktuellen `main` bringen
   - Mod-Build und alle Testprojekte laufen lassen
   - Validator `python3 scripts/validate_project_state.py`
   - ausdrückliche Freigabe des Benutzers (Regel 32)
5. **Kein Force-Push**; keine fremden Branches löschen oder umschreiben.
6. **Status prüfen:** Vor Beginn einer Aufgabe auf `origin` nachsehen, ob die andere Bahn sie nicht schon begonnen hat (`git ls-remote --heads origin`).
