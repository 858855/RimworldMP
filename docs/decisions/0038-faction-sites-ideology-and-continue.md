# ADR 0038 – Fraktionsmodus: Startort und Ideologie je Fraktion, Fortsetzen eines Spielstands (Koordinator-Seite)

- **Status:** angenommen für `claude/faction-sites`, auf `main` @ `9fc1519`. Designnotiz nach `AGENTS.md` Regel 4. Aufgabe `FACTION-006` (Bahn Claude): Koordinator-Seite von `FACTION-001` und der Fraktionsteil von `SAVE-002`.
- **Grundlage:** ADR 0017 (Phasen), ADR 0031 (Modi), ADR 0037 (Platz und Erzähler je PlayerKey, Codes 96/97). Gegenstück der Spielseite: ADR 0046 (Codex, `FACTION-005`, Branch `codex/faction-setup`). Dessen Wunsch: Startort und Ideologie **je `PlayerKey`/`FactionSlot`** autoritativ über den Koordinator; der globale `LobbyView.Site` genügt nicht.
- **Ziel (Koordinator, ohne RimWorld):**
  - Wahl von Startort und Ideologie je Fraktionsplatz, mit Rechteprüfung, Lobbyzustand und Fertig-Regeln.
  - Wiederherstellung bei Rückkehr (Reconnect) und beim Fortsetzen eines Fraktions-Spielstands.
- **Nicht-Ziele:**
  - RimWorld-Prüfungen: ob ein Feld besiedelbar ist, Abstände zwischen Kolonien, ob eine Ideologie gültig ist und wie sie kodiert wird. Das ist Spielseite (Codex); der Koordinator behauptet dafür keine APIs.
  - Startfiguren je Fraktion (Phase `Characters`): bleibt wie bisher. Karten tragen den `PlayerKey` als Besitzer.
  - Fortsetzen im Coop: Teil des restlichen `SAVE-002`, offen.
  - Freischalten des Fraktionsstarts.

## Entscheidung

### 1. Startort je Fraktion (Phase `SiteSelection`, nur Fraktionsmodus)

- `FactionSiteChoice` (98) setzt oder entfernt (`leer`) den Startort der **eigenen** Fraktion.
  - Die Nachricht trägt keinen Platz; der Server nimmt den Platz zum `PlayerKey` des Absenders. **Eine fremde Wahl ist damit nicht möglich.**
- **Server prüft:**
  - Fraktionsmodus, Phase `SiteSelection`, Absender hat einen Platz (sonst Code 6).
  - **Kein Feld doppelt:** Hat ein anderer Platz dasselbe Feld (`tileId`, `layerId`), wird die Wahl abgelehnt (Code 6).
  - Dieselbe Wahl noch einmal ändert nichts; eine neue Wahl löscht das eigene Fertig.
- **Coop unverändert:** Fähnchen (26) und Host-Wahl (27). Im Fraktionsmodus lehnt der Server beide ab (Code 6), weil jeder seinen eigenen Ort wählt. `LobbyView.Site` bleibt dort `null`.
- **Fertig** gilt im Fraktionsmodus auch in `SiteSelection` und verlangt einen eigenen Startort.
- **Weiter zu `Ideology`:** `SetupAdvance` des Hosts, wenn jedes Mitglied einen Startort hat und alle fertig sind.

### 2. Ideologie je Fraktion (Phase `Ideology`, nur Fraktionsmodus)

- **Wahl:** `FactionIdeologyChoice` (99, C→S) trägt eine **Bezeichnung** (1–96 Byte UTF-8, druckbar) und **undurchsichtige Daten** (1–49 152 Byte).
  - Das Format der Daten legt die Spielseite fest, z. B. Vorlage oder vollständig bearbeitete Ideologie, gern komprimiert.
  - Ohne Ideology-DLC sendet die Spielseite trotzdem eine feste Wahl ihres Formats; der Koordinator verlangt in diesem Modus immer eine.
- **Server:**
  - Er speichert je PlayerKey nur **Bezeichnung und SHA-256 der Daten**. Der Lobbyzustand zeigt beides allen.
  - Die vollständigen Daten leitet er an den **Host** weiter: Code 99 S→C mit Platz, PlayerKey, Bezeichnung und Daten. Der Host erzeugt die Welt und braucht sie als Einziger.
  - Gleiche Daten wie vorher: keine Änderung, keine Weiterleitung.
- **Rechte:** wie beim Startort, nur die eigene Fraktion. Zwei Spieler dürfen dieselbe Ideologie wählen; jede Fraktion erhält ihre eigene Instanz.
- **Fertig** gilt im Fraktionsmodus auch in `Ideology` und verlangt eine eigene Wahl.
- **Weiter zu `Characters`:** wenn alle gewählt haben und fertig sind.
- **Ohne Daten beim Host:** Fehlen dem Host die Daten zu einem Hash, etwa nach einem verpassten Relais, löscht er die Wahl (Abschnitt 3). Der Spieler sendet sie dann erneut.

### 3. Prüfung durch das Spiel des Hosts: Wahl zurückweisen

- **Grund:** Nur das Spiel kann prüfen, ob ein Feld besiedelbar ist, ob Kolonien zu nah beieinander liegen oder ob eine Ideologie gültig ist.
- **Zuständig:** Das Spiel des **Hosts** prüft alle Wahlen, bevor es `SetupAdvance` sendet.
- **Zurückweisen:** Ist eine Wahl ungültig, sendet der Host `FactionChoiceClear` (100):
  - Inhalt: Platz, Art (1 Startort, 2 Ideologie), Grund (1–128 Byte, druckbar).
  - Nur der Host (sonst Code 3), nur in der Phase der jeweiligen Art und nur für einen vorhandenen Platz (sonst Code 6).
  - Der Server löscht die Wahl und das Fertig dieses Spielers und zeigt den Grund in dessen Lobbyeintrag (`FactionNote`).
- **Hinweis-Text:** Er verschwindet mit der nächsten eigenen Wahl dieses Spielers oder beim Phasenwechsel.
- **Vorher prüfen:** Clients sollen schon vor dem Senden selbst prüfen; die Zurückweisung ist die letzte Instanz.

### 4. Plätze bleiben stabil

- **Vergabe** wie ADR 0037: beim Start der Einrichtung, Host 0, dann Beitrittsreihenfolge. Plätze werden nie neu nummeriert, und Wahlen ändern den Platz nie.
- **Austritt vor `Generating`** (neues Spiel): Platz, Erzähler, Startort, Ideologie und Hinweis des Spielers fallen weg.
- **Ab `Generating`:** Alles bleibt am PlayerKey.
- **Reconnect in `Playing`:** Der Zurückkehrende sieht Platz, Erzähler, Startort und Ideologie-Bezeichnung wieder. Ein Spieler ohne Platz wird abgewiesen (ADR 0037).
- **Bindend** sind nach dem Start die Werte im Spielstand (ADR 0046).

### 5. Fortsetzen eines Fraktions-Spielstands (SAVE-002, Fraktionsteil)

- **Wiederherstellen:** Der Host lädt den Spielstand in seinem Spiel, erstellt die Lobby im Fraktionsmodus und sendet `FactionRestore` (101).
  - Inhalt: die Tabelle aus dem Spielstand, 1–64 Einträge mit `PlayerKey`, Platz (0–63) und Erzähler-defName.
  - Keine Doppelten bei Schlüssel und Platz.
- **Annahme:** nur vom Host (Code 3), nur in der Phase `Lobby` einer Fraktionslobby (Code 6). Der Host selbst muss in der Tabelle stehen.
- **Wirkung:**
  - Die Plätze und Erzähler kommen genau so in die Lobby.
  - Die Lobby gilt als **fortgesetzt** (`LobbyView.FactionsRestored`).
  - Alle verlieren ihr Bereit.
  - Erneutes Senden ersetzt die Tabelle.
- **Beitritt:** Er gelingt nur mit einem PlayerKey aus der Tabelle (Code 6). Wer die Lobby vor dem Start verlässt, behält seinen Platz und kann zurückkommen, denn seine Fraktion steht im Spielstand.
- **Start:** `SetupAdvance(Generating)` direkt aus `Lobby`; die Schritte `Scenario` bis `Characters` entfallen (Plan §5.2).
  - Bedingungen wie beim normalen Start: mindestens 2 Spieler, Mods passen, alle bereit.
  - Dazu: Jedes anwesende Mitglied hat einen Platz.
  - `Lobby → Scenario` wird in einer fortgesetzten Lobby abgelehnt.
  - Danach gilt `Generating → Playing` wie bei SETUP-005: Der Host sendet den geladenen Spielstand, alle melden den Fingerabdruck.
- **Fehlende Spieler** behalten ihren Platz; ihre Kolonie läuft ohne sie weiter, sie können in `Playing` beitreten.
- **Sperre:** Auch das Fortsetzen ist ein Fraktionsstart und bleibt ohne Test-Schalter gesperrt (Code 6).
- **Abbruch oder Austritt des Hosts** setzt die Lobby zurück und löscht die Tabelle. Das Spiel des Hosts sendet sie dann erneut.

## Datenvertrag

**Lobbyzustand** (11), Fraktionsblock nach ADR 0037:
- **Wann vorhanden:**
  - Pflicht in Fraktionslobbys ab dem Start der Einrichtung (`LobbyPayloads.HasFactionBlock(mode, phase)`).
  - **Zusätzlich** in der Phase `Lobby` einer fortgesetzten Fraktionslobby. Dort zeigt seine Anwesenheit `FactionsRestored` an.
  - Sonst fehlt er, und der Zustand ist byte-gleich wie vorher.
- **Je Spieler erweitert:**

| Feld | Kodierung |
|---|---|
| Platz, Erzähler | wie ADR 0037 |
| Startort | `bool` + `tileId:u32`, `layerId:u32` |
| Ideologie | `bool` + Bezeichnung (≤ 96 Byte) + SHA-256 (32 Byte) |
| Hinweis | `bool` + Text (≤ 128 Byte) |

- **C#-Felder:**
  - `PlayerView.FactionSite`, `PlayerView.Ideology` (`IdeologySummary`: `Label`, `Hash`), `PlayerView.FactionNote`
  - `LobbyView.FactionsRestored`
- **Größe:** Höchstens etwa 43 KB bei 64 Spielern und vollen Feldern, unter der Frame-Grenze von 64 KiB.

**Nachrichten** (Claude-Bereich 96–111):

| Code | Typ | Richtung | Nutzdaten |
|---|---|---|---|
| 98 | FactionSiteChoice | C→S | `bool` + Feld (`tileId:u32`, `layerId:u32`) |
| 99 | FactionIdeologyChoice | C→S | Bezeichnung (≤ 96 Byte), Daten (`u32`-Länge, 1–49 152 Byte) |
| 99 | FactionIdeologyChoice | S→C, nur an den Host | Platz `u8`, PlayerKey, Bezeichnung, Daten |
| 100 | FactionChoiceClear | C→S, nur Host | Platz `u8`, Art `u8` (1 Startort, 2 Ideologie), Grund (≤ 128 Byte) |
| 101 | FactionRestore | C→S, nur Host | Anzahl `u8` (1–64), je Eintrag PlayerKey, Platz `u8`, Erzähler-defName |

- **Antworten:** `LobbyStateUpdate` an alle, an den Absender mit dessen Request-ID. Fehlercodes 3 und 6 wie oben. Ungültige Nutzdaten ergeben Code 1 und trennen die Verbindung.
- **Reserviert:** 102–111 bleiben frei und bis zur Belegung ungültig.
- **Verträglichkeit (Wire-Version 3):**
  - Andere Builds scheitern an der Mod-Prüfung (ADR 0037).
  - Coop-Zustände und neue Fraktionslobbys vor dem Start bleiben byte-gleich.
  - Die neuen Felder entstehen nur in Fraktionsdaten, die ein anderer Build nie erhält.

## Persistenz, Leistung, Konflikte

- **Persistenz:** Der Koordinator speichert nichts. Die Tabelle des Fortsetzens kommt aus dem Spielstand des Hosts (Spielseite, ADR 0046).
- **Leistung:** Eine Ideologie-Wahl schickt höchstens 48 KiB an den Host, nur wenn sich die Daten ändern. Lobby-Nachrichten haben keine Ratenbegrenzung; das ist wie bei den übrigen Einrichtungsnachrichten bekannt und hingenommen.
- **Konflikte:**
  - Codex nutzt auf `main` Fähnchen und `SiteDecided` nur im Coop; die neue Ablehnung gilt nur im Fraktionsmodus, der gesperrt ist.
  - `StorytellerBlockReason` bleibt. Neu ist `LobbyRegistry.FactionSetupBlockReason(view)` für alle Fraktionsschritte.

## Tests und Rollback

- **Protokoll:**
  - Rundlauf und Grenzen aller neuen Nutzdaten.
  - Lobbyzustand mit neuen Feldern und mit `FactionsRestored`; Coop und neue Fraktionslobby byte-gleich.
  - Frames 98–101 gültig, 102 nicht.
- **Lobby-Kern:**
  - fremde Wahl unmöglich; doppeltes Feld abgelehnt; gleiche Ideologie für zwei erlaubt
  - Fertig- und Weiter-Regeln; Zurückweisen nur durch den Host
  - Plätze stabil über Wahlen, Austritte, Reconnect und Fortsetzen
  - Fortsetzen: Beitritt nur aus der Tabelle, Start nach `Generating`, Sperre
- **Ende zu Ende über `FoundationServer`:**
  - alle Nachrichten mit Rechten
  - Relais der Ideologie nur an den Host; ein Gast kann sich nicht als Relais ausgeben
  - Reconnect und Fortsetzen
- **Spieltests:** `NICHT_GETESTET`.
- **Rollback:** Branch verwerfen. Ohne ihn bleibt der Fraktionsstart gesperrt und Coop unverändert.
