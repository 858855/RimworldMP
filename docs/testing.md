# Prüfstrategie und Ist-Stand

Nach Änderungstyp testen. Ausgeführte Nachweise liegen unter `docs/verification/`.

**Menü-/Chat-Vorschau auf `feat/main-menu-chat`:** Wire-Version 2; sechs Protokollgruppen, 18 Serverintegrationsgruppen, fünf Hosting-, drei Client-, drei Netzwerk- und acht Mehrprozess-Schritte bestanden. RimWorld-Mod und Dedicated Server gebaut. Neun ZIP-Einträge mit Lizenz geprüft und bytegleich installiert. Der Benutzer bestätigte den Hauptmenü-Knopf direkt unter Tutorial und die Fensteröffnung; Chat mit zwei RimWorld-Spielclients ist noch offen. Siehe `docs/verification/2026-10-15-menu-chat.md`.

- Archivmanifest: 49 Einträge geprüft; drei XML-Beispiele syntaktisch geprüft.
- Governance-Validator bestanden.
- Protokoll: fünf Gruppen bestanden. Netzwerk: drei reale TCP-Loopback-Gruppen bestanden. Lobby-Core-Regeltest bestanden.
- Dedicated Server: Release-Build und 13 Integrationstestgruppen mit zwei echten TCP-Clients bestanden; siehe `2026-10-15-dedicated-server.md` und `2026-10-15-integration-extended.md`.
- RimWorld-Client: 1.6-Release-Build und Paketprüfung bestanden; drei TCP-Client-Gruppen bestanden.
- Offen: erfolgreicher Game-Start, UI-Bedienung, zwei RimWorld-Prozesse und andere Spielversionen. `VALIDATION.md` dokumentiert nur die frühere Wissensbasis.

- Erneuter grafischer Lauf mit isolierter Modliste: Exit 124 nach 180 s; Fenster/OpenGL, aber kein belegter Mod-Ladeeintrag oder UI. `Version.txt` rev598, Runtime rev600; siehe `docs/verification/2026-10-15-rimworld-runtime-retry.md`.

- Mehrprozess-Test: `dotnet run --project tests/ProcessIntegration.Tests/ProcessIntegration.Tests.csproj -c Release --nologo`; eigenständiger Server und zwei Client-Prozesse. Sieben Testschritte zu Verbindung, Lobby-Liste, Join, identischen Snapshots, Ready und Disconnect bestanden. Spiel-UI bleibt offen.
- Serverstabilität: Integrationstests prüfen verzögertes Pong, doppelte und ungesendete Nonce. Der Mehrprozess-Test prüft SIGTERM bei aktiver Client-Verbindung. 16 Integrationstestgruppen und acht Mehrprozess-Schritte bestanden; siehe `docs/verification/2026-10-15-server-heartbeat-shutdown.md`.

- Direkt-Hosting: `dotnet run --project tests/Hosting.Tests/Hosting.Tests.csproj -c Release --nologo`; vier Gruppen für integrierten Host, Gast, Ready und Stopp bestanden. Server-Integration (16 Gruppen), Mehrprozess-Test (acht Schritte) und Client-Test (drei Gruppen) nach der Auslagerung erneut bestanden. Der Mod-Build gegen lokale RimWorld-1.6-Assemblies gelang ohne Warnungen; Spiel-UI bleibt offen.
