# Verifikation – Alle Funktionen der Host-Seite für Gäste (SETUP-004)

- **Datum:** 2026-10-16
- **Branch:** `feat/mod-check-setup-ui` (lokal), auf `09f5b4d`
- **Umgebung:** RimWorld `1.6.4871` rev591, alle DLCs; .NET SDK 8.0.403.

## Anlass

Zweiter Zwei-Spieler-Lauf des Benutzers (2026-10-16):
- Die Gastseite soll alles können, was die vanilla-Seite des Hosts kann.
- Umbenennen blieb nicht erhalten. Das Host-Log zeigt keine abgelehnte Anfrage, also kam keine an: Gesendet wurde nur mit dem kleinen ✓, und nur der Spitzname war änderbar.

Der Lauf selbst landete beide Spieler (`players 2; owners 2`).

## Umsetzung

Siehe den zweiten Nachtrag in ADR 0022.
- Anfragen `6` (alle drei Namen), `7` (Xenotyp, auch eigener) und `8` (Altersstufe).
- Vanillas Xenotyp-Editor läuft beim Gast.
- Vier Bildvarianten für „Show headgear/Show apparel“.
- Tooltips, Fähigkeiten, Info-Knöpfe beim Besitz, Fertigkeitsnamen in der Sprache des Gastes.
- Der Host fällt auf eine Liste ohne Tooltips zurück, wenn sie nicht in einen Frame passt.
- Die Warnung „GuestCharacterPage probably needs a StaticConstructorOnStartup attribute“ aus dem Benutzer-Log ist behoben: Die Textur ist jetzt ein Instanzfeld.

APIs per Metadaten gegen `Assembly-CSharp.dll` 1.6.4871 geprüft:
- `StartingPawnUtility.GetGenerationRequest`/`SetGenerationRequest`/`RandomizeInPlace`
- `PawnGenerationRequest.AllowedDevelopmentalStages`/`ForcedXenotype`/`ForcedCustomXenotype`
- `CustomXenotype` (`name`, `genes`, `inheritable`, `iconDef`), `Dialog_CreateXenotype(int, Action)`, `CharacterCardUtility.CustomXenotypesForReading`
- `BackstoryDef.FullDescriptionFor`, `Trait.TipString`, `AbilityDef.uiIcon`, `Widgets.InfoCardButton(…, Def)`
- `GenFilePaths.AllCustomXenotypeFiles`

Der Selbsttest ruft vanillas nicht öffentliches `AcceptInner` des Editors über Harmony-`AccessTools` auf (nur im Selbsttest).

## Automatisierte Tests (.NET-8-Harness)

Wie in `2026-10-16-guest-character-page.md`. Alle sechs mit Exit 0.

Neu in Protocol.Tests:
- Namen-Text hin und zurück.
- Xenotyp-Wahl, Standard und eigener mit Genen, hin und zurück; fehlerhafte Texte werden abgelehnt.
- Neue Detailfelder.
- Bildvariante, ungültige Variante.

## Selbsttest im Spiel

`RimWorldWin64.exe -mpWorldSelfTest -mpWorldSelfTestCoverage=0.3 -mpSelfTestScreenshot -mpQuitAfterSelfTest`. Der Selbsttest setzt jetzt `Application.runInBackground` für den eigenen Lauf.

- **Xenotyp-Editor wie beim Gast:** im Spiel noch ohne Startfiguren (`pawnsBefore=0`). Name und ein Gen setzen, annehmen. Rückruf kommt, Ergebnis steht in der Anfrage des Platzes, 0 Fehler im Log.
- **Host-Anfragen auf echten Figuren:**
  - `setName` (alle drei), `setNameNeedsFirst`;
  - `foreignXenotypeRefused`, `setXenotype` + `xenotypeApplied` (neue Figur mit dem Xenotyp, Besitz bleibt), `setCustomXenotype` + `customApplied` (Name und Gen), `unknownGeneRefused`;
  - `setChild`, `backToAdult` (Altersstufe auf der Karte);
  - `tips`.
- **Gastseite:** 8 Karten, 32 Bilder (8 × 4 Ansichten, zusammen etwa 205 KiB, größtes 8,6 KiB), 716 Frames, 0 Zeichenfehler, 0 Fehler im Log.

| Lauf | Ergebnis |
|---|---|
| 1 | Alle Stufen PASS. Der Editor hatte aber `MP Selbsttest.xtp` im Xenotyp-Ordner des Benutzers hinterlassen: Er speichert erst nach dem Annehmen. Die Datei wurde entfernt; die übrigen Xenotyp-Dateien des Benutzers sind unberührt. |
| 2 | Aufräumen auch später und am Ende. Alle Stufen PASS, keine Testdatei übrig. Foto zeigt Ansichtsschalter, Randomize, Altersstufe und Xenotyp, Freigeben, „Xenotype editor“ unten, Info-Knopf beim Besitz. |

## Nicht geprüft

- Zwei echte Spieler mit den neuen Anfragen.
- Klicks von Hand auf die Menüs (Altersstufe, Xenotyp) und Annehmen im Editor per Knopf: Der Selbsttest ruft dieselben Anfragen und `AcceptInner` direkt auf.
- Eigene Xenotypen mit Genen, die beim Host fehlen: Die Anfrage wird abgelehnt (`unknownGeneRefused`); der Gast sieht dann keine Änderung.
- Das Spiel läuft nach der Landung weiter pausiert: SYNC-001 ist nicht umgesetzt. Das ist kein Fehler dieser Arbeit.

## Einordnung nach AGENTS.md Regel 25

`BUILD_ERFOLGREICH` (0 Warnungen); Harness-Tests bestanden; `IN_GAME_GETESTET` (Selbsttest, ein Prozess, ohne Netz); `MP_ZWEI_CLIENTS_GETESTET`: nein (nur der Lauf davor).
