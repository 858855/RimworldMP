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

## Nachtrag: Transportstrecke (Codes 96/97, Server, Ende zu Ende)

- **Basis:** `claude/faction-storyteller` @ `2a2c818`. Codebereich 96–111 vom Benutzer am 2026-10-16 vergeben (Rückfrage in der Sitzung); 96 `FactionOptions`, 97 `StorytellerChoice`.

| Befehl/Prüfung | Ergebnis |
|---|---|
| `Protocol.Tests` | Exit 0; 18 Gruppen |
| `Lobby.Tests` | Exit 0; 10 Szenarien |
| `Hosting.Tests` | Exit 0; 28 Gruppen; drei Läufe nacheinander, alle bestanden |
| `Network`, `Session`, `Client`, `Integration`, `ProcessIntegration` | Exit 0; unverändert bestanden |
| `DOTNET_BIN=…/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen, 0 Fehler |
| `DOTNET_BIN=…/dotnet bash scripts/build-server.sh` | Exit 0 |
| `python3 scripts/validate_project_state.py` | Exit 0 |
| Mod-Prüfsumme umfasst DLLs (Code gelesen) | `ModContentHasher`: About.xml, `Assemblies/*.dll`, `Defs`/`Patches`-XML je Lade-Ordner; `ModManifestBuilder` nimmt alle laufenden Mods |

Neu in `Protocol.Tests`:
- **Coop und Fraktionslobby vor dem Start:** Der Zustand endet mit den drei Modus-Bytes. Drei angehängte Bytes werden abgelehnt.
- **Fraktionsdaten am falschen Ort** werden beim Kodieren abgelehnt: in Coop (Platz oder Optionen) und in einer Fraktionslobby vor dem Start.
- **Ab dem Start der Fraktionseinrichtung:**
  - Der Block hängt hinter einem sonst gleichen Präfix. Ein Leser, der nach dem Modus endet (Stand vor ADR 0037), findet zusätzliche Bytes.
  - Ein nach dem Modus abgeschnittener Zustand wird abgelehnt.
- **Frames:** 96 und 97 laufen als Frame hin und zurück; Typ 98 wird als unbekannt abgelehnt.

Neu in `Hosting.Tests`, über einen echten `FoundationServer` per TCP mit `EnableFactionsSetup` (internal) und drei Spielern:
- **In der Lobby:** keine Plätze; Erzählerwahl abgelehnt (6).
- **Nach dem Start:** Plätze 0/1/2, auch beim Gast angekommen.
- **Phase `Storyteller`, Ablehnungen:**
  - Gast sendet `FactionOptions` (3).
  - Wahl vor der Liste (6), Fertig ohne Wahl (6), Weiter ohne Optionen (6).
- **Optionen und Wahl:**
  - Optionen des Hosts kommen beim Gast an.
  - „Tutor“ ist nicht angeboten (6).
  - Host und Gast wählen; `StorytellerBlockReason` meldet „Erzähler fehlt: Third“; Weiter abgelehnt (6).
  - Neue Schwierigkeit löscht Fertig, die Wahl bleibt.
- **Ungültige Wahl** von „Third“ (defName mit Leerzeichen): Code 1, Verbindung getrennt. Der Host sieht zwei Spieler; Plätze 0/1 bleiben.
- **Weiter bis `Playing`:** Danach werden Optionen und Wahl außerhalb der Phase abgelehnt (6). Plätze und Wahl bleiben im laufenden Spiel.
- **Trennung und Rückkehr:**
  - Ein neuer Spieler wird abgewiesen (6).
  - Der Gast verbindet sich mit demselben Schlüssel neu und erhält Platz 1, „Randy“ und „Brutal“.
- **Coop auf demselben Server:** keine Fraktionsdaten. `FactionOptions`, Wahl und Fertig in `Storyteller` werden abgelehnt (6).
- **`ListenHost` (Weg der Mod):** Der Fraktionsstart wird abgelehnt (6). `new HostConfig().EnableFactionsSetup` ist falsch.

**Klassifizierung:** `BUILD_ERFOLGREICH`, Koordinator und Server automatisiert Ende zu Ende getestet. Im Spiel `NICHT_GETESTET`; die Spielseite fehlt (Codex).
