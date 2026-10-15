# Build-Umgebung

Lokales .NET-SDK `10.0.402` liegt unter `/home/fabiok/.local/share/dotnet-rimworldmp` (ohne Systeminstallation). Der Protokollkern zielt auf `netstandard2.0`; der paketfreie Test-Runner auf `net10.0`.

```bash
/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Protocol.Tests/Protocol.Tests.csproj -c Release
```

Am 2026-10-15 bestanden fünf Testgruppen. Binäre Artefakte landen in ignorierten `bin/`/`obj/`-Verzeichnissen. RimWorld-Client-Code wurde noch nicht gebaut. Lokal installiert: RimWorld `1.6.4871 rev598`, Steam-Build `23969874`, `Assembly-CSharp.dll` unter `RimWorldLinux_Data/Managed`. Proprietäre Assemblies bleiben außerhalb von Git.
