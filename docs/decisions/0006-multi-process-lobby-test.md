# ADR 0006: Mehrprozess-Nachweis der Lobby-Verbindung

- Status: angenommen für Testphase
- Ziel: Zwei unabhängige Client-Prozesse gegen den bestehenden eigenständigen Serverprozess verbinden.
- Betroffene Klassen: `LobbyClient`, `FoundationServer`, `LobbyPayloads`; kein RimWorld-Adapter und keine Spielzustände.
- Zustandsmodell: Server bleibt autoritativ; Clients senden Liste/Erstellen/Beitreten/Ready/Disconnect und vergleichen empfangene Lobby-Snapshots.
- Persistenz: keine; Testkonfiguration liegt nur in einem temporären Verzeichnis.
- UI-Aktionen: keine. Der Prozess-Test steuert die öffentliche Netzwerk-Client-API über Standardeingabe.
- Netzwerk-Sync: echte TCP-Loopback-Verbindungen, getrennte Betriebssystemprozesse, serverseitige Snapshots.
- Konfliktbereich: Prozessstart/Port-Belegung; Test wählt einen freien Loopback-Port und startet nur eigene Prozesse.
- Performance-Risiko: Polling der Client-Ereignisqueue mit kurzer Pause; nur Testcode.
- Tests: Serverstart, zwei Handshakes, Lobby-Liste, Join, identische Snapshots, Ready, Disconnect-Bereinigung. Kein Spiel-UI- oder Gameplay-Test.
- Rollback: Testprojekt und ADR entfernen; keine Produktdateien betroffen.
- Begründung: Die bisherigen Integrationstests verbinden zwei Clients im selben Prozess. Die Prozessgrenze prüft zusätzlich Paketierung, Start der Serveranwendung und unabhängige Client-Lebenszyklen.
- Alternative: Zwei `NetworkConnection`-Instanzen im selben Testprozess; bereits vorhanden und für die fehlende Prozessgrenze unzureichend.
