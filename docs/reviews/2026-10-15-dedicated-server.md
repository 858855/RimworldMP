# Review: SERVER-001 und LOBBY-001

- **Quellbranch/Ziel:** `feat/dedicated-server` → später `main` über abhängige Branches; keine Merge-Freigabe. Basis `feat/network-protocol`/`f14142c`.
- **Änderung:** Eigenständiger TCP-Lobby-Server, Session- und Heartbeat-Verwaltung, autoritative Aktionen sowie Build-/Startskripte. Zwei Clients erhalten Lobby- und Ready-Snapshots; nicht berechtigte Aktionen werden zurückgewiesen.
- **Betroffene Komponenten:** Protocol-Payloads, Network, LobbyRegistry, FoundationServer, Deployment, Integration.Tests.
- **Commits:** `2ac3d63` (Lobby-Core) plus aktueller Server-Commit (`git log` prüfen).
- **Prüfung:** Release-Build und zehn TCP-Integrationstestgruppen bestanden; siehe `docs/verification/2026-10-15-dedicated-server.md`.
- **Risiken:** Keine Authentifizierung/TLS, nur kontrolliertes LAN. Revisionen müssen beim künftigen Client gegen veraltete Snapshots geprüft werden. Keine RimWorld-Laufzeitprüfung.
- **Menschlich prüfen:** Netzwerkexposition/Konfiguration, Host-Rechte, Fehlerbehandlung und gewünschte Zielbranch-Reihenfolge. Keine automatische Freigabe oder Integration.
