# ADR 0009: In-Game-Listen-Host zusätzlich zum Dedicated Server

- Status: angenommen für Version 0.1-Testpaket
- Ziel: Der Host startet aus der RimWorld-Mod eine Lobby, ohne ein separates Serverprogramm zu starten. Andere Spieler verbinden sich mit seiner LAN-IP.
- Betroffene Klassen: `FoundationServer`, `HostConfig`, `ListenHost`, `FoundationMod`, `FoundationSettings`; keine Defs oder Harmony-Patches.
- Zustandsmodell: Beide Modi verwenden denselben autoritativen Lobby-Kern. Im Listen-Modus laufen Listener und Host-Client im Spielprozess; Gäste sind normale TCP-Clients.
- Persistenz: Lobbys bleiben flüchtig; nur Host-IP, Port und Spielername werden als Mod-Einstellungen gespeichert.
- UI-Aktionen: Direkt hosten, Verbinden, Lobby anzeigen, Ready, Trennen. Ein gestoppter Host schließt den Listener und Gastverbindungen.
- Netzwerk-Sync: Bestehendes Protokoll und dieselben Snapshot-Revisionen; keine Gameplay-Synchronisation.
- Konfliktbereich: Gemeinsamer Serverkern muss `netstandard2.1` für RimWorld 1.6 erfüllen. Dedizierter .NET-10-Prozess bleibt als dünner Start-/Konfigurationsadapter.
- Performance-Risiken: Netzwerk- und Heartbeat-Aufgaben laufen im Spielprozess; keine Spielweltmutation auf Hintergrundthreads. Begrenzte Clients/Nachrichten wie bisher.
- Tests: Host und Gast im selben Testprozess über echte TCP-Sockets, Ready, Host-Stopp und Portfreigabe; bestehende Server-/Mehrprozess-Tests; Mod-Build und ZIP-Inhalt.
- Rollback: Änderungen und neues ZIP auf dem Arbeitsbranch zurücknehmen; bestehender Dedicated Server bleibt im vorherigen Commit verfügbar.
- Begründung: Ein zweiter, abweichender Lobby-Server im Mod würde Protokoll und Rechte duplizieren. Ein gemeinsamer Kern ermöglicht beide Betriebsarten mit denselben Regeln.
- Alternative: Separates Serverprogramm automatisch vom Spiel starten; würde weiterhin einen Serverprozess und eine .NET-10-Runtime beim Spieler benötigen.
