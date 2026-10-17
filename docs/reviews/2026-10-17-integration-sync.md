# Review – Integration aller Branches nach `main` (Phase 1), 2026-10-17

- **Auftrag:** Besitzer (Fabio), 2026-10-17: „RimworldMP vollständig integrieren und aufräumen“. Ausgeführt von Sven mit Claude auf Svens Windows-Rechner, nicht auf dem Linux-Rechner des Besitzers. Dadurch weichen Pfade, SDK und die erreichbaren Branches vom Auftragstext ab (siehe Abweichungen).
- **Integrationsbranch:** `integration/sync-2026-10-17` im Worktree `E:\RimworldMP-integration`, abgezweigt von `origin/main` @ `85e02f9` (unverändert seit Auftragserteilung, am Ende erneut geprüft).
- **Freigabe:** Am 2026-10-17 nach dem Freigabe-Stopp vom Besitzer erteilt (von Sven übermittelt: „Der Owner hat Phase 2 freigegeben: main per Fast-Forward auf ffa46c9, Push, Archiv-Tags und Loeschungen laut Loeschliste. feat/sync-lockstep und feat/sync-commands behalten.“). Ablauf und Ergebnis unter „Phase 2“ am Ende.
- **Prüfung:** `docs/verification/2026-10-17-integration-sync.md`.

## Abweichungen von der Ausgangslage des Auftrags

| Auftrag | Hier | Folge |
|---|---|---|
| Repository `/mnt/Arbeit/Git Projekte/RimworldMP`, Codex-Worktree | `E:\RimworldMP` (Branch `feat/mod-check-setup-ui`, 8 Commits vor `origin`), Worktree `E:\RimworldMP-sync` (`feat/sync-commands`); beide sauber | Integrations-Worktree `E:\RimworldMP-integration` zusätzlich angelegt |
| `claude/continuous-sync` = `da2e4b7` mit `a779f21`, `da2e4b7` (nur lokal beim Besitzer) | Nicht vorhanden; nur `origin/codex/continuous-sync` = `f1ebe66` | 1.1 nur für `de63452`, `1de7bb3`, `f1ebe66` möglich; die Koordinator-Seite der Verbindungsqualität (Ping je Spieler, Codes 102/103) und ADR 0039 fehlen hier |
| Lokale Branches `claude/faction-sites`, `codex/command-pipeline`, `codex/faction-setup`, `codex/faction-storytellers`, `codex/faction-ui`, `codex/manual-test-candidate`, `codex/mode-ui`, `codex/resync-game`, `codex/save-join-game`, `codex/state-hash`, `codex/tick-driver`, `codex/ver-tickpath` | Nur beim Besitzer; hier nur die Remote-Branches | Matrix unten umfasst alle Remote-Branches und Svens lokale Branches; die lokalen Branches des Besitzers muss er selbst gegen `origin/main` prüfen (die meisten sind laut Historie gemergt: `codex/faction-ui`, `codex/manual-test-candidate`, `codex/resync-game`, `codex/tick-driver` sind Vorfahren bzw. patch-identisch) |
| Im Auftrag nicht genannt | `origin/feat/sync-lockstep` (`2a52807`) und `origin/feat/sync-commands` (`ed5b33b`), am 2026-10-17 von Sven gepusht | Zweite Gleichlauf-Linie; siehe „Fachliche Entscheidung“ |
| .NET-SDK `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet` (10.0.402) | Nur SDK 8.0.403 | Tests im lokalen .NET-8-Harness, nicht eingecheckt (wie `docs/verification/2026-10-15-lobby-hardening.md`); Server als `net8.0`-Harness gebaut |
| RimWorld-Assemblies Linux rev598 | Windows `E:/SteamLibrary/.../RimWorldWin64_Data/Managed`, `Version.txt` 1.6.4871 rev590 | Mod-Build gegen rev590 |
| `ADR 0039 ergänzen` | 0039 nicht auf `origin` | Nachtrag in ADR 0049; beim Zusammenführen mit `claude/continuous-sync` nach 0039 übertragen |

## 1.1 Sync-Arbeit

`origin/main` stand noch auf `85e02f9`, daher Variante (a): Der Integrationsbranch wurde per Fast-Forward auf `f1ebe66` gesetzt; `de63452`, `1de7bb3` und `f1ebe66` behalten ihre SHAs. `f1ebe66` enthält nur die Zeile „Push freigegeben, kein Merge autorisiert“ in `docs/ai/handoff/codex.md`; sie wurde im Doku-Commit dieser Integration durch den aktuellen Stand ersetzt, kein überholter Status bleibt stehen. `a779f21`/`da2e4b7` konnten nicht übernommen werden (nicht vorhanden).

## 1.2 Fachliche Entscheidung „Tempo bei schlechter Verbindung“

Umgesetzt als **SYNC-010** (Commit `85a10d4`), Koordinator-seitig in `LockstepSession`, mit `PaceNotice` (Code 104) und Tests. Schwellen: herunter nach 1,5 s gehaltenem Horizont innerhalb von 5 s; hinauf eine Stufe nach 10 s ohne Stall und 10 s Haltezeit; Untergrenze 1×; nie über das vom Host gewählte Tempo; Rückkehr automatisch; Grund im Overlay. Eingang sind nur die tatsächlichen Wartezeiten am Horizont; Ping je Spieler, Stille und Steam-Verlust (`PeerQuality`) liegen in `a779f21` beim Besitzer und sind als Ergänzung in ADR 0049 (Nachtrag) und TASKS (SYNC-010) als offen geführt. Die lokale Tempoobergrenze aus `de63452` wurde entfernt (wie in `da2e4b7`, dessen übrige Inhalte hier nicht vorliegen). Für TCP wird kein Paketverlust behauptet. Nebenbefund behoben: Die Clock-Basis fiel beim gehaltenen Horizont unter den Horizont und fror die Zeit nach dem Aufholen für Sekunden ein. Kein Spieltest.

## 1.3 Branch-Matrix

Werkzeuge: `git merge-base --is-ancestor`, `git cherry -v origin/main <branch>`, `git range-diff`, Patch-Vergleich (`git diff <c>^ <c>`), gezielte Dateivergleiche. Alle Angaben gegen `origin/main` @ `85e02f9`.

| Branch | Kopf | Einzigartige Änderung | Schon auf `main`? (Beleg) | Übernommen / ausgelassen |
|---|---|---|---|---|
| `origin/codex/continuous-sync` | `f1ebe66` | SYNC-009: Hot-Rejoin, `ConnectionQuality`, lokale Tempoobergrenze (`de63452`, `1de7bb3`), Doku (`f1ebe66`) | Nein (`cherry`: 3 ×`+`) | **Übernommen** per Fast-Forward, SHAs erhalten; lokale Tempoobergrenze danach durch SYNC-010 ersetzt (`85a10d4`) |
| `origin/codex/tick-driver` | `19b3bf7` | `0ad1f31` Tick-Treiber, `19b3bf7` Doku | Ja: `0ad1f31` ist patch-identisch mit `16abece` (`git diff` beider Patches über `src`/`tests`: 0 Unterschiede), `19b3bf7` mit `6992f71` (`cherry`: `-`) | Ausgelassen: Doppelt |
| `origin/codex/faction-ui` | `2cd5f0c` | – | Ja: Vorfahr von `origin/main` | Ausgelassen: enthalten |
| `origin/codex/manual-test-candidate` | `e18eac5` | – | Ja: Vorfahr | Ausgelassen: enthalten |
| `origin/codex/resync-game` | `0bde512` | – | Ja: Vorfahr | Ausgelassen: enthalten |
| `origin/docs/full-multiplayer-plan` | `1e5a529` | – | Ja: Vorfahr | Ausgelassen: enthalten |
| `origin/feat/lobby-hardening` (Sven) | `61dc9f7` | Eigene Umsetzung von ID-001, XFER-001, MOD-001, START-001 (Wire-Version 3 eigener Prägung), Lobby-UI; Doku 2026-10-15 | Fachlich ja: `main` hat ID-001/XFER-001 (ADR 0014/0016, `PlayerKey`, `TransferPayloads`), MOD-001 (`ModManifest.cs`, `ModDiffWindow.cs`), Setup-Regeln (ADR 0017) und Start-Knopf über `feat/setup-rules`/`feat/mod-check-setup-ui`; `references/verified-apis.md` auf `main` ist eine Obermenge (Diff enthält nur Zeilen, die `main` zusätzlich hat). Der Branch selbst nennt sich „nicht kombinierbar“ mit diesen (`61dc9f7`). | Ausgelassen: Überholt. Benutzertest 2026-10-15 ist in `PROJECT_STATE.user_reported_capabilities` festgehalten; Archiv-Tag bewahrt die Doku |
| `origin/feat/mod-check-setup-ui` (Sven) | `9343e85` | `cf41b65` Start behält Figuren/Startort; `8265fe8`, `9343e85` Doku mit Benutzer-Solo-Lauf | Nein: `77534f9` (Codex, ADR 0048) prüft den Figurenbesitz vor/nach dem Start, nicht aber die Ursache (das Hauptmenü verwirft das Spiel, solange kein Fenster offen ist) | **Übernommen**: `cf41b65` manuell portiert als `fd68525`; Verifikationsdoku als `docs/verification/2026-10-16-start-keeps-setup.md` mit Herkunftsvermerk |
| lokal `feat/mod-check-setup-ui` (Sven, `E:\RimworldMP`) | `5c3f2b7` | Zusätzlich `37ad103` Gast-Welt verwerfen; `228a6df`, `3412a08`, `5a597bb`, `f5084a0` vanilla-nahe Gast-Figurenseite mit allen Host-Funktionen (Protokoll-Erweiterungen in Svens Code-Bereich); Doku | Nein. Gast-Welt-Fix fehlt auf `main` (`WorldCodec.Discard` ließ die Welt aktuell). Die Gast-Figurenseite steht gegen Codex' `CharacterSelectionWindow`/PAWN-001 (ADR 0048) | **Teilweise**: `37ad103` portiert als `1711a92` (+ Doku `2026-10-16-guest-world-discard.md`). Gast-Figurenseite **ausgelassen**: UI-Alternative zu PAWN-001, ~1 400 Zeilen, Entscheidung des Besitzers. Alle Commits liegen auch in `origin/feat/sync-lockstep` |
| `origin/feat/sync-lockstep` (Sven) | `2a52807` | Zweite Gleichlauf-Implementierung (ADR 0024): eigener `LockstepSequencer`, Wire-Version 4, Codes 38–43, `LockstepDriver`, Determinismus-Fixes, Selbsttest, Zwei-Spieler-Lauf 2026-10-16 (Zeit lief synchron) | Nein; `main` hat ADR 0030/0040/0042 (Koordinator, Tick-Treiber, Hash) aus der Codex-/Claude-Bahn, nie im Spiel getestet. 25 überlappende Dateien (`FoundationServer.cs`, `LockstepPayloads.cs`, `MessageType.cs`, `FoundationMod.cs`, `SessionPausePatch.cs`, …) | **Teilweise**: die drei isolierbaren Determinismus-Patches aus `5f1b782` portiert als `b0f5780`. Rest **ausgelassen**: nicht mechanisch zusammenführbar, Architektur-Entscheidung des Besitzers (unten) |
| `origin/feat/sync-commands` (Sven) | `ed5b33b` | Geteilte Spieleraktionen (ADR 0025): Befehls-Codec, Zeichnen, Aufträge, Zonen, Bereiche, Verbieten, Arbeitsprioritäten, Zeitplan, Forschung | Nein; `main` hat erst Draft/Forbid/Goto (ADR 0043, X4) | **Ausgelassen**: baut auf `feat/sync-lockstep` auf; fachlich sehr wertvoll, aber nur mit der Linien-Entscheidung übernehmbar |
| lokal `feat/sync-lockstep`, `feat/sync-commands`, `docs/full-multiplayer-plan`, `feat/lobby-hardening` (Sven) | wie `origin` | – | identisch mit `origin` | wie oben |
| `origin/main` | `85e02f9` | Ziel | – | – |

### Fachliche Entscheidung für den Besitzer: zwei Gleichlauf-Linien

`main` (ADR 0030/0040/0042/0043, Codex/Claude beim Besitzer) und `feat/sync-lockstep` → `feat/sync-commands` (ADR 0024/0025, Sven) implementieren dieselben Aufgaben (SYNC-001/002/003, DET-001) unabhängig voneinander ab `08e7f9b`. Beide können nicht nebeneinander bestehen (zwei Sequencer in `FoundationServer`, zwei Tick-Treiber, verschiedene Wire-Versionen und Codes). Was jede Linie der anderen voraus hat:

| | `main` | `feat/sync-*` |
|---|---|---|
| Koordinator | Speichern/Beitritt, Desync-Reparatur, Abstimmungen, Snapshot-Zeitgrenze, Fraktionen, IP-Sicherheit, jetzt Tempoanpassung | Sequencer mit Flusssteuerung, sonst nichts davon |
| Spielseite | Tick-Treiber, Hash, Rand-Neusaat, Save/Join-Adapter, Resync, Modus-UI, Fraktions-UI, 3 Befehle | Tick-Treiber mit Determinismus-Fixes (jetzt portiert), Hash, breite Befehlsmenge (Zeichnen, Aufträge, Designationen, Bauen, Zonen, Bereiche, Verbieten, Prioritäten, Zeitplan, Forschung), Selbsttest mit Befehlsskript und Rand-Trace |
| Nachweis im Spiel | keiner (Spieltests ausgesetzt) | Zwei-Spieler-Lauf 2026-10-16: Zeit synchron, Marker synchron; Selbsttest 6 000 Ticks identisch; seltene Abweichung nach 20 000 Ticks offen |

Empfehlung: `main` als Grundlage behalten (Koordinator-Funktionen und Fraktionen sind dort), und die Befehlsmenge aus `e17d8ca` (`feat/sync-commands`) als nächste SYNC-004-Schritte auf `GameCommandCodec`/`SessionGameCommandPatch` übertragen. Bis zur Entscheidung `feat/sync-lockstep` und `feat/sync-commands` **nicht löschen**.

## Konflikte

Keine Git-Konflikte: Fast-Forward für `codex/continuous-sync`, manuelle Portierungen für die drei Fixes (je Commit dokumentiert, mit Quell-SHA). Bei den Portierungen gewählte Fassungen:

| Datei | Fassung | Grund |
|---|---|---|
| `FoundationMod.cs` (`StartSharedGame`) | `main`-Logik (Figurenprüfung ADR 0048) **plus** `GameInitData`-Guard und Fenster-Haltung aus `cf41b65` | beide Prüfungen ergänzen sich; ohne das Fenster trat der Fehler beim Benutzer auf |
| `FoundationMod.cs` (Untertitel, `DrawSetupDetails`) | Text an den `main`-Stand angepasst („Lobby, gemeinsame Einrichtung, Spielstart und Gleichlauf (Vorschau)“) | Svens Text verwies auf das damals fehlende SYNC-001 |
| `SetupMirror.cs` | nur der SETUP-004-Hinweis entfernt | wie `37ad103`; der SETUP-003-Hinweis stand auch bei Sven noch |
| Determinismus-Patches | Bedingung `SessionRandom.Active` (Lockstep-Tick oder Befehl) statt `LockstepDriver.Ticking`; Tickraten-Patch über `FoundationMod.InSharedGame` | `main` hat Svens Treiber nicht; Ziehungen in isolierten Methoden bleiben aus dem Hash-Fenster (`SessionRandom.RecordInt`) |
| `WorldSelfTest.cs` (`landedChosen`) | Teilmengenprüfung statt Sequenzgleichheit | verträglich mit dem Fraktions-Selbsttest auf `main`, erkennt den Zufallsfiguren-Fehler weiterhin |

## 1.4 Prüfung

Siehe `docs/verification/2026-10-17-integration-sync.md`. Nicht geprüft ohne Laufzeittest: Zwei-Client-Gleichlauf, Hot-Rejoin, Steam-Werte zur Laufzeit, Fraktionen, Tempoabsenkung im Spiel, die drei Determinismus-Patches auf `main`s Treiber.

## Löschliste für Phase 2 (nur nach Freigabe)

Worktrees am 2026-10-17: `E:\RimworldMP` (`feat/mod-check-setup-ui` @ `5c3f2b7`, sauber), `E:\RimworldMP-sync` (`feat/sync-commands` @ `ed5b33b`, sauber), `E:\RimworldMP-integration` (`integration/sync-2026-10-17`, sauber nach den Commits). Vor jeder Löschung: `git fetch origin --prune`, Archiv-Tag `archive/<branch>` auf den Kopf, Tags pushen.

| Branch | Wo | Kopf | Vorschlag | Grund |
|---|---|---|---|---|
| `codex/continuous-sync` | remote | `f1ebe66` | löschen | Vorfahr des Integrationsbranchs |
| `codex/tick-driver` | remote | `19b3bf7` | löschen | patch-identisch auf `main` |
| `codex/faction-ui`, `codex/manual-test-candidate`, `codex/resync-game` | remote | `2cd5f0c`, `e18eac5`, `0bde512` | löschen | Vorfahren |
| `docs/full-multiplayer-plan` | remote + lokal | `1e5a529` | löschen | Vorfahr |
| `feat/lobby-hardening` (Sven) | remote + lokal | `61dc9f7` | löschen | überholt; Doku im Archiv-Tag |
| `feat/mod-check-setup-ui` (Sven) | remote `9343e85`; lokal `5c3f2b7` in `E:\RimworldMP` | – | löschen (Worktree vorher mit `git checkout --detach origin/main` lösen bzw. auf `main` stellen) | Code portiert bzw. in `origin/feat/sync-lockstep` enthalten |
| `feat/sync-lockstep`, `feat/sync-commands` (Sven) | remote + lokal (`E:\RimworldMP-sync`) | `2a52807`, `ed5b33b` | **behalten bis zur Linien-Entscheidung** | nicht integrierte Gleichlauf-Linie und Gast-Figurenseite |
| `integration/sync-2026-10-17` | lokal | Kopf dieses Berichts | nach dem Fast-Forward von `main` löschen | Zweck erfüllt |
| lokale Branches des Besitzers (`claude/faction-sites`, `codex/*`) | nur beim Besitzer | – | vom Besitzer gegen `origin/main` prüfen | hier nicht vorhanden |

Sven ist über die Löschung seiner Branches informiert: Er hat diesen Lauf selbst ausgeführt.
