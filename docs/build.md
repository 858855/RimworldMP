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

Für andere Installationen `RIMWORLD_MANAGED` setzen, unter Windows z. B. `RIMWORLD_MANAGED=E:/SteamLibrary/steamapps/common/RimWorld/RimWorldWin64_Data/Managed DOTNET_BIN=dotnet bash scripts/build-mod.sh` in Git Bash. Das Paket liegt danach unter `dist/RimWorldMultiplayerFoundation/`. Die Menü-/Chat-Vorschau referenziert `Lib.Harmony` 2.4.3; der Paketbau kopiert dessen `net472`-Assembly und MIT-Lizenz aus dem lokalen NuGet-Cache. RimWorld-Assemblies und Steamworks.NET werden nicht mitgeliefert.

Die lokal in `main` integrierte Steam-Vorschau referenziert zusätzlich die Steamworks.NET-Assembly aus dem installierten RimWorld-1.6-`Managed`-Verzeichnis. Sie wird nur zum Kompilieren verwendet und nicht in das Mod-Paket kopiert. Ein Build belegt keine Steam-Laufzeitfunktion; siehe `docs/verification/2026-10-15-steam-transport.md`.

## Installierbares Mod-ZIP (RimWorld 1.6)

```bash
DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh
python3 scripts/package-mod.py
```

Das zweite Kommando prüft `About.xml`, die fünf eigenen Assemblies, `0Harmony.dll`, Lizenz und Archivinhalt und erzeugt `releases/RimWorldMultiplayerFoundation-0.1.0-dev-menu-chat-preview-rw1.6.zip` mit neun Einträgen. Die [Installationsanleitung](../mod/INSTALLATION.md) liegt auch im ZIP. Entpacken direkt in `RimWorld/Mods/`; der Archivordner ist bereits `RimWorldMultiplayerFoundation/`. Das ZIP enthält keinen Server. Menüposition und Fensteröffnung wurden vom Benutzer im Spiel bestätigt; Steam-/IP-Bedienung und Chat bleiben zu prüfen.

Für einen anders benannten Teststand kann der ZIP-Name als Argument übergeben werden. Beide Spieler und ein optionaler Dedicated Server müssen aus demselben Branch/Wire-Version 2 stammen; das ältere Steam-Vorschau-ZIP verwendet Wire-Version 1.

## Windows ohne .NET 10 (Stand 2026-10-17)

Auf Svens Rechner ist nur das .NET-SDK 8.0.403 installiert. Der Mod-Build läuft dort mit `RIMWORLD_MANAGED=E:/SteamLibrary/steamapps/common/RimWorld/RimWorldWin64_Data/Managed DOTNET_BIN=dotnet bash scripts/build-mod.sh` (Git Bash). Die Testprojekte und der Server zielen auf `net10.0` und bauen mit SDK 8 nicht (`NETSDK1045`; `-p:TargetFramework=net8.0` greift nicht). Die Tests liefen deshalb in einem lokalen, nicht eingecheckten .NET-8-Harness: je Testprojekt eine `net8.0`-Konsole, die genau die Repo-Testquellen kompiliert und dieselben Bibliotheksprojekte referenziert; der Server als `net8.0`-Harness mit demselben Assembly-Namen, damit `ProcessIntegration.Tests` ihn startet. Unter .NET 10 sind diese Läufe nicht wiederholt.
