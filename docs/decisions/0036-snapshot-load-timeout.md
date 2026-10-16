# ADR 0036 – Zeitgrenze für nie geladene Snapshots

- **Status:** angenommen für `claude/snapshot-timeout`, auf `main` @ `ff2f178`. Designnotiz nach `AGENTS.md` Regel 4. Ergänzt ADR 0032 (Beitritt) und ADR 0033 (Resync). Offener Punkt aus der Codex-Übergabe („für Snapshot-Ladefehler bleibt eine zeitliche Abbruchregel zu prüfen“). Bahn Claude, C5.
- **Problem:** Ein Beitretender oder ein zu reparierendes Spiel wird erst nach `SnapshotLoaded` wieder aktiv. Kommt die Bestätigung nie, etwa weil die Übertragung scheitert, das Laden abstürzt oder das Spiel hängt, dann:
  - bleibt ein Beitretender für immer unvollständig dabei;
  - bleibt bei einer Reparatur die vom Koordinator gesetzte Pause für **alle** bestehen.
- **Entscheidung:**
  - Wer einen Snapshot laden soll, hat dafür `SnapshotLoadTimeoutSeconds` Zeit; Standard 180 s, einstellbar 5–3600 s über `HostConfig` bzw. `ListenHost.StartAsync(..., snapshotLoadTimeoutSeconds:)`. Die Frist beginnt beim Beitritt bzw. beim Auslösen der Reparatur und endet mit `SnapshotLoaded`.
  - Läuft sie ab:
    1. Alle, der Betroffene eingeschlossen, erhalten `ResyncGaveUp` (74) mit ihm. Bedeutung hier: Der Koordinator hat ihn aufgegeben.
    2. Der Koordinator nimmt ihn aus der Sitzung. Abstimmungen, Reparatur-Status und Fortsetzen werden wie bei einem Verbindungsabbruch neu bewertet. War nur er der Grund der System-Pause, geht es für die anderen an einer gemeinsamen Stelle weiter (`DesyncResolved`, Fortsetzen).
    3. Der Server nimmt ihn aus der Lobby (`PlayerLeft` an die anderen, `LeaveLobby` an ihn). Die Verbindung bleibt bestehen; er kann sofort erneut beitreten und bekommt einen frischen Snapshot.
- **Warum vor der Pausenprüfung:** `Advance` prüft die Frist **vor** „pausiert?“. Gerade bei einer Reparatur ist pausiert, und genau dort darf das Warten nicht endlos sein.
- **Abgrenzung:** Der Host lädt nie einen Snapshot und ist nicht betroffen. Abgrenzung zu „aufgegeben nach drei Reparaturen“ (ADR 0033): Dort bleibt der Spieler drin und die Zeit pausiert, der Host entscheidet. Hier hat das Spiel gar nicht reagiert, also wird es entfernt.
- **Spielseite (Codex):** Auf `ResyncGaveUp` mit der eigenen Session und `LeaveLobby` „Laden fehlgeschlagen – erneut beitreten“ anzeigen; den alten Ladeversuch abbrechen.
- **Tests:**
  - Regeln: Frist nicht vorher, genau danach; nur der Säumige wird entfernt und einmal gemeldet; eine hängende Reparatur wird aufgelöst, und die anderen setzen fort.
  - Ende zu Ende über `ListenHost` mit Steam-ähnlichem Spät-Beitretendem und 5 s Frist: Er wird benachrichtigt, aus Sitzung und Lobby genommen und kann erneut beitreten.
- **Rollback:** Branch verwerfen; ohne ihn wartet der Koordinator unbegrenzt.
