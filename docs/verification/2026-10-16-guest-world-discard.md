# Verifikation – Gast verwirft die Einrichtungswelt sauber (SETUP-002/005, Fehlerbehebung)

- **Datum:** 2026-10-16
- **Branch:** `feat/mod-check-setup-ui` (lokal), auf `9343e85`
- **Umgebung:** Host RimWorld `1.6.4871` rev591; Gast rev600 (Bildschirmfotos des Benutzers); .NET SDK 8.0.403.

## Fehlerbild (erster Zwei-Spieler-Lauf des Benutzers, 2026-10-16)

- **Gast-Log:**
  - Zweimal „Exception filling window for FoundationWindow: NullReferenceException“ und einmal „Root level exception in OnGUI(): NullReferenceException at Verse.Find.get_WorldSelector()“.
  - Beides kam genau in der Sekunde, in der der empfangene Spielstand zu laden begann.
  - Landung und Fingerabdruck danach in Ordnung (`players 2; owners 2`).
- **Gast-Log, beide Läufe:** „Faction had no ideoligions after loading. Adding random one.“ beim Laden der Einrichtungswelt (SETUP-002).
- **Host-Fenster nach der Landung:**
  - Es zeigte noch die letzte Einrichtungsseite („Seite des Hosts: Figuren“) mit dem veralteten Hinweis „Eigene Figurenwahl je Spieler folgt mit SETUP-004.“
  - Der Untertitel lautete „gemeinsames Spiel folgt später“.
- Das Host-Log war bis auf die alte `Multiplayer.Foundation.Session.dll` fehlerfrei.

## Ursachen

- `WorldCodec.Discard` rief nur `MemoryUtility.ClearAllMapsAndWorld` auf. Das leert die Welt, lässt sie aber als aktuelle Welt stehen (im Selbsttest belegt: `world=True grid=False`). `Find.World` besteht damit jede Null-Prüfung, während Raster und Oberfläche fehlen. Das Hauptmenü und das Multiplayer-Fenster zeichnen weiter und werfen.
- **Ideoligion-Fehler (WAHRSCHEINLICH, hier nicht nachgestellt):**
  - Die Welt reist vor der Ideoligion-Seite. Mindestens eine Fraktion (vermutlich die des Spielers) hat noch keine Ideoligion.
  - Beim Laden gibt vanilla der Kopie des Gastes eine zufällige und meldet einen Fehler.
  - Die Kopie dient nur der Startortwahl und wird vor dem Spielstand verworfen.
  - Im Selbsttest (rev591) tritt die Meldung nicht auf; der Gast lief mit rev600.

## Behebung

- `Discard` setzt nach dem Leeren auch `Current.Game = null`. So machen es schon der Selbsttest und das Hauptmenü selbst.
- `SetupWorldIdeoErrorPatch` (Prefix auf `Log.Error(string)`) unterdrückt genau diese eine Meldung, und zwar nur während `WorldCodec.Load`. Stattdessen gibt es einen Hinweis im Log. Alle anderen Fehler bleiben unberührt.
- Nach dem Spielstart zeigt das Fenster die Einrichtungsseite des Hosts nicht mehr. Der veraltete Hinweis und der Untertitel sind korrigiert.
- Der Selbsttest setzt für seinen eigenen Lauf `Application.runInBackground`. Ohne Fokus blieb er sonst stehen; die Einstellungen des Benutzers bleiben unverändert.

## Selbsttest im Spiel

`RimWorldWin64.exe -mpWorldSelfTest -mpWorldSelfTestCoverage=0.3 -mpQuitAfterSelfTest`. Neu: Nach „world without a setup page kept“ verwirft der Selbsttest die Gastwelt wie `LoadGuestGame` und prüft `world discard`.

| Lauf | Stand | Ergebnis |
|---|---|---|
| 1 | Beide Behebungen aus, ohne `runInBackground` | Nach dem Weltteil stehen geblieben (Fenster ohne Fokus, `runInBackground=False` in den Einstellungen); Abbruch nach 540 s |
| 2 | Beide Behebungen aus | `world discard FAIL (game=True world=True grid=False)`. Danach 9978 „Root level exception“ in `UIRoot_Entry` (`WorldComponentUtility.WorldComponentOnGUI`, `UIRootUpdate`); der Spielteil kam nicht mehr voran. |
| 3 | Mit Behebungen | `world discard PASS (game=False world=False grid=False)`; 0 „Root level exception“. Welt-, Startort-, Figuren- und Spielteil PASS (`startKept`, `landedChosen`, `site`, `fingerprint` usw. ok). Harmony-Patch ohne Fehler angewandt. |

## Nicht geprüft

- Der Filter für den Ideoligion-Fehler: Die Meldung tritt hier nicht auf. Beim nächsten Gast-Lauf im Gast-Log prüfen, ob statt des Fehlers der Hinweis „setup world loaded before the ideoligion page“ erscheint.
- Der echte Gastablauf mit Übertragung: Der Selbsttest ruft dieselbe `Discard`-Methode auf, aber nicht `LoadGuestGame`.
- Die sechs Harness-Testprojekte (`net10.0`, nur SDK 8 vorhanden; sie decken das geänderte Projekt nicht ab).

## Einordnung nach AGENTS.md Regel 25

`BUILD_ERFOLGREICH` (0 Warnungen); `IN_GAME_GETESTET` (Selbsttest, ein Prozess); `MP_ZWEI_CLIENTS_GETESTET`: Fehlerbild aus dem Benutzer-Lauf, Behebung dort noch nicht erneut geprüft.
