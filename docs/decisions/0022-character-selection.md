# ADR 0022 – Figurenwahl je Spieler (SETUP-004, Spielseite)

- **Status:** angenommen für `feat/mod-check-setup-ui`. Designnotiz nach `AGENTS.md` Regel 4. Setzt die Charakterwahl aus ADR 0015 um (kein Erzwingen, alle fertig); Phasenregeln aus ADR 0017 unverändert.
- **Ziel:** Jeder Spieler übernimmt genau eine Figur der Startgruppe, kann sie neu würfeln, mit einer zurückgelassenen tauschen und umbenennen und meldet sich fertig. Weiter geht es erst, wenn alle fertig sind.
- **Wo die Figuren leben:** nur im Spiel des Hosts, in der vanilla-Liste `GameInitData.startingAndOptionalPawns` (Startgruppe = die ersten `startingPawnCount`). Alle Zufallswürfe macht vanilla beim Host (`StartingPawnUtility`). Die Gäste bekommen nur Karten zur Anzeige; ihre Spiele erzeugen keine Figuren.
- **Protokoll (Wire-Version 3):**
  - `PawnRoster` (Code 33, Host → Server → Gäste): alle Figuren als Karten, Startgruppe zuerst: Thing-ID, Startgruppe ja/nein, Besitzer (`PlayerKey`), Name, Kurzname, Kurzbeschreibung (Geschlecht, Alter, Xenotyp), Kindheit, Erwachsenenleben, Merkmale, Fertigkeiten (Stufe, `255` = unfähig, Leidenschaft), Unfähigkeiten, sichtbare Gesundheit. Höchstens 24 Karten und 24 Einträge je Liste, ein Frame.
  - `PawnRequest` (Code 34, Gast → Server → Host): Art (`1` übernehmen, `2` freigeben, `3` neu würfeln, `4` tauschen, `5` umbenennen), Figur, andere Figur, Text. Der Server setzt den `PlayerKey` des Absenders ein; was der Gast mitschickt, zählt nicht.
- **Server:** nimmt `PawnRoster` nur vom Host und nur in `Characters` an. Je Spieler bildet er einen SHA-256 über dessen Karten; ändert sich der Wert (neu gewürfelt, getauscht, umbenannt, anderer Besitz) oder besitzt der Spieler keine Figur der Startgruppe mehr, wird sein Fertig gelöscht. Fertig verlangt eine eigene Figur der Startgruppe (Code 6). `PawnRequest` nur von Gästen und nur in `Characters`; der Host ändert Figuren in seinem eigenen Spiel. Phasenwechsel und Abbruch löschen die Liste.
- **Host (`CharacterHost`):**
  - Besitz wird nach Thing-ID gehalten. Ersetzt vanilla eine Figur an derselben Stelle (Neu würfeln auf der vanilla-Seite), erbt die neue Figur den Besitzer. Figuren von Spielern, die gegangen sind, werden frei.
  - Die Startgruppe wird auf eine Figur je Spieler angehoben: erst die erste zurückgelassene Figur, sonst `StartingPawnUtility.AddNewPawn`.
  - Anfragen: Übernehmen nur in der Startgruppe, nur freie Figuren, eine je Spieler. Freigeben, neu würfeln (`RandomizeInPlace`), umbenennen (Spitzname, 1–24 druckbare Zeichen) nur die eigene. Tauschen nur die eigene Figur der Startgruppe gegen eine freie zurückgelassene; die Plätze wechseln und `ReorderRequests` hält die vanilla-Erzeugungswünsche je Platz passend.
  - Der Host übernimmt die gerade auf der vanilla-Seite gezeigte Figur über sein Feld oben rechts und meldet sich dort fertig. Die Liste wird nur gesendet, wenn sie sich geändert hat.
- **Gast:** Beim Wechsel auf `Characters` öffnet sich das Figurenfenster: links Startgruppe und zurückgelassene Figuren mit Besitzern, rechts die Karte und die erlaubten Knöpfe, unten **Fertig** (nur mit eigener Figur) und **Chat / Lobby**. Abgelehnte Anfragen meldet der Host im Log; die Anzeige folgt immer der nächsten Liste.
- **Start:** Der letzte vanilla-Start bleibt gesperrt (SETUP-005); die Meldung nennt jetzt, auf wen gewartet wird.
- **Persistenz:** keine. Der Besitz gilt nur in der Einrichtung; der gemeinsame Start (SETUP-005) und der Spielstand (OWN-001) übernehmen ihn.
- **Tests:** Lobby-, Hosting- und Protokolltests für Liste, Anfragen, Fertig-Regeln. Selbsttest im Spiel (`-mpWorldSelfTest`): Host-Logik auf echten vanilla-Startfiguren, alle Prüfungen PASS. Zwei-Spieler-Lauf: **offen**.
- **Rollback:** Commits zurücknehmen; die Charakterwahl hat dann wieder keine Figurenzuordnung.
- **Nachtrag 2026-10-16 (Gastseite wie vanilla):**
  - Der Benutzer wollte die Gastseite wie vanillas „Create characters“, nicht als Textliste.
  - Die Figuren bleiben beim Host. Die Karten tragen jetzt Details (Namensteile, Alter, Xenotyp mit Symbol, Lieblingsfarbe, Gesundheit nach Körperteil, Beziehungen mit beiden Meinungen, Besitz).
  - Der Host zeichnet jede Figur mit `PortraitsCache` und sendet sie als PNG (`PawnPortrait`, Code 37). Höchstens zwei Bilder je Durchlauf, erneut nur bei neuem Aussehen.
  - `GuestCharacterPage` baut die vanilla-Seite nach: Liste mit Bildern, Namensfelder, Karte in drei Spalten, Team-Fertigkeiten, Knöpfe an vanillas Stellen. Texte nimmt sie über vanillas Übersetzungsschlüssel, wo es sie gibt.
  - Verworfen: echte Figuren beim Gast (Scribe der Figuren samt Ideoligion und Verwandten), um vanillas eigene Karte zu nutzen. Zu viele Querverweise, die beim Gast fehlen, und jede Änderung müsste ganze Figuren neu übertragen.
  - Nachweis: `docs/verification/2026-10-16-guest-character-page.md`.
- **Nachtrag 2026-10-16 (alle Funktionen der Host-Seite):** Der Benutzer wollte beim Gast alle Möglichkeiten der vanilla-Seite. Neue Anfragen:
  - Alle drei Namen (`6`). Das Feld wird beim Verlassen oder mit Enter übernommen, ohne ✓; vorher kam ein Umbenennen ohne ✓ nie an.
  - Xenotyp (`7`), auch eigene Xenotypen.
  - Altersstufe (`8`).

  Der Host setzt bei Xenotyp und Altersstufe die Generierungsanfrage des Platzes und erzeugt neu (`StartingPawnUtility`), so bleiben alle Würfe beim Host.

  Der **Xenotyp-Editor** ist vanillas eigener (`Dialog_CreateXenotype`) und läuft beim Gast. Sein Ergebnis landet in der leeren Generierungsanfrage des Gast-Spiels für den Platz; der Rückruf schickt es als Anfrage `7` an den Host. Gene werden beim Host über Def-Namen aufgelöst.

  Außerdem:
  - Ansichten „Show headgear/Show apparel“ (vier Bildvarianten vom Host).
  - Tooltips für Hintergründe und Merkmale (Text vom Host), Fertigkeiten und Xenotyp (aus den Defs des Gastes).
  - Fähigkeiten-Symbole und Info-Knöpfe beim Besitz.
  - Fertigkeitsnamen in der Sprache des Gastes.

  Nicht nachgebaut: vanillas Info-Karte der Figur (braucht die Figur), Ziehen zum Umsortieren (Tauschen-Knopf statt dessen). Nachweis: `docs/verification/2026-10-16-guest-page-functions.md`.
