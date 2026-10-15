# ADR 0021 – Startort-Fähnchen auf der Weltkarte (SETUP-003, Spielseite)

- **Status:** angenommen für `feat/mod-check-setup-ui`. Designnotiz nach `AGENTS.md` Regel 4. Setzt die Spielseite von ADR 0015 um; Serverregeln aus ADR 0017 (`SiteFlag`, `SiteDecided`) unverändert. Grundlage ADR 0020 (der Gast hat die Welt geladen).
- **Ziel:** Jeder setzt ein Fähnchen als Vorschlag, alle sehen alle Fähnchen live, nur der Host wählt.
- **Gast:** Wechselt die Phase auf `SiteSelection` und hat der Gast die Welt geladen, öffnet die Mod die vanilla-Seite `Page_SelectStartingSite` und das Multiplayer-Feld. Harmony-Prefixe, **nur während der eigenen Gast-Karte aktiv**:
  - `Page_SelectStartingSite.DoNext` und `OnAcceptKeyPressed`: setzen das Fähnchen auf das ausgewählte Feld statt weiterzugehen; so gerät ein Gast nie in die Einzelspieler-Einrichtung.
  - `Page.DoBack` (nur für diese Seite): schließt nur die Karte; „Weltkarte öffnen“ im Multiplayer-Fenster öffnet sie wieder.
- **Fähnchen setzen:** Vorher die vanilla-Siedlungsprüfung `TileFinder.IsValidTileForNewSettlement(tile, reason, false)`; bei Ablehnung eine Meldung mit Grund. „Entfernen“ sendet ein leeres Fähnchen.
- **Anzeige:** Postfix auf `Page_SelectStartingSite.ExtraOnGUI` zeichnet für jedes Fähnchen eine Stange, eine Farbe je Spieler und den Namen über dem Feld (`WorldGrid.GetTileCenter` → `GenWorldUI.WorldToUIPosition`; Felder auf der Rückseite werden mit `WorldRendererUtility.HiddenBehindTerrainNow` ausgelassen). Das Feld oben rechts listet bis zu vier Fähnchen mit Biom und „Hin“ (Kamera und Auswahl springen dorthin). Gilt für Host und Gäste.
- **Host:** wählt wie bisher mit „Weiter“ auf seiner Seite; die Mod meldet `SiteDecided`. Danach schließen die Gäste ihre Karte und sehen „Startort (vom Host gewählt)“.
- **Persistenz:** keine; Fähnchen leben nur im Lobbyzustand.
- **Tests:** Serverregeln in Lobby- und Hosting-Tests (ADR 0017). Selbsttest im Spiel: die Startortseite öffnet sich auf der wie beim Gast geladenen Welt, zwei Testfähnchen werden gezeichnet (774 Frames), Bildschirmfoto bestätigt Fähnchen und Namen. Zwei-Spieler-Lauf: **offen**.
- **Rollback:** Commit zurücknehmen; Gäste sehen dann keine Karte, die Serverregeln bleiben.
