# Merge Menü/Chat und Internet-Erreichbarkeit in main – Prüfung 2026-10-15

- **Freigabe:** Ausdrückliche Anweisung des Benutzers am 2026-10-15, aktive Branches zu prüfen, `fix/internet-connectivity` bei weiterer Relevanz zu mergen und alles nach `main` zu bringen (Regel 32).
- **Branches:** `feat/main-menu-chat` (`d632a5c`) per Fast-Forward auf `origin/main` (`dfe9d23`); danach `origin/fix/internet-connectivity` (`bcb7eef`, Basis `5b533cd`) per Merge-Commit. Alle übrigen lokalen Branches waren bereits vollständig in `main` enthalten.
- **Relevanzprüfung:** Vor dem Merge banden `main` und `feat/main-menu-chat` Direkt-Host, Server-Beispielkonfiguration und Mod-Einstellungen weiterhin hart an `127.0.0.1`; `UpnpPortMapping.cs`, `NetworkAddresses.cs` und `HostEndpoint.cs` fehlten. Der Fix war damit nicht überholt.
- **Umgebung:** Linux, .NET SDK 10.0.402 / Runtime 10.0.13 (`/home/fabiok/.local/share/dotnet-rimworldmp`), lokale RimWorld-Assemblies `1.6.4871` (`Version.txt` rev598, Unity/Mono), `Lib.Harmony` 2.4.3.

## Konfliktauflösung

- `FoundationMod.cs`: Der Fix zweigte vor der Steam-Integration ab und änderte das alte Fensterlayout. Neues Layout (Steam-Abschnitt, IP-Abschnitt, Scrollansicht, Chat) behalten; Fix-Elemente portiert: `host:port`-Tooltip, Bind-Hinweis `::`, UPnP-Checkbox, Erreichbarkeitsinfos und **Adresse kopieren** in der Verbunden-Ansicht.
- Steam-Hosting bindet den eingebetteten Listener bewusst an `127.0.0.1`. `StartReachability` (UPnP, Adressliste) läuft deshalb nur beim IP-Hosting; Erreichbarkeitstext und Kopierknopf erscheinen nicht bei `steam.IsHosting`. Ohne diese Bedingung hätte jede Steam-Lobby „nur auf diesem PC erreichbar“ gemeldet.
- Beide Abbau-Pfade (Trennen, Verbindungsabbruch) rufen `ReleaseMapping()`; die UPnP-Freigabe wird geschlossen.
- `ListenHost.cs`: `AcceptsIPv4`/`AcceptsIPv6` (Fix) und `AttachConnection` (Steam) beide behalten.
- ADR-Nummernkollision: `0010-steam-home-host.md` und `0010-internet-reachability.md`. Letzterer wurde zu `0012-internet-reachability.md`; Verweise angepasst.
- Dokumentation (`CHANGELOG`, `ARCHITECTURE`, `CODEMAP`, `client.md`, `build.md`, `mod/INSTALLATION.md`, Projektstatus, Aufgaben, Übergabe) inhaltlich zusammengeführt.

## Tatsächlich ausgeführte Prüfungen

| Befehl/Prüfung | Ergebnis |
|---|---|
| `DOTNET_BIN=…/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen, 0 Fehler gegen lokale 1.6-Assemblies |
| `DOTNET_BIN=…/dotnet bash scripts/build-server.sh` | Exit 0 |
| `dotnet run --project tests/Protocol.Tests/… -c Release` | Exit 0; sechs Gruppen |
| `dotnet run --project tests/Network.Tests/… -c Release` | Exit 0; fünf Gruppen (drei aus `main`, zwei aus dem Fix) |
| `dotnet run --project tests/Lobby.Tests/… -c Release` | Exit 0 |
| `dotnet run --project tests/Integration.Tests/… -c Release` | Exit 0; 18 Gruppen inkl. Chat |
| `dotnet run --project tests/Hosting.Tests/… -c Release` | Exit 0; acht Gruppen (fünf aus `main`, drei aus dem Fix inkl. Dual-Stack und Fake-UPnP-Gateway) |
| `dotnet run --project tests/Client.Tests/… -c Release` | Exit 0; drei Gruppen |
| `dotnet run --project tests/ProcessIntegration.Tests/… -c Release` | Exit 0; acht Schritte |
| `python3 scripts/validate_project_state.py` | Exit 0; 22 Pflichtdateien, 19 Aufgaben, Status und Verweise |

Damit liefen die Fix-Tests erstmals unter .NET 10 auf Linux statt nur unter .NET 8.

## Klassifizierung und Grenzen

`BUILD_ERFOLGREICH`. Das zusammengeführte Fenster ist `NICHT_GETESTET` im Spiel. Echte UPnP-Freigabe, Gastverbindung aus fremdem Netz und Chat mit zwei RimWorld-Clients sind offen. Die ZIPs unter `releases/` stammen aus den Einzelbranches und entsprechen nicht dem gemergten Code. `LAUFZEIT NICHT VERIFIZIERT`.
