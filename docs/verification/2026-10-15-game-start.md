# Verifikation – Gemeinsamer Spielstart (SETUP-005)

- **Datum:** 2026-10-15
- **Branch:** `feat/mod-check-setup-ui` (lokal), Commits `4bed15f` (Server/Protokoll) und `5aa758c` (Spielseite)
- **Umgebung:** wie in `2026-10-15-world-transfer.md` (RimWorld `1.6.4871`, alle DLCs, .NET SDK 8.0.403).

## Automatisierte Tests (.NET-8-Harness)

| Projekt | Ergebnis |
|---|---|
| Protocol.Tests | Exit 0 (neu: Phase `Playing`, unbekannte Phase 10 abgelehnt) |
| Lobby.Tests | Exit 0 (neu: Spielstand-Meldungen, frühe Gastmeldung zählt nach Host, Abweichung Code 8, Landung, nichts nach `Playing`, Abbruch setzt Status zurück) |
| Network.Tests | Exit 0 |
| Hosting.Tests | Exit 0 (neu: `GameReady` nur Host, `GameLoaded`, Landung über das Netz) |
| Integration.Tests | Exit 0 |
| Client.Tests | Exit 0 |

## Build und APIs

Mod mit `scripts/build-mod.sh` gebaut: 0 Warnungen, 0 Fehler; installiert. Neue Symbole per Metadaten gegen `Assembly-CSharp.dll` 1.6.4871 geprüft (Abfragen 19–23, alle vorhanden; Tabelle in `references/verified-apis.md`).

## Selbsttest im Spiel

`RimWorldWin64.exe -mpWorldSelfTest -mpWorldSelfTestCoverage=0.3 -mpQuitAfterSelfTest`. Nach Welt-, Startort- und Figurenteil: frisches Host-Spiel (eigene Welt, 5 %), `PageUtility.InitGameStart`, Sitzung füllen, speichern, gzip hin und zurück, als Host neu laden, als Gast laden, vergleichen, Pause prüfen.

| Lauf | Befund | Folge |
|---|---|---|
| 1 | NullReferenceException: `Current.Game` fehlt einen Frame nach dem Schließen der Startortseite | Untersucht in Lauf 3 |
| 2 | Wie Lauf 1 (Diagnose: `game=False state=Entry`) | – |
| 3 | Mit offenem Multiplayer-Fenster bleibt die Welt erhalten; ohne Fenster verwirft das Hauptmenü das Spiel | Gast-Fenster bleibt in `WorldTransfer`/`SiteSelection` offen; Selbsttest lässt es offen |
| 4 | Start, Speichern (synchron, 446 ms, 4,7 MB, gzip 0,96 MB) und Laden laufen; doppelte Load-IDs (Precept, Gene, WorldObject), Fingerabdruck und Pause FAIL | Spiel war von der Gast-Kopie der Welt gestartet (ohne ID-Zähler) – Testfehler; Host lädt künftig seinen Spielstand selbst neu (ADR 0023) |
| 5 | Fingerabdruck Host (neu geladen) = Gast; noch Precept-Doppel-IDs; Pause FAIL | Selbsttest erzeugt für den Spielteil eine eigene Host-Welt |
| 6 | 0 Fehler, Fingerabdruck gleich; Pause FAIL: 26 Ticks in 2 s, 45-mal zurückgesetzt | Rücksetzen der Geschwindigkeit reicht nicht |
| 7 | **PASS**: `colonists component ownersLanded gzip ticksHeld sessionLoaded fingerprint tick ticksHeldAfterLoad` alle ok; Ticks 0→0 bei erzwungener Geschwindigkeit „Normal“ (50 von 239 Frames lief das Spiel nach dem Update wieder an, 29 Ticks blockiert); erzeugen 5,7 s, speichern 335 ms (synchron), Host neu laden 2,3 s, Gast laden 2,3 s; 6,6 MB, gzip 433 KB; 3 Kolonisten, 2 Spieler, 2 Besitzer; 0 Fehler | `DoSingleTick`-Sperre bleibt |

Testdateien (`Multiplayer - Selbsttest Host/Gast`) löscht der Selbsttest am Ende.

## Einordnung nach AGENTS.md Regel 25

- `BUILD_ERFOLGREICH`; automatisierte Tests bestanden.
- `IN_GAME_GETESTET`: vanilla-Spielstart, Sitzung im Spielstand, Speichern, Laden als Host und Gast, Fingerabdruck, Pause (Selbsttest, ein Spielprozess, ohne Netz).
- **Nicht geprüft:** Ablauf mit Server (Startanfrage, `GameReady`/`GameLoaded`, Landung), Übertragung zwischen zwei echten Spielern, Feld im Spiel, Geschwindigkeitsknöpfe von Hand (mögliches Flackern der Anzeige), Allein-Lauf über die Seiten, Sitzungsende im Spiel.
