# Übergabe Claude (Koordinator und Protokoll)

- **Bahn:** siehe `docs/ai/ASSIGNMENTS.md`. Branch-Präfix `claude/`, ADR 0032–0039, Nachrichtencodes 71–79 und 96–111 (zweiter Bereich, Benutzer 2026-10-16; belegt 96–97). Worktree `/mnt/Arbeit/Git Projekte/RimworldMP-claude`.
- **Stand auf `main` (`85af3a9`):**
  - Chunk-Kanal (ADR 0014), PlayerKey (ADR 0016), Setup-Regeln (ADR 0017).
  - Lockstep-Koordinator (ADR 0030): `LockstepSession`, `LockstepClient`, `LockstepHost`, `OrderedOutbox`; Codes 64–70.
  - Spielmodus (ADR 0031, Koordinator-Seite): `LobbyMode` in `CreateLobby`, Lobbyzustand und -liste; Fraktionsstart abgelehnt bis `FACTION-001`.
- **C1 erledigt (Koordinator-Seite), Branch `claude/save-join`, ADR 0032:** Speichern als geplanter Befehl (`ScheduledKind.Save`), Autosave je 60 000 Ticks, Host-Speichern; Beitritt während `Playing` mit passenden Mods; Snapshot-Befehl, `JoinSnapshot` (71), Aufholen ohne Flusssteuerung, `PlayerSynced` (72). Simulation: Der Neue führt danach exakt dieselbe Folge aus.
- **C2 erledigt (Koordinator-Seite), Branch `claude/resync` auf `claude/save-join`, ADR 0033:** Desync führt zu Snapshot und Neuladen der Abweichenden; automatisches Fortsetzen, `DesyncResolved` (73); nach drei Fehlschlägen `ResyncGaveUp` (74); `SnapshotLoaded` (75) jetzt Pflicht für Beitritt **und** Resync.
- **C3 erledigt (Koordinator-Seite), Branch `claude/vote` auf `claude/resync`, ADR 0034:** Abstimmungen nur in Coop-Demokratie; Host öffnet und beendet; gemeinsame Pause, Fortsetzen gesperrt; Mehrheit, Host bei Gleichstand, Enthaltung; Ergebnis als `ScheduledKind.VoteResult` (4); Codes 76–79. Gemeinsame System-Pause für Desync und Abstimmung.
- **Nummernbereich 71–79 ist ausgeschöpft.** C4 (`SEC-001`, Lobby-Passwort) kommt ohne neue Codes aus (Erweiterung von `CreateLobby`/`JoinLobby`); sonst neuen Bereich mit dem Benutzer abstimmen.
- **Gemergt nach `main` (2026-10-16, Freigabe des Benutzers):** C1, C2 und C3 per Fast-Forward. Die Branches `claude/save-join`, `claude/resync` und `claude/vote` sind danach entfernt.
- **C4 erledigt (Protokoll und Server), Branch `claude/ip-password`, ADR 0035:** Lobby-Passwort; Schlüsselbindung per signiertem Hello (RSA-2048, Zeitstempel, Zufallswert) mit Bindung beim ersten Kontakt, Wiederholungsschutz und Pflicht-Modus; Steam ausgenommen. Keine neuen Codes.
- **Alle Aufgaben C1–C4 der Claude-Bahn sind erledigt** und auf `main` (C4 seit `0af2bcc`). Codex' Spielseite ist seit `ff2f178` gemergt.
- **C5, Branch `claude/snapshot-timeout`, ADR 0036:** Wer einen Snapshot nicht binnen 180 s bestätigt, wird aus Sitzung und Lobby genommen; die anderen setzen fort. Für Codex: bei `ResyncGaveUp` mit eigener Session und `LeaveLobby` „Laden fehlgeschlagen – erneut beitreten“ anzeigen.
- **C6, Branch `claude/faction-storyteller` von `main` @ `d568766`, ADR 0037, Aufgabe `FACTION-004`:** Fraktionsmodus, Koordinator-Seite der Benutzerentscheidung vom 2026-10-16. **Auf `main`.**
  - **Fraktionsplatz:** Beim Start der Einrichtung erhält jeder Spieler einen Platz (Host 0, dann Beitrittsreihenfolge). Der Platz hängt am `PlayerKey` und bleibt bis zum Ende der Partie.
  - **Phase `Storyteller`:**
    - Der Host sendet `FactionOptions`: gemeinsame `DifficultyDef` und wählbare `StorytellerDef`s.
    - Jeder Spieler wählt mit `StorytellerChoice` den Erzähler seiner Fraktion und meldet sich fertig.
    - Weiter geht es erst, wenn alle gewählt haben und fertig sind.
  - **Gesperrt:** Der Fraktionsstart bleibt abgelehnt. Öffnen lässt er sich nur in Tests:
    - `new LobbyRegistry(…, factionsSetupEnabled: true)`
    - `HostConfig.EnableFactionsSetup`, `internal`, nur für `Hosting.Tests` sichtbar; `ListenHost` hat keinen Parameter dafür.
  - **Codes vergeben (Benutzer, 2026-10-16):** neuer Claude-Bereich **96–111**, in `ASSIGNMENTS.md` eingetragen.
    - Belegt: 96 `FactionOptions`, 97 `StorytellerChoice`. Beide sind in `FoundationServer` verdrahtet und Ende zu Ende getestet (`Hosting.Tests`).
    - 98–111 sind frei, aber noch ungültige Frame-Typen.
  - **Nachtrag-Commit** auf `2a2c818`: Codes, Server, strengerer Lobbyzustand, Tests, Vertrag.
  - **Gemergt nach `main`** (Freigabe des Benutzers, 2026-10-16) per Fast-Forward; der Branch ist danach entfernt.
  - **Geänderte Schnittstellen für Codex** (alle in Claudes Projekten; Spielseite nicht angefasst):

    | Schnittstelle | Änderung | Was die Spielseite tun muss |
    |---|---|---|
    | `MessageType.FactionOptions = 96` | neu, C→S, nur Host, Fraktionsmodus, Phase `Storyteller` | Senden: `client.SendAsync(MessageType.FactionOptions, id, new FactionOptions(difficultyDefName, storytellerDefNames).ToPayload())` |
    | `MessageType.StorytellerChoice = 97` | neu, C→S, jeder Spieler, gleiche Bedingungen | Senden: `FactionPayloads.StorytellerChoice(defName)`; vorher `DefNames.IsValid` prüfen, denn ungültige Nutzdaten ergeben Code 1 und trennen die Verbindung |
    | Antwort auf 96/97 | `LobbyStateUpdate` mit Request-ID an den Absender, an alle anderen als Push; Fehler 3 (nicht Host) bzw. 6 (Coop, Phase, Liste fehlt, nicht angeboten) | Fehler im UI anzeigen; Anzeige immer aus dem nächsten Lobbyzustand |
    | `PlayerReadyChanged` (14) | im Fraktionsmodus auch in der Phase `Storyteller` gültig; verlangt eine Wahl | Knopf „Fertig“ auf der Erzählerseite |
    | `LobbyPayloads.State`/`ReadState` | Fraktionsblock **genau dann**, wenn `LobbyPayloads.HasFactionBlock(mode, phase)` (Fraktionen, Phase ≠ `Lobby`); sonst byte-gleich wie vorher; Fraktionsdaten am falschen Ort ergeben eine `ProtocolException` | nichts, solange nur gelesen wird; keine eigenen `LobbyView` mit Fraktionsdaten außerhalb der Fraktionseinrichtung bauen |
    | `LobbyView.FactionOptions`, `PlayerView.FactionSlot`, `PlayerView.Storyteller` | wie im ersten Commit; ohne Block `null` | Anzeige und Anwenden beim Generieren |
    | `LobbyRegistry.StorytellerBlockReason(view)` | unverändert | Anzeigetext „Erzähler fehlt: …“ / „Warte auf: …“ |
    | Beitritt in `Playing`, Fraktionsmodus | neuer Spieler ohne Platz: Code 6 „This game has no faction for this player.“ | Meldung im Beitrittsfenster |
    | `HostConfig.EnableFactionsSetup` | `internal`, Standard aus | nicht verwenden. Beim Freischalten des Fraktionsstarts (nach Spielseite und Zwei-Client-Test) wird der Schalter entfernt und die Sperre in `LobbyRegistry` aufgehoben, nicht öffentlich gemacht. |

  - **Vertrag zur Spielseite (ADR 0037 §6, mit ADR 0045 abgestimmt):**
    - `FactionSlot` ist stabil; die Liste im Spielstand folgt der Platzreihenfolge.
    - Die Erzählerwahl hängt am `PlayerKey`.
    - Benutzerdefinierte Schwierigkeitswerte stammen nur aus dem Spielstand des Hosts.
    - Kein Ereignis wird vom Host entschieden und verteilt. Jedes Spiel rechnet alle Erzähler im Lockstep, je Tick einer je Fraktion in aufsteigender Platzreihenfolge, im kontrollierten `Rand`-Bereich.
    - **Weltweite Ereignisse (R4) sind offen.** Bis zur Entscheidung des Benutzers lösen Fraktionserzähler keine aus.
  - **Mögliche Konflikte:**
    - `LobbyPayloads.State`/`ReadState` haben im Fraktionsmodus einen angehängten Block; Coop ist byte-gleich.
    - `PlayerView` und `LobbyView` haben neue optionale Konstruktor-Parameter am Ende; bestehende Aufrufe bauen unverändert (Mod-Build geprüft).
    - **Aufgaben-ID:** Codex hat auf `codex/faction-storytellers` (`671adb1`) die Spielseite als `FACTION-003` mit ADR 0045 angelegt. Die Koordinator-Seite heißt deshalb `FACTION-004`; weitere Codex-IDs bitte ab `FACTION-005`.
    - **ADR 0031:** Beide Branches ändern dieselbe Zeile (Verweis auf 0045 bzw. 0037). Beim zweiten Merge beide Verweise behalten. `CHANGELOG.md` und `CODEMAP.md` ergeben nur einfache Anfügekonflikte.
    - **Abgleich mit ADR 0045:**
      - Die Wahl je PlayerKey im Spielstand bitte in Platzreihenfolge ablegen (`PlayerView.FactionSlot`).
      - Erzähler mit erzwungener anderer Schwierigkeit gar nicht erst in die Host-Liste aufnehmen.
    - Die Beschreibung von `FACTION-001` (ohne Besitzer) nennt noch „Erzähler wählt der Host“; das gilt nach ADR 0037 nur für Coop.
  - **Offen für die Benutzer-Entscheidung:** Regel R4 in ADR 0037. Wirken Weltbedingungen nur auf die Karten der auslösenden Fraktion, oder plant sie ein einziger Erzähler für alle? Bis dahin gilt „keine“.
  - **Weitere offene Punkte:** Plätze beim Fortsetzen eines Spielstands wieder einspielen (`SAVE-002`; braucht eine weitere Nachricht aus 98–111); neue Fraktionen mitten im Spiel.
- **Wünsche an Codex:**
  - **Fraktionsmodus, Erzähler (ADR 0037)**, alles Spielseite:
    - **Lesen:**
      - Wahl und Optionen kommen über den Lobbyzustand: `LobbyView.FactionOptions` (Schwierigkeit, Liste), `PlayerView.FactionSlot`, `PlayerView.Storyteller`.
      - Den Grund für die UI liefert `LobbyRegistry.StorytellerBlockReason(view)`.
    - **Host:**
      - In der Phase `Storyteller` die Liste aus `StorytellerDef`s mit `listVisible`, nach `listOrder`, und die gewählte `DifficultyDef` senden (`new FactionOptions(…).ToPayload()`).
      - Gäste bekommen kein Schwierigkeitsfeld.
    - **Alle Spieler:** Wahl mit `FactionPayloads.StorytellerChoice(defName)`, dann Fertig über `PlayerReadyChanged` (14).
    - **Beim Generieren** (Host):
      - je Platz in aufsteigender Reihenfolge eine Spielerfraktion
      - je Fraktion eine eigene `Storyteller`-Instanz mit dem gewählten `StorytellerDef` und derselben `Difficulty`
      - PlayerKey → Fraktion im Spielstand speichern
    - **`Verse.Game.storyteller`** ist ein einzelnes Feld; die Erzähler je Fraktion brauchen eine eigene Ablage (z. B. `GameComponent`, Regel 13). Sie ticken in Platzreihenfolge.
    - **Ereignisse:** Zuordnung nach ADR 0037 §6 (R1–R6; R4 offen, bis dahin keine weltweiten Ereignisse aus Fraktionserzählern).
    - **Senden** über die Codes 96/97, siehe Tabelle „Geänderte Schnittstellen“ oben.
  - Tick-Treiber nach ADR 0030:
    - Tick c+1 nur, wenn `LockstepClient.MayRunTick(c)` wahr ist; vorher und in jedem Frame während der Pause `TakeCommandsAt(c)`.
    - Sitzungstick = `TicksGame − TicksGame` bei der Landung.
    - Etwa 10/s `ProgressMessage`, alle 60 Ticks `HashMessage`.
    - Pause und Geschwindigkeit nur als `ControlMessage`.
  - Paketlisten in `scripts/build-mod.sh` und `scripts/package-mod.py` müssen jede neue Bibliothek enthalten, sonst stürzt der Host beim Laden ab.
  - **Speichern und Beitritt (ADR 0032)**, alles Spielseite:
    - `TakeCommandsAt` liefert jetzt auch `ScheduledKind.Save`.
    - Leere Daten: koordiniert speichern; maßgeblich ist der Stand des Hosts, die anderen behalten eine lokale Sicherung.
    - 16 Byte Daten (Session-ID eines Neuen): Der **Host** speichert genau dort und schickt den Spielstand über den Chunk-Kanal (`TransferKind.Snapshot`) an diesen Spieler.
    - Der **Neue** wartet auf `JoinSnapshot` (71) und den Spielstand, lädt ihn und erzeugt `LockstepClient.ForJoin(snapshot)`. Er rechnet mit voller Geschwindigkeit bis zum Horizont und meldet dabei `ProgressMessage`.
    - **Wichtig:** Der Snapshot entsteht mitten in den Befehlen von Tick T. Für Tick T selbst darf der Neue **keinen** Zustands-Hash melden; erst ab dem nächsten Hash-Tick.
    - Besitz und Rechte über den `PlayerKey` wiederherstellen (Reconnect).
    - Anzeige „holt auf“ nach `PlayerSynced` (72) beenden.
    - Option „pausieren beim Beitritt“ (Plan §7.8) in der UI.
  - **Desync-Reparatur (ADR 0033)**, alles Spielseite:
    - `DesyncDetected` ist **nicht mehr endgültig**.
    - Kommt danach `JoinSnapshot` an dich, bist du betroffen: Stand und `LockstepClient` verwerfen, auf den Snapshot des Hosts warten (Chunk-Kanal), laden, `LockstepClient.ForJoin` und dann `LockstepClient.SnapshotLoadedMessage()` senden. Ohne diese Bestätigung bleibst du inaktiv.
    - Der Speicherbefehl davor nennt dich in `LockstepPayloads.SaveTargets(data)`. Für dich ist das kein Speicherauftrag; für den Host heißt es: an diese Spieler senden.
    - Beim Beitritt ebenfalls `SnapshotLoadedMessage()` nach dem Laden senden (neu gegenüber ADR 0032).
    - Anzeigen: `DesyncResolved` (73) als „Desync behoben (Tick X)“, `ResyncGaveUp` (74) als Hinweis an den Host.
  - **Abstimmung (ADR 0034)**, alles Spielseite; setzt die Befehlserfassung voraus:
    - Im Modus Coop-Demokratie (`LobbyMode.CoopDemocracy`) sendet das Spiel des **Hosts** bei einem Ereignis mit Wahl (Wanderer, Quest annehmen) `VoteOpen(Optionen, Ereignisschlüssel)`. Der Schlüssel muss in allen Spielen dasselbe Ereignis bezeichnen.
    - Ein Fenster zeigt `VoteState` (79): Optionen, Stimmen je Spieler, beim Host einen Knopf „Beenden“ (`VoteClose`).
    - Spieler stimmen mit `VoteCast` ab.
    - `ScheduledKind.VoteResult` kommt über `TakeCommandsAt`: `LockstepPayloads.ReadVoteResultData` liefert Nummer, Option und Schlüssel. Das Ereignis an dieser Stelle mit dieser Option ausführen.
    - Im Modus Coop-Host trifft der Host die Wahl über einen normalen Spielbefehl; Gäste dürfen dort nicht wählen (Rechte, `SYNC-005`).
  - **IP-Sicherheit (ADR 0035)**, alles Spielseite:
    - Einmal `PlayerIdentity.Create()`, `ToBytes()` in den Mod-Einstellungen speichern (lokal; privater Schlüssel, nie versenden), beim Start `PlayerIdentity.FromBytes`.
    - Bei jedem Verbinden als `identity` an `LobbyClient.ConnectAsync` bzw. `ListenHost.StartAsync` übergeben.
    - Passwortfeld beim Erstellen (`ListenHost.StartAsync(..., password:)`) und Beitreten (`LobbyPayloads.Join(id, password)`); Schloss nach `LobbySummary.PasswordRequired` anzeigen.
    - Für IP-Lobbys `requireKeyProof: true` setzen, sobald die Identität überall mitgegeben wird.
    - `ListenHost.Pins.Export()` mit dem Multiplayer-Spielstand speichern und beim Fortsetzen über den Parameter `pins` wieder einspielen.
    - Mono: `RSA`-APIs sind laut Metadaten vorhanden; bitte im Spiel bestätigen, dass `PlayerIdentity.Create` 2048 Bit liefert und `Prove` funktioniert.

