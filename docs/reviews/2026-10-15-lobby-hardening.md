# Review-Unterlage – Lobby 0.2-Vorschau

- **Aufgaben:** `VER-001` (Teil Metadaten), `ID-001`, `XFER-001`, `MOD-001`, `START-001`
- **Quellbranch:** `feat/lobby-hardening` (nur lokal) · **Zielbranch:** `main`
- **Abhängiger, ungemergter Branch:** `docs/full-multiplayer-plan` (PLAN-001) – muss zuerst gemergt werden
- **Commits:** `git log docs/full-multiplayer-plan..feat/lobby-hardening`
- **Status:** `ready_for_human_review` – prüfbar, **nicht freigegeben**, nicht gepusht (Benutzerwunsch: vorerst nur lokal).

## Änderungen

- Protokoll Wire-Version 3: Spielerschlüssel im `Hello`; Nachrichten 20–27 (Mod-Manifest, Chunk-Übertragung, Start/Abbruch); Lobby-Snapshot mit Phase und Mod-Status je Spieler; Lobbyliste mit Host-Manifest und Phase; Fehlercodes 8 und 9.
- Server: Mod-Status-Vergleich mit dem Host, Ready-Sperre, Weiterleitung der Mod-Listen-Anfrage, Chunk-Relay mit Abbruch bei Trennung/Verlassen, Start-/Abbruchregeln, kein Host-Wechsel während der Einrichtung.
- Mod: Manifest im Hintergrund, Status in der Lobby, Unterschiede-Fenster, Spiel-starten-Knopf mit Begründung, Einrichtungsansicht nur mit Abbrechen, Steam-Lobby-Kennung folgt der Wire-Version.
- Doku: `docs/protocol.md`, `docs/client.md`, `references/verified-apis.md`, CODEMAP, CHANGELOG, Projektstand, Übergabe, Verifikation.

## Tatsächliche Prüfungen

Siehe `docs/verification/2026-10-15-lobby-hardening.md`: sechs Testprojekte im .NET-8-Harness bestanden, Mod-Build ohne Warnungen, Rauchtest im Spiel (Mod lädt, Manifest geloggt).

## Risiken

- Wire-Version 3 bricht die Kompatibilität mit allen älteren ZIPs; alle Spieler und ein Dedicated Server brauchen denselben Stand.
- Tests liefen unter .NET 8 statt .NET 10; `ProcessIntegration.Tests` und der Serverbuild liefen nicht.
- Der Spielerschlüssel kann bei IP-Lobbys ohne Authentifizierung gefälscht werden (SEC-001).
- Die Manifestprüfung ist streng: auch reine UI-Mods müssen übereinstimmen (Plan §12 Q7).

## Manuelle Prüfpunkte

1. Zwei RimWorld-Clients mit gleicher Modliste: „Mods passen“, beide bereit, **Spiel starten** aktiv, Bestätigung, Einrichtungsansicht, **Einrichtung abbrechen**.
2. Ein Client mit zusätzlichem oder umsortiertem Mod: rot markiert, **Bereit** gesperrt, **Unterschiede anzeigen** zeigt die richtige Abweichung.
3. Lobbyliste: „passt“/„abweichend“ und Tooltip; gesperrte Lobby mit „[Einrichtung]“ und deaktiviertem Beitreten.
4. Dasselbe über eine Steam-Lobby.
