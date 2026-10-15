# ADR 0018 – Mod-Prüfung in der Lobby und Start-Knopf (MOD-001, START-001)

- **Status:** angenommen für `feat/mod-check-setup-ui` (Basis `feat/setup-rules`). Designnotiz nach `AGENTS.md` Regel 4. Plan §5.1/§5.2. Zuerst auf `feat/lobby-hardening` gebaut und vom Benutzer mit zwei Spielern getestet („works“), dann auf Wunsch des Benutzers auf die Protokollbasis von ADR 0014/0016/0017 übertragen.
- **Ziel:** Alle Spieler einer Lobby nutzen dieselben Mods in derselben Reihenfolge. Der Host startet die gemeinsame Einrichtung mit einem Knopf, der erst bei erfüllten Bedingungen aktiv wird.
- **Manifest:** Jeder Client liest beim Spielstart `LoadedModManager.RunningModsListForReading` (Package-ID, Name, `ModMetaData.ModVersion`, `RootDir`, `foldersToLoadDescendingOrder`) und hasht im Hintergrund je Mod `About/About.xml` sowie `Assemblies/**/*.dll`, `Defs/**/*.xml` und `Patches/**/*.xml` seiner Ladeordner. Texturen und Sounds zählen nicht. Kern plus fünf DLCs: 1 572 Dateien in 0,19 s.
- **Prüfsumme:** SHA-256 nur über die Mods in Ladereihenfolge (Package-ID, Version, Inhalts-Hash). **Spielversion und Anzeigenamen zählen nicht** (Entscheidung des Benutzers, 2026-10-15); die Spielversion wird als Hinweis angezeigt.
- **Server:** speichert je Mitglied nur die Zusammenfassung (`ModManifestSummary`) und vergleicht mit dem **aktuellen** Host. Status je Spieler im Lobbyzustand: `0` ungeprüft (niemand hat ein Manifest, z. B. Werkzeuge), `1` Prüfung läuft, `2` passt, `3` abweichend. In der Lobbyphase lehnt der Server `Bereit` bei `1`/`3` mit Code 8 ab, und `SetupAdvance(Scenario)` prüft erneut. Wechselt die Host-Rolle, wird neu verglichen und die Bereitschaft abweichender Mitglieder zurückgesetzt. Die Lobbyliste trägt die Zusammenfassung des Hosts.
- **Unterschiedsbericht:** `ModManifestRequest` geht an den Host; der Host sendet seine volle Liste mit dem Chunk-Kanal (Art `ModManifest`, ADR 0014) an den Anfragenden. Der Client vergleicht lokal: fehlend, zusätzlich, andere Version/Inhalt, Reihenfolge, Spielversion als Hinweis.
- **Start-Knopf:** Der Host sieht „Spiel starten“; daneben steht der erste fehlende Grund, berechnet mit `LobbyRegistry.StartBlockReason`, also nach denselben Regeln wie der Server. Bestätigung → `SetupAdvance(Scenario)`.
- **Protokoll:** Codes 28 `ModManifestSummary` und 29 `ModManifestRequest`; Mod-Status je Spieler im Snapshot; optionale Host-Zusammenfassung in der Lobbyliste. Die Steam-Lobby-Kennung `foundation_version` folgt jetzt der Wire-Version (`3`).
- **Tests:** Protocol (Manifest, Bericht, Inhalts-Hash, Prüfsumme ohne Spielversion, Nutzdaten), Lobby (Status, Bereit-Sperre, Start-Prüfung, Host-Wechsel), Hosting über Steam-ähnlichen Strom (Status, Sperre, Listenanfrage, Übertragung, Bericht). Spieltest: Benutzerbericht auf `feat/lobby-hardening`.
- **Rollback:** Commit zurücknehmen; Codes 28/29 entfallen, der Rest von ADR 0017 bleibt.

## Nachtrag 2026-10-15: Debug-Option „allein starten“

- **Anlass:** Wunsch des Benutzers, die Spieleinrichtung ohne zweiten Spieler testen zu können.
- **Regel:** `HostConfig.AllowSoloStart` (Standard `false`) senkt die Mindestspielerzahl für `SetupAdvance(Scenario)` von 2 auf 1. Der Server des Hosts setzt das durch; `StartBlockReason` erhält denselben Schalter, damit Knopf und Server übereinstimmen.
- **Mod:** Kontrollkästchen „Debug: Spiel allein starten erlauben (nur zum Testen)“ im Multiplayer-Fenster und in den Mod-Einstellungen; gilt beim nächsten Hosten (Direkt oder Steam). In der Lobby steht dann „Debug: Allein-Start erlaubt“. Alle übrigen Bedingungen (bereit, Mods passen) bleiben.
- **Dedicated Server:** unverändert, solange `AllowSoloStart` in der Konfiguration nicht gesetzt ist.
