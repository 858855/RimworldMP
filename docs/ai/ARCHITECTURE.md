# Architekturstand

## Tatsächlicher Zustand

Das Repository enthält eine RimWorld-Wissensbasis, Recherche, Workflows, Referenzen und **nicht kompilierten Beispielcode**. Es enthält noch keinen produktiven Mod, keine Netzwerkimplementierung und keine getestete Runtime-Komponente. Der Dateiplan steht in `CODEMAP.md`.

## Vorgaben für spätere Implementierung

- Ziel für unmittelbare Multiplayer-Arbeit ist der lokal vorhandene 1.6-Zweig (`1.6.4871 rev598`); 1.7/Waylith bleibt ein separater experimenteller Pfad ohne MP-Zusage.
- Bevorzugte Reihenfolge: Vanilla XML/Defs, dann Components, erst danach begründete Harmony-Patches.
- Vorgesehene Schichten: `Defs -> Domänenmodell -> deterministische Simulation -> Save`; `UI -> validierter Befehl -> Simulation`.
- Simulationszustand wird game-/map-/thing-gebunden persistiert; synchronisierte Aktionen mutieren ihn im autorisierten Tick-/Command-Kontext.
- Build-Ziele und Referenzen müssen nach Version getrennt bleiben: 1.6 `net472`, 1.7 `net10.0`.
- API-Signaturen, MP-Synchronisation und Kompatibilität werden für das **konkrete** Feature gegen feste Quellen und lokale Assemblies geprüft.

Diese Leitlinien sind noch keine implementierte Architektur. Begründungen und Beispielstruktur stehen in `codex/architecture-guidelines.md`; die derzeitige Entscheidung in `docs/decisions/0001-research-before-mod-code.md`.
