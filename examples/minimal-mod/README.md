# Minimaler Mod-Scaffold — RimWorld 1.6

- Verzeichnisse `About`, `Defs`, `src` als mögliche Startstruktur.
- `About/About.xml` hat eine syntaktisch valide `ModMetaData`-Form und unique `packageId`.
- `ResearchExample.xml` definiert eine **didaktische, möglicherweise unvollständige** `ThingDef` mit `ParentName=ResourceBase`; vor Laufzeiteinsatz Parent und restliche Felder in Vanilla-Defs validieren.
- `ExampleMod.cs` demonstriert `Verse.Mod`, `ModSettings` und `Scribe_Values.Look` für settings. **Die tatsächliche API-Signatur soll vor Build aus der lokal installierten 1.6-Assembly verifiziert werden.**
- C# `.csproj` referenziert nur `Assembly-CSharp.dll`; `UnityEngine`-Referenzen/Framework-Reference-Pack können fehlen. Kein erfolgreicher Build wird behauptet.

```bash
# NO WARRANTY: needs local RimWorld .NET Framework reference assemblies.
dotnet build src/ExampleMod.csproj -c Release /p:RimWorldManaged="/ABSOLUTE/PATH/TO/RIMWORLD_MANAGED"
```

**Nicht** auf 1.7 ungeprüft als `net472`-Projekt deployen. Für neuen 1.7-Code eigenes `net10.0`-Build und Ludeons `Waylith.dll`, `UnityStubs.dll` verwenden.
