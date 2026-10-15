# Review-Unterlage – Mod-Prüfung, Start-Knopf, Seitenspiegel

- **Aufgaben:** `MOD-001`, `START-001` (Spielseite), `SETUP-001`, `VER-001` (Metadatenteil)
- **Quellbranch:** `feat/mod-check-setup-ui` · **Zielbranch:** `main`
- **Abhängige, ungemergte Branches:** `feat/setup-rules` → `feat/player-key` → `feat/chunk-transfer` → `docs/full-multiplayer-plan`
- **Ersetzt:** `feat/lobby-hardening` (gleiche Funktionen auf eigenem Protokoll; nach Wahl des Benutzers auf diese Basis übertragen, eigene Doppelungen bei Spielerkennung, Chunk-Kanal und Phasen verworfen)
- **Commits:** `git log origin/feat/setup-rules..feat/mod-check-setup-ui`
- **Status:** `ready_for_human_review` – prüfbar, nicht freigegeben.

## Änderungen

Siehe ADR 0018 und 0019: Manifest und Prüfsumme nur über Mods, Status je Spieler, Bereit-Sperre und Start-Prüfung, Unterschiedsbericht über den Chunk-Kanal, Start-Knopf mit Begründung, Einrichtungsansicht, Seitenspiegel des Hosts mit Phasensteuerung, Feld über den Seiten, Sperre des letzten Starts; Codes 28–30; Steam-Lobby-Kennung folgt der Wire-Version.

## Tatsächliche Prüfungen

`docs/verification/2026-10-15-mod-check-setup-ui.md`.

## Risiken

- Der Seitenspiegel liest private Felder der vanilla-Seiten; andere Mods, die diese Seiten ersetzen (z. B. Prepare Carefully), können ihn leeren. Lesen ist fehlertolerant.
- Die Serverphase folgt der am weitesten erreichten Seite; geht der Host zurück und wählt einen anderen Startort, bleibt das zuerst gemeldete Feld.
- Tests unter .NET 8 statt .NET 10.

## Nachtrag: SETUP-002

Weltübertragung und Fingerabdruck (ADR 0020, Codes 31/32, `WorldStatus` im Lobbyzustand). Verifikation: `docs/verification/2026-10-15-world-transfer.md`.

## Manuelle Prüfpunkte

1. Zwei Spieler, gleiche Mods: **Spiel starten** → beim Host öffnen sich die Seiten mit dem Multiplayer-Feld; der Gast sieht Szenario, Erzähler, Welt, Startort, Ideoligion, Figuren live.
2. Host klickt auf der Figurenseite **Start**: Meldung, kein Einzelspieler-Spiel.
3. **Abbrechen** im Feld und „Zurück“ auf der ersten Seite: beide landen wieder in der Lobby.
4. Abweichende Mods: rot, Bereit gesperrt, **Unterschiede anzeigen**.
5. Nach „Welt erstellen“: Feld des Hosts zeigt „Welt wird gesendet …“, beim Gast „Welt wird empfangen …“, dann „Welt geladen, passt“ für alle; erst dann öffnet sich beim Server die Startortwahl (Phase „Startort“ beim Gast).
6. Allein (Debug): nach „Welt erstellen“ geht es ohne Übertragung weiter; `Player.log` zeigt „Welt gesendet (… KiB, Prüfsumme …)“.
7. Startortwahl: beim Gast öffnet sich die Weltkarte; Feld wählen + „Weiter“ setzt ein Fähnchen (kein Weitergehen), beide sehen beide Fähnchen mit Namen; „Hin“ springt; „Zurück“ schließt nur die Karte, „Weltkarte öffnen“ öffnet sie wieder. Host wählt mit „Weiter“, der Gast sieht danach den gewählten Startort.
8. Charakterwahl (ADR 0022): beim Gast öffnet sich das Figurenfenster; die Startgruppe hat eine Figur je Spieler. Gast übernimmt eine Figur, der Host übernimmt die gezeigte im Feld oben rechts; eine schon vergebene Figur lässt sich nicht übernehmen. Neu würfeln, Umbenennen und Tauschen beim Gast erscheinen beim Host auf der vanilla-Seite; jede Änderung nimmt Fertig zurück. **Fertig** bei beiden; der letzte Start meldet weiterhin die Sperre (SETUP-005) und nennt vorher, auf wen gewartet wird.
