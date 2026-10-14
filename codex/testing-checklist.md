# Codex — Test-Checkliste

## Automatisch/lokal

- [ ] Modverzeichnis, About.xml, XML-Wellformedness, `packageId`, supportedVersions.
- [ ] XML-Patch-Ziele gegen tatsächliche Defs; ParentDefs/StatNames gegen Zielspiel.
- [ ] Kein Case-Mismatch in Textures/Includes/Assemblies.
- [ ] Reproducible Restore/Build, keine lokalen proprietären DLLs im Repo.
- [ ] Keine unerwarteten neuen Harmony-Patches; keine externen Netzwerkanfragen in Game-Tick.

## Echte Laufzeit Singleplayer

- [ ] Vanilla + Mod startet; Def- und Patch-Log sauber.
- [ ] Neue und bestehende Savegames; Mod-Einstellungen und Scribe bleiben erhalten.
- [ ] Tick-Raten, Pausen, UI-Bedienung, Game-Session-Wechsel.
- [ ] Neue Items/Recipes/Research/AI/Faction-Hooks entsprechend Feature.

## Echte Laufzeit Multiplayer

- [ ] 1.6-Spielbuild + MP Release + Prepatcher + API exakt dokumentiert.
- [ ] Host/Client gleiche Modlist/DLC/Settings/Harmony-Reihenfolge.
- [ ] Host-Klick, Client-Klick, doppelter Klick, Pause, 1x/3x Speeds.
- [ ] Wiederbeitritt, Save/Load und Sessionwechsel.
- [ ] Zwei Karten und unterschiedliche View-/UI-Kontexte.
- [ ] 30 Minuten Stresstest mit Desync-Erkennung und Logs beider Peers.
- [ ] Linux-Windows Cross-OS nur falls explizit als unterstützt ausgewiesen.

## Release

- [ ] Tags statt mutable `master`-Branchpins.
- [ ] Nutzer bekommt Versionsmatrix und bekannte Einschränkungen.
- [ ] Nicht ausgeführte Tests explizit als `NOT_RUN`; insbesondere **1.7+MP unverifiziert**.
