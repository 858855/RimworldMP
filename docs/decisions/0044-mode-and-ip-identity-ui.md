# ADR 0044 – Spielmodus und IP-Identität im RimWorld-Fenster

- **Status:** angenommen für `codex/mode-ui`; RimWorld 1.6.4871 rev598, Unity/Mono.
- **Ziel:** Die in ADR 0031 und 0035 vorhandenen Serverfunktionen im Spiel bedienbar machen.
- **Klassen:** `FoundationMod`, `FoundationSettings`, `MultiplayerSession`; Protokoll und Server bleiben unverändert.
- **Zustand/Persistenz:** Der Host wählt `LobbyMode` vor dem Erstellen. Die Lobbyanzeige übernimmt ausschließlich den Serverzustand. Ein gestartetes Spiel schreibt den Modus in `MultiplayerSession`; ältere Spielstände gelten als Coop mit Hostentscheid. Eine lokale `PlayerKey`-Kennung und das private `PlayerIdentity`-Schlüsselmaterial liegen nur in den Mod-Einstellungen. Der Host speichert die Zuordnung fremder Kennungen zu öffentlichen Fingerabdrücken dort ebenfalls.
- **UI/Sync:** Steam und IP teilen sich die Modusauswahl. IP-Lobbys können ein optionales Passwort erhalten; für geschützte Lobbys fragt die Liste beim Beitritt danach. Die Spielseite sendet nur Erstellen-/Beitrittsanfragen. Fraktionen werden angezeigt, aber der Server verweigert den Spielstart bis `FACTION-001`.
- **Konfliktbereich:** Die Spielseite benutzt die bereits auf `main` vorhandenen Payloads und Signaturen; keine neuen Nachrichtencodes. IP benutzt stets die lokale, signierte Kennung. Steam behält seine bestätigte Steam-Kennung.
- **Performance/Risiken:** Fingerabdrücke nur bei Änderungen der Hostliste in Einstellungen schreiben. Das IP-Passwort reist ohne TLS im Klartext; öffentlicher Internetbetrieb ist dadurch nicht abgesichert. RSA-Laufzeit in RimWorld bleibt ungeprüft.
- **Prüfung:** Mod gegen lokale 1.6-Assemblies bauen, vorhandene Protokoll-/Hosttests und Dokumentvalidator. Zwei echte RimWorld-Instanzen sowie Spielstand-Laden erst nach Benutzerwunsch prüfen.
- **Rollback:** Commit auf `codex/mode-ui` verwerfen; kein Protokoll- oder Servermigrationseffekt.
