# ADR 0020 – Weltübertragung und Welt-Fingerabdruck (SETUP-002)

- **Status:** angenommen für `feat/mod-check-setup-ui`. Designnotiz nach `AGENTS.md` Regel 4. Plan §6.3 (Weltübertragung), Aufgabe `SETUP-002`; beantwortet den Laufzeitteil von `VER-001` „Welt ohne Karte speichern und laden“. Grundlage ADR 0014 (Chunk-Kanal), 0017 (Phasen), 0019 (Seitenspiegel).
- **Ziel:** Nur der Host erzeugt die Welt. Alle Gäste bekommen genau diese Welt und beweisen mit einem Fingerabdruck, dass ihr Spiel daraus dieselbe Welt macht. Erst dann geht es zur Startortwahl.
- **Ablauf:**
  1. Der Host klickt auf „Welt erstellen“; RimWorld erzeugt die Welt wie gewohnt. Sobald die Startortseite offen ist, schaltet die Mod den Server nach `WorldTransfer`.
  2. In einem langen Ereignis ruft der Host `WorldGrid.StandardizeTileData()` auf, berechnet den Fingerabdruck und speichert die Welt mit Scribe (`Scribe_Deep.Look(ref world, "world")` in eine eigene Datei, Wurzel `mpWorld`), gzip.
  3. `WorldReady` (31) meldet dem Server den Fingerabdruck; danach geht die Datei als Chunk-Übertragung der Art `World` an alle Gäste. Allein (Debug) entfällt die Übertragung.
  4. Der Gast lädt sie im Hauptmenü in ein frisches Einrichtungsspiel (`Game` mit `GameInitData` und dem Szenario aus dem Seitenspiegel) und meldet mit `WorldLoaded` (32) seinen Fingerabdruck.
  5. Der Server zeigt je Spieler `WorldStatus` (wartet / passt / weicht ab) und öffnet `SiteSelection` erst, wenn alle passen. Abweichung: Code 8, der Host sieht die Namen und kann abbrechen. Eine neue Host-Welt (zurück und neu erzeugt) setzt alle Meldungen zurück.
- **Laden auf dem Gast:** Die Welt muss beim Laden schon die aktuelle Welt sein, weil `WorldGrid.ExposeData` über `Find.World` die Planetenabdeckung liest (NullReferenceException in `CalculateViewCenterAndAngle`, Selbsttest 2026-10-15). Deshalb legt die Mod das `World`-Objekt selbst an, setzt `Current.Game.World`, lädt mit `Scribe.EnterNode("world")` + `world.ExposeData()` und meldet es bei `crossRefs` und `initer` an, wie `Scribe_Deep` es täte. Danach `FinalizeLoading()`, `World.FinalizeInit(true)`, `StandardizeTileData()`.
- **Fingerabdruck:** SHA-256 über die **diskreten** Teile: Seed und Abdeckung, je Oberflächenfeld Biom und Hügel, Fluss- und Straßenzahl, Fraktionen (Lade-ID, Def, Name), Siedlungen (Feld, Fraktion, Name). Kontinuierliche Feldwerte fehlen bewusst: Der Spielstand speichert sie gröber als die laufende Welt, Sumpfigkeit (±0,01) und Höhe (±1) wichen im Selbsttest an Rundungsgrenzen ab, auch nach `StandardizeTileData`. Gleichheit der Bytes beweist bereits der SHA-256 der Übertragung; der Fingerabdruck beweist, dass das Spiel des Gasts dieselben Biome, Fraktionen und Weltobjekte daraus macht.
- **Selbsttest:** `RimWorldWin64.exe -mpWorldSelfTest [-mpWorldSelfTestCoverage=0.3] [-mpQuitAfterSelfTest]` erzeugt im Hauptmenü eine Welt, speichert, verwirft und lädt sie wie ein Gast und vergleicht. Ergebnis im `Player.log`. Ergebnisse 2026-10-15 (Kern + 5 DLCs):

  | Abdeckung | Felder | Siedlungen | gzip | Erzeugen | Speichern | Laden |
  |---|---|---|---|---|---|---|
  | 5 % | 3 787 | 13 | 0,12 MB | 0,8 s | 0,13 s | 0,2 s |
  | 30 % | 119 904 | 95 | 0,84 MB | 5,2 s | 0,33 s | 1,0 s |
  | 50 % | 295 732 | 234 | 1,9 MB | 13 s | 0,57 s | 1,75 s |

- **Grenzen:** Der Gast sieht die Weltkarte noch nicht (Fähnchen: `SETUP-003`). Die Weltdatei liegt zwischendurch unter `SaveData/MultiplayerFoundation/` (`world-out.xml`, `world-in.xml`). Bei Abbruch verwirft der Gast seine Einrichtungswelt (`MemoryUtility.ClearAllMapsAndWorld`).
- **Tests:** Protocol (Fingerabdruck, Rundung, Nutzdaten), Lobby (Regeln: nur Host meldet, nur in der Phase, Abweichung Code 8, Neuanfang setzt zurück), Hosting (3 MB über Steam-ähnlichen Strom mit Fingerabdruck-Freigabe), Selbsttest im Spiel. Zwei-Spieler-Lauf: **offen**.
- **Rollback:** Commits zurücknehmen; ohne Fingerabdruck-Bedingung gilt wieder ADR 0017 („vorerst keine Bedingung“).
