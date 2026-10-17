# Erweiterungs-API für andere Mods (`Multiplayer.Foundation.Api`)

**Stand:** v1.0.0 auf `feat/mod-api` (ADR 0029, API-001). Im Selbsttest geprüft, noch nicht mit zwei Clients und noch nicht mit einem echten Fremd-Mod. Die Netzwerk- und Koordinatorbibliotheken bleiben ohne Verse/Unity; dieses Assembly verweist nur auf `Assembly-CSharp`.

## Grundidee

Eine Spieleraktion ist eine Methode. Du registrierst sie als **Sync-Methode**; die Foundation setzt einen Harmony-Prefix. Ruft ein Spieler die Methode in einer gemeinsamen Partie auf, wird der Aufruf nicht ausgeführt, sondern mit Empfänger und Argumenten kodiert an den Koordinator geschickt. An der nächsten Tickgrenze führt **jeder Client, auch der Aussteller**, die Methode mit denselben Werten aus. Innerhalb eines Befehls und innerhalb eines gemeinsamen Ticks läuft die Methode direkt (dort ist sie schon deterministisch). Außerhalb einer gemeinsamen Partie ändert sich nichts.

Regeln für Mod-Code:
- Spielzustand nur innerhalb einer Sync-Methode, eines Ticks oder wenn `MP.IsExecutingCommand` wahr ist ändern. UI-Code ruft die Sync-Methode auf und ändert selbst nichts.
- Keine lokalen Eingaben in der Methode (kein `Find.Selector`, kein `Input`, kein `Event.current`): Alles, was die Methode braucht, kommt als Argument.
- Gleiche Registrierungen auf allen Clients. Die Lobby-Prüfung erzwingt gleiche Mods, Versionen und Ladereihenfolge; Registrierungen dürfen nicht von Einstellungen oder Zufall abhängen.
- Rückgabewerte werden nicht transportiert (die Methode liefert dem Aufrufer `default`). Ergebnisse gehören in den Spielzustand oder in eine `Messages.Message`.

## Einbinden

`About.xml` deines Mods:
```xml
<modDependencies><li><packageId>rimworldmp.foundation</packageId><displayName>RimWorld Multiplayer Foundation</displayName></li></modDependencies>
<loadAfter><li>rimworldmp.foundation</li></loadAfter>
```
Projektverweis auf `Multiplayer.Foundation.Api.dll` aus dem Mod-Ordner `Assemblies/` (nicht mitliefern; die Foundation bringt sie mit). Soll dein Mod auch ohne Foundation laufen, kapsle alle `MP`-Aufrufe in einer eigenen Klasse und rufe sie nur, wenn `ModLister.GetActiveModWithIdentifier("rimworldmp.foundation") != null`.

```csharp
using Multiplayer.Foundation.Api;

[StaticConstructorOnStartup]
static class MeinModMultiplayer
{
    static MeinModMultiplayer()
    {
        if (MP.IsAvailable) MP.RegisterAll();   // Worker, dann Hash-Beiträge, dann Methoden dieses Assemblys
    }
}
```

## Sync-Methoden

```csharp
public static class MaschinenBefehle
{
    [SyncMethod]
    public static void SetzeAktiv(CompMaschine comp, bool aktiv)
    {
        comp.aktiv = aktiv;   // läuft auf jedem Client am selben Tick
    }
}
```
Statt des Attributs: `MP.RegisterSyncMethod(typeof(MaschinenBefehle), nameof(MaschinenBefehle.SetzeAktiv))` oder mit `MethodInfo`. Instanzmethoden sind erlaubt, wenn der Empfängertyp serialisierbar ist (z. B. eine `ThingComp`-Ableitung oder ein Typ mit Worker). Nicht erlaubt: generische oder abstrakte Methoden, `ref`/`out`, Instanzmethoden von Strukturen.

Die Kennung einer Methode ist ein 64-Bit-Hash ihrer Signatur (Typ, Name, Parametertypen). Umbenennen oder Parameter ändern ändert die Kennung; das ist unkritisch, solange alle Clients dieselbe Mod-Version haben.

Ein Befehl darf höchstens 32 KiB groß sein; größere werden nicht gesendet (Statuszeile im Overlay, Logzeile), und das Original läuft trotzdem nicht.

## Unterstützte Typen

| Typ | Kodierung |
|---|---|
| `bool`, `byte`, `sbyte`, `short`, `ushort`, `char`, `int`, `uint`, `long`, `ulong`, `float`, `double` | Wert (Big-Endian) |
| `string` | Null-Bit, UTF-8 bis 65 535 Byte |
| Enums | Int64; undefinierte Werte werden abgelehnt (außer `[Flags]`) |
| `Nullable<T>`, `T[]`, `List<T>` | Vorhanden-Bit bzw. Anzahl (≤ 65 535) + Elemente |
| `IntVec3`, `IntVec2`, `Rot4`, `CellRect`, `LocalTargetInfo` | Wert; Ziel als Thing oder Zelle |
| `Def` und Ableitungen | `defName`, bei abweichendem Laufzeittyp zusätzlich Typname |
| `Thing`, `Pawn` und Ableitungen | platziert: Karte + ID; Weltfigur: ID; in platziertem Behälter (Inventar, Container): Karte + Träger + ID |
| `ThingComp` und Ableitungen | Träger + Index in `AllComps` |
| `Map` | `uniqueID` |
| `Faction` | `loadID` |
| `Zone`, `Area` und Ableitungen | Karte + ID |
| `Bill` und Ableitungen | Werkbank + `GetUniqueLoadID()` |

Fehlt ein referenziertes Objekt beim Ausführen (zerstört, abgereist), wird der ganze Befehl auf allen Clients zum No-op mit einer Warnzeile im Log. Nicht platzierbare Dinge (z. B. ein Thing ohne Karte und ohne Träger) können nicht gesendet werden; der Aufruf wird verworfen und im Log vermerkt.

## Eigene Typen: SyncWorker

```csharp
public sealed class Auswahl { public Pawn Pawn; public int Menge; }

static class Worker
{
    [SyncWorker]
    static void Arbeite(SyncWorker sync, ref Auswahl wert)
    {
        if (sync.IsWriting) { sync.Write(wert.Pawn); sync.Write(wert.Menge); }
        else wert = new Auswahl { Pawn = sync.Read<Pawn>(), Menge = sync.Read<int>() };
    }
}
```
Oder `MP.RegisterSyncWorker<Auswahl>(Arbeite)`. `sync.Bind(ref feld)` schreibt oder liest je nach Seite. Ein Worker gilt auch für Ableitungen des Typs. Worker müssen vor den Methoden registriert sein, die den Typ verwenden; `RegisterAll` erledigt das.

## Zustand in die Hash-Prüfung

Alle 60 Ticks vergleicht der Koordinator einen Zustands-Hash aller Clients. Eigener synchronisierter Zustand, der nicht in Things, Bedürfnissen, Fraktionen oder Quests sichtbar ist, gehört hinein, damit eine Abweichung erkannt wird:
```csharp
[SyncHash]
static ulong Hash() => (ulong)MeineKomponente.Zaehler * 31 + (ulong)MeineKomponente.Stufe;
```
Oder `MP.RegisterHashContributor("meinmod.zaehler", () => …)`. Nur synchronisierten Zustand einbeziehen, nie Rendering, Einstellungen oder lokale Auswahl. Beiträge werden in Kennungsreihenfolge gehasht; eine Ausnahme im Beitrag zählt als 0 und wird einmal gemeldet.

## Abfragen

| Mitglied | Bedeutung |
|---|---|
| `MP.IsAvailable` | Foundation hat die Implementierung installiert |
| `MP.IsInSession` | aktuelle Partie ist eine gemeinsame (ab dem gemeinsamen Start) |
| `MP.IsHost` | dieser Client hostet die Lobby |
| `MP.IsExecutingCommand` | gerade wird ein geplanter Befehl ausgeführt |
| `MP.LocalPlayerKey` | Schlüssel des lokalen Spielers (Lobby), sonst `null` |
| `MP.ExecutingIssuer` | Schlüssel des Spielers, der den laufenden Befehl ausgelöst hat; `null` für Koordinatorbefehle |

Berechtigungen (wer darf welche Figur steuern, ADR 0026) prüft die Foundation nur für ihre eigenen Befehle. Eine Mod kann `MP.ExecutingIssuer` in ihrer Methode mit dem Besitzer der Figur vergleichen und bei Abweichung nichts tun.

## Was fehlt

- `SyncField`/`Watch` für Felder, die UI-Widgets direkt schreiben, und `SyncDelegate` für Gizmo-Lambdas (Plan §7.2). Umgehung: ein Knopf ruft eine Sync-Methode mit dem neuen Wert.
- `GlobalTargetInfo`, `Verb`, `Lord`, `Quest`, `Ideo` als Argument.
- Kompatibilität mit dem rwmt-`Multiplayer.API`-Vertrag (API-002) und die Mod-Matrix mit Lobby-Warnung (COMPAT-001).
- Zwei-Client-Nachweis mit einem Beispiel-Mod (`examples/multiplayer/FoundationApiExample.cs` ist nicht gebaut).

## Versionierung und Abkündigung

`MP.ApiVersion` folgt SemVer. Neue Mitglieder heben die Minor-Version (1.1.0); kein bestehendes Mitglied ändert Bedeutung oder Signatur innerhalb einer Major-Version. Zu entfernende Mitglieder werden eine Minor-Version lang als `[Obsolete]` mit Hinweis geführt und erst in der nächsten Major-Version entfernt. Das Drahtformat der Befehle (Codec-Version 1) ist kein Teil dieser API; ungleiche Foundation-Versionen lehnt bereits die Lobby ab.
