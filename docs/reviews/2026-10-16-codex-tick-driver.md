# Review X2 – Spielseitiger Tick-Treiber

- **Aufgaben:** `SYNC-001` und `SYNC-003` (Spielseite); Branch `codex/tick-driver`, Ziel `main`, Basis `e4501df`. Kein Merge ohne ausdrückliche Freigabe.
- **Änderung:** Nach der Landung hält der Client den Spieltick am gemeinsamen Horizont an. Pause/Tempo laufen als Koordinator-Anfragen; Overlay zeigt Fortschritt und den langsamsten Spieler. Der alte Dauerpause-Patch lässt freigegebene Ticks durch.
- **Betroffene Komponenten:** RimWorld-Adapter und Harmony-Tick-Hooks; keine Änderung am Koordinator oder an Nachrichtencodes. `scripts/build-mod.sh`/`scripts/package-mod.py` enthielten `Multiplayer.Foundation.Session.dll` bereits und wurden durch Build/Paketprüfung bestätigt.
- **Entscheidung:** [ADR 0040](../decisions/0040-rimworld-tick-driver.md). Spielbefehle ohne `SYNC-002` werden ausdrücklich gestoppt; der Hash ist bis X3 unvollständig.
- **Nachweise:** [Prüfbericht](../verification/2026-10-16-codex-tick-driver.md): API-Reflection, Mod-Build ohne Warnungen, `Session.Tests`, `Client.Tests`, ZIP mit 10 Einträgen.
- **Risiken / manuell prüfen:** Zwei echte RimWorld-Clients und die Harmony-Laufzeit sind noch ungeprüft. Pause/Tempo bei beiden Clients, Host-Recht, langsamster Spieler, Tick- und Hash-Gleichheit, Verhalten mit weiteren Mods prüfen. Die offene Determinismus-Aufgabe `DET-001` und `SYNC-002` verhindern eine Zusage für spielbares Multiplayer-Gameplay.
- **Status:** `ready_for_human_review`; keine dokumentierte Merge-Freigabe für diesen Branch.
