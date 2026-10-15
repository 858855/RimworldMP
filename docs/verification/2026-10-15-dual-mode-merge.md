# Verifikation des freigegebenen Merges, 2026-10-15

- **Umgebung:** Linux; lokales .NET-SDK `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet`; lokale RimWorld-1.6-Assemblies.
- **Ausgang:** `origin/main` bei `9d96665`, `release/mod-install-0.1-dev` bei `2b78155`; Arbeitsverzeichnis sauber, `origin/main` Vorfahr des Release-Branches.
- **Dokumentation:** `python3 scripts/validate_project_state.py` → Exit 0, 22 Pflichtdateien und 15 Aufgaben geprüft.
- **Direkt-Hosting:** `dotnet run --project tests/Hosting.Tests/Hosting.Tests.csproj -c Release --nologo` → Exit 0, vier Testgruppen bestanden.
- **Server/Lobby:** `dotnet run --project tests/Integration.Tests/Integration.Tests.csproj -c Release --nologo` → Exit 0, 16 Testgruppen bestanden.
- **Prozessgrenzen:** `dotnet run --project tests/ProcessIntegration.Tests/ProcessIntegration.Tests.csproj -c Release --nologo` → Exit 0, acht Schritte bestanden.
- **Mod-Build:** `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` → Exit 0, null Warnungen und null Fehler. Der erste Skriptaufruf ohne `DOTNET_BIN` endete mit Exit 127, da `dotnet` nicht im Standard-PATH liegt; mit dem vorhandenen SDK erfolgreich wiederholt.
- **Git:** `git merge --no-ff release/mod-install-0.1-dev` auf `main` → Exit 0, Merge `e3905144a8fae86de82161226114f38990e62403`; `git push origin main` → Exit 0, `9d96665..e390514`.
- **Grenzen:** Kein tatsächlicher Spiel-UI-Test, kein LAN-Test auf zwei Rechnern und kein RimWorld-1.7-Test. Build- und TCP-Tests belegen diese Punkte nicht.
