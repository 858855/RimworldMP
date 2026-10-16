# Gemeinsames Mod-Paket für spätere manuelle Spieltests

- **Datum:** 2026-10-16; Linux; lokaler RimWorld-Build `1.6.4871 rev598`.
- **Branch:** `codex/manual-test-candidate` von `codex/command-pipeline` @ `f91655a`, mit X1 (`2068ea3`) und X5 (`017f24e`) als Cherry-Picks; Codezustand `f04cb14`.
- **Quellumfang:** X1 statischer Tickpfad-Audit, X4 Draft/Forbid/Goto-Befehle (WIP), X5 Modus- und IP-Identitäts-UI. `main` blieb auf `b4255c6`.
- **Artefakt:** `releases/RimWorldMultiplayerFoundation-0.2.0-preview-f04cb14-rw1.6.zip`, 1.025.895 Byte, 10 Dateien. SHA-256 `34d73bc0b24718b5658e750115f82cc2206aa286e48254108beebaa9933e290c`.

| Prüfung | Befehl | Ergebnis |
|---|---|---|
| Mod-Release-Build | `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Exit 0, 0 Warnungen/Fehler. |
| ZIP | `python3 scripts/package-mod.py RimWorldMultiplayerFoundation-0.2.0-preview-f04cb14-rw1.6.zip` | Exit 0; Inhalt und Bytes vom Skript geprüft. |
| Prüfsumme | `sha256sum releases/RimWorldMultiplayerFoundation-0.2.0-preview-f04cb14-rw1.6.zip` | Exit 0; stimmt mit Paketmeldung überein. |
| Inhalt | `unzip -Z -1 releases/RimWorldMultiplayerFoundation-0.2.0-preview-f04cb14-rw1.6.zip` | Exit 0; Ordner `RimWorldMultiplayerFoundation/`, `About.xml`, sieben DLLs, Lizenz und Installation. |
| Session-Simulationen | `dotnet run --project tests/Session.Tests/Session.Tests.csproj --no-restore` | Exit 0; ausgegebene Lockstep-/Save-/Join-/Resync-/Vote-Szenarien PASS. Beim parallelen Projektbuild trat eine temporäre MSB3026-Dateikopierwarnung auf; sie verhinderte die Testausführung nicht. |
| Client-Pumpe | `dotnet run --project tests/Client.Tests/Client.Tests.csproj --no-restore` | Exit 0; drei Szenarien PASS. |
| Projektdokumentation | `python3 scripts/validate_project_state.py` | Exit 0; 22 Pflichtdateien, 61 Aufgaben (vor PKG-002), Status und Verweise gültig. |

Nach Eintrag von `PKG-002` erneut `python3 scripts/validate_project_state.py`: Exit 0, jetzt 62 Aufgaben.

**Nicht ausgeführt:** RimWorld-Start, zwei lokale Spiele, Fabio/Sven über zwei Rechner, Steam-/IP-UI, Save/Load, Gameplay-Gleichlauf. Der Benutzer möchte diese Tests später selbst mit Sven durchführen. X4 unterstützt nur drei Aktionen; weitere geordnete Jobs und Designatoren fehlen. Die parallele Pfadsuche aus X1 ist ein ungeprüftes Desync-Risiko. Das ZIP ist daher eine **Vorschau zum späteren Test**, kein freigegebenes Multiplayer-Release.

**Installation später:** ZIP in `RimWorld/Mods/` **entpacken**, sodass dort `RimWorldMultiplayerFoundation/About/About.xml` liegt; auf beiden Rechnern denselben ZIP-Hash verwenden. Bestehende Mod-Verzeichnisse vor dem Ersetzen sichern. Keine Installation in dieser Arbeitseinheit.
