# Prüfstrategie und Ist-Stand

Nach Änderungstyp testen. Ausgeführte Nachweise liegen unter `docs/verification/`.

- Archivmanifest: 49 Einträge geprüft; drei XML-Beispiele syntaktisch geprüft.
- Governance-Validator bestanden.
- Protokoll: fünf Gruppen bestanden. Netzwerk: drei reale TCP-Loopback-Gruppen bestanden. Lobby-Core-Regeltest bestanden.
- Dedicated Server: Release-Build und 13 Integrationstestgruppen mit zwei echten TCP-Clients bestanden; siehe `2026-10-15-dedicated-server.md` und `2026-10-15-integration-extended.md`.
- RimWorld-Client: 1.6-Release-Build und Paketprüfung bestanden; drei TCP-Client-Gruppen bestanden.
- Offen: erfolgreicher Game-Start, UI-Bedienung, zwei RimWorld-Prozesse und andere Spielversionen. `VALIDATION.md` dokumentiert nur die frühere Wissensbasis.

- Erneuter grafischer Lauf mit isolierter Modliste: Exit 124 nach 180 s; Fenster/OpenGL, aber kein belegter Mod-Ladeeintrag oder UI. `Version.txt` rev598, Runtime rev600; siehe `docs/verification/2026-10-15-rimworld-runtime-retry.md`.
