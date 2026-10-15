# Verifikation – Figurenwahl je Spieler (SETUP-004)

- **Datum:** 2026-10-15
- **Branch:** `feat/mod-check-setup-ui` (lokal), Commits `21bb90d` (Server/Protokoll) und `d922cf4` (Spielseite)
- **Umgebung:** wie in `2026-10-15-world-transfer.md` (RimWorld `1.6.4871`, alle DLCs, .NET SDK 8.0.403).

## Automatisierte Tests (.NET-8-Harness)

| Projekt | Ergebnis |
|---|---|
| Protocol.Tests | Exit 0 (neu: Liste und Anfragen hin und zurück, Grenzen, Besitzer-Prüfsumme) |
| Lobby.Tests | Exit 0 (neu: Liste nur vom Host, Fertig braucht eigene Figur, Änderung setzt Fertig zurück; bestehende Tests setzen vor Fertig eine Liste) |
| Network.Tests | Exit 0 |
| Hosting.Tests | Exit 0 (neu: Anfrage mit Server-Kennung an den Host, Liste an die Gäste, Fertig ohne Figur abgelehnt) |
| Integration.Tests | Exit 0 |
| Client.Tests | Exit 0 |

## Build

Mod mit `scripts/build-mod.sh` gebaut: 0 Warnungen, 0 Fehler. In `Mods/RimWorldMultiplayerFoundation` installiert.

## RimWorld-APIs

Alle neuen Symbole gegen `Assembly-CSharp.dll` 1.6.4871 per Metadaten geprüft (29 + 2 Abfragen, 0 fehlend). Tabelle in `references/verified-apis.md`.

## Selbsttest im Spiel

`RimWorldWin64.exe -mpWorldSelfTest -mpQuitAfterSelfTest`; nach Welt- und Startortteil läuft die Host-Logik auf den echten Startfiguren des Szenarios.

| Lauf | Befund | Folge |
|---|---|---|
| 1 | NullReferenceException beim Erzeugen einer neuen Startfigur (Erzähler) | Der `Game`-Konstruktor legt einen leeren Erzähler an; der Selbsttest prüft jetzt `storyteller?.def == null` |
| 2 | **PASS**: `start=3/8 now=4/8 raise=ok card=ok claim=ok secondClaimRefused=ok reroll=ok rerollKeepsOwner=ok rename=ok foreignRefused=ok swap=ok swapResult=ok leaveFrees=ok payload=ok`, Beispielkarte mit Name, Spitzname, Geschlecht, Alter, Xenotyp, Lebenslauf und Merkmalen | – |

Geprüft: Startgruppe von 3 auf 4 angehoben, Karte vollständig, Übernehmen und zweites Übernehmen abgelehnt, neu würfeln behält den Besitzer bei neuer Thing-ID, umbenennen, fremde Anfrage abgelehnt, Tausch mit zurückgelassener Figur (Plätze und Besitz), Weggang gibt frei, Liste passt in ein Frame. Keine Fehler im `Player.log`.

## Einordnung nach AGENTS.md Regel 25

- `BUILD_ERFOLGREICH`; automatisierte Tests bestanden.
- `IN_GAME_GETESTET`: Host-Logik auf vanilla-Startfiguren (Selbsttest, ein Spielprozess).
- **Nicht geprüft:** Figurenfenster beim echten Gast, Feld des Hosts auf der vanilla-Seite, Fertig über das Netz, Rücksetzen von Fertig im laufenden Spiel, Allein-Lauf mit diesem Build.
