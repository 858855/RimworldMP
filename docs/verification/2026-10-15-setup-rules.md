# Serverseitige Setup-Regeln (START-001, SETUP-003, SETUP-004) – Prüfung 2026-10-15

- **Branch/Basis:** `feat/setup-rules` von `feat/player-key` @ `2221608` (abhängig, ungemergt), Worktree `/mnt/Arbeit/Git Projekte/RimworldMP-claude`.
- **Umgebung:** Linux, .NET SDK 10.0.402 / Runtime 10.0.13, lokale RimWorld-Assemblies `1.6.4871` (rev598).
- **API-Prüfung:** `RimWorld.Planet.PlanetTile` ist in 1.6 ein `struct` mit `int tileId` und `int layerId`; Konstruktoren `(Int32)`, `(Int32, Int32)`, `(Int32, PlanetLayer)`. Geprüft per Reflection gegen die lokale `Assembly-CSharp.dll`. Daraus folgt `SiteTile` mit beiden Werten.
- **Umfang:** nur Server und Lobby-Kern (ADR 0017). Start-Knopf, vanilla-Seiten, Weltkarte mit Fähnchen und Charakterfenster im Spiel fehlen noch.

## Tatsächlich ausgeführte Prüfungen

| Befehl/Prüfung | Ergebnis |
|---|---|
| `DOTNET_BIN=…/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen, 0 Fehler |
| `DOTNET_BIN=…/dotnet bash scripts/build-server.sh` | Exit 0 |
| `Protocol.Tests` | Exit 0; neun Gruppen, neu: Setup-Nutzdaten, Feld mit Ebene, Phase in Lobbyzustand und -liste |
| `Lobby.Tests` | Exit 0; vier Gruppen, neu: kompletter Phasen-Durchlauf mit allen Ablehnungen; Gast verlässt, Host verlässt |
| `Hosting.Tests` | Exit 0; 15 Gruppen, neu: Setup-Regeln Ende zu Ende über `ListenHost` mit Steam-ähnlichem Gast |
| `Network.Tests`, `Integration.Tests`, `Client.Tests`, `ProcessIntegration.Tests` | Exit 0; elf, 18, drei, acht unverändert |
| `python3 scripts/validate_project_state.py` | Exit 0 |

Im Lobby-Kern geprüfte Regeln:
- Start erst ab 2 Spielern und wenn alle bereit sind, Host eingeschlossen.
- Nur der Host startet und schaltet weiter. Eine falsche oder übersprungene Zielphase wird abgelehnt, ein doppelter Klick ebenso.
- Während der Einrichtung ist die Lobby für Beitritte gesperrt, und die Liste zeigt die Phase.
- Bereit ist außerhalb von Lobby und Charakterwahl abgelehnt. Fähnchen gibt es nur in der Startortwahl.
- Fähnchen: eines je Spieler, ersetzbar und entfernbar; die Planetenebene bleibt erhalten.
- Startort: Ein Gast kann nicht wählen. Der Host kann auch ein Feld ohne Fähnchen wählen. Die Wahl beendet die Startortwahl und löscht alle Fähnchen.
- Ein „Bereit“ aus der Lobby zählt nie als „Fertig“.
- Charakterwahl geht nicht weiter, solange jemand nicht fertig ist, auch nicht, wenn jemand sein Fertig zurücknimmt. Es gibt kein Erzwingen.
- Nur der Host bricht ab; der Abbruch setzt alles zurück und hebt die Sperre auf.
- Ein Gast, der geht, blockiert nicht mehr. Geht der Host, endet die Einrichtung, bevor die Host-Rolle wechselt.

Über das Netz mit Steam-ähnlichem Gast geprüft:
- Der Gast wird bei Start, Startortwahl und Abbruch mit Code 3 abgelehnt.
- Der Start wird vor „alle bereit“ abgelehnt.
- Ein Beitritt während der Einrichtung wird abgelehnt.
- Der Host sieht das Fähnchen des Gastes und wählt dieses Feld.
- Die Charakterwahl wartet auf den Gast.

## Klassifizierung und Grenzen

`BUILD_ERFOLGREICH`, Serverregeln automatisiert getestet. Die Spielseite fehlt (SETUP-001 und folgende). `NICHT_GETESTET` im Spiel; Spieltests sind laut Benutzer derzeit nicht möglich. Die Bedingungen „Mods stimmen überein“ (MOD-001) und „Welt-Fingerabdruck gleich“ (SETUP-002) fehlen noch.
