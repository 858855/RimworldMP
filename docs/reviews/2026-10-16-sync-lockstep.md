# Review-Unterlage – Gleichlauf, erster Schnitt (SYNC-001, Minimum DET-001)

- **Aufgaben:** `SYNC-001`; Teile von `DET-001`, `DESYNC-001` (Prüfsumme und Vergleich), `SYNC-003` (Pause und Geschwindigkeit)
- **Quellbranch:** `feat/sync-lockstep` (lokal) · **Basis:** `feat/mod-check-setup-ui` @ `5c3f2b7` (lokal, nicht gemergt)
- **Commits:** `git log feat/mod-check-setup-ui..feat/sync-lockstep`
- **Status:** `ready_for_human_review` – prüfbar, nicht freigegeben, nicht gepusht.

## Änderungen

Siehe ADR 0024 mit Nachtrag:
- Der Server ordnet Befehle und gibt Ticks frei; der langsamste Spieler bestimmt das Tempo.
- Pause und Geschwindigkeit sind geteilte Befehle; das Spiel startet pausiert.
- Prüfsumme alle 60 Ticks.
- Gegenmaßnahmen gegen RimWorld-1.6-Nichtdeterminismus: Zufall von Fischschatten und Geräuschen isoliert, Tickrate der Figuren unabhängig von der Sicht, Pfade sofort.
- Wire-Version 4: Ältere Builds verbinden nicht.

## Zwei-Spieler-Lauf (Prüfpunkte)

**Vorbereitung:** Beide Spieler brauchen denselben Build. Er liegt als `dist/RimWorldMultiplayerFoundation-sync-lockstep.zip` im Worktree `E:\RimworldMP-sync`.
- Den Ordner `Mods/RimWorldMultiplayerFoundation` ganz ersetzen, nicht darüber kopieren.
- Andere Kopien der Mod im Mods-Ordner entfernen oder deaktivieren.
- Die Mod-Prüfung in der Lobby muss „Mods passen“ zeigen.

1. **Landung wie bisher:** Lobby, Einrichtung, Figuren, Start. Das Feld oben rechts zeigt erst „Gleichlauf: warte auf alle Spieler …“, dann „Tick … · pausiert – Leertaste startet für alle“.
2. **Starten:** Ein Spieler (auch der Gast) drückt Leertaste oder ▶. Bei beiden läuft die Zeit; das Feld zeigt „Normal (1×)“.
3. **Geschwindigkeit:** Der Host drückt 2 oder 3; beide laufen schneller.
   - Drückt der Gast eine andere Stufe, kommt „Nur der Host ändert die Geschwindigkeit …“.
   - Pause und Fortsetzen gehen bei beiden.
4. **Prüfsumme:** „Prüfsummen gleich bis Tick …“ steigt etwa einmal je Sekunde (bei 1×). Beide sollen ohne Eingaben mindestens 10 Minuten zuschauen; bitte die Zahl notieren, die zuletzt grün war.
5. **Testmarkierung:** Ein Spieler klickt „Testmarkierung“. Beide sehen „Markierung #n von … bei Tick t“ mit **gleichem** t. Bitte vergleichen.
6. **Langsamster bestimmt:** Ein Spieler hält sein Fenster am Titel gedrückt (Windows hält dann das Spiel an).
   - Beim anderen erscheint nach etwa 2 s „Warte auf … (N Ticks zurück)“, und die Zeit steht.
   - Nach dem Loslassen holt das Spiel auf, und beide laufen weiter.
7. **Hintergrund:** Ein Spieler wechselt mit Alt-Tab weg. Die Zeit läuft bei beiden weiter.
8. **Abweichung absichtlich (am Schluss):** Ein Spieler gibt einen Befehl, z. B. Einziehen oder Abbauen markieren. Nach höchstens 60 Ticks zeigt das Feld bei beiden „Abweichung bei Tick …“ (Befehle werden erst mit SYNC-002 geteilt).
9. **Logs:** Beide `Player.log` schicken; sie enthalten „lockstep started“, Markierungen und gegebenenfalls „lockstep desync at tick …“ mit einer Zusammenfassung.

**Erwartung, ehrlich:** Im Selbsttest (ein Prozess) blieben 6 000 Ticks gleich. Über einen ganzen Spieltag gab es noch eine seltene Abweichung mit offener Ursache. Zwischen zwei PCs kann früher eine Abweichung auftreten. Genau das soll Prüfpunkt 4 zeigen.

## Tatsächliche Prüfungen

`docs/verification/2026-10-16-lockstep-core.md`.

## Risiken

- Weitere sicht- oder hörabhängige Zufallsziehungen in Lagen, die der Selbsttest nicht hatte (Kampf, Feuer, Wetter, Effekter).
- Feste Tickrate und synchrone Pfade kosten Rechenzeit in großen Kolonien.
- `FindPathNow` gibt Fläche und genaues Ziel einer Pfadanfrage nicht weiter.
- Vanillas Autopause pausiert für alle.
- Fenster mit `forcePause` halten die Sitzung nicht an.
- Tests unter .NET 8 statt .NET 10.
