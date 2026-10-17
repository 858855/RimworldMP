# ADR 0026 – Steuerungsrechte: nur eigene oder gemeinsame Figuren (SYNC-005, erster Schnitt; OWN-001 Anzeige)

- **Status:** Vorschlag auf `feat/permissions` (Sven, 2026-10-17), von `main` @ `060d2be`; Ziel RimWorld 1.6.4871, Unity/Mono. Nummer aus Svens ADR-Bereich 0024–0029 (`docs/ai/ASSIGNMENTS.md`); 0024/0025 liegen auf `feat/sync-lockstep`/`feat/sync-commands`.
- **Ziel:** Ein Spieler steuert nur die Figuren, die er in der Einrichtung übernommen hat. Befehle für fremde Figuren bleiben überall ohne Wirkung, der Auslöser sieht sofort den Grund. Besitz ist im Spiel sichtbar.
- **Bestand:** `MultiplayerSession` speichert Figur → `PlayerKey` (SETUP-005, ADR 0023) und der Spielstand-Fingerabdruck enthält die Tabelle, also haben alle Spiele dieselbe Zuordnung. Jeder `ScheduledCommand` trägt den vom Koordinator gestempelten `IssuerKey` (ADR 0030). Bisher las ihn niemand: Jeder konnte jede Figur zeichnen oder schicken.
- **Regel (Plan §7.7, Standardmatrix):**

  | Ziel | Wer darf |
  |---|---|
  | eigene Figur | nur der Besitzer |
  | gemeinsame Figur (kein Besitzer, z. B. später rekrutiert) | alle |
  | Figur eines Mitspielers | niemand sonst (Leihen und „Besitzer offline“ folgen) |
  | Kolonie-Aktionen (Verbieten, Designationen, Zonen, Bauen, Forschung …) | alle |
  | Fraktionsmodus | nur Figuren der eigenen Fraktion (`MultiplayerSession.FactionIdFor`) |
  | Befehle des Koordinators (ohne Schlüssel, `Guid.Empty`) | immer |

- **Umsetzung:**
  - `PawnControl.Denial(pawn, issuer, session)` liefert `null` oder den Grund. `MultiplayerSession.OwnerKeyOf`, `PlayerNameOf`, `FactionOwnerNameOf` neu.
  - **Deterministisch:** `GameCommandCodec.Execute(command, localSession)` prüft bei `Zeichnen` und `Spielauftrag` nach dem Auflösen der Figur. Abgelehnt heißt No-op in jedem Spiel plus Log-Zeile; nur das Spiel des Auslösers (`IssuerSession == localSession`) zeigt „Keine Berechtigung: …“ (`Messages.Message`, nicht historisch, kein Spielzustand). `Forbid` ist Kolonie-Aktion und bleibt frei.
  - **Lokal:** `FoundationMod.CaptureDraft`/`CaptureOrderedJob` prüfen dieselbe Regel mit dem eigenen Schlüssel aus dem Lobbyzustand, bevor ein Befehl gesendet wird; die Vanilla-Aktion läuft dann nicht und der Hinweis erscheint sofort. Bindend bleibt die Prüfung bei der Ausführung.
  - **Anzeige (OWN-001, erster Schritt):** Postfix auf `Pawn.GetInspectString` mit „Multiplayer: Figur von <Name>“ bzw. „gemeinsame Figur“; im Fraktionsmodus der Spieler der Fraktion.
- **Determinismus:** Alle Eingaben der Prüfung stehen im Spielstand (`ownedPawns`/`pawnOwners`, Fraktionstabelle) oder im Befehl (`IssuerKey`). Keine Uhrzeit, kein Verbindungszustand. Die Anzeige ändert keinen Zustand.
- **Nicht enthalten (Folgearbeit SYNC-005/OWN-001):** Übernahme, Freigabe und Leihen als Befehle (`GameCommandCodec`-Opcodes, die `MultiplayerSession` in der Ausführung ändern); Regel „Besitzer offline“ (braucht ein vom Koordinator geplantes Anwesenheitssignal, damit alle Spiele dasselbe sehen; `PlayerView` hat kein Verbindungsfeld); Kolonie-Aktionen jenseits der drei vorhandenen Befehle (SYNC-004); Farben in der Kolonistenleiste und Namensschilder; Host-Entscheidungen bei Coop-Host-Ereignissen (ADR 0034); Gizmos fremder Figuren ausblenden.
- **Prüfung:** Mod-Build gegen lokale 1.6.4871-Assemblies (Windows rev590) 0 Warnungen. Selbsttest `-mpWorldSelfTest` erweitert: nach der Landung ein Zeichnen-Befehl mit fremdem Schlüssel (`controlDenied`, Zustand unverändert) und mit dem Besitzerschlüssel (`controlAllowed`), danach zurückgesetzt. Lauf 2026-10-17 auf Svens Rechner (rev591): `game self-test: PASS … controlDenied=ok controlAllowed=ok`, Ablehnung im Log, 0 Exceptions (`docs/verification/2026-10-17-pawn-control.md`). Kein Zwei-Client-Test; Prüfung mit zwei Spielern: Gast zeichnet die Figur des Hosts → Hinweis, kein Effekt bei beiden; eigene Figur → Effekt bei beiden, Hashes gleich.
- **Rollback:** `Refused`-Aufrufe und `RefusedLocally` entfernen; Anzeige-Patch löschen. Spielstände bleiben kompatibel (keine neuen Felder).
