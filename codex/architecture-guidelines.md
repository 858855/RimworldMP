# Codex — Architekturleitlinien

## Zielarchitektur eines neuen Mods

```text
MyMod/
  About/About.xml
  Defs/...
  Patches/...              # möglichst minimieren
  Languages/...
  Textures/...
  1.6/Assemblies/...      # net472, MP-Ziel bei Bedarf
  1.7/Assemblies/...      # net10.0, Waylith SP-Ziel (separat)
  LoadFolders.xml         # nur wenn tatsächliche Layout-Eignung bestätigt
  src/Core/               # pure Berechnungen und Validierung
  src/Defs/               # Verse Def/CompProperties
  src/Simulation/         # Components, Tick, Save/Load
  src/Commands/           # serialisierbare Aktionen
  src/UI/                 # ohne direkte Simulation-Mutation
  src/Compat/             # Harmony, MP API, andere Mods per opt-in
  tests/                  # unabhängig spielbare pure Funktionen
```

## Schichten und Invarianten

`Defs -> Domain model -> deterministic Simulation -> serialized save`; separate `UI -> validated Command -> Simulation`. `Compat/MP` registriert Command-Handlers, ohne die Core-Domäne vom MP-Mod hart abhängig zu machen (nur wenn optionales MP-Feature). `Compat/Harmony` kapselt Patches, dokumentiert Target+Patch-ID.

## Abhängigkeitsentscheidung

1. Für jedes Feature Def-basierte Vanilla-Lösung prüfen.
2. Component für Thing-/Hediff-/Game-/Map-gebundenen Status bevorzugen.
3. Scribe-Keys als bewusst migrierbare Schema-Schnittstelle behandeln.
4. Komplexe Game-Klassen nur erweitern, wenn vorhandene Hooks belegt.
5. Sync-Grenze an Commandmethoden, keine Netzwerkschicht in jedem Job/Thing.
6. Performancemessung vor Optimierung; keine Worker-Mutationen unter 1.6/1.7/MP ohne Beweis.

## Versionsvarianten

Trennung der Ziel-Frameworks ist **Pflicht**. Gemeinsamer reiner C#-Teil kann source-shared sein. Konfliktbehaftete Kompatibilitäts-DLLs zur Buildzeit trennen. Keinerlei 1.7-MP-Garantie wegen Prepatcher. `UnityStubs.dll` ist 1.7-Kompatibilitätsebene und nicht automatisch Zweck von eigener API-Migration.
