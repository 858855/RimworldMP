# Verifikation – Gastseite der Charakterwahl wie vanilla (SETUP-004)

- **Datum:** 2026-10-16
- **Branch:** `feat/mod-check-setup-ui` (lokal), auf `dff1142`
- **Umgebung:** RimWorld `1.6.4871` rev591, alle DLCs; .NET SDK 8.0.403.

## Anlass

Benutzer-Rückmeldung nach dem ersten Zwei-Spieler-Lauf: Das Figurenfenster des Gastes war eine Textliste. Es soll aussehen wie vanillas „Create characters“-Seite. Siehe den Nachtrag in ADR 0022.

## Umsetzung

- **Protokoll:**
  - `PawnCard` hat optionale `PawnCardDetails`: Namensteile, Geschlecht, Alter, Xenotyp mit Symbolschlüssel, Lieblingsfarbe, Gesundheit nach Körperteil, Beziehungen mit beiden Meinungen, Besitz.
  - Neu: `PawnPortrait` (37, PNG ≤ 48 KiB).
  - Server: nur vom Host, nur in `Characters`, an alle anderen.
- **Host:**
  - `CharacterHost` füllt die Details.
  - `RenderPortrait` zeichnet mit `PortraitsCache.Get` in vanillas Bildgröße (ohne Kopfbedeckung, mit Kleidung) und kodiert PNG.
  - `PublishPortraits` sendet höchstens zwei Bilder je Durchlauf und erneut nur, wenn sich das Aussehen ändert (`PortraitKey`).
- **Gast:** `GuestCharacterPage` in Fenstergröße `Page.StandardSize`:
  - Titel; Liste „Selected“ / „Left behind“ mit Bild, Hintergrund und Besitzer.
  - Namensfelder; der Spitzname ist bei der eigenen Figur editierbar und wird mit ✓ übernommen.
  - Bild, Geschlecht und Alter, Xenotyp mit Symbol, Fraktion mit Lieblingsfarbe.
  - Drei Spalten: Kindheit/Erwachsen, Merkmale, Unfähig zu | Fertigkeiten mit Flammen und Balken | Gesundheit, Beziehungen, Besitz.
  - Team-Fertigkeiten; „Chat / Lobby“ und „Fertig“ an vanillas Plätzen von Back/Start.
  - Besitz-Knöpfe (Randomize, Freigeben, Übernehmen, Tauschen) dort, wo vanilla Randomize hat.
  - Texte über vanillas Übersetzungsschlüssel, sonst Deutsch.
- API-Signaturen per Metadaten gegen `Assembly-CSharp.dll` 1.6.4871 geprüft. Darunter: `PortraitsCache.Get`, `Page_ConfigureStartingPawns.PawnPortraitSize`, `SkillUI.PassionMinorIcon`/`PassionMajorIcon`, `GenderUtility.GetIcon`, `Pawn_StoryTracker.favoriteColor` (`ColorDef`), `CustomXenotype.iconDef`, `GameInitData.startingPossessions`, `PawnRelationUtility.GetMostImportantRelation`, `Page.StandardSize`. Der Build gegen dieselbe Assembly hat 0 Warnungen.

## Automatisierte Tests (.NET-8-Harness)

Die Testprojekte zielen auf `net10.0`; installiert ist nur SDK 8. Der Harness im Sitzungs-Scratchpad kompiliert die unveränderten Repo-Testquellen gegen die Repo-Projekte mit `net8.0`. `ServerConfig.cs` wird direkt einkompiliert, weil der Server `net10.0` ist.

| Projekt | Ergebnis |
|---|---|
| Protocol.Tests | Exit 0 (neu: Details und `PawnPortrait` hin und zurück, Größengrenzen) |
| Lobby.Tests | Exit 0 (neu: Bilder nur vom Host, nur in `Characters`, an den Gast) |
| Network.Tests | Exit 0 |
| Hosting.Tests | Exit 0 (neu: Bild über den echten Host zum Gast, Gast darf keins senden: Code 3) |
| Integration.Tests | Exit 0 |
| Client.Tests | Exit 0 |

## Selbsttest im Spiel

`RimWorldWin64.exe -mpWorldSelfTest -mpWorldSelfTestCoverage=0.3 -mpSelfTestScreenshot -mpQuitAfterSelfTest`. Neue Stufe nach dem Figurenteil:

1. Auf den echten vanilla-Startfiguren übernimmt ein Testspieler die erste Figur.
2. Liste und Bilder laufen durch die Wire-Formate.
3. Die Gastseite wird 3 s in einem Fenster gezeichnet, mit Bildschirmfoto nach 2 s.

| Lauf | Ergebnis |
|---|---|
| 1 | **PASS**: 8 Karten, 8 Bilder (zusammen 50 KiB, größtes 7876 B), Details vollständig, 716 Frames, 0 Zeichenfehler. Auf dem Foto lag das große Bild hinter dem Besitz-Kasten. |
| 2 | Bild neben die Namensfelder, Umbenennen als ✓. **PASS**: 8/8 Bilder (61 KiB, größtes 9740 B), 718 Frames, 0 Fehler. Foto entspricht dem vanilla-Aufbau. Alle anderen Stufen PASS (Welt, Startort, Figuren, Gastwelt verwerfen, Spielstart). |

Das Foto liegt unter `SaveData/MultiplayerFoundation/selftest-guest-page.png` (nicht im Repo, enthält Spielgrafik).

## Nicht geprüft

- Zwei echte Spieler: Bilder über Steam, Aktualisierung nach Randomize und Xenotyp-Editor, Umbenennen per ✓.
- Andere Sprachen beim Gast als beim Host: Fertigkeiten und Beziehungen kommen in der Sprache des Hosts.
- Fähigkeiten (Abilities) und Tooltips der vanilla-Karte fehlen.

## Einordnung nach AGENTS.md Regel 25

`BUILD_ERFOLGREICH` (0 Warnungen); Harness-Tests bestanden; `IN_GAME_GETESTET` (Selbsttest, ein Prozess, ohne Netz); `MP_ZWEI_CLIENTS_GETESTET`: nein.
