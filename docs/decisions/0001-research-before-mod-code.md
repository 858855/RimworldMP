# ADR 0001: Wissensbasis vor produktivem Mod-Code absichern

- **Datum:** 2026-10-14
- **Status:** Vorgeschlagen; menschliche Review-Freigabe steht aus.
- **Kontext:** Das Ausgangsrepository enthielt nur eine minimale README. Der Benutzer stellte `AGENTS.md`, eine Forschungs-README und `RimWorld-Codex-Knowledge.zip` bereit. Die Beispiele im Archiv sind ausdrücklich nicht kompiliert oder im Spiel geprüft.
- **Entscheidung:** Archivinhalt unverändert zugänglich machen und ein kompaktes Projektgedächtnis ergänzen. Für spätere MP-Arbeit dient der lokal installierte RimWorld-1.6-Build als nachgewiesene Umgebung. Ein Mod-Feature beginnt erst mit konkretem Umfang, Designnotiz und geprüften APIs.
- **Gründe:** Das README verweist auf Archivdateien, die sonst nicht einzeln verfügbar wären. Projektstatus und Prüfnachweise müssen Forschungsbehauptungen von Laufzeitbelegen trennen. Für 1.7-MP ist die Prepatcher-Abhängigkeit ungeklärt.
- **Alternativen:** Nur das ZIP behalten (Querverweise blieben unpraktisch); sofort Mod-Code erzeugen (Anforderungen und API-Belege fehlen); 1.7-MP als Ziel setzen (Unterstützung nicht belegt).
- **Folgen:** Keine produktive Funktion entsteht in dieser Phase. Das ursprüngliche ZIP bleibt als Quellartefakt erhalten; `MANIFEST.sha256` prüft den extrahierten Inhalt. Vor jeder Implementierung werden bewegliche Quellen gepinnt und lokale Signaturen geprüft.
- **Referenzen:** `AGENTS.md`, `README.md`, `VALIDATION.md`, `references/open-questions.md`, `codex/architecture-guidelines.md`.
