# Build-Umgebung

Lokales .NET-SDK `10.0.402`: `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet`. Protocol, Network und Lobby zielen auf `netstandard2.0`; Server und Test-Runner auf `net10.0`. Binäre Artefakte landen in ignorierten `bin/`, `obj/` und `dist/`.

```bash
DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-server.sh
/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Protocol.Tests/Protocol.Tests.csproj -c Release
/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Network.Tests/Network.Tests.csproj -c Release
/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Lobby.Tests/Lobby.Tests.csproj -c Release
/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Integration.Tests/Integration.Tests.csproj -c Release
```

RimWorld `1.6.4871 rev598` ist lokal installiert. Seine proprietären Assemblies werden nicht ins Git-Repository kopiert. Der Client-Mod-Build und Spielstart stehen noch aus.
