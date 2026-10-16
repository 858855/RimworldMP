# Sitzungsübergabe

Codex und Claude arbeiten parallel in getrennten Bahnen; siehe `docs/ai/ASSIGNMENTS.md`.

- **Claude** (Koordinator und Protokoll): `docs/ai/handoff/claude.md`
- **Codex** (Spielseite): `docs/ai/handoff/codex.md`

Gemeinsamer Stand ist `main` @ `85af3a9`. Er enthält Lobby, Steam, Mod-Prüfung, gemeinsame Einrichtung bis zur Landung, Lockstep-Koordinator und Spielmodus. Nach der Landung bleibt das Spiel pausiert, bis der Tick-Treiber (Codex, X2) fertig ist. Spieltests sind laut Benutzer derzeit nicht möglich.
