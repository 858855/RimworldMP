# Zwischenstand NET-001 – Protokoll

- **Branch/Ziel:** `feat/network-protocol` (baut auf `docs/project-governance` / `7e3a9ae`) → später `main`, nur nach menschlicher Freigabe.
- **Änderung:** Versioniertes, größenbegrenztes Frameformat und primitive Nutzdatenfelder; zusätzlich TCP-Verbindung und Handshake, aber noch keine Lobbyfunktion.
- **Tests:** Fünf Protokoll- und drei Netzwerkgruppen bestanden, siehe `docs/verification/2026-10-15-protocol.md` und `docs/verification/2026-10-15-network.md`.
- **Risiko/Review:** Binärformat muss mit tatsächlichen Server-/Client-Nachrichten ergänzt und erneut getestet werden. Keine öffentliche Internet-Sicherheitszusage. Aufgabe noch nicht integrationsbereit.
