# Lobby-Core-Prüfung – 2026-10-15

- **Branch:** `feat/dedicated-server`, Basis `feat/network-protocol` / `f14142c`.
- **Umgebung:** Linux x64, .NET-SDK `10.0.402`; Lobby/Protocol `netstandard2.0`, Runner `net10.0`.
- **Befehl:** `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Lobby.Tests/Lobby.Tests.csproj -c Release`
- **Ergebnis:** Exit 0. Create/List/Join, Kapazität, Mehrfachbeitritt, Host-Rechte, Ready, Host-Übergabe, Disconnect-Bereinigung und Listen-/Snapshot-Roundtrip bestanden.
- **Grenze:** Direkter Bibliothekstest; kein TCP-Server, kein RimWorld-Client und keine persistente Lobby.
