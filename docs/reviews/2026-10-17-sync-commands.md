# Review-Unterlage – Geteilte Spieleraktionen, erster Schnitt (SYNC-002, Teil SYNC-004)

- **Aufgaben:** `SYNC-002`; Teil von `SYNC-004` (die Zeilen aus ADR 0025)
- **Quellbranch:** `feat/sync-commands` (lokal) · **Basis:** `feat/sync-lockstep` → `feat/mod-check-setup-ui` (beide lokal, nicht gemergt)
- **Commits:** `git log feat/sync-lockstep..feat/sync-commands`
- **Status:** `ready_for_human_review` – prüfbar, nicht freigegeben, nicht gepusht.

## Änderungen

Siehe ADR 0025 mit Nachtrag. Was ein Spieler tut, passiert bei allen beim selben Tick:
- Einziehen und Feuer frei.
- Rechtsklick-Befehle.
- Bezeichnungen, Bauen mit Material und Drehung.
- Zonen samt Pflanze und erlaubte Bereiche.
- Verbieten, Arbeitsprioritäten, Zeitplan, Forschung.

Noch nicht geteilt (lokal, mit Hinweis): Rechnungen, Lager-Filter und -Priorität, Richtlinien, Medizin, Handel, Gebäude-Knöpfe, Installieren, Malen, Pläne, Designatoren anderer Mods.

## Zwei-Spieler-Lauf (Prüfpunkte)

**Vorbereitung:**
- Beide Spieler müssen denselben RimWorld-Build haben. Im Lauf vom 2026-10-16 lief der Host mit rev591, der Gast mit rev600: bitte über Steam angleichen.
- Beide brauchen dieselbe Mod: `dist/RimWorldMultiplayerFoundation-sync-commands.zip` aus dem Worktree `E:\RimworldMP-sync`.
  - Den Ordner `Mods/RimWorldMultiplayerFoundation` ganz ersetzen.
  - Andere Kopien der Mod entfernen oder deaktivieren.
  - In der Lobby muss „Mods passen“ stehen.

1. **Landen und starten:** Einrichtung wie bisher; danach Leertaste.
2. **Einziehen:** Ein Spieler zieht eine Figur ein (R oder Knopf). Beim anderen ist sie auch eingezogen.
3. **Bewegen:** Rechtsklick auf eine Stelle. Die Figur läuft bei beiden dorthin.
4. **Bezeichnen:** Ein Spieler markiert Bäume zum Fällen und Felsen zum Abbauen. Der andere sieht die Markierungen, und die Figuren arbeiten sie bei beiden gleich ab.
5. **Bauen:** Eine Wand aus Holz ziehen und ein Bett mit Drehung setzen. Bei beiden stehen dieselben Blaupausen.
6. **Zonen:** Ein Lager anlegen, es dann ausgewählt erweitern; eine Anbauzone anlegen und die Pflanze wechseln. Bei beiden gleich.
7. **Arbeit und Zeitplan:** Im Arbeits-Tab eine Priorität ändern, im Zeitplan Stunden umfärben. Beim anderen gleich.
8. **Forschung:** Ein Projekt wählen. Beim anderen ist es dasselbe.
9. **Verbieten:** Mehrere Gegenstände markieren und verbieten. Bei beiden verboten.
10. **Prüfsumme:** „Prüfsummen gleich bis Tick …“ läuft während all dessen weiter. Erscheint „Abweichung bei Tick …“, bitte notieren, was kurz davor getan wurde. Die Zeile „zuletzt lokal: …“ nennt eine nicht geteilte Aktion.
11. **Logs:** Beide `Player.log` schicken.

**Erwartung, ehrlich:**
- Die geteilten Aktionen liefen im Selbsttest bei allen gleich; 5 × 6 000 Ticks mit einem Befehlsskript ergaben dieselbe Prüfsumme.
- Aktionen, die noch nicht geteilt werden, führen weiter zur Abweichung.
- Über einen ganzen Spieltag gab es im Selbsttest noch eine seltene Abweichung mit offener Ursache (siehe ADR 0024).

## Tatsächliche Prüfungen

`docs/verification/2026-10-17-shared-actions.md`.

## Risiken

- Designatoren, die beim Ausführen weitere Oberflächen-Zustände lesen.
- Rechtsklick-Befehle mit Feldern, die der Serializer nicht kennt: Sie bleiben lokal mit Hinweis.
- Rechte fehlen: Jeder darf jede Figur befehligen (SYNC-005).
- Ein Klick wirkt erst nach einem Netz-Umlauf.
- Tests unter .NET 8 statt .NET 10.
