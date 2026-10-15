# ADR 0008: Installierbares Mod-ZIP

- Status: angenommen für Testpaket 0.1.0-dev
- Ziel: Den gebauten RimWorld-1.6-Client ohne Steam Workshop manuell installierbar machen.
- Betroffene Dateien/Klassen: `mod/About/About.xml`, drei eigene Assembly-Projekte; keine Änderung an Spielzustand, UI oder Netzprotokoll.
- Zustandsmodell/Persistenz: Paket enthält keine Laufzeitdaten oder persönlichen Einstellungen. Die Mod legt Einstellungen erst im Spiel an.
- UI-Aktionen: Mod im RimWorld-Modmenü aktivieren; Verbindung anschließend über die Mod-Einstellungen. Kein gemeinsamer Spielstart.
- Netzwerk-Sync: bestehender `LobbyClient`; das ZIP enthält keinen Serverprozess.
- Konfliktbereich: Nur RimWorld 1.6 als Ziel; ZIP enthält keine RimWorld-, Unity- oder anderen proprietären DLLs.
- Performance-Risiko: keines zur Laufzeit durch die Paketierung.
- Tests: Release-Build, exakte ZIP-Einträge, XML-Syntax, DLL-Präsenz, Prüfsumme; Spiel-UI bleibt separat offen.
- Rollback: ZIP und Branch entfernen beziehungsweise durch neues Paket ersetzen; keine Installation auf Benutzerrechnern wird automatisiert.
- Entscheidung: Ein Top-Level-Ordner `RimWorldMultiplayerFoundation/` im ZIP wird in `RimWorld/Mods/` entpackt. Das Paket wird als experimenteller Download auf einem Arbeitsbranch bereitgestellt.
- Alternative: GitHub-Releases-Asset. Dafür ist in dieser Umgebung kein `gh`-Werkzeug verfügbar; das versionierte ZIP auf GitHub ist direkt herunterladbar und benötigt keinen Merge.
