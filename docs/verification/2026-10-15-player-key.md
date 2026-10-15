# Persistente Spielerkennung (ID-001) – Prüfung 2026-10-15

- **Branch/Basis:** `feat/player-key` von `feat/chunk-transfer` @ `c7ed45a` (abhängig, ungemergt), Worktree `/mnt/Arbeit/Git Projekte/RimworldMP-claude`.
- **Umgebung:** Linux, .NET SDK 10.0.402 / Runtime 10.0.13, lokale RimWorld-Assemblies `1.6.4871` (rev598), Steamworks.NET aus `RimWorldLinux_Data/Managed`.
- **API-Prüfung per Reflection** (lokale Assemblies, kein dekompilierter Code):
  - `Verse.Mod.WriteSettings()`, `Verse.Mod.GetSettings<T>()`, `Verse.ModSettings.Write()`
  - `Steamworks.SteamUser.GetSteamID()`, `CSteamID.IsValid()`, `CSteamID`⇄`UInt64`
  - `SteamNetworkingIdentity.GetSteamID()`, `SteamNetworkingSockets.CloseConnection(HSteamNetConnection, Int32, String, Boolean)`

## Tatsächlich ausgeführte Prüfungen

| Befehl/Prüfung | Ergebnis |
|---|---|
| `DOTNET_BIN=…/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen, 0 Fehler |
| `DOTNET_BIN=…/dotnet bash scripts/build-server.sh` | Exit 0 |
| `Protocol.Tests` | Exit 0; acht Gruppen, neu: PlayerKey-Format, Hello mit Kennung, ungültige Arten und SteamID 0 |
| `Lobby.Tests` | Exit 0; neu: Kennung eindeutig, im Lobbyzustand enthalten, nach dem Entfernen wieder frei |
| `Hosting.Tests` | Exit 0; 14 Gruppen, neu siehe unten |
| `Network.Tests`, `Integration.Tests`, `Client.Tests`, `ProcessIntegration.Tests` | Exit 0; elf, 18, drei, acht unverändert |
| `python3 scripts/validate_project_state.py` | Exit 0 |

Neu in `Hosting.Tests`, über `ListenHost` und eine Steam-Nachbildung:
- Die Host-Kennung erscheint im Lobbyzustand.
- Eine von „Steam“ verifizierte Kennung wird übernommen und ist für den Host sichtbar.
- Ein Gast, der als SteamID A verifiziert ist und SteamID B behauptet, wird mit Begründung abgelehnt.
- Ein zweiter Verbindungsversuch mit derselben Kennung wird abgelehnt.
- Nach dem Trennen ist die Kennung binnen 1,5 s wieder frei; vorher dauerte das bis zu 5 s.

## Klassifizierung und Grenzen

`BUILD_ERFOLGREICH`, automatisiert getestet. Kennung über einen Spielneustart, echtes Steam und echte Steam-Ablehnung mit Linger: `NICHT_GETESTET`, Spieltests sind laut Benutzer derzeit nicht möglich. `LAUFZEIT NICHT VERIFIZIERT`.
