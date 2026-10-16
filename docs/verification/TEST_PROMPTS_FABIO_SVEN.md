# Kopierbare Aufträge für den späteren Spieltest

Die folgenden Aufträge gelten **erst**, wenn Fabio den Spieltest starten möchte. Aktuell keine RimWorld-Instanzen für automatisierte Tests öffnen. Vor der Paketierung alle vorgesehenen Branches prüfen und nur nach ausdrücklicher Freigabe mergen.

## Prompt 1: Testpaket vorbereiten

> Lies `AGENTS.md`, `docs/ai/ASSIGNMENTS.md`, `docs/ai/PROJECT_STATE.yaml`, `docs/ai/TASKS.yaml` und die Übergaben beider Bahnen. Prüfe Git-Status, die aktuellen Branches `codex/ver-tickpath`, `codex/command-pipeline`, `codex/mode-ui` und `main` sowie Review-Berichte. Baue die Mod gegen die lokal installierten RimWorld-1.6-Assemblies, führe den Dokumentvalidator und die relevanten spielunabhängigen Testprogramme aus. Erstelle danach **ein eindeutig versioniertes ZIP** mit exakt demselben Mod-Build für Fabio und Sven; prüfe Inhalt und SHA-256. Stelle das ZIP und eine kurze Installationsanleitung bereit. Starte RimWorld noch nicht. Führe keinen Merge oder Push ohne meine dafür geltende Freigabe aus.

## Prompt 2: Fabio und Sven testen zwei Rechner

> Wir beginnen jetzt den manuellen Test mit Fabio und Sven. Verwende auf beiden Rechnern das identische Mod-ZIP aus Prompt 1 und notiere SHA-256, RimWorld-Build, Betriebssystem und Modliste. Führe Steam-Einladung und IP-Verbindung getrennt aus. Prüfe Lobbybeitritt, Passwort, Ready, Modus-Anzeige, gemeinsame Einrichtung, Landung, Pause/Tempo, gleiche Tick-/Hashwerte, einen Befehl, Save/Load, Reconnect und Desync-Reparatur. Protokolliere für jeden Schritt Ergebnis und Uhrzeit. Bei einem Desync sichere **beide** `Player.log`-Dateien, den ersten abweichenden Tick/Hash und die betroffene Aktion; stoppe danach den Testlauf für Ursachenanalyse. Behaupte keinen Erfolg für nicht durchgeführte Schritte. Aktualisiere die passenden Prüfberichte und Aufgabenstatus.

## Prompt 3: Determinismus-Go/No-Go ohne zweite Person

> Erst nach ausdrücklichem Startsignal für den Spieltest: Übernimm `TOOL-001` und `DET-001` auf einem eigenen Arbeitsbranch gemäß `docs/ai/ASSIGNMENTS.md`. Starte zwei lokale RimWorld-1.6-Prozesse mit getrennten Save-Verzeichnissen und identischem Mod-ZIP, verbinde sie über `127.0.0.1`, gleiche Ausgangsdaten und Modlisten. Vergleiche Hashmeldungen und Rand-Züge ab der ersten vollen Hashgrenze; melde den **ersten** abweichenden Tick mit beiden Logs und reproduzierbaren Schritten. Trenne Skript-/Harness-Prüfungen von einem tatsächlichen Spieltest. Keine Änderungen an normalen Spielständen, kein Merge ohne Freigabe.
