# Codex — Entwicklungsregeln

Die verbindlichen Vorgaben stehen in `../AGENTS.md`. Dieses Dokument ist die konkrete PR-Checkliste.

## Vorab

- [ ] Ziel-RimWorld 1.6 oder 1.7 **mit Buildnummer** und installierter Runtime gefunden.
- [ ] SP-only oder MP-Zielentscheidung dokumentiert. 1.7+MP: **BLOCKED/UNVERIFIED**.
- [ ] Geplante Funktion auf Vanilla XML/Defs, dann Components, dann Harmony untersucht.
- [ ] Direkte Quellen zu *allen* verwendeten Methoden, Gameklassen, MP-Overloads geprüft.
- [ ] `modDependencies`, `loadAfter`, `supportedVersions`, gesonderte Assemblies definiert.

## Codequalität

- [ ] Methoden klein, UI vom Zustand getrennt, stabile Thing/Map/World-IDs.
- [ ] Caches rebuildable, Scribe umfasst persistierte Variablen, keine Game-State-Mutation außerhalb Tick/Command.
- [ ] Exceptions mit Kontext protokolliert, kein geschluckter Fehler.
- [ ] Kein proprietärer Code in Repo; Lizenz jeder fremden Library berücksichtigt.
- [ ] Geänderte XML-Defs & XPath nach Game-Def-Struktur validiert.

## Abnahme

- [ ] XML- und statische Checks durchgeführt.
- [ ] Kompiliert mit **genauer lokaler Referenzversion**; bei fehlender Spielassembly als nicht getestet markieren.
- [ ] Reale Singleplayer-Tests mit Startup, Save/Load, Featurezustand.
- [ ] Reale Multiplayer-Tests, falls MP versprochen wird; Commands, RNG, Reconnect, multi-map.
- [ ] Arbeitsbericht mit Build-/Test-Logs, Version-Matrix, verbleibenden Risiken.
