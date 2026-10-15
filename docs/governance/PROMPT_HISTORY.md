# Herkunft der KI-Projektregeln

Die drei ursprünglichen Benutzerprompts sind im Unterordner `history/` **wortgetreu** aus der lokalen Sitzungsaufzeichnung vom 14./15. Oktober 2026 abgelegt. Sie sind ein historischer Nachweis; ihre Länge macht sie ungeeignet als Pflichtkontext jeder KI-Sitzung. Die konkreten Projektregeln stehen in [`AGENTS.md`](../../AGENTS.md), die aktuellen technischen Fakten in [`PROJECT_STATE.yaml`](../ai/PROJECT_STATE.yaml), die Aufgaben in [`TASKS.yaml`](../ai/TASKS.yaml).

| Quelle | Historischer Text | Zeichen | SHA-256 (UTF-8) |
|---|---|---:|---|
| Ursprüngliche Überprompt | [UEBERPROMPT_V1.md](history/UEBERPROMPT_V1.md) | 8 806 | `a445b1f786c303f5755301588116c871d8acadd6aadd7c97ec2aa276f8e841d9` |
| Erste Masterprompt | [MASTERPROMPT_V1.md](history/MASTERPROMPT_V1.md) | 37 116 | `116127f6224e973f8530c8e0a60bfae5c6ba41218a43ee4bdafb364f54ba56be` |
| Masterprompt 2.0 mit Überprompt 2.0 | [MASTERPROMPT_V2.md](history/MASTERPROMPT_V2.md) | 46 916 | `fc83366fc22fa1648b65e74e78b6f0c654a60b8d4058ffd6b92e99c53da8964a` |

**Geltung:** Die Überprompt 2.0 und Masterprompt 2.0 ersetzen ältere Projektregeln bei Widerspruch. Ihre fortgeschriebene, kompakte Umsetzung ist `AGENTS.md` zusammen mit [`WORKFLOW.md`](WORKFLOW.md), [`REVIEW_POLICY.md`](REVIEW_POLICY.md) und [`DOCUMENTATION_POLICY.md`](DOCUMENTATION_POLICY.md). Neue direkte Benutzeranweisungen und übergeordnete Systemvorgaben haben Vorrang. Historische Anforderungen sind keine Behauptung über den aktuellen Implementierungsstand; dafür die Status- und Prüfnachweise lesen.

Die Quelldateien enthalten ausschließlich die drei genannten Benutzertexte; Sitzungs-Metadaten, Umgebungsinformationen und Assistentenantworten wurden nicht übernommen. Eine vollständige Neuauswertung ist nur für Regel-Audits oder unklare Migrationsfragen nötig.
