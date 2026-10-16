# Lockstep-Koordinator (SYNC-001, DESYNC-001 Koordinator-Seite) – Prüfung 2026-10-16

- **Branch/Basis:** `feat/lockstep-session` von `feat/mod-check-setup-ui` @ `08e7f9b` (abhängig, ungemergt), Worktree `/mnt/Arbeit/Git Projekte/RimworldMP-claude`. Aufteilung laut Benutzer: Koordinator-Seite hier, Spielseite beim zweiten Beitragenden.
- **Umgebung:** Linux, .NET SDK 10.0.402 / Runtime 10.0.13, lokale RimWorld-Assemblies `1.6.4871` (rev598).
- **Design:** ADR 0030.

## Tatsächlich ausgeführte Prüfungen

| Befehl/Prüfung | Ergebnis |
|---|---|
| `DOTNET_BIN=…/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen; Paket enthält jetzt `Multiplayer.Foundation.Session.dll` |
| `DOTNET_BIN=…/dotnet bash scripts/build-server.sh` | Exit 0 |
| `dotnet run --project tests/Session.Tests/Session.Tests.csproj -c Release` (neu) | Exit 0; sechs Gruppen |
| `Hosting.Tests` | Exit 0; 21 Gruppen, neu: Lockstep über das Netz |
| `Protocol.Tests`, `Network.Tests`, `Lobby.Tests`, `Integration.Tests`, `Client.Tests`, `ProcessIntegration.Tests` | Exit 0; 15, elf, sieben, 18, drei, acht |
| `python3 scripts/validate_project_state.py` | Exit 0 |

## Kernaussage: gleiche Befehle an gleichen Ticks

`Session.Tests` simuliert mit virtueller Uhr einen Koordinator und drei Spiele:
- eines davon fünfmal langsamer
- FIFO-Verbindungen mit zufälliger Latenz von 5–80 ms je Nachricht
- zufällige Befehle aller Spieler
- Fortsetzen, Pause, Fortsetzen
- Geschwindigkeit 3, abgelehnter Geschwindigkeitswunsch eines Gastes, Geschwindigkeit 2

Ergebnis in **6 Läufen mit verschiedenen Zufallswerten**:
- Alle Spiele führten bis zum gemeinsamen Tick **exakt dieselbe Folge (Tick, Sequenz)** aus, z. B. 386 Befehle bis Tick 2944.
- Der Abstand zwischen schnellstem und langsamstem Spiel blieb im erlaubten Vorsprung.

**Gegenprobe Desync:** Ein Spiel meldet bei Tick 600 einen abweichenden Zustands-Hash.
- Alle drei erhalten `DesyncDetected` mit diesem Spieler.
- Alle drei halten **am selben Tick** an (814).
- Die Verzögerung entsteht, weil der Vergleich auf die Meldung des langsamsten Spiels für Tick 600 wartet.

**Gegenprobe Reihenfolge:** Kommt ein Befehl für eine Grenze an, die das Spiel schon überschritten hat, lehnt `LockstepClient` ihn mit Protokollfehler ab, statt still auseinanderzulaufen. Ebenso bei einer Sequenzlücke oder einem zurücklaufenden Horizont.

## Weitere Prüfungen

Koordinator-Regeln:
- Start pausiert bei Tick 0.
- Befehl in der Pause bekommt Grenze 0 und den gestempelten Absender.
- Fortsetzen darf jeder, die Geschwindigkeit nur der Host.
- Uhr: 60 Ticks/s bei Geschwindigkeit 1, 360 Ticks/s bei Geschwindigkeit 3.
- Vorsprungsgrenze 2 s; danach Rücksetzung der Uhr ohne Nachholsprung.
- Tick über dem Horizont wird abgelehnt.
- Ratenbegrenzung: 60 Befehle als Spitze, dann Ablehnung, Nachfüllen über die Zeit.

Hash-Vergleich:
- Vergleich erst, wenn alle gemeldet haben.
- Abweichung vom Host wird gemeldet und pausiert.
- Späte Meldungen werden ignoriert.
- Ein gehender Spieler vervollständigt den Vergleich; geht der Host, endet die Sitzung.

Ende zu Ende über `ListenHost` mit Steam-ähnlichem Gast, nach dem kompletten Setup-Durchlauf bis `Playing`:
- Ein Befehl in der Pause wirkt sofort in beiden Spielen, Absender gestempelt.
- Gast-Geschwindigkeit wird abgelehnt.
- Nach dem Fortsetzen läuft die Zeit; der langsam rechnende Gast bestimmt das Tempo.
- Beide führen dieselben Befehle an denselben Ticks aus.
- Ein abweichender Hash stoppt beide am selben Tick.

## Gefundene und behobene Fehler

- `LockstepClient.ProgressMessage`: Startwert `long.MinValue` ließ `now − last` überlaufen. Es wurde nie ein Fortschritt gemeldet, und jede Partie wäre nach 2 s Vorsprung stehen geblieben. Die Simulation fand das.
- `MayRunTick` ließ ein Weiterrechnen zu, wenn der Tick-Treiber die Befehle des aktuellen Ticks nicht abgeholt hatte. Jetzt ist das Abholen Pflicht.
- Das Mod-Paket hätte die neue `Multiplayer.Foundation.Session.dll` nicht enthalten, weil `build-mod.sh` und `package-mod.py` feste Listen haben. Der Host wäre beim Spielstart mit einem Ladefehler abgestürzt.

## Klassifizierung und Grenzen

`BUILD_ERFOLGREICH`, Koordinator und Client-Logik automatisiert getestet. Offen:
- **Spielseite:** Tick-Treiber an `TickManager` statt der heutigen Dauerpause, Befehlserfassung (`SYNC-002`), Zustands-Hash, Rand-Neusaat (Plan §7.4).
- Tickraten 60/180/360 nur laut Plan, im Spiel nicht gemessen.
- Im Spiel `NICHT_GETESTET`; Spieltests sind laut Benutzer derzeit nicht möglich.
