# Codex — Schritt-für-Schritt-Mod-Workflow

1. **Research Freeze:** Recherchedatum, Spiel-/Mod-/Framework-Version, relevante Commits, konkrete API-Signaturen in `research-log.md` festhalten.
2. **Feature Specs:** Def-Namen, Datenmodell, Ticks/RNG, Zustands-Scribe, UI-Befehle, Sync/MP-Plan, Windows/Linux-Case testen.
3. **Skelett:** `About.xml`, unique packageId, unterstützte Versionen und Mods, `Defs`, separate src/build Varianten.
4. **XML zuerst:** Definitionen, PatchGuard, Sprachstrings, Texture/Sound-Assets, XPath und Def-Parents; `python3 scripts/validate_structure.py` ausführen.
5. **C# nur bei Bedarf:** SDK-Target festlegen (`net472`/`net10.0`), lokale DLL-Referenzen, `ThingComp`/Game/Map Components.
6. **Persistenz:** `ExposeData/Scribe`, New Game + Old Save, restart/serialization loops.
7. **MP (1.6):** registrierte Actions, SyncFields mit Watch, custom SyncWorker; UI und Tick strikt trennen.
8. **Harmony:** nur falls unumgänglich und mit Release-bezogenem Target/Regressionstest.
9. **CI:** XML-Syntax, Defreferenzen soweit möglich, Build, SHA der lokalen Quellen/DLL-Versionen.
10. **In-game:** SP-Start/Game Log, In-Game-Aktion, Save/Load/Restart.
11. **MP:** Host/Client, speed/pause, reconnect, multi-map, desync stress, checksums.
12. **Release:** Manifest/Changelog, Lizenz, Vergleich vorher/nachher, bestätigte/unklare Compatibility, Rollback.

## Lokales Finden von DLLs

```bash
RIMWORLD_DIR="${RIMWORLD_DIR:-$HOME/.steam/steam/steamapps/common/RimWorld}"
find "$RIMWORLD_DIR" -type f \
  \( -name 'Assembly-CSharp.dll' -o -name 'Waylith.dll' -o -name 'UnityStubs.dll' \) -print
# Beispiel: Dotnet-Projekt erwartet Build-Property RimWorldManaged:
# dotnet build src/MyMod.csproj -c Release /p:RimWorldManaged=/opt/mygame/Managed
```

**Nicht verifizierte Dateisystem-/Executablepfade vor Ausführung explizit prüfen.** Kein GUI-Verhalten allein aus Shell-Test ableiten.
