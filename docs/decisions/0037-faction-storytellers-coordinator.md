# ADR 0037 – Fraktionsmodus: Erzähler je Fraktion, gemeinsame Schwierigkeit (Koordinator-Seite)

- **Status:** angenommen für `claude/faction-storyteller`, auf `main` @ `d568766`. Designnotiz nach `AGENTS.md` Regel 4. Aufgabe `FACTION-004` (Bahn Claude). Grundlage ADR 0017 (Einrichtungsphasen), ADR 0016 (PlayerKey), ADR 0031 (Spielmodi). Gegenstück der Spielseite: ADR 0045 (Codex, `FACTION-003`). Es weist der Claude-Bahn Auswahl- und Fertig-Zustand, Prüfung und Verteilung zu; genau das regelt diese ADR. **Nachrichtencodes 96/97** vergeben am 2026-10-16 aus dem neuen Claude-Bereich 96–111 (Benutzer).
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
- Nur ein Test-Schalter erlaubt ihn:
  - `new LobbyRegistry(…, factionsSetupEnabled: true)` im Lobby-Kern.
  - `HostConfig.EnableFactionsSetup` für Ende-zu-Ende-Tests. Die Eigenschaft ist `internal` (nur `Hosting.Tests` sieht sie). Weder die Mod noch eine Konfigurationsdatei kann sie setzen, und `ListenHost` hat keinen Parameter dafür.
- Freigeschaltet wird erst, wenn die Spielseite und ihre Tests fertig sind (Benutzer).

### 6. Vertrag zur Spielseite (abgestimmt mit ADR 0045)

**Zuordnung**
- **`FactionSlot` ist stabil:**
  - Er wird einmal beim Start der Einrichtung vergeben und nie neu nummeriert.
  - Ab `Generating` bleibt er bis zum Ende der Partie, auch über Trennung und Rückkehr hinweg.
  - Er bestimmt die Reihenfolge der Fraktionen. ADR 0045 legt `PlayerKey → Fraktion → StorytellerDef.defName` im Spielstand ab; die Liste dort folgt der Platzreihenfolge.
- **Die Erzählerwahl hängt am `PlayerKey`**, nicht an Sitzung oder Name. `PlayerView.Storyteller` ist die Wahl des Spielers mit diesem Schlüssel.
- **Schwierigkeit:**
  - Die Lobby kennt nur den defName der gemeinsamen `DifficultyDef`.
  - **Benutzerdefinierte Werte** (`DifficultyDef.isCustom`, das Objekt `Difficulty`) stammen ausschließlich aus dem Spielstand des Hosts.
  - Jede Erzähler-Instanz erhält eine gleichwertige Kopie davon; kein Spiel übernimmt Werte aus eigenen Einstellungen.
- **Erzählerliste:** Der Host bietet nur Erzähler an, die mit der gemeinsamen Schwierigkeit laufen. Erzähler mit erzwungener anderer Schwierigkeit lässt er weg (ADR 0045); der Server lehnt dann jede Wahl außerhalb der Liste ab.

**Ereignisautorität**
- Kein Ereignis wird im Spiel des Hosts entschieden und an andere verteilt. Jedes Spiel rechnet alle Erzähler selbst; der Lockstep sorgt dafür, dass alle dasselbe ergeben (ADR 0030).
- Entscheidungen von Spielern, etwa ein Dialog, laufen als Lockstep-Befehle und wirken überall an derselben Tickgrenze (ADR 0045).
- **Abstimmungen** nach ADR 0034 gibt es nur in Coop-Demokratie. Im Fraktionsmodus entscheidet jeder Spieler für seine Fraktion.
- **Schreibzugriff:** Den vanilla-`Game.storyteller` ändert kein Client lokal (ADR 0045).

**Tick-Reihenfolge**
- Je Tick läuft **genau ein Erzähler je Fraktion**, in **aufsteigender Platzreihenfolge**, an der Stelle, an der vanilla den einen Erzähler tickt. Einen zusätzlichen globalen Erzähler gibt es nicht.
- Ob der vanilla-`Game.storyteller` als Erzähler für Platz 0 dient, entscheidet die Spielseite.
- Alle Zufallszüge der Erzähler liegen im kontrollierten `Rand`-Bereich des Ticks (Plan §7.4, ADR 0042).

| Regel | Inhalt |
|---|---|
| R1 | Jede Spielerfraktion hat einen **eigenen Erzähler**: eigene `Storyteller`-Instanz mit eigener `incidentQueue` und eigenen Komponenten. Alle nutzen dieselbe Schwierigkeit. Häufigkeit und Planung sind je Fraktion getrennt. |
| R2 | Ein Ereignis gehört der Fraktion, deren Erzähler es geplant hat. Es zielt nur auf **deren** Karten, Karawanen und Weltobjekte. Ein Erzähler plant nie gegen eine andere Spielerfraktion. |
| R3 | Quests und Questreihen werden **je Fraktion** gestellt: eigenes Gravship je Fraktion (ADR 0031, Antwort 1). |
| R4 **OFFEN** | **Weltweite Ereignisse** (vanilla-Weltbedingungen wie Giftfallout oder Vulkanwinter) haben **noch keine sichere Zuordnung**. **Bis eine Regel entschieden und umgesetzt ist, lösen Erzähler im Fraktionsmodus keine weltweiten Ereignisse aus.** Sie werden nicht blind vervielfacht (ADR 0045). Zur Wahl für den Benutzer: (A) wirken nur auf die Karten der auslösenden Fraktion; (B) plant allein ein Erzähler, für alle Fraktionen. |
| R5 | Die Stärke eines Raids ergibt sich aus dem Vermögen der **Zielfraktion** und der gemeinsamen Schwierigkeit. |
| R6 | Siehe Tick-Reihenfolge: Platzreihenfolge, kontrollierter Zufall. Was ein Erzähler plant, ist damit in jedem Spiel gleich. |

## Datenvertrag

**Lobbyzustand** (`LobbyStateUpdate`, Code 11):
- Der Fraktionsblock folgt nach dem Modus **genau dann**, wenn `LobbyPayloads.HasFactionBlock(mode, phase)` gilt: Fraktionsmodus und Phase ≠ `Lobby`. Dann ist er Pflicht.

| Feld | Kodierung |
|---|---|
| Optionen vorhanden | `bool`; dann defName der Schwierigkeit, Anzahl `u8` (1–32), defNames der Erzähler |
| je Spieler, in Spielerreihenfolge | Platz vorhanden `bool` + Platz `u8` (0–63, eindeutig); Wahl vorhanden `bool` + defName |

- **Ohne Block** (Coop; Fraktionslobby vor dem Start):
  - Der Zustand endet mit dem Modus, byte-gleich wie vor dieser ADR. Zusätzliche Bytes lehnt der Leser ab.
  - Fraktionsdaten lassen sich in einem solchen Zustand nicht kodieren (`ProtocolException`); sie werden also nie still verworfen.
- **C#-Felder:** `LobbyView.FactionOptions`, `PlayerView.FactionSlot`, `PlayerView.Storyteller`. Ohne Block sind alle `null`.
- **defName:** 1–128 Zeichen aus `[A-Za-z0-9_-]`, entsprechend `Verse.Def.AllowedDefNamesRegex`. Prüfen mit `DefNames.IsValid`.

**Nachrichten (C→S)**, Claude-Bereich 96–111 (Benutzer, 2026-10-16):

| Code | Typ | Richtung | Nutzdaten | Antwort |
|---|---|---|---|---|
| 96 | FactionOptions | C→S, nur Host | `FactionOptions.ToPayload()`: defName der Schwierigkeit, Anzahl `u8`, defNames der Erzähler | `LobbyStateUpdate` an alle, an den Absender mit dessen Request-ID; Fehler Code 3 bzw. 6 |
| 97 | StorytellerChoice | C→S | `FactionPayloads.StorytellerChoice(defName)` | wie oben |

- **Fehler:** Ungültige Nutzdaten ergeben Code 1, danach trennt der Server die Verbindung (wie bei allen Nachrichten). Deshalb vor dem Senden mit `DefNames.IsValid` prüfen.
- **Reserviert:** 98–111 sind bis zu ihrer Belegung ungültige Frame-Typen.
- **Warum neue Codes:** Bestehende Nachrichten passen nicht.
  - `SetupSettings` (30) ist reine Anzeige des Hosts.
  - `PlayerReadyChanged` (14) ist ein Fertig-Schalter; die Wahl muss sich aber ohne Fertig ändern lassen.
  - `PawnRequest` (34) gehört zur Charakterwahl.

**Verträglichkeit (Wire-Version bleibt 3)**
- **Andere Builds gelten nicht als verträglich:**
  - Die Mod-Prüfsumme (`ModContentHasher`) umfasst `Assemblies/*.dll` jeder laufenden Mod, also auch unserer.
  - Ein anderer Build zeigt deshalb „Mods stimmen nicht“ (MOD-001). Er kann nicht bereit werden, die Einrichtung nicht starten und in `Playing` nicht beitreten (Code 8).
- **Kein fremder Build sieht die neuen Daten:** Der Fraktionsblock entsteht erst nach dem Start der Einrichtung; bis dahin sind alle Zustände byte-gleich wie bisher.
- **Neue Codes:** 96/97 laufen nur vom Client zum Server. Ein älterer Server würde sie als unbekannten Typ ablehnen, startet aber ohnehin keine Fraktionseinrichtung.

## Persistenz, Leistung, Konflikte

- **Persistenz:** Der Koordinator speichert nichts. Plätze und Wahlen leben im Speicher des Host-Servers. Nach dem Start sind die Werte im Spielstand bindend (Spielseite). Beim Fortsetzen (`SAVE-002`) muss das Spiel des Hosts die Plätze wieder einspielen; dafür braucht es eine weitere Nachricht, **offen**.
- **Leistung:** vernachlässigbar.
- **Konflikte:**
  - `LobbyPayloads.State` hat den Fraktionsblock nach den Regeln oben. Die Spielseite liest über dieselbe Bibliothek; wer selbst eine `LobbyView` mit Fraktionsdaten außerhalb einer begonnenen Fraktionseinrichtung baut, erhält eine `ProtocolException`.
  - Aufgaben-IDs: `FACTION-003` ist die Spielseite (Codex, ADR 0045), `FACTION-004` diese Koordinator-Seite.
  - ADR 0031 wurde auf beiden Branches an derselben Zeile ergänzt (Verweis auf ADR 0045 bzw. 0037). Beim Merge auf beide verweisen.
  - **Abgleich mit ADR 0045:**
    - Das Spiel speichert dort die Wahl je PlayerKey in einer festen Reihenfolge. Diese Reihenfolge sollte die Platzreihenfolge sein: Host zuerst, dann Beitrittsreihenfolge.
    - Erzähler mit erzwungener anderer Schwierigkeit lehnt ADR 0045 ab. Sie gehören nicht in die Liste des Hosts; der Server lehnt dann jede Wahl außerhalb der Liste ab.
    - Für wirklich globale Effekte verlangt ADR 0045 eine einzeln dokumentierte Regel. R4 ist deshalb **offen**: Bis zur Entscheidung keine weltweiten Ereignisse aus Fraktionserzählern.

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
- **Ende zu Ende** über einen echten `FoundationServer` (TCP, Test-Schalter):
  - Optionen nur vom Host; Wahl, Fertig und Weiter über 96/97 und 14.
  - Jede Ablehnung mit dem richtigen Code; ungültige Wahl ergibt Code 1 und trennt die Verbindung, der Platz des Getrennten fällt weg.
  - Weiter bis `Playing`; Rückkehr mit demselben Schlüssel findet Platz, Erzähler und Schwierigkeit; ein neuer Spieler wird abgewiesen.
  - Coop auf demselben Server bleibt ohne Fraktionsdaten.
  - Über `ListenHost`, den echten Weg der Mod, bleibt der Fraktionsstart abgelehnt.
- **Protokoll, zusätzlich:** Coop und Fraktionslobby vor dem Start enden mit dem Modus; zusätzliche Bytes und Fraktionsdaten am falschen Ort werden abgelehnt; der Block ist ab dem Start Pflicht; 96/97 laufen als Frame, 98 nicht.
- **Spieltests:** `NICHT_GETESTET`.
- **Rollback:** Branch verwerfen. Ohne ihn bleibt der Fraktionsstart gesperrt wie bisher.
