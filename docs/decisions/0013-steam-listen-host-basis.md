# ADR 0013 – Vollständiger Multiplayer auf Basis Steam-Listen-Host

- **Status:** angenommen durch ausdrückliche Anweisung des Benutzers am 2026-10-15 („kein Dedicated Server, sondern basierend auf dem, was wir bis dato gemacht haben, mit dem Steam-Invite“). Ergänzt `docs/plans/rimworldmp-full-plan.md` (PLAN-001).
- **Kontext:** Der Gesamtplan sah für Setup und Gameplay-Sync gleichrangig Dedicated Server und Listen-Host vor (Plan §2, §3.1, §4, §7.8). Vorhanden und vom Benutzer genutzt ist der Weg über ADR 0009 (eingebetteter Host im Spielprozess) und ADR 0010 (Steam-Freundeslobby, Einladung, Steam Networking P2P).
- **Entscheidung:** Alle Meilensteine ab Plan Teil A bauen auf dem **Listen-Host im Spiel des Hosts** auf. Gäste treten über **Steam-Einladung** bei; Steam P2P ist der primäre Transport. Der im Spiel des Hosts eingebettete `FoundationServer` übernimmt die Rollen, die der Plan dem „Server“ zuschreibt: Lobby, Setup-Zustandsmaschine, Sequencer, Tick-Takt, Hash-Vergleich und Chunk-Relay.
- **Abgrenzung:**
  - IP/TCP mit UPnP (ADR 0012) bleibt als Ausweichweg für **denselben** eingebetteten Server erhalten; er wird nicht entfernt.
  - Der eigenständige Dedicated Server (`Multiplayer.Foundation.Server`) bleibt im Repository als Lobby-Testwerkzeug. Neue Funktionen werden **nicht** gegen ihn abgenommen, und Abnahmekriterien verlangen ihn nicht mehr.
  - Host-Migration entfällt dauerhaft für 1.0: Verlässt der Host, endet die Sitzung für alle. Fortsetzen erfolgt über den zuletzt koordinierten Spielstand (Plan §7.8, Listen-Host-Fall).
- **Folgen für den Plan:**
  - §2 „Steam P2P, IP/TCP and the dedicated server“ gilt als „Steam P2P (primär), IP/TCP (Ausweichweg)“.
  - §3.1 „the existing Linux dedicated server works unchanged“ entfällt als Begründung. Lockstep bleibt die Architekturwahl: Der Host simuliert ohnehin selbst, und nur Befehle zu übertragen, schont Steam-Bandbreite.
  - §7.8 „Host leaves – Dedicated server“ entfällt.
  - Aufgaben mit Komponente `dedicated_server` werden bei Übernahme auf `listen_host` umgestellt.
- **Konsequenz für die Technik:** Steam-Grenzen sind harte Entwurfsrandbedingungen, keine Randfälle. `SteamPeerStream` beendet die Verbindung, sobald mehr als 1 MiB in der Empfangsqueue liegt. `SendMessageToConnection` schlägt fehl, wenn Steams Sendepuffer voll ist (Standard laut Steamworks-Dokumentation 512 KB, Status **WAHRSCHEINLICH**, lokal nicht gemessen). Jeder Datenfluss, der größer als ein paar hundert KiB werden kann, braucht Gegendruck von Ende zu Ende. Siehe ADR 0014.
- **Tests:** Neue Funktionen werden gegen `ListenHost` mit einem Steam-ähnlichen Nachrichtenstrom geprüft (begrenzte Queue, Pump im Frame-Takt). Echte Steam-Tests erfordern zwei Steam-Konten und bleiben bis zur Ausführung `NICHT_GETESTET`.
- **Rollback:** ADR zurückziehen; der Dedicated Server ist weiterhin funktionsfähig, es wurde nichts entfernt.
- **Alternativen:** Dedicated Server als Sequencer würde einen zweiten Betriebsweg mit eigener Infrastruktur und Portfreigabe verlangen, den der Benutzer ausdrücklich nicht will. Host-Migration wäre nur mit Dedicated Server sinnvoll.
