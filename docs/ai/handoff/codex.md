# Übergabe Codex (Spielseite)

- **Stand:** 2026-10-16. `codex/manual-test-candidate` wurde nach ausdrücklicher Benutzerfreigabe gepusht und mit `237ac93` in `main` gemergt. Git-HEAD bei Fortsetzung neu prüfen.
- **Inhalt:** X1 Tickpfad-Thread-Audit (`VER-001`), X4 erste Draft/Forbid/Goto-Befehle (`SYNC-002`, WIP), X5 Moduswahl und IP-Identität (`MODE-001`). Das installierbare Vorschau-ZIP liegt unter `releases/RimWorldMultiplayerFoundation-0.2.0-preview-f04cb14-rw1.6.zip` (SHA-256 in `docs/verification/2026-10-16-manual-test-candidate-merge.md`).
- **Prüfung:** Mod-Release-Build ohne Warnungen, acht .NET-Testprojekte und Projektvalidator bestanden. Kein RimWorld-Spielprozess mit diesem Stand gestartet. Keine Zwei-Spieler-Gleichheit nachgewiesen.
- **Offen:** X4 braucht weitere stabile Referenzen, Ordered Jobs und Designatoren. Parallele Pfadsuche ist ein Desync-Risiko. Harmony, Save/Load und RSA unter RimWorld-Mono sowie Gameplay-Gleichlauf sind im Spiel offen.
- **Nächster Schritt:** Fabio und Sven prüfen das identische ZIP nach `docs/verification/TEST_PROMPTS_FABIO_SVEN.md`. Codex setzt X4 auf neuem `codex/…`-Branch vom aktuellen `main` fort und dokumentiert jeden neuen Spielbefehl mit passendem Nachweis.
- **Regeln/Befehle:** `docs/ai/ASSIGNMENTS.md`; Mod-Build: `env DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh`; Validator: `python3 scripts/validate_project_state.py`. Kein weiterer Merge oder Push ohne neue ausdrückliche Freigabe.
