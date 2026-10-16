# Übergabe Codex (Spielseite)

- **X4 / 2026-10-16:** `codex/command-pipeline` auf `main` @ `b4255c6` rebasiert. Draft/Undraft, Forbid/Unforbid und einfaches Goto mit Karten-/Objekt-ID, gemeinsamem `GameReferenceCodec` und Harmony-GUI-Capture gebaut; andere UI-Aufträge werden gesperrt. Echter Zwei-Client-Lauf und Pfadsuche-Determinismus offen. Nächster Schritt: weitere stabile Referenztypen und Ordered Jobs/Designatoren nach Plan §7.3. ADR 0043, `docs/verification/2026-10-16-command-pipeline-first-action.md`. Kein Push/Merge ohne passende Freigabe.
- **X1:** `codex/ver-tickpath` @ `2068ea3` nach Rebase auf `main` @ `b4255c6`; der statische 1.6-Thread-Audit liegt unter `docs/verification/2026-10-16-tick-thread-audit.md`. Die Assembly belegt parallele Karten- und Weltpfadsuche sowie einen möglichen asynchronen Autosave-Pfad. Kein Laufzeit-Threadtrace, noch nicht gemergt.
- **Bahn:** siehe `docs/ai/ASSIGNMENTS.md`. Branch-Präfix `codex/`, ADR 0040–0049, Nachrichtencodes 80–95. Arbeitsverzeichnis `/mnt/Arbeit/Git Projekte/RimworldMP`.
- **Nächste Aufgaben:** X4 `SYNC-002` auf lokalem `codex/command-pipeline` vervollständigen. X1 `VER-001` und X5 `MODE-001` liegen auf separaten lokalen Branches; `codex/manual-test-candidate` vereint sie zur Paketprüfung. RimWorld-Spieltests warten auf Fabio und Sven. Siehe `ASSIGNMENTS.md`.
- **Koordinator:** Claude C1–C3 sind seit `8f7c32f` auf `main`. `claude/save-join`, `claude/resync` und `claude/vote` sind gelöscht; für Snapshot-Ladefehler bleibt eine zeitliche Abbruchregel zu prüfen.

## X5 – Spielmodus und IP-Identität (2026-10-16)

- **Branch/Basis:** `codex/mode-ui` von `origin/main` @ `b4255c6`. Letzten Commit per `git log -1` prüfen. Kein Merge/Push ohne passende Freigabe.
- **Ziel/Umfang:** MODE-001-Spielseite und der in ADR 0035 angeforderte SEC-001-Spieladapter in `FoundationMod`, `FoundationSettings`, `MultiplayerSession` und `WorldSelfTest`. Kein Koordinator-/Protokollcode, kein Gameplay-Fraktionsmodus.
- **Ergebnis:** Host wählt Coop/Host, Coop/Demokratie oder Fraktionsbeziehung; Servermodus erscheint in Liste/Lobby und wird im gemeinsamen Save gespeichert. Ältere Saves gelten als Coop/Host; Modusabweichung hält Tick-Treiber an. IP-Verbindungen verwenden gespeicherte lokale Kennung plus RSA-Nachweis; direkter Host verlangt ihn und speichert Key-Pins. Optionales Lobby-Passwort wird abgefragt. Steam-Kennung nur über Steam.
- **Prüfung:** Mod-Build gegen lokale 1.6.4871-rev598-Assemblies ohne Warnungen; `Protocol.Tests` und `Hosting.Tests` als Konsolenprogramme bestanden. Genaue Befehle und Grenzen: `docs/verification/2026-10-16-codex-mode-ui.md`. RimWorld selbst auf Benutzerwunsch nicht gestartet.
- **Offen:** UI, RSA unter RimWorld-Mono, tatsächliches Laden des Modus, Zwei-Spieler-Simulation, Windows/Linux-Test. X4 ist ein separater lokaler WIP-Branch. Näheres in ADR 0044 und `docs/reviews/2026-10-16-codex-mode-ui.md`.

## Aktuell – ADR 0032 auf Spielseite und X3

- **Branch/Basis:** `codex/resync-game` auf `origin/main` @ `8f7c32f` rebasiert; aktuelles HEAD per `git log -1` prüfen. X2, Save/Join, X3 und C2-Spieladapter sind enthalten. Push von `codex/resync-game` ist vom Benutzer freigegeben; kein Merge ohne separate Freigabe. `codex/tick-driver` @ `19b3bf7` ist überholt.
- **Aufgabenvertrag:** `SAVE-003`, `JOIN-002`, `DESYNC-001` (Spielseite). Ziel: Save-Befehle und Join-Snapshots im Verse-Spielpfad ausführen; Zustand gemäß Plan §7.9 hashen; `Rand` nach §7.4 isolieren. Nicht-Ziele: Spielbefehle (`SYNC-002`), C2-Koordinator, vollständige modübergreifende Determinismus-Zusage.
- **Umfang:** Nur `src/Multiplayer.Foundation.RimWorld/`, eigene Docs/Task-Einträge. Save-Befehle laufen einzeln in Sequenz; Folgebefehle warten auf die Datei. Host versendet gezielt `TransferKind.Snapshot`. Joiner lädt die Datei, nutzt `ForJoin`, überspringt immer das erste Hashfenster nach Einstieg, bestätigt `SnapshotLoaded` und holt mit `Ultrafast` bis zum Horizont auf. Bei `DesyncDetected` wartet nur ein abweichender Client auf den Reparatur-Snapshot; unbetroffene folgen der Koordinator-Pause. Vanilla-Autosave wird während `Playing` unterdrückt.
- **X3:** `SessionRandom` setzt `Verse.Rand` pro Tick und bietet einen Command-Scope für X4. `SessionStateHash` erfasst Dinge, Pawn-Jobs/Needs, Faction-Goodwill ohne Selbstbeziehung, Quest-State und Rand-Ziehungen. Der gecachte Health-Prozentwert wurde nach IL-Prüfung entfernt. ADR 0041/0042; aktuelle Prüfung `docs/verification/2026-10-16-codex-rebase-review-fixes.md`.
- **Prüfstrategie/Ergebnis:** Lokale 1.6.4871 rev598-API per Reflection/IL, Mod-Build, `Session.Tests`, `Hosting.Tests`, Dokumentationsvalidator. Kein RimWorld-Zwei-Client-Test. Die Testprotokolle sind für tatsächlich ausgeführte Befehle maßgeblich.
- **Risiken/Nächster Schritt:** Save/Load und Harmony unter realer Spiel-Last prüfen; im ersten Zwei-Client-Test Rand-Desync erwarten und ersten abweichenden Zustand protokollieren. Insbesondere C2-Snapshot und automatische Fortsetzung prüfen. Danach X4 Spielbefehle. `main` nicht ohne neue menschliche Merge-Freigabe verändern.

## X2 – Aufgabenvertrag und Ergebnis

- **Branch/Basis:** `codex/tick-driver` von `main`/`e4501df`; Feature-Commit `0ad1f31`, letzten Commit per `git log -1 --oneline` prüfen. `SYNC-001` und `SYNC-003` (Spielseite).
- **Ziel:** Nach der Landung Tick `c+1` erst nach Befehlsübernahme und Freigabe durch `LockstepClient.MayRunTick(c)`; Pause/Tempo über den Koordinator; „Warte auf …“ anzeigen.
- **Umfang/Nicht-Ziele:** Nur RimWorld-Adapter, eigene Übergabe, ADR 0040 und Prüfnachweis. Kein Koordinator-Code, keine Spielbefehls-Serializer (`SYNC-002`), kein vollständiger Zustands-Hash/Rand (`X3`).
- **Abhängigkeiten:** `LockstepClient`/ADR 0030 auf `main`; `Playing` und gemeinsamer Save aus `SETUP-005`. `DET-001` bleibt offen.
- **Umgesetzt:** `SessionTickDriver` verbindet den Verse-Tickpfad mit `LockstepClient`; die Harmony-Hooks bearbeiten Grenzbefehle, Tickfreigabe und Koordinator-Pause/-Tempo. Overlay zeigt Sitzungstick und Wartezustand. Fortschritt etwa 10/s und vorläufiger Kernhash alle 60 Ticks. Paketliste enthält `Session.dll` bereits.
- **Prüfung:** Lokale 1.6-API per Reflection, Mod-Build ohne Warnungen, `Session.Tests` und `Client.Tests` bestanden; Mod-ZIP mit 10 Dateien einschließlich `Session.dll` geprüft. Details: `docs/verification/2026-10-16-codex-tick-driver.md`. Spieltest mit zwei Clients laut Projektstatus nicht möglich.
- **Risiken:** Harmony-Reihenfolge, automatisch ausgelöste Vanilla-Pause, noch fehlender Ausführer für Spielbefehle. Design: `docs/decisions/0040-rimworld-tick-driver.md`.
- **Offen / nächster Schritt:** X2 im Spiel mit zwei Clients prüfen, insbesondere Pause, Host-Tempo, langsamen Client, Tick-/Hash-Gleichheit und Disconnect. Danach X3 oder X4 auf eigenem `codex/…`-Branch von aktuellem `main` gemäß `ASSIGNMENTS.md`. `main` nicht ohne menschliche Freigabe mergen.

## Vorgeschichte der Spielseite (bisherige gemeinsame Übergabe, unverändert übernommen)

- **Spielmodus (`feat/game-mode`, ADR 0031, MODE-001):**
  - Der Benutzer will zwei Modi: Coop (Host-Entscheid oder Demokratie) und Fraktionen. Gewählt wird einmalig beim Erstellen, danach nicht änderbar.
  - Koordinator-Seite fertig: `LobbyMode` in `CreateLobby`, Lobbyzustand und -liste; Fraktionsstart abgelehnt bis `FACTION-001`.
  - **Für die Spielseite:** Auswahl im Lobby-Fenster (`LobbyPayloads.Create(name, capacity, mode)` bzw. `ListenHost.StartAsync(..., mode:)`), Anzeige des Modus in der Lobbyliste, Modus in `MultiplayerSession` speichern und beim Laden übernehmen.
  - Neue Aufgaben: `VOTE-001`, `FACTION-001`, `FACTION-002`. Offene Benutzerfragen stehen im ADR.

- **Lockstep-Koordinator (`feat/lockstep-session`, ADR 0030):**
  - Branch von `feat/mod-check-setup-ui` @ `08e7f9b`. Aufteilung laut Benutzer am 2026-10-16: Koordinator-Seite hier, Spielseite beim zweiten Beitragenden.
  - Neues Projekt `Multiplayer.Foundation.Session` (`LockstepSession`), dazu `LockstepClient` im Network-Projekt sowie `LockstepHost` und `OrderedOutbox` im Server. Codes 64–70.
  - Die Sitzung startet automatisch beim Eintritt in `Playing`, pausiert bei Tick 0.
  - **Für die Spielseite:** Den Dauerpause-Patch (`SessionPauseTickPatch`) durch den Tick-Treiber ersetzen. Der rechnet Tick c+1 nur, wenn `LockstepClient.MayRunTick(c)`, und ruft vorher und während der Pause in jedem Frame `TakeCommandsAt(c)` auf. Sitzungstick = `TicksGame − TicksGame bei der Landung`. `ProgressMessage` ~10/s senden, alle 60 Ticks `HashMessage`. Spieler-Pause und -Geschwindigkeit als `ControlMessage` senden, statt lokal umzuschalten.
  - Paketliste in `build-mod.sh` und `package-mod.py` um `Multiplayer.Foundation.Session.dll` ergänzt.
  - Verifikation: `docs/verification/2026-10-16-lockstep-coordinator.md`.

- **Bereich:** `MOD-001`, Spielseite von `START-001`, `SETUP-001`, `SETUP-002`, `SETUP-003`, `SETUP-004`, `SETUP-005`, `VER-001`, auf Basis von `feat/setup-rules` (ADR 0013–0017).
- **Branch:** `feat/mod-check-setup-ui`, lokal unter `E:/RimworldMP`, abgezweigt von `origin/feat/setup-rules` @ `5798f95`. Ersetzt `origin/feat/lobby-hardening` (gleiche Funktionen auf eigenem Protokoll; auf Wunsch des Benutzers hierher übertragen).
- **Stand:** Mod-Prüfung (Prüfsumme nur über Mods), Bereit-Sperre, Unterschiedsbericht über den Chunk-Kanal, **Spiel starten** mit Begründung; nach dem Start öffnet der Host die vanilla-Seiten, die Mod spiegelt Seite und Auswahl an die Gäste (Code 30) und schaltet die Serverphasen nach; der letzte Start ist während einer gemeinsamen Einrichtung gesperrt. ADR 0018 und 0019.
- **Geprüft:** Sechs Testprojekte im .NET-8-Harness bestanden; Mod ohne Warnungen gebaut, installiert, Rauchtest (Manifest im Log, Patches geladen). Details: `docs/verification/2026-10-15-mod-check-setup-ui.md`.
- **Offen:** Zwei-Spieler-Spieltest (Prüfpunkte 1–9); VER-001 Tickpfad-Threads; danach `OWN-001`, `SYNC-001`.
- **Nächster Schritt:** Spieltest nach `docs/reviews/2026-10-15-mod-check-setup-ui.md`; danach `OWN-001`/`SYNC-001`. Selbsttest: `RimWorldWin64.exe -mpWorldSelfTest [-mpSelfTestScreenshot] -mpQuitAfterSelfTest`, Ergebnis im `Player.log`, Bild unter `SaveData/MultiplayerFoundation/selftest-site.png`.
- **Dateien/Befehle:** `src/Multiplayer.Foundation.Protocol/ModManifest.cs`, `SetupPayloads.cs`, `src/Multiplayer.Foundation.Lobby/LobbyRegistry.cs`, `src/Multiplayer.Foundation.Hosting/FoundationServer.cs`, `src/Multiplayer.Foundation.RimWorld/{FoundationMod,SetupMirror,SetupOverlayWindow,SharedStartGuardPatch,ModManifestBuilder,ModDiffWindow}.cs`; Build: `RIMWORLD_MANAGED=E:/SteamLibrary/steamapps/common/RimWorld/RimWorldWin64_Data/Managed DOTNET_BIN=dotnet bash scripts/build-mod.sh`.
- **SETUP-002:** Host sendet die Welt nach „Welt erstellen“ (Scribe + gzip, Chunk-Art `World`), Gäste laden sie im Hauptmenü und melden einen Fingerabdruck (nur diskrete Werte); `SiteSelection` erst, wenn alle passen. ADR 0020, Verifikation `docs/verification/2026-10-15-world-transfer.md`.
- **SETUP-003:** Gäste sehen in der Startortwahl die Weltkarte, „Weiter“ setzt ihr Fähnchen; alle sehen alle Fähnchen; nur der Host wählt. ADR 0021.
- **SETUP-004:** Figuren nur im Spiel des Hosts; `CharacterHost` hält den Besitz und führt Gastanfragen mit `StartingPawnUtility` aus; Liste als Karten (Code 33), Anfragen (Code 34). Gäste haben ein Figurenfenster, der Host die vanilla-Seite plus Feld. Fertig braucht eine eigene Figur, Änderungen nehmen es zurück. ADR 0022, Verifikation `docs/verification/2026-10-15-character-selection.md`.
- **SETUP-005:** Host startet über vanilla `InitGameStart` erst nach Serverzustimmung, füllt `MultiplayerSession` (Spieler, Besitzer), speichert, sendet (Chunk-Art 3) und lädt selbst neu; Gäste laden die Kopie; Fingerabdruck nach dem Laden (Codes 35/36), dann `Playing`. Zeit pausiert bis SYNC-001 (`DoSingleTick`-Sperre). Hauptmenü verwirft das Spiel ohne offenes Fenster. ADR 0023, Verifikation `docs/verification/2026-10-15-game-start.md`.
