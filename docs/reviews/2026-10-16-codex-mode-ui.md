# Review: Spielmodus und IP-Identität im RimWorld-Fenster

- **Aufgabe:** MODE-001 (X5), Spieladapter für SEC-001
- **Quellbranch:** `codex/mode-ui`, basiert auf `main` @ `b4255c6`; Commits mit `git log origin/main..codex/mode-ui` prüfen.
- **Zielbranch:** `main` nach ausdrücklicher menschlicher Review-Freigabe.
- **Änderung:** Auswahl und Anzeige der vom Server festgelegten Spielmodi; Modus im gemeinsamen Save; optionales Passwort für IP-Lobbys; persistent signierte lokale IP-Kennung und gespeicherte Host-Bindungen. Fraktionen können gewählt, aber noch nicht gestartet werden.
- **Technik:** Nur RimWorld-Adapter und Dokumentation. ADR 0044 begründet UI, Persistenz und Trennung von Steam-/IP-Identität. Der Server und das Protokoll bleiben auf dem Stand von `main`.
- **Prüfung:** Mod-Build ohne Warnungen; Protocol- und Hosting-Konsolentests bestanden. Details: `docs/verification/2026-10-16-codex-mode-ui.md`.
- **Risiken:** Spiel-UI, RSA unter RimWorld-Mono und Save/Load wurden nicht zur Laufzeit geprüft. IP-Verkehr bleibt unverschlüsselt.
- **Manuell prüfen:** Mod auf zwei identischen Spielständen installieren, Modusliste und Passwortabfrage bedienen, Verbindung samt Reconnect prüfen, Save laden und Modus kontrollieren. Test und Paket sind auf später mit Fabio/Sven verschoben.
- **Review-Status:** `ready_for_human_review`; keine Freigabe dokumentiert, kein Merge.
