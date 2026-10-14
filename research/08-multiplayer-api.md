# 08 — Multiplayer-API: verfügbares, historisches und ungeklärtes Interface

**Quellen:** https://github.com/rwmt/MultiplayerAPI/wiki (teilweise alte Wiki-Daten); https://github.com/rwmt/MultiplayerAPI (Quellprojekt); https://www.nuget.org/packages/RimWorld.MultiplayerAPI/ (Paket **0.6.0**, Framework `net472`). **Geltungsbereich: RimWorld 1.6; 1.7 nicht nachgewiesen.**

## Prinzip

Eine **SyncMethod** überträgt eine Aktion/ihre Argumente so, dass sie bei allen Peers im gleichen lockstep-Kontext statt nur lokal ausgeführt wird. Ein **SyncField** überträgt Feldänderungen, die in einer UI ohne unmittelbaren Command geschrieben werden. Ein **SyncWorker** de-/serialisiert benutzerdefinierte Typen über den Netzwerkkanal, idealerweise mittels stabiler Spiel-Referenzen.

Die API-Dokumentation enthält sowohl `[SyncMethod]`, `[SyncField]`, `[SyncWorker]` als auch `MP.RegisterAll()`, `MP.RegisterSyncMethod(...)`, `MP.RegisterSyncField(...)` und `MP.WatchBegin/Watch/WatchEnd`. **Das Verfügbarkeitssignal aus einem historischen Wiki ist kein verifizierter Beweis für die exakte `0.6.0`-Binary-Signatur**; vor Copy/Paste das lokale Paket reflektieren.

## API-Register mit Evidenzstatus

| API | Zweck | Status | Caveat |
|---|---|---|---|
| `Multiplayer.API.MP.enabled` | API einsatzbereit? | **BESTÄTIGT im öffentlichen Wiki** | unterscheidet „API initialisiert“ von „Multiplayer-Session aktiv“ |
| `MP.IsInMultiplayer` | echte MP-Session aktiv? | **BESTÄTIGT im Wiki** | Singleplayer kann API installiert haben, ohne Session |
| `MP.IsHosting` | Host-Status | **BESTÄTIGT im Wiki** | kein Freibrief für nur-Host-mutierende Weltlogik |
| `MP.RegisterAll()` | mit Attributen deklarierte Syncs registrieren | **BESTÄTIGT im historischen Guide** | bei Startup und mit `MP.enabled` Guard |
| `[SyncMethod]` | Methode registrieren | **BESTÄTIGT historisch** | serialisierbare Parameter; Aufrufkontext checken |
| `[SyncField]` | Feld registrieren | **BESTÄTIGT historisch** | UI Watch-Paar erforderlich |
| `MP.RegisterSyncMethod(type,methodOrPropertyName,argTypes)` | Reflection-registrierte Methode | **BESTÄTIGT historisch** | genaue Overloads/SyncType[] für lokale 0.6.0 prüfen |
| `MP.RegisterSyncField(type,memberPath)` | Feld mit Zieltyp/Pfad anmelden | **BESTÄTIGT historisch** | `MP.Watch` um UI-Änderung |
| `MP.WatchBegin()`, `MP.Watch(...)`, `MP.WatchEnd()` | Sync-UI-State-Änderungen erfassen | **BESTÄTIGT historisch** | als zusammengehöriger Scope, immer sauber schließen |
| `[SyncWorker]`, `SyncWorker.Write/Read` | unbekannte Objekttypen übertragen | **BESTÄTIGT historisch** | Objektidentität und Nicht-Null beachten |
| `MP.IsExecutingSyncCommand` | angeblicher Indikator aktiver Sync-Command | **UNGEKLÄRT** | weder Name noch Signatur in überprüften 0.6.0-Belegen bestätigt, vor Nutzung suchen |

## Synchronisierte Aktion: Muster, **nicht kompiliert**

```csharp
using Multiplayer.API;
using Verse;

[StaticConstructorOnStartup]
public static class ExampleSyncRegistration
{
    static ExampleSyncRegistration()
    {
        if (MP.enabled)
            MP.RegisterAll();
    }
}

public static class ExampleCommands
{
    [SyncMethod]
    public static void SetThingEnabled(Thing thing, bool enabled)
    {
        // NUR SCHEMA: realen ThingComp-Typ/Validierung ergänzen.
        // Die Implementierung muss im synchronisierten Command-Kontext laufen.
    }
}
```

Wichtige Details: `Thing` muss zur Ausführungszeit valide sein; `[SyncMethod]` synchronisiert eine Methode, **nicht automatisch** externe Nebenwirkungen (Dateisystem, lokale Events), mutable globale Felder oder beliebige Closure-Kontexte. Falls für Fremdmethoden kein Attribut möglich ist, `MP.RegisterSyncMethod` mit per Reflection geprüfter konkreter Signatur.

## Synchronisiertes UI-Feld: Muster

```csharp
// Nicht lauffähige Demonstration: registriertes ExampleWidget.enable-
// Feld muss auf einer sicher referenzierbaren Instanz liegen.
if (MP.IsInMultiplayer) MP.WatchBegin();
try
{
    if (MP.IsInMultiplayer)
        MP.Watch(widget, nameof(widget.enabled));
    // ... Widget/UI-Code könnte widget.enabled verändern ...
}
finally
{
    if (MP.IsInMultiplayer) MP.WatchEnd();
}
```

**Quellenabweichung:** der alte Wiki-Guide dokumentiert `Watch`-Parameter in verschiedenen Overloads; exakten `MP.Watch(widget, nameof(widget.enabled))`-Overload in der installierten API prüfen. Falsche Registrierung führt zu null-/type-Fehlern, fehlender Synchronisation oder leisen Divergenzen.

## Serialisierung mit SyncWorker (historisches API-Schema)

```csharp
[SyncWorker]
static void SyncCustomTarget(SyncWorker sync, ref CustomTarget obj)
{
    if (sync.isWriting)
        sync.Write(obj.thing); // instanzgebundene Spielreferenz, NICHT Pointer
    else
        obj = new CustomTarget(sync.Read<Thing>());
}
```

**Nur Schemacode**, `CustomTarget` ist ein Platzhalter. `SyncWorker` muss ohne Seiteneffekte reproduzierbar lesen/schreiben und keine nicht serialisierbaren UI-/Map-Context-Objekte im Transport behalten. Der Wiki-Guide nennt außerdem `shouldConstruct=true` für parameterlose Typen.

## Entscheidungsbaum

- Aktion verändert Spielmodell (`SetRecipe`, `ToggleMachine`)? → **SyncMethod** mit deterministischem Handler.
- UI verändert ein Feld direkt, ohne Handler? → bevorzugt Handler refaktorieren; nur wenn nötig **SyncField+Watch**.
- Argument enthält eigenen Class/Struct-Typ? → `SyncWorker` mit stabiler Identität und unterstützten API-Serialisierungen.
- Rein lokale UI-Hilfe/Tooltip/Mouseover? → **nicht syncen**, solange keine Weltdaten beeinflusst werden.

## Validierung

1. Lokale NuGet-`0.6.0`-API-Signatur reflektieren und gegen Repos/Commit pinnen.
2. Mod in Singleplayer ohne MP API-Assembly testen (wenn optionale MP-Dependency vorgesehen).
3. MP-Session mit Host + Client starten; Klick **nur auf Client**, Handler einmal je Peer pro Command; Game-State nach N Ticks vergleichen.
4. Gleiches für Host-Klick, mehrfaches Toggle, schnelle Klickserie, Pause, Save/Load, Join/Disconnect.
5. Unerwünschte double-execution, unpassende Map, fehlende Null-Handling, UI-Rückspulen oder fehlenden Watch-End prüfen.

**Verwechslungswarnung:** `Multiplayer.Client.Multiplayer` aus dem MP-Repositorium ist **nicht** dasselbe wie `Multiplayer.API.MP` des Modding-Pakets.
