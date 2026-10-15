# Integrationsprüfung vor erstem GitHub-Upload – 2026-10-15

- **Branch/Stand:** `test/lobby-integration` bei `c61b47d` zu Beginn der Prüfung; `origin/main` bei `0909d11` nach `git fetch origin`. Divergenz `0` Remote-Commits gegenüber `8` lokalen Commits. Arbeitsverzeichnis zuvor sauber.
- **Umgebung:** Linux, .NET-SDK `10.0.402`, lokal installierte RimWorld-Dateien `1.6.4871 rev598`. Kein RimWorld-Spielprozess in dieser Prüfung.
- **Dokumentation:** `python3 scripts/validate_project_state.py` → Exit 0, 22 Pflichtdateien/9 Aufgaben; `python3 scripts/verify_knowledge_archive.py` → Exit 0, 49 Archiveinträge; `python3 scripts/validate_structure.py` → Exit 0, fünf XML-Dateien syntaktisch gültig.
- **Protokoll:** `dotnet run --project tests/Protocol.Tests/Protocol.Tests.csproj -c Release --nologo` → Exit 0, fünf Gruppen.
- **Netzwerk:** `dotnet run --project tests/Network.Tests/Network.Tests.csproj -c Release --nologo` → Exit 0, drei Gruppen.
- **Lobby:** `dotnet run --project tests/Lobby.Tests/Lobby.Tests.csproj -c Release --nologo` → Exit 0, Regel-/Payload-Gruppe.
- **Client:** `dotnet run --project tests/Client.Tests/Client.Tests.csproj -c Release --nologo` → Exit 0, drei Gruppen.
- **Integration:** `dotnet run --project tests/Integration.Tests/Integration.Tests.csproj -c Release --nologo` → Exit 0, 13 Gruppen mit echten TCP-Verbindungen.
- **Release-Builds:** `env DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-server.sh` → Exit 0; derselbe Aufruf mit `scripts/build-mod.sh` → Exit 0, Mod-Build 0 Warnungen/Fehler.
- **Grenze:** `dist/` ist generiert und ignoriert; das Spiel-UI wurde noch nicht erfolgreich geladen. Ein Build und TCP-Test sind keine RimWorld-Laufzeitabnahme. Keine 1.7-, Mod-Kompatibilitäts- oder Internet-Sicherheitszusage.

- **Git-Integration und Remote-Nachweis:** `git merge --ff-only test/lobby-integration` → Exit 0; `git push origin main` mit authentifiziertem GitHub-Konto → Exit 0, Remote `0909d11..3a5e299`; `git fetch origin` → Exit 0; `git status --short --branch` → `## main...origin/main`, keine Änderungen. Keine History-Umschreibung.
