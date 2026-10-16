# Startort und Ideologie je Fraktion, Fortsetzen (ADR 0038) – Prüfung 2026-10-16

- **Branch/Basis:** `claude/faction-sites` von `main` @ `9fc1519`. Bahn Claude, `FACTION-006`.
- **Umgebung:** Linux, .NET SDK 10.0.402 / Runtime 10.0.13, lokale RimWorld-Assemblies `1.6.4871` (rev598) für den Mod-Build. Keine RimWorld-API in diesem Branch verwendet; Feld- und Ideologieprüfung sind Spielseite.

| Befehl/Prüfung | Ergebnis |
|---|---|
| `Protocol.Tests` | Exit 0; 19 Gruppen, neu siehe unten |
| `Lobby.Tests` | Exit 0; 12 Szenarien |
| `Hosting.Tests` | Exit 0; 30 Gruppen; drei Läufe nacheinander, alle bestanden |
| `Network`, `Session`, `Client`, `Integration`, `ProcessIntegration` | Exit 0; unverändert bestanden |
| `DOTNET_BIN=…/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen, 0 Fehler |
| `DOTNET_BIN=…/dotnet bash scripts/build-server.sh` | Exit 0 |
| `python3 scripts/validate_project_state.py` | Exit 0 |

**Angepasste bestehende Tests:**
- Der ADR-0037-Ablauf im Lobby-Kern und im Hosting-Test ging im Fraktionsmodus noch über die Coop-Wahl des Hosts (`SiteDecided`). Er wählt jetzt Startort und Ideologie je Spieler (`ChooseOwnSitesAndIdeologies`, `FinishSetupAsync(…, factions: true)`).
- Der Frame-Test prüft 102 statt 98 als noch ungültigen Typ.

**Neu in `Protocol.Tests`:**
- **Rundläufe:** Startort (auch leer), Ideologie (Grenze 49 152 Byte), Relais an den Host, Zurückweisen, Tabelle zum Fortsetzen.
- **Abgelehnt** werden:
  - Daten über der Grenze oder leer
  - Bezeichnung leer, mit Zeilenumbruch oder über 96 Byte
  - unbekannte Art auf der Leitung
  - Platz 64, Grund leer oder über 128 Byte
  - Tabelle leer, über 64 Einträge, mit Schlüssel oder Platz doppelt, mit ungültigem defName
- **Relais:** Seine Form wird als Wahl abgelehnt (ein Gast kann sich nicht als Relais ausgeben).
- **`IdeologySummary`:** Der Hash entspricht SHA-256 der Daten.
- **Lobbyzustand:**
  - Startort, Ideologie und Hinweis kommen unverändert zurück; doppelte Startorte werden abgelehnt.
  - `FactionsRestored` kommt in der Phase `Lobby` mit Plätzen zurück. Abgelehnt werden: in Coop, nach dem Start, und Fraktionsdaten in Coop.

**Neu in `Lobby.Tests`:**
- **Eigene Fraktion:** Eine Wahl ändert nur den eigenen Eintrag.
- **Fähnchen und `SiteDecided`** im Fraktionsmodus abgelehnt; Ideologie in `SiteSelection` und Fertig ohne Startort abgelehnt.
- **Startorte:**
  - Ein Feld, das ein anderer Platz hat, wird abgelehnt („already starts“). Dieselbe `tileId` auf anderer Ebene ist erlaubt.
  - Dieselbe Wahl noch einmal behält Fertig und die Revision; eine neue Wahl löscht Fertig. Zurückziehen und Neuwahl gibt das Feld frei.
- **Zurückweisen:**
  - Nur der Host (3), nur passende Phase und vorhandener Platz (6).
  - Es löscht nur Wahl und Fertig des Betroffenen. Der Hinweis erscheint und verschwindet mit der nächsten eigenen Wahl.
  - `FactionSetupBlockReason` meldet „Startort fehlt: …“, „Warte auf: …“, „Ideologie fehlt: …“.
- **Ideologie:**
  - Die Weiterleitung nennt Host, Platz und Schlüssel des Wählenden. Unverändert wird nicht erneut weitergeleitet, und Fertig bleibt.
  - Zwei Fraktionen dürfen dieselbe Ideologie wählen. Neue Daten werden weitergeleitet und löschen Fertig.
- **Plätze:**
  - Austritt vor `Generating` löscht die Fraktion; die Plätze 0/1 bleiben.
  - Weiter bis `Playing`; Reconnect findet Platz, Startort und Ideologie; alles übersteht das Payload.
- **Coop:** Fraktionsnachrichten abgelehnt; Fähnchen und Host-Wahl gehen weiter.
- **Fortsetzen** (mit und ohne Test-Schalter):
  - Nur der Host spielt die Tabelle ein, und der Host muss darin stehen.
  - Der Grund „Kein Platz im Spielstand: Früh“ erscheint für jemanden, der vorher beigetreten ist.
  - Ein Fremder kann nicht beitreten; ein Spieler der Tabelle bekommt seinen Platz (mit Lücken) und behält ihn nach Verlassen.
  - Start blockiert, solange jemand ohne Platz da ist; `Scenario` abgelehnt; mit Schalter Start nach `Generating`, ohne Schalter abgelehnt.
  - Nach dem Start kein erneutes Einspielen; ein abwesender Spieler tritt später in `Playing` mit seinem Platz bei.

**Neu in `Hosting.Tests`** (echter `FoundationServer` per TCP mit `EnableFactionsSetup`):
- **Ablehnungen:** Fähnchen, `SiteDecided`, Ideologie zur falschen Zeit und Fertig ohne Startort (je 6).
- **Startorte:**
  - Ein Startort landet nur bei der eigenen Fraktion; ein besetztes Feld wird abgelehnt (6).
  - Zurückweisen durch einen Gast: 3. Durch den Host: Grund kommt beim Betroffenen an; neue Wahl löscht ihn.
- **Ideologie:**
  - Bezeichnung und Hash für alle.
  - Die Daten (2000 Byte) kommen beim Host mit Platz 1 und dem Schlüssel des Gastes an, nie bei Gästen. Unverändert wird nicht erneut weitergeleitet.
- **Gefälschtes Relais** von „Third“: Code 1, Verbindung getrennt, Fraktion weg, Plätze 0/1 stabil.
- **Zurückweisen der Ideologie** und erneutes Senden erreicht den Host wieder.
- **Weiter bis `Playing`:** Reconnect findet Platz, Startort, Ideologie und Erzähler.
- **Fortsetzen:**
  - Tabelle nur vom Host (Gast: 3); Fremder abgewiesen (6); Gast sieht Platz 2 und „Phoebe“.
  - `Scenario` abgelehnt; Start nach `Generating`; Übertragung bis `Playing`.
  - Ein Abwesender tritt mit Platz 1 und „Cassandra“ bei.
- **Coop auf demselben Server:** 98–101 abgelehnt (6).
- **Über `ListenHost`:** Fortsetzen einer Fraktionslobby abgelehnt (6).

**Klassifizierung:** `BUILD_ERFOLGREICH`, Koordinator und Server automatisiert Ende zu Ende getestet. Im Spiel `NICHT_GETESTET`. Die Spielseite fehlt (Codex, `FACTION-005`).
