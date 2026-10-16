# Erzähler je Fraktion und gemeinsame Schwierigkeit (ADR 0037) – Prüfung 2026-10-16

- **Branch/Basis:** `claude/faction-storyteller` von `main` @ `d568766`. Bahn Claude, `FACTION-004`.
- **Umgebung:** Linux, .NET SDK 10.0.402 / Runtime 10.0.13, lokale RimWorld-Assemblies `1.6.4871` (rev598).

| Befehl/Prüfung | Ergebnis |
|---|---|
| Reflection gegen `RimWorldLinux_Data/Managed/Assembly-CSharp.dll` (Regel 5) | `RimWorld.StorytellerDef` (`listVisible`, `listOrder`), `RimWorld.DifficultyDef` (`isCustom`), `RimWorld.Storyteller` (`def`, `difficultyDef`, `difficulty`, `incidentQueue`, `StorytellerTick()`), `Verse.Game.storyteller` (ein Feld), `Verse.Find.Storyteller`, `Verse.Def.AllowedDefNamesRegex` vorhanden. Einziges passendes Muster in den Assembly-Strings: `^[a-zA-Z0-9\-_]*$` (Zuordnung zum Feld: WAHRSCHEINLICH) |
| `DOTNET_BIN=…/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen, 0 Fehler |
| `Protocol.Tests` | Exit 0; 17 Gruppen, neu siehe unten |
| `Lobby.Tests` | Exit 0; 10 Szenarien, neu siehe unten |
| `Network`, `Session`, `Hosting`, `Client`, `Integration`, `ProcessIntegration` | Exit 0; unverändert bestanden |
| `python3 scripts/validate_project_state.py` | Exit 0 |

Neu in `Protocol.Tests`:
- `FactionOptions` und `StorytellerChoice` kommen unverändert zurück.
- **Abgelehnt** werden:
  - eine leere Liste (auch auf der Leitung), mehr als 32 Einträge und Doppelte
  - ungültige defNames: Leerzeichen, Umlaut, Punkt, mehr als 128 Zeichen
- **Lobbyzustand mit Fraktionsblock:**
  - Optionen, Plätze und Wahlen kommen unverändert zurück; ebenso der Zustand ohne Optionen.
  - Ein Coop-Zustand hat genau die Länge wie vor ADR 0037.
  - Doppelte Plätze und Platz 64 werden abgelehnt.

Neu in `Lobby.Tests`, mit `factionsSetupEnabled: true`, drei Spielern und einem Nachzügler:
- **Plätze:** Vor der Einrichtung gibt es keine. Beim Start erhält der Host 0, die anderen folgen in Beitrittsreihenfolge.
- **Phase:** Optionen und Wahl gibt es nur in der Phase `Storyteller`. Eine Wahl ist erst möglich, wenn die Liste da ist, und nur aus ihr.
- **Schwierigkeit:** Ein Gast kann Schwierigkeit und Liste nicht setzen (Code 3).
- **Fertig und Weiter:**
  - Fertig verlangt eine Wahl.
  - Weiter geht es nur, wenn alle gewählt haben und fertig sind; Erzwingen gibt es nicht.
  - `StorytellerBlockReason` nennt jeweils den Grund: Optionen fehlen, „Erzähler fehlt: Guest2“, „Warte auf: Guest2“.
- **Änderungen:**
  - Ein Wahlwechsel löscht nur das eigene Fertig; dieselbe Wahl noch einmal behält es.
  - Neue Schwierigkeit: alle verlieren ihr Fertig, die Wahlen bleiben. Eine kürzere Liste löscht nicht mehr angebotene Wahlen. Dieselben Optionen noch einmal ändern nichts.
- **Austritt vor `Generating`:** Platz und Wahl fallen weg, die anderen behalten ihre Nummern.
- **Weiter durch die Einrichtung bis `Playing`:** Plätze und Wahlen bleiben erhalten und überstehen das Lobbyzustand-Payload.
  - Ein neuer Spieler kann dem laufenden Fraktionsspiel nicht beitreten (Code 6, „no faction“).
  - Ein Zurückkehrender bekommt mit neuer Sitzung und demselben Schlüssel Platz 1 und „Randy“ zurück.
- **Coop:** keine Plätze; Optionen, Wahl und Fertig in der Phase `Storyteller` abgelehnt; Weiter ohne Wahl.
- **Abbruch** löscht Plätze, Wahlen und Optionen.
- **Ohne Test-Schalter** bleibt der Fraktionsstart gesperrt (Code 6).

**Nicht geprüft:**
- Ende zu Ende über `ListenHost`: Die Nachrichtencodes sind noch nicht vergeben, also gibt es keine Verdrahtung in `FoundationServer`.
- Die Spielseite (Codex).

**Klassifizierung:** `BUILD_ERFOLGREICH`, Koordinator automatisiert getestet. Im Spiel `NICHT_GETESTET`.
