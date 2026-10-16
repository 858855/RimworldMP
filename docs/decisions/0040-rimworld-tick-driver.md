# ADR 0040 – Tick-Treiber auf der Spielseite (X2)

- **Status:** angenommen auf `codex/tick-driver`, Basis `main`/`e4501df`; RimWorld 1.6.4871 rev598, Unity/Mono.
- **Ziel:** Nach `Playing` läuft die gemeinsame Zeit nur bis zum vom Koordinator freigegebenen Horizont. Der bisherige Dauerstopp wird ersetzt.
- **Bereich:** `FoundationMod`, `SessionPausePatch`, Spieloverlay; `LockstepClient` aus dem Network-Projekt bleibt unverändert. Keine neuen Nachrichtencodes.
- **Zustand:** Pro laufender Lobby ein flüchtiger `LockstepClient` und `startTicksGame`. Sitzungstick = `TicksGame - startTicksGame`. Beim Verlassen/Verbindungsabbruch wird der Treiber verworfen. Vor `Playing`, während des Ladens und bei Fehlern bleibt der Tick gesperrt.
- **Grenze:** `TickManagerUpdate` verarbeitet auch im Pausenframe anstehende Befehle und setzt die vom Lockstep bestätigte Geschwindigkeit. Vor `DoSingleTick` werden Befehle derselben Grenze abgeholt; nur `MayRunTick(c)` und `!Paused` lassen den Vanilla-Tick zu. Der Postfix meldet Fortschritt, etwa zehnmal pro Sekunde. Spielbefehle werden bis `SYNC-002` bei Eingang fail-closed gestoppt, nicht ignoriert.
- **Pause/Tempo:** Vanilla-Eingaben an `CurTimeSpeed` und `TogglePaused` werden als `SessionControl` angefordert; allein der Koordinator entscheidet. Speed ist für Gäste gesperrt. Der interne Setzpfad ist gegen Rückkopplung geschützt.
- **Hash:** Alle 60 Sitzungsticks wird ein vorläufiger, stabil sortierter Kernhash über Tick, Karten-IDs und Kolonisten-IDs/-Positionen gesendet. `X3` erweitert ihn um umfassenden Spielzustand und Rand-Neusaat. Dieser Kernhash ist kein vollständiger Desync-Nachweis.
- **UI:** Das Overlay zeigt Pause/Lauf, Sitzungstick und bei Horizontstopp den vom Koordinator gemeldeten langsamsten Spieler. Es zeigt keine unbekannte Spieleridentität als gesichert an.
- **Abwägung:** Der vorhandene `DoSingleTick`-Hook ist an lokalen 1.6-Assemblies geprüft und erhält Vanilla für Einzelspieler. Ein kompletter Ersatz von `TickManagerUpdate` würde mehr Versionierungs- und Modkonflikte verursachen.
- **Risiko/Prüfung:** Harmony-Reihenfolge, erzwungene Vanilla-Pausen und Tick-/Befehlstiming brauchen einen Zwei-Spieler-Spieltest. Lokal sind API-Reflection, Mod-Build und Dokumentationsprüfung möglich. Rollback durch Entfernen des Branches; `main` bleibt unverändert.
