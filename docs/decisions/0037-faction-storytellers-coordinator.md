# ADR 0037 – Fraktionsmodus: Erzähler je Fraktion, gemeinsame Schwierigkeit (Koordinator-Seite)

- **Status:** angenommen für `claude/faction-storyteller`, auf `main` @ `d568766`. Designnotiz nach `AGENTS.md` Regel 4. Aufgabe `FACTION-004` (Bahn Claude). Grundlage ADR 0017 (Einrichtungsphasen), ADR 0016 (PlayerKey), ADR 0031 (Spielmodi). Gegenstück der Spielseite: ADR 0045 (Codex, `FACTION-003`). Es weist der Claude-Bahn Auswahl- und Fertig-Zustand, Prüfung und Verteilung zu; genau das regelt diese ADR.
- **Benutzerentscheidung (2026-10-16, über Codex weitergegeben):**
  - **Coop:** unverändert. Der Host wählt Erzähler und Schwierigkeit.
  - **Fraktionen:**
    - Der Host legt eine **gemeinsame Schwierigkeit** und die globalen Spiel- und Welteinstellungen fest.
    - **Jeder Spieler wählt für seine Fraktion einen Erzähler.**
    - Ereignisplanung und Ereignishäufigkeit sind **je Fraktion getrennt**.
    - Gemeinsam wirkende Weltereignisse brauchen eine ausdrückliche Zuordnungsregel.
  - Die Aussage in ADR 0031 „Erzähler wählt der Host“ gilt damit nur noch für Coop.
- **Ziel (Koordinator, ohne RimWorld):**
  - stabile Zuordnung PlayerKey ↔ Fraktion ↔ `StorytellerDef`
  - Prüfung von Auswahl und Fertig-Bedingung im Server
  - klarer Datenvertrag für die Spielseite
- **Nicht-Ziele:**
  - **Spielseite (Codex):** mehrere Erzähler-Instanzen; Trennung von `Faction.OfPlayer`; Anwenden der Wahl beim Generieren.
  - **Später (`FACTION-001`):** eigener Startort und eigene Ideologie je Fraktion.
  - **Fortsetzen eines Spielstands:** `SAVE-002`.
  - **Freischalten des Fraktionsstarts.**
- **Geprüft an RimWorld 1.6.4871 rev598** (Regel 5, per Reflection gegen die lokale `Assembly-CSharp.dll`):
  - `RimWorld.StorytellerDef` mit `listVisible` und `listOrder` sowie `RimWorld.DifficultyDef` mit `isCustom` sind vorhanden, beide abgeleitet von `Verse.Def`.
  - `RimWorld.Storyteller` hat je Instanz `def`, `difficultyDef`, `difficulty`, `incidentQueue` und `StorytellerTick()`.
  - **Engpass für die Spielseite:** `Verse.Game.storyteller` ist **ein einzelnes Feld**, und `Find.Storyteller` gibt genau diesen Erzähler zurück. Mehrere Erzähler sind in Vanilla nicht vorgesehen.
  - `Verse.Def.AllowedDefNamesRegex` existiert. In der Assembly steht als einziges passendes Muster `^[a-zA-Z0-9\-_]*$`; dass das Feld genau dieses Muster enthält, ist **WAHRSCHEINLICH**.

## Entscheidung

### 1. Fraktionsplatz je PlayerKey

- **Vergabe:**
  - Wenn die Einrichtung beginnt (`Lobby → Scenario`), erhält jedes Mitglied einen **Platz** (0–63): der Host 0, die anderen in Beitrittsreihenfolge.
  - Der Platz hängt am `PlayerKey`, nicht an der Sitzung.
  - Plätze werden nie neu nummeriert; Lücken sind erlaubt.
- **Bedeutung für die Spielseite:**
  - Das Spiel des Hosts erzeugt beim Generieren **genau eine Spielerfraktion je Platz**, in aufsteigender Reihenfolge.
  - Die Zuordnung PlayerKey → Fraktion speichert es im Spielstand.
  - Alle Spiele laden denselben Stand und haben damit dieselbe Zuordnung.
- **Feste Reihenfolge:** Die Erzähler der Fraktionen laufen in jedem Tick in **aufsteigender Platzreihenfolge**. Damit hängt nichts an der Reihenfolge eines Dictionary (Regel 17).
- **Austritt und Rückkehr:**
  - Verlässt ein Gast die Einrichtung **vor `Generating`**, verliert er Platz und Wahl. Während der Einrichtung kann er ohnehin nicht wieder beitreten (ADR 0017).
  - **Ab `Generating`** bleibt der Platz erhalten. Kehrt der Spieler in `Playing` mit demselben PlayerKey zurück, findet er ihn wieder.
- **Beitritt in `Playing`:** Im Fraktionsmodus nur mit vorhandenem Platz; sonst Code 6, „This game has no faction for this player.“ Neue Fraktionen mitten im Spiel sind **offen**.
- **Zurücksetzen:** Abbruch der Einrichtung oder Austritt des Hosts löscht alle Plätze, Wahlen und Optionen.

### 2. Gemeinsame Schwierigkeit und Erzählerliste (nur der Host)

- **Optionen:** In der Phase `Storyteller` sendet der Host `FactionOptions`:
  - die `DifficultyDef` als defName
  - die wählbaren `StorytellerDef`s: 1–32 defNames, ohne Doppelte, in Anzeigereihenfolge
- **Prüfung:** Annahme nur vom Host (sonst Code 3), nur im Fraktionsmodus und nur in der Phase `Storyteller` (sonst Code 6).
- **Gäste können die Schwierigkeit nicht ändern.** Keine Nachricht eines Gastes trägt sie; seine Wahl enthält nur einen Erzähler.
- **Änderung durch den Host:**
  - Ändert er Schwierigkeit oder Liste, verlieren **alle** ihr Fertig.
  - Wahlen, die nicht mehr in der Liste stehen, werden gelöscht.
  - Dieselben Optionen erneut zu senden ändert nichts.
- **Bindend** sind wie im Coop die Werte im Spielstand des Hosts (Plan §6.3); die Lobby führt nur die Auswahl. Eine benutzerdefinierte Schwierigkeit (`DifficultyDef.isCustom`) reist mit dem Spielstand. Die Lobby kennt nur den defName.

### 3. Erzählerwahl je Spieler

- **Nachricht:** `StorytellerChoice(defName)` kann jedes Mitglied senden, auch der Host, für die eigene Fraktion.
- **Bedingungen** (sonst Code 6):
  - nur im Fraktionsmodus, im Coop also nicht
  - nur in der Phase `Storyteller`
  - erst, wenn die Liste des Hosts da ist
  - nur ein Eintrag aus dieser Liste
- **Wechselt** jemand seine Wahl, verliert er sein Fertig. Die Wahl hängt am PlayerKey.

### 4. Fertig-Bedingung

- **Wann es gilt:** Fertig ist die bisherige Bereit-Markierung (ADR 0017). Im Fraktionsmodus gilt sie zusätzlich in der Phase `Storyteller` und setzt dort eine gültige eigene Wahl voraus.
- **Weiter zu `WorldParams`** geht es nur, wenn
  - die Liste vorliegt,
  - jedes verbundene Mitglied eine Wahl aus der Liste hat und
  - alle fertig sind, der Host eingeschlossen.
  - Erzwingen gibt es nicht (wie ADR 0015).
- **Anzeige:** `LobbyRegistry.StorytellerBlockReason(LobbyView)` liefert der UI denselben Grund, den der Server durchsetzt.

### 5. Der Fraktionsstart bleibt gesperrt

- `Lobby → Scenario` wird im Fraktionsmodus weiter abgelehnt (Code 6, ADR 0031).
- Nur `new LobbyRegistry(…, factionsSetupEnabled: true)` erlaubt ihn, für die Tests des Koordinators. Das ist bewusst **nicht** mit `HostConfig` oder `ListenHost` verbunden.
- Freigeschaltet wird erst, wenn die Spielseite und ihre Tests fertig sind (Benutzer).

### 6. Zuordnungsregel für Ereignisse (Vertrag für die Spielseite)

| Regel | Inhalt |
|---|---|
| R1 | Jede Spielerfraktion hat einen **eigenen Erzähler**: eigene `Storyteller`-Instanz mit eigener `incidentQueue` und eigenen Komponenten. Alle nutzen dieselbe Schwierigkeit. Häufigkeit und Planung sind je Fraktion getrennt. |
| R2 | Ein Ereignis gehört der Fraktion, deren Erzähler es geplant hat. Es zielt nur auf **deren** Karten, Karawanen und Weltobjekte. Ein Erzähler plant nie gegen eine andere Spielerfraktion. |
| R3 | Quests und Questreihen werden **je Fraktion** gestellt: eigenes Gravship je Fraktion (ADR 0031, Antwort 1). |
| R4 [A] | Ereignisse, die in Vanilla die **ganze Welt** treffen (Weltbedingungen, z. B. Giftfallout oder Vulkanwinter), wirken im Fraktionsmodus nur auf die Karten der auslösenden Fraktion. So bleibt die Häufigkeit je Fraktion getrennt, und niemand bekommt Ereignisse aus dem Erzähler eines anderen. **Alternative:** Weltbedingungen plant allein der Erzähler des Hosts, für alle Fraktionen. **Die Wahl trifft der Benutzer.** |
| R5 | Die Stärke eines Raids ergibt sich aus dem Vermögen der **Zielfraktion** und der gemeinsamen Schwierigkeit. |
| R6 | Die Erzähler laufen in Platzreihenfolge, und ihre Zufallszüge gehören zum Lockstep (Plan §7.4). Was ein Erzähler plant, ist damit in jedem Spiel gleich. |

## Datenvertrag

**Lobbyzustand** (`LobbyStateUpdate`, Code 11): Im Fraktionsmodus hängt ein Block an, nach dem Modus und optional wie bei ADR 0035:

| Feld | Kodierung |
|---|---|
| Optionen vorhanden | `bool`; dann defName der Schwierigkeit, Anzahl `u8` (1–32), defNames der Erzähler |
| je Spieler, in Spielerreihenfolge | Platz vorhanden `bool` + Platz `u8`; Wahl vorhanden `bool` + defName |

- **C#-Felder:** `LobbyView.FactionOptions` sowie `PlayerView.FactionSlot` und `PlayerView.Storyteller`. Im Coop sind alle drei `null`.
- **defName:** 1–128 Zeichen aus `[A-Za-z0-9_-]`, entsprechend `Verse.Def.AllowedDefNamesRegex`. Strings wie überall als `u16`-Länge plus UTF-8.

**Neue Nachrichten (C→S):** Die Codes sind **noch nicht belegt**. Der Vorschlag wartet auf die Zustimmung des Benutzers.

| Vorschlag | Typ | Richtung | Nutzdaten | Antwort |
|---|---|---|---|---|
| 96 | FactionOptions | C→S, nur Host | defName der Schwierigkeit, Anzahl `u8`, defNames der Erzähler | `LobbyStateUpdate` an alle; Fehler Code 3 bzw. 6 |
| 97 | StorytellerChoice | C→S | defName des Erzählers | wie oben |

- **Warum neue Codes:** Bestehende Nachrichten passen nicht.
  - `SetupSettings` (30) ist reine Anzeige des Hosts.
  - `PlayerReadyChanged` (14) ist ein Fertig-Schalter; die Wahl muss sich aber ohne Fertig ändern lassen.
  - `PawnRequest` (34) gehört zur Charakterwahl.
- **Belegte Bereiche:** 71–79 (Claude) sind voll, 80–95 gehören Codex, 37–63 Sven.
- **Vorschlag:** neuer Claude-Bereich **96–111**.
- **Bis zur Zustimmung** sind Payloads, Regeln und Tests fertig. Nur die Verdrahtung im Server (`FoundationServer`) fehlt.

## Persistenz, Leistung, Konflikte

- **Persistenz:** Der Koordinator speichert nichts. Plätze und Wahlen leben im Speicher des Host-Servers. Nach dem Start sind die Werte im Spielstand bindend (Spielseite). Beim Fortsetzen (`SAVE-002`) muss das Spiel des Hosts die Plätze wieder einspielen; dafür braucht es eine weitere Nachricht, **offen**.
- **Leistung:** vernachlässigbar.
- **Konflikte:**
  - `LobbyPayloads.State` erhält einen weiteren optionalen Block am Ende. Die Spielseite liest über dieselbe Bibliothek und muss nichts anpassen.
  - Aufgaben-IDs: `FACTION-003` ist die Spielseite (Codex, ADR 0045), `FACTION-004` diese Koordinator-Seite.
  - ADR 0031 wurde auf beiden Branches an derselben Zeile ergänzt (Verweis auf ADR 0045 bzw. 0037). Beim Merge auf beide verweisen.
  - **Abgleich mit ADR 0045:**
    - Das Spiel speichert dort die Wahl je PlayerKey in einer festen Reihenfolge. Diese Reihenfolge sollte die Platzreihenfolge sein: Host zuerst, dann Beitrittsreihenfolge.
    - Erzähler mit erzwungener anderer Schwierigkeit lehnt ADR 0045 ab. Sie gehören nicht in die Liste des Hosts; der Server lehnt dann jede Wahl außerhalb der Liste ab.
    - Für wirklich globale Effekte verlangt ADR 0045 eine einzeln dokumentierte Regel. R4 hier ist ein Vorschlag dafür und wartet auf den Benutzer.

## Tests und Rollback

- **Protokoll:**
  - Rundlauf von Optionen, Wahl und Lobbyzustand mit und ohne Fraktionsblock.
  - Grenzen: leere Liste, mehr als 32 Einträge, Doppelte, ungültige defNames.
- **Lobby-Kern:**
  - Plätze und Reihenfolge; Coop abgelehnt.
  - Nur der Host setzt Optionen, nur in der Phase `Storyteller`.
  - Wahl erst nach Liste und nur aus der Liste.
  - Fertig verlangt eine Wahl; Wahlwechsel und Optionsänderung löschen Fertig.
  - Weiter nur, wenn alle eine Wahl haben und fertig sind.
  - Austritt vor und nach `Generating`; Beitritt in `Playing` nur mit Platz; Abbruch löscht alles.
  - Der Start bleibt ohne den Test-Schalter gesperrt.
- **Ende zu Ende über `ListenHost`:** folgt mit der Code-Vergabe.
- **Spieltests:** `NICHT_GETESTET`.
- **Rollback:** Branch verwerfen. Ohne ihn bleibt der Fraktionsstart gesperrt wie bisher.
