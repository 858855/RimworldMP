# 04 — XML: Defs, Vererbung, PatchOperation, XPath

## Modell

`Def` bezeichnet eine geladene Definition; `ThingDef` definiert Gegenstände/Pawns/Gebäude; weitere gängige Def-Typen sind `RecipeDef`, `ResearchProjectDef`, `HediffDef`, `JobDef`, `IncidentDef`, `StatDef`, `PawnKindDef`, `FactionDef`. Namen und Felder variieren je Spielversion, insbesondere DLC-Defs. Vor dem Verwenden eines XML-Felds im tatsächlich geladenen `Core/Defs` oder der passenden Reflection-Definition nachsehen.

## PatchOperation

Die Vanilla-Mechanik lädt XML-Patches aus `Patches/` und verwendet XPath-Zielselektoren. Offene Community-Referenz: https://rimworldwiki.com/wiki/Modding_Tutorials/PatchOperations

```xml
<?xml version="1.0" encoding="utf-8"?>
<Patch>
  <Operation Class="PatchOperationAdd">
    <xpath>Defs/ThingDef[defName="Example_Target"]/comps</xpath>
    <value>
      <li Class="Example.Namespace.CompProperties_Example" />
    </value>
  </Operation>
</Patch>
```

**WARNUNG: schematisch, nicht lauffähig ohne `Example_Target` und Example-Assembly!** Die Syntax demonstriert nur die Form; `PatchOperationAdd` benötigt existierenden XPath-Zielknoten. Patches müssen geordnet sein, XPath darf nicht unbeabsichtigt mehrere Einträge treffen, Patch-Log auf `failed` prüfen. Nutzung von `PatchOperationReplace`/`PatchOperationRemove` kann andere Mods überschreiben und soll mit Patch-Kontext abgesichert sein.

## XML-Def-Beispiel (ebenfalls schematisch)

```xml
<Defs>
  <ThingDef ParentName="ResourceBase">
    <defName>Example_SampleResource</defName>
    <label>sample resource</label>
    <description>A research example, not a shipped object.</description>
    <stackLimit>75</stackLimit>
  </ThingDef>
</Defs>
```

**Für die konkrete 1.6-/1.7-Version müssen** Parent-Name, notwendige Thing-Felder (Graphic, Kategorie, StatBases etc.), Texture-Pfade, DLC-Features und Ladefolge gegen Core-Defs verifiziert werden. Solche Beispiele sind kein Ersatz für einen vollständigen ThingDef.

## Def-Vererbung und Namen

`ParentName` weist auf ein abstraktes Eltern-Def/XML-Template (nicht auf C#-Vererbung). Änderungen am Eltern-Def können viele Untertypen beeinflussen; gleiche `defName` in Modlisten führt zu Konflikten. `packageId` ist die Mod-Identität, `defName` die Definition innerhalb des Spiels. Def-Patching beim Startup passiert vor der laufenden Simulation und muss für alle Peers identisch sein.

## Aufgaben-spezifische Checks

| Typ | Überprüfen |
|---|---|
| ThingDef | Parent, `thingClass`, `comps`, Stats, Graphic, Inventar-/Spawn-Kontext |
| RecipeDef | Product, Ingredients, `workAmount`, Werkbank/Research |
| ResearchProjectDef | prereqs, costList, UI-Presentation |
| HediffDef | vorhandene `HediffCompProperties`, Health/CrossRefs |
| JobDef | `driverClass` muss C#-Assembly und passende Base-Class enthalten |
| IncidentDef | `workerClass`, Targeting/RNG und DLC-Manager |
| StatDef | `workerClass`, StatParts, Cache/Invaliderung |
| PawnKindDef/FactionDef | Pawn-Lifecycle, Faction-Reputation, Worldgenerierung |

## XML-Tests

1. XML parser/wellformedness; 2. `About.xml` `packageId`/`supportedVersions`; 3. `Defs`-Elemente gegen vorhandene Basistypen/Parents; 4. XPath in echter Spiel-Def-Datenbank; 5. Fehlerlog/Debug-Konsole; 6. Spawn/Forschung/Rezepte im Singleplayer; 7. gleiche Defs, IDs und DLCs auf Host und Client. **Ein parsergültiger Patch kann zur Laufzeit fehlschlagen.**
