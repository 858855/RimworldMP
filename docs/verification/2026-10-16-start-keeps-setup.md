# Verifikation – Spielstart behält Figuren und Startort (SETUP-005, Fehlerbehebung)

- **Datum:** 2026-10-16
- **Branch:** `feat/mod-check-setup-ui` (lokal), auf `08e7f9b`
- **Umgebung:** RimWorld `1.6.4871` (rev591), Kern und alle DLCs; .NET SDK 8.0.403.

## Fehlerbild (Benutzer-Lauf allein über die Debug-Option, Video und `Player.log`)

- Gewählt: Startgruppe Dominguez, Jen, Kiri. Der Host hat „Irma Dominguez“ übernommen und war fertig. Startort: Trockenbuschland (Feld 162724).
- Gelandet: Mayumi, Lachi, Tsumi auf einem anderen Feld, in einer anderen Gegend.
- `Player.log`: `NullReferenceException` in `RimWorld.PageUtility.<InitGameStart>g__PreLoadAct|1_0`, danach „Initializing new game“. Der gesendete Spielstand hatte `owners 0`. Der Lauf davor zeigte denselben Ablauf.

## Ursache

- Vanilla schließt die Figurenseite direkt nach `nextAct` (`Page.DoNext`). Die Mod fragt an dieser Stelle zuerst den Server. Bis die Antwort kommt, ist im Hauptmenü nur das Overlay offen (Ebene `Super`). Ohne Fenster verwirft das Hauptmenü das Spiel (siehe ADR 0023, Hauptmenü-Befund). Mit dem Spiel gehen auch `GameInitData`, die Figuren und der Startort verloren.
- Auf die Zustimmung des Servers ruft die Mod `InitGameStart` erneut auf. Dessen Vorab-Aktion scheitert an `Find.GameInitData == null`. Danach erzeugt vanilla beim Szenenwechsel ein Spiel ohne Einrichtung, mit anderer Welt- und Ortslage und mit Zufallsfiguren. Die Mod speichert dieses Spiel und sendet es. (Dass es vanillas Schnelltest-Start ist, ist WAHRSCHEINLICH: abgeleitet aus Log und Verhalten, nicht im Quelltext geprüft.)
- Der Selbsttest rief `InitGameStart` direkt auf, ohne Seite und ohne Wartezeit. Die gelandeten Figuren prüfte er nicht.

## Behebung

- `TryBeginSharedStart` öffnet beim Anfragen das Multiplayer-Fenster. Solange die Anfrage läuft, lässt es sich nicht schließen (`KeepWindowOpen`). Lehnt der Server ab, kommt die Figurenseite zurück und das Fenster schließt sich.
- `StartSharedGame` startet nie ohne `GameInitData`. Fehlen die Daten, gibt es einen Logfehler, einen Hinweis und `SetupAbort` statt eines Zufallsspiels.

## Selbsttest im Spiel

`RimWorldWin64.exe -mpWorldSelfTest -mpWorldSelfTestCoverage=0.3 -mpQuitAfterSelfTest`. Neu im Spielteil:

1. Die vanilla-Figurenseite wird geöffnet, ohne Multiplayer-Fenster.
2. Nach 1 s folgt „Start“ wie bei der Mod: Fenster öffnen, Seite schließen.
3. Nach 1,5 s wird `startKept` geprüft.
4. Nach dem Start werden `landedChosen` (gelandete Kolonisten = Startgruppe) und `site` (Kartenfeld = Startort) geprüft.

| Lauf | Stand | Ergebnis |
|---|---|---|
| 1 | Ohne Fenster: die Zeile im Selbsttest auskommentiert, wie vor der Behebung | `game self-test: FAIL startKept=FAIL` – das Spiel ist weg |
| 2 | Mit Behebung | **PASS**: `startKept colonists landedChosen site component ownersLanded gzip ticksHeld sessionLoaded fingerprint tick ticksHeldAfterLoad` alle ok. 3 Kolonisten, 2 Besitzer. Welt-, Startort- und Figurenteil PASS, keine `InitGameStart`-Ausnahme. |

In beiden Läufen stammt der einzige weitere Logfehler aus einer alten `Multiplayer.Foundation.Session.dll` im installierten Mod-Ordner (`TypeLoadException` für `LockstepSession`). Diese Datei baut dieser Branch nicht.

## Nicht geprüft

- Die sechs Harness-Testprojekte: Sie zielen auf `net10.0`, installiert ist nur SDK 8. Der .NET-8-Harness der Vorsitzungen ist nicht mehr vorhanden. Kein Testprojekt bindet `Multiplayer.Foundation.RimWorld` ein, und nur dieses Projekt ist geändert.
- Der echte Ablauf mit Server, also der Harmony-Prefix und die Antwort: Der Selbsttest bildet ihn nach und ruft dieselbe `OpenWindow`-Methode auf.
- Die Ablehnung durch den Server.
- Ein Benutzer-Lauf allein und mit zwei Spielern.

## Einordnung nach AGENTS.md Regel 25

`BUILD_ERFOLGREICH` (0 Warnungen); `IN_GAME_GETESTET` (Selbsttest, ein Prozess, ohne Netz); `MP_ZWEI_CLIENTS_GETESTET`: nein.
