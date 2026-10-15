# Build-Umgebung

Lokales .NET-SDK `10.0.402`: `/home/fabiok/.local/share/dotnet-rimworldmp/dotnet`. Protocol, Network und Lobby zielen auf `netstandard2.0`; gemeinsamer Hosting-Kern und RimWorld-Adapter auf `netstandard2.1`; Server und Test-Runner auf `net10.0`. Binäre Artefakte landen in ignorierten `bin/`, `obj/` und `dist/`.

```bash
DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-server.sh
/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Protocol.Tests/Protocol.Tests.csproj -c Release
/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Network.Tests/Network.Tests.csproj -c Release
/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Lobby.Tests/Lobby.Tests.csproj -c Release
/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Integration.Tests/Integration.Tests.csproj -c Release
```

RimWorld `1.6.4871` ist lokal installiert (`Version.txt`: rev598; Spielprozess: rev600). Seine proprietären Assemblies werden nicht ins Git-Repository kopiert. Der Client-Mod baut mit `netstandard2.1`; der Spielstart bleibt offen.

```bash
DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh
/home/fabiok/.local/share/dotnet-rimworldmp/dotnet run --project tests/Client.Tests/Client.Tests.csproj -c Release
```

Für andere Installationen `RIMWORLD_MANAGED` setzen. Das Paket liegt danach unter `dist/RimWorldMultiplayerFoundation/`.

Die lokal in `main` integrierte Steam-Vorschau referenziert zusätzlich die Steamworks.NET-Assembly aus dem installierten RimWorld-1.6-`Managed`-Verzeichnis. Sie wird nur zum Kompilieren verwendet und nicht in das Mod-Paket kopiert. Ein Build belegt keine Steam-Laufzeitfunktion; siehe `docs/verification/2026-10-15-steam-transport.md`.

## Installierbares Mod-ZIP (RimWorld 1.6)

```bash
DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh
python3 scripts/package-mod.py
```

Das zweite Kommando prüft `About.xml`, die fünf eigenen Assemblies und den Archivinhalt und erzeugt `releases/RimWorldMultiplayerFoundation-0.1.0-dev-rw1.6.zip`. Die [Installationsanleitung](../mod/INSTALLATION.md) liegt auch im ZIP. Entpacken direkt in `RimWorld/Mods/`; der Archivordner ist bereits `RimWorldMultiplayerFoundation/`. Das ZIP enthält keinen Server. Laufzeit und UI im Spiel sind weiterhin nicht verifiziert.

Für einen gesondert benannten Teststand kann `python3 scripts/package-mod.py RimWorldMultiplayerFoundation-0.1.0-dev-steam-preview-rw1.6.zip` verwendet werden. Dadurch wird das reguläre Release-ZIP nicht überschrieben. Die Steam-Vorschau ist erst nach einem erfolgreichen Mod-Build sinnvoll; zwei Steam-Konten müssen sie noch im Spiel prüfen.
