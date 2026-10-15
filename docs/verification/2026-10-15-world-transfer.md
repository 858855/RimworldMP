# Verifikation – Weltübertragung und Fingerabdruck (SETUP-002)

- **Datum:** 2026-10-15
- **Branch:** `feat/mod-check-setup-ui` (lokal)
- **Umgebung:** Windows 11 Pro; RimWorld `1.6.4871` (Prozess rev591), Kern + Royalty, Ideology, Biotech, Anomaly, Odyssey; .NET SDK 8.0.403 (Harness wie in `2026-10-15-lobby-hardening.md`).

## Automatisierte Tests (.NET-8-Harness)

| Projekt | Ergebnis |
|---|---|
| Protocol.Tests | Exit 0 (neu: Fingerabdruck, Rundung, Weltstatus in Nutzdaten) |
| Lobby.Tests | Exit 0 (neu: Weltregeln; bestehende Phasentests um Weltmeldungen ergänzt) |
| Hosting.Tests | Exit 0 (neu: 3 MB „Welt“ über Steam-ähnlichen Strom, falscher Fingerabdruck sperrt mit Code 8, passender gibt frei) |
| Network, Integration, Client | Exit 0 |

## Selbsttest im Spiel

`RimWorldWin64.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -mpWorldSelfTest -mpWorldSelfTestCoverage=<x> -mpQuitAfterSelfTest`; Ergebnis aus `Player.log`.

| Lauf | Befund | Folge |
|---|---|---|
| 1 | Weltgenerierung: NullReferenceException in `StorytellerUtilityPopulation` (kein Erzähler im Selbsttest); Laden: NullReferenceException in `WorldGrid.CalculateViewCenterAndAngle`, keine Welt | Selbsttest setzt Erzähler; Welt wird vor dem Laden als aktuelle Welt gesetzt und angemeldet |
| 2 | Laden ohne Fehler, 13/13 Siedlungen; Fingerabdruck weicht ab (Sumpfigkeit ±0,01) | Teil-Fingerabdrücke und Feldvergleich ergänzt |
| 3–4 | trotz `StandardizeTileData` weiter Sumpfigkeit, danach Höhe ±1 an Rundungsgrenzen | nur diskrete Werte im Fingerabdruck |
| 5 | **PASS** bei 5 %, 30 %, 50 % (Tabelle in ADR 0020); keine Fehler, Spiel beendet sich selbst | – |
| 6 | Endbuild: **PASS** bei 30 %, Mod-Prüfsumme 708c, keine Fehler | – |

## Nachtrag: SETUP-003 (Startortseite und Fähnchen)

| Lauf | Befund | Folge |
|---|---|---|
| 7 | Startortseite öffnet auf der geladenen Welt ohne Fehler, aber 0 Fähnchen | Die Fähnchenliste wurde jedes Frame aus dem (leeren) Lobbyzustand überschrieben; während des Selbsttests gehört sie jetzt dem Selbsttest |
| 8 | **PASS**: Seite offen, 2 Fähnchen, in 774 Frames gezeichnet | – |
| 9 | **PASS** mit `-mpSelfTestScreenshot`; Bildschirmfoto (3840×2160) zeigt rotes „Test-Host“- und blaues „Test-Gast“-Fähnchen mit Namen über den Feldern, alle Siedlungen der geladenen Welt | – |

Automatisierte Tests nach SETUP-003 erneut: alle sechs Projekte Exit 0.

## Einordnung nach AGENTS.md Regel 25

- `BUILD_ERFOLGREICH`; automatisierte Tests bestanden.
- `IN_GAME_GETESTET`: Welt erzeugen, speichern, verwerfen, wie ein Gast laden, Fingerabdruck gleich; Startortseite auf dieser Welt mit gezeichneten Fähnchen (Selbsttest, ein Spielprozess).
- **Nicht geprüft:** Übertragung zwischen zwei echten Spielern, Anzeige von Fortschritt und Weltstatus im Spiel, Abbruch während der Übertragung, Allein-Lauf über die Einrichtungsseiten mit diesem Build.
