# Direkt-Hosting und Dedicated Server, 15.10.2026

- Branch: `release/mod-install-0.1-dev`, Basis `fix/server-heartbeat` bei `1192eb5`; Paket-Commits `e16ac3b` (Verschiebung) und `a8aadae428446f08648d957582deb1c41f8e244b` (Funktion/ZIP) auf GitHub gepusht; kein Merge.
- Umgebung: Linux, .NET SDK 10.0.402, lokale RimWorld-1.6-Assemblies (`Version.txt` 1.6.4871 rev598; frühere Runtime rev600). `Hosting` und RimWorld-Adapter zielen auf `netstandard2.1`.
- Architektur: `FoundationServer` in `Multiplayer.Foundation.Hosting` verschoben; Dedicated Server und eingebetteter Listen-Host referenzieren denselben Kern. Keine Änderung am Wire-Protokoll.
- `dotnet run --project tests/Hosting.Tests/Hosting.Tests.csproj -c Release --nologo`: Exit 0, vier PASS-Gruppen. Integrierter Host erstellt Lobby ohne Serverprozess, Gast joint über TCP, Ready wird auf beiden Clients sichtbar, Host-Stopp trennt Gast und gibt Port frei.
- `dotnet run --project tests/Integration.Tests/Integration.Tests.csproj -c Release --nologo`: Exit 0, 16 PASS-Gruppen für dedizierten Server.
- `dotnet run --project tests/ProcessIntegration.Tests/ProcessIntegration.Tests.csproj -c Release --nologo`: Exit 0, acht PASS-Schritte mit separatem Server und zwei Client-Prozessen.
- `dotnet run --project tests/Client.Tests/Client.Tests.csproj -c Release --nologo`: Exit 0, drei PASS-Gruppen für Client-Pumpe/Heartbeat/Disconnect.
- `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh`: Exit 0, 0 Warnungen/Fehler. `python3 scripts/package-mod.py`: Exit 0, sieben ZIP-Dateien, 42.985 Bytes. SHA-256: `c3be56469212afc0690c99c498252d0cf37ddaf32a43ff476cd57490b0112ea8`.
- Extraktionstest mit `zipfile`: Exit 0; `RimWorld/Mods/RimWorldMultiplayerFoundation/About/About.xml`, fünf eigene DLLs und Anleitung vorhanden.
- GitHub-Download: `https://raw.githubusercontent.com/858855/RimworldMP/a8aadae428446f08648d957582deb1c41f8e244b/releases/RimWorldMultiplayerFoundation-0.1.0-dev-rw1.6.zip`. `curl --fail --location` ergab HTTP 200 und 42.985 Bytes. SHA-256 der heruntergeladenen Datei entspricht dem lokalen ZIP: `c3be56469212afc0690c99c498252d0cf37ddaf32a43ff476cd57490b0112ea8`.
- Dokumentationsvalidator: `python3 scripts/validate_project_state.py`, Exit 0, 22 Pflichtdateien und 15 Aufgaben.
- Nicht geprüft: UI im laufenden RimWorld, zwei Spielclients, LAN über zwei Rechner, 1.7. Keine Authentifizierung/TLS; nur kontrolliertes Testnetz.
