# Sitzungsübergabe

- **Branch/Commit:** `feat/dedicated-server`, Basis `feat/network-protocol` bei `f14142c`; dieser hängt von `docs/project-governance`/`7e3a9ae` ab. `git rev-parse HEAD` liefert den aktuellen Commit. Kein Merge/Push.
- **Letzte Arbeit:** Lobby-Domänenkern und typisierte Listen-/Snapshot-Payloads. `LOBBY-001` teilweise; `SERVER-001` beginnt.
- **Belegt:** `dotnet run --project tests/Lobby.Tests/Lobby.Tests.csproj -c Release` bestanden. Details: `docs/verification/2026-10-15-lobby-core.md`. Protokoll-/Netzwerktests stehen in ihren früheren Nachweisen.
- **Offen:** TCP-Server, Heartbeat, echte Mehrclient-Lobby, RimWorld-UI. Maßgeblich sind `PROJECT_STATE.yaml` und `TASKS.yaml`.
- **Umgebung:** .NET-SDK `10.0.402`, RimWorld `1.6.4871 rev598` (Steam `23969874`); kein Game-Test.
- **Nächster Schritt:** `ServerHost` und Konfiguration implementieren, LobbyRegistry anbinden, mit zwei unabhängigen TCP-Clients testen.
