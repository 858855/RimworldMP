# Review – Zusammenführung der vier Feature-Branches und Alpha 0.3.0-alpha.1

- **Datum:** 2026-10-17 (Sven, auf Wunsch des Benutzers: „merge all to main and test, make an alpha release“)
- **Ausgang:** `main` @ `060d2be` (Integration 2026-10-17, Phase 2 abgeschlossen). Alle vier Branches zweigen genau dort ab.
- **Ergebnis:** vier Merge-Commits auf `main` (`2ab54e1`, `6077751`, `8212fc4`, `c28718c`), danach der Release-Commit `98ab63f` (Version, Hash-Fix, Paket, Doku). Am 2026-10-17 auf Freigabe des Benutzers (Sven) nach `origin/main` gepusht, Tag `v0.3.0-alpha.1` ebenfalls; das GitHub-Release mit dem ZIP legt der Besitzer an.

## Branch-Matrix

| Branch | Inhalt | ADR | Merge | Konflikte | Prüfung vor dem Merge |
|---|---|---|---|---|---|
| `feat/permissions` @ `886e096` | Steuerungsrechte je übernommener Figur (SYNC-005 erster Schnitt, OWN-001 Anzeige) | 0026 | `2ab54e1`, ohne Konflikt | – | Selbsttest PASS (`controlDenied`/`controlAllowed`) |
| `feat/continue-save` @ `9c8fbea` | Spielstand aus der Lobby fortsetzen (SAVE-002 Coop), Code 105, `Restored` | 0027 | `6077751` | `CHANGELOG.md` (beide Abschnitte behalten) | Protocol/Lobby/Hosting-Tests, Selbsttest `-mpContinueSelfTest` PASS |
| `feat/factions-generation` @ `b31033e` | Fraktion, Ideologie, Siedlung, Karte je Platz (FACTION-005 Spielseite), Ideologie-Patch, hinter `-mpFactionStart` | 0028 | `8212fc4` | `CHANGELOG.md`; `WorldSelfTest.cs` (Schlüsselwahl: Fraktionstest-Schlüssel vor Continue-Host-Schlüssel) | Selbsttest `-mpFactionSelfTest` PASS |
| `feat/mod-api` @ `3685df2` | Sync-API v1 (`Multiplayer.Foundation.Api`), Opcode-Tabelle, Serialisierer, Hash-Beiträge | 0029 | `c28718c` | `CHANGELOG.md`, `CODEMAP.md`, `PROJECT_STATE.yaml`, `handoff/codex.md` (jeweils beide Einträge); `FoundationMod.cs` (`CaptureSyncMethod` + `ExecuteGameCommand` mit Sitzung); `WorldSelfTest.cs` (Rechte-Prüfung und API-Prüfung nacheinander); `GameCommandCodec.cs` (siehe unten) | Selbsttest PASS (API-Prüfungen) |
| `feat/sync-lockstep` @ `2a52807`, `feat/sync-commands` @ `ed5b33b` | zweite Gleichlauf-Linie (ADR 0024/0025) | – | **nicht gemergt** | nicht mechanisch zusammenführbar (`docs/reviews/2026-10-17-integration-sync.md`) | Entscheidung des Besitzers offen; Branches bleiben |

## Die eine inhaltliche Auflösung: `GameCommandCodec`

`feat/permissions` hatte in der if-Kette `Execute(command, localSession)` mit `Refused(...)` für Draft und Goto eingeführt; `feat/mod-api` hatte dieselbe Kette durch die Opcode-Tabelle ersetzt. Auflösung: Tabelle aus ADR 0029, Ausführer mit Signatur `(ScheduledCommand, Guid localSession)`, `Refused` vor `Run` in `ExecuteDraft` und `ExecuteGoto` (Forbid bleibt Kolonie-Aktion), `SyncMethods.Execute` nimmt die Sitzung an und prüft keine Rechte (ADR 0029: die Mod entscheidet über `MP.ExecutingIssuer`). `ApiSelfTest` ruft `Execute(…, Guid.Empty)`. Beide Selbsttests bestehen auf dem Merge-Stand.

## Was die Alpha enthält und was nicht

- Enthalten: Lobby (Steam/IP/dedizierter Server, Mod-Prüfung, Chat), gemeinsamer Start (Welt, Startort, Figurenwahl, Pause), Koordinator-Lockstep mit Tempoanpassung, Hash alle 60 Ticks, Speichern an der Tickgrenze, Beitritt/Resync, Befehle Draft/Forbid/Goto mit Steuerungsrechten, Fortsetzen aus der Lobby (Coop), Fraktionsgenerierung hinter Server-Sperre und `-mpFactionStart`, Sync-API.
- Nicht enthalten: alle weiteren Spieleraktionen (SYNC-004), Fraktions-Trennung (FACTION-002), `SyncField`/`SyncDelegate`, API-002, COMPAT-001, TLS/Auth (SEC-001), Pins im Spielstand.
- Risiko: Kein Zwei-Client-Test irgendeiner Funktion dieser Linie. Die Alpha ist zum Testen gedacht, nicht zum Spielen.

## Prüfung

Siehe `docs/verification/2026-10-17-alpha-release.md`: Mod-Build 0 Warnungen, neun Testläufe und Server-Build im .NET-8-Harness, Validator, `git diff --check`, drei Selbsttests im Spiel (Welt + Rechte + API; Fortsetzen; Fraktionen), Paket mit `package-mod.py`.

## Offen für den Besitzer

1. GitHub-Release zum Tag `v0.3.0-alpha.1` mit dem ZIP anlegen (kein `gh` auf Svens Rechner); `main` und Tag sind gepusht.
2. Entscheidung zu `feat/sync-lockstep`/`feat/sync-commands`.
3. Neue ADR-Nummernbereiche: Svens 0024–0029 sind aufgebraucht; Claude 0032–0039 und Codex 0040–0049 ebenso.
4. Zwei-Client-Spieltest der Alpha mit beiden PCs auf demselben RimWorld-Build.
