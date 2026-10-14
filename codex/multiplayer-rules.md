# Codex — Verbindliche Multiplayer-Regeln und Muster

## Ausgangspunkt

Ziel **RimWorld 1.6** + `rwmt/Multiplayer` + passende Prepatcher-/API-Version. 1.7 vor offizieller/technischer Portierung kein Ziel für MP-Zusage. Lies zuerst `../research/07-multiplayer-architecture.md` bis `11-desync-debugging.md`.

## Architekturvertrag

- **Command Query Separation:** UI darf nur die Absicht formulieren; Commands mutieren reproduzierbaren Zustand unter identischer Simulationszeit bei allen Peers.
- **State ownership:** `GameComponent`, `MapComponent`, `ThingComp` statt globaler mutable statics; `Scribe` speichert alle gameplayrelevanten Felder.
- **Event order:** kanonische stabile IDs und sortierte Collections, reproduzierbare Tie-Breaks; keine `HashSet`-/`Dictionary`-Iteration ohne Sortierung für weltverändernde Entscheidungen.
- **RNG:** keine Wallclock-/`System.Random`-/Visual-RNG-Entscheidungen für Weltzustand, kontrollierte `Verse.Rand`-Benutzung nach Codeprüfung. Gleiche Seeds plus **gleiche Draw-Anzahl**.
- **Tick:** Zeit per `TickManager`/Map-/World-Kontext. Frame-Update, GUI-Draw und Unity-/Waylith-Renderzeit sind keine legitimen Mutationsticks.
- **Serialization:** `SyncMethod` für echte Aktionen; `SyncField+Watch` für UI-Felder; `SyncWorker` für custom Types. Signaturen aus `Multiplayer.API` der installierten Version prüfen.
- **Context:** `Thing`/Map-ID/Pawn statt aktuell geöffnetem UI-Map-Kontext. `Find.CurrentMap` kann zwischen Peers unterschiedlich sein. Importierte MP-Interna sind keine öffentliche stabile API.
- **Settings:** Gameplay-Konfig synchronisierte Zustandskomponente; lokale Anzeigeoptionen separat. Gleiche Mods/DLC-/Harmony-Reihenfolge.
- **Harmony:** keine Änderung an `TickPatch`, Scribe/Rand/Job-Loop ohne gezielte Zusammenarbeit/Tests; minimale Patches, Konfliktmatrix.

## Verbotene Abkürzungen

- Kein „lokaler Button plus `if (host)`“-Trick, der Clients überspringt.
- Kein `DateTime.UtcNow` als Gameplay-Cooldown.
- Kein `Task.Run` mit mutierendem Pawn-/Map-Datenzugriff.
- Keine ungetestete Reflection-Methode, die nur manchmal existiert.
- Kein dynamisch erzeugter GUID ohne synchronisierte Verteilung als persistent relevante ID.
- Kein „Build erfolgreich = MP funktioniert“.

## API-Entscheidung

| Änderung | Mechanismus | Validierung |
|---|---|---|
| Start/Stop einer Maschine | `MP.RegisterAll` + `[SyncMethod]` | Click nur bei Client; beide Peers gleicher Status |
| veränderlicher UI-Slider | `SyncField` + `WatchBegin/Watch/WatchEnd` (wenn zwingend) | Slider auch bei Pause und nur einem offenem Fenster |
| serialisierter custom command arg | `SyncWorker` | 2 Clients+Save/Load+Null/-despawned object |
| passive Anzeige | keine Multiplayer-Sync | UI offen/geschlossen darf keine Weltänderung auslösen |

### Grenzfall Simulations- vs. clientlokaler State

UI-Hover-Highlights, Fensterposition oder Cursor können lokal sein; Auswahl eines neuen Forschungsprojekts, Maschinenmodus, globale Prioritäten, Kolonie-Befehl, Jobqueue und Pawn-Order müssen synchronisiert werden. Visuelle Zufallswerte können lokal sein, dürfen aber nicht `Verse.Rand` des Gameplays verbrauchen.

### Mindestnachweis

Zwei unabhängige Clientprozesse. Jeweils exakt same World/Map/Thing, Commands Host->Client und Client->Host, Pause/Unpause, 1x/3x Speed, Save/Load, Join/Reconnect, 30-Minuten-Langlauf und Replay-Diff. Pro Fehler: **erste divergent field/tick**. Dokumentieren, nicht nur Anzahl gemeldeter Fehlermeldungen.
