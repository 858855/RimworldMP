# Architektur – Stand und Grenzen

## Tatsächlich vorhanden

Das Repository enthält versionierte Forschung, Governance und nicht kompilierte Beispiele. Produktiver Code unter `src/` und ein lauffähiger Mod unter `mod/` sind noch nicht vorhanden. `docs/verification/RECOVERY_REPORT.md` belegt den Ausgangsstand.

## Ziel für 0.1 (geplant)

`RimWorld UI -> Network Core -> Protocol/TCP -> Dedicated Server -> Lobby Core`. Der Server ist autoritativ für Sitzungen und Lobbys. Die UI empfängt Zustandsnachrichten; sie verändert keinen RimWorld-Spielzustand. Der Server simuliert keine Spielwelt. Protokoll und Netzwerkbibliothek enthalten keine Verse-, Unity- oder Waylith-Abhängigkeit.

- **Protocol:** feste Version, Längenbegrenzung, Nachrichtentypen, Fehlercodes.
- **Network Core:** asynchrone Verbindung, Framing, Handshake, Heartbeat, Timeout und sauberes Schließen.
- **Lobby Core:** geprüfte Mitgliedschaft, Host-Rechte, Ready-Status und konsistente Zustandsupdates.
- **Dedicated Server:** Konfiguration, TCP-Listener, Sitzungen, Logging und Shutdown ohne Game-Runtime.
- **RimWorld Adapter:** lokal installierter 1.6-Build; nur UI und Netzwerkadapter. 1.7 ist nicht verifiziert.

**Sicherheitsgrenze:** Der LAN-Testserver erhält zunächst keine öffentliche Authentifizierung oder Transportverschlüsselung; keine Internet-Sicherheitszusage. Datenfelder und Framegröße werden begrenzt. Architekturentscheidungen stehen unter `docs/decisions/`.
