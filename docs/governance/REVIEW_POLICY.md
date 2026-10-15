# Menschliche Integration

Jeder Merge benötigt eine ausdrückliche menschliche Prüfung und Freigabe, unabhängig vom Zielbranch oder Merge-Verfahren. Erfolgreiche Tests und allgemeine Entwicklungsaufträge ersetzen diese Freigabe nicht. Pushes benötigen ebenfalls eine ausdrückliche Freigabe; lokale Commits nicht.

Ein Review-Bericht in `docs/reviews/` nennt Aufgabe, Quell-/Zielbranch, abhängige Branches, Commits, Änderungen, tatsächliche Build-/Testergebnisse, Risiken und manuelle Prüfpunkte. `ready_for_human_review` bedeutet prüfbar, nicht freigegeben. `approved` und `merged` dürfen nur nach belegten Ereignissen gesetzt werden.

Kein Force-Push, destruktiver Reset, Verwerfen fremder Änderungen oder verdeckter Merge. Für unvollständige, aber gesicherte Zwischenstände WIP-Commit auf dem Arbeitsbranch mit klarer Restaufgabe.
