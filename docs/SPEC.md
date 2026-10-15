# Produktspezifikation – 0.1 Connection & Lobby Foundation

**Verbindlich:** Ein eigenständiger TCP-Server ohne RimWorld; versioniertes begrenztes Protokoll; zwei unabhängige Clients; Handshake; mehrere autoritative Lobbys; Liste, Erstellen, Beitreten, Verlassen, Spieler-/Host- und Ready-Status; Disconnect/Heartbeat/Shutdown; RimWorld-1.6-Mod mit bedienbarer Verbindung und Lobbyanzeige. Öffentliche Schnittstellen und tatsächliche Prüfungen werden in Fachdocs dokumentiert.

**Nicht Ziel von 0.1:** gemeinsame Kolonie, Pawn-/Job-/Tick-Synchronisation, Savegame-Replikation, Gameplay-Lockstep, serverseitige RimWorld-Simulation. Ein Spielstart-Button darf keine fertige Funktion vortäuschen.

**Optional nach Nachweis:** Reconnect-Erleichterung und Modulregistrierung. Keine überflüssigen Abstraktionen vor einem realen Anwendungsfall.

**Akzeptanz:** Server baut und startet unter Linux; zwei Clients führen echte TCP-Lobbyaktionen aus und sehen denselben Zustand; Fehlerfälle sind automatisiert geprüft; der Mod baut gegen den lokalen 1.6-Build und wird im Spiel getestet. Fehlende Game-Tests begrenzen die Verifikationsaussage. Ziel: lokaler RimWorld-1.6-Build `1.6.4871` (Steam `23969874`); `Version.txt` meldet rev598, der Spielprozess rev600. Die exakte Revision und andere Versionen sind nicht laufzeitverifiziert.
