# ADR 0017 – Serverseitige Regeln der Spieleinrichtung

- **Status:** angenommen für `feat/setup-rules`. Designnotiz nach `AGENTS.md` Regel 4. Setzt die Serverseite von `START-001`, `SETUP-003` und `SETUP-004` nach ADR 0015 um; Grundlage ADR 0013 (Listen-Host) und ADR 0016 (PlayerKey).
- **Ziel:** Der eingebettete Server des Hosts führt die Phasen der gemeinsamen Spieleinrichtung und setzt die Benutzerregeln selbst durch, nicht nur die UI:
  - Start nur, wenn alle bereit sind.
  - Fähnchen von allen.
  - Startort wählt nur der Host.
  - Charakterwahl geht erst weiter, wenn alle fertig sind.
- **Nicht-Ziele:**
  - UI und Spiel-Hooks (Start-Knopf, vanilla-Seiten, Weltkarte, Fähnchen-Darstellung): `SETUP-001` und folgende.
  - Mod-Abgleich als Startbedingung: `MOD-001`.
  - Welt-Fingerabdruck als Bedingung für `WorldTransfer → SiteSelection`: `SETUP-002`.
  - Pawn-Besitz und „Fertig verlangt mindestens einen Pawn“: `SETUP-004` (Spielseite).
  - Phasen nach `Generating`: `SETUP-005`.
- **Zustandsmodell** (`LobbyRegistry`, unter dem bestehenden Lock):

  | Phase | Weiter durch | Bedingung |
  |---|---|---|
  | `Lobby` | Host: `SetupAdvance(Scenario)` | ≥ 2 Spieler, alle bereit, Host eingeschlossen |
  | `Scenario`, `Storyteller`, `WorldParams` | Host: `SetupAdvance(nächste)` | keine |
  | `WorldTransfer` | Host: `SetupAdvance(SiteSelection)` | vorerst keine; `SETUP-002` ergänzt die Fingerabdruck-Bedingung |
  | `SiteSelection` | Host: `SiteDecided(Feld)` | nur der Host; `SetupAdvance` ist hier nicht erlaubt |
  | `Ideology` | Host: `SetupAdvance(Characters)` | keine |
  | `Characters` | Host: `SetupAdvance(Generating)` | alle verbundenen Mitglieder fertig, Host eingeschlossen, ohne Erzwingen |
  | `Generating` | – | weitere Phasen kommen mit `SETUP-005` |

  - `SetupAdvance` nennt die **Zielphase**. Der Server nimmt nur die unmittelbar nächste an; ein doppelt gesendeter Klick überspringt keine Phase.
  - `SetupAbort` (Host, jede Phase außer `Lobby`) setzt zurück auf `Lobby`.
- **Bereit/Fertig:** dieselbe Markierung je Spieler, gültig nur in `Lobby` (Bereit) und `Characters` (Fertig). In anderen Phasen lehnt der Server sie ab. Bei **jedem** Phasenwechsel und beim Zurücksetzen wird sie für alle gelöscht, sodass ein „Bereit“ aus der Lobby nie als „Fertig“ zählt. Fällt ein Gast weg, zählt er nicht mehr (ADR 0015 [A]).
- **Fähnchen:** `SiteFlag(Feld | leer)` ist nur in `SiteSelection` gültig, je Spieler höchstens eines. Ein neues ersetzt das alte, leer entfernt es. Fähnchen stehen im Lobbyzustand beim jeweiligen Spieler. Mit `SiteDecided` speichert der Server das gewählte Feld im Lobbyzustand, löscht alle Fähnchen und wechselt zu `Ideology`. Der Server prüft kein Weltwissen; ob ein Feld besiedelbar ist, prüft das Spiel des Hosts vor dem Senden [U].
- **Feld:** RimWorld 1.6 adressiert Weltfelder als `RimWorld.Planet.PlanetTile` mit `int tileId` und `int layerId` (Odyssey-Ebenen, per Reflection gegen die lokale `Assembly-CSharp.dll` geprüft). Das Protokoll überträgt deshalb beide Werte (`SiteTile`), nicht nur eine Feldnummer; beide müssen ≥ 0 sein.
- **Sperre:** Außerhalb von `Lobby` lehnt der Server Beitritte ab (Code 6, „Setup in progress.“). Die Lobbyliste zeigt die Phase, damit die UI `[Einrichtung]` anzeigen kann.
- **Host verlässt:** Während der Einrichtung setzt der Server erst auf `Lobby` zurück, dann geht die Host-Rolle wie bisher an das nächste Mitglied. Nur der Host hält den erzeugten Zustand (Plan §6.2). Im Listen-Host-Betrieb endet mit dem Host ohnehin der Server.
- **Protokoll (Wire-Version 3):**

  | Code | Typ | Richtung | Nutzdaten |
  |---|---|---|---|
  | 24 | SetupAdvance | C→S | Zielphase `u8` |
  | 25 | SetupAbort | C→S | leer |
  | 26 | SiteFlag | C→S | `bool` + optional Feld |
  | 27 | SiteDecided | C→S | Feld |

  Antwort ist jeweils der vollständige Lobbyzustand (`LobbyStateUpdate`) an alle Mitglieder, wie bei Bereit; Regelverstöße kommen als `Error` mit Code 3 (nicht Host) oder 6 (falsche Phase / Bedingung fehlt).
  - Lobbyzustand neu: Phase, gewähltes Feld, je Spieler sein Fähnchen.
  - Lobbyliste neu: Phase.
- **Persistenz:** keine; der Zustand lebt im Speicher des Hosts.
- **Netzwerk-Sync:** Jede Änderung erhöht die Revision; Clients verwerfen ältere Revisionen wie bisher.
- **Konflikte:** Codes 24–27 und die erweiterten Lobby-Nutzdaten müssen beim Merge mit parallelen Branches abgeglichen werden.
- **Performance:** vernachlässigbar.
- **Tests:**
  - Lobby-Kern: jede Regel und jede Ablehnung.
  - Protokoll: Rundlauf und Grenzen.
  - Ende zu Ende über `ListenHost`: Ein Gast versucht jede Host-Aktion und wird abgelehnt; Start und Charakter-Weiter werden erst mit allen Bereit angenommen; ein Beitritt während der Einrichtung wird abgelehnt; der Host bricht ab.
  - Spieltests derzeit nicht möglich (Benutzer): `NICHT_GETESTET` im Spiel.
- **Rollback:** Branch verwerfen; die bisherigen Lobby-Nachrichten 1–23 behalten ihre Bedeutung.
