# Codex — Fehlerdiagnose und Minimal-Repro

1. **Sichten:** vollständiger Stacktrace, erste Exception, Spielbuild, Mods, DLCs, OS, installierte DLLs und Harmony-ID.
2. **Klassifizieren:** XML-Def nicht geladen, AssemblyLoadException, MissingMethod/Type, Harmony Conflict, `Scribe`-Load, logischer Gameplaybug, Desync.
3. **Minimalisieren:** reproduzierbaren Save/Seed sichern; halbierte Modliste, neuester reproduzierbarer Tick und eine einzelne Aktion.
4. **Eigentümer:** Exceptioncallstack und Patchbesitzer unterscheiden; ein stacktrace mit mehreren Transpilern macht den ersten aufgelisteten Patch nicht automatisch zur Ursache.
5. **Synchronisation:** in `TickPatch.RunCmds` `ScheduledCommand`/Tick/MapID verfolgen, in `AsyncTimeComp.ExecuteCmd` World/Map/Faction-Context prüfen.
6. **RNG:** Anzahl/Quelle der Zufallsziehungen vor erster Divergenz. Kein ersetzter Random-Seed ohne Ursachenbeleg.
7. **Persistenz:** `ExposeData`, game/map resets, first tick after save/load, nullable/unspawned reference.
8. **Fix:** minimaler Eingriff in Source, ggf. Featureflag; kein breiter Prefix `false` ohne Begründung.
9. **Validieren:** originaler Minimalfall plus Regressionstest und Langlauf, sowohl Host als auch Client.
10. **Berichten:** Ursachenbeleg, betroffene Zeilen/Commits, exakte Codeänderung, Build/Teststatus, Remaining Risk.

**Kein ungezieltes Löschen von Savegames/Prefs, keine Behauptung eines „Reset“-Fixes, ohne Ursache und Backup.** Der Nutzer muss eine experimentelle Spielstandmigration ausdrücklich kontrollieren können.

Detail: `../research/11-desync-debugging.md`.
