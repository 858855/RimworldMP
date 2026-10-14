# Verifizierung des Projektgrundstands — 2026-10-14

## Umgebung

- Repository-Branch: `docs/project-governance`; Basis-Commit `0909d11`.
- Lokale Spielversion aus `Version.txt`: `1.6.4871 rev598`.
- Steam-Manifest `appmanifest_294100.acf`: Build-ID `23969874`.
- Kein RimWorld-Prozess und kein Multiplayer-Client für diese Prüfung gestartet.

## Tatsächlich ausgeführt

| Befehl | Ergebnis | Bedeutung |
|---|---|---|
| `sha256sum -c MANIFEST.sha256` im frisch entpackten Archiv | Alle 49 Einträge `OK` | Gelieferter Archivinhalt vor der Ergänzung geprüft |
| `python3 scripts/validate_structure.py` | `PASS`, 3 XML-Dateien | XML syntaktisch gültig und einfache `About.xml`-Struktur vorhanden |
| `python3 scripts/verify_knowledge_archive.py` | `PASS`, 49 Einträge | ZIP-Inhalte stimmen mit dem eingebetteten Manifest überein |
| PyYAML-Parse von `PROJECT_STATE.yaml` und `TASKS.yaml` | `PASS` | YAML lesbar; Aufgabenstatus und Abhängigkeiten konsistent |
| `git diff --check` | Exit 0 | Keine Whitespace-Fehler in verfolgten Änderungen |

## Nicht verifiziert

- C#-Kompilation und lokale API-Binärsignaturen.
- RimWorld-Start, Def-Laden, Save/Load und Laufzeitverhalten.
- Zwei-Client-Multiplayer, Determinismus, Reconnect und Desync-Freiheit.
- RimWorld-1.7-Kompatibilität.

Die Root-`AGENTS.md` wurde um Governance-Regeln ergänzt und stimmt deshalb absichtlich nicht mehr mit der Archiv-Prüfsumme überein. Für wiederholbare Quellprüfung `python3 scripts/verify_knowledge_archive.py` verwenden. Die XML-Prüfung ist kein Mod-, Spiel- oder Multiplayer-Test. Der Branch wird nach Commit erneut auf einen sauberen Git-Status geprüft.
