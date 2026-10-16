# Review-Vorbereitung: erster synchronisierter Spielbefehl

- **Aufgabe/Branch/Ziel:** `SYNC-002`, `codex/command-pipeline` → später `main`, abhängig von `codex/resync-game` @ `0bde512`. Kein Merge ohne menschliche Freigabe.
- **Änderung:** Draft/Undraft wird im GUI-Kontext zuerst zum Server geschickt. Der Koordinator ordnet die Aktion; der Tick-Treiber führt sie mit eigener Rand-Neusaat auf jedem Spiel aus. Ein nicht mehr vorhandener Pawn führt zum protokollierten No-op.
- **Betroffene Dateien:** `GameCommandCodec`, `SessionGameCommandPatch`, `SessionTickDriver`, `FoundationMod` und zugehörige Dokumentation. Keine Wire-Protokolländerung.
- **Prüfung:** Siehe `docs/verification/2026-10-16-command-pipeline-first-action.md`.
- **Risiken/manuell prüfen:** Zwei echte RimWorld-Clients: Draft/Undraft auf beiden sichtbar, gleiche Tickgrenze/Hash; Klick bei Pause und während Netzwerklatenz; fehlender Pawn; andere Harmony-Mods. Danach Ordered Jobs und Designatoren ergänzen.
- **Review-Status:** Zwischenstand, noch nicht bereit zur Integration; `SYNC-002` bleibt in Arbeit.
