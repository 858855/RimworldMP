# ADR 0015 – Startort per Fähnchen, Charakterwahl erst wenn alle fertig

- **Status:** angenommen durch ausdrückliche Anweisung des Benutzers am 2026-10-15. Ändert `docs/plans/rimworldmp-full-plan.md` §6.1–§6.3, §8, §10 (`SETUP-003`, `SETUP-004`) und beantwortet offene Frage §12.3.
- **Benutzervorgabe:** „Beide müssen fertig sein mit der Charakterwahl, bevor es weitergeht. Auf der Weltkarte beim Wählen der Map soll jeder ein Fähnchen setzen, aber nur der Host auswählen können – quasi als ‚hey, hier würde ich gerne, schau dir das mal an‘. Der Host hat die Entscheidungsmöglichkeit.“

## Startort (Setup-Schritt 4)

- Jeder Spieler, auch der Host, kann auf der Weltkarte **ein Fähnchen** auf ein Feld setzen. Ein neues Fähnchen ersetzt das alte desselben Spielers; der Spieler kann es auch wieder entfernen.
- Ein Fähnchen ist ein **Vorschlag ohne Stimmgewicht**. Alle sehen alle Fähnchen live, mit Spielername und Farbe. Ein Klick auf ein fremdes Fähnchen springt dorthin, damit man sich den Ort ansehen kann.
- **Nur der Host wählt den Startort.** Er kann jedes gültige Feld nehmen, mit oder ohne Fähnchen. Gültigkeit prüft die vanilla-Siedlungsregel auf dem Host [U].
- Es gibt **keinen Timer, keine Stimmenzählung und keine Gleichstand-Regel**. Die Phase endet nur durch die Wahl des Hosts. Alle sehen danach „Host hat X gewählt“.
- Fähnchen sind flüchtig. Sie werden weder gespeichert noch nach dem Setup weitergegeben.

## Charakterwahl (Setup-Schritt 6)

- Das Spiel geht erst weiter, wenn **jeder verbundene Spieler**, Host eingeschlossen, seine Wahl mit **Fertig** bestätigt hat.
- **Kein „Start erzwingen“** für den Host. Der Start-Knopf des Hosts ist gesperrt und zeigt, auf wen gewartet wird, z. B. `Warte auf: Player2`.
- Fertig setzt voraus, dass der Spieler mindestens einen Pawn besitzt. Ändert ein Spieler danach seine Pawns (neu würfeln, tauschen, umbenennen), wird sein Fertig zurückgesetzt. Ändert der Host fremde Zuordnungen, werden die betroffenen Spieler ebenfalls zurückgesetzt.
- **[A] Verbindungsabbruch:** Verlässt ein Gast das Setup, zählt er nicht mehr zur Wartebedingung. Seine Pawns werden zu gemeinsamen Pawns. Verlässt der Host, bricht das Setup ab (Plan §6.2, unverändert). Diese Annahme ist nicht vom Benutzer bestätigt.

## Folgen für Protokoll und Zustandsmaschine

- Plan §8: `SiteVote`/`SiteVoteState` werden zu `SiteFlag` (Client→Server: Feld oder leer) und `SiteFlagState` (Server→alle: alle Fähnchen). `SiteDecided` sendet nur noch der Host. Der Server lehnt `SiteDecided` von Nicht-Hosts ab.
- Plan §6.2: `SiteVote --> Ideology: vote closed / host overrule` wird zu `SiteSelection --> Ideology: host chooses site`. `Characters --> Generating` verlangt „alle verbundenen Spieler fertig“ statt „jeder besitzt einen Pawn“.
- Der Server prüft beide Regeln selbst, nicht nur die UI: Er lehnt die Wahl eines Nicht-Hosts ab und lehnt den Start ab, solange jemand nicht fertig ist.

## Tests

- Fähnchen mehrerer Spieler sind bei allen sichtbar; ersetzen und entfernen funktioniert.
- `SiteDecided` von einem Gast wird abgelehnt.
- Der Start wird abgelehnt, solange ein Spieler nicht fertig ist, und ist erst danach möglich.
- Eine Pawn-Änderung setzt Fertig zurück; ein abgemeldeter Gast blockiert nicht mehr.
- Echte Spieltests mit zwei Clients sind derzeit nicht möglich (Benutzer, 2026-10-15) und bleiben `NICHT_GETESTET`.

## Rollback und Alternativen

- **Rollback:** ADR zurückziehen; der Plan-Text vor dieser Änderung steht in der Git-Historie.
- **Alternativen:** Abstimmung mit Timer und Host-Überstimmung (ursprünglicher Plan), vom Benutzer verworfen. Host darf Start erzwingen (ursprünglicher Plan), vom Benutzer verworfen.
